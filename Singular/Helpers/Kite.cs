using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Collections.Generic;
using Styx.Pathing;
using CommonBehaviors.Actions;
using System.Drawing;
using Styx.WoWInternals;
using Styx.Helpers;


namespace Singular.Helpers
{
    /// <summary>
    /// class which implements kiting.  finds the closest safest spot that can be navigated to and moves there. 
    /// can be configured to perform behaviors while running away (back to target), while jump turning (facing target),
    /// or both.  any abilities cast during these phases must be instant
    /// </summary>
    public static class Kite
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        public enum State
        {
            None,
            Moving,
            NonFaceAttack,
            AttackWithoutJumpTurn,
            JumpTurnAndAttack,
            End
        }

        public static State bstate = State.None;
        public static WoWPoint safeSpot = WoWPoint.Empty;
        public static DateTime timeOut = DateTime.Now;

        const int DISTANCE_WE_NEED_TO_START_BACK_PEDAL = 7;
        const int DISTANCE_CLOSE_ENOUGH_TO_DESTINATION = 2;
        const int DISTANCE_TOO_FAR_FROM_DESTINATION = 30;

        /// <summary>
        /// creates a behavior that provides kiting support.  intent is to move to closest safest spot possible away from target
        /// while optionally performing behaviors while moving there
        /// </summary>
        /// <param name="runawayAttack">behavior to use while running away (should not require facing target.) should be null if not needed</param>
        /// <param name="jumpturnAttack">behavior to use in middle of jump turn (while facing target.)  should be null if not needed</param>
        /// <returns></returns>
        public static Composite CreateKitingBehavior(Composite runawayAttack, Composite jumpturnAttack)
        {
            if (!SingularSettings.Instance.AllowKiting)
                return new PrioritySelector();

            return
                new PrioritySelector(
                    new Decorator(
                        ret => bstate != State.None,

                        new PrioritySelector(
                            new Decorator(ret => !StyxWoW.IsInGame, new Action(ret => EndKiting("BP: not in game so cancelling"))),
                            new Decorator(ret => !Me.IsAlive, new Action(ret => EndKiting("BP: i am dead so cancelling"))),
                            new Decorator(ret => timeOut < DateTime.Now, new Action(ret => EndKiting("BP: taking too long, so cancelling"))),
                            new Decorator(ret => jumpturnAttack != null && !Me.GotTarget, new Action(ret => EndKiting("BP: attack behavior but no target, cancelling"))),

                            new Decorator(ret => Me.Stunned || Me.IsStunned(), new Action(ret => EndKiting("BP: stunned, cancelling"))),
                            new Decorator(ret => Me.Rooted || Me.IsRooted(), new Action(ret => EndKiting("BP: rooted, cancelling"))),

                            new Decorator(ret => Me.Location.Distance(safeSpot) < DISTANCE_CLOSE_ENOUGH_TO_DESTINATION, new Action(ret => EndKiting("BP: reached safe spot!!!!"))),
                            new Decorator(ret => Me.Location.Distance(safeSpot) > DISTANCE_TOO_FAR_FROM_DESTINATION, new Action(ret => EndKiting("BP: too far from safe spot, cancelling"))),

                            new Decorator(ret => bstate == State.Moving,
                                new Sequence(
                                    new Action(d => Navigator.MoveTo(safeSpot)),
                // following 3 lines make sure we are facing and have started moving in the correct direction.  it will force
                //  .. a minimum wait of 250 ms after movement has started in the correct direction
                                    new WaitContinue(new TimeSpan(0, 0, 0, 0, 250), r => Me.IsDirectlyFacing(safeSpot), new ActionAlwaysSucceed()),
                                    new WaitContinue(new TimeSpan(0, 0, 0, 0, 500), r => Me.IsMoving, new ActionAlwaysSucceed()),  // wait till we are moving (should be very quick)
                                    new WaitContinue(new TimeSpan(0, 0, 0, 0, 250), r => !Me.IsMoving, new Action( r => { return RunStatus.Failure; } )),    // wait for 250ms, failing if we stop moving

                                    new Action(ret =>
                                    {
                                        if (runawayAttack != null)
                                        {
                                            bstate = State.NonFaceAttack;
                                            Logger.WriteDebug(Color.Cyan, "BP: transition from Moving to NonFaceAttack");
                                            return RunStatus.Failure;
                                        }
                                        return RunStatus.Success;
                                    }),

                                    new Action(delegate
            {
                if (jumpturnAttack != null)
                {
                    if (JumpTurn.IsJumpTurnInProgress())
                    {
                        bstate = State.JumpTurnAndAttack;
                        Logger.WriteDebug(Color.Cyan, "BP: transition error - active jumpturn? forcing state JumpTurn");
                        return RunStatus.Failure;
                    }

                    if (Me.IsMoving && Me.IsSafelyFacing(Me.CurrentTarget, 120f))
                    {
                        bstate = State.AttackWithoutJumpTurn;
                        Logger.WriteDebug(Color.Cyan, "BP: already facing so transition from Moving to AttackNoJumpTurn");
                        return RunStatus.Failure;
                    }

                    if (JumpTurn.IsJumpTurnNeeded())
                    {
                        bstate = State.JumpTurnAndAttack;
                        Logger.WriteDebug(Color.Cyan, "BP: transition from Moving to JumpTurn");
                        return RunStatus.Failure;
                    }
                }
                return RunStatus.Success;
            })
                                    )
                /*
                                                new Action( delegate {
                                                    Navigator.MoveTo( safeSpot );
                                                    if (attackBehavior != null )
                                                    {
                                                        if (WoWMathHelper.IsFacing( Me.Location, Me.RenderFacing, safeSpot, SafeArea.ONE_DEGREE_AS_RADIAN ))
                                                        {
                                                            if (JumpTurn.IsNeeded())
                                                            {
                                                                bstate = State.JumpTurn;
                                                                Logger.WriteDebug(Color.Cyan, "BP: transition from Moving to JumpTurn");
                                                            }
                                                            else if (JumpTurn.ActiveJumpTurn())
                                                            {
                                                                bstate = State.JumpTurn;
                                                                Logger.WriteDebug(Color.Cyan, "BP: transition error - active jumpturn? forcing state JumpTurn");
                                                            }
                                                        }
                                                    }
                                                    })
                */
                                ),

                            new Decorator(ret => bstate == State.NonFaceAttack,
                                new PrioritySelector(
                                    new Sequence(
                                        new Action(ret => Logger.WriteDebug(Color.Cyan, "BP: entering NonFaceAttack behavior")),
                                        runawayAttack ?? new Action( r => { return RunStatus.Failure; } ),
                                        new Action( r => { return RunStatus.Failure; } )
                                        ),
                                    new Action(delegate
            {
                Logger.WriteDebug(Color.Cyan, "BP: transition from NonFaceAttack to Moving");
                bstate = State.Moving;
            })
                                    )
                                ),

                            new Decorator(ret => bstate == State.AttackWithoutJumpTurn,
                                new PrioritySelector(
                                    new Sequence(
                                        new Action(ret => Logger.WriteDebug(Color.Cyan, "BP: entering AttackNoJumpTurn behavior")),
                                        jumpturnAttack ?? new Action( r => { return RunStatus.Failure; } ),
                                        new Action( r => { return RunStatus.Failure; } )
                                        ),
                                    new Action(delegate
            {
                Logger.WriteDebug(Color.Cyan, "BP: transition from AttackNoJumpTurn to Moving");
                bstate = State.Moving;
            })
                                    )
                                ),

                            new Decorator(ret => bstate == State.JumpTurnAndAttack,
                                new PrioritySelector(
                                    JumpTurn.CreateBehavior(jumpturnAttack),
                                    new Decorator(ret => !JumpTurn.IsJumpTurnInProgress(),
                                        new Action(delegate
            {
                bstate = State.Moving;
                Logger.WriteDebug(Color.Cyan, "BP: transition from JumpTurn to Moving");
            })
                                        )
                                    )
                                ),

                            new Action(ret => Logger.WriteDebug(Color.Cyan, "BP: fell through with state {0}", bstate.ToString()))
                            )
                        ),

                    new Decorator(
                        ret => IsKitingNeeded(),
                        new Action(delegate
            {
                bstate = State.Moving;
                Logger.WriteDebug(Color.Cyan, "Back Peddle");
            }))
                    );
        }

