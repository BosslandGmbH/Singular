// #define REACT_TO_HOTKEYS_IN_PULSE

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
using Styx.Common;
using Styx.TreeSharp;
using System.Windows.Forms;
using Styx.Pathing;

namespace Singular.Managers
{
    internal static class HotkeyManager
    {
        /// <summary>
        /// True: if AOE spells are allowed, False: Single target only
        /// </summary>
        public static bool IsAoeEnabled { get { return _AoeEnabled; } }

        /// <summary>
        /// True: allow normal combat, False: CombatBuff and Combat behaviors are suppressed
        /// </summary>
        public static bool IsCombatEnabled { get { return _CombatEnabled; } }

        /// <summary>
        /// True: allow normal Bot movement, False: prevent any movement by Bot, Combat Routine, or Plugins
        /// </summary>
        public static bool IsMovementEnabled { get { return _MovementEnabled && !IsMovementTemporarilySuspended; } }


        private static bool IsMovementTemporarilySuspended
        {
            get { return _MovementTemporarySuspendEndtime > DateTime.Now; }
            set
            {
                _MovementTemporarySuspendEndtime = DateTime.Now;
                if (value)
                    _MovementTemporarySuspendEndtime += TimeSpan.FromSeconds(SingularSettings.Instance.Hotkeys.SuspendDuration);
            }
        }

        /// <summary>
        /// sets initial values for all key states. registers a local botevent handler so 
        /// we know when we are running and when we arent to enable/disable hotkeys
        /// </summary>
        internal static void Init()
        {
            InitKeyStates();

            SingularRoutine.OnBotEvent += (src,arg) =>
            {
                if (arg.Event == SingularBotEvent.BotStart)
                    HotkeyManager.Start(true);
                else if (arg.Event == SingularBotEvent.BotStop)
                    HotkeyManager.Stop();
            };
        }

        internal static void Start(bool needReset = false)
        {
            if (needReset)
                InitKeyStates();

            _HotkeysRegistered = true;

            // Hook the  hotkeys for the appropriate WOW Window...
            Hotkeys.SetWindowHandle(StyxWoW.Memory.Process.MainWindowHandle);

            // register hotkey for commands with 1:1 key assignment
            if (SingularSettings.Instance.Hotkeys.AoeToggle != Keys.None)
            {
                WriteHotkeyAssignment("AOE Spells", SingularSettings.Instance.Hotkeys.AoeToggle);
                Hotkeys.RegisterHotkey("Toggle AOE", () => { AoeToggle(); }, SingularSettings.Instance.Hotkeys.AoeToggle);
            }
            if (SingularSettings.Instance.Hotkeys.CombatToggle != Keys.None)
            {
                WriteHotkeyAssignment("Combat", SingularSettings.Instance.Hotkeys.CombatToggle);
                Hotkeys.RegisterHotkey("Toggle Combat", () => { CombatToggle(); }, SingularSettings.Instance.Hotkeys.CombatToggle);
            }

            // note: important to not check MovementManager if movement disabled here, since MovementManager calls us
            // .. and the potential for side-effects exists.  check SingularSettings directly for this only
            if (!SingularSettings.Instance.DisableAllMovement && SingularSettings.Instance.Hotkeys.MovementToggle != Keys.None)
            {
                WriteHotkeyAssignment("Movement", SingularSettings.Instance.Hotkeys.MovementToggle);
                Hotkeys.RegisterHotkey("Toggle Movement", () => { MovementToggle(); }, SingularSettings.Instance.Hotkeys.MovementToggle);
            }

            // note: important to not check MovementManager if movement disabled here, since MovementManager calls us
            // .. and the potential for side-effects exists.  check SingularSettings directly for this only
            if (SingularSettings.Instance.DisableAllMovement || !SingularSettings.Instance.Hotkeys.SuspendMovement )
                _registeredMovementSuspendKeys = null;
            else
            {
                // save shallow copy of keys so we can remove if user changes keys in settings
                _registeredMovementSuspendKeys = (Keys[])SingularSettings.Instance.Hotkeys.SuspendMovementKeys.Clone();

                // register hotkeys for commands with 1:M key assignment
                foreach (var key in _registeredMovementSuspendKeys)
                {
                    Hotkeys.RegisterHotkey("Movement Suspend(" + key.ToString() + ")", () => { MovementTemporary_Suspend(); }, key);
                }
            }
        }

        private static void WriteHotkeyAssignment(string keyCommand, Keys key)
        {
            Logger.Write(Color.White, "Hotkey: To disable {0}, press: [{1}]", keyCommand, key);
        }

        internal static void Stop()
        {
            if (!_HotkeysRegistered)
                return;

            _HotkeysRegistered = false;

            // remove hotkeys for commands with 1:1 key assignment
            Hotkeys.RemoveHotkey("Toggle AOE");
            Hotkeys.RemoveHotkey("Toggle Combat");
            Hotkeys.RemoveHotkey("Toggle Movement");

            // remove hotkeys for commands with 1:M key assignment
            if (_registeredMovementSuspendKeys != null)
            {
                foreach (var key in _registeredMovementSuspendKeys)
                {
                    Hotkeys.RemoveHotkey("Movement Suspend(" + key.ToString() + ")");
                }
            }
        }

