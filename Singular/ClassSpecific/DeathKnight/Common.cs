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

namespace Singular.ClassSpecific.DeathKnight
{
    public class Common
    {
        internal const uint Ghoul = 26125;

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DeathKnightSettings DeathKnightSettings { get { return SingularSettings.Instance.DeathKnight(); } }

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
                return StyxWoW.Me.CurrentTarget.HasMyAura("Blood Plague") &&
                       StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") &&
                       Unit.NearbyUnfriendlyUnits.Count(u =>
                                                        u.DistanceSqr < 10 * 10 &&
                                                        !u.HasMyAura("Blood Plague") &&
                                                        !u.HasMyAura("Frost Fever")) > 0;
            }
        }

        internal static int BloodRuneSlotsActive { get { return StyxWoW.Me.GetRuneCount(0) + StyxWoW.Me.GetRuneCount(1); } }
        internal static int FrostRuneSlotsActive { get { return StyxWoW.Me.GetRuneCount(2) + StyxWoW.Me.GetRuneCount(3); } }
        internal static int UnholyRuneSlotsActive { get { return StyxWoW.Me.GetRuneCount(4) + StyxWoW.Me.GetRuneCount(5); } }

        internal static bool CanCastPlagueLeech
        {
            get
            {
                if (!StyxWoW.Me.GotTarget)
                    return false;

                WoWAura frostFever =
                    StyxWoW.Me.CurrentTarget.GetAllAuras().FirstOrDefault(
                        u => u.CreatorGuid == StyxWoW.Me.Guid && u.Name == "Frost Fever");
                WoWAura bloodPlague =
                    StyxWoW.Me.CurrentTarget.GetAllAuras().FirstOrDefault(
                        u => u.CreatorGuid == StyxWoW.Me.Guid && u.Name == "Blood Plague");
                // if there is 3 or less seconds left on the diseases and we have a fully depleted rune then return true.
                return frostFever != null && frostFever.TimeLeft <= TimeSpan.FromSeconds(3) ||
                       bloodPlague != null && bloodPlague.TimeLeft <= TimeSpan.FromSeconds(3) &&
                       (BloodRuneSlotsActive == 0 || FrostRuneSlotsActive == 0 || UnholyRuneSlotsActive == 0);
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
        // [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, 0, WoWContext.Instances)]
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
            return
                new PrioritySelector(
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
                            ret => DeathKnightSettings.UsePathOfFrost )
                            )
                    );
        }

        #endregion

        #region Heal

        [Behavior(BehaviorType.Heal, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy)]
        [Behavior(BehaviorType.Heal, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost)]
        public static Composite CreateDeathKnightHeals()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.BuffSelf("Death Pact",
                                   ret =>
                                   TalentManager.IsSelected((int)DeathKnightTalents.DeathPact) &&
                                   StyxWoW.Me.HealthPercent < DeathKnightSettings.DeathPactPercent &&
                                   (StyxWoW.Me.GotAlivePet || GhoulMinionIsActive)),
                    Spell.Cast("Death Siphon",
                               ret =>
                               TalentManager.IsSelected((int)DeathKnightTalents.DeathSiphon) &&
                               StyxWoW.Me.GotTarget &&
                               StyxWoW.Me.HealthPercent < DeathKnightSettings.DeathSiphonPercent),
                    Spell.BuffSelf("Conversion",
                                   ret =>
                                   TalentManager.IsSelected((int)DeathKnightTalents.Conversion) &&
                                   StyxWoW.Me.HealthPercent < DeathKnightSettings.ConversionPercent &&
                                   StyxWoW.Me.RunicPowerPercent >=
                                   DeathKnightSettings.MinimumConversionRunicPowerPrecent),
                    Spell.BuffSelf("Rune Tap",
                                   ret =>
                                   TalentManager.CurrentSpec == WoWSpec.DeathKnightBlood &&
                                   StyxWoW.Me.HealthPercent < DeathKnightSettings.RuneTapPercent),
                    Spell.Cast("Death Strike",
                               ret =>
                               StyxWoW.Me.GotTarget &&
                               StyxWoW.Me.HealthPercent <
                               DeathKnightSettings.DeathStrikeEmergencyPercent),
                    Spell.BuffSelf("Death Coil",
                                   ret =>
                                   StyxWoW.Me.HealthPercent < DeathKnightSettings.LichbornePercent &&
                                   StyxWoW.Me.HasAura("Lichborne")),

                    Spell.BuffSelf("Lichborne",
                                   ret => // use it to heal with deathcoils.
                                          (StyxWoW.Me.HealthPercent <
                                           DeathKnightSettings.LichbornePercent
                                           && StyxWoW.Me.CurrentRunicPower >= 60
                                           && (!DeathKnightSettings.LichborneExclusive ||
                                               (!StyxWoW.Me.HasAura("Bone Shield")
                                                && !StyxWoW.Me.HasAura("Vampiric Blood")
                                                && !StyxWoW.Me.HasAura("Dancing Rune Weapon")
                                                && !StyxWoW.Me.HasAura("Icebound Fortitude"))))),
                    Spell.BuffSelf("Raise Dead",
                                   ret =>
                                       // I'm frost or blood and I need to summon pet for Death Pact
                                   ((TalentManager.CurrentSpec == WoWSpec.DeathKnightFrost &&
                                     StyxWoW.Me.HealthPercent <
                                     DeathKnightSettings.SummonGhoulPercentFrost) ||
                                    (TalentManager.CurrentSpec == WoWSpec.DeathKnightBlood &&
                                     StyxWoW.Me.HealthPercent <
                                     DeathKnightSettings.SummonGhoulPercentBlood)) &&
                                   !GhoulMinionIsActive &&
                                   (!DeathKnightSettings.DeathPactExclusive ||
                                    (!StyxWoW.Me.HasAura("Bone Shield")
                                     && !StyxWoW.Me.HasAura("Vampiric Blood")
                                     && !StyxWoW.Me.HasAura("Dancing Rune Weapon")
                                     && !StyxWoW.Me.HasAura("Lichborne")
                                     && !StyxWoW.Me.HasAura("Icebound Fortitude"))))
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
                ret => !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown(),
                new PrioritySelector(
                // *** Defensive Cooldowns ***
                // Anti-magic shell - no cost and doesnt trigger GCD 
                    Spell.BuffSelf("Anti-Magic Shell",
                                   ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                                         (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                                                         u.CurrentTargetGuid == StyxWoW.Me.Guid)),
                // we want to make sure our primary target is within melee range so we don't run outside of anti-magic zone.
                    Spell.CastOnGround("Anti-Magic Zone", ctx => StyxWoW.Me.Location,
                                       ret => TalentManager.IsSelected((int)DeathKnightTalents.AntiMagicZone) &&
                                              !StyxWoW.Me.HasAura("Anti-Magic Shell") &&
                                              Unit.NearbyUnfriendlyUnits.Any(u =>
                                                                             (u.IsCasting ||
                                                                              u.ChanneledCastingSpellId != 0) &&
                                                                             u.CurrentTargetGuid == StyxWoW.Me.Guid) &&
                                              Targeting.Instance.FirstUnit != null &&
                                              Targeting.Instance.FirstUnit.IsWithinMeleeRange),

                    Spell.BuffSelf("Icebound Fortitude", ret => StyxWoW.Me.HealthPercent < DeathKnightSettings.IceboundFortitudePercent),

                    Spell.BuffSelf("Lichborne", ret => StyxWoW.Me.IsCrowdControlled()),
                    Spell.BuffSelf("Desecrated Ground", ret => TalentManager.IsSelected((int)DeathKnightTalents.DesecratedGround) && StyxWoW.Me.IsCrowdControlled()),
                    
                    Spell.Cast("Raise Ally", 
                        ctx => StyxWoW.Me.PartyMembers.FirstOrDefault( u=> u.IsDead && u.DistanceSqr < 40 * 40 && u.InLineOfSpellSight), 
                        ret => DeathKnightSettings.UseRaiseAlly ),

                    // *** Offensive Cooldowns ***

                    Spell.BuffSelf("Raise Dead",
                                   ret =>
                                       // I'm unholy and I don't have a pet or I am blood/frost and I am using pet as dps bonus
                                   (TalentManager.CurrentSpec == WoWSpec.DeathKnightUnholy && !StyxWoW.Me.GotAlivePet) ||
                                   (TalentManager.CurrentSpec == WoWSpec.DeathKnightFrost &&
                                    DeathKnightSettings.UseGhoulAsDpsCdFrost &&
                                   !GhoulMinionIsActive)),

                    // never use army of the dead in instances if not blood specced unless you have the army of the dead glyph to take away the taunting
                    Spell.BuffSelf("Army of the Dead", ret => DeathKnightSettings.UseArmyOfTheDead &&
                                                              (UseLongCoolDownAbility &&
                                                               (TalentManager.CurrentSpec != WoWSpec.DeathKnightBlood &&
                                                                (SingularRoutine.CurrentWoWContext !=
                                                                 WoWContext.Instances ||
                                                                 TalentManager.HasGlyph("Army of the Dead"))))),

                    Spell.BuffSelf("Empower Rune Weapon",
                                   ret => UseLongCoolDownAbility && StyxWoW.Me.RunicPowerPercent < 70 && ActiveRuneCount == 0),

                    Spell.BuffSelf("Death's Advance",
                                   ret =>
                                   TalentManager.IsSelected((int)DeathKnightTalents.DeathsAdvance) &&
                                   StyxWoW.Me.GotTarget &&
                                   (!SpellManager.CanCast("Death Grip", false) || SingularRoutine.CurrentWoWContext == WoWContext.Instances) &&
                                   StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),

                    Spell.BuffSelf("Blood Tap",
                                   ret =>
                                   StyxWoW.Me.HasAura("Blood Charge") &&
                                   StyxWoW.Me.Auras["Blood Charge"].StackCount >= 5 &&
                                   (BloodRuneSlotsActive == 0 || FrostRuneSlotsActive == 0 || UnholyRuneSlotsActive == 0)),
                    Spell.Cast("Plague Leech", ret => CanCastPlagueLeech)
                    )
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
                Spell.Cast("Death Grip", ret => !Me.CurrentTarget.IsBoss && Me.CurrentTarget.DistanceSqr > 10 * 10 && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.TaggedByMe)),
                new DecoratorContinue( ret => StyxWoW.Me.IsMoving, new Action(ret => Navigator.PlayerMover.MoveStop())),
                new WaitContinue( 1, until => Me.CurrentTarget.IsWithinMeleeRange, new ActionAlwaysSucceed())
                );
        }

        public static Composite CreateChainsOfIceBehavior()
        {
            return Spell.Buff("Chains of Ice", ret => Unit.CurrentTargetIsMovingAwayFromMe && !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost));
        }

        #endregion

        #region Nested type: DeathKnightTalents

        internal enum DeathKnightTalents
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
}