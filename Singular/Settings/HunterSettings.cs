
using System;
using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class HunterSettings : Styx.Helpers.Settings
    {
        public HunterSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "Hunter.xml"))
        {
        }

        #region Category: Pet

        [Setting]
        [DefaultValue(1)]
        [Category("Pet")]
        [DisplayName("Pet Number ( 1 thru 5 )")]
        public int PetNumber { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Pet")]
        [DisplayName("Mend Pet Percent")]
        public double MendPetPercent { get; set; }

        #endregion

        #region Category: Common

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Use Disengage")]
        [Description("Will be used in battlegrounds no matter what this is set")]
        public bool UseDisengage { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Movement")]
        [DisplayName("Allow Kiting")]
        [Description("Controls if movement to evade an enemy during combat is allowed.")]
        public bool AllowKiting { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Widow Venom")]
        [Description("True: keep debuff up on players; False: don't cast.")]
        public bool UseWidowVenom { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Feign Death")]
        [Description("True: use Feign Death if needed; False: don't cast.")]
        public bool UseFeignDeath { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Crowd Control")]
        [DisplayName("Crowd Ctrl Focus Unit")]
        [Description("True: Crowd Control used only for Focus Unit; False: used on any add.")]
        public bool CrowdControlFocus { get; set; }


        #endregion
    }
}