        private static bool IsKitingNeeded()
        {
            if (SingularSettings.Instance.DisableAllMovement || !SingularSettings.Instance.AllowKiting)
                return false;

            // note:  PullDistance MUST be longer than our out of melee distance (DISTANCE_WE_NEED_TO_START_BACK_PEDDLING)
            // otherwise it will run back and forth
            if (IsKitingActive() || !Me.IsAlive || Me.IsCasting)
                return false;

            if (Me.Stunned || Me.IsStunned())
                return false;

            if (Me.Rooted || Me.IsRooted())
                return false;

            if (Me.IsFalling)
                return false;

            WoWUnit mob = SafeArea.NearestEnemyMobAttackingMe;
            //mob = Me.CurrentTarget;  // FOR TESTING ONLY 
            if (mob == null)
                return false;

            // check if 5yds beyond melee
            if (mob.Distance > 10f)
                return false;

            SafeArea sa = new SafeArea();
            if (Battlegrounds.IsInsideBattleground)
            {
                sa.MinSafeDistance = 12;
                sa.MinScanDistance = 15;
                sa.IncrementScanDistance = 10;
                sa.MaxScanDistance = sa.MinScanDistance + 2 * sa.IncrementScanDistance;
                sa.RaysToCheck = 18;
                sa.MobToRunFrom = mob;
                sa.LineOfSightMob = Me.CurrentTarget;
                sa.CheckLineOfSightToSafeLocation = false;
                sa.CheckSpellLineOfSightToMob = false;
            }
            else
            {
                sa.MinSafeDistance = 20;
                sa.MinScanDistance = 10;
                sa.IncrementScanDistance = 5;
                sa.MaxScanDistance = sa.MinScanDistance + (2 * sa.IncrementScanDistance);
                sa.RaysToCheck = 36;
                sa.MobToRunFrom = mob;
                sa.LineOfSightMob = Me.CurrentTarget;
                sa.CheckLineOfSightToSafeLocation = false;
                sa.CheckSpellLineOfSightToMob = false;
            }

            safeSpot = sa.FindLocation();
            if (safeSpot == WoWPoint.Empty)
                return false;

            return BeginKiting(String.Format("BP: back peddle initiated due to {0} @ {1:F1} yds", mob.Name, mob.Distance));
        }

        public static bool IsKitingActive()
        {
            return bstate != State.None;
        }

        public static bool BeginKiting(string s)
        {
            bstate = State.Moving;
            Logger.WriteDebug(Color.Gold, s);
            timeOut = DateTime.Now.Add(new TimeSpan(0, 0, 6));
            return true;
        }

