﻿
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
using System.Drawing;
using System.Numerics;
using Styx.CommonBot;
using System.Reflection;
using Styx.Common;
using Styx.WoWInternals;
using Styx.CommonBot.Routines;

namespace Singular.Helpers
{
    internal static class Movement
    {
        private static MoveContext MoveContext(this object ctx) { return (MoveContext)ctx; }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        /// <summary>
        ///  Creates a behavior that does nothing more than check if we're in Line of Sight of the target; and if not, move towards the target.
        /// </summary>
        /// <remarks>
        ///  Created 23/5/2011
        /// </remarks>
        /// <returns>.</returns>
        public static Composite CreateMoveToLosBehavior()
        {
            return CreateMoveToLosBehavior(ret => Me.CurrentTarget);
        }

        private static MoveResult _lastLossMoveResult;
        private static WoWGuid _lastLossGuid;
        private static DateTime _nextLossMessage = DateTime.MinValue;

        public static Composite CreateMoveToLosBehavior(UnitSelectionDelegate toUnit, bool stopInLoss = false)
        {
            return new Sequence(

                ctx => (WoWUnit) (toUnit == null ? null : toUnit(ctx)),

                new DecoratorContinue(
                    req => req == null || MovementManager.IsMovementDisabled || InLineOfSpellSight((req as WoWUnit)) || (req as WoWUnit).IsMe,
                    new ActionAlwaysFail()
                    ),

                new Action(ret =>
                {
                    WoWUnit unit = (WoWUnit) ret;
                    if (unit != null)
                    {
                        var parameters = new MoveToParameters(unit.Location) {DistanceTolerance = unit.MeleeRange};
                        MoveResult moveRes = Navigator.MoveTo(parameters);
                        if (unit.Guid != _lastLossGuid || moveRes != _lastLossMoveResult || DateTime.UtcNow > _nextLossMessage)
                        {
                            _lastLossGuid = unit.Guid;
                            _lastLossMoveResult = moveRes;
                            _nextLossMessage = DateTime.UtcNow + TimeSpan.FromMilliseconds(250);
                            Logger.WriteDebug(Color.White, "MoveToLoss[{0}]: moving within LoSS of {1} @ {2:F1} yds", moveRes, unit.SafeName(), unit.SpellDistance());
                        }

                        if (moveRes != MoveResult.Failed && moveRes != MoveResult.PathGenerationFailed)
                            return RunStatus.Success;
                    }
                    return RunStatus.Failure;
                })
                );
        }

        public static Composite WaitForLineOfSpellSight(UnitSelectionDelegate toUnit = null)
        {
            toUnit = toUnit ?? (u => Me.CurrentTarget);

            return new Sequence(

                ctx => toUnit(ctx),

                new DecoratorContinue(
                    req => req != null && (InLineOfSpellSight(req as WoWUnit) || (req as WoWUnit).IsMe),
                    new ActionAlwaysFail()
                    ),

                new DecoratorContinue(
                    req => req == null,
                    new PrioritySelector(
                        new Throttle(5, new Action(r => Logger.Write(LogColor.Diagnostic, "warning: waiting for line of sight, but no target selected"))),
                        new ActionAlwaysSucceed()
                        )
                    ),

                new DecoratorContinue(
                    req => req != null && MovementManager.IsMovementDisabled,
                    new Sequence(
                        new Throttle(5, new Action(r => Logger.Write(LogColor.Diagnostic, "warning: movement disabled and not in line of sight of {0} @ {1:F1} yds", (r as WoWUnit).SafeName(), (r as WoWUnit).SpellDistance()))),
                        new ActionAlwaysFail()
                        )
                    ),

                // at this point, we have target, movement allowed, so return Success until in line of sight
                new ActionAlwaysSucceed()
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
        public static bool InLineOfSpellSight(WoWUnit unit, int timeOut = 1000)
        {
            if (unit != null && unit.InLineOfSpellSight)
            {
                if ((DateTime.UtcNow - EventHandlers.LastLineOfSightFailure).TotalMilliseconds < timeOut)
                {
                    if (nextLossMessage < DateTime.UtcNow)
                    {
                        nextLossMessage = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeOut / 3);
                        Logger.WriteDebug(Color.White, "InLineOfSpellSight: last LoS error < {0} ms, pretending still not in LoS", timeOut);
                    }

                    return false;
                }

                return true;
            }

            return false;
        }

        static DateTime nextLossMessage { get; set; }

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
                    ret => Me.IsMoving,
                    new Sequence(
                        new Throttle(1, TimeSpan.FromSeconds(1), RunStatus.Success,
                            new Action(ret =>
                            {
                                if (MovementManager.IsMovementDisabled)
                                    Logger.WriteDebug(Color.White, "EnsureMovementStopped: movement disabled, suppress stop!{0}", reason ?? "");
                                else
                                    Logger.WriteDebug(Color.White, "EnsureMovementStopped: stopping! {0}", reason ?? "");
                            })
                            ),
                        new DecoratorContinue(
                            req => !MovementManager.IsMovementDisabled,
                            new Action(ret => StopMoving.Now())
                            )
                        )
                    );
            }

            if (onUnit == null)
                onUnit = del => Me.CurrentTarget;

