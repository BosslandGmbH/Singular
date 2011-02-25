using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx;
using Styx.Logic.Pathing;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular
{
    partial class SingularRoutine
    {
        /// <summary>Creates a behavior to move within range, within LOS, and keep facing the specified target.</summary>
        /// <remarks>Created 2/25/2011.</remarks>
        /// <param name="maxRange">The maximum range.</param>
        /// <param name="distanceFrom">The distance from.</param>
        /// <returns>.</returns>
        protected Composite CreateRangeAndFace(float maxRange, UnitSelectionDelegate distanceFrom)
        {
            return new Decorator(
                ret => distanceFrom(ret) != null,
                new PrioritySelector(
                    new Decorator(
                // Either get in range, or get in LOS.
                        ret => StyxWoW.Me.Location.DistanceSqr(distanceFrom(ret).Location) > maxRange * maxRange || !distanceFrom(ret).InLineOfSightOCD,
                        new Action(ret => Navigator.MoveTo(distanceFrom(ret).Location))),
                    new Decorator(
                        ret => Me.IsMoving && StyxWoW.Me.Location.DistanceSqr(distanceFrom(ret).Location) <= maxRange * maxRange,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new Decorator(
                        ret => Me.CurrentTarget != null && Me.CurrentTarget.IsAlive && !Me.IsSafelyFacing(Me.CurrentTarget, 70),
                        new Action(ret => Me.CurrentTarget.Face()))
                    ));
        }

        /// <summary>Creates a behavior to move to within LOS, and faces the specified target.</summary>
        /// <remarks>Created 2/25/2011.</remarks>
        /// <param name="unitToCheck">The unit to check.</param>
        /// <returns>.</returns>
        protected Composite CreateLosAndFace(UnitSelectionDelegate unitToCheck)
        {
            return CreateLosAndFace(unitToCheck, 70f);
        }

        /// <summary>Creates a behavior to move to within LOS, and faces the specified target.</summary>
        /// <remarks>Created 2/25/2011.</remarks>
        /// <param name="unitToCheck">The unit to check.</param>
        /// <param name="coneDegree">The cone degree, of how wide we should allow "facing", before we're no longer facing the target.</param>
        /// <returns>.</returns>
        protected Composite CreateLosAndFace(UnitSelectionDelegate unitToCheck, float coneDegree)
        {
            return
                new Decorator(
                    ret => unitToCheck(ret) != null,
                    new PrioritySelector(
                        new Decorator(
                            ret => !unitToCheck(ret).InLineOfSightOCD,
                            new Action(ret => Navigator.MoveTo(unitToCheck(ret).Location))),
                        new Decorator(
							ret => Me.CurrentTarget != null && Me.CurrentTarget.IsAlive && !Me.IsSafelyFacing(Me.CurrentTarget, coneDegree),
							new Action(ret => Me.CurrentTarget.Face()))
                ));
        }
    }
}
