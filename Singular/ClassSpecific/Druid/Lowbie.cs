using System;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;
using CommonBehaviors.Actions;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.Druid
{
    public class Lowbie
    {
        private static DruidSettings DruidSettings => SingularSettings.Instance.Druid();
	    private static LocalPlayer Me => StyxWoW.Me;


	    [Behavior(BehaviorType.Pull, WoWClass.Druid, 0)]
        public static Composite CreateLowbieDruidPull()
        {
            return new PrioritySelector(
				Common.CreateProwlBehavior(),
				Common.CreateMoveBehindTargetWhileProwling(),
				Helpers.Common.EnsureReadyToAttackFromMelee(ret => false),
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

						Spell.Cast("Rake"),
						Helpers.Common.EnsureReadyToAttackFromMelee(),
						Spell.Cast("Shred")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, 0)]
        public static Composite CreateLowbieDruidCombat()
        {
            return new PrioritySelector(

                new Action(r => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),

                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Common.CastForm( ShapeshiftForm.Cat, req => !Utilities.EventHandlers.IsShapeshiftSuppressed),
                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Spell.BuffSelf("Regrowth", ret => Me.HealthPercent < 60),
                        Spell.Cast("Moonfire", ret => Me.CurrentTarget.GetAuraTimeLeft("Moonfire") < TimeSpan.FromSeconds(2)),
                        Spell.Cast("Solar Wrath")
                        )
                    )
				);
        }
    }
}
