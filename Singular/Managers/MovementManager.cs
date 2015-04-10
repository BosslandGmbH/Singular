using Bots.DungeonBuddy.Helpers;
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using System;
using System.Linq;
using Singular.Settings;
using System.Drawing;
using Styx.Common.Helpers;
using System.Collections.Generic;
using Singular.Helpers;
using Styx.Pathing;
using System.Diagnostics;
using System.IO;

namespace Singular.Managers
{
    internal static class MovementManager
    {
        /// <summary>
        /// True: Singular movement is currently disabled.  This could be due to a setting,
        /// the current Bot, or a Hotkey toggled.  All code needing to check if
        /// movement is allowed should call this or MovementManager.IsMovementEnabled
        /// </summary>
        public static bool IsMovementDisabled
        {
            get
            {
                if (IsBotMovementDisabled)
                    return true;

                if (!SingularRoutine.IsAllowed(Styx.CommonBot.Routines.CapabilityFlags.Movement))
                    return true;

                if (SingularSettings.Instance.AllowMovement == AllowMovementType.Auto)
                    return IsManualMovementBotActive;

                return SingularSettings.Instance.AllowMovement != AllowMovementType.All;
            }
        }

        /// <summary>
        /// True: Singular movement is currently disabled.  This could be due to a setting,
        /// the current Bot, or a Hotkey toggled.  All code needing to check if
        /// movement is allowed should call this or MovementManager.IsMovementEnabled
        /// </summary>
        public static bool IsFacingDisabled
        {
            get
            {
                if (IsBotMovementDisabled)
                    return true;

                if (!SingularRoutine.IsAllowed(Styx.CommonBot.Routines.CapabilityFlags.Facing))
                    return true;

                if (SingularSettings.Instance.AllowMovement == AllowMovementType.Auto)
                    return IsManualMovementBotActive;

                return SingularSettings.Instance.AllowMovement != AllowMovementType.All;
            }
        }

        /// <summary>
        /// True: Bot movement should be disabled by Singular.  This is controlled
        /// only by the state of the Hotkeys toggle for movement since we only want
        /// to interfere with bot movement when the user tells us to
        /// </summary>
        private static bool IsBotMovementDisabled
        {
            get
            {
                return !HotkeyDirector.IsMovementEnabled || (SingularRoutine.IsQuestBotActive && SingularSettings.Instance.DisableInQuestVehicle && StyxWoW.Me.InVehicle);
            }
        }



        /// <summary>
        /// True: Singular Class specific movement is currently disabled.  This could be due to a setting,
        /// the current Bot, or a Hotkey toggled.  This should be used by all class specific spells
        /// such as Charge, Roll, Shadow Step, Wild Charge
        /// </summary>
        public static bool IsClassMovementAllowed
        {
            get
            {
                if (IsBotMovementDisabled)
                    return false;

                if (!SingularRoutine.IsAllowed(Styx.CommonBot.Routines.CapabilityFlags.Movement)
                    || !SingularRoutine.IsAllowed(Styx.CommonBot.Routines.CapabilityFlags.GapCloser))
                    return false;

                if (SingularSettings.Instance.AllowMovement == AllowMovementType.Auto)
                    return !IsManualMovementBotActive;

                return SingularSettings.Instance.AllowMovement >= AllowMovementType.ClassSpecificOnly;
            }
        }

        public static bool IsMoveBehindAllowed
        {
            get
            {
                if (IsBotMovementDisabled)
                    return false;

                if (!SingularRoutine.IsAllowed(Styx.CommonBot.Routines.CapabilityFlags.Movement)
                    || !SingularRoutine.IsAllowed(Styx.CommonBot.Routines.CapabilityFlags.MoveBehind))
                    return false;

                if (SingularSettings.Instance.AllowMovement == AllowMovementType.Auto)
                    return !IsManualMovementBotActive;

                return SingularSettings.Instance.AllowMovement >= AllowMovementType.ClassSpecificOnly;
            }
        }

        /// <summary>
        /// we determine this value locally at present to avoid possible botevent sequence errors
        /// which would occur if this depended on the SingularRoutine value but this event 
        /// handler were called before 
        /// </summary>
        /// <remarks>
        /// query the active bot only on a bot event and then cache the result.  we don't
        /// need to check more often than that
        /// </remarks>
        private static bool IsManualMovementBotActive { get; set; }

        // static INavigationProvider _prevNavigation = null;
        static IPlayerMover _prevPlayerMover = null;
        static StuckHandler _prevStuckHandler = null;

        #region Initialization

        internal static void Init()
        {
            SingularRoutine.OnBotEvent += (src, arg) =>
            {
                IsManualMovementBotActive = SingularRoutine.IsBotInUse("LazyRaider", "Raid Bot", "Tyrael", "Enyo");
                if (arg.Event == SingularBotEvent.BotStarted)
                    MovementManager.Start();
                else if (arg.Event == SingularBotEvent.BotStopped)
                    MovementManager.Stop();
                else if (arg.Event == SingularBotEvent.BotChanged)
                    MovementManager.Change();
            };
        }

