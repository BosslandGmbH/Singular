using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Singular.Settings;

namespace Singular.ClassSpecific.Rogue
{
    public class Lowbie
    {
        private static LocalPlayer Me => StyxWoW.Me;

	    [Behavior(BehaviorType.Combat, WoWClass.Rogue, 0)]
        public static Composite CreateLowbieRogueCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
						Spell.BuffSelf("Evasion", req => Me.HealthPercent < 40),
                        Helpers.Common.CreateInterruptBehavior(),
                        Spell.Cast("Envenom", ret => Me.ComboPoints == 5 || Me.CurrentTarget.HealthPercent <= 40 && Me.ComboPoints >= 2),
                        Spell.Cast("Mutilate")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Rogue, 0)]
        public static Composite CreateLowbieRoguePull()
        {

            return new PrioritySelector(
                Safers.EnsureTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && Me.GotTarget() && Me.IsSafelyFacing(Me.CurrentTarget),
                    new PrioritySelector(
                        Common.CreateStealthBehavior(),
                        Common.CreateRogueOpenerBehavior(),
                        Spell.Cast("Mutilate"))
                    )
                );
        }
    }
}
