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
                return StyxWoW.Me.BloodRuneCount + StyxWoW.Me.FrostRuneCount + StyxWoW.Me.UnholyRuneCount +
                       StyxWoW.Me.DeathRuneCount;
            }
        }

        internal static bool GhoulMinionIsActive
        {
            get { return StyxWoW.Me.Minions.Any(u => u.Entry == Ghoul); }
        }

        internal static bool UseLongCoolDownAbility
        {
            get
            {
                return (SingularRoutine.CurrentWoWContext == WoWContext.Instances && StyxWoW.Me.GotTarget &&
                        StyxWoW.Me.CurrentTarget.IsBoss()) ||
                       SingularRoutine.CurrentWoWContext != WoWContext.Instances;
            }
        }

        internal static bool ShouldSpreadDiseases
        {
            get
            {
                return Me.CurrentTarget.HasMyAura("Blood Plague") 
                    && Me.CurrentTarget.HasMyAura("Frost Fever") 
                    && Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 10 * 10 && u.MyAuraMissing( "Blood Plague") && u.MyAuraMissing("Frost Fever"));
            }
        }

        internal static int BloodRuneSlotsActive { get { return StyxWoW.Me.GetRuneCount(0) + StyxWoW.Me.GetRuneCount(1); } }
        internal static int FrostRuneSlotsActive { get { return StyxWoW.Me.GetRuneCount(2) + StyxWoW.Me.GetRuneCount(3); } }
        internal static int UnholyRuneSlotsActive { get { return StyxWoW.Me.GetRuneCount(4) + StyxWoW.Me.GetRuneCount(5); } }

        /// <summary>
        /// check that we are in the last tick of Frost Fever or Blood Plague on current target and have a fully depleted rune
        /// </summary>
        internal static bool CanCastPlagueLeech
        {
            get
            {
                if (!Me.GotTarget)
                    return false;

                WoWAura frostFever = Me.CurrentTarget.GetAllAuras().FirstOrDefault( u => u.CreatorGuid == Me.Guid && u.Name == "Frost Fever");
                WoWAura bloodPlague = Me.CurrentTarget.GetAllAuras().FirstOrDefault( u => u.CreatorGuid == Me.Guid && u.Name == "Blood Plague");
                // if there is 3 or less seconds left on the diseases and we have a fully depleted rune then return true.
                return (frostFever != null && frostFever.TimeLeft <= TimeSpan.FromSeconds(3) || bloodPlague != null && bloodPlague.TimeLeft <= TimeSpan.FromSeconds(3))
                    && (BloodRuneSlotsActive == 0 || FrostRuneSlotsActive == 0 || UnholyRuneSlotsActive == 0);
            }
        }

        #region Pull

        // All DKs should be throwing death grip when not in intances. It just speeds things up, and makes a mess for PVP :)
        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, (WoWSpec)int.MaxValue, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightNormalAndPvPPull()
        {
            return new PrioritySelector(
                Movement.CreateMoveToLosBehavior(), 
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
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
            return
                new PrioritySelector(
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateDismount("Pulling"),
                    Spell.Cast("Howling Blast"),
                    Spell.Cast("Icy Touch"),
                    Movement.CreateMoveToMeleeBehavior(true)
                    );
        }

        #endregion

        #region PreCombatBuffs

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightPreCombatBuffs()
        {
            // Note: This is one of few places where this is slightly more valid than making multiple functions.
            // Since this type of stuff is shared, we are safe to do this. Jus leave as-is.
            return new PrioritySelector(
                Spell.BuffSelf(
                    "Frost Presence",
                    ret => TalentManager.CurrentSpec == (WoWSpec)0),
                Spell.BuffSelf(
                    "Blood Presence",
                    ret => TalentManager.CurrentSpec == WoWSpec.DeathKnightBlood),
                    
                Spell.BuffSelf(
                    "Frost Presence",
                    ret => TalentManager.CurrentSpec == WoWSpec.DeathKnightFrost),

                Spell.BuffSelf(
                    "Unholy Presence",
                    ret =>
                    TalentManager.CurrentSpec == WoWSpec.DeathKnightUnholy),

                Spell.BuffSelf(
                    "Horn of Winter",
                    ret =>
                    !StyxWoW.Me.HasPartyBuff( PartyBuffType.AttackPower)),

                // limit PoF to once every ten seconds in case there is some
                // .. oddness here
                new Throttle( 10,
                    Spell.BuffSelf(
                        "Path of Frost",
                        ret => Settings.UsePathOfFrost )
                        )
                );
        }

        #endregion

        [Behavior(BehaviorType.LossOfControl, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.BuffSelf("Icebound Fortitude", ret => StyxWoW.Me.HealthPercent < Settings.IceboundFortitudePercent)
                    )
                );
        }

        #region Heal

        [Behavior(BehaviorType.Heal, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy)]
        [Behavior(BehaviorType.Heal, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost)]
        public static Composite CreateDeathKnightHeals()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    Spell.BuffSelf("Death Coil",
                        ret => StyxWoW.Me.HealthPercent < Settings.LichbornePercent
                            && StyxWoW.Me.HasAura("Lichborne")),

                    Spell.BuffSelf("Death Pact",
                        ret => Common.HasTalent( DeathKnightTalents.DeathPact) 
                            && StyxWoW.Me.HealthPercent < Settings.DeathPactPercent 
                            && (StyxWoW.Me.GotAlivePet || GhoulMinionIsActive)),

                    Spell.Cast("Death Siphon",
                        ret => Common.HasTalent( DeathKnightTalents.DeathSiphon) 
                            && Me.GotTarget && Me.InLineOfSpellSight && Me.IsSafelyFacing(Me.CurrentTarget)
                            && StyxWoW.Me.HealthPercent < Settings.DeathSiphonPercent),

                    Spell.BuffSelf("Conversion",
                        ret => Common.HasTalent( DeathKnightTalents.Conversion) 
                            && StyxWoW.Me.HealthPercent < Settings.ConversionPercent 
                            && StyxWoW.Me.RunicPowerPercent >= Settings.MinimumConversionRunicPowerPrecent),

                    Spell.Cast("Death Strike",
                        ret => Me.GotTarget && Me.InLineOfSpellSight && Me.IsSafelyFacing(Me.CurrentTarget)
                            && StyxWoW.Me.HealthPercent < Settings.DeathStrikeEmergencyPercent),

                    // use it to heal with deathcoils.
                    Spell.BuffSelf("Lichborne",
                        ret => StyxWoW.Me.HealthPercent < Settings.LichbornePercent 
                            && StyxWoW.Me.CurrentRunicPower >= 60
                            && (!Settings.LichborneExclusive || !Me.HasAnyAura( "Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Icebound Fortitude"))),

                    // I'm frost or blood and I need to summon pet for Death Pact
                    new Decorator(
                        ret => TalentManager.CurrentSpec == WoWSpec.DeathKnightFrost && !GhoulMinionIsActive,
                        Spell.BuffSelf("Raise Dead",
                            ret => Me.HealthPercent < Settings.SummonGhoulPercentFrost
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
            return new PrioritySelector(
                // *** Defensive Cooldowns ***
                // Anti-magic shell - no cost and doesnt trigger GCD 
                    Spell.BuffSelf("Anti-Magic Shell",
                                   ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                                         (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                                                         u.CurrentTargetGuid == StyxWoW.Me.Guid)),
                // we want to make sure our primary target is within melee range so we don't run outside of anti-magic zone.
                    Spell.CastOnGround("Anti-Magic Zone", ctx => StyxWoW.Me.Location,
                                       ret => Common.HasTalent( DeathKnightTalents.AntiMagicZone) &&
                                              !StyxWoW.Me.HasAura("Anti-Magic Shell") &&
                                              Unit.NearbyUnfriendlyUnits.Any(u =>
                                                                             (u.IsCasting ||
                                                                              u.ChanneledCastingSpellId != 0) &&
                                                                             u.CurrentTargetGuid == StyxWoW.Me.Guid) &&
                                              Targeting.Instance.FirstUnit != null &&
                                              Targeting.Instance.FirstUnit.IsWithinMeleeRange),

                    Spell.BuffSelf("Icebound Fortitude", ret => StyxWoW.Me.HealthPercent < Settings.IceboundFortitudePercent),

                    Spell.BuffSelf("Lichborne", ret => StyxWoW.Me.IsCrowdControlled()),

                    Spell.BuffSelf("Desecrated Ground", ret => Common.HasTalent( DeathKnightTalents.DesecratedGround) && StyxWoW.Me.IsCrowdControlled()),
                    
                    CreateRaiseAllyBehavior( on => Unit.NearbyGroupMembers.FirstOrDefault( u=> u.IsDead && u.DistanceSqr < 40 * 40 && u.InLineOfSpellSight)),

                    // *** Offensive Cooldowns ***

                    // I'm unholy and I don't have a pet or I am blood/frost and I am using pet as dps bonus
                    Spell.BuffSelf("Raise Dead",
                        ret => (TalentManager.CurrentSpec == WoWSpec.DeathKnightUnholy && !StyxWoW.Me.GotAlivePet) 
                            || (TalentManager.CurrentSpec == WoWSpec.DeathKnightFrost && Settings.UseGhoulAsDpsCdFrost && !GhoulMinionIsActive)),

                    // never use army of the dead in instances if not blood specced unless you have the army of the dead glyph to take away the taunting
                    Spell.BuffSelf("Army of the Dead", 
                        ret => Settings.UseArmyOfTheDead 
                            && UseLongCoolDownAbility 
                            && (SingularRoutine.CurrentWoWContext != WoWContext.Instances || TalentManager.HasGlyph("Army of the Dead"))),

                    Spell.BuffSelf("Empower Rune Weapon",
                            ret => UseLongCoolDownAbility && StyxWoW.Me.RunicPowerPercent < 70 && ActiveRuneCount == 0),

                    Spell.BuffSelf("Death's Advance",
                        ret => Common.HasTalent( DeathKnightTalents.DeathsAdvance) 
                            && Me.GotTarget 
                            && (!SpellManager.CanCast("Death Grip", false) || SingularRoutine.CurrentWoWContext == WoWContext.Instances) 
                            && StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),

                    Spell.BuffSelf("Blood Tap",
                        ret => Me.HasAura( "Blood Charge", 5) 
                            && (BloodRuneSlotsActive == 0 || FrostRuneSlotsActive == 0 || UnholyRuneSlotsActive == 0)),

                    Spell.Cast("Plague Leech", ret => CanCastPlagueLeech)
                );
        }

        #endregion

        #region Death Grip

        public static Composite CreateGetOverHereBehavior()
        {
            return new Throttle( 1,
                new PrioritySelector(
                    CreateDeathGripBehavior(),
                    CreateChainsOfIceBehavior()
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
                        && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.TaggedByMe)
                    ),
                new DecoratorContinue( ret => StyxWoW.Me.IsMoving, new Action(ret => Navigator.PlayerMover.MoveStop())),
                new WaitContinue( 1, until => Me.CurrentTarget.IsWithinMeleeRange, new ActionAlwaysSucceed())
                );
        }

        public static Composite CreateChainsOfIceBehavior()
        {
            return Spell.Buff("Chains of Ice", ret => Unit.CurrentTargetIsMovingAwayFromMe && !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost));
        }

        public static Composite CreateDarkSuccorBehavior()
        {
            // health below determined %
            // user wants to cast on cooldown without regard to health
            // we have aura AND (target is about to die OR aura expires in less than 3 secs)
            return new Decorator(
                ret => Me.HasAura("Dark Succor") 
                    && (Me.HealthPercent < 80 || Me.HasAuraExpired("Dark Succor", 3) || (Me.GotTarget && Me.CurrentTarget.TimeToDeath() < 6) 
                    && Me.CurrentTarget.InLineOfSpellSight 
                    && Me.IsSafelyFacing( Me.CurrentTarget)
                    && SpellManager.CanCast("Death Strike", Me.CurrentTarget)),
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
                        ret => SpellManager.CanCast("Unholy Blight")
                            && Unit.NearbyUnfriendlyUnits.Any(u => (u.IsPlayer || u.IsBoss()) && u.Distance < (u.MeleeDistance() + 5) && u.MyAuraMissing("Blood Plague"))),

                    Spell.Cast("Outbreak", ret => Me.CurrentTarget.MyAuraMissing("Frost Fever") || Me.CurrentTarget.MyAuraMissing("Blood Plague")),

                // now Rune based abilities
                    new Decorator(
                        ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.CurrentTarget.MyAuraMissing("Frost Fever"),
                        new PrioritySelector(
                            Spell.Cast("Howling Blast", ret => Me.Specialization == WoWSpec.DeathKnightFrost),
                            Spell.Cast("Icy Touch", ret => Me.Specialization != WoWSpec.DeathKnightFrost)
                            )
                        ),

                    Spell.Cast(
                        "Plague Strike", 
                        on => Me.CurrentTarget,
                        ret => Me.CurrentTarget.MyAuraMissing("Blood Plague")
                        )
                    )
                );
        }

        public static Composite CreateRaiseAllyBehavior(UnitSelectionDelegate onUnit)
        {
            if (!Settings.UseRaiseAlly)
                return new PrioritySelector();

            if (onUnit == null)
            {
                Logger.WriteDebug("CreateRaiseAllyBehavior: error - onUnit == null");
                return new PrioritySelector();
            }

            return new PrioritySelector(
                ctx => onUnit(ctx),
                new Decorator(
                    ret => onUnit(ret) != null && Spell.GetSpellCooldown("Raise Ally") == TimeSpan.Zero,
                    new PrioritySelector(
                        Spell.WaitForCast(true),
                        Movement.CreateMoveToRangeAndStopBehavior(ret => (WoWUnit)ret, range => 40f),
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            Spell.Cast("Raise Ally", ret => (WoWUnit)ret)
                            )
                        )
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