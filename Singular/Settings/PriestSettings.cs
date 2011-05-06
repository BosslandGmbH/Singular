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

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class PriestSettings : Styx.Helpers.Settings
    {
        public PriestSettings()
            : base(SingularSettings.SettingsPath + "_Priest.xml")
        {
        }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Shadow Protection")]
        [Description("Use Shadow Protection buff")]
        public bool UseShadowProtection { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Fear Ward")]
        [Description("Use Fear Ward buff")]
        public bool UseFearWard { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Inner Fire")]
        [Description("Use Inner Fire, otherwise uses Inner Will")]
        public bool UseInnerFire { get; set; }
    }
}