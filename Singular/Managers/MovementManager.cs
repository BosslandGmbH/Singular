using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.DBC;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Singular.Dynamics;
using Singular.Settings;
using Styx.Pathing;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;


namespace Singular.Managers
{
    internal static class MovementManager
    {
        #region INIT

        [Behavior(BehaviorType.Initialize, WoWClass.None, priority: int.MaxValue - 2)]
        public static Composite CreateMovementManagerInitializeBehaviour()
        {
            return null;
        }

        #endregion

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

        #region Initialization

        internal static void Init()
        {
            SingularRoutine.OnBotEvent += (src, arg) =>
            {
                IsManualMovementBotActive = SingularRoutine.IsBotInUse("LazyRaider", "Raid Bot", "Tyrael", "Enyo", "HazzNyo");
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
            if (!(Navigator.NavigationProvider is NoMovementWrapper))
                return;

            Logger.WriteDebug("MovementManager: restoring Player Movement");
            Navigator.NavigationProvider =
                ((NoMovementWrapper)Navigator.NavigationProvider).Original;
        }

        private static void SuppressMovement()
        {
            if (Navigator.NavigationProvider is NoMovementWrapper)
                return;

            Logger.WriteDebug("MovementManager: setting No Player Movement");
            Navigator.NavigationProvider = new NoMovementWrapper(Navigator.NavigationProvider);
        }

        #endregion

        private class NoMovementWrapper : NavigationProvider
        {
            public NoMovementWrapper(NavigationProvider original)
            {
                Original = original;
            }

            public NavigationProvider Original { get; }

            public override void OnPulse()
            {
                Original.OnPulse();
            }

            public override MoveResult MoveTo(MoveToParameters parameters)
            {
                return MoveResult.Failed;
            }

            public override bool AtLocation(Vector3 point1, Vector3 point2)
            {
                return Original.AtLocation(point1, point2);
            }

            public override bool Clear()
            {
                return Original.Clear();
            }

            public override void ClearStuckInfo()
            {
                Original.ClearStuckInfo();
            }

            public override PathInformation LookupPathInfo(WoWObject obj, float distanceTolerance = 3)
            {
                return Original.LookupPathInfo(obj, distanceTolerance);
            }

            public override void OnSetAsCurrent()
            {
                Original.OnSetAsCurrent();
            }

            public override void OnRemoveAsCurrent()
            {
                Original.OnRemoveAsCurrent();
            }
        }
    }
}