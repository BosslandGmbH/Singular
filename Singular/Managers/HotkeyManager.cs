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
using System.Runtime.InteropServices;

namespace Singular.Managers
{
    internal static class HotkeyDirector
    {
        private static HotkeySettings HotkeySettings { get { return SingularSettings.Instance.Hotkeys(); } }

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
            get 
            { 
                // check if not suspended
                if (_MovementTemporarySuspendEndtime == DateTime.MinValue )
                    return false;

                // check if still suspended
                if ( _MovementTemporarySuspendEndtime > DateTime.Now )
                    return true;

                // suspend has timed out, so refresh suspend timer if key is still down
                // -- currently only check last key pressed rather than every suspend key configured
                // if ( HotkeySettings.SuspendMovementKeys.Any( k => IsKeyDown( k )))
                if ( IsKeyDown( _lastMovementTemporarySuspendKey ))
                {
                    _MovementTemporarySuspendEndtime = DateTime.Now + TimeSpan.FromSeconds(HotkeySettings.SuspendDuration);
                    return true;
                }

                _MovementTemporarySuspendEndtime = DateTime.MinValue;
                return false;
            }

            set
            {
                if (value)
                    _MovementTemporarySuspendEndtime = DateTime.Now + TimeSpan.FromSeconds(HotkeySettings.SuspendDuration);
                else
                    _MovementTemporarySuspendEndtime = DateTime.MinValue;
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
                    HotkeyDirector.Start(true);
                else if (arg.Event == SingularBotEvent.BotStop)
                    HotkeyDirector.Stop();
            };
        }

        internal static void Start(bool needReset = false)
        {
            if (needReset)
                InitKeyStates();

            _HotkeysRegistered = true;

            // Hook the  hotkeys for the appropriate WOW Window...
            HotkeysManager.Initialize( StyxWoW.Memory.Process.MainWindowHandle);

            // register hotkey for commands with 1:1 key assignment
            if (HotkeySettings.AoeToggle != Keys.None)
                RegisterHotkeyAssignment("AOE", HotkeySettings.AoeToggle, (hk) => { AoeToggle(); });

            if (HotkeySettings.CombatToggle != Keys.None)
                RegisterHotkeyAssignment("Combat", HotkeySettings.CombatToggle, (hk) => { CombatToggle(); });

            // note: important to not check MovementManager if movement disabled here, since MovementManager calls us
            // .. and the potential for side-effects exists.  check SingularSettings directly for this only
            if (!SingularSettings.Instance.DisableAllMovement && HotkeySettings.MovementToggle != Keys.None)
                RegisterHotkeyAssignment("Movement", HotkeySettings.MovementToggle, (hk) => { MovementToggle(); });

            // note: important to not check MovementManager if movement disabled here, since MovementManager calls us
            // .. and the potential for side-effects exists.  check SingularSettings directly for this only
#if HONORBUDDY_SUPPORTS_HOTKEYS_WITHOUT_REQUIRING_A_MODIFIER
            if (SingularSettings.Instance.DisableAllMovement || !HotkeySettings.SuspendMovement )
                _registeredMovementSuspendKeys = null;
            else
            {
                // save shallow copy of keys so we can remove if user changes keys in settings
                _registeredMovementSuspendKeys = (Keys[])HotkeySettings.SuspendMovementKeys.Clone();

                // register hotkeys for commands with 1:M key assignment
                foreach (var key in _registeredMovementSuspendKeys)
                {
                    HotkeysManager.Register("Movement Suspend(" + key.ToString() + ")", key, 0, (hk) => { MovementTemporary_Suspend(hk); });
                }
            }
#else
            if ( HotkeySettings.SuspendMovement )
            {
                Logger.Write(".");
                Logger.Write(Color.HotPink, "warning: HonorBuddy does not currently support Hotkeys defined without a "
                                        + "modifier like Shift, Alt, or Control.  This prevents you from "
                                        + "setting up Singular to temporarily suspend movement as you have in the past.  "
                                        + "This feature will remain disabled until that has been resolved.  "
                                        + "Your settings have been retained so your setup is available "
                                        + "when the feature is restored for use.");
            }
#endif
        }

