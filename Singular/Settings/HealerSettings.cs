#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author: $
// $Date: $
// $HeadURL: $
// $LastChangedBy: $
// $LastChangedDate: $
// $LastChangedRevision: $
// $Revision: $

#endregion

using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class HealerSettings : Styx.Helpers.Settings
    {
        [Browsable(false)]
        public WoWContext WoWContext { get; set; }

        // reqd ctor
        public HealerSettings(string className, WoWContext ctx)
            : base(Path.Combine(SingularSettings.SettingsPath, className + "-Heal-" + ctx.ToString() + ".xml"))
        {
            WoWContext = ctx;
        }

        // hide default ctor
        private HealerSettings()
            : base(null)
        {
        }


#if false

        [Setting]
        [DefaultValue(47)]
        [Category("Talents")]
        [DisplayName("Stone Bulwark Totem %")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int StoneBulwarkTotemPercent { get; set; }

#endif
    }
}