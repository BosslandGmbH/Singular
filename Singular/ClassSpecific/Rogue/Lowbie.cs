using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Singular.Settings;

namespace Singular.ClassSpecific.Rogue
{
    public class Lowbie
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static RogueSettings RogueSettings { get { return SingularSettings.Instance.Rogue(); } }

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, 0)]
        public static Composite CreateLowbieRogueCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateAutoAttack(false),
                        Helpers.Common.CreateInterruptBehavior(),
                        Spell.Cast("Eviscerate", ret => StyxWoW.Me.ComboPoints == 5 || StyxWoW.Me.CurrentTarget.HealthPercent <= 40 && StyxWoW.Me.ComboPoints >= 2),
                        Spell.Cast("Sinister Strike")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Rogue, 0)]
        public static Composite CreateLowbieRoguePull()
        {

            return new PrioritySelector(
                Safers.EnsureTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && Me.GotTarget && Me.IsSafelyFacing(Me.CurrentTarget),
                    new PrioritySelector(
                        Common.CreateStealthBehavior(),
                        Common.CreateRogueOpenerBehavior(),
                        Spell.Cast("Sinister Strike"),
                        Helpers.Common.CreateAutoAttack(true)
                        )
                    )
                );
        }
    }
}
