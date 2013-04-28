using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.TreeSharp;

namespace Singular.ClassSpecific.Priest
{
    public class Lowbie
    {
        [Behavior(BehaviorType.Combat | BehaviorType.Pull, WoWClass.Priest, 0)]
        public static Composite CreateLowbiePriestCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Movement.CreateEnsureMovementStoppedBehavior(23f),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptBehavior(),

                Spell.BuffSelf("Power Word: Shield", ret => !StyxWoW.Me.HasAura("Weakened Soul")),
                Spell.Cast("Flash Heal", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent <= 40),

                Spell.Buff("Shadow Word: Pain"),
                Spell.Cast("Smite"),
                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 28f, 23f)
                );
        }
    }
}
