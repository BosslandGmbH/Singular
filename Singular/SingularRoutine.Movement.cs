#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author$
// $Date$
// $HeadURL$
// $LastChangedBy$
// $LastChangedDate$
// $LastChangedRevision$
// $Revision$

#endregion

using Singular.Settings;

using Styx.Logic.Pathing;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        /// <summary>
        ///   Creates a behavior to move within range, within LOS, and keep facing the specified target.
        /// </summary>
        /// <remarks>
        ///   Created 3/4/2011.
        /// </remarks>
        /// <param name = "maxRange">The maximum range.</param>
        /// <param name = "coneDegrees">The cone in degrees. (If we're facing +/- this many degrees from the target, we will face the target)</param>
        /// <param name = "unit">The unit.</param>
        /// <param name="noMovement"></param>
        /// <returns>.</returns>
        protected Composite CreateMoveToAndFace(float maxRange, float coneDegrees, UnitSelectionDelegate unit, bool noMovement)
        {
            return new Decorator(
                ret => unit(ret) != null,
                new PrioritySelector(
                    new Decorator(
                        ret => !SingularSettings.Instance.DisableAllMovement && (!unit(ret).InLineOfSightOCD || (!noMovement && unit(ret).Distance > maxRange)),
                        new Action(ret => 
						{
						Navigator.MoveTo(unit(ret).Location);
						return RunStatus.Failure;
						})),
                    new Decorator(
                        ret => !SingularSettings.Instance.DisableAllMovement && Me.IsMoving && unit(ret).Distance <= maxRange,
                        new Action(ret => 
						{
						Navigator.PlayerMover.MoveStop();
						return RunStatus.Failure;
						})),
                    new Decorator(
                        ret => Me.CurrentTarget != null && Me.CurrentTarget.IsAlive && !Me.IsSafelyFacing(Me.CurrentTarget, coneDegrees),
                        new Action(ret => 
						{
						Me.CurrentTarget.Face();
						return RunStatus.Failure;
                    }))
					));
        }

        /// <summary>
        ///   Creates a behavior to move within range, within LOS, and keep facing the specified target.
        /// </summary>
        /// <remarks>
        ///   Created 3/4/2011.
        /// </remarks>
        /// <param name = "maxRange">The maximum range.</param>
        /// <param name = "coneDegrees">The cone in degrees. (If we're facing +/- this many degrees from the target, we will face the target)</param>
        /// <param name = "unit">The unit.</param>
        /// <returns>.</returns>
        protected Composite CreateMoveToAndFace(float maxRange, float coneDegrees, UnitSelectionDelegate unit)
        {
            return CreateMoveToAndFace(maxRange, coneDegrees, unit, false);
        }

        /// <summary>
        ///   Creates a behavior to move within range, within LOS, and keep facing the specified target.
        /// </summary>
        /// <remarks>
        ///   Created 2/25/2011.
        /// </remarks>
        /// <param name = "maxRange">The maximum range.</param>
        /// <param name = "distanceFrom">The distance from.</param>
        /// <returns>.</returns>
        protected Composite CreateMoveToAndFace(float maxRange, UnitSelectionDelegate distanceFrom)
        {
            return CreateMoveToAndFace(maxRange, 70, distanceFrom);
        }

        /// <summary>
        ///   Creates a behavior to move to within LOS, and faces the specified target.
        /// </summary>
        /// <remarks>
        ///   Created 2/25/2011.
        /// </remarks>
        /// <param name = "unitToCheck">The unit to check.</param>
        /// <returns>.</returns>
        protected Composite CreateMoveToAndFace(UnitSelectionDelegate unitToCheck)
        {
            return CreateMoveToAndFace(5f, unitToCheck);
        }

        /// <summary>
        ///   Creates a behavior to move to within LOS, and faces the current target
        /// </summary>
        /// <returns></returns>
        protected Composite CreateMoveToAndFace()
        {
            return CreateMoveToAndFace(5f, ret => Me.CurrentTarget);
        }

        /// <summary>
        ///   Creates a behavior to move to within LOS, and faces the specified target with no distance check
        /// </summary>
        /// <param name = "unitToCheck">The unit to check.</param>
        /// <returns></returns>
        protected Composite CreateFaceUnit(UnitSelectionDelegate unitToCheck)
        {
            return CreateMoveToAndFace(5f, 70, unitToCheck, true);
        }

        /// <summary>
        ///   Creates a behavior to move to within LOS, and faces the current target with no distance check
        /// </summary>
        /// <returns></returns>
        protected Composite CreateFaceUnit()
        {
            return CreateFaceUnit(ret => Me.CurrentTarget);
        }
    }
}