using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;

namespace Singular.ClassSpecific.Mage
{
    public class Lowbie
    {
        [Behavior(BehaviorType.Combat|BehaviorType.Pull,WoWClass.Mage,0)]
        public static Composite CreateLowbieMageCombat()
        {
            return new PrioritySelector(
                 Safers.EnsureTarget(),
                 Common.CreateStayAwayFromFrozenTargetsBehavior(),
                 Movement.CreateMoveToLosBehavior(),
                 Movement.CreateFaceTargetBehavior(),
                 Helpers.Common.CreateDismount("Pulling"),
                 Spell.WaitForCast(true),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                         Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                         Common.CreateMagePolymorphOnAddBehavior(),

                         Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 12 * 12)),
                         Spell.Cast("Fire Blast", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 10),
                         Spell.Cast("Frostfire Bolt")
                         )
                     ),

                 Movement.CreateMoveToTargetBehavior(true, 38f)
                 );
        }
    }
}
