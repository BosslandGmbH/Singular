using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using TreeSharp;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Lowbie
    {
        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbieDeathKnightCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.Cast("Death Grip", ret => StyxWoW.Me.CurrentTarget.Distance > 15),
                Spell.Cast("Death Coil"),
                Spell.Cast("Icy Touch"),
                Spell.Cast("Blood Strike"),
                Spell.Cast("Plague Strike"),
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }
    }
}