        /// <summary>
        /// Update the current status of MovementManager.  Should be called when an
        /// outside influence has possibly caused a configuration change, such as
        /// settings window, user interface bot change, etc.
        /// </summary>
        public static void Update()
        {
            if (IsBotMovementDisabled)
                SuppressMovement();
            else
                AllowMovement();
        }

        #endregion

        #region Event Handlers

        private static void Start()
        {
            Update();
        }

        private static void Stop()
        {
            // restore in case we had taken over 
            AllowMovement();
        }

        private static void Change()
        {
            // restore in case we had taken over
            AllowMovement();
        }

        #endregion

        #region Movement Handler Primitives

        private static void AllowMovement()
        {
            if (Navigator.PlayerMover == pNoPlayerMovement)
            {
                Logger.WriteDebug("MovementManager: restoring Player Movement");
                Navigator.PlayerMover = _prevPlayerMover;
            }

            //if (Navigator.NavigationProvider == pNoNavigation)
            //{
            //    Logger.WriteDebug("MovementManager: restoring Player Navigation");
            //    Navigator.NavigationProvider = _prevNavigation;
            //}

            if (Navigator.NavigationProvider.StuckHandler == pNoStuckHandling)
            {
                Logger.WriteDebug("MovementManager: restoring Stuck Handler");
                Navigator.NavigationProvider.StuckHandler = _prevStuckHandler;
            }
        }

        private static void SuppressMovement()
        {
            if (Navigator.PlayerMover != pNoPlayerMovement)
            {
                Logger.WriteDebug("MovementManager: setting No Player Movement");
                _prevPlayerMover = Navigator.PlayerMover;
                Navigator.PlayerMover = pNoPlayerMovement;
            }

            //if (Navigator.NavigationProvider != pNoNavigation)
            //{
            //    Logger.WriteDebug("MovementManager: setting No Player Navigation");
            //    _prevNavigation = Navigator.NavigationProvider;
            //    Navigator.NavigationProvider = pNoNavigation;
            //}

            if (Navigator.NavigationProvider.StuckHandler != pNoStuckHandling )
            {
                Logger.WriteDebug("MovementManager: setting No Stuck Handling");
                _prevStuckHandler = Navigator.NavigationProvider.StuckHandler ;
                Navigator.NavigationProvider.StuckHandler  = pNoStuckHandling ;
            }
        }

        #endregion

        #region Local Classes for No Movement Providers

        class NoNavigation : NavigationProvider
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
            public NoNavigation()
	        {
		        StuckHandler = new ScriptHelpers.NoUnstuck();
	        }

	        public override MoveResult MoveTo(WoWPoint location)
	        {
		        return MoveResult.Moved;
	        }

	        public override WoWPoint[] GeneratePath(WoWPoint @from, WoWPoint to)
	        {
		        return new[]
		        {
			        from
		        };
	        }

	        public override bool AtLocation(WoWPoint point1, WoWPoint point2)
	        {
		        return true;
	        }

	        public override float PathPrecision { get; set; }
        }

        class NoPlayerMovement : IPlayerMover
        {
            public void Move(WoWMovement.MovementDirection direction)   { }
            public void MoveStop() { }
            public void MoveTowards(WoWPoint location) { }
        }

        class NoStuckHandling : StuckHandler
        {
            public override bool IsStuck() { return false; }
			public override void Reset() { }
			public override void Unstick() { }
        }

        private static NoNavigation pNoNavigation = new NoNavigation();
        private static NoPlayerMovement pNoPlayerMovement = new NoPlayerMovement();
        private static NoStuckHandling pNoStuckHandling = new NoStuckHandling();


        public class PlayerMovementDebug : IPlayerMover
        {
            private IPlayerMover Prev { get; set; }
            public static PlayerMovementDebug Instance { get; set; }

            public static void Install()
            {
                Instance = new PlayerMovementDebug();
                Instance.Prev = Navigator.PlayerMover;
                Navigator.PlayerMover = Instance;
            }

            public static void Remove()
            {
                Navigator.PlayerMover = Instance.Prev;
            }

            public void Move(WoWMovement.MovementDirection direction) 
            {
                Logger.WriteDebug("~Move({0}): {1}", direction, StackCaller(2));
                Instance.Prev.Move(direction);
            }

            public void MoveStop() 
            {
                Logger.WriteDebug("~Move(Stop): {0}", StackCaller(2));
                Instance.Prev.MoveStop();
            }

            public void MoveTowards(WoWPoint location) 
            {
                Logger.WriteDebug("~Move({0}): to {1} {2}", StyxWoW.Me.Location.Distance(location), location, StackCaller(2));
                Instance.Prev.MoveTowards(location);
            }

            public static string StackCaller(int levelsUp)
            {
                var stackTrace = new StackTrace(true);
                StackFrame[] stackFrames = stackTrace.GetFrames();
                StackFrame frame = stackFrames[1 + levelsUp];
                return string.Format("{0}, {1} line {2}", frame.GetMethod().Name, Path.GetFileName(frame.GetFileName()), frame.GetFileLineNumber());
            }

        }

        #endregion
    }
}