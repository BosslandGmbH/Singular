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
    internal class RogueSettings : Styx.Helpers.Settings
    {
        public RogueSettings()
            : base(SingularSettings.SettingsPath + "_Rogue.xml")
        {
        }

        [Setting]
        [Styx.Helpers.DefaultValue(PoisonType.Instant)]
        [Category("Common")]
        [DisplayName("Main Hand Poison")]
        [Description("Main Hand Poison")]
        public PoisonType MHPoison { get; set; }

        [Setting]
        [DefaultValue(PoisonType.Instant)]
        [Category("Common")]
        [DisplayName("Off Hand Poison")]
        [Description("Off Hand Poison")]
        public PoisonType OHPoison { get; set; }
    }
}