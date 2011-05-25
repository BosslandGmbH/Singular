using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public static class Lowbie
    {
        [Spec(TalentSpec.Lowbie)]
        [Class(WoWClass.Druid)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Heal)]
        public static Composite CreateLowbieDruidCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Cat;
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                // Make sure we're in cat form first, period.
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift != Common.WantedDruidForm,
                    Spell.Cast("Cat Form")),
                //Healing if needed in combat
                Spell.BuffSelf("Rejuvenation", ret => StyxWoW.Me.HealthPercent <= 60),
                Helpers.Common.CreateAutoAttack(true),
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Cat,
                    new PrioritySelector(
                        Spell.Cast("Rake", ret => !StyxWoW.Me.CurrentTarget.HasAura("Rake") || StyxWoW.Me.CurrentTarget.GetAuraByName("Rake").CreatorGuid != StyxWoW.Me.Guid),
                        Spell.Cast("Ferocious Bite", ret => StyxWoW.Me.ComboPoints > 4 || StyxWoW.Me.ComboPoints > 0 && StyxWoW.Me.CurrentTarget.HealthPercent < 40),
                        Spell.Cast("Claw"))),
                //Pre Cat spells
                Spell.Cast("Moonfire", ret => !StyxWoW.Me.HasAura("Cat Form") && !StyxWoW.Me.CurrentTarget.HasAura("Moonfire")),
                Spell.Cast("Wrath", ret => !StyxWoW.Me.HasAura("Cat Form")),
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }

        [Spec(TalentSpec.Lowbie)]
        [Class(WoWClass.Druid)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Pull)]
        public static Composite CreateLowbiePull()
        {
            Common.WantedDruidForm = ShapeshiftForm.Cat;
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                // Make sure we're in cat form first, period.
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift != Common.WantedDruidForm,
                    Spell.Cast("Cat Form")),
                Helpers.Common.CreateAutoAttack(true),
                Spell.Cast("Rake", ret => (!StyxWoW.Me.CurrentTarget.HasAura("Rake") || StyxWoW.Me.CurrentTarget.GetAuraByName("Rake").CreatorGuid != StyxWoW.Me.Guid) && SpellManager.HasSpell("Cat Form")),
                Spell.Cast("Starfire", ret => !StyxWoW.Me.HasAura("Cat Form")),
                Spell.Cast("Wrath", ret => !StyxWoW.Me.HasAura("Cat Form")),
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }
    }
}