        internal static void Update()
        {
            if (_HotkeysRegistered)
            {
                Stop();
                Start();
            }
        }

        /// <summary>
        /// checks whether the state of any of the ability toggles we control via hotkey
        /// has changed.  if so, update the user with a message
        /// </summary>
        internal static void Pulse()
        {
#if REACT_TO_HOTKEYS_IN_PULSE
            AoeKeyHandler();
            CombatKeyHandler();
            MovementKeyHandler();
#endif
            TemporaryMovementKeyHandler();
        }

        internal static void AoeKeyHandler()
        {
            if (_AoeEnabled != last_IsAoeEnabled)
            {
                last_IsAoeEnabled = _AoeEnabled;
                if (last_IsAoeEnabled)
                    TellUser("AoE now active!");
                else 
                    TellUser("AoE disabled... press {0} to enable", SingularSettings.Instance.Hotkeys.AoeToggle );
            }
        }

        internal static void CombatKeyHandler()
        {
            if (_CombatEnabled != last_IsCombatEnabled)
            {
                last_IsCombatEnabled = _CombatEnabled;
                if (last_IsCombatEnabled)
                    TellUser("Combat now enabled!");
                else
                    TellUser("Combat disabled... press {0} to enable", SingularSettings.Instance.Hotkeys.CombatToggle);
            }
        }

        internal static void MovementKeyHandler()
        {
            if (_MovementEnabled != last_IsMovementEnabled)
            {
                last_IsMovementEnabled = _MovementEnabled;
                if (last_IsMovementEnabled)
                    TellUser("Movement now enabled!");
                else
                    TellUser("Movement disabled... press {0} to enable", SingularSettings.Instance.Hotkeys.MovementToggle );

                MovementManager.Update();
            }
        }

        internal static void TemporaryMovementKeyHandler()
        {
            if (IsMovementTemporarilySuspended != last_IsMovementTemporarilySuspended)
            {
                last_IsMovementTemporarilySuspended = IsMovementTemporarilySuspended;

                // keep these notifications in Log window only
                if ( last_IsMovementTemporarilySuspended )
                    Logger.Write( Color.White, "Bot Movement disabled during user movement...");
                else
                    Logger.Write(Color.White, "Bot Movement restored!");

                MovementManager.Update();
            }
        }

        #region Helpers

        private static void TellUser(string template, params object[] args)
        {
            string msg = string.Format(template, args);
            Logger.Write( Color.Yellow, string.Format("Hotkey: " + msg));
            if ( true )
                Lua.DoString(string.Format("print('{0}!')", msg));
        }

        #endregion

        // track whether keys registered yet
        private static bool _HotkeysRegistered = false;

        // state of each toggle kept here
        private static bool _AoeEnabled;
        private static bool _CombatEnabled;
        private static bool _MovementEnabled;
        private static DateTime _MovementTemporarySuspendEndtime = DateTime.Now;

        // save keys used at last Register
        public static Keys[] _registeredMovementSuspendKeys;

        // state prior to last puls saved here
        private static bool last_IsAoeEnabled;
        private static bool last_IsCombatEnabled;
        private static bool last_IsMovementEnabled;
        private static bool last_IsMovementTemporarilySuspended; 

        // state toggle helpers
        private static bool AoeToggle() 
        {   
            _AoeEnabled = _AoeEnabled ? false : true;
#if !REACT_TO_HOTKEYS_IN_PULSE
            AoeKeyHandler();
#endif
            return (_AoeEnabled); 
        }

        private static bool CombatToggle() 
        { 
            _CombatEnabled = _CombatEnabled ? false : true;
#if !REACT_TO_HOTKEYS_IN_PULSE
            CombatKeyHandler();
#endif
            return (_CombatEnabled); 
        }

        private static bool MovementToggle() 
        { 
            _MovementEnabled = _MovementEnabled ? false : true;
            if ( !_MovementEnabled )
                WoWMovement.MoveStop();

#if !REACT_TO_HOTKEYS_IN_PULSE
            MovementKeyHandler();
#endif
            return (_MovementEnabled); 
        }

        private static void MovementTemporary_Suspend()
        {
            if (_MovementEnabled)
            {
                IsMovementTemporarilySuspended = true;
#if !REACT_TO_HOTKEYS_IN_PULSE
                TemporaryMovementKeyHandler();
#endif
            }
        }

        private static void InitKeyStates()
        {
            // reset these values so we begin at same state every Start
            _AoeEnabled = true;
            _CombatEnabled = true;
            _MovementEnabled = true;
            _MovementTemporarySuspendEndtime = DateTime.Now;

            last_IsAoeEnabled = true;
            last_IsCombatEnabled = true;
            last_IsMovementEnabled = true;
            last_IsMovementTemporarilySuspended = false;
        }

        private static Dictionary<string, Keys> mapWowKeyToWindows = new Dictionary<string, Keys>
        {
        };
    }

}