            return new Decorator(
                ret => !MovementManager.IsMovementDisabled
                    && Me.IsMoving
                    && (onUnit(ret) == null || (onUnit(ret).Distance < range && onUnit(ret).InLineOfSpellSight)),
                new Sequence(
                    new Throttle(1, TimeSpan.FromSeconds(1), RunStatus.Success,
                        new Action(ret =>
                        {
                            if (MovementManager.IsMovementDisabled)
                                Logger.WriteDebug(Color.White, "EnsureMovementStopped: movement disabled, suppress stop for {0}", onUnit(ret) == null ? "No CurrentTarget" : string.Format("target @ {0:F1} yds, stop range: {1:F1}", onUnit(ret).Distance, range));
                            else
                                Logger.WriteDebug(Color.White, "EnsureMovementStopped: stopping because {0}", onUnit(ret) == null ? "No CurrentTarget" : string.Format("target @ {0:F1} yds, stop range: {1:F1}", onUnit(ret).Distance, range));
                        })
                        ),
                    new DecoratorContinue(
                        req => !MovementManager.IsMovementDisabled,
                        new Action(ret => StopMoving.Now())
                        )
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
                    && Me.IsMoving
                    && InMoveToMeleeStopRange(Me.CurrentTarget),
                new Sequence(
                    new Action(ret => Logger.WriteDebug(Color.White, "EnsureMovementStoppedWithinMelee: stopping because {0}", !Me.GotTarget() ? "No CurrentTarget" : string.Format("target at {0:F1} yds", Me.CurrentTarget.Distance))),
                    new Action(ret => StopMoving.Now())
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
        public static Composite CreateFaceTargetBehavior(float viewDegrees = 70f, bool waitForFacing = true)
        {
            return CreateFaceTargetBehavior(ret => Me.CurrentTarget, viewDegrees, waitForFacing );
        }

        private static WoWMovement.MovementDirection _strafeDirection { get; set; }
        private static DateTime _strafeStopTime { get; set; }
        private static TimeSpan _strafeDuration { get; set; }
        public static Composite CreateFaceTargetBehavior(UnitSelectionDelegate toUnit, float viewDegrees = 100f, bool waitForFacing = true)
        {
            System.Diagnostics.Debug.Assert( toUnit != null);

            return new Sequence(
                ctx => toUnit(ctx),

                new DecoratorContinue(
                    req => req == null || MovementManager.IsFacingDisabled || (req as WoWUnit).IsMe,
                    new ActionAlwaysFail()
                    ),

                new PrioritySelector(
                    new Decorator(
                        req => (req as WoWUnit).IsNotFacingErrorTarget(),
                        new SeqDiag(0.5, s => string.Format("FaceTarget: {0} matches last Not Facing Target error", (s as WoWUnit).SafeName()))
                        ),
                    new DecoratorContinue(
                        req => Me.IsSafelyFacing((WoWUnit)req, viewDegrees),
                        new ActionAlwaysFail()
                        )
                    ),

                // Failure: wait to face until not moving
                new DecoratorContinue(
                    req => Me.IsMoving,
                    new Sequence(
                        new SeqDiag( 0.5, s => string.Format("FaceTarget: more than {0} degrees, but suppress since moving=true", (long)viewDegrees)),
                        new ActionAlwaysFail()
                        )
                    ),

                // Success: when too close (< 0.1) strafe before facing to avoid Facing = Y that is incorrect
                new DecoratorContinue(
                    req => (req as WoWUnit).Distance < 0.1,
                    new Sequence(
                        new Action(ret =>
                        {
                            WoWUnit unit = (WoWUnit)ret;
                            _strafeDuration = TimeSpan.FromMilliseconds(150f);
                            _strafeStopTime = DateTime.UtcNow + _strafeDuration;
                            _strafeDirection = (((int)DateTime.UtcNow.Second) & 1) == 0 ? WoWMovement.MovementDirection.StrafeLeft : WoWMovement.MovementDirection.StrafeRight;
                            Logger.WriteDiagnostic(LogColor.Hilite, "FaceTarget: {0} for {1:F0} ms since too close to target @ {2:F2} yds", _strafeDirection, _strafeDuration.TotalMilliseconds, unit.Distance);
                            WoWMovement.Move(_strafeDirection, _strafeDuration);
                        }),
                        new WaitContinue(
                            _strafeDuration,
                            until => DateTime.UtcNow >= _strafeStopTime || (until as WoWUnit).Distance > 1.5,
                            new ActionAlwaysSucceed()
                            ),
                        new Action(r =>
                        {
                            Logger.WriteDiagnostic(LogColor.Hilite, "FaceTarget: stop {0} @ {1:F2} secs distance {1:F2} yds",
                                _strafeDirection,
                                (DateTime.UtcNow - (_strafeStopTime - _strafeDuration)).TotalSeconds,
                                (r as WoWUnit).Distance
                                );
                            WoWMovement.MoveStop(_strafeDirection);
                        })
                        )
                    ),

                // Success: we are okay to face at this point
                new SeqDiag( 0.5, s => string.Format("FaceTarget: facing since more than {0} degrees (moving={1})", (long)viewDegrees, Me.IsMoving.ToYN())),

                // Success: face now
                new Action(r => ((WoWUnit)r).Face()),

                // Conditional: Success if waiting for facing, Failure otherwise
                new DecoratorContinue(
                    req => !waitForFacing,
                    new ActionAlwaysFail()
                    ),

                new SeqDiag( 0.5, s => string.Format("FaceTarget: waiting (waitForFacing={0})", waitForFacing)),

                // even though we may want a tighter conical facing check, allow
                // .. behavior to continue if 180 or better so we can cast while turning
                new DecoratorContinue(
                    req => Me.IsSafelyFacing((WoWUnit)req, 180f),
                    new ActionAlwaysFail()
                    ),

                new DecoratorContinue(
                    req => Me.InVehicle,
                    new ThrottlePasses(
                        1,
                        TimeSpan.FromSeconds(5),
                        RunStatus.Failure,
                        new Action(r =>
                        {
                            Logger.WriteDebug("FaceTarget: don't wait to face {0} since we are in vehicle", ((WoWUnit) r).SafeName());
                            return RunStatus.Failure;
                        })
                        )
                    )
                );
        }

        public static Composite WaitForFacing(UnitSelectionDelegate toUnit = null, float viewDegrees = 180f)
        {
            toUnit = toUnit ?? (u => Me.CurrentTarget);

            return new Sequence(

                ctx => toUnit(ctx),

                new DecoratorContinue(
                    req => req != null && (Me.IsSafelyFacing(req as WoWUnit, viewDegrees) || (req as WoWUnit).IsMe),
                    new ActionAlwaysFail()
                    ),

                new DecoratorContinue(
                    req => req == null,
                    new SeqLog( 5, LogColor.Diagnostic, s => "waiting: for WaitFaceTarget, but no target selected")
                    ),

                new DecoratorContinue(
                    req => req != null && Me.InVehicle,
                    new PriLog( 5f, LogColor.Diagnostic, s => string.Format("warning: in vehicle so ignoring facing requirement for {0}", (s as WoWUnit).SafeName()))
                    ),

                new DecoratorContinue(
                    req => req != null && MovementManager.IsFacingDisabled,
                    new SeqLog( 5, LogColor.Diagnostic, s => string.Format("warning: facing disabled and not facing {0} @ {1:F1} yds", (s as WoWUnit).SafeName(), (s as WoWUnit).SpellDistance()))
                    ),

                new DecoratorContinue(
                    req => req != null && !MovementManager.IsFacingDisabled,
                    new SeqDbg( 1, LogColor.Diagnostic, s => string.Format("waiting: for WaitFaceTarget, attempting to face {0} @ {1:F1} yds", (s as WoWUnit).SafeName(), (s as WoWUnit).SpellDistance()))
                    ),

                // at this point, we have target, movement allowed, so return Success until in line of sight
                new ActionAlwaysSucceed()
                );
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
            return CreateMoveToTargetBehavior(stopInRange, range, ret => Me.CurrentTarget);
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
                    ret => onUnit != null && onUnit(ret) != null && onUnit(ret) != Me && !Spell.IsCastingOrChannelling(),
                    CreateMoveToLocationBehavior(ret => onUnit(ret).Location, stopInRange, ret => onUnit(ret).SpellRange(range)));
        }

        public static Composite CreateMoveToUnitBehavior(float range, UnitSelectionDelegate onUnit)
        {
            return CreateMoveToUnitBehavior(onUnit, range);
        }

        private static MoveResult _lastMoveResult;
        private static WoWGuid _lastGuid;
        private static DateTime _nextMessage = DateTime.MinValue;
        public static Composite CreateMoveToUnitBehavior(UnitSelectionDelegate onUnit, float range, float stopAt = float.MinValue, RunStatus statusWhenMoving = RunStatus.Failure )
        {
            return new Decorator(
                ret => !MovementManager.IsMovementDisabled,
                new Action(ret => {
                    WoWUnit unit = onUnit?.Invoke(ret);
                    if ( unit != null && unit.SpellDistance() > range)
                    {
                        var parameters = new MoveToParameters(unit.Location) { DistanceTolerance = Math.Max(range, 3f) };
                        MoveResult moveRes = Navigator.MoveTo(parameters);
                        if (unit.Guid != _lastGuid || moveRes != _lastMoveResult || DateTime.UtcNow > _nextMessage)
                        {
                            _lastGuid = unit.Guid;
                            _lastMoveResult = moveRes;
                            _nextMessage = DateTime.UtcNow + TimeSpan.FromMilliseconds(250);
                            Logger.WriteDebug(Color.White, "MoveToUnit[{0}]: moving within {1:F1} yds of {2} @ {3:F1} yds", moveRes, range, unit.SafeName(), unit.SpellDistance());
                        }

                        StopMoving.InRangeOfUnit(unit, stopAt == float.MinValue ? range : stopAt);
                        if (moveRes != MoveResult.Failed && moveRes != MoveResult.PathGenerationFailed)
                            return statusWhenMoving;
                    }
                    return RunStatus.Failure;
                    })
                );
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
            return CreateMoveToMeleeBehavior(ret => Me.CurrentTarget.Location, stopInRange);
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
#if OLD_MELEE_MOVE
            return new Decorator(
                ret => !MovementManager.IsMovementDisabled,
                new PrioritySelector(
                    new Decorator(
                        ret => stopInRange && InMoveToMeleeStopRange(Me.CurrentTarget),
                        new PrioritySelector(
                            CreateEnsureMovementStoppedWithinMelee(),
                            new Action(ret => RunStatus.Success)
                            )
                        ),
                    new Decorator(
                        ret => Me.GotTarget() && Me.CurrentTarget.IsValid,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug(Color.White, "MoveToMelee: towards {0} @ {1:F1} yds", Me.CurrentTarget.SafeName(), Me.CurrentTarget.Distance)),
                            new Action(ret => Navigator.MoveTo(Me.CurrentTarget.Location)),
                            new Action(ret => StopMoving.InMeleeRangeOfUnit(Me.CurrentTarget, and => InMoveToMeleeStopRange(Me.CurrentTarget))),
                            new ActionAlwaysFail()
                            )
                        )
                    )
                );
#else
            return new PrioritySelector(
                ctx => Me.CurrentTarget,
                new Decorator(
                    ret => !MovementManager.IsMovementDisabled && SingularRoutine.CurrentWoWContext == WoWContext.Instances,
                    CreateMoveBehindTargetBehavior(ctx => ctx != null && ((WoWUnit)ctx).IsBoss() && !((WoWUnit)ctx).IsMoving)
                    ),
                new Decorator(
                    ret => !MovementManager.IsMovementDisabled && Me.GotTarget() && !InMoveToMeleeStopRange(Me.CurrentTarget),
                    new Sequence(
                        ctx => new MoveContext(),
                        new DecoratorContinue(
                            req => SingularSettings.Debug,  // save time if debug = false
                            new Throttle( 1, TimeSpan.FromSeconds(1), RunStatus.Success,
                                new Action(ret => {
                                    Logger.WriteDebug(Color.White, "MoveToMelee({0}): towards {1} @ {2:F1} yds", MoveContext(ret).Result, MoveContext(ret).Unit.SafeName(), MoveContext(ret).Distance);
                                    })
                                )
                            ),
                        new Action(ret => StopMoving.InMeleeRangeOfUnit(MoveContext(ret).Unit)),
                        new ActionAlwaysFail()
                        )
                    )
                );
#endif
        }

