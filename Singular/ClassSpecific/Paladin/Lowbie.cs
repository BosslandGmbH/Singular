using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.TreeSharp;


namespace Singular.ClassSpecific.Paladin
{
    public class Lowbie
    {
        [Behavior(BehaviorType.Pull, WoWClass.Paladin, 0)]
        public static Composite CreateLowbiePaladinPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Spell.Cast("Judgment"),
                        Spell.Cast("Crusader Strike")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Paladin, 0)]
        public static Composite CreateLowbiePaladinCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),
                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Spell.Cast("Crusader Strike"),
                        Spell.Cast("Judgment")
                        )
                    )
                );
        }
    }
}