        public static RunStatus EndKiting(string s)
        {
            bstate = State.None;
            JumpTurn.EndJumpTurn(null);
            Logger.WriteDebug(Color.Gold, s);
            WoWMovement.StopFace();
            WoWMovement.MoveStop(WoWMovement.MovementDirection.All);
            return RunStatus.Success;
        }
    }

    public static class JumpTurn
    {
        enum State
        {
            None,
            Jump,
            Face,
            Attack,
            FaceRestore,
            EndJump
        }
        static State jstate = State.None;
        static float originalFacing;

        public static Composite CreateBehavior(Composite jumpturnAttack)
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => jstate != State.None,
                        new PrioritySelector(
                // add sanity checks here to test if jump should continue
                            new Decorator(ret => !StyxWoW.Me.IsAlive, new Action(ret => EndJumpTurn("JT: I am dead, cancelling"))),
                            new Decorator(ret => StyxWoW.Me.Rooted || StyxWoW.Me.IsRooted(), new Action(ret => EndJumpTurn("JT: I am rooted, cancelling"))),
                            new Decorator(ret => StyxWoW.Me.Stunned || StyxWoW.Me.IsStunned(), new Action(ret => EndJumpTurn("JT: I am stunned, cancelling"))),

                            new Decorator(ret => jstate == State.Jump,
                                new Sequence(
                                    new Action(delegate
            {
                // jump up
                Logger.WriteDebug(Color.Cyan, "JT: enter Jump state");
                WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
            }),
                                    new WaitContinue(new TimeSpan(0, 0, 0, 0, 100), ret => false, new ActionAlwaysSucceed()),
                                    new PrioritySelector(
                                        new Wait(new TimeSpan(0, 0, 0, 0, 250), ret => StyxWoW.Me.MovementInfo.IsAscending || StyxWoW.Me.MovementInfo.IsDescending || StyxWoW.Me.MovementInfo.IsFalling, new ActionAlwaysSucceed()),
                                        new Action(ret => { EndJumpTurn("JT: timed out waiting for JumpAscend to occur"); return RunStatus.Failure; })
                                        ),
                                    new Action(delegate
            {
                Logger.WriteDebug(Color.Cyan, "JT: transition from Jump to Face");
                jstate = State.Face;
            })
                                    )
                                ),
                            new Decorator(ret => jstate == State.Face,
                                new Sequence(
                                    new Action(ret => StyxWoW.Me.SetFacing(StyxWoW.Me.CurrentTarget)),
                                    new PrioritySelector(
                                        new Wait(new TimeSpan(0, 0, 0, 0, 500), ret => StyxWoW.Me.IsSafelyFacing(StyxWoW.Me.CurrentTarget, 20f), new ActionAlwaysSucceed()),
                                        new Action(ret => Logger.WriteDebug("JT: timed out waiting for SafelyFacing to occur"))
                                        ),
                                    new Action(delegate
            {
                WoWMovement.StopFace();
                if (StyxWoW.Me.IsSafelyFacing(StyxWoW.Me.CurrentTarget, 20f))
                {
                    Logger.WriteDebug(Color.Cyan, "JT: transition from Face to Attack");
                    jstate = State.Attack;
                }
                else
                {
                    Logger.WriteDebug(Color.Cyan, "JT: transition from Face to FaceRestore");
                    jstate = State.FaceRestore;
                }
            })
                                    )
                                ),
                            new Decorator(ret => jstate == State.Attack,
                                new PrioritySelector(
                                    new Sequence(
                                        new ActionDebugString("JT: entering Attack behavior"),
                                        jumpturnAttack ?? new Action( r => { return RunStatus.Failure; } ),
                                        new Action( r => { return RunStatus.Failure; } )
                                        ),
                                    new Action(delegate
            {
                Logger.WriteDebug(Color.Cyan, "JT: transition from Attack to FaceRestore");
                jstate = State.FaceRestore;
            })
                                    )
                                ),
                            new Decorator(ret => jstate == State.FaceRestore,
                                new Sequence(
                /*
                                                    new Action( ret => StyxWoW.Me.SetFacing( originalFacing)),
                                                    new WaitContinue( new TimeSpan(0,0,0,0,250), ret => StyxWoW.Me.IsDirectlyFacing(originalFacing), new ActionAlwaysSucceed()),
                */
                                    new Action(ret => Navigator.MoveTo(Kite.safeSpot)),
                                    new WaitContinue(new TimeSpan(0, 0, 0, 0, 250), ret => StyxWoW.Me.IsDirectlyFacing(originalFacing), new ActionAlwaysSucceed()),

                                    new Action(delegate
            {
                Logger.WriteDebug(Color.Cyan, "JT: transition from FaceRestore to EndJump");
                WoWMovement.StopFace();
                jstate = State.EndJump;
            })
                                    )
                                ),
                            new Decorator(ret => jstate == State.EndJump,
                                new Action(delegate
            {
                EndJumpTurn("JT: transition from EndJump to None");
            })
                                ),

                            new Action(ret => Logger.WriteDebug(Color.Cyan, "JT: fell through with state {0}", jstate.ToString()))
                            )
                        ),
                    new Decorator(
                        ret => IsJumpTurnNeeded(),
                        new Action(delegate
            {
                originalFacing = StyxWoW.Me.RenderFacing;
                jstate = State.Jump;
                Logger.WriteDebug(Color.Cyan, "Jump Turn");
            })
                        )
                    );
        }

        public static bool IsJumpTurnNeeded()
        {
            if (SingularSettings.Instance.DisableAllMovement)
                return false;

            if (SpellManager.GlobalCooldown || StyxWoW.Me.IsCasting)
                return false;

            if (IsJumpTurnInProgress())
                return false;

            if (!StyxWoW.Me.IsMoving)
                return false;

            if (StyxWoW.Me.IsFalling || StyxWoW.Me.IsSwimming || StyxWoW.Me.IsFlying)
                return false;

            if (StyxWoW.Me.Rooted || StyxWoW.Me.IsRooted())
                return false;

            if (StyxWoW.Me.Stunned || StyxWoW.Me.IsStunned())
                return false;

            return true;
        }

        public static bool IsJumpTurnInProgress()
        {
            return jstate != State.None;
        }

        private static RunStatus BeginJumpTurn()
        {
            // find safespot
            throw new NotImplementedException();
        }

        public static RunStatus EndJumpTurn(string s)
        {
            if (IsJumpTurnInProgress())
            {
                jstate = State.None;
                if (s != null)
                    Logger.WriteDebug(Color.Cyan, s);

                WoWMovement.StopFace();
                WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
            }

            return RunStatus.Success;
        }
    }

