using System;

using Singular.Managers;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals;
using CommonBehaviors.Actions;

namespace Singular.Helpers
{
    internal static class Common
    {
        /// <summary>
        ///  Creates a behavior to start auto attacking to current target.
        /// </summary>
        /// <remarks>
        ///  Created 23/05/2011
        /// </remarks>
        /// <param name="includePet"> This will also toggle pet auto attack. </param>
        /// <returns></returns>
        public static Composite CreateAutoAttack(bool includePet)
        {
            const int spellIdAutoShot = 75;

            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.IsAutoAttacking && StyxWoW.Me.AutoRepeatingSpellId != spellIdAutoShot,
                    new Action(ret =>
                        {
                            Lua.DoString("StartAttack()");
                            return RunStatus.Failure;
                        })),
                new Decorator(
                    ret => includePet && StyxWoW.Me.GotAlivePet && (StyxWoW.Me.Pet.CurrentTarget == null || StyxWoW.Me.Pet.CurrentTarget != StyxWoW.Me.CurrentTarget),
                    new Action(
                        delegate
                        {
                            PetManager.CastPetAction("Attack");
                            return RunStatus.Failure;
                        }))
                );
        }

        /// <summary>
        ///  Creates a behavior to start shooting current target with the wand.
        /// </summary>
        /// <remarks>
        ///  Created 23/05/2011
        /// </remarks>
        /// <returns></returns>
        public static Composite CreateUseWand()
        {
            return CreateUseWand(ret => true);
        }

        /// <summary>
        ///  Creates a behavior to start shooting current target with the wand if extra conditions are met.
        /// </summary>
        /// <param name="extra"> Extra conditions to check to start shooting. </param>
        /// <returns></returns>
        public static Composite CreateUseWand(SimpleBooleanDelegate extra)
        {
#if USE_WANDS
            return new PrioritySelector(
                new Decorator(
                    ret => Item.HasWand && !StyxWoW.Me.IsWanding() && extra(ret),
                    new Action(ret => SpellManager.Cast("Shoot")))
                );
#else
            return new ActionAlwaysFail();
#endif
        }

        /// <summary>Creates an interrupt spell cast composite. This will attempt to use racials before any class/spec abilities. It will attempt to stun if possible!</summary>
        /// <remarks>Created 9/7/2011.</remarks>
        /// <param name="onUnit">The on unit.</param>
        /// <returns>.</returns>
        public static Composite CreateInterruptSpellCast(UnitSelectionDelegate onUnit)
        {
            return
                new Decorator(
                // If the target is casting, and can actually be interrupted, AND we've waited out the double-interrupt timer, then find something to interrupt with.
                    ret => onUnit != null && onUnit(ret) != null && onUnit(ret).IsCasting && onUnit(ret).CanInterruptCurrentSpellCast
                /* && PreventDoubleInterrupt*/,
                    new PrioritySelector(
                        Spell.Cast("Rebuke", onUnit),
                        Spell.Cast("Avenger's Shield", onUnit),
                        Spell.Cast("Hammer of Justice", onUnit),

                        Spell.Cast("Kick", onUnit),
                        Spell.Cast("Gouge", onUnit, ret => !onUnit(ret).IsBoss() && !onUnit(ret).MeIsSafelyBehind), // Can't gouge bosses.

                        Spell.Cast("Counterspell", onUnit),

                        Spell.Cast("Wind Shear", onUnit),

                        Spell.Cast("Pummel", onUnit),

                        Spell.Cast("Spear Hand Strike", onUnit), 

                        // AOE interrupt
                        Spell.Cast("Disrupting Shout", onUnit),
                        // Gag Order only works on non-bosses due to it being a silence, not an interrupt!
                        Spell.Cast("Heroic Throw", onUnit, ret => TalentManager.IsSelected(7) && !onUnit(ret).IsBoss()),

                        Spell.Cast("Silence", onUnit),

                        Spell.Cast("Silencing Shot", onUnit),

                        // Can't stun most bosses. So use it on trash, etc.
                        Spell.Cast("Bash", onUnit, ret => !onUnit(ret).IsBoss()),
                        Spell.Cast("Skull Bash (Cat)", onUnit, ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Cat),
                        Spell.Cast("Skull Bash (Bear)", onUnit, ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Bear),
                        Spell.Cast("Mighty Bash", onUnit, ret => !onUnit(ret).IsBoss() && onUnit(ret).IsWithinMeleeRange ),
                        Spell.Cast("Solar Beam", onUnit, ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Moonkin),

                        Spell.Cast("Strangulate", onUnit),
                        Spell.Cast("Mind Freeze", onUnit),


                        // Racials last.
                        Spell.Cast("Arcane Torrent", onUnit),
                // Don't waste stomp on bosses. They can't be stunned 99% of the time!
                        Spell.Cast("War Stomp", onUnit, ret => !onUnit(ret).IsBoss() && onUnit(ret).Distance < 8),
                        Spell.Cast("Quaking Palm", onUnit)

                        ));
        }

        /// <summary>
        /// Creates a dismount composite that only stops if we are flying.
        /// </summary>
        /// <param name="reason">The reason to dismount</param>
        /// <returns></returns>
        public static Composite CreateDismount(string reason)
        {
            return new Decorator(
                ret => StyxWoW.Me.Mounted && !MovementManager.IsMovementDisabled,
                new Sequence(
                    new DecoratorContinue(ret => StyxWoW.Me.IsFlying,
                        new Sequence(
                            new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                new Sequence(
                                    new Action(ret => Logger.WriteDebug("Stopping to descend..." + (!string.IsNullOrEmpty(reason) ? (" Reason: " + reason) : string.Empty))),
                                    new Action(ret => WoWMovement.MoveStop()),
                                    new Wait( 1, ret => !StyxWoW.Me.IsMoving, new ActionAlwaysSucceed())
                                    )
                                ),
                            new Action( ret => Logger.WriteDebug( "Descending to land..." + (!string.IsNullOrEmpty(reason) ? (" Reason: " + reason) : string.Empty))),
                            new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Descend)),
                            new PrioritySelector(
                                new Wait( 1, ret => StyxWoW.Me.IsMoving, new ActionAlwaysSucceed()),
                                new Action( ret => Logger.WriteDebug( "warning -- tried to descend but IsMoving == false ....!"))
                                ),
                            new WaitContinue(30, ret => !StyxWoW.Me.IsFlying, new ActionAlwaysSucceed()),
                            new DecoratorContinue( 
                                ret => StyxWoW.Me.IsFlying, 
                                new Action( ret => Logger.WriteDebug( "error -- still flying -- descend appears to have failed....!"))
                                ),
                            new Action(ret => WoWMovement.MoveStop(WoWMovement.MovementDirection.Descend))
                            )
                        ), // and finally dismount. 
                    new Action(r => {
                        Logger.WriteDebug( "Dismounting..." + (!string.IsNullOrEmpty(reason) ? (" Reason: " + reason) : string.Empty));
                        ShapeshiftForm shapeshift = StyxWoW.Me.Shapeshift;
                        if (StyxWoW.Me.Class == WoWClass.Druid && (shapeshift == ShapeshiftForm.FlightForm || shapeshift == ShapeshiftForm.EpicFlightForm))
                            Lua.DoString("RunMacroText('/cancelform')");
                        else
                            Lua.DoString("Dismount()");
                        })
                    )
                );
        }

        /// <summary>
        /// Creates a stop and dismount composite. Matches the prior behavior of old CreateDismount()
        /// </summary>
        /// <param name="reason">The reason to dismount</param>
        /// <returns></returns>
        public static Composite CreateStopAndDismount(string reason)
        {
            return new Decorator( 
                ret => !MovementManager.IsMovementDisabled,
                new PrioritySelector(
                    new Decorator(
                        ret => StyxWoW.Me.IsMoving,
                        new Sequence( 
                            new Action(ret => Logger.WriteDebug("Stopping..." + (!string.IsNullOrEmpty(reason) ? (" Reason: " + reason) : string.Empty))),
                            Movement.CreateEnsureMovementStoppedBehavior()
                            )
                        ),

                    CreateDismount( reason)
                    )
                );
        }
        /// <summary>
        /// This is meant to replace the 'SleepForLagDuration()' method. Should only be used in a Sequence
        /// </summary>
        /// <returns></returns>
        public static Composite CreateWaitForLagDuration()
        {
            // return new WaitContinue(TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150), ret => false, new ActionAlwaysSucceed());
            return CreateWaitForLagDuration(ret => false);
        }

        /// <summary>
        /// Allows waiting for SleepForLagDuration() but ending sooner if condition is met
        /// </summary>
        /// <param name="orUntil">if true will stop waiting sooner than lag maximum</param>
        /// <returns></returns>
        public static Composite CreateWaitForLagDuration( CanRunDecoratorDelegate orUntil)
        {
            return new WaitContinue(TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150), orUntil, new ActionAlwaysSucceed());
        }

        private static readonly WaitTimer InterruptTimer = new WaitTimer(TimeSpan.FromMilliseconds(500));

        private static bool PreventDoubleInterrupt
        {
            get
            {
                
                var tmp = InterruptTimer.IsFinished;
                if (tmp)
                    InterruptTimer.Reset();
                return tmp;
            }
        }
    }
}
