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

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class HunterSettings : Styx.Helpers.Settings
    {
        public HunterSettings()
            : base(SingularSettings.SettingsPath + "_Hunter.xml")
        {
        }

        #region Category: Pet
        [Setting]
        [DefaultValue("1")]
        [Category("Pet")]
        [DisplayName("Pet Slot")]
        public string PetSlot { get; set; }
        #endregion
    }
}