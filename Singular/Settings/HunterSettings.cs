
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

        #endregion
    }
}