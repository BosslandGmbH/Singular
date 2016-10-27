using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Singular.Utilities;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System;
using System.Linq;
using Styx.Common;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Paladin
{
    public class Common
    {
        private static PaladinSettings PaladinSettings { get { return SingularSettings.Instance.Paladin(); } }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Paladin)]
        public static Composite CreatePaladinPreCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    new Decorator(
                        ret => TalentManager.CurrentSpec != WoWSpec.PaladinHoly,
                        new PrioritySelector(
                            Spell.BuffSelf("Righteous Fury", ret => TalentManager.CurrentSpec == WoWSpec.PaladinProtection && StyxWoW.Me.GroupInfo.IsInParty)
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Paladin, (WoWSpec)0)]
        [Behavior(BehaviorType.Heal, WoWClass.Paladin, WoWSpec.PaladinProtection, WoWContext.Normal | WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.Heal, WoWClass.Paladin, WoWSpec.PaladinRetribution, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateDpsPaladinHeal()
        {
            return new PrioritySelector(
                Spell.BuffSelfAndWait("Divine Shield", ret => Me.HealthPercent <= PaladinSettings.DivineShieldHealthProt && !Me.HasAura("Forbearance") && !Me.HasAnyAura("Horde Flag", "Alliance Flag")),

                Spell.BuffSelf("Devotion Aura", req => Me.Silenced),

                Spell.Cast(
                    "Lay on Hands",
                    mov => false,
                    on => Me,
                    req => Me.PredictedHealthPercent(includeMyHeals: true) <= PaladinSettings.SelfLayOnHandsHealth
                        && !Me.HasAura("Divine Shield") && Spell.CanCastHack("Flash of Light", Me) // save if we have DS and able to cast FoL
                    ),

                new Decorator(
                    req => Me.Race == WoWRace.Tauren
                        && EventHandlers.TimeSinceAttackedByEnemyPlayer.TotalSeconds < 5
                        && EventHandlers.AttackingEnemyPlayer != null
                        && EventHandlers.AttackingEnemyPlayer.SpellDistance() < 8
                        && Me.HealthPercent <= PaladinSettings.SelfFlashOfLightHealth,
                    Spell.BuffSelf("War Stomp", req => Unit.UnitsInCombatWithMeOrMyStuff(8).Any(u => u.IsPlayer && !u.IsCrowdControlled()))
                    ),

                Spell.WaitForCastOrChannel(),

                Spell.Cast("Flash of Light",
                    mov => false,
                    on => Me,
                    req => NeedFlashOfLight(),
                    cancel => CancelFlashOfLight()
                    )
                );
        }

        private static bool NeedFlashOfLight()
        {
            // always check predicted health with our heals included when determining need for self-heal
            float myPredictedHealth = Me.PredictedHealthPercent(includeMyHeals: true);
            if (myPredictedHealth <= PaladinSettings.SelfFlashOfLightHealth)
                return true;

            if (myPredictedHealth <= 80)
            {
                if (Me.HasAura("Divine Shield"))
                    return true;

                if (!Me.IsMoving)
                {
                    if (!Unit.UnitsInCombatWithMeOrMyStuff(45)
                            .Any(u => !u.HasAuraWithEffect(WoWApplyAuraType.ModConfuse, WoWApplyAuraType.ModCharm, WoWApplyAuraType.ModFear, WoWApplyAuraType.ModPacify, WoWApplyAuraType.ModPossess, WoWApplyAuraType.ModStun)))
                    {
                        return true;
                    }
                }

                if (!Me.Combat)
                    return true;
            }
            return false;
        }

        private static bool CancelFlashOfLight()
        {
            // use our current health (not predicted) when seeing if we should cancel
            double myHealth = Me.HealthPercent;
            if (myHealth <= PaladinSettings.SelfFlashOfLightHealth)
                return false;

            if (myHealth <= 90)
            {
                if (Me.HasAura("Divine Shield"))
                    return false;

                if (!Unit.UnitsInCombatWithMeOrMyStuff(45)
                        .Any(u => !u.HasAuraWithEffect(WoWApplyAuraType.ModConfuse, WoWApplyAuraType.ModCharm, WoWApplyAuraType.ModFear, WoWApplyAuraType.ModPacify, WoWApplyAuraType.ModPossess, WoWApplyAuraType.ModStun)))
                {
                    return false;
                }
            }

            return true;
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Paladin, (WoWSpec)int.MaxValue, WoWContext.Normal, 1)]
        public static Composite CreatePaladinCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Devotion Aura", req => Me.Silenced),
                new Decorator(
                    req => !Unit.IsTrivial(Me.CurrentTarget),
                    new PrioritySelector(
                        Spell.Cast("Repentance",
                            onUnit =>
                            Unit.NearbyUnfriendlyUnits
                            .Where(u => (u.IsPlayer || u.IsDemon || u.IsHumanoid || u.IsDragon || u.IsGiant || u.IsUndead)
                                    && Me.CurrentTargetGuid != u.Guid
                                    && (u.Aggro || u.PetAggro || (u.Combat && u.IsTargetingMeOrPet))
                                    && !u.IsCrowdControlled()
                                    && u.Distance.Between(10, 30) && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && (!Me.GotTarget() || u.Location.Distance(Me.CurrentTarget.Location) > 10))
                            .OrderByDescending(u => u.Distance)
                            .FirstOrDefault()
                            )
                        )
                        )
                );
        }


        public static Composite CreatePaladinBlindingLightBehavior()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                return new ActionAlwaysFail();

            return Spell.Cast(
                sp => "Blinding Light",
                mov => true,
                on => Me.CurrentTarget,
                req =>
                {
                    if (!Spell.UseAOE)
                        return false;
                    if (Unit.UnitsInCombatWithUsOrOurStuff(10).Count(u => u.IsSafelyFacing(Me, 130f)) > 2)
                        return true;
                    if (Me.CurrentTarget.IsPlayer && Me.CurrentTarget.SpellDistance() < 10 && Me.CurrentTarget.IsFacing(Me))
                        return true;
                    return false;
                },
                cancel => Me.CurrentTarget.IsPlayer && (Me.CurrentTarget.SpellDistance() > 10 || !Me.CurrentTarget.IsFacing(Me))
                );


        }

        /// <summary>
        /// invoke on CurrentTarget if not tagged. use ranged instant casts if possible.  this
        /// is a blend of abilities across all specializations
        /// </summary>
        /// <returns></returns>
        public static Composite CreatePaladinPullMore()
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return new ActionAlwaysFail();

            return new Throttle(
                2,
                new Decorator(
                    req => Me.GotTarget()
                        && !Me.CurrentTarget.IsPlayer
                        && !Me.CurrentTarget.IsTagged
                        && !Me.CurrentTarget.IsWithinMeleeRange,
                    new PrioritySelector(
                        Spell.Cast("Judgement"),
                        Spell.Cast("Reckoning"),
                        Spell.Cast("Hammer of Justice"),
                        Spell.Cast("Holy Shock")
                        )
                    )
                );
        }


        public static bool HasTalent(PaladinTalents tal)
        {
            return TalentManager.IsSelected((int)tal);
        }
    }

    public enum PaladinTalents
    {
        BestowFaith = 1,
        LightsHammer,
        CrusadersMight,

        FirstAvenger = BestowFaith,
        BastionOfLight = LightsHammer,
        CrusadersJudgment = CrusadersMight,

        FinalVerdict = BestowFaith,
        ExecutionSentence = LightsHammer,
        Consecration = CrusadersMight,



        DivineSteedHoly = 4,
        UnbreakableSpirit,
        RuleOfLaw,

        HolyShield = DivineSteedHoly,
        BlessedHammer = UnbreakableSpirit,
        ConsecratedHammer = RuleOfLaw,

        TheFiresOfJustice = DivineSteedHoly,
        Zeal = UnbreakableSpirit,
        GreaterJudgment = RuleOfLaw,


        FistOfJustice = 7,
        Repentance,
        BlindingLight,



        DevotionAura = 10,
        AuraOfSacrifice,
        AuraOfMercy,

        BlessingOfSpellwarding = DevotionAura,
        BlesingOfSalvation = AuraOfSacrifice,
        RetributionAura = AuraOfMercy,

        VirtuesBlade = DevotionAura,
        BladeOfWrath = AuraOfSacrifice,
        DivineHammer = AuraOfMercy,


        DivinePurposeHoly = 13,
        HolyAvenger,
        HolyPrism,

        HandOfTheProtector = DivinePurposeHoly,
        KnightTemplar = HolyAvenger,
        FinalStand = HolyPrism,

        JusticarsVengeance = DivinePurposeHoly,
        EyeForAnEye = HolyAvenger,
        WordOfGlory = HolyPrism,


        FerventMartyr = 16,
        SanctifiedWrath,
        JudgmentOfLightHoly,

        AegisOfLight = FerventMartyr,
        JudgmentOfLightProtection = SanctifiedWrath,
        ConsecratedGround = JudgmentOfLightHoly,

        DivineIntervention = FerventMartyr,
        DivineSteedRetribution = SanctifiedWrath,
        SealOfLight = JudgmentOfLightHoly,


        BeaconOfFaith = 19,
        BeaconOfTheLightbringer,
        BeaconOfVirtue,

        RighteousProtector = BeaconOfFaith,
        Seraphim = BeaconOfTheLightbringer,
        LastDefender = BeaconOfVirtue,

        DivinePurposeRetribution = BeaconOfFaith,
        Crusade = Seraphim,
        HolyWrath = BeaconOfVirtue

    }

}
