using System.Linq;
using System.Runtime.Remoting.Contexts;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.Paladin
{
    public static class Holy
    {
        [Behavior(BehaviorType.Rest,WoWClass.Paladin,WoWSpec.PaladinHoly)]
        public static Composite CreatePaladinHolyRest()
        {
            return new PrioritySelector(
                // Heal self before resting. There is no need to eat while we have 100% mana
                CreatePaladinHealBehavior(true),
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour(),
                // Can we res people?
                Spell.Resurrect("Redemption"),
                // Make sure we're healing OOC too!
                CreatePaladinHealBehavior(false, false));
        }
        [Behavior(BehaviorType.Heal, WoWClass.Paladin, WoWSpec.PaladinHoly)]
        public static Composite CreatePaladinHolyHealBehavior()
        {
            return
                new PrioritySelector(
                    CreatePaladinHealBehavior());
        }
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Paladin, WoWSpec.PaladinHoly)]
        public static Composite CreatePaladinHolyCombatBuffsBehavior()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf(
                        "Divine Plea",
                        ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Paladin.DivinePleaMana)
                    );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinHoly)]
        public static Composite CreatePaladinHolyCombatBehavior()
        {
            return
                new PrioritySelector(
                    Spell.Buff("Judgment",
                               ret => SpellManager.HasSpell("Judgment") && StyxWoW.Me.GotTarget &&
                                       StyxWoW.Me.CurrentTarget.Distance <= SpellManager.Spells["Judgment"].MaxRange - 2 &&
                                       StyxWoW.Me.CurrentTarget.InLineOfSpellSight &&
                                       StyxWoW.Me.IsSafelyFacing(StyxWoW.Me.CurrentTarget)),
                    new Decorator(
                        ret => Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0,
                        new PrioritySelector(
                            Safers.EnsureTarget(),
                            Movement.CreateMoveToLosBehavior(),
                            Movement.CreateFaceTargetBehavior(),
                            Helpers.Common.CreateAutoAttack(true),
                            Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                            Spell.Buff("Judgment"),
                            Spell.Cast("Hammer of Wrath"),
                            Spell.Cast("Holy Shock"),
                            Spell.Cast("Crusader Strike"),
                            Spell.Cast("Denounce"),
                            Movement.CreateMoveToTargetBehavior(true, 5f)
                            ))
                    );
        }
        [Behavior(BehaviorType.Pull, WoWClass.Paladin, WoWSpec.PaladinHoly)]
        public static Composite CreatePaladinHolyPullBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.GroupInfo.IsInParty && !StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        Movement.CreateMoveToLosBehavior(),
                        Movement.CreateFaceTargetBehavior(),
                        Spell.Cast("Judgment"),
                        Helpers.Common.CreateAutoAttack(true),
                        Movement.CreateMoveToTargetBehavior(true, 5f)
                        ))
                );
        }

        internal static Composite CreatePaladinHealBehavior()
        {
            return CreatePaladinHealBehavior(false, true);
        }

        internal static Composite CreatePaladinHealBehavior(bool selfOnly)
        {
            return CreatePaladinHealBehavior(selfOnly, false);
        }

        internal static Composite CreatePaladinHealBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;

            return
                new PrioritySelector(
                    ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                    new Decorator(
                    ret => ret != null && (moveInRange || ((WoWUnit)ret).InLineOfSpellSight && ((WoWUnit)ret).DistanceSqr < 40 * 40),
                        new PrioritySelector(
                            Spell.WaitForCast(),
                            new Decorator(
                                ret => moveInRange,
                                Movement.CreateMoveToLosBehavior(ret => (WoWUnit)ret)),
                            Spell.Cast(
                                "Beacon of Light",
                                ret => (WoWUnit)ret,
                                ret => ret is WoWPlayer && Group.Tanks.Contains((WoWPlayer)ret) && Group.Tanks.All(t => !t.HasMyAura("Beacon of Light"))),



                                //Try and keep it up if requested
                             Spell.Heal(
                                "Eternal Flame",
                                ret => (WoWUnit)ret,
                               ret => ret is WoWPlayer && SingularSettings.Instance.Paladin.KeepEternalFlameUp && Group.Tanks.Contains((WoWPlayer)ret) && Group.Tanks.All(t => !t.HasMyAura("Eternal Flame"))),

                            Spell.Heal(
                                "Eternal Flame",
                                ret => (WoWUnit)ret,
                               ret => StyxWoW.Me.CurrentHolyPower >= 3 && (((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.WordOfGloryHealth)),

                           Spell.Heal(
                                "Lay on Hands",
                                ret => (WoWUnit)ret,
                                ret => StyxWoW.Me.Combat && !((WoWUnit)ret).HasAura("Forbearance") &&
                                       ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.LayOnHandsHealth),
                            Spell.Heal(
                                "Light of Dawn",
                                ret => StyxWoW.Me,
                                ret => StyxWoW.Me.CurrentHolyPower >= 3 &&
                                       Unit.NearbyFriendlyPlayers.Count(p =>
                                           p.HealthPercent <= SingularSettings.Instance.Paladin.LightOfDawnHealth && p != StyxWoW.Me &&
                                           p.DistanceSqr < 30 * 30 && StyxWoW.Me.IsSafelyFacing(p.Location)) >= SingularSettings.Instance.Paladin.LightOfDawnCount),
                            Spell.Heal(
                                "Word of Glory",
                                ret => (WoWUnit)ret,
                                ret => StyxWoW.Me.CurrentHolyPower >= 3 && ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.WordOfGloryHealth),
                            Spell.Heal(
                                "Holy Shock",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.HolyShockHealth),
                            Spell.Heal(
                                "Flash of Light",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.FlashOfLightHealth),
                            Spell.Heal(
                                "Divine Light",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.DivineLightHealth),
                            Spell.Heal(
                                "Holy Light",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.HolyLightHealth),
                            new Decorator(
                                ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0,
                                new PrioritySelector(
                                    Movement.CreateMoveToLosBehavior(),
                                    Movement.CreateFaceTargetBehavior(),
                                    Helpers.Common.CreateAutoAttack(true),
                                    Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                                    Spell.Buff("Judgment"),
                                    Spell.Cast("Hammer of Wrath"),
                                    Spell.Cast("Holy Shock"),
                                    Spell.Cast("Crusader Strike"),
                                    Spell.Cast("Denounce"),
                                    Movement.CreateMoveToTargetBehavior(true, 5f)
                                    )),
                            new Decorator(
                                ret => moveInRange,
                // Get in range
                                Movement.CreateMoveToTargetBehavior(true, 35f, ret => (WoWUnit)ret))
                            )));
        }
    }
}
