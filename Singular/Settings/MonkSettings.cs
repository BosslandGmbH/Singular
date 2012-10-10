#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author: Laria $
// $Date: 2011-05-03 18:16:12 +0300 (Sal, 03 May 2011) $
// $HeadURL: http://svn.apocdev.com/singular/trunk/Singular/Settings/MonkSettings.cs $
// $LastChangedBy: Laria$
// $LastChangedDate: 2011-05-03 18:16:12 +0300 (Sal, 03 May 2011) $
// $LastChangedRevision: 307 $
// $Revision: 307 $

#endregion

using System.ComponentModel;
using System.IO;
using Singular.ClassSpecific.Rogue;
using Styx.Helpers;

namespace Singular.Settings
{
    internal class MonkSettings : Styx.Helpers.Settings
    {
        public MonkSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "Monk.xml")) { }

        #region Common

        [Setting]
        [Styx.Helpers.DefaultValue(60)]
        [Category("Common")]
        [DisplayName("Fortifying Brew Percent")]
        [Description("Fortifying Brew is used when health percent is at or below this value")]
        public float FortifyingBrewPercent { get; set; }

        [Setting]
        [Styx.Helpers.DefaultValue(70)]
        [Category("Common")]
        [DisplayName("Chi Wave Percent")]
        [Description("Chi Wave is used when health percent is at or below this value")]
        public float ChiWavePercent { get; set; }

        #endregion

        #region Brewmaster

        [Setting]
        [Styx.Helpers.DefaultValue(70)]
        [Category("Brewmaster")]
        [DisplayName("Avert Harm Group Health Percent")]
        [Description("Avert Harm is used when the averge health percent of group falls below this value")]
        public float AvertHarmGroupHealthPercent { get; set; }

        [Setting]
        [Styx.Helpers.DefaultValue(true)]
        [Category("Brewmaster")]
        [DisplayName("Use Avert Harm")]
        public bool UseAvertHarm { get; set; }
        #endregion


    }
}