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

namespace Singular.Settings
{
    internal class MageSettings : Styx.Helpers.Settings
    {
        public MageSettings()
            : base(SingularSettings.SettingsPath + "_Mage.xml")
        {

        }
        #region Category: Common

        [SettingAttribute]
        [Styx.Helpers.DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Summon Table If In A Party")]
        [Description("Summons a food table instead of using conjured food if in a party")]
        public bool SummonTableIfInParty { get; set; }

        #endregion
    }
}