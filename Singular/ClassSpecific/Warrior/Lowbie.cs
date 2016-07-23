using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;


using Styx.TreeSharp;
using Singular.Settings;

namespace Singular.ClassSpecific.Warrior
{
    public class Lowbie
    {
        [Behavior(BehaviorType.Combat, WoWClass.Warrior, 0, WoWContext.Normal)]
        public static Composite CreateLowbieWarriorCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateChargeBehavior(),

                        // Heal
                        Common.CreateVictoryRushBehavior(),

                        // AOE
                        new Decorator(
                            ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f) >= 2,
                            new PrioritySelector(
                                Spell.Cast("Thunder Clap", req => Spell.UseAOE)
                                )
                            ),
                        // DPS
                        Spell.Cast("Slam"),
                        Spell.Cast("Execute"),
                        Spell.Cast("Thunder Clap", ret => Spell.UseAOE && StyxWoW.Me.RagePercent > 50 && StyxWoW.Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8))

                        )
                    )
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, 0, WoWContext.Normal, 500)]
        public static Composite CreateLowbieWarriorPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Common.CreateAttackFlyingOrUnreachableMobs(),
                Common.CreateChargeBehavior(),

                // move to melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}
