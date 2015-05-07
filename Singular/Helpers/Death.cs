using System.Linq;
using CommonBehaviors.Actions;

using Singular.Settings;
using Styx;
using Singular.Helpers;

using Styx.CommonBot;
using Styx.CommonBot.Inventory;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using System;

using Action = Styx.TreeSharp.Action;
using Singular.Dynamics;
using Singular.Managers;
using System.Collections.Generic;
using System.Drawing;

namespace Singular.Helpers
{
    internal static class Death
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        private static string SelfRezSpell { get; set; }
        private static int MobsNearby { get; set; }
        private static int SafeDistance { get; set; }
        private static DateTime NextSuppressMessage = DateTime.MinValue;

        [Behavior(BehaviorType.Death)]
        public static Composite CreateDefaultDeathBehavior()
        {
            return new Throttle( 60,
                new Decorator(
                    req => {
                        if (Me.IsAlive || Me.IsGhost)
                        {
                            Logger.WriteDiagnostic(LogColor.Hilite, "Death: ERROR - should not be called with Alive={0}, Ghost={1}", Me.IsAlive.ToYN(), Me.IsGhost.ToYN());
                            return false;
                        }

                        Logger.WriteDiagnostic(LogColor.Hilite, "Death Behavior: invoked!  Alive={0}, Ghost={1}", Me.IsAlive.ToYN(), Me.IsGhost.ToYN());
                        if (SingularSettings.Instance.SelfRessurect == Singular.Settings.SelfRessurectStyle.None)
                        {
                            Logger.WriteDiagnostic(LogColor.Hilite, "Death: ERROR - should not be called with Alive={0}, Ghost={1}", Me.IsAlive.ToYN(), Me.IsGhost.ToYN());
                            return false;
                        }

                        List<string> hasSoulstone = Lua.GetReturnValues("return HasSoulstone()", "hawker.lua");
                        if (hasSoulstone == null || hasSoulstone.Count == 0 || String.IsNullOrEmpty(hasSoulstone[0]) || hasSoulstone[0].ToLower() == "nil")
                        {
                            Logger.WriteDiagnostic(LogColor.Hilite, "Death: no self-rez ability available, release to standard Death Behavior");
                            return false;
                        }

                        if (SingularSettings.Instance.SelfRessurect == Singular.Settings.SelfRessurectStyle.Auto && MovementManager.IsMovementDisabled)
                        {
                            if (NextSuppressMessage < DateTime.UtcNow)
                            {
                                NextSuppressMessage = DateTime.UtcNow.AddSeconds(SingularSettings.Instance.RezMaxWaitTime);
                                Logger.Write(Color.Aquamarine, "Death: Suppressing automatic {0} since movement disabled...", hasSoulstone[0]);
                            }
                            return false;
                        }

                        SelfRezSpell = hasSoulstone[0];
                        Logger.WriteDiagnostic(LogColor.Hilite, "Death: detected self-rez spell {0} available", SelfRezSpell);
                        return true;
                        },
                    new Sequence(
                        new Action( r => 
                        {
                            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                                SafeDistance = Utilities.EventHandlers.TimeSinceAttackedByEnemyPlayer.TotalSeconds < 10 ? SingularSettings.Instance.RezSafeDistPVP : SingularSettings.Instance.RezSafeDistSolo;
                            else if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                                SafeDistance = SingularSettings.Instance.RezSafeDistPVP;
                            else
                                SafeDistance = SingularSettings.Instance.RezSafeDistInstance;
                        }),
                        new Action(r => Logger.Write(Color.Aquamarine, "Death: Waiting {0} secs for {1} yds clear safe area to use {2}...", SingularSettings.Instance.RezMaxWaitTime, SafeDistance, SelfRezSpell)),
                        new WaitContinue(
                            SingularSettings.Instance.RezMaxWaitTime, 
                            until => {
                                MobsNearby = ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                                    .Count( u => (u.GetReactionTowards(Me) < WoWUnitReaction.Unfriendly || (u.IsPlayer && u.ToPlayer().IsHorde != Me.IsHorde)) && u.IsAlive && u.SpellDistance() < SafeDistance);
                                return MobsNearby == 0 || Me.IsAlive || Me.IsGhost;
                                },
                            new ActionAlwaysSucceed()
                            ),
                        new DecoratorContinue(
                            req => Me.IsAlive,
                            new Action(r =>
                            {
                                Logger.Write(Color.Aquamarine, "Death: Player alive via some other action, skipping {0}...", SelfRezSpell);
                                return RunStatus.Failure;
                            })
                            ),

                        new DecoratorContinue(
                            req => Me.IsGhost,
                            new Action(r =>
                            {
                                Logger.Write(Color.Aquamarine, "Death: Insignia taken or corpse release by something other than Singular, skipping {0}...", SelfRezSpell);
                                return RunStatus.Failure;
                            })
                            ),

                        new DecoratorContinue(
                            req => MobsNearby > 0,
                            new Action(r =>
                            {
                                Logger.Write(Color.Aquamarine, "Death: After {0} secs still {1} enemies within {2} yds, skipping {3}...", SingularSettings.Instance.RezMaxWaitTime, MobsNearby, SafeDistance, SelfRezSpell);
                                return RunStatus.Failure;
                            })
                            ),

                        new Action(r => Logger.Write(LogColor.Hilite, "Death: Singular ressurecting by invoking {0}...", SelfRezSpell)),

                        new Action(r => Lua.DoString("UseSoulstone()")),

                        new PrioritySelector(
                            new Wait( 1, until => Me.IsAlive || Me.IsGhost, new ActionAlwaysSucceed()),
                            new Action( r =>
                            {
                                Logger.WriteDiagnostic(Color.Aquamarine, "Death: use of {0} failed", SelfRezSpell);
                                return RunStatus.Failure;
                            })
                            ),

                        new Action(r => Logger.WriteDiagnostic(Color.Aquamarine, "Death: successfully ressurrected with {0}", SelfRezSpell))
                        )
                    )
                );
        }

    }
}