#if OLD
    public static class Kiting
    {
        const int DISTANCE_WE_NEED_TO_START_BACK_PEDAL = 7;
        const int DISTANCE_CLOSE_ENOUGH_TO_DESTINATION = 2;
        const int DISTANCE_TOO_FAR_FROM_DESTINATION = 30;
        const int WAIT_BEFORE_ATTACK = 150;
        const int WAIT_AFTER_ATTACK = 100;

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        enum JumpTurnState
        {
            None,
            RunAway,
            JumpUp,
            WaitBeforeAttack,
            Attacking,
            WaitAfterAttack
        }

        static JumpTurnState state;
        static WoWPoint bpwjDest;
        static DateTime movementCheck;
        static DateTime stopKiting;
        static DateTime stopJump;
        static DateTime waitBeforeJumpTurnAttack;
        static DateTime waitAfterJumpTurnAttack;

        /// <summary>
        /// creates Composite that if we are too close to melee range
        /// will pick a safe point away and run to it, doing jump turns
        /// if non-null <paramref name="attack"/> composite provided
        /// </summary>
        /// <param name="attack">attack rotation of instant spells. jump turn disabled if 'null'</param>
        /// <returns>Composite to add in behavior</returns>
        public static Composite CreateKitingWithJump(Composite attack)
        {
            return
                new Decorator(
                    ret => !SingularSettings.Instance.DisableAllMovement,
                    new PrioritySelector(

                        // BACK PEDDLE
                        new Decorator(ret => IsKitingNeeded(), new Action(ret => BeginKitingToSafeArea())),

                        // IN PROGRESS ?
                        new Decorator(
                            ret => state != JumpTurnState.None, // do this to do fewer if's during 95% of combat

                            new PrioritySelector(

                                // RESET IF NOT READY
                                new Decorator(ret => stopKiting < DateTime.Now, new Action(ret => EndKiting("BPWJ: back peddle timed out, cancelling"))),
                                new Decorator(ret => !StyxWoW.IsInGame, new Action(ret => EndKiting("BPWJ: not in game so cancelling"))),
                                new Decorator(ret => !Me.IsAlive, new Action(ret => EndKiting("BPWJ: i am dead so cancelling"))),
                                new Decorator(ret => !Me.GotTarget, new Action(ret => EndKiting("BPWJ: no target, cancelling"))),
                                new Decorator(ret => !Me.CurrentTarget.IsAlive, new Action(ret => EndKiting("BPWJ: target dead, cancelling"))),

                                // new DecoratorContinue(ret => Me.IsMoving, new Action( a => movementCheck = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 1000)))),
                                new Decorator(ret => movementCheck < DateTime.Now && !Me.IsMoving, 
                                    new Action( delegate {
                                        if (Me.Stunned || Me.IsStunned())
                                            EndKiting("BPWJ: stunned so cancelling");
                                        else if (Me.Rooted || Me.IsRooted())
                                            EndKiting("BPWJ: rooted so cancelling");
                                        else
                                            return RunStatus.Failure;

                                        return RunStatus.Success;
                                    })),

                                // STOP IF NEAR DEST
                                new Decorator(
                                    ret => bpwjDest.Distance(Me.Location) < DISTANCE_CLOSE_ENOUGH_TO_DESTINATION,
                                    new Action(ret => EndKiting("BPWJ: reached destination"))),

                                // STOP IF TOO FAR AWAY FROM DEST
                                new Decorator(
                                    ret => bpwjDest.Distance(Me.Location) > DISTANCE_TOO_FAR_FROM_DESTINATION,
                                    new Action(ret => EndKiting(String.Format("BPWJ: cancel, too far from destination @ {0:F1} yds", bpwjDest.Distance(Me.Location))))),

                                // STOP IF JUMP TIMED OUT
                                new Decorator(
                                    ret => IsStateJumping() && stopJump <= DateTime.Now,
                                    new Action(ret => EndKiting("BPWJ: cancel, jump turn timeout"))),

                                // GOBLINS ROCKET JUMP IF WE CAN AND MAKES SENSE 
                                Spell.BuffSelf("Rocket Jump", ret => IsRocketJumpNeeded()),

                                // DO JUMP TURN NOW (MUST APPEAR AFTER GCD / CASTING CHECK SO JUMP NOT STARTED WHILE ABILITY CANNOT BE CAST
                                new Decorator(ret => state == JumpTurnState.RunAway, new Action(ret => StartJumpTurn())),

                                // JUMPED AND TURNED, SO WAITING TO ATTACK
                                new Decorator(ret => state == JumpTurnState.WaitBeforeAttack, new Action(ret => WaitingBeforeAttack())),

                                // ATTACK IN MID-AIR
                                new Decorator(
                                    ret => state == JumpTurnState.Attacking && Me.IsSafelyFacing(Me.CurrentTarget),
                                    new Sequence(
                                        new Decorator(ret => attack != null, attack),
                                        new Action(ret => Logger.WriteDebug( Color.Cyan,  "BPWJ: post-attack")),
                                        new Action(ret => state = JumpTurnState.WaitAfterAttack)
                                        )
                                    ),

                                // ATTACKED, SO WAITING TO TURN BACK AND MOVE AWAY
                                new Decorator(ret => state == JumpTurnState.WaitAfterAttack, new Action(ret => WaitingAfterAttack())),

                                // CATCH ALL
                                new Action(ret => Logger.WriteDebug( Color.Cyan,  "BPWJ: current state {0}...", state.ToString()))
                                )
                            )
                        )
                    );
        }

        private static bool IsRocketJumpNeeded()
        {
            return SingularSettings.Instance.UseRacials
                && state == JumpTurnState.RunAway
                && Me.Race == WoWRace.Goblin
                && !SpellManager.GlobalCooldown
                && !Me.IsCasting
                && Me.Location.Distance(bpwjDest) >= 15
                && !Me.IsFalling
                && !Me.IsOnTransport
                && Me.IsSafelyFacing(bpwjDest)
                && SpellManager.CanCast("Rocket Jump", Me);
        }

        private static bool IsKitingNeeded()
        {
            // note:  PullDistance MUST be longer than our out of melee distance (DISTANCE_WE_NEED_TO_START_BACK_PEDDLING)
            // otherwise it will run back and forth
            WoWUnit mob = SafeArea.NearestEnemyMobAttackingMe;
            return state == JumpTurnState.None
                && !Me.IsCasting
                && Me.IsAlive
                && mob != null
                && (Me.Level - mob.Level) < (mob.Elite ? 10 : 5)
                && !Spell.BuffWasCastRecently("Disengage", 1000)
                && !Me.Stunned && !Me.IsStunned()
                && !Me.Rooted && !Me.IsRooted()
                && !Me.IsFalling
                && Styx.Helpers.CharacterSettings.Instance.PullDistance >= (Me.MeleeDistance(mob) + DISTANCE_WE_NEED_TO_START_BACK_PEDAL)
                && mob.Distance <= Me.MeleeDistance(mob) + DISTANCE_WE_NEED_TO_START_BACK_PEDAL
                // && Me.CurrentTarget.Distance <= Me.MeleeDistance(mob) + DISTANCE_WE_NEED_TO_START_BACK_PEDAL
                // && (Me.CurrentTarget.CurrentTarget == null || Me.CurrentTarget.CurrentTarget != Me || Me.CurrentTarget.IsMoveSpeedReduced())
                ;
        }
/*
        private static RunStatus BeginKiting()
        {
            bpwjDest = WoWMathHelper.CalculatePointFrom(Me.Location, Me.CurrentTarget.Location, Spell.MeleeRange + DISTANCE_WE_NEED_TO_START_BACK_PEDAL);
            if (Navigator.CanNavigateFully(StyxWoW.Me.Location, bpwjDest))
            {
                state = JumpTurnState.RunAway;
                Logger.WriteDebug( Color.Cyan,  "Back peddling");
                movementCheck = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 500));
                Navigator.MoveTo(bpwjDest);
            }

            return RunStatus.Success;
        }
*/
        private static RunStatus BeginKitingToSafeArea()
        {
            SafeArea sa = new SafeArea();
            sa.MinScanDistance = Spell.MeleeRange + 15;
            sa.IncrementScanDistance = 5;
            sa.MaxScanDistance = sa.MinScanDistance + sa.IncrementScanDistance;
            sa.RaysToCheck = 18;
            sa.MinSafeDistance = 15;

            bpwjDest = sa.FindLocation();
            if (bpwjDest != WoWPoint.Empty)
            {
                state = JumpTurnState.RunAway;
                Logger.WriteDebug( Color.Cyan,  "Back peddling");
                Navigator.MoveTo(bpwjDest);
                movementCheck = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 1000));
                stopKiting = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 8000));
                return RunStatus.Success;
            }

            return RunStatus.Failure;
        }

        private static RunStatus EndKiting(string s)
        {
            state = JumpTurnState.None;
            Logger.WriteDebug( Color.Gold,  s);
            WoWMovement.StopFace();
            WoWMovement.MoveStop(WoWMovement.MovementDirection.All);
            return RunStatus.Success;
        }

        private static bool IsStateJumping()
        {
            return state != JumpTurnState.None && state != JumpTurnState.RunAway;
        }

        private static RunStatus StartJumpTurn()
        {
            Debug.Assert(state == JumpTurnState.RunAway, "State is not RUNAWAY");

            if (!SingularSettings.Instance.Hunter.UseJumpTurn)
                return RunStatus.Failure;

            if (SpellManager.GlobalCooldown || Me.IsCasting)
                return RunStatus.Failure;

            if (Me.IsSwimming)
            {
                Logger.WriteDebug( Color.Cyan, "BPWJ: swimming so Jump Turn skipped");
                return RunStatus.Failure;
            }

            stopJump = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 750));
            waitBeforeJumpTurnAttack = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, WAIT_BEFORE_ATTACK ));
            WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
            Me.CurrentTarget.Face();
            Logger.WriteDebug( Color.Cyan,  "Jump Turn");

            state = JumpTurnState.WaitBeforeAttack;
            return RunStatus.Success;
        }

        private static RunStatus WaitingBeforeAttack()
        {
            Debug.Assert(state == JumpTurnState.WaitBeforeAttack, "State is not WAITBEFOREATTACK");

            Logger.WriteDebug(Color.Cyan, "BPWJ: pre-attack");
            if (waitBeforeJumpTurnAttack > DateTime.Now)
            {
                return RunStatus.Success;
            }

            waitAfterJumpTurnAttack = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, WAIT_AFTER_ATTACK ));
            Logger.WriteDebug( Color.Cyan,  "BPWJ: transition to attack");

            state = JumpTurnState.Attacking;
            return RunStatus.Failure;
        }

        private static RunStatus WaitingAfterAttack()
        {
            Debug.Assert(state == JumpTurnState.WaitAfterAttack, "State is not WAITAFTERATTACK");

            Logger.WriteDebug(Color.Cyan, "BPWJ: waiting to turn back and move away");
            if (waitAfterJumpTurnAttack > DateTime.Now)
            {
                return RunStatus.Success;
            }

            Logger.WriteDebug( Color.Cyan,  "BPWJ: turning back and move away");
            WoWMovement.StopFace();
            WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
            Navigator.MoveTo(bpwjDest);

            state = JumpTurnState.RunAway;
            return RunStatus.Success;
        }
    }
