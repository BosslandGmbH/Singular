using Singular.Dynamics;
using Singular.Helpers;
using Singular.Lists;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;

using TreeSharp;
using Styx.Logic.Combat;

namespace Singular.ClassSpecific.Warrior
{
    public static class Lowbie
    {
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal)]
        public static Composite CreateLowbieWarriorCombat()
        {
            return new PrioritySelector(
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                new Decorator(
                    ret => !StyxWoW.Me.IsAutoAttacking,
                    new Action(ret => StyxWoW.Me.ToggleAttack())),
                // clear target
                new Decorator(
                    ret => !StyxWoW.Me.CurrentTarget.IsAlive &&
                            StyxWoW.Me.IsActuallyInCombat,
                    new Action(ret => StyxWoW.Me.ClearTarget())),
                // charge
                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance > 10 && StyxWoW.Me.CurrentTarget.Distance < 25),
                // move to melee
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.Distance > 6,
                    new PrioritySelector(
                        Movement.CreateMoveToTargetBehavior(true, 5f))),
                // Heal
                Spell.Cast("Victory Rush"),
                //rend
                new Decorator(
                    ret => !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Rend", 1) && StyxWoW.Me.CurrentTarget.HealthPercent > 50,
                    new PrioritySelector(
                        Spell.Buff("Rend"))),
                // AOE
                new Decorator(
                    ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 2,
                    new PrioritySelector(
                        Spell.Cast("Victory Rush", ret => StyxWoW.Me.HealthPercent < 80),
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Strike"))),
                // DPS
                Spell.Cast("Strike"),
                Spell.Cast("Thunder Clap", ret => StyxWoW.Me.RagePercent > 50),
                //move to melee
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }

        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal)]
        public static Composite CreateLowbieWarriorPull()
        {
            return new PrioritySelector(
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                new Decorator(
                    ret => !StyxWoW.Me.IsAutoAttacking,
                    new Action(ret => StyxWoW.Me.ToggleAttack())),
                // charge
                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance > 10 && StyxWoW.Me.CurrentTarget.Distance < 25),
                // move to melee
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }
    }
}
