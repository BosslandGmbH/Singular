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
                    CreatePaladinBlessBehavior(),
                    CreatePaladinSealBehavior(),
                    new Decorator(
                        ret => TalentManager.CurrentSpec != WoWSpec.PaladinHoly,
                        new PrioritySelector(
                            Spell.BuffSelf("Righteous Fury", ret => TalentManager.CurrentSpec == WoWSpec.PaladinProtection && StyxWoW.Me.GroupInfo.IsInParty)
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Paladin)]
        public static Composite CreatePaladinLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.BuffSelf("Hand of Freedom")
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Paladin, (WoWSpec) 0)]
        [Behavior(BehaviorType.Heal, WoWClass.Paladin, WoWSpec.PaladinProtection, WoWContext.Normal | WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.Heal, WoWClass.Paladin, WoWSpec.PaladinRetribution, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateDpsPaladinHeal()
        {
            return new PrioritySelector(
                Spell.BuffSelfAndWait("Divine Shield", ret => Me.HealthPercent <= PaladinSettings.DivineShieldHealthProt && !Me.HasAura("Forbearance") && !Me.HasAnyAura("Horde Flag", "Alliance Flag")),
                Spell.BuffSelf("Divine Protection", ret => Me.HealthPercent <= PaladinSettings.DivineProtectionHealthProt),

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

            if (myPredictedHealth <= 90)
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
                    ),

                new Throttle(
                    3,
                    Spell.BuffSelf(
                        "Emancipate",
                        req =>
                        {
                            if (!PaladinSettings.UseEmancipate)
                                return false;

                            WoWAura aura = Me.GetAuraWithMechanic(WoWSpellMechanic.Rooted, WoWSpellMechanic.Slowed, WoWSpellMechanic.Snared);
                            if (aura != null)
                            {
                                TimeSpan left = aura.TimeLeft;
                                if (left.TotalSeconds > 1.5 && Spell.CanCastHack("Emancipate", Me))
                                {
                                    Logger.Write( LogColor.Hilite, "^Emancipate: countering {0}#{1} which has {2:F1} seconds remaining", aura.Name, aura.SpellId, left.TotalSeconds );
                                    return true;
                                }
                            }
                            return false;
                        }
                        )
                    )
                );
        }



        /// <summary>
        /// cast Blessing of Kings or Blessing of Might based upon configuration setting.
        /// 
        /// </summary>
        /// <returns></returns>
        private static Composite CreatePaladinBlessBehavior()
        {
            return
                new PrioritySelector(

                        PartyBuff.BuffGroup( 
                            "Blessing of Kings", 
                            ret => PaladinSettings.Blessings == PaladinBlessings.Auto || PaladinSettings.Blessings == PaladinBlessings.Kings,
                            "Blessing of Might"),

                        PartyBuff.BuffGroup(
                            "Blessing of Might",
                            ret => PaladinSettings.Blessings == PaladinBlessings.Auto || PaladinSettings.Blessings == PaladinBlessings.Might, 
                            "Blessing of Kings")
                    );
        }

        /// <summary>
        /// behavior to cast appropriate seal 
        /// </summary>
        /// <returns></returns>
        public static Composite CreatePaladinSealBehavior()
        {
            return new Throttle( TimeSpan.FromMilliseconds(500),
                new Sequence(
                    new Action( ret => _seal = GetBestSeal() ),
                    new Decorator(
                        ret => _seal != PaladinSeal.None
                            && !Me.HasMyAura(SealSpell(_seal))
                            && Spell.CanCastHack(SealSpell(_seal), Me),
                        Spell.BuffSelfAndWaitPassive( s => SealSpell(_seal))
                        )
                    )
                );
        }

        static PaladinSeal _seal;

        static string SealSpell( PaladinSeal s)
        { 
            return "Seal of " + s.ToString(); 
        }

        /// <summary>
        /// determines the best PaladinSeal value to use.  Attempts to use 
        /// user setting first, but defaults to something reasonable otherwise
        /// </summary>
        /// <returns>PaladinSeal to use</returns>
        public static PaladinSeal GetBestSeal()
        {
            if (PaladinSettings.Seal == PaladinSeal.None)
                return PaladinSeal.None;

            if (TalentManager.CurrentSpec == WoWSpec.None)
                return SpellManager.HasSpell("Seal of Command") ? PaladinSeal.Command : PaladinSeal.None;

            PaladinSeal bestSeal = Settings.PaladinSeal.Truth;
            if (PaladinSettings.Seal != Settings.PaladinSeal.Auto )
                bestSeal = PaladinSettings.Seal;
            else
            {
                switch (TalentManager.CurrentSpec)
                {
                    case WoWSpec.PaladinHoly:
                        if (Me.IsInGroup())
                            bestSeal = Settings.PaladinSeal.Insight;
                        break;

                    // Seal Twisting.  fixed bug in prior implementation that would cause it
                    // .. to flip seal too quickly.  When we have Insight and go above 5%
                    // .. would cause casting another seal, which would take back below 5% and
                    // .. and recast Insight.  Wait till we build up to 30% if we do this to 
                    // .. avoid wasting mana and gcd's
                    case WoWSpec.PaladinRetribution:
                    case WoWSpec.PaladinProtection:
                        if (Me.ManaPercent < 5 || (Me.ManaPercent < 30 && Me.HasMyAura("Seal of Insight")))
                            bestSeal = Settings.PaladinSeal.Insight;
                        else if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                            bestSeal = Settings.PaladinSeal.Truth;
                        else if (Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4)
                            bestSeal = Settings.PaladinSeal.Righteousness;
                        break;
                }
            }

            if (!SpellManager.HasSpell(SealSpell(bestSeal)))
                bestSeal = Settings.PaladinSeal.Command;

            if (bestSeal == Settings.PaladinSeal.Command && SpellManager.HasSpell("Seal of Truth"))
                bestSeal = Settings.PaladinSeal.Truth;

            return bestSeal;
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
                        Spell.Cast("Exorcism"),
                        Spell.Cast("Hammer of Wrath"),
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
