using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Rest = Singular.Helpers.Rest;
using Styx.WoWInternals;

namespace Singular.ClassSpecific.Druid
{
    public class Resto
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DruidSettings Settings { get { return SingularSettings.Instance.Druid(); } }

        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidHealRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateRestoDruidHealOnlyBehavior(true),
                        Rest.CreateDefaultRestBehaviour(),
                        Spell.Resurrect("Revive"),
                        CreateRestoDruidHealOnlyBehavior(false,false)
                        )              
                    )
                );
        }

        public static Composite CreateRestoDruidHealOnlyBehavior()
        {
            return CreateRestoDruidHealOnlyBehavior(false, true);
        }

        public static Composite CreateRestoDruidHealOnlyBehavior(bool selfOnly)
        {
            return CreateRestoDruidHealOnlyBehavior(selfOnly, false);
        }

        public static Composite CreateRestoDruidHealOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;
            const uint mapleSeedId = 17034;

            return new
                PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                    new Decorator(
                        ret => ret != null && (moveInRange || ((WoWUnit)ret).InLineOfSpellSight && ((WoWUnit)ret).DistanceSqr < 40 * 40),
                        new PrioritySelector(
                        Spell.WaitForCast(),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToLosBehavior(ret => (WoWUnit)ret)),
                        // Ensure we're in range of the unit to heal, and it's in LOS.
                        //CreateMoveToAndFace(35f, ret => (WoWUnit)ret),
                        //Cast Lifebloom on tank if
                        //1- Tank doesn't have lifebloom
                        //2- Tank has less then 3 stacks of lifebloom
                        //3- Tank has 3 stacks of lifebloom but it will expire in 3 seconds
                        Spell.Cast(
                            "Lifebloom",
                            ret => (WoWUnit)ret,
                            ret =>
                            StyxWoW.Me.Combat &&
                                // Keep 3 stacks up on the tank/leader at all times.
                                // If we're in ToL form, we can do rolling LBs for everyone. So ignore the fact that its the leader or not.
                                // LB is cheap, and VERY powerful in ToL form since you can spam it on the entire raid, for a cheap HoT and quite good 'bloom'
                            ((RaFHelper.Leader != null && (WoWUnit)ret == RaFHelper.Leader) || StyxWoW.Me.Shapeshift == ShapeshiftForm.TreeOfLife) &&
                            ((WoWUnit)ret).HealthPercent > 60 &&
                            (!((WoWUnit)ret).HasAura("Lifebloom") || ((WoWUnit)ret).Auras["Lifebloom"].StackCount < 3 ||
                             ((WoWUnit)ret).Auras["Lifebloom"].TimeLeft <= TimeSpan.FromSeconds(3))),
                        //Cast rebirth if the tank is dead. Check for Unburdened Rebirth glyph or Maple seed reagent
                        Spell.Cast(
                            "Rebirth",
                            ret => (WoWUnit)ret,
                            ret => StyxWoW.Me.Combat && RaFHelper.Leader != null && (WoWUnit)ret == RaFHelper.Leader &&
                                   ((WoWUnit)ret).IsDead && (TalentManager.HasGlyph("Unburdened Rebirth") || StyxWoW.Me.BagItems.Any(i => i.Entry == mapleSeedId))),
                        Spell.Cast(
                            "Tranquility",
                            ret => StyxWoW.Me.Combat && StyxWoW.Me.GroupInfo.IsInParty && Unit.NearbyFriendlyPlayers.Count(
                                p =>
                                p.IsAlive && p.HealthPercent <= Settings.TranquilityHealth && p.Distance <= 30) >=
                                   Settings.TranquilityCount),
                        //Use Innervate on party members if we have Glyph of Innervate
                        Spell.Buff(
                            "Innervate",
                            ret => (WoWUnit)ret,
                            ret =>
                            TalentManager.HasGlyph("Innervate") && StyxWoW.Me.Combat && (WoWUnit)ret != StyxWoW.Me &&
                            StyxWoW.Me.ManaPercent <= Settings.InnervateMana &&
                            ((WoWUnit)ret).PowerType == WoWPowerType.Mana && ((WoWUnit)ret).ManaPercent <= Settings.InnervateMana),
                        Spell.Cast(
                            "Swiftmend",
                            ret => (WoWUnit)ret,
                            ret => StyxWoW.Me.Combat && ((WoWUnit)ret).HealthPercent <= Settings.Swiftmend &&
                                   (((WoWUnit)ret).HasAura("Rejuvenation") || ((WoWUnit)ret).HasAura("Regrowth"))),
                        Spell.Cast(
                            "Wild Growth",
                            ret => (WoWUnit)ret,
                            ret => StyxWoW.Me.GroupInfo.IsInParty && Unit.NearbyFriendlyPlayers.Count(
                                p => p.IsAlive && p.HealthPercent <= Settings.WildGrowthHealth &&
                                     p.Location.DistanceSqr(((WoWUnit)ret).Location) <= 30*30) >= Settings.WildGrowthCount),
                        Spell.Cast(
                            "Regrowth",
                            ret => (WoWUnit)ret,
                            ret => !((WoWUnit)ret).HasMyAura("Regrowth") && ((WoWUnit)ret).HealthPercent <= Settings.Regrowth),
                        Spell.Cast(
                            "Healing Touch",
                            ret => (WoWUnit)ret,
                            ret => ((WoWUnit)ret).HealthPercent <= Settings.HealingTouch),
                        Spell.Cast(
                            "Nourish",
                            ret => (WoWUnit)ret,
                            ret => ((WoWUnit)ret).HealthPercent <= Settings.Nourish &&
                                   ((((WoWUnit)ret).HasAura("Rejuvenation") || ((WoWUnit)ret).HasAura("Regrowth") ||
                                    ((WoWUnit)ret).HasAura("Lifebloom") || ((WoWUnit)ret).HasAura("Wild Growth")))),
                        Spell.Cast(
                            "Rejuvenation",
                            ret => (WoWUnit)ret,
                            ret => !((WoWUnit)ret).HasMyAura("Rejuvenation") &&
                                   ((WoWUnit)ret).HealthPercent <= Settings.Rejuvenation),
                        new Decorator(
                            ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0,
                            new PrioritySelector(
                                Movement.CreateMoveToLosBehavior(),
                                Movement.CreateFaceTargetBehavior(),
                                Helpers.Common.CreateInterruptBehavior(),
                                Spell.Buff("Moonfire"),
                                Spell.Cast("Starfire", ret => StyxWoW.Me.HasAura("Fury of Stormrage")),
                                Spell.Cast("Wrath"),
                                Movement.CreateMoveToTargetBehavior(true, 35f)
                                )),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToTargetBehavior(true, 35f, ret => (WoWUnit)ret))
                        )));
        }
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidHealBehavior()
        {
            return
                new PrioritySelector(
                    CreateRestoDruidHealOnlyBehavior());
        }
        [Behavior(BehaviorType.Combat|BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidCombat()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0,
                        new PrioritySelector(
                            Safers.EnsureTarget(),
                            Movement.CreateMoveToLosBehavior(),
                            Movement.CreateFaceTargetBehavior(),
                            Helpers.Common.CreateDismount("Pulling"),
                            Helpers.Common.CreateInterruptBehavior(),
                            Spell.Buff("Moonfire"),
                            Spell.Cast("Starfire", ret => StyxWoW.Me.HasAura("Fury of Stormrage")),
                            Spell.Cast("Wrath"),
                            Movement.CreateMoveToTargetBehavior(true, 35f)
                            ))
                    );
        }
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Tree of Life",
                    ret => StyxWoW.Me.GroupInfo.IsInParty 
                        && Unit.NearbyFriendlyPlayers.Count( p => p.IsAlive && p.HealthPercent <= Settings.TreeOfLifeHealth) >= Settings.TreeOfLifeCount),

                Spell.BuffSelf("Innervate", ret => StyxWoW.Me.ManaPercent < 15 || StyxWoW.Me.ManaPercent <= Settings.InnervateMana),
                Spell.BuffSelf("Barkskin", ret => StyxWoW.Me.HealthPercent <= Settings.Barkskin),

                // Symbiosis
                Spell.BuffSelf("Icebound Fortitude", ret => Me.HealthPercent < Settings.Barkskin),
                Spell.BuffSelf("Deterrence", ret => Me.HealthPercent < Settings.Barkskin),
                Spell.BuffSelf("Evasion", ret => Me.HealthPercent < Settings.Barkskin),
                Spell.BuffSelf("Fortifying Brew", ret => Me.HealthPercent < Settings.Barkskin),
                Spell.BuffSelf("Intimidating Roar",  ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8 * 8) > 1)

                );
        }


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidRestoration, WoWContext.Battlegrounds | WoWContext.Instances, 2)]
        public static Composite CreateRestoPreCombatBuffForSymbiosis(UnitSelectionDelegate onUnit)
        {
            return Common.CreateDruidCastSymbiosis(on => GetRestoBestSymbiosisTarget());
        }

        private static WoWUnit GetRestoBestSymbiosisTarget()
        {
            WoWUnit target = null;

            if ( SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds )
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Warrior);

            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.DeathKnight);
            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Hunter);
            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Rogue);
            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Monk);

            return target;
        }
    }
}
