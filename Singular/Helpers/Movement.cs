
using Singular.Lists;
using Singular.Settings;

using Styx;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using System;
using System.Linq;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
using Singular.Utilities;
using Styx.CommonBot.POI;
using CommonBehaviors.Actions;
using System.Diagnostics;
using Singular.Managers;

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

        public static Composite CreateMoveToLosBehavior(UnitSelectionDelegate toUnit)
        {
            return new Decorator(
                ret => !MovementManager.IsMovementDisabled
                    && toUnit != null
                    && toUnit(ret) != null
                    && toUnit(ret) != StyxWoW.Me
                    && !InLineOfSpellSight(toUnit(ret)),
                new Sequence(
                    new Action(ret => Logger.WriteDebug( "MoveToLoss: moving to LoSS of {0}", toUnit(ret).SafeName())),
                    new Action(ret => Navigator.MoveTo(toUnit(ret).Location))
                    )
                );
        }

        /// <summary>
        /// true if Target in line of spell sight AND a spell hasn't just failed due
        /// to a line of sight error.  This test is required for the unit we are moving
        /// towards because WoWUnit.InLineOfSpellSight will return true while the
        /// WOW Game Client fails the spell cast.  See EventHandler.cs for setting
        /// LastLineOfSightError
        /// 
        /// Only use this for unit we are moving towards.  Not to be used when checking
        /// ability to cast spells on various mobs
        /// </summary>
        /// <param name="unit">target we are moving towards</param>
        /// <returns></returns>
        public static bool InLineOfSpellSight(WoWUnit unit)
        {
            if (unit.InLineOfSpellSight)
            {
                if ((DateTime.Now - EventHandlers.LastLineOfSightError).TotalMilliseconds < 1000)
                {
                    if (unit.IsWithinMeleeRange)
                    {
                        Logger.WriteDebug("InLineOfSpellSight: last LoS error < 1 sec but in melee range, pretending we are in LoS");
                        return true;
                    }

                    Logger.WriteDebug("InLineOfSpellSight: last LoS error < 1 sec, pretending still not in LoS");
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        ///   Creates the ensure movement stopped behavior. if no range specified, will stop immediately.  if range given, will stop if within range yds of current target
        /// </summary>
        /// <remarks>
        ///   Created 5/1/2011.
        /// </remarks>
        /// <returns>.</returns>
        public static Composite CreateEnsureMovementStoppedBehavior(float range = float.MaxValue, UnitSelectionDelegate onUnit = null, string reason = null)
        {
            if (range == float.MaxValue)
            {
                return new Decorator(
                    ret => !MovementManager.IsMovementDisabled && StyxWoW.Me.IsMoving,
                    new Sequence(
                        new Action(ret => Logger.WriteDebug("EnsureMovementStopped: stopping! {0}", reason ?? "")),
                        new Action(ret => Navigator.PlayerMover.MoveStop())
                        )
                    );
            }

            if (onUnit == null)
                onUnit = del => StyxWoW.Me.CurrentTarget;

            return new Decorator(
                ret => !MovementManager.IsMovementDisabled
                    && StyxWoW.Me.IsMoving
                    && (onUnit(ret) == null || (InLineOfSpellSight(onUnit(ret)) && onUnit(ret).Distance < range)),
                new Sequence(
                    new Action(ret => Logger.WriteDebug("EnsureMovementStopped: stopping because {0}", onUnit(ret) == null ? "No CurrentTarget" : string.Format("target @ {0:F1} yds, stop range: {1:F1}", onUnit(ret).Distance, range))),
                    new Action(ret => Navigator.PlayerMover.MoveStop())
                    )
                );
        }

        /// <summary>
        /// Creates ensure movement stopped if within melee range behavior.
        /// </summary>
        /// <returns></returns>
        public static Composite CreateEnsureMovementStoppedWithinMelee()
        {
            return new Decorator(
                ret => !MovementManager.IsMovementDisabled
                    && StyxWoW.Me.IsMoving
                    && InMoveToMeleeStopRange,
                new Sequence(
                    new Action(ret => Logger.WriteDebug("EnsureMovementStoppedWithinMelee: stopping because ", !StyxWoW.Me.GotTarget ? "No CurrentTarget" : string.Format("{0:F1} yds target distance is within melee", StyxWoW.Me.CurrentTarget.Distance))),
                    new Action(ret => Navigator.PlayerMover.MoveStop())
                    )
                );
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
            return CreateFaceTargetBehavior(ret => StyxWoW.Me.CurrentTarget, viewDegrees);
        }

        public static Composite CreateFaceTargetBehavior(UnitSelectionDelegate toUnit, float viewDegrees = 70f)
        {
            return new Decorator(
                ret =>
                !MovementManager.IsMovementDisabled && toUnit != null && toUnit(ret) != null &&
                !StyxWoW.Me.IsMoving && !toUnit(ret).IsMe &&
                !StyxWoW.Me.IsSafelyFacing(toUnit(ret), viewDegrees),
                new Action(ret =>
                               {
                                   toUnit(ret).Face();
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
                    ret => onUnit != null && onUnit(ret) != null && onUnit(ret) != StyxWoW.Me && !Spell.IsCastingOrChannelling(),
                    CreateMoveToLocationBehavior(ret => onUnit(ret).Location, stopInRange, ret => onUnit(ret).SpellRange(range)));
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
                ret => !MovementManager.IsMovementDisabled,
                new PrioritySelector(
                    new Decorator(
                        ret => stopInRange && InMoveToMeleeStopRange,
                        new PrioritySelector(
                            CreateEnsureMovementStoppedWithinMelee(),
                            new Action(ret => RunStatus.Success)
                            )
                        ),
                    new Sequence(
                        new Action( ret => Logger.WriteDebug( "MoveToMelee: towards {0} @ {1:F1} yds", StyxWoW.Me.CurrentTarget.SafeName(), StyxWoW.Me.Distance)),
                        new Action(ret => Navigator.MoveTo(StyxWoW.Me.CurrentTarget.Location))
                        )
                    )
                );
        }

        private static bool InMoveToMeleeStopRange
        {
            get
            {
                if (!StyxWoW.Me.GotTarget)
                    return true;

                if (StyxWoW.Me.CurrentTarget.IsPlayer)
                    return StyxWoW.Me.CurrentTarget.DistanceSqr < (2 * 2);

                return StyxWoW.Me.CurrentTarget.IsWithinMeleeRange;
            }
        }
#endif

        public static Composite CreateMoveToMeleeBehavior(LocationRetriever location, bool stopInRange)
        {
            return
                new Decorator(
                    ret => !Spell.IsCastingOrChannelling(),
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
            return new Decorator(
                ret =>
                {
                    if (MovementManager.IsMovementDisabled || SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || !requirements(ret) || Spell.IsCastingOrChannelling() || Group.MeIsTank)
                        return false;
                    var currentTarget = StyxWoW.Me.CurrentTarget;
                    if (currentTarget == null || currentTarget.MeIsSafelyBehind || !currentTarget.IsAlive || BossList.AvoidRearBosses.Contains(currentTarget.Entry))
                        return false;
                    return currentTarget.Stunned || currentTarget.CurrentTarget != StyxWoW.Me;
                },
                new PrioritySelector(
                    ctx => CalculatePointBehindTarget(),
                    new Decorator(
                        req => Navigator.CanNavigateFully(StyxWoW.Me.Location, (WoWPoint)req, 4),
                        new Action(behindPoint => Navigator.MoveTo((WoWPoint)behindPoint))
                        )
                    )
                );
        }

        public static WoWPoint CalculatePointBehindTarget()
        {
            float facing = StyxWoW.Me.CurrentTarget.Rotation;
            facing += WoWMathHelper.DegreesToRadians(180); // was 150 ?
            facing = WoWMathHelper.NormalizeRadian(facing);

            return StyxWoW.Me.CurrentTarget.Location.RayCast(facing, Spell.MeleeRange - 2f);
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
                    ret => !MovementManager.IsMovementDisabled,
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

                    ret => !MovementManager.IsMovementDisabled && toUnit != null && toUnit(ret) != null,

                    new PrioritySelector(
                // save check for whether we are in range to avoid duplicate calls
                        new Action(ret =>
                        {
                            inRange = toUnit(ret).Distance < range(ret) && InLineOfSpellSight(toUnit(ret));
                            return RunStatus.Failure;
                        }),

                        // check if we are still out of range and either not moving or have reached the last spot
                        new Decorator(
                            ret => !inRange && (!StyxWoW.Me.IsMoving || StyxWoW.Me.Location.Distance(lastMoveToRangeSpot) <= 0.5),
                            new Action(ret =>
                            {
                                WoWPoint[] spots = Navigator.GeneratePath(StyxWoW.Me.Location, toUnit(ret).Location);
                                if (spots.GetLength(0) <= 0)
                                {
                                    Logger.Write("MoveToRangeAndStop: can't move, unable to calculate path to {0} @ {1:F1} yds and LoSS={2}", toUnit(ret).SafeName(), toUnit(ret).Distance, toUnit(ret).InLineOfSpellSight);
                                    return RunStatus.Failure;
                                }

                                int i = 0;
                                WoWPoint nextSpot;
                                do
                                {
                                    nextSpot = spots[i];
                                } while (++i < spots.GetLength(0) && StyxWoW.Me.Location.Distance(nextSpot) < Navigator.PathPrecision);

                                if (StyxWoW.Me.Location.Distance(nextSpot) < Navigator.PathPrecision)
                                {
                                    Logger.Write("MoveToRangeAndStop: can't move, furthest point in path {0} @ {1:F1} yds less than PathPrecision {2}", nextSpot, StyxWoW.Me.Location.Distance(nextSpot), Navigator.PathPrecision);
                                    return RunStatus.Failure;
                                }

                                if (StyxWoW.Me.Location.Distance(nextSpot) > 20)
                                {
                                    Logger.WriteDebug("MoveToRangeAndStop: next spot in path {0} @ {1:F1} yds, shortening to 10 yds", nextSpot, StyxWoW.Me.Location.Distance(nextSpot));
                                    nextSpot = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, nextSpot, 10);
                                }

                                Logger.WriteDebug("MoveToRangeAndStop: moving to {0} @ {1:F1} yds which is towards {2} @ {3:F1} yds", nextSpot, StyxWoW.Me.Location.Distance(nextSpot), toUnit(ret).SafeName(), toUnit(ret).Distance);
                                lastMoveToRangeSpot = nextSpot;
                                Navigator.MoveTo(lastMoveToRangeSpot);
                                return RunStatus.Success;
                            })
                            ),

                        // we are now in range 
                        new Decorator(
                            ret => inRange && StyxWoW.Me.IsMoving && StyxWoW.Me.Combat,
                            new Action(ret =>
                            {
                                Logger.WriteDebug("MoveToRangeAndStop:  stopping - within {0:F1} yds and LoSS of {1}", range(ret), toUnit(ret).SafeName());
                                Navigator.PlayerMover.MoveStop();
                            })
                            ),

                        // at this point if we are moving, tell tree we are still busy
                        new Decorator(
                            ret => StyxWoW.Me.IsMoving,
                            new ActionAlwaysSucceed()
                            )
                        )
                    );
        }

        public static Composite CreateWorgenDarkFlightBehavior()
        {
            return new Decorator(
                ret => SingularSettings.Instance.UseRacials
                    && !MovementManager.IsMovementDisabled
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