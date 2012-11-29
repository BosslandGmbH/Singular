
using Singular.Settings;

using Styx;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using System;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
using Singular.Utilities;
using Styx.CommonBot.POI;

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
        public static Composite CreateFaceTargetBehavior(float viewDegrees = 70f)
        {
            return CreateFaceTargetBehavior(ret => StyxWoW.Me.CurrentTarget, viewDegrees );
        }

        public static Composite CreateFaceTargetBehavior(UnitSelectionDelegate toUnit, float viewDegrees = 70f)
        {
            return new Decorator(
                ret =>
                !SingularSettings.Instance.DisableAllMovement && toUnit != null && toUnit(ret) != null && 
                !StyxWoW.Me.IsMoving && !toUnit(ret).IsMe && 
                !StyxWoW.Me.IsSafelyFacing(toUnit(ret), viewDegrees ),
                new Action(ret =>
                               {
                                   StyxWoW.Me.CurrentTarget.Face();
                                   return RunStatus.Failure;
                               }));
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
            return CreateMoveToTargetBehavior(stopInRange, range, ret => StyxWoW.Me.CurrentTarget);
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
            return 
                new Decorator(
                    ret => onUnit != null && onUnit(ret) != null && onUnit(ret) != StyxWoW.Me && !StyxWoW.Me.IsCasting,
                    CreateMoveToLocationBehavior(ret => onUnit(ret).Location, stopInRange, ret => range));
        }

#if USE_OLD_VERSION
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
#else

        /// <summary>
        /// Creates a move to melee range behavior.  Tests .IsWithinMeleeRange so we know whether WoW thinks
        /// we are within range, which is more important than our distance calc.  For players keep moving 
        /// until 2 yds away so we stick to them in pvp
        /// </summary>
        /// <param name="stopInRange"></param>
        /// <returns></returns>
        public static Composite CreateMoveToMeleeBehavior(bool stopInRange)
        {
            return new Decorator(
                ret => !SingularSettings.Instance.DisableAllMovement,
                new PrioritySelector(
                    new Decorator(
                        ret => stopInRange 
                            && StyxWoW.Me.IsWithinMeleeRange 
                            && (!StyxWoW.Me.IsPlayer || StyxWoW.Me.CurrentTarget.Distance < 2),
                        new PrioritySelector(
                            CreateEnsureMovementStoppedBehavior(),
                            new Action(ret => RunStatus.Success)
                            )
                        ),
                    new Action(ret => Navigator.MoveTo(StyxWoW.Me.CurrentTarget.Location))
                    )
                );
        }
#endif

        public static Composite CreateMoveToMeleeBehavior(LocationRetriever location, bool stopInRange)
        {
            return 
                new Decorator(
                    ret => !StyxWoW.Me.IsCasting,
                    CreateMoveToLocationBehavior(location, stopInRange, ret => StyxWoW.Me.CurrentTarget.IsPlayer ? 2f : Spell.MeleeRange));
        }

        #region Move Behind

        /// <summary>
        ///   Creates a move behind target behavior. If it cannot fully navigate will move to target location
        /// </summary>
        /// <remarks>
        ///   Created 2/12/2011.
        /// </remarks>
        /// <returns>.</returns>
        public static Composite CreateMoveBehindTargetBehavior()
        {
            return CreateMoveBehindTargetBehavior(ret => true);
        }

        /// <summary>
        ///   Creates a move behind target behavior. If it cannot fully navigate will move to target location
        /// </summary>
        /// <remarks>
        ///   Created 2/12/2011.
        /// </remarks>
        /// <param name="requirements">Aditional requirments.</param>
        /// <returns>.</returns>
        public static Composite CreateMoveBehindTargetBehavior(SimpleBooleanDelegate requirements)
        {
            return 
                new Decorator(
                    ret => !SingularSettings.Instance.DisableAllMovement &&
                            SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds && 
                            requirements(ret) && !StyxWoW.Me.IsCasting &&
                            !Group.MeIsTank && !StyxWoW.Me.CurrentTarget.MeIsBehind &&
                            StyxWoW.Me.CurrentTarget.IsAlive &&
                            (StyxWoW.Me.CurrentTarget.CurrentTarget == null || 
                             StyxWoW.Me.CurrentTarget.CurrentTarget != StyxWoW.Me || 
                             StyxWoW.Me.CurrentTarget.Stunned),
                    new Action(ret => Navigator.MoveTo(CalculatePointBehindTarget())));
        }

        private static WoWPoint CalculatePointBehindTarget()
        {
            return
                StyxWoW.Me.CurrentTarget.Location.RayCast(
                    StyxWoW.Me.CurrentTarget.Rotation + WoWMathHelper.DegreesToRadians(150), Spell.MeleeRange - 2f);
        }

        #endregion

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
        public static Composite CreateMoveToLocationBehavior(LocationRetriever location, bool stopInRange, DynamicRangeRetriever range)
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
                            ret => stopInRange && StyxWoW.Me.Location.Distance(location(ret)) < range(ret),
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
                ret => !SingularSettings.Instance.DisableAllMovement 
                    && toUnit != null 
                    && toUnit(ret) != null 
                    && toUnit(ret) != StyxWoW.Me 
                    && (!toUnit(ret).InLineOfSpellSight || (FalsePositiveLineOfSightTest && !toUnit(ret).IsWithinMeleeRange)),
                new Action(ret => Navigator.MoveTo(toUnit(ret).Location)));
        }

        /// <summary>
        /// true if a Target not in line of sight spell failure occurred
        /// recently.  This is a necessary test because in some circumstances
        /// .InLineOfSight and .InLineOfSpellSight will return true while
        /// the WOW Game Client fails the spell cast.  See EventHandler.cs
        /// </summary>
        public static bool FalsePositiveLineOfSightTest
        {
            get
            {
                return (DateTime.Now - EventHandlers.LastLineOfSightError).TotalMilliseconds < 1500;
            }
        }

        private static WoWPoint lastMoveToRangeSpot = WoWPoint.Empty;
        private static bool inRange = false;
        /// <summary>
        ///   Movement for Ranged Classes or Ranged Pulls.  Move to Unit at range behavior 
        ///   that stops in line of spell sight in range of target. Moves a maximum of 
        ///   10 yds at a time to minimize run past. will also only move towards unit if
        ///   not currently moving (to allow bot/human momvement precendence.)
        /// </summary>
        /// <remarks>
        ///   Created 9/25/2012.
        /// </remarks>
        /// <param name = "toUnit">unit to move towards</param>
        /// <param name = "range">The range.</param>
        /// <returns>.</returns>       
        public static Composite CreateMoveToRangeAndStopBehavior(UnitSelectionDelegate toUnit, DynamicRangeRetriever range)
        {
            return
                new Decorator(

                    ret => !SingularSettings.Instance.DisableAllMovement && toUnit != null && toUnit(ret) != null,

                    new PrioritySelector(
                        // save check for whether we are in range to avoid duplicate calls
                        new Action( ret => {
                            inRange = toUnit(ret).Distance < range(ret) 
                                && toUnit(ret).InLineOfSpellSight 
                                && !FalsePositiveLineOfSightTest;
                            return RunStatus.Failure;
                        }),

                        new Decorator(
                            ret => inRange && StyxWoW.Me.IsMoving,
                            new Action(ret => {
                                Logger.WriteDebug("MoveToRangeAndStop:  stopping - within {0:F1} yds and LoSS of {1}", range(ret), StyxWoW.Me.CurrentTarget.SafeName());
                                Navigator.PlayerMover.MoveStop();
                                })
                            ),

                        new Decorator(
                            ret => !inRange && (!StyxWoW.Me.IsMoving || StyxWoW.Me.Location.DistanceSqr(lastMoveToRangeSpot) < 3 * 3),
                            new Action(ret => {
                                    WoWPoint[] spots = Navigator.GeneratePath( StyxWoW.Me.Location, toUnit(ret).Location);
                                    if (spots.GetLength(0) > 0)
                                    {
                                        if ( spots[0].Distance(toUnit(ret).Location) < range(ret))
                                        {
                                            float spotDistFromMe = spots[0].Distance(StyxWoW.Me.Location);
                                            float moveToDistFromSpot = spotDistFromMe - 10;
                                            if (moveToDistFromSpot > 0)
                                            {
                                                spots[0] = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, spots[0], moveToDistFromSpot);
                                            }
                                        }

                                        lastMoveToRangeSpot = spots[0];
                                        Navigator.MoveTo(lastMoveToRangeSpot);
                                    }
                                    else
                                    {
                                        Logger.WriteDebug("MoveToRangeAndStop:  unable to calculate path to {0} @ {1:F1} yds and LoSS={2}", StyxWoW.Me.CurrentTarget.SafeName(), StyxWoW.Me.CurrentTarget.Distance, StyxWoW.Me.CurrentTarget.InLineOfSpellSight);
                                    }
                                })
                            )
                        )
                    );
        }

        public static Composite CreateWorgenDarkFlightBehavior()
        {
            return new Decorator(
                ret => SingularSettings.Instance.UseRacials 
                    && !SingularSettings.Instance.DisableAllMovement 
                    && StyxWoW.Me.IsAlive 
                    && StyxWoW.Me.IsMoving
                    && !StyxWoW.Me.Mounted
                    && !StyxWoW.Me.IsOnTransport
                    && !StyxWoW.Me.OnTaxi
                    && StyxWoW.Me.Race == WoWRace.Worgen 
                    && !StyxWoW.Me.HasAnyAura("Darkflight")
                    && (BotPoi.Current == null || BotPoi.Current.Type == PoiType.None || BotPoi.Current.Location.Distance(StyxWoW.Me.Location) > 10)
                    && !StyxWoW.Me.IsAboveTheGround(),

                new PrioritySelector(
                    Spell.WaitForCast(),
                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        Spell.BuffSelf("Darkflight")
                        )
                    )
                );
        }
    }

    public delegate WoWPoint LocationRetriever(object context);

    public delegate float DynamicRangeRetriever(object context);
}