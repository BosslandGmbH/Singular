using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using System.Drawing;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Common
    {
        internal const uint Ghoul = 26125;

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DeathKnightSettings Settings { get { return SingularSettings.Instance.DeathKnight(); } }

        public static bool HasTalent(DeathKnightTalents tal)
        {
            return TalentManager.IsSelected((int)tal);
        }

        internal static int ActiveRuneCount
        {
            get
            {
                return Me.BloodRuneCount + Me.FrostRuneCount + Me.UnholyRuneCount +
                       Me.DeathRuneCount;
            }
        }

        internal static bool GhoulMinionIsActive
        {
            get { return Me.Minions.Any(u => u.Entry == Ghoul); }
        }

        internal static void DestroyGhoulMinion()
        {
            Lua.DoString("DestroyTotem(1)");
        }

        internal static bool ShouldSpreadDiseases
        {
            get
            {
                int radius = TalentManager.HasGlyph("Pestilence") ? 15 : 10;

                return !Me.CurrentTarget.HasAuraExpired("Blood Plague") 
                    && !Me.CurrentTarget.HasAuraExpired("Frost Fever") 
                    && Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < radius && u.HasAuraExpired( "Blood Plague") && u.HasAuraExpired("Frost Fever"));
            }
        }

        internal static int BloodRuneSlotsActive { get { return Me.GetRuneCount(0) + Me.GetRuneCount(1); } }
        internal static int FrostRuneSlotsActive { get { return Me.GetRuneCount(2) + Me.GetRuneCount(3); } }
        internal static int UnholyRuneSlotsActive { get { return Me.GetRuneCount(4) + Me.GetRuneCount(5); } }
        internal static int DeathRuneSlotsActive { get { return Me.GetRuneCount(RuneType.Death); } }

        /// <summary>
        /// check that we are in the last tick of Frost Fever or Blood Plague on current target and have a fully depleted rune
        /// </summary>
        internal static bool CanCastPlagueLeech
        {
            get
            {
                // check talent only to avoid some unnecessary LUA if not needed
                if (!HasTalent(DeathKnightTalents.PlagueLeech) || !Me.GotTarget)
                    return false;

                WoWAura auraFrostFever = Me.GetAllAuras().Where(a => a.Name == "Frost Fever" && a.TimeLeft.TotalMilliseconds > 250).FirstOrDefault();
                WoWAura auraBloodPlague = Me.GetAllAuras().Where(a => a.Name == "Blood Plague" && a.TimeLeft.TotalMilliseconds > 250).FirstOrDefault();
                if (auraFrostFever == null || auraBloodPlague == null)
                    return false;

                bool depletedBlood, depletedFrost, depletedUnholy;

                // Check Runes per http://wow.joystiq.com/2013/06/25/lichborne-patch-5-4-patch-note-analysis-for-death-knights/#continued
                if (Me.Specialization == WoWSpec.DeathKnightUnholy)
                {
                    depletedBlood = BloodRuneSlotsActive == 0;
                    depletedFrost = FrostRuneSlotsActive == 0;
                    return (depletedFrost && depletedBlood);
                }

                depletedFrost = FrostRuneSlotsActive == 0;
                depletedUnholy = UnholyRuneSlotsActive == 0;

                return (depletedFrost && depletedUnholy);
            }
        }

        #region Pull

        // All DKs should be throwing death grip when not in intances. It just speeds things up, and makes a mess for PVP :)
        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, (WoWSpec)int.MaxValue, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightNormalAndPvPPull()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),
                        CreateDarkSuccorBehavior(),
                        Common.CreateGetOverHereBehavior(),
                        Spell.Cast("Outbreak"),
                        Spell.Cast("Howling Blast"),
                        Spell.Cast("Icy Touch")
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        // Non-blood DKs shouldn't be using Death Grip in instances. Only tanks should!
        // You also shouldn't be a blood DK if you're DPSing. Thats just silly. (Like taking a prot war as DPS... you just don't do it)
        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Instances)]
        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Instances)]
        public static Composite CreateDeathKnightFrostAndUnholyInstancePull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),
                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(
                        Spell.Cast("Howling Blast"),
                        Spell.Cast("Icy Touch")
                        )
                    ),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region PreCombatBuffs

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightPreCombatBuffs()
        {
            return new PrioritySelector(
                CreateDeathKnightPresenceBehavior(),

                // limit PoF to once every ten seconds in case there is some
                // .. oddness here
                new Throttle(10, Spell.BuffSelf("Path of Frost", ret => Settings.UsePathOfFrost)),

                // Bone Shield has 1 min cd and 5 min duration, so cast out of combat if possible
                Spell.BuffSelf( "Bone Shield", req => Me.IsInInstance || Battlegrounds.IsInsideBattleground)
                );
        }

        #endregion

        #region Pull Buffs

        [Behavior(BehaviorType.PullBuffs, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightPullBuffs()
        {
            return new PrioritySelector(
                CreateDeathKnightPresenceBehavior(),
                Spell.BuffSelf("Horn of Winter", ret => !Me.HasPartyBuff(PartyBuffType.AttackPower))
                );
        }

        #endregion

        #region Loss of Control 

        [Behavior(BehaviorType.LossOfControl, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.BuffSelf("Icebound Fortitude", ret => Me.HealthPercent < Settings.IceboundFortitudePercent)
                    )
                );
        }

        #endregion 

        #region Heal

        [Behavior(BehaviorType.Heal, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightHeals()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    Spell.Cast("Death Coil", on => Me, ret => Me.HasAura("Lichborne") && Me.HealthPercent < Settings.LichbornePercent),

                    Spell.BuffSelf("Death Pact",
                        ret => Common.HasTalent( DeathKnightTalents.DeathPact) 
                            && Me.HealthPercent < Settings.DeathPactPercent 
                            && (Me.GotAlivePet || GhoulMinionIsActive)),

                    Spell.Cast("Death Siphon",
                        ret => Common.HasTalent( DeathKnightTalents.DeathSiphon) 
                            && Me.GotTarget && Me.CurrentTarget.InLineOfSpellSight && Me.IsSafelyFacing(Me.CurrentTarget)
                            && Me.HealthPercent < Settings.DeathSiphonPercent),

                    Spell.BuffSelf("Conversion",
                        ret => Common.HasTalent( DeathKnightTalents.Conversion) 
                            && Me.HealthPercent < Settings.ConversionPercent 
                            && Me.RunicPowerPercent >= Settings.MinimumConversionRunicPowerPrecent),

                    Spell.BuffSelf("Rune Tap",
                        ret => Me.HealthPercent < Settings.RuneTapPercent 
                            || Me.HealthPercent < 90 && Me.HasAura("Will of the Necropolis")),

                    // following for DPS only -- let Blood fall through in instances
                    Spell.Cast("Death Strike",
                        ret => (Me.Specialization != WoWSpec.DeathKnightBlood || SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                            && Me.GotTarget && Me.CurrentTarget.InLineOfSpellSight && Me.IsSafelyFacing(Me.CurrentTarget)
                            && Me.HealthPercent < Settings.DeathStrikeEmergencyPercent),

                    // use it to heal with deathcoils.
                    Spell.BuffSelf("Lichborne",
                        ret => Me.HealthPercent < Settings.LichbornePercent 
                            && Me.CurrentRunicPower >= 60
                            && (!Settings.LichborneExclusive || !Me.HasAnyAura( "Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Icebound Fortitude"))),

                    // Frost or Blood may need to summon pet for Death Pact
                    new Decorator(
                        ret => TalentManager.CurrentSpec != WoWSpec.DeathKnightUnholy && !GhoulMinionIsActive,
                        Spell.BuffSelf("Raise Dead",
                            ret => (Me.Specialization == WoWSpec.DeathKnightBlood && Me.HealthPercent < Settings.SummonGhoulPercentBlood)
                                || (Me.Specialization == WoWSpec.DeathKnightFrost && Me.HealthPercent < Settings.SummonGhoulPercentFrost)
                                || (Me.HealthPercent < Settings.DeathPactPercent && (!Settings.DeathPactExclusive || !Me.HasAnyAura("Bone Shield","Vampiric Blood","Dancing Rune Weapon","Lichborne","Icebound Fortitude")))
                            )
                        )
                    )
                );
        }

        #endregion

        #region CombatBuffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost)]
        public static Composite CreateDeathKnightCombatBuffs()
        {
            return new Decorator(
                req => !Me.GotTarget || !Me.CurrentTarget.IsTrivial(),
                new PrioritySelector(
                    // *** Defensive Cooldowns ***
                    // Anti-magic shell - no cost and doesnt trigger GCD 
                        Spell.BuffSelf("Anti-Magic Shell",
                                       ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                                             (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                                                             u.CurrentTargetGuid == Me.Guid)),
                    // we want to make sure our primary target is within melee range so we don't run outside of anti-magic zone.
                        Spell.CastOnGround("Anti-Magic Zone", ctx => Me.Location,
                                           ret => Common.HasTalent( DeathKnightTalents.AntiMagicZone) &&
                                                  !Me.HasAura("Anti-Magic Shell") &&
                                                  Unit.NearbyUnfriendlyUnits.Any(u =>
                                                                                 (u.IsCasting ||
                                                                                  u.ChanneledCastingSpellId != 0) &&
                                                                                 u.CurrentTargetGuid == Me.Guid) &&
                                                  Targeting.Instance.FirstUnit != null &&
                                                  Targeting.Instance.FirstUnit.IsWithinMeleeRange),

                        Spell.BuffSelf("Icebound Fortitude", ret => Me.HealthPercent < Settings.IceboundFortitudePercent),

                        Spell.BuffSelf("Lichborne", ret => Me.IsCrowdControlled()),

                        Spell.BuffSelf("Desecrated Ground", ret => Common.HasTalent( DeathKnightTalents.DesecratedGround) && Me.IsCrowdControlled()),
                    
                        Helpers.Common.CreateCombatRezBehavior("Raise Ally", on => ((WoWUnit)on).SpellDistance() < 40 && ((WoWUnit)on).InLineOfSpellSight),

                        // *** Offensive Cooldowns ***

                        // I'm unholy and I don't have a pet or I am blood/frost and I am using pet as dps bonus
                        Spell.BuffSelf("Raise Dead",
                            ret => (TalentManager.CurrentSpec == WoWSpec.DeathKnightUnholy && !Me.GotAlivePet) 
                                || (TalentManager.CurrentSpec == WoWSpec.DeathKnightFrost && Settings.UseGhoulAsDpsCdFrost && !GhoulMinionIsActive)),

                        // never use army of the dead in instances if not blood specced unless you have the army of the dead glyph to take away the taunting
                        Spell.BuffSelf("Army of the Dead", 
                            ret => Settings.UseArmyOfTheDead 
                                && Helpers.Common.UseLongCoolDownAbility
                                && (SingularRoutine.CurrentWoWContext != WoWContext.Instances || TalentManager.HasGlyph("Army of the Dead"))),

                        Spell.BuffSelf("Empower Rune Weapon",
                                ret => Helpers.Common.UseLongCoolDownAbility && Me.RunicPowerPercent < 70 && ActiveRuneCount == 0),

                        Spell.BuffSelf("Death's Advance",
                            ret => Common.HasTalent( DeathKnightTalents.DeathsAdvance) 
                                && Me.GotTarget 
                                && (!Spell.CanCastHack("Death Grip") || SingularRoutine.CurrentWoWContext == WoWContext.Instances) 
                                && Me.CurrentTarget.DistanceSqr > 10 * 10),

                        Spell.BuffSelf("Blood Tap",
                            ret => Me.HasAura( "Blood Charge", 5) 
                                && (BloodRuneSlotsActive == 0 || FrostRuneSlotsActive == 0 || UnholyRuneSlotsActive == 0)),

                        Spell.Cast("Plague Leech", ret => CanCastPlagueLeech)
                    )
                );
        }

        #endregion

        #region Presence

        /// <summary>
        /// presence doesn't change change, so only need to call in PreCombatBuffs and PullBuffs. 
        /// </summary>
        /// <returns></returns>
        public static Composite CreateDeathKnightPresenceBehavior()
        {
            return Spell.BuffSelf(sp => SelectedPresence.ToString() + " Presence", req => SelectedPresence != DeathKnightPresence.None);
        }

        /// <summary>
        /// returns the users selected Presence after validating.  return is guarranteed
        /// to be valid for casting if != .None
        /// </summary>
        public static DeathKnightPresence SelectedPresence
        {
            get
            {
                var Presence = Settings.Presence;
                if ( Presence == DeathKnightPresence.None)
                    return Presence;

                if (Presence == DeathKnightPresence.Auto)
                {
                    switch (Me.Specialization)
                    {
                        case WoWSpec.DeathKnightBlood:
                            Presence =  DeathKnightPresence.Blood;
                            break;
                        default:
                        case WoWSpec.DeathKnightFrost:
                            Presence = DeathKnightPresence.Frost;
                            break;
                        case WoWSpec.DeathKnightUnholy:
                            Presence = DeathKnightPresence.Unholy ;
                            break;
                    }
                }

                if (!SpellManager.HasSpell(Presence.ToString() + " Presence"))
                {
                    Presence = DeathKnightPresence.Frost;
                    if (!SpellManager.HasSpell(Presence.ToString() + " Presence"))
                    {
                        Presence = DeathKnightPresence.None;
                    }
                }

                return Presence;
            }
        }

        #endregion 

        #region Death Grip

        public static Composite CreateGetOverHereBehavior()
        {
            return new Throttle( 1,
                new PrioritySelector(
                    CreateDeathGripBehavior(),
                    new Decorator(
                        ret => (Me.Combat || Me.CurrentTarget.Combat) && (Me.CurrentTarget.IsPlayer || (Me.CurrentTarget.IsMoving && !Me.CurrentTarget.IsWithinMeleeRange && Me.IsSafelyBehind(Me.CurrentTarget))),
                        CreateChainsOfIceBehavior()
                        )
                    )
                );
        }

        public static Composite CreateDeathGripBehavior()
        {
            return new Sequence(
                Spell.Cast("Death Grip", 
                    ret => !MovementManager.IsMovementDisabled 
                        && !Me.CurrentTarget.IsBoss() 
                        && Me.CurrentTarget.DistanceSqr > 10 * 10 
                        && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.TaggedByMe || (!Me.CurrentTarget.TaggedByOther && Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull && SingularRoutine.CurrentWoWContext != WoWContext.Instances))
                    ),
                new DecoratorContinue( ret => Me.IsMoving, new Action(ret => StopMoving.Now())),
                new WaitContinue( 1, until => Me.CurrentTarget.IsWithinMeleeRange, new ActionAlwaysSucceed())
                );
        }

        public static Composite CreateChainsOfIceBehavior()
        {
            return Spell.Buff("Chains of Ice", ret => Unit.CurrentTargetIsMovingAwayFromMe && !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost));
        }

        public static Composite CreateDarkSuccorBehavior()
        {
            if (Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Combat)
            {
                if (Common.SelectedPresence == DeathKnightPresence.Blood && TalentManager.HasGlyph("Dark Succor"))
                {
                    Logger.Write(Color.White, "User Error:  Glyph of Dark Succor does not proc with Blood Presence");
                }
            }

            // health below determined %
            // user wants to cast on cooldown without regard to health
            // we have aura AND (target is about to die OR aura expires in less than 3 secs)
            return new Decorator(
                ret => Me.HasAura("Dark Succor") 
                    && (Me.HealthPercent < 80 || Me.HasAuraExpired("Dark Succor", 3) || (Me.GotTarget && Me.CurrentTarget.TimeToDeath() < 6) 
                    && Me.CurrentTarget.InLineOfSpellSight 
                    && Me.IsSafelyFacing( Me.CurrentTarget)
                    && Spell.CanCastHack("Death Strike", Me.CurrentTarget)),
                new Sequence(
                    new Action( r => Logger.WriteDebug( Color.White, "Dark Succor ({0} ms left) influenced Death Strike coming....", (int) Me.GetAuraTimeLeft("Dark Succor").TotalMilliseconds  )),
                    Spell.Cast("Death Strike")
                    )
                );
        }

        public static Composite CreateApplyDiseases()
        {
            // throttle to avoid/reduce following an Outbreak with a Plague Strike for example
            return new Throttle(
                new PrioritySelector(
                // abilities that don't require Runes first
                    Spell.BuffSelf(
                        "Unholy Blight",
                        ret => Spell.CanCastHack("Unholy Blight")
                            && Unit.NearbyUnfriendlyUnits.Any(u => (u.IsPlayer || u.IsBoss()) && u.Distance < (u.MeleeDistance() + 5) && u.HasAuraExpired("Blood Plague"))),

                    Spell.Cast("Outbreak", ret => Me.CurrentTarget.HasAuraExpired("Frost Fever") || Me.CurrentTarget.HasAuraExpired("Blood Plague")),

                // now Rune based abilities
                    new Decorator(
                        ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.CurrentTarget.HasAuraExpired("Frost Fever"),
                        new PrioritySelector(
                            Spell.Cast("Howling Blast", ret => Spell.UseAOE && Me.Specialization == WoWSpec.DeathKnightFrost),
                            Spell.Cast("Icy Touch", ret => !Spell.UseAOE || Me.Specialization != WoWSpec.DeathKnightFrost)
                            )
                        ),

                    Spell.Cast("Plague Strike", ret => Me.CurrentTarget.HasAuraExpired("Blood Plague"))
                    )
                );
        }

        #endregion

    }

    #region Nested type: DeathKnightTalents

    public enum DeathKnightTalents
    {
        RollingBlood = 1,
        PlagueLeech,
        UnholyBlight,
        LichBorne,
        AntiMagicZone,
        Purgatory,
        DeathsAdvance,
        Chilblains,
        Asphyxiate,
        DeathPact,
        DeathSiphon,
        Conversion,
        BloodTap,
        RunicEmpowerment,
        RunicCorruption,
        GorefiendsGrasp,
        RemoreselessWinter,
        DesecratedGround
    }

    #endregion
}