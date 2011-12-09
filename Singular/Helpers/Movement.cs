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

using System;
using System.Linq;

using Singular.Settings;

using Styx;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular.Helpers
{
    internal static class Movement
    {
        /// <summary>
        ///  Creates a behavior that does nothing more than check if we're in Line of Sight of the target; and if not, move towards the target.
        /// </summary>
        /// <remarks>
        ///  Created 23/5/2011
        /// </remarks>
        /// <returns>.</returns>
        public static Composite CreateMoveToLosBehavior()
        {
            return CreateMoveToLosBehavior(ret => StyxWoW.Me.CurrentTarget);
        }

        /// <summary>
        ///   Creates the ensure movement stopped behavior. Will return RunStatus.Success if it has stopped any movement, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/1/2011.
        /// </remarks>
        /// <returns>.</returns>
        public static Composite CreateEnsureMovementStoppedBehavior()
        {
            return new Decorator(
                ret => !SingularSettings.Instance.DisableAllMovement && StyxWoW.Me.IsMoving,
                new Action(ret => Navigator.PlayerMover.MoveStop()));
        }

        /// <summary>
        ///   Creates a behavior that does nothing more than check if we're facing the target; and if not, faces the target. (Uses a hard-coded 70degree frontal cone)
        /// </summary>
        /// <remarks>
        ///   Created 5/1/2011.
        /// </remarks>
        /// <returns>.</returns>
        public static Composite CreateFaceTargetBehavior()
        {
            return new Decorator(
                ret => !SingularSettings.Instance.DisableAllMovement && !StyxWoW.Me.IsMoving && StyxWoW.Me.CurrentTarget != null && !StyxWoW.Me.IsSafelyFacing(StyxWoW.Me.CurrentTarget, 70f),
                new Action(ret => StyxWoW.Me.CurrentTarget.Face()));
        }

        /// <summary>
        ///   Creates a move to target behavior. Will return RunStatus.Success if it has reached the location, or stopped in range. Best used at the end of a rotation.
        /// </summary>
        /// <remarks>
        ///   Created 5/1/2011.
        /// </remarks>
        /// <param name = "stopInRange">true to stop in range.</param>
        /// <param name = "range">The range.</param>
        /// <returns>.</returns>
        public static Composite CreateMoveToTargetBehavior(bool stopInRange, float range)
        {
            return CreateMoveToLocationBehavior(ret => StyxWoW.Me.CurrentTarget.Location, stopInRange, range);
        }

        /// <summary>
        ///   Creates a move to target behavior. Will return RunStatus.Success if it has reached the location, or stopped in range. Best used at the end of a rotation.
        /// </summary>
        /// <remarks>
        ///   Created 5/1/2011.
        /// </remarks>
        /// <param name = "stopInRange">true to stop in range.</param>
        /// <param name = "range">The range.</param>
        /// <param name="onUnit">The unit to move to.</param>
        /// <returns>.</returns>
        public static Composite CreateMoveToTargetBehavior(bool stopInRange, float range, UnitSelectionDelegate onUnit)
        {
            return CreateMoveToLocationBehavior(ret => onUnit(ret).Location, stopInRange, range);
        }

        /// <summary>
        ///   Creates a move to melee range behavior. Will return RunStatus.Success if it has reached the location, or stopped in range. Best used at the end of a rotation.
        /// </summary>
        /// <remarks>
        ///   Created 5/1/2011.
        /// </remarks>
        /// <param name = "stopInRange">true to stop in range.</param>
        /// <param name = "range">The range.</param>
        /// <returns>.</returns>
        public static Composite CreateMoveToMeleeBehavior(bool stopInRange)
        {
            return CreateMoveToMeleeBehavior(ret => StyxWoW.Me.CurrentTarget.Location, stopInRange);
        }

        /// <summary>
        ///   Creates a move behind target behavior. Will return RunStatus.Success if it has reached the location, or stopped in range. Best used at the end of a rotation.
        /// </summary>
        /// <remarks>
        ///   Created 5/1/2011.
        /// </remarks>
        /// <param name = "distanceBehind">The distance behind the target to move to. Passing 0 here will cause undefined behavior.</param>
        /// <returns>.</returns>
        public static Composite CreateMoveBehindTargetBehavior(float distanceBehind)
        {
            // This should more or less ensure we're at the point.
            return CreateMoveToLocationBehavior(ret => CalculatePointBehindTarget(distanceBehind), true, 5f);
        }

        private static WoWPoint CalculatePointBehindTarget(float distanceBehind)
        {
            return WoWMathHelper.CalculatePointBehind(StyxWoW.Me.CurrentTarget.Location, StyxWoW.Me.CurrentTarget.Rotation, distanceBehind);
        }

        #region Root Move To Location

        /// <summary>
        ///   Creates a move to location behavior. Will return RunStatus.Success if it has reached the location, or stopped in range. Best used at the end of a rotation.
        /// </summary>
        /// <remarks>
        ///   Created 5/1/2011.
        /// </remarks>
        /// <param name = "location">The location.</param>
        /// <param name = "stopInRange">true to stop in range.</param>
        /// <param name = "range">The range.</param>
        /// <returns>.</returns>
        public static Composite CreateMoveToLocationBehavior(LocationRetriever location, bool stopInRange, float range)
        {
            // Do not fuck with this. It will ensure we stop in range if we're supposed to.
            // Otherwise it'll stick to the targets ass like flies on dog shit.
            // Specifying a range of, 2 or so, will ensure we're constantly running to the target. Specifying 0 will cause us to spin in circles around the target
            // or chase it down like mad. (PVP oriented behavior)
            return
                new Decorator( 
                    // Don't run if the movement is disabled.
                    ret => !SingularSettings.Instance.DisableAllMovement,
                    new PrioritySelector(
                        new Decorator(
                            // Give it a little more than 1/2 a yard buffer to get it right. CTM is never 'exact' on where we land. So don't expect it to be.
                            ret => stopInRange && StyxWoW.Me.Location.Distance(location(ret)) + 0.6f < range,
                            new PrioritySelector(
                                CreateEnsureMovementStoppedBehavior(),
                                // In short; if we're not moving, just 'succeed' here, so we break the tree.
                                new Action(ret => RunStatus.Success)
                                )
                            ),
                        new Action(ret => Navigator.MoveTo(location(ret)))
                        ));
        }



        public static Composite CreateMoveToMeleeBehavior(LocationRetriever location, bool stopInRange)
        {
            // Do not fuck with this. It will ensure we stop in range if we're supposed to.
            // Otherwise it'll stick to the targets ass like flies on dog shit.
            // Specifying a range of, 2 or so, will ensure we're constantly running to the target. Specifying 0 will cause us to spin in circles around the target
            // or chase it down like mad. (PVP oriented behavior)
            return
                new Decorator(
                    // Don't run if the movement is disabled.
                    ret => !SingularSettings.Instance.DisableAllMovement,
                    new PrioritySelector(
                        new Decorator(
                            // Give it a little more than 1/2 a yard buffer to get it right. CTM is never 'exact' on where we land. So don't expect it to be.
                            ret => stopInRange && StyxWoW.Me.Location.Distance(location(ret)) + 0.6f < Spell.SafeMeleeRange,
                            new PrioritySelector(
                                CreateEnsureMovementStoppedBehavior(),
                                // In short; if we're not moving, just 'succeed' here, so we break the tree.
                                new Action(ret => RunStatus.Success)
                                )
                            ),
                        new Action(ret => Navigator.MoveTo(location(ret)))
                        ));
        }

        #endregion

        public static Composite CreateMoveToLosBehavior(UnitSelectionDelegate toUnit)
        {
            return new Decorator(
                ret =>
                !SingularSettings.Instance.DisableAllMovement && toUnit != null && toUnit(ret) != null && !toUnit(ret).InLineOfSightOCD,
                new Action(ret => Navigator.MoveTo(toUnit(ret).Location)));
        }
    }

    public delegate WoWPoint LocationRetriever(object context);

    public delegate float DynamicRangeRetriever(object context);
}