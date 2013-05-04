using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;
using CommonBehaviors.Actions;

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
                new Decorator(
                    req => !SpellManager.HasSpell("Cat Form"),
                    new PrioritySelector(
                        Helpers.Common.EnsureReadyToAttackFromLongRange(),
                        Spell.WaitForCast(true),
                        new Decorator(
                            req => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(
                                Helpers.Common.CreateAutoAttack(true),
                                Helpers.Common.CreateInterruptBehavior(),
                                Spell.Cast("Rejuvenation", on => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent <= DruidSettings.SelfRejuvenationHealth && StyxWoW.Me.HasAuraExpired("Rejuvenation", 1)),
                                Spell.Buff("Moonfire"),

                                //Pre Cat spells
                                Spell.Cast("Wrath")
                                )
                            ),
                        new ActionAlwaysSucceed()
                        )
                    ),


                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.EnsureReadyToAttackFromMelee(),

                        // rejuv will take us out of form if needed
                        Spell.Cast("Rejuvenation", on => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent <= DruidSettings.SelfRejuvenationHealth && StyxWoW.Me.HasAuraExpired("Rejuvenation", 1)),

                        // moonfire if already out of form
                        Spell.Buff("Moonfire", req => StyxWoW.Me.Shapeshift != ShapeshiftForm.Cat || StyxWoW.Me.CurrentTarget.Distance > 8),

                        Spell.BuffSelf("Cat Form"),
                        Helpers.Common.CreateInterruptBehavior(),
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
                            )
                        )
                    )
                );
        }
    }
}