        private static void RegisterHotkeyAssignment(string name, Keys key, Action<Hotkey> callback)
        {
            Keys keyCode = key & Keys.KeyCode;
            ModifierKeys mods = 0;

            if ((key & Keys.Shift) != 0)
                mods |= ModifierKeys.Shift;
            if ((key & Keys.Alt) != 0)
                mods |= ModifierKeys.Alt;
            if ((key & Keys.Control) != 0)
                mods |= ModifierKeys.Control;

            if (mods != 0)
            {
                Logger.Write(Color.White, "Hotkey: To disable {0}, press: [{1}]", name, key.ToFormattedString());
                HotkeysManager.Register(name, keyCode, mods, callback);
            }
            else
            {
                Logger.Write("-");
                Logger.Write(Color.HotPink, "warning: {0} cannot be a hotkey for disabling {1}!  HonorBuddy now requires you to add a Shift, Alt, or Control modifier key to work.  For example, change your config to use Shift+{0}",
                    key.ToFormattedString(),
                    name);
            }
        }

        private static string ToFormattedString(this Keys key)
        {
            string txt = "";

            if ((key & Keys.Shift) != 0)
                txt += "Shift+";
            if ((key & Keys.Alt) != 0)
                txt += "Alt+";
            if ((key & Keys.Control) != 0)
                txt += "Ctrl+";
            txt += (key & Keys.KeyCode).ToString();
            return txt;
        }

        internal static void Stop()
        {
            if (!_HotkeysRegistered)
                return;

            _HotkeysRegistered = false;

            // remove hotkeys for commands with 1:1 key assignment          
            HotkeysManager.Unregister("AOE");
            HotkeysManager.Unregister("Combat");
            HotkeysManager.Unregister("Movement");

            // remove hotkeys for commands with 1:M key assignment
            if (_registeredMovementSuspendKeys != null)
            {
                foreach (var key in _registeredMovementSuspendKeys)
                {
                    HotkeysManager.Unregister("Movement Suspend(" + key.ToString() + ")");
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
                    TellUser("AoE disabled... press {0} to enable", HotkeySettings.AoeToggle.ToFormattedString() );
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
                    TellUser("Combat disabled... press {0} to enable", HotkeySettings.CombatToggle.ToFormattedString());
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
                    TellUser("Movement disabled... press {0} to enable", HotkeySettings.MovementToggle.ToFormattedString() );

                MovementManager.Update();
            }
        }

        internal static void TemporaryMovementKeyHandler()
        {
            if (IsMovementTemporarilySuspended != last_IsMovementTemporarilySuspended)
            {
                last_IsMovementTemporarilySuspended = IsMovementTemporarilySuspended;

                // keep these notifications in Log window only
                if (last_IsMovementTemporarilySuspended)
                    Logger.Write(Color.White, "Bot Movement disabled during user movement...");
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
            if ( HotkeySettings.ChatFrameMessage )
                Lua.DoString(string.Format("print('{0}!')", msg));
        }

        #endregion

        // track whether keys registered yet
        private static bool _HotkeysRegistered = false;

        // state of each toggle kept here
        private static bool _AoeEnabled;
        private static bool _CombatEnabled;
        private static bool _MovementEnabled;
        private static DateTime _MovementTemporarySuspendEndtime = DateTime.MinValue;
        private static Keys _lastMovementTemporarySuspendKey;

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
                StopMoving.Now();

#if !REACT_TO_HOTKEYS_IN_PULSE
            MovementKeyHandler();
#endif
            return (_MovementEnabled); 
        }

        private static void MovementTemporary_Suspend( Styx.Common.Hotkey hk)
        {
            _lastMovementTemporarySuspendKey = hk.Key;
            if (_MovementEnabled)
            {
                if (!IsWowKeyBoardFocusInFrame())
                    IsMovementTemporarilySuspended = true;

#if !REACT_TO_HOTKEYS_IN_PULSE
                TemporaryMovementKeyHandler();
#endif
            }
        }

        /// <summary>
        /// returns true if WOW keyboard focus is in a frame/entry field
        /// </summary>
        /// <returns></returns>
        private static bool IsWowKeyBoardFocusInFrame()
        {
            List<string> ret = Lua.GetReturnValues("return GetCurrentKeyBoardFocus()");
            return ret != null;
        }

        private static void InitKeyStates()
        {
            // reset these values so we begin at same state every Start
            _AoeEnabled = true;
            _CombatEnabled = true;
            _MovementEnabled = true;
            _MovementTemporarySuspendEndtime = DateTime.MinValue;

            last_IsAoeEnabled = true;
            last_IsCombatEnabled = true;
            last_IsMovementEnabled = true;
            last_IsMovementTemporarilySuspended = false;
        }

        private static Dictionary<string, Keys> mapWowKeyToWindows = new Dictionary<string, Keys>
        {
        };

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern short GetAsyncKeyState(int vkey);

        static bool IsKeyDown(Keys key)
        {
            return (GetAsyncKeyState((int) key) & 0x8000) != 0;
        }
    }

}