using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using TreeSharp;

namespace Singular.ClassSpecific.Rogue
{
    public class Lowbie
    {
        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbieRogueCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.Cast("Eviscerate", ret => StyxWoW.Me.ComboPoints == 5 || StyxWoW.Me.CurrentTarget.HealthPercent <= 40 && StyxWoW.Me.ComboPoints >= 2),
                Spell.Cast("Sinister Strike"),
                Movement.CreateMoveToTargetBehavior(true,5f)
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbieRoguePull()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Stealth"),
                Helpers.Common.CreateAutoAttack(true),
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }
    }
}
