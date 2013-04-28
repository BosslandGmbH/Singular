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
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.Priest
{
    public class Discipline
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PriestSettings PriestSettings { get { return SingularSettings.Instance.Priest(); } }
        public static bool HasTalent(PriestTalents tal) { return TalentManager.IsSelected((int)tal); }

        [Behavior(BehaviorType.Rest,WoWClass.Priest,WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscHealRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        // Heal self before resting. There is no need to eat while we have 100% mana
                        CreateDiscHealOnlyBehavior(true),
                        // Rest up damnit! Do this first, so we make sure we're fully rested.
                        Rest.CreateDefaultRestBehaviour( null, "Resurrection"),
                        // Make sure we're healing OOC too!
                        CreateDiscHealOnlyBehavior(false, false),
                        // now buff our movement if possible
                        Common.CreatePriestMovementBuff("Rest")
                        )
                    )
                );

        }

        public static Composite CreateDiscHealOnlyBehavior()
        {
            return CreateDiscHealOnlyBehavior(false, true);
        }

        public static Composite CreateDiscHealOnlyBehavior(bool selfOnly)
        {
            return CreateDiscHealOnlyBehavior(selfOnly, false);
        }

        private static WoWUnit _healTarget;

        public static Composite CreateDiscHealOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;
            return new
                PrioritySelector(
                    ret => _healTarget = (selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit),

                    new Decorator(
                        ret => ret != null && (moveInRange || ((WoWUnit)ret).InLineOfSpellSight && ((WoWUnit)ret).DistanceSqr < 40 * 40),
                        new PrioritySelector(
                        Spell.WaitForCast(),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToLosBehavior(ret => (WoWUnit)ret)),
                        
                            // Ensure we're in range of the unit to heal, and it's in LOS.
                            // //CreateMoveToAndFace(35f, ret => (WoWUnit)ret),
                            // //Spell.Buff("Renew", ret => HealTargeting.Instance.TargetList.FirstOrDefault(u => !u.HasAura("Renew") && u.HealthPercent < 90) != null, ret => HealTargeting.Instance.TargetList.FirstOrDefault(u => !u.HasAura("Renew") && u.HealthPercent < 90)),
                        
                        // Cast prayer of healing whenever we have spirit shell buffed and any parymember has less than 80% HP
                        Spell.BuffSelf(
                            "Prayer of Healing",
                            ret => StyxWoW.Me.HasAura("Spirit Shell") &&
                                Unit.NearbyGroupMembers.Count(m => m.DistanceSqr <= 30f * 30f && m.HealthPercent <= SingularSettings.Instance.Priest().SpiritShell) != 0
                        ),
                        
                        // Same condition as above more or less.
                        Spell.Cast(
                        "Spirit Shell",
                        ret => StyxWoW.Me.Combat && Unit.NearbyGroupMembers.Count(m => m.DistanceSqr <= 30f * 30f && m.HealthPercent <= SingularSettings.Instance.Priest().SpiritShell) != 0
                        ),
                        
                        // Buff with Archangel once we get 5 stacks of Evangelism!
                        Spell.BuffSelf(
                        "Archangel",
                        ret => StyxWoW.Me.HasAura("Evangelism", 5)
                        ),

                        // Mana check is for mana management. Don't mess with it
                        Common.CreateShadowfiendBehavior(),

                        // Cast Hymn of Hope if we are low on mana.
                        Spell.BuffSelf(
                            "Hymn of Hope",
                            ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Priest().HymnofHopeMana),

                        // use fade to drop aggro.
                        Spell.Cast("Fade", ret => (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid) && StyxWoW.Me.CurrentMap.IsInstance && Targeting.GetAggroOnMeWithin(StyxWoW.Me.Location, 30) > 0),
                        Spell.Cast("Desperate Prayer", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 30),
                        Spell.Buff(
                            "Power Word: Shield",
                            ret => (WoWUnit)ret,
                            ret => !((WoWUnit)ret).HasAura("Weakened Soul") && ((WoWUnit)ret).Combat && ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest().PowerWordShield),
                        new Decorator(
                            ret =>
                            Unit.NearbyFriendlyPlayers.Count(p => !p.IsDead && p.HealthPercent < SingularSettings.Instance.Priest().PrayerOfHealing) >
                            SingularSettings.Instance.Priest().PrayerOfHealingCount &&
                            (SpellManager.CanCast("Prayer of Healing") || SpellManager.CanCast("Divine Hymn")),
                            new Sequence(
                                Spell.Cast("Power Infusion", ret => ((WoWUnit)ret).HealthPercent < 40 || StyxWoW.Me.ManaPercent <= 20),
                                Spell.Cast("Archangel"),
                        // This will skip over DH if we can't cast it.
                        // If we can, the sequence fails, since PoH can't be cast (as we're still casting at this point)
                                new DecoratorContinue(
                                    ret => SpellManager.CanCast("Divine Hymn"),
                                    Spell.Cast("Divine Hymn")),
                                Spell.Cast("Prayer of Healing"))),
                        Spell.Cast(
                            "Pain Suppression",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest().PainSuppression),
                        Spell.Cast(
                            "Penance",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest().Penance),
                        Spell.Cast("Desperate Prayer", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 30),
                        Spell.Cast(
                            "Flash Heal",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest().FlashHeal),
                        Spell.Cast(
                            "Binding Heal",
                            ret => (WoWUnit)ret,
                            ret => (WoWUnit)ret != StyxWoW.Me && ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest().BindingHealThem &&
                                   StyxWoW.Me.HealthPercent < SingularSettings.Instance.Priest().BindingHealMe),
                        Spell.Cast(
                            "Greater Heal",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest().GreaterHeal),
                        Spell.Cast(
                            "Heal",
                            ret => (WoWUnit)ret, 
                            ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest().HolyHealPercent),
                        Spell.Cast(
                            "Renew",
                            ret => (WoWUnit)ret, 
                            ret => !((WoWUnit)ret).HasMyAura("Renew") && ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Priest().Renew),
                        Spell.Cast(
                            "Prayer of Mending",
                            ret => (WoWUnit)ret,
                            ret => !((WoWUnit)ret).HasMyAura("Prayer of Mending") && ((WoWUnit)ret).HealthPercent < 90),
                        new Decorator(
                            ret => StyxWoW.Me.Combat && StyxWoW.Me.ManaPercent > SingularSettings.Instance.Priest().DpsMana,
                            new PrioritySelector(
                                Safers.EnsureTarget(),
                                Movement.CreateMoveToLosBehavior(),
                                Movement.CreateFaceTargetBehavior(),
                                Helpers.Common.CreateInterruptBehavior(),
                                Common.CreateShadowfiendBehavior(),
                                Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                                Spell.Buff("Shadow Word: Pain", true),
                                Spell.Cast("Penance"),
                                Spell.Cast("Holy Fire"),
                                // Spell.Cast("Power Word: Solace", ret => StyxWoW.Me.ManaPercent < 15),
                                Spell.Cast("Smite"),
                                //Spell.Cast("Mind Spike", ret => !SpellManager.HasSpell("Power Word: Solace")),
                                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 35f, 30f)
                                )),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToUnitBehavior( on => _healTarget, 35f, 30f)

                        // Divine Hymn
                        // Desperate Prayer
                        // Prayer of Mending
                        // Prayer of Healing
                        // Power Word: Barrier
                        // TODO: Add smite healing. Atonement is now specced as all discs. (Its useless otherwise)
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Priest, WoWSpec.PriestDiscipline )]
        public static Composite CreateDiscHealComposite()
        {
            return new PrioritySelector(
                // group healing
                new Decorator(
                    ret => Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                    CreateDiscHealOnlyBehavior(false, true)),
                // solo healing
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

        // This behavior is used in combat/heal AND pull. Just so we're always healing our party.
        // Note: This will probably break shit if we're solo, but oh well!
        [Behavior(BehaviorType.Combat|BehaviorType.Pull,WoWClass.Priest,WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscCombatComposite()
        {
            return new Decorator(
                ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateDismount("Pulling"),
                    Movement.CreateEnsureMovementStoppedBehavior(25f),
                    Helpers.Common.CreateInterruptBehavior(),
                    Common.CreateShadowfiendBehavior(),
                    //Spell.BuffSelf("Archangel", ret => StyxWoW.Me.HasAura("Evangelism", 5)),
                    Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                    Spell.Buff("Shadow Word: Pain", true),
                    Spell.Cast("Penance"),
                    Spell.Cast("Holy Fire"),
                    // Spell.Cast("Power Word: Solace", ret => StyxWoW.Me.ManaPercent < 15),
                    Spell.Cast("Smite"),
                //Spell.Cast("Mind Spike", ret => !SpellManager.HasSpell("Power Word: Solace")),
                    Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 30f, 25f)
                    )
                );
        }
    }
}
