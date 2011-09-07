#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author$
// $Date$
// $HeadURL$
// $LastChangedBy$
// $LastChangedDate$
// $LastChangedRevision$
// $Revision$

#endregion

using System.ComponentModel;

using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

namespace Singular.Settings
{
    internal class ShamanSettings : Styx.Helpers.Settings
    {
        public ShamanSettings()
            : base(SingularSettings.SettingsPath + "_Shaman.xml")
        {
        }

        [Setting]
        [Styx.Helpers.DefaultValue(WoWTotem.None)]
        [Category("Totems")]
        [Description("The totem to use for this slot. Select 'None' for automatic usage.")]
        public WoWTotem FireTotem { get; set; }
        [Setting]
        [Styx.Helpers.DefaultValue(WoWTotem.None)]
        [Category("Totems")]
        [Description("The totem to use for this slot. Select 'None' for automatic usage.")]
        public WoWTotem EarthTotem { get; set; }
        [Setting]
        [Styx.Helpers.DefaultValue(WoWTotem.None)]
        [Category("Totems")]
        [Description("The totem to use for this slot. Select 'None' for automatic usage.")]
        public WoWTotem WaterTotem { get; set; }
        [Setting]
        [Styx.Helpers.DefaultValue(WoWTotem.None)]
        [Category("Totems")]
        [Description("The totem to use for this slot. Select 'None' for automatic usage.")]
        public WoWTotem AirTotem { get; set; }
    }
}