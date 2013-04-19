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
                // Ensure Target
                Safers.EnsureTarget(),
                // LOS Check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),
                Helpers.Common.CreateInterruptBehavior(),
                // Heal
                Common.CreateVictoryRushBehavior(),

                // AOE
                new Decorator(
                    ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 2,
                    new PrioritySelector(
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Heroic Strike"))),
                // DPS
                Spell.Cast("Heroic Strike"),
                Spell.Cast("Thunder Clap", ret => StyxWoW.Me.RagePercent > 50),
                //move to melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, 0, WoWContext.Normal, 500)]
        public static Composite CreateLowbieWarriorPull()
        {
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                // LOS
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),
                // charge
                Spell.Cast("Charge",
                    ret => MovementManager.IsClassMovementAllowed 
                        && StyxWoW.Me.CurrentTarget.Distance > 10 
                        && StyxWoW.Me.CurrentTarget.Distance < 25),
                Spell.Cast("Throw", ret => StyxWoW.Me.CurrentTarget.IsFlying && Item.RangedIsType(WoWItemWeaponClass.Thrown)), Spell.Cast(
                    "Shoot",
                    ret =>
                    StyxWoW.Me.CurrentTarget.IsFlying && (Item.RangedIsType(WoWItemWeaponClass.Bow) || Item.RangedIsType(WoWItemWeaponClass.Gun))),
                // move to melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}
