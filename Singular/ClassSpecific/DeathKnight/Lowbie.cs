using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Pathing;
using Styx.TreeSharp;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Lowbie
    {
        [Behavior(BehaviorType.Combat,WoWClass.DeathKnight)]
        public static Composite CreateLowbieDeathKnightCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // Anti-magic shell - no cost and doesnt trigger GCD 
                    Spell.BuffSelf("Anti-Magic Shell",
                                    ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                                u.CurrentTargetGuid == StyxWoW.Me.Guid)),

                new Sequence(
                    Spell.Cast("Death Grip",
                                ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),
                Spell.Cast("Death Coil"),
                Spell.Buff("Icy Touch", true, "Frost Fever"),
                Spell.Buff("Plague Strike", true, "Blood Plague"),
                Spell.Cast("Blood Strike"),
                Spell.Cast("Icy Touch"),
                Spell.Cast("Plague Strike"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}