#endif

    /// <summary>
    /// <para>provides ability to find a safe spot nearby that is away from
    /// enemies.  properties can be modified to customize search</para>
    /// </summary>
    public class SafeArea
    {
        public const float ONE_DEGREE_AS_RADIAN = 0.0174532925f;

        /// <summary><para>Minimum distance away from character to find safe place</para></summary>
        public float MinScanDistance { get; set; }
        /// <summary><para>Maximum distance away from character to find safe place</para></summary>
        public float MaxScanDistance { get; set; }
        /// <summary><para>Increment added each repetition to MinScanDistance until MaxScanDistance reached.</para></summary>
        public float IncrementScanDistance { get; set; }
        /// <summary><para>Number of evenly spaced checks around perimter.  36 would yield 36 checks around perimeter spaced 10 degrees apart.</para></summary>
        public int RaysToCheck { get; set; }
        /// <summary>Minimum distance from safe spot to nearest enemy</summary>
        public int MinSafeDistance { get; set; }
        /// <summary>Range to keep to LineOfSightMob if CheckRangeToLineOfSightMob</summary>
        public float RangeToLineOfSightMob { get; set; }
        /// <summary>Unit we are trying to get away from</summary>
        public WoWUnit MobToRunFrom { get; set; }
        /// <summary>Unit we need to be able to attack from destination</summary>
        public WoWUnit LineOfSightMob { get; set; }
        /// <summary>Require line of sight from origin to safe location</summary>
        public bool CheckLineOfSightToSafeLocation { get; set; }
        /// <summary>Require spell line of sight from safe location to mob</summary>
        public bool CheckSpellLineOfSightToMob { get; set; }
        /// <summary>Require safe location to be within 40 yds of current target</summary>
        public bool CheckRangeToLineOfSightMob { get; set; }
        /// <summary>Select best navigable point available</summary>
        public bool ChooseSafestAvailable { get; set; }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        public SafeArea()
        {
            MinScanDistance = 10;               // minimum distance to scan for safe spot
            MaxScanDistance = 30;               // maximum distance to scan for safe spot
            IncrementScanDistance = 10;             // distance increment for each iteration of safe spot check 
            RaysToCheck = 36;                   // number of checks within 360 degrees (36 = every 10 degrees)
            MinSafeDistance = 15;                       // radius of the minimal safe area at safe spot
            MobToRunFrom = NearestEnemyMobAttackingMe;  // trying to get away from this guy
            LineOfSightMob = Me.CurrentTarget;          // safe spot must have line of sight to this mob

            CheckLineOfSightToSafeLocation = true;
            CheckSpellLineOfSightToMob = LineOfSightMob != null;

            RangeToLineOfSightMob = 0;
            CheckRangeToLineOfSightMob = false;
            /*
                        if (Me.Class == WoWClass.Priest || Me.Class == WoWClass.Warlock || Me.Class == WoWClass.Mage || Me.Class == WoWClass.Hunter)
                            RangeToLineOfSightMob = Styx.Helpers.CharacterSettings.Instance.PullDistance;
                        else if (SpellManager.HasSpell("Meditation"))
                            RangeToLineOfSightMob = Styx.Helpers.CharacterSettings.Instance.PullDistance;
                        else if (SpellManager.HasSpell("Starsurge"))
                            RangeToLineOfSightMob = Styx.Helpers.CharacterSettings.Instance.PullDistance;
                        else if (SpellManager.HasSpell("Thunderstorm"))
                            RangeToLineOfSightMob = Styx.Helpers.CharacterSettings.Instance.PullDistance;

                        CheckRangeToLineOfSightMob = Me.GotTarget && RangeToLineOfSightMob > 0;
            */
            ChooseSafestAvailable = true;
        }

        public static bool IsEnemy(WoWUnit u)
        {
            if (u == null || !u.CanSelect || !u.Attackable || !u.IsAlive || u.IsNonCombatPet || u.IsCritter)
                return false;

            if (!u.IsPlayer)
                return u.IsHostile || u.Aggro || u.PetAggro;

            WoWPlayer p = u.ToPlayer();
/* // not supported currently
            if (Battlegrounds.IsInsideBattleground)
                return p.BattlefieldArenaFaction != Me.BattlefieldArenaFaction;
*/
            return p.IsHorde != Me.IsHorde;
        }

        public static IEnumerable<WoWUnit> AllEnemyMobs
        {
            get
            {
                return ObjectManager.ObjectList.Where(o => o is WoWUnit && IsEnemy(o.ToUnit() )).Select(o => o.ToUnit()).ToList();
            }
        }

        public List<WoWPoint> AllEnemyMobLocationsToCheck
        {
            get
            {
                return (from u in AllEnemyMobs
                        where u != MobToRunFrom && u != LineOfSightMob && u.Distance2DSqr < (65 * 65)
                        select u.Location).ToList();
            }
        }

        public static IEnumerable<WoWUnit> AllEnemyMobsAttackingMe
        {
            get
            {
                return AllEnemyMobs.Where(u =>
                    (u.CurrentTargetGuid == Me.Guid || Me.CurrentTargetGuid == u.Guid)
                    && !u.IsPet
                    && StyxWoW.Me.Level < (u.Level + (u.Elite ? 12 : 5)));
            }
        }

        public static WoWUnit NearestEnemyMobAttackingMe
        {
            get
            {
                return AllEnemyMobsAttackingMe.OrderBy(uu => uu.Distance2DSqr).FirstOrDefault();
            }
        }

        public static WoWPoint NearestMobLoc(WoWPoint p, IEnumerable<WoWPoint> mobLocs)
        {
            if (!mobLocs.Any())
                return WoWPoint.Empty;

            return mobLocs.OrderBy(u => u.Distance2DSqr(p)).First();
        }

        /// <summary>
        /// locates safe point away from enemies
        /// </summary>
        /// <param name="minSafeDist">min distance to be safe</param>
        /// <returns></returns>
        public WoWPoint FindLocation()
        {
            return FindLocation(Me.Location);
        }

        public WoWPoint FindLocation(WoWPoint ptOrigin)
        {
            DateTime startFind = DateTime.Now;
            int countPointsChecked = 0;
            int countFailToPointNav = 0;
            int countFailRange = 0;
            int countFailSafe = 0;
            int countFailToPointLoS = 0;
            int countFailToMobLoS = 0;
            double furthestNearMobDistSqr = 0f;
            WoWPoint ptFurthest = WoWPoint.Empty;
            bool reallyCheckRangeToLineOfSightMob = CheckRangeToLineOfSightMob && Me.GotTarget;
            WoWPoint ptAdjOrigin = ptOrigin;
            ptAdjOrigin.Z += 1f;

            WoWPoint ptDestination = new WoWPoint();
            List<WoWPoint> mobLocations = new List<WoWPoint>();
            float arcIncrement = ((float)Math.PI * 2) / RaysToCheck;
            
            mobLocations = AllEnemyMobLocationsToCheck;
            double minSafeDistSqr = MinSafeDistance * MinSafeDistance;

            float baseDestinationFacing = MobToRunFrom == null ?
                                            Me.RenderFacing + (float)Math.PI
                                            : Styx.Helpers.WoWMathHelper.CalculateNeededFacing(MobToRunFrom.Location, Me.Location);

            // Logger.WriteDebug( Color.Cyan, "SafeArea: search near {0:F0}d @ {1:F1} yds for mob free area", RadiansToDegrees(baseDestinationFacing), MinSafeDistance);

            for (int arcIndex = 0; arcIndex < RaysToCheck; arcIndex++)
            {
                // rather than tracing around the circle, toggle between clockwise and counter clockwise for each test
                // .. so we favor a position furthest away from mob
                float checkFacing = baseDestinationFacing;
                if ((arcIndex & 1) == 0)
                    checkFacing += arcIncrement * (arcIndex >> 1);
                else
                    checkFacing -= arcIncrement * ((arcIndex >> 1) + 1);

                for (float distFromOrigin = MinScanDistance; distFromOrigin <= MaxScanDistance; distFromOrigin += IncrementScanDistance)
                {
                    countPointsChecked++;

                    ptDestination = ptOrigin.RayCast(checkFacing, distFromOrigin);
                    if (!Navigator.CanNavigateFully(Me.Location, ptDestination))
                    {
                        // Logger.WriteDebug( Color.Cyan, "Safe Location failed navigation check for degrees={0:F1} dist={1:F1}", RadiansToDegrees(checkFacing), distFromOrigin);
                        countFailToPointNav++;
                        continue;
                    }

                    WoWPoint ptNearest = NearestMobLoc(ptDestination, mobLocations);
                    if (ptNearest == WoWPoint.Empty)
                    {
                        if (furthestNearMobDistSqr < minSafeDistSqr)
                        {
                            furthestNearMobDistSqr = minSafeDistSqr;
                            ptFurthest = ptDestination;     // set best available if others fail
                        }
                    }
                    else
                    {
                        double mobDistSqr = ptDestination.Distance2DSqr(ptNearest);
                        if (furthestNearMobDistSqr < mobDistSqr)
                        {
                            furthestNearMobDistSqr = mobDistSqr;
                            ptFurthest = ptDestination;     // set best available if others fail
                        }
                        if (mobDistSqr <= minSafeDistSqr)
                        {
                            countFailSafe++;
                            continue;
                        }
                    }

                    if (reallyCheckRangeToLineOfSightMob && RangeToLineOfSightMob < ptDestination.Distance(LineOfSightMob.Location) - LineOfSightMob.MeleeDistance())
                    {
                        countFailRange++;
                        continue;
                    }

                    if (CheckLineOfSightToSafeLocation)
                    {
                        WoWPoint ptAdjDest = ptDestination;
                        ptAdjDest.Z += 1f;
                        if (!Styx.WoWInternals.World.GameWorld.IsInLineOfSight(ptAdjOrigin, ptAdjDest))
                        {
                            // Logger.WriteDebug( Color.Cyan, "Mob-free location failed line of sight check for degrees={0:F1} dist={1:F1}", degreesFrom, distFromOrigin);
                            countFailToPointLoS++;
                            continue;
                        }
                    }

                    if (CheckSpellLineOfSightToMob && LineOfSightMob != null)
                    {
                        if (!Styx.WoWInternals.World.GameWorld.IsInLineOfSpellSight(ptDestination, LineOfSightMob.Location))
                        {
                            if (!Styx.WoWInternals.World.GameWorld.IsInLineOfSight(ptDestination, LineOfSightMob.Location))
                            {
                                // Logger.WriteDebug( Color.Cyan, "Mob-free location failed line of sight check for degrees={0:F1} dist={1:F1}", degreesFrom, distFromOrigin);
                                countFailToMobLoS++;
                                continue;
                            }
                        }
                    }

                    Logger.WriteDebug(Color.Cyan, "SafeArea: Found mob-free location ({0:F1} yd radius) at degrees={1:F1} dist={2:F1} on point check# {3} at {4}, {5}, {6}", MinSafeDistance, WoWMathHelper.RadiansToDegrees(checkFacing), distFromOrigin, countPointsChecked, ptDestination.X, ptDestination.Y, ptDestination.Z);
                    Logger.WriteDebug(Color.Cyan, "SafeArea: processing took {0:F0} ms", (DateTime.Now - startFind).TotalMilliseconds);
                    return ptDestination;
                }
            }

            Logger.WriteDebug(Color.Cyan, "SafeArea: No mob-free location ({0:F1} yd radius) found within {1:F1} yds ({2} checked, {3} nav, {4} not safe, {5} range, {6} pt los, {7} mob los)", MinSafeDistance, MaxScanDistance, countPointsChecked, countFailToPointNav, countFailSafe, countFailRange, countFailToPointLoS, countFailToMobLoS);
            if (ChooseSafestAvailable && ptFurthest != WoWPoint.Empty)
            {
                Logger.WriteDebug(Color.Cyan, "SafeArea: choosing best available spot in {0:F1} yd radius where closest mob is {1:F1} yds", MinSafeDistance, Math.Sqrt(furthestNearMobDistSqr));
                Logger.WriteDebug(Color.Cyan, "SafeArea: processing took {0:F0} ms", (DateTime.Now - startFind).TotalMilliseconds);
                return ChooseSafestAvailable ? ptFurthest : WoWPoint.Empty;
            }

            Logger.WriteDebug(Color.Cyan, "SafeArea: processing took {0:F0} ms", (DateTime.Now - startFind).TotalMilliseconds);
            return WoWPoint.Empty;
        }

#if DONT_USE
        /// <summary>
        /// locates safe point away from enemies
        /// </summary>
        /// <param name="ptOrigin">start point for search</param>
        /// <param name="minSafeDist">min distance to be safe</param>
        /// <returns></returns>
        public WoWPoint FindLocationOriginal(WoWPoint ptOrigin)
        {
            WoWPoint destinationLocation = new WoWPoint();
            List<WoWPoint> mobLocations = new List<WoWPoint>();
            int arcIncrement = 360 / RaysToCheck;

            mobLocations = AllEnemyMobLocations;

            double minSafeDistSqr = MinSafeDistance * MinSafeDistance;

            double degreesFacing = (Me.RenderFacing * 180f) / Math.PI;
            // Logger.WriteDebug( Color.Cyan, "Facing {0:F0}d {1:F2}r Searching for {2:F1} yard mob free area", degreesFacing, Me.RenderFacing, MinSafeDistance);
            for (int arcIndex = 0; arcIndex < RaysToCheck ; arcIndex++)
            {
                float degreesFrom = 180;
                if ((arcIndex & 1) == 0)
                    degreesFrom += (arcIndex >> 1) * arcIncrement;
                else
                    degreesFrom -= (arcIndex >> 1) * arcIncrement;

                for (float distFromOrigin = MinScanDistance; distFromOrigin <= MaxScanDistance ; distFromOrigin += IncrementScanDistance )
                {
                    float heading = (float)(degreesFrom * Math.PI / 180f);
                    heading -= Me.RenderFacing;
                    destinationLocation = ptOrigin.RayCast((float)(degreesFrom * Math.PI / 180f), distFromOrigin);
                    double mobDistSqr = destinationLocation.Distance2DSqr(NearestMobLoc(destinationLocation, mobLocations));

                    if (mobDistSqr <= minSafeDistSqr)
                        continue;

                    //if (Navigator.CanNavigateFully(Me.Location, destinationLocation))
                    if (Navigator.GeneratePath(Me.Location, destinationLocation).Length <= 0)
                    {
                        // Logger.WriteDebug( Color.Cyan, "Mob-free location failed path check for degrees={0:F1} dist={1:F1}", degreesFrom, distFromOrigin);
                        continue;
                    }

                    if (!Styx.WoWInternals.World.GameWorld.IsInLineOfSight(Me.Location, destinationLocation))
                    {
                        // Logger.WriteDebug( Color.Cyan, "Mob-free location failed line of sight check for degrees={0:F1} dist={1:F1}", degreesFrom, distFromOrigin);
                        continue;
                    }

                    if (MobToRunFrom != null)
                    {
                        if (!Styx.WoWInternals.World.GameWorld.IsInLineOfSpellSight(destinationLocation, MobToRunFrom.Location))
                        {
                            // Logger.WriteDebug( Color.Cyan, "Mob-free location failed line of sight check for degrees={0:F1} dist={1:F1}", degreesFrom, distFromOrigin);
                            continue;
                        }
                    }

                    Logger.WriteDebug(Color.Cyan, "Found mob-free location ({0:F1} yd radius) at degrees={1:F1} dist={2:F1}", MinSafeDistance, degreesFrom, distFromOrigin);
                    return destinationLocation;
                }
            }

            Logger.WriteDebug(Color.Cyan, "No mob-free location ({0:F1} yd radius) found within {1:F1} yds", MinSafeDistance, MaxScanDistance );
            return WoWPoint.Empty;
        }
#endif

    }

    public static class CommonExtensions
    {
        public static bool IsDirectlyFacing(this WoWUnit u, float radians)
        {
            float diff = Math.Abs(u.RenderFacing - radians);
            if (diff > Math.PI)
                diff = (float)(2 * Math.PI) - diff;
            return diff < SafeArea.ONE_DEGREE_AS_RADIAN;
        }

        public static bool IsDirectlyFacing(this WoWUnit u, WoWPoint pt)
        {
            return WoWMathHelper.IsFacing(u.Location, u.RenderFacing, pt, SafeArea.ONE_DEGREE_AS_RADIAN);
        }
    }
}


