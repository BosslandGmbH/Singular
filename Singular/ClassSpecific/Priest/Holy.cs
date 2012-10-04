using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Styx.WoWInternals.World;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.Priest
{
    public class Holy
    {
        [Behavior(BehaviorType.Rest, WoWClass.Priest, WoWSpec.PriestHoly)]
        public static Composite CreateHolyHealRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                // Heal self before resting. There is no need to eat while we have 100% mana
                CreateHolyHealOnlyBehavior(true),
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour(),
                // Can we res people?
                Spell.Resurrect("Resurrection"),
                // Make sure we're healing OOC too!
                CreateHolyHealOnlyBehavior(false, false)
                );
        }

        public static Composite CreateHolyHealOnlyBehavior()
        {
            return CreateHolyHealOnlyBehavior(false, true);
        }

        public static Composite CreateHolyHealOnlyBehavior(bool selfOnly)
        {
            return CreateHolyHealOnlyBehavior(selfOnly, false);
        }

        public static Composite CreateHolyHealOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;
            WoWUnit healTarget = null;
            return new
                PrioritySelector(
                ret => healTarget = selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                    new Decorator(
                        ret => healTarget != null && (moveInRange || healTarget.InLineOfSpellSight && healTarget.DistanceSqr < 40 * 40),
                        new PrioritySelector(
                        Spell.WaitForCast(),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToLosBehavior(ret => healTarget)),
                // use fade to drop aggro.
                        Spell.Cast("Fade", ret => (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid) && StyxWoW.Me.CurrentMap.IsInstance && Targeting.GetAggroOnMeWithin(StyxWoW.Me.Location, 30) > 0),

                        Spell.Cast("Mindbender", ret => StyxWoW.Me.ManaPercent <= 80 && StyxWoW.Me.GotTarget),
                        Spell.Cast("Shadowfiend", ret => StyxWoW.Me.ManaPercent <= 80 && StyxWoW.Me.GotTarget),

                        Spell.BuffSelf("Desperate Prayer", ret => StyxWoW.Me.GetPredictedHealthPercent() <= 50),
                        Spell.BuffSelf("Hymn of Hope", ret => StyxWoW.Me.ManaPercent <= 15 && (!SpellManager.HasSpell("Shadowfiend") || SpellManager.Spells["Shadowfiend"].Cooldown)),
                        Spell.BuffSelf("Divine Hymn", ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() <= SingularSettings.Instance.Priest.DivineHymnHealth) >= SingularSettings.Instance.Priest.DivineHymnCount),

                        Spell.BuffSelf("Chakra: Sanctuary"), // all 3 are avail with a cd in holy - add them to the UI manager for holy priest - default Sanctuary
                        Spell.Cast(
                            "Prayer of Mending",
                            ret => (WoWUnit)ret,
                            ret => ret is WoWPlayer && Group.Tanks.Contains((WoWPlayer)ret) && !((WoWUnit)ret).HasMyAura("Prayer of Mending", 3) &&
                                   Group.Tanks.Where(t => t != healTarget).All(p => !p.HasMyAura("Prayer of Mending"))),
                        Spell.Heal(
                            "Renew",
                            ret => healTarget,
                            ret => healTarget is WoWPlayer && Group.Tanks.Contains(healTarget) && !healTarget.HasMyAura("Renew")),
                        Spell.Heal("Prayer of Healing",
                            ret => healTarget,
                            ret => StyxWoW.Me.HasAura("Serendipity", 2) && Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() <= SingularSettings.Instance.Priest.PrayerOfHealingSerendipityHealth) >= SingularSettings.Instance.Priest.PrayerOfHealingSerendipityCount),
                        Spell.Heal("Circle of Healing",
                            ret => healTarget,
                            ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() <= SingularSettings.Instance.Priest.CircleOfHealingHealth) >= SingularSettings.Instance.Priest.CircleOfHealingCount),
                        Spell.CastOnGround(
                            "Holy Word: Sanctuary",
                            ret => Clusters.GetBestUnitForCluster(Unit.NearbyFriendlyPlayers.Select(p => p.ToUnit()), ClusterType.Radius, 10f).Location,
                            ret => Clusters.GetClusterCount(healTarget,
                                                            Unit.NearbyFriendlyPlayers.Select(p => p.ToUnit()),
                                                            ClusterType.Radius, 10f) >= 4 ),
                        Spell.Heal(
                            "Holy Word: Serenity",
                            ret => healTarget,
                            ret => ret is WoWPlayer && Group.Tanks.Contains(healTarget)),

                        Spell.Buff("Guardian Spirit",
                            ret => healTarget,
                            ret => healTarget.GetPredictedHealthPercent() <= 10),

                        Spell.CastOnGround("Lightwell",ret => WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, healTarget.Location, 5f)),

                        Spell.Cast("Power Infusion", ret => healTarget.GetPredictedHealthPercent() < 40 || StyxWoW.Me.ManaPercent <= 20),
                        Spell.Heal(
                            "Flash Heal",
                            ret => (WoWUnit)ret,
                            ret => StyxWoW.Me.HasAura("Surge of Light") && healTarget.GetPredictedHealthPercent() <= 90),
                        Spell.Heal(
                            "Flash Heal",
                            ret => healTarget,
                            ret => healTarget.GetPredictedHealthPercent() < SingularSettings.Instance.Priest.HolyFlashHeal),
                        Spell.Heal(
                            "Greater Heal",
                            ret => healTarget,
                            ret => healTarget.GetPredictedHealthPercent() < SingularSettings.Instance.Priest.HolyGreaterHeal),
                        Spell.Heal(
                            "Heal",
                            ret => healTarget,
                            ret => healTarget.GetPredictedHealthPercent() < SingularSettings.Instance.Priest.HolyHeal),
                        new Decorator(
                            ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0,
                            new PrioritySelector(
                                Movement.CreateMoveToLosBehavior(),
                                Movement.CreateFaceTargetBehavior(),
                                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                                Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.GetPredictedHealthPercent() <= 20),
                                Spell.Buff("Shadow Word: Pain", true, ret => SpellManager.HasSpell("Power Word: Solace")),
                                Spell.Cast("Holy Word: Chastise"),
                                Spell.Cast("Mindbender"),
                                Spell.Cast("Holy Fire"),
                                Spell.Cast("Power Word: Solace"),
                                Spell.Cast("Smite", ret => !SpellManager.HasSpell("Power Word: Solace")),
                                Spell.Cast("Mind Spike", ret => !SpellManager.HasSpell("Power Word: Solace")),
                                Movement.CreateMoveToTargetBehavior(true, 35f)
                                )),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToTargetBehavior(true, 35f, ret => healTarget))

                        // Divine Hymn
                // Desperate Prayer
                // Prayer of Mending
                // Prayer of Healing
                // Power Word: Barrier
                // TODO: Add smite healing. Only if Atonement is talented. (Its useless otherwise)
                        )));
        }
        [Behavior(BehaviorType.Heal, WoWClass.Priest, WoWSpec.PriestHoly)]
        public static Composite CreateHolyHealComposite()
        {
            return
                new PrioritySelector(
                    CreateHolyHealOnlyBehavior());
        }

        // This behavior is used in combat/heal AND pull. Just so we're always healing our party.
        // Note: This will probably break shit if we're solo, but oh well!
        [Behavior(BehaviorType.Combat | BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestHoly)]
        public static Composite CreateHolyCombatComposite()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0,
                    new PrioritySelector(
                        Safers.EnsureTarget(),
                        Movement.CreateMoveToLosBehavior(),
                        Movement.CreateFaceTargetBehavior(),
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                        Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                        Spell.Buff("Shadow Word: Pain", true, ret => SpellManager.HasSpell("Power Word: Solace")),
                        Spell.Cast("Holy Word: Chastise"),
                        Spell.Cast("Mindbender"),
                        Spell.Cast("Holy Fire"),
                        Spell.Cast("Power Word: Solace"),
                        Spell.Cast("Smite", ret => !SpellManager.HasSpell("Power Word: Solace")),
                        Spell.Cast("Mind Spike", ret => !SpellManager.HasSpell("Power Word: Solace")),
                        Movement.CreateMoveToTargetBehavior(true, 35f)
                        ))
                );
        }
    }
}
