using System.Linq;

using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateLowbieRogueCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateAutoAttack(true),
                CreateMoveToAndFace(),
                CreateSpellCast("Eviscerate", ret => Me.ComboPoints == 5 || Me.CurrentTarget.HealthPercent <= 40 && Me.ComboPoints >= 2),
                CreateSpellCast("Sinister Strike")
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateLowbieRoguePull()
        {
            return new PrioritySelector(
                CreateSpellBuffOnSelf("Stealth"),
                CreateAutoAttack(true),
                CreateMoveToAndFace()
                );
        }
    }
}