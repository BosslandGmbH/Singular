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
        public static Composite CreateHolyRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        // Heal self before resting. There is no need to eat while we have 100% mana
                        CreateHolyHealOnlyBehavior(true, false),
                        // Rest up damnit! Do this first, so we make sure we're fully rested.
                        Rest.CreateDefaultRestBehaviour( null, "Resurrection"),
                        // Make sure we're healing OOC too!
                        CreateHolyHealOnlyBehavior(false, false),
                        // now buff our movement if possible
                        Common.CreatePriestMovementBuff("Rest")
                        )
                    )
                );

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
                        Spell.BuffSelf("Divine Hymn", ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() <= SingularSettings.Instance.Priest().DivineHymnHealth) >= SingularSettings.Instance.Priest().DivineHymnCount),

                        Spell.Cast(
                            "Prayer of Mending",
                            ret => healTarget,
                            ret => ret is WoWPlayer && Group.Tanks.Contains((WoWPlayer)ret) && !((WoWUnit)ret).HasMyAura("Prayer of Mending", 3) &&
                                   Group.Tanks.Where(t => t != healTarget).All(p => !p.HasMyAura("Prayer of Mending"))),
                        Spell.Cast(
                            "Renew",
                            ret => healTarget,
                            ret => healTarget is WoWPlayer && Group.Tanks.Contains(healTarget) && !healTarget.HasMyAura("Renew")),
                        Spell.Cast("Prayer of Healing",
                            ret => healTarget,
                            ret => StyxWoW.Me.HasAura("Serendipity", 2) && Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() <= SingularSettings.Instance.Priest().PrayerOfHealingSerendipityHealth) >= SingularSettings.Instance.Priest().PrayerOfHealingSerendipityCount),
                        Spell.Cast("Circle of Healing",
                            ret => healTarget,
                            ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() <= SingularSettings.Instance.Priest().CircleOfHealingHealth) >= SingularSettings.Instance.Priest().CircleOfHealingCount),
                        Spell.CastOnGround(
                            "Holy Word: Sanctuary",
                            ret => Clusters.GetBestUnitForCluster(Unit.NearbyFriendlyPlayers.Select(p => p.ToUnit()), ClusterType.Radius, 10f).Location,
                            ret => Clusters.GetClusterCount(healTarget,
                                                            Unit.NearbyFriendlyPlayers.Select(p => p.ToUnit()),
                                                            ClusterType.Radius, 10f) >= 4 ),
                        Spell.Cast(
                            "Holy Word: Serenity",
                            ret => healTarget,
                            ret => ret is WoWPlayer && Group.Tanks.Contains(healTarget)),

                        Spell.Buff("Guardian Spirit",
                            ret => healTarget,
                            ret => healTarget.GetPredictedHealthPercent() <= 10),

                        Spell.CastOnGround("Lightwell",ret => WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, healTarget.Location, 5f)),

                        Spell.Cast("Power Infusion", ret => healTarget.GetPredictedHealthPercent() < 40 || StyxWoW.Me.ManaPercent <= 20),
                        Spell.Cast(
                            "Flash Heal",
                            ret => healTarget,
                            ret => StyxWoW.Me.HasAura("Surge of Light") && healTarget.GetPredictedHealthPercent() <= 90),
                        Spell.Cast(
                            "Flash Heal",
                            ret => healTarget,
                            ret => healTarget.GetPredictedHealthPercent() < SingularSettings.Instance.Priest().HolyFlashHeal),
                        Spell.Cast(
                            "Greater Heal",
                            ret => healTarget,
                            ret => healTarget.GetPredictedHealthPercent() < SingularSettings.Instance.Priest().HolyGreaterHeal),
                        Spell.Cast(
                            "Heal",
                            ret => healTarget,
                            ret => healTarget.GetPredictedHealthPercent() < SingularSettings.Instance.Priest().HolyHeal),
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
                    new Decorator(
                        ret => Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                        CreateHolyHealOnlyBehavior(false, true)),
                    new Decorator(
                        ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                        new PrioritySelector(
                            Spell.Cast("Flash Heal",
                                ctx => StyxWoW.Me,
                                ret => StyxWoW.Me.HealthPercent <= 20 // check actual health for low health situations
                                    || (!StyxWoW.Me.Combat && StyxWoW.Me.GetPredictedHealthPercent(true) <= 85)),

                            Spell.BuffSelf("Renew",
                                ret => StyxWoW.Me.GetPredictedHealthPercent(true) <= 75),

                            Spell.Cast("Greater Heal",
                                ctx => StyxWoW.Me,
                                ret => StyxWoW.Me.GetPredictedHealthPercent(true) <= 50),

                            Spell.Cast("Flash Heal",
                                ctx => StyxWoW.Me,
                                ret => StyxWoW.Me.GetPredictedHealthPercent(true) <= 50)
                            )
                        )
                    );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Priest, WoWSpec.PriestHoly)]
        public static Composite CreateHolyCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.BuffSelf( "Chakra: Sanctuary", ret => Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe)),
                        Spell.BuffSelf( "Chakra: Chastise", ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe))
                        )
                    )
                );
        }

        // This behavior is used in combat/heal AND pull. Just so we're always healing our party.
        // Note: This will probably break shit if we're solo, but oh well!
        [Behavior(BehaviorType.Combat | BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestHoly)]
        public static Composite CreateHolyCombatComposite()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                    new PrioritySelector(
                        Safers.EnsureTarget(),
                        Movement.CreateMoveToLosBehavior(),
                        Movement.CreateFaceTargetBehavior(),
                        Helpers.Common.CreateDismount("Pulling"),
                        Helpers.Common.CreateInterruptBehavior(),
                        Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                        Spell.Buff("Shadow Word: Pain", true),
                        Spell.Buff("Holy Word: Chastise", ret => StyxWoW.Me.HasAura( "Chakra: Chastise")),
                        Spell.Cast("Mindbender"),
                        Spell.Cast("Holy Fire"),
                        // Spell.Cast("Power Word: Solace", ret => StyxWoW.Me.ManaPercent < 15),
                        Spell.Cast("Smite"),
                        Movement.CreateMoveToTargetBehavior(true, 35f)
                        ))
                );
        }
    }
}
