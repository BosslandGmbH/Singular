using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;
using System.Drawing;
using Styx.WoWInternals;

namespace Singular.ClassSpecific.Mage
{
    public class Lowbie
    {
        private static bool LowbieNeedsFrostNova
        {
            get {
                return Unit.UnfriendlyUnits(12).Any(u => u.IsHostile || (u.Combat && u.IsTargetingMyStuff()))
                    && !Unit.UnfriendlyUnits(12).Any(u => !u.Combat && u.IsNeutral); 
            }
        }

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, 0)]
        public static Composite CreateLowbieMageCombat()
        {
            return new PrioritySelector(
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
					new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),
                        Common.CreateMageAvoidanceBehavior(),

						Movement.WaitForFacing(),
						Movement.WaitForLineOfSpellSight(),

                        Spell.BuffSelf("Frost Nova", ret => LowbieNeedsFrostNova),
                        Spell.Cast("Ice Lance", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.IsFrozen())),
                        Spell.Cast("Frostbolt")
                        )
                    )
                );
        }
    }
}