        /// <summary>
        /// Creates a move to melee range behavior.  Tests .IsWithinMeleeRange so we know whether WoW thinks
        /// we are within range, which is more important than our distance calc.  For players keep moving
        /// until 2 yds away so we stick to them in pvp
        /// </summary>
        /// <param name="stopInRange"></param>
        /// <returns></returns>
        public static Composite CreateMoveToMeleeTightBehavior(bool stopInRange)
        {
            return new PrioritySelector(
                ctx => Me.CurrentTarget,
                new Decorator(
                    ret => !MovementManager.IsMovementDisabled && ret != null && !InMoveToMeleeStopRange(ret as WoWUnit),
                    new Sequence(
                        ctx => new MoveContext(),
                        new DecoratorContinue(
                            req => SingularSettings.Debug,  // save time if debug = false
                            new Throttle(1, TimeSpan.FromSeconds(1), RunStatus.Success,
                                new Action(ret =>
                                {
                                    Logger.WriteDebug(Color.White, "MoveInTight({0}): towards {1} @ {2:F1} yds", MoveContext(ret).Result, MoveContext(ret).Unit.SafeName(), MoveContext(ret).Distance);
                                })
                                )
                            ),
                        new Action(ret => StopMoving.InMeleeRangeOfUnit( ((MoveContext)ret).Unit)),
                        new ActionAlwaysFail()
                        )
                    )
                );
        }

