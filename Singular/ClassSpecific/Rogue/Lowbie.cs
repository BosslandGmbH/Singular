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

	    [Behavior(BehaviorType.Pull|BehaviorType.Combat, WoWClass.Rogue, 0)]
        public static Composite CreateLowbieRogueCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),
                        Spell.Cast("Eviscerate", ret => Me.ComboPoints > 3),
                        Spell.Cast("Sinister Strike")
                        )
                    )
                );
        }
    }
}
