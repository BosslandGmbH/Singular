using Singular.Dynamics;
using Singular.Managers;

using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular.ClassSpecific.Priest
{
    public class Shadow
    {
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.Combat | BehaviorType.Pull)]
        [Class(WoWClass.Priest)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreatePriestShadowCombat()
        {
            return new PrioritySelector();
        }
    }
}
