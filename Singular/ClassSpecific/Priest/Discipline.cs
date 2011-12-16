using System;
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
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Priest
{
    public class Discipline
    {
        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.DisciplineHealingPriest)]
        [Spec(TalentSpec.DisciplinePriest)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public static Composite CreateDiscHealRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                // Heal self before resting. There is no need to eat while we have 100% mana
                CreateDiscHealOnlyBehavior(true),
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour(),
                // Can we res people?
                Spell.Resurrect("Resurrection"),
                // Make sure we're healing OOC too!
                CreateDiscHealOnlyBehavior(false, false)
                );
        }

        public static Composite CreateDiscHealOnlyBehavior()
        {
            return CreateDiscHealOnlyBehavior(false, false);
        }

        public static Composite CreateDiscHealOnlyBehavior(bool selfOnly)
        {
            return CreateDiscHealOnlyBehavior(selfOnly, false);
        }

        public static Composite CreateDiscHealOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            // Atonement - Tab 1  index 10 - 1/2 pts
            HealerManager.NeedHealTargeting = true;
            return new
                PrioritySelector(
                ret => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                    new Decorator(
                        ret => ret != null,
                        new PrioritySelector(
                        Spell.WaitForCast(),
                        // Ensure we're in range of the unit to heal, and it's in LOS.
                        //CreateMoveToAndFace(35f, ret => (WoWUnit)ret),
                        //Spell.Buff("Renew", ret => HealTargeting.Instance.TargetList.FirstOrDefault(u => !u.HasAura("Renew") && u.HealthPercent < 90) != null, ret => HealTargeting.Instance.TargetList.FirstOrDefault(u => !u.HasAura("Renew") && u.HealthPercent < 90)),
                        Spell.Buff(
                            "Power Word: Shield",
                            ret => (WoWUnit)ret, 
                            ret => !((WoWUnit)ret).HasAura("Weakened Soul") && ((WoWUnit)ret).Combat),
                        new Decorator(
                            ret =>
                            Unit.NearbyFriendlyPlayers.Count(p => !p.Dead && p.HealthPercent < SingularSettings.Instance.Priest.PrayerOfHealing) >
                            SingularSettings.Instance.Priest.PrayerOfHealingCount &&
                            (SpellManager.CanCast("Prayer of Healing") || SpellManager.CanCast("Divine Hymn")),
                            new Sequence(
                                Spell.Cast("Archangel"),
                        // This will skip over DH if we can't cast it.
                        // If we can, the sequence fails, since PoH can't be cast (as we're still casting at this point)
                                new DecoratorContinue(
                                    ret => SpellManager.CanCast("Divine Hymn"),
                                    Spell.Cast("Divine Hymn")),
                                Spell.Cast("Prayer of Healing"))),
                        Spell.Buff(
                            "Pain Supression",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest.PainSuppression),
                        Spell.Buff(
                            "Penance",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest.Penance),
                        Spell.Cast(
                            "Flash Heal",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest.FlashHeal),
                        Spell.Cast(
                            "Binding Heal",
                            ret => (WoWUnit)ret,
                            ret => (WoWUnit)ret != StyxWoW.Me && ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest.BindingHealThem &&
                                   StyxWoW.Me.HealthPercent < SingularSettings.Instance.Priest.BindingHealMe),
                        Spell.Cast(
                            "Greater Heal",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest.GreaterHeal),
                        Spell.Cast(
                            "Heal",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest.Heal),
                        Spell.Buff(
                            "Renew",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest.Renew),
                        Spell.Buff(
                            "Prayer of Mending",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < 90),

                        new Decorator(
                            ret => moveInRange,
                            new PrioritySelector(
                                Movement.CreateMoveToLosBehavior(ret => (WoWUnit)ret),
                                Movement.CreateMoveToTargetBehavior(true, 35f, ret => (WoWUnit)ret)))

                        // Divine Hymn
                        // Desperate Prayer
                        // Prayer of Mending
                        // Prayer of Healing
                        // Power Word: Barrier
                        // TODO: Add smite healing. Only if Atonement is talented. (Its useless otherwise)
                        )));
        }

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.DisciplineHealingPriest)]
        [Spec(TalentSpec.DisciplinePriest)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.All)]
        public static Composite CreateDiscHealComposite()
        {
            return
                new PrioritySelector(
                    CreateDiscHealOnlyBehavior());
        }

        // This behavior is used in combat/heal AND pull. Just so we're always healing our party.
        // Note: This will probably break shit if we're solo, but oh well!
        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.DisciplineHealingPriest)]
        [Spec(TalentSpec.DisciplinePriest)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateDiscCombatComposite()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                //Pull stuff
                new Decorator(
                    ret => !StyxWoW.Me.IsInParty && !StyxWoW.Me.Combat,
                    new PrioritySelector(
                        Movement.CreateMoveToLosBehavior(),
                        Movement.CreateFaceTargetBehavior(),
                        Spell.Cast("Holy Fire", ret => !StyxWoW.Me.IsInParty && !StyxWoW.Me.Combat),
                        Spell.Cast("Smite", ret => !StyxWoW.Me.IsInParty && !StyxWoW.Me.Combat),
                        Movement.CreateMoveToTargetBehavior(true, 28f)
                        )),
                // If we have nothing to heal, and we're in combat (or the leader is)... kill something!
                new Decorator(
                    ret => StyxWoW.Me.Combat || (RaFHelper.Leader != null && RaFHelper.Leader.Combat),
                    new PrioritySelector(
                        Movement.CreateMoveToLosBehavior(),
                        Movement.CreateFaceTargetBehavior(),
                        Spell.Buff("Shadow Word: Pain", ret => !StyxWoW.Me.IsInParty || StyxWoW.Me.ManaPercent >= SingularSettings.Instance.Priest.DpsMana),
                //Solo combat rotation
                        new Decorator(
                            ret => !StyxWoW.Me.IsInParty,
                            new PrioritySelector(
                                Spell.Cast("Holy Fire"),
                                Spell.Cast("Penance"))),
                //Don't smite while mana is below the setting while in a party (default 70)
                        Spell.Cast("Smite", ret => !StyxWoW.Me.IsInParty || StyxWoW.Me.ManaPercent >= SingularSettings.Instance.Priest.DpsMana),
                        Movement.CreateMoveToTargetBehavior(true, 35f)
                        ))
                );
        }
    }
}
