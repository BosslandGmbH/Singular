using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using TreeSharp;

namespace Singular.ClassSpecific.Monk
{
    /*  Basic low level monk class routine by Laria. Commented out until MOP release.
    public class Lowbie
    {
        [Class(WoWClass.Monk)] 
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbieMonkCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.Cast("BlackoutKick", ret => StyxWoW.Me.Chi >= 1 || StyxWoW.Me.CurrentTarget.HealthPercent <= 50),
				Spell.Cast("TigerPalm", ret => StyxWoW.Me.Chi>= 1),
				Spell.Cast("Jab"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Class(WoWClass.Monk)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbieMonkPull()
        {
            return new PrioritySelector(
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
				Spell.Cast("Roll", ret => StyxWoW.Me.CurrentTarget.Distance > 10),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
     */
}
