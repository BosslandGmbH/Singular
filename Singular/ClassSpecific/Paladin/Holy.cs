using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Singular.ClassSpecific.Paladin
{
    public class Holy
    {
        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.HolyPaladin)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public static Composite CreateHolyPaladinRest()
        {
            return new PrioritySelector(
                // Heal self before resting. There is no need to eat while we have 100% mana
                CreatePaladinHealBehavior(true),
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour(),
                // Make sure we're healing OOC too!
                CreatePaladinHealBehavior(),
                // Can we res people?
                new Decorator(
                    ret => Unit.ResurrectablePlayers.Count != 0,
                    Spell.Cast("Redemption", ret => Unit.ResurrectablePlayers.FirstOrDefault()))
                );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.HolyPaladin)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateHolyPaladinCombatBehavior()
        {
            return
                new PrioritySelector(
                    CreatePaladinHealBehavior(),
                    Safers.EnsureTarget(),
                    Spell.Buff("Judgement", 
                                ret => SpellManager.HasSpell("Judgement") && 
                                       StyxWoW.Me.CurrentTarget.Distance <= SpellManager.Spells["Judgement"].MaxRange - 2 &&
                                       StyxWoW.Me.CurrentTarget.InLineOfSight &&
                                       StyxWoW.Me.IsSafelyFacing(StyxWoW.Me.CurrentTarget)),
                    new Decorator(
                        ret => !StyxWoW.Me.IsInParty && !StyxWoW.Me.IsInRaid,
                        new PrioritySelector(
                            Movement.CreateMoveToLosBehavior(),
                            Movement.CreateFaceTargetBehavior(),
                            Helpers.Common.CreateAutoAttack(true),
                            Spell.Buff("Judgement"),
                            Spell.Cast("Hammer of Wrath"),
                            Spell.Cast("Holy Shock"),
                            Spell.Cast("Crusader Strike"),
                            Spell.Cast("Exorcism"),
                            Spell.Cast("Holy Wrath"),
                            Spell.Cast("Consecration"),
                            Movement.CreateMoveToTargetBehavior(true, 5f)
                            ))
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.HolyPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateHolyPaladinPullBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.IsInParty && !StyxWoW.Me.IsInRaid,
                    new PrioritySelector(
                        Movement.CreateMoveToLosBehavior(),
                        Movement.CreateFaceTargetBehavior(),
                        Spell.Cast("Judgement"),
                        Helpers.Common.CreateAutoAttack(true),
                        Movement.CreateMoveToTargetBehavior(true, 5f)
                        ))
                );
        }

        private static Composite CreatePaladinHealBehavior()
        {
            return CreatePaladinHealBehavior(false);
        }

        private static Composite CreatePaladinHealBehavior(bool selfOnly)
        {
            HealerManager.NeedHealTargeting = true;

            return
                new PrioritySelector(
                    Waiters.WaitForCast(),
                    new Decorator(
                    ret => HealerManager.Instance.FirstUnit != null,
                        new PrioritySelector(
                            ret => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                            Spell.Buff(
                                "Beacon of Light",
                                ret => (WoWUnit)ret,
                                ret => (StyxWoW.Me.IsInParty || StyxWoW.Me.IsInRaid) && RaFHelper.Leader != null && (WoWUnit)ret == RaFHelper.Leader),
                            Spell.Cast(
                                "Lay on Hands",
                                ret => (WoWUnit)ret,
                                ret => StyxWoW.Me.Combat && !((WoWUnit)ret).HasAura("Forbearance") &&
                                       ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.LayOnHandsHealthHoly),
                            Spell.Cast(
                                "Light of Dawn",
                                ret => (WoWUnit)ret,
                                ret => StyxWoW.Me.CurrentHolyPower == 3 &&
                                       Unit.NearbyFriendlyPlayers.Count(p =>
                                           p.HealthPercent <= SingularSettings.Instance.Paladin.LightOfDawnHealth && p != StyxWoW.Me &&
                                           p.DistanceSqr < 30*30 && StyxWoW.Me.IsSafelyFacing(p.Location)) >= SingularSettings.Instance.Paladin.LightOfDawnCount),
                            Spell.Cast(
                                "Word of Glory",
                                ret => (WoWUnit)ret,
                                ret => StyxWoW.Me.CurrentHolyPower == 3 && ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.WordOfGloryHealth),
                            Spell.Cast(
                                "Holy Shock",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.HolyLightHealth),
                            Spell.Cast(
                                "Flash of Light",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.FlashOfLightHealth),
                            Spell.Cast(
                                "Divine Light",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.DivineLightHealth),
                            Spell.Cast(
                                "Holy Light",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.HolyLightHealth)
                            )));
        }
    }
}
