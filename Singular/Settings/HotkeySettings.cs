
using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using System.Windows.Forms;

namespace Singular.Settings
{
    internal class HotkeySettings : Styx.Helpers.Settings
    {
        public HotkeySettings()
            : base(Path.Combine(SingularSettings.CharacterSettingsPath, "SingularSettings.Hotkeys.xml"))
        { 
            // bit of a hack -- SavedToFile setting tracks if we have ever saved
            // .. these settings.  this is needed because we can't use the DefaultValue
            // .. attribute for a multi values setting
            if (!SavedToFile)
            {
                // force to true so when loaded next time we skip the following
                SavedToFile = true;

                // set default values for array
                SuspendMovementKeys = new Keys[] 
                {
                    // default WOW bindings for movement keys
                    Keys.MButton,
                    Keys.RButton,
                    Keys.W,
                    Keys.S,
                    Keys.A,
                    Keys.D,
                    Keys.Q,
                    Keys.E,
                    Keys.Up,
                    Keys.Down,
                    Keys.Left,
                    Keys.Right
                };
            }
        }


        // hidden setting to track if we have ever saved this settings file before
        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool SavedToFile { get; set; }

        [Setting]
        [DefaultValue(Keys.None)]
        [Category("Behavior Toggle")]
        [DisplayName("AOE Combat Toggle")]
        [Description("Enables/Disables AOE Combat Abilities")]
        public Keys AoeToggle { get; set; }

        [Setting]
        [DefaultValue(Keys.None)]
        [Category("Behavior Toggle")]
        [DisplayName("Combat Toggle")]
        [Description("Enables/Disables All Combat Abilities")]
        public Keys CombatToggle { get; set; }

        [Setting]
        [DefaultValue(Keys.None)]
        [Category("Behavior Toggle")]
        [DisplayName("Movement Toggle")]
        [Description("Enables/Disables Singular Movement")]
        public Keys MovementToggle { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Temporary Behavior")]
        [DisplayName("Suspend Movement")]
        [Description("True: movement keys configured below suspend bot movement for # seconds")]
        public bool SuspendMovement { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Temporary Behavior")]
        [DisplayName("Suspend Duration")]
        [Description("Seconds after last suspend keypress to disable movement")]
        public int SuspendDuration { get; set; }

        [Setting]
        [Category("Temporary Behavior")]
        [DisplayName("Suspend Keys")]
        [Description("Keys that will disable ALL movement for # seconds")]
        public Keys[] SuspendMovementKeys { get; set; }

    }
}