        public static bool InMoveToMeleeStopRange(WoWUnit unit)
        {
            if (unit == null || !unit.IsValid)
                return false;

            if (unit.IsPlayer)
                return unit.DistanceSqr < 2 * 2;

            float preferredDistance = Math.Max(2f, unit.MeleeRange - 1.33f);
            if (unit.Distance <= preferredDistance && unit.IsWithinMeleeRange)
                return true;

            return false;
        }
#endif


        #region Move Behind

        /// <summary>
        ///   Creates a move behind target behavior. If it cannot fully navigate will move to target location
        /// </summary>
        /// <remarks>
        ///   Created 2/12/2011.
        /// </remarks>
        /// <returns>.</returns>
        public static Composite CreateMoveBehindTargetBehavior(Composite gapCloser = null)
        {
            return CreateMoveBehindTargetBehavior(ret => true, gapCloser);
        }

        /// <summary>
        ///   Creates a move behind target behavior. If it cannot fully navigate will move to target location
        /// </summary>
        /// <remarks>
        ///   Created 2/12/2011.
        /// </remarks>
        /// <param name="requirements">Aditional requirments.</param>
        /// <returns>.</returns>
        public static Composite CreateMoveBehindTargetBehavior(SimpleBooleanDelegate requirements, Composite gapCloser = null)
        {
            if ( !SingularSettings.Instance.MeleeMoveBehind)
                return new ActionAlwaysFail();

            if (requirements == null)
                return new ActionAlwaysFail();

            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                return new ActionAlwaysFail();

            if (gapCloser == null)
                gapCloser = new ActionAlwaysFail();

            return new Decorator(
                ret =>
                {
                    if (MovementManager.IsMovementDisabled
                        || !SingularRoutine.IsAllowed(Styx.CommonBot.Routines.CapabilityFlags.MoveBehind)
                        || !requirements(ret)
                        || Spell.IsCastingOrChannelling()
                        || Group.MeIsTank)
                        return false;
                    var currentTarget = Me.CurrentTarget;
                    if (currentTarget == null || Me.IsBehind(currentTarget) || !currentTarget.IsAlive || BossList.AvoidRearBosses.Contains(currentTarget.Entry))
                        return false;
                    return (currentTarget.Stunned || currentTarget.CurrentTargetGuid != Me.Guid)
                        && requirements(ret);
                },
                new PrioritySelector(
                    ctx => CalculatePointBehindTarget(),
                    new Decorator(
                        // TODO: FIXME: CanNavigateFully
                        // Mesh sampling to the rescue!
//                        req => Navigator.CanNavigateFully(Me.Location, (Vector3)req),
                        req => true,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug(Color.White, "MoveBehind: behind {0} @ {1:F1} yds", Me.CurrentTarget.SafeName(), Me.CurrentTarget.Distance)),
                            new Action(behindPoint => Navigator.MoveTo((Vector3)behindPoint)),
                            new Action(behindPoint => StopMoving.AtLocation((Vector3)behindPoint)),
                            new PrioritySelector(
                                new Decorator(
                                    req => MovementManager.IsClassMovementAllowed,
                                    gapCloser
                                    ),
                                new ActionAlwaysSucceed()
                                )
                            )
                        )
                    )
                );
        }

        public static Vector3 CalculatePointBehindTarget()
        {
            float facing = Me.CurrentTarget.Rotation;
            facing += WoWMathHelper.DegreesToRadians(180); // was 150 ?
            facing = WoWMathHelper.NormalizeRadian(facing);

            return Me.CurrentTarget.Location.RayCast(facing, Spell.MeleeRange - 2f);
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
        public static Composite CreateMoveToLocationBehavior(SimpleLocationRetriever location, bool stopInRange, DynamicRangeRetriever range)
        {
            return new Decorator(
                ret => !MovementManager.IsMovementDisabled,
                new PrioritySelector(
                    new Decorator(
                        req => stopInRange,
                        new PrioritySelector(
                            new Action( r => {
                                if (StopMoving.Type != StopMoving.StopType.RangeOfLocation || location(r) != StopMoving.Point || range(r) != StopMoving.Range )
                                    StopMoving.InRangeOfLocation(location(r), range(r));
                                return RunStatus.Failure;
                            }),
                            new Decorator(
                                req => Me.Location.Distance(location(req)) < range(req),
                                CreateEnsureMovementStoppedBehavior()
                                )
                            )
                        ),
                    new Decorator(
                        req => Me.Location.Distance(location(req)) > range(req),
                        new Action(r => {
                            string s = stopInRange ? string.Format("MoveInRangeOfLoc({0:F1} yds)", range(r)) : "MoveToLocation";
                            Logger.WriteDebug(Color.White, s + ": towards {0} @ {1:F1} yds", location(r),  range(r));
                            var parameters = new MoveToParameters(location(r)) { DistanceTolerance = Math.Max(range(r), 3f)};
                            Navigator.MoveTo(parameters);
                        })
                        )
                    )
                );
        }

        #endregion

/*
        private static Vector3 lastMoveToRangeSpot = Vector3.Empty;
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
        private static float _range;
        public static Composite CreateMoveToRangeAndStopBehavior(UnitSelectionDelegate toUnit, DynamicRangeRetriever range)
        {
            return new Sequence(
                new Action(r => _range = range(r)),
                CreateMoveToUnitBehavior(toUnit, _range)
                );
        }
*/

        public static Composite CreateWorgenDarkFlightBehavior()
        {
            return new Decorator(
                ret => SingularSettings.Instance.UseRacials
                    && !MovementManager.IsMovementDisabled
                    && Me.IsAlive
                    && Me.IsMoving
                    && !Me.Mounted
                    && !Me.IsOnTransport
                    && !Me.OnTaxi
                    && Me.Race == WoWRace.Worgen
                    && !Me.HasAnyAura("Darkflight", "Running Wild", "Travel Form")
                    && (BotPoi.Current == null || BotPoi.Current.Type == PoiType.None || BotPoi.Current.Location.Distance(Me.Location) > 10)
                    && !Me.IsAboveTheGround(),

                new PrioritySelector(
                    Spell.WaitForCast(),
                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        Spell.BuffSelf("Darkflight")
                        )
                    )
                );
        }

        /// <summary>
        /// top level movement behavior. detects if a mobs are hitting us in the back while Solo
        /// and moves forward diagonally then faces to attempt to get all mobs in front of us.
        /// this helps with being able to parry as well as any conal front attacks
        /// </summary>
        /// <returns></returns>
        public static Composite CreatePositionMobsInFront()
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return new ActionAlwaysFail();

            if (!SingularSettings.Instance.MeleeKeepMobsInFront)
                return new ActionAlwaysFail();

            return new Decorator(
                req => !MovementManager.IsMovementDisabled && Me.GotTarget() && !Me.IsMoving,
                new ThrottlePasses(
                    1,
                    TimeSpan.FromSeconds(2),
                    RunStatus.Failure,
                    new Decorator(
                        req => 1 < Unit.NearbyUnitsInCombatWithUsOrOurStuff.Count( u => u.IsWithinMeleeRange && !Me.IsSafelyFacing(u,160)),
                        // CreateStrafe( 3f, TimeSpan.FromMilliseconds(500))
                        CreateMoveToSide()
                        )
                    )
                );
        }

        internal class CMTSData
        {
            internal float Distance;
            internal float Facing;
            internal float Speed;
            internal float MoveTime;
            internal Vector3 Origin;
            internal Vector3 Destination;
            internal WoWMovement.MovementDirection Direction;
            internal DateTime TimeToStop;

            internal CMTSData()
            {
                Distance = (float) Math.Max( 2.5, Math.Min( 5.0, StyxWoW.Me.CurrentTarget.Distance));
                Facing = Me.RenderFacing;
                Speed = Me.MovementInfo.RunSpeed;
                MoveTime = Distance / Speed;
                Origin = Me.Location;
            }
        }

        public static CMTSData CMTS(object ctx) { return ctx as CMTSData; }

        public static Composite CreateMoveToSide( int maxTime = 1)
        {
            return new Decorator(

                req => Me.GotTarget(),

                new Sequence(

                    ctx => new CMTSData(),

                    new Action(r =>
                    {
                        CMTS(r).Direction = new Random().Next(1) == 0 ? WoWMovement.MovementDirection.StrafeLeft : WoWMovement.MovementDirection.StrafeRight;
                        float dirmultiplier = CMTS(r).Direction == WoWMovement.MovementDirection.StrafeLeft ? -1 : 1;
                        float newFacing = CMTS(r).Facing + (dirmultiplier * (float)Math.PI);    // PI = 90 degree turn, PI/2 is a 45 degree turn
                        CMTS(r).Destination = Me.Location.RayCast(newFacing, CMTS(r).Distance);

                        bool movementObstructed = MeshTraceline(CMTS(r).Origin, CMTS(r).Destination);
                        if (movementObstructed == true)
                        {
                            CMTS(r).Direction = CMTS(r).Direction != WoWMovement.MovementDirection.StrafeLeft ? WoWMovement.MovementDirection.StrafeLeft : WoWMovement.MovementDirection.StrafeRight;
                            dirmultiplier = -dirmultiplier;
                            newFacing = CMTS(r).Facing + (dirmultiplier * ((float)Math.PI) / 2f);
                            CMTS(r).Destination = Me.Location.RayCast(newFacing, CMTS(r).Distance);
                            movementObstructed = MeshTraceline(CMTS(r).Origin, CMTS(r).Destination);
                            if (movementObstructed == true)
                            {
                                Logger.WriteDebug("MoveToSide: unable to move {0:F1} yds to either side of target", CMTS(r).Distance);
                                return RunStatus.Failure;
                            }
                        }

                        Logger.Write( LogColor.Hilite, "MoveToSide: moving diagonally {0} for {1:F1} yds", CMTS(r).Direction.ToString().Substring(6), CMTS(r).Distance);
                        Navigator.MoveTo(CMTS(r).Destination);
                        CMTS(r).TimeToStop = DateTime.UtcNow + TimeSpan.FromSeconds(CMTS(r).MoveTime);
                        return RunStatus.Success;
                    }),

                    new WaitContinue(
                        TimeSpan.FromMilliseconds(500),
                        until => Me.IsMoving,
                        new Action(r => Logger.WriteDebug("MoveToSide: started diagonal movement"))
                        ),

                    new WaitContinue(
                        TimeSpan.FromSeconds(3),
                        until => !Me.IsMoving || DateTime.UtcNow > CMTS(until).TimeToStop,
                        new Action(r => Logger.WriteDebug("MoveToSide: timed stop of diagonal movement {0} successful", Me.IsMoving ? "WAS NOT" : "was"))
                        ),

                    new Action(r =>
                    {
                        if (Me.IsMoving)
                        {
                            Logger.WriteDebug("MoveToSide: forcefully stopping diagonal movement after {0:F2} seconds", CMTS(r).MoveTime);
                            Navigator.PlayerMover.MoveStop();
                        }
                    })
                    )
                );

        }


        public static Composite CreateStrafe(float maxDist, TimeSpan maxTime)
        {
            return new Sequence(

                new Action(r =>
                {
                    Vector3 src = Me.Location;
                    float currFacing = Me.RenderFacing;

                    WoWMovement.MovementDirection dir = new Random().Next(1) == 0 ? WoWMovement.MovementDirection.StrafeLeft : WoWMovement.MovementDirection.StrafeRight;
                    float dirmultiplier = dir == WoWMovement.MovementDirection.StrafeLeft ? -1 : 1;
                    float newFacing = currFacing + (dirmultiplier * ((float)Math.PI) / 2f);
                    Vector3 dst = Me.Location.RayCast(newFacing, maxDist);
                    bool movementObstructed = MeshTraceline(src, dst);
                    if (movementObstructed == true)
                    {
                        dir = dir == WoWMovement.MovementDirection.StrafeLeft ? WoWMovement.MovementDirection.StrafeRight : WoWMovement.MovementDirection.StrafeLeft;
                        dirmultiplier = -dirmultiplier;
                        newFacing = currFacing + (dirmultiplier * ((float)Math.PI) / 2f);
                        dst = Me.Location.RayCast(newFacing, maxDist);
                        movementObstructed = MeshTraceline(src, dst);
                        if (movementObstructed == true)
                        {
                            Logger.WriteDebug("CreateStafe: unable to strafe {0:F1} yds either direction", maxDist);
                            return RunStatus.Failure;
                        }
                    }

                    Logger.WriteDebug("CreateStafe: moving {0} for {1:F1} yds or {2:F2} seconds", dir, maxDist, maxTime.TotalSeconds);
                    WoWMovement.Move(dir, maxTime);
                    return RunStatus.Success;
                }),

                new WaitContinue(
                    TimeSpan.FromMilliseconds(500),
                    until => Me.IsMoving,
                    new Action(r => Logger.WriteDebug("CreateStrafe: movement successfully started"))
                    ),

                new WaitContinue(
                    maxTime,
                    until => !Me.IsMoving,
                    new Action(r => Logger.WriteDebug("CreateStrafe: timed stop of strafe was successful"))
                    ),

                new Action(r =>
                {
                    if (Me.IsMoving)
                    {
                        Logger.WriteDebug("CreateStrafe: forcefully stopping movement since timed stop failed.");
                        WoWMovement.MoveStop();
                    }
                })
                );

        }

        public static bool CanNavigateToMelee(WoWUnit obj)
        {
            PathInformation inf = Navigator.LookupPathInfo(obj, obj.MeleeRange);
            return inf.Navigability == PathNavigability.Navigable;
        }

        /// <summary>
        /// determines if there is an obstruction in a straight line from source point to destination.
        /// </summary>
        /// <param name="src">origin</param>
        /// <param name="dest">destination</param>
        /// <returns>true if obstruction or cannot determine, false if safe to walk</returns>
        public static bool MeshTraceline(Vector3 src, Vector3 dest)
        {
            Vector3 hit;
            bool? pathObstructed = MeshTraceline(src, dest, out hit);
            return pathObstructed == null || (bool) pathObstructed;
        }

        /// <summary>
        /// Checks if obstruction exists in walkable ray on the surface of the mesh from <c>Vector3Src</c> to <c>Vector3Dest</c>.
        /// Return value indicates whether a wall (disjointed polygon edge) was encountered
        /// </summary>
        /// <param name="Vector3Src"></param>
        /// <param name="Vector3Dest"></param>
        /// <param name="hitLocation">
        /// The point where a wall (disjointed polygon edge) was encountered if any, otherwise Vector3.Empty.
        /// The hit calculation is done in 2d so the Z coord will not be accurate; It is an interpolation between <c>Vector3Src</c>'s and <c>Vector3Dest</c>'s Z coords
        /// </param>
        /// <returns>Returns null if a result cannot be determined e.g <c>Vector3Dest</c> is not on mesh, True if a wall (disjointed polygon edge) is encountered otherwise false</returns>
        public static bool? MeshTraceline(Vector3 Vector3Src, Vector3 Vector3Dest, out Vector3 hitLocation)
        {
	        hitLocation = Vector3.Zero;
	        return null;

#warning FIXME: MeshTraceline
			// TODO: MeshTraceline
            // Most users of this can use mesh sampling. For the monk behavior
            // we could probably try generating a path and using its corridor.
            // This will need a significant amount of work, so for now it can be
            // delayed.
//			hitLocation = Vector3.Empty;
//            var meshNav = Navigator.NavigationProvider as MeshNavigator;
//            // 99.999999 % of the time Navigator.NavigationProvider will be a MeshNavigator type or subtype -
//            // but if it isn't then bail because another navigation system is being used.
//            if (meshNav == null)
//                return null;
//            var wowNav = meshNav.Nav;
//            Vector3 detourPointSrc = NavHelper.ToNav(Vector3Src);
//            Vector3 detourPointDest = NavHelper.ToNav(Vector3Dest);
//            // ensure tiles for start and end location are loaded. this does nothing if they're already loaded
//            wowNav.LoadTile(TileIdentifier.GetByPosition(Vector3Src));
//            wowNav.LoadTile(TileIdentifier.GetByPosition(Vector3Dest));
//
//            Vector3 nearestPolyPoint;
//            PolygonReference polyRef;
//            var status = wowNav.MeshQuery.FindNearestPolygon(
//                detourPointSrc,
//                wowNav.Extents,
//                wowNav.QueryFilter.InternalFilter,
//                out nearestPolyPoint,
//                out polyRef);
//            if (status.Failed || polyRef.Id == 0)
//                return null;
//
//            PolygonReference[] raycastPolys;
//            float rayHitDist;
//            Vector3 rayHitNorml;
//
//            //  normalized distance (0 to 1.0) if there was a hit, otherwise float.MaxValue.
//            status = wowNav.MeshQuery.Raycast(
//                polyRef,
//                detourPointSrc,
//                detourPointDest,
//                wowNav.QueryFilter.InternalFilter,
//                500,
//                out raycastPolys,
//                out rayHitDist,
//                out rayHitNorml);
//            if (status.Failed)
//                return null;
//
//            // check if there's a hit
//            if (rayHitDist != float.MaxValue)
//            {
//                // get Vector3Src to Vector3Dest vector
//                var startToEndOffset = Vector3Dest - Vector3Src;
//                // multiply segmentEndToNewPoint by rayHitDistance and add quanity to segmentEnd to get ray hit point.
//                // N.B. the Z coord will be an interpolation between Vector3Src and Vector3Desc Z coords because the hit calculation is done in 2d
//                hitLocation = startToEndOffset * rayHitDist + Vector3Src;
//                return true;
//            }
//            return false;
        }


    }

    public static class StopMoving
    {
        internal static StopType Type { get; set; }
        internal static Vector3 Point { get; set; }
        internal static WoWUnit Unit { get; set; }
        internal static double Range { get; set; }
        internal static SimpleBooleanDelegate StopRequestDelegate { get; set; }

        internal static string callerName;
        internal static string callerFile;
        internal static int callerLine;

        public enum StopType
        {
            None = 0,
            Now,
            AsSoonAsPossible,
            Location,
            RangeOfLocation,
            RangeOfUnit,
            MeleeRangeOfUnit,
            LosOfUnit
        }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        public static void Clear()
        {
            if (Type != StopType.None)
                Set(StopType.None, null, Vector3.Zero, 0, stop => false, null);
        }

        public static void Pulse()
        {
            if (Type == StopType.None)
                return;

            if (MovementManager.IsMovementDisabled)
            {
                Clear();
                return;
            }

            bool stopMovingNow;
            try
            {
                stopMovingNow = StopRequestDelegate(null);
            }
            catch
            {
                stopMovingNow = true;
            }

            if (stopMovingNow )
            {
                if (!Me.IsMoving)
                {
                    if (SingularSettings.DebugStopMoving)
                        Logger.WriteDebug(Color.LightPink, "StopMoving STOP: character already stopped, clearing {0} stop request", Type);
                }
                else
                {
                    Navigator.PlayerMover.MoveStop();
                    WoWMovement.MoveStop();

                    if (SingularSettings.DebugStopMoving)
                    {
                        string line = string.Format("StopMoving STOP: {0}", Type);
                        if (Type == StopType.Location)
                            line += string.Format(", within {0:F1} yds of {1}", Me.Location.Distance(Point), Point);
                        else if (Type == StopType.RangeOfLocation)
                            line += string.Format(", within {0:F1} yds of {1} @ {2:F1} yds", Range, Point, Me.Location.Distance(Point));
                        else if (Unit == null || !Unit.IsValid)
                            line += ", unit == null";
                        else if (Type == StopType.LosOfUnit)
                            line += string.Format(", have LoSS of {0} @ {1:F1} yds", Unit.SafeName(), Unit.Distance);
                        else if (Type == StopType.MeleeRangeOfUnit)
                            line += string.Format(", within melee range of {0} @ {1:F1} yds", Unit.SafeName(), Unit.Distance);
                        else if (Type == StopType.RangeOfUnit)
                            line += string.Format(", within {0:F1} yds of {1} @ {2:F1} yds", Range, Unit.SafeName(), Unit.Distance);

                        if (callerLine > 0)
                            line += ", source: " + callerFile + " @ " + callerLine + " [" + callerName + "]";
                        else if (callerLine == 0)
                            line += ", method: " + callerName;

                        Logger.WriteDebug(Color.LightPink, line);
                    }
                }

                Clear();
            }
        }

        private static void Set( StopType type, WoWUnit unit, Vector3 pt, double range, SimpleBooleanDelegate stop, SimpleBooleanDelegate and )
        {
            if (MovementManager.IsMovementDisabled)
                return;

            if (SingularSettings.DebugStopMoving)
            {
                int levelsUp = 2;
                if (type == StopType.None)
                    levelsUp++;

                StackFrame frame = new StackFrame(levelsUp);
                if (frame != null)
                {
                    MethodBase method = frame.GetMethod();
                    callerName = method.DeclaringType.FullName + "." + method.Name;
                    callerFile = frame.GetFileName();
                    callerLine = frame.GetFileLineNumber();
                    Logger.WriteDebug(Color.DeepPink, "StopMoving SET: type={0} at {1} @ {2} [{3}]", type, callerFile, callerLine, callerName);
                }
                else
                {
                    callerName = "na";
                    callerFile = "na";
                    callerLine = -1;
                }
            }

            Type = type;
            Unit = unit;
            Point = pt;
            Range = range;

            if (and == null)
                and = ret => true;

            StopRequestDelegate = ret => stop(ret) && and(ret);
        }

        public static void AtLocation(Vector3 pt, SimpleBooleanDelegate and = null)
        {
            Set( StopType.Location, null, pt, 0, at => Me.Location.Distance(pt) <= 1, and);
        }

        public static void InRangeOfLocation(Vector3 pt, double range, SimpleBooleanDelegate and = null)
        {
            Set(StopType.RangeOfLocation, null, pt, range, at => Me.Location.Distance(pt) <= range, and);
        }

        public static void InRangeOfUnit(WoWUnit unit, double range, SimpleBooleanDelegate and = null)
        {
            Set(StopType.RangeOfUnit, unit, Vector3.Zero, range, at => Unit == null || !Unit.IsValid || (Unit.SpellDistance() <= range && Unit.InLineOfSpellSight), and);
        }

        public static void InMeleeRangeOfUnit(WoWUnit unit, SimpleBooleanDelegate and = null)
        {
            Set(StopType.RangeOfUnit, unit, Vector3.Zero, 0, at => Unit == null || !Unit.IsValid || Movement.InMoveToMeleeStopRange(Unit), and);
        }

        public static void InLosOfUnit(WoWUnit unit, SimpleBooleanDelegate and = null)
        {
            Set(StopType.LosOfUnit, unit, Vector3.Zero, 0, at => Unit == null || !Unit.IsValid || Movement.InLineOfSpellSight(Unit), and);
        }

        public static void Now()
        {
            Set(StopType.Now, null, Vector3.Zero, 0, at => true, null);
            Pulse();
        }

        public static void AsSoonAsPossible( SimpleBooleanDelegate and = null)
        {
            Set(StopType.AsSoonAsPossible, null, Vector3.Zero, 0, at => true, and);
        }
    }

    public class MoveContext
    {
        public Vector3 Location { get; }
        public WoWUnit Unit { get; }
        public MoveResult Result { get; }

        public float Distance => Location.Distance(StyxWoW.Me.Location);

        public MoveContext()
            : this( StyxWoW.Me.CurrentTarget)
        {
        }

        private MoveContext(WoWUnit unit)
        {
            Unit = unit;
            Location = Unit.Location;
            MoveToParameters parameters = new MoveToParameters(Location) {DistanceTolerance = unit.MeleeRange};
            Result = Navigator.MoveTo(parameters);
        }
    }


    public delegate Vector3 SimpleLocationRetriever(object context);

    public delegate float DynamicRangeRetriever(object context);
}