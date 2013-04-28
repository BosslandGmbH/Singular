using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;

namespace Singular.ClassSpecific.Mage
{
    public class Lowbie
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MageSettings MageSettings { get { return SingularSettings.Instance.Mage(); } }

        [Behavior(BehaviorType.Pull, WoWClass.Mage, 0)]
        public static Composite CreateLowbieMagePull()
        {
            return new PrioritySelector(
                 Safers.EnsureTarget(),
                 Common.CreateStayAwayFromFrozenTargetsBehavior(),
                 Movement.CreateMoveToLosBehavior(),
                 Movement.CreateFaceTargetBehavior(),
                 Helpers.Common.CreateDismount("Pulling"),
                 Movement.CreateEnsureMovementStoppedBehavior(33f),
                 Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                         Helpers.Common.CreateInterruptBehavior(),
                         Common.CreateMagePolymorphOnAddBehavior(),

                         Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 12 * 12 && Me.CurrentTarget.Combat && Me.CurrentTarget.IsTargetingMeOrPet)),
                         // only Fire Blast if already in Combat
                         Spell.Cast("Fire Blast", ret => Me.CurrentTarget.Combat && Me.CurrentTarget.IsTargetingMeOrPet),
                         // otherwise take advantage of casting without incoming damage
                         Spell.Cast("Frostfire Bolt")
                         )
                     ),

                 Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                 );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Mage, 0)]
        public static Composite CreateLowbieMageCombat()
        {
            return new PrioritySelector(
                 Safers.EnsureTarget(),
                 Common.CreateStayAwayFromFrozenTargetsBehavior(),
                 Movement.CreateMoveToLosBehavior(),
                 Movement.CreateFaceTargetBehavior(),
                 Helpers.Common.CreateDismount("Pulling"),
                 // Movement.CreateEnsureMovementStoppedBehavior(33f),
                 Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                         Helpers.Common.CreateInterruptBehavior(),
                         Common.CreateMagePolymorphOnAddBehavior(),

                         Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 12 * 12 && Me.CurrentTarget.Combat && Me.CurrentTarget.IsTargetingMeOrPet)),
                         Spell.Cast("Fire Blast"),
                         Spell.Cast("Frostfire Bolt")
                         )
                     ),

                 Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                 );
        }
    }
}
