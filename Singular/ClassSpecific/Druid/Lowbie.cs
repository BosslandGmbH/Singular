using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;

namespace Singular.ClassSpecific.Druid
{
    public class Lowbie
    {
        private static DruidSettings DruidSettings { get { return SingularSettings.Instance.Druid(); } }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        [Behavior(BehaviorType.Pull, WoWClass.Druid, 0)]
        public static Composite CreateLowbieDruidPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMediumRange(),
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.Buff("Entangling Roots", ret => !SpellManager.HasSpell("Cat Form")),
                        Spell.Buff("Moonfire", ret => SpellManager.HasSpell("Cat Form")),
                        Spell.Cast("Wrath")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, 0)]
        public static Composite CreateLowbieDruidCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Spell.Buff("Moonfire", req => StyxWoW.Me.Shapeshift != ShapeshiftForm.Cat ),

                        // Make sure we're in cat form first, period.
                        Spell.BuffSelf("Cat Form"),
                        Helpers.Common.CreateInterruptBehavior(),
                        //Healing if needed in combat
                        Spell.Cast("Rejuvenation", on => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent <= DruidSettings.SelfRejuvenationHealth && StyxWoW.Me.HasAuraExpired("Rejuvenation", 1)),
                        Helpers.Common.CreateAutoAttack(true),

                        new Decorator(
                            ret => StyxWoW.Me.HasAura("Cat Form"),
                            new PrioritySelector(
                                Spell.Buff("Rake", true),
                                Spell.Cast("Ferocious Bite", 
                                    ret => StyxWoW.Me.ComboPoints > 4 || 
                                           StyxWoW.Me.ComboPoints > 1 && StyxWoW.Me.CurrentTarget.HealthPercent < 40),
                                Spell.Cast("Mangle"),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        //Pre Cat spells
                        Spell.Cast("Wrath"),
                        Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 30f, 25f)
                        )
                    )
                );
        }
    }
}
