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

using Styx;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class WarriorSettings : Styx.Helpers.Settings
    {
        public WarriorSettings()
            : base(SingularSettings.SettingsPath + "_Warrior.xml")
        {
        }

		[Setting]
		[DefaultValue(50)]
		[Category("Protection")]
		[DisplayName("Enraged Regeneration Health")]
		[Description("Enrage Regeneration will be used when your health drops below this value")]
		public int WarriorEnragedRegenerationHealth { get; set; }

		[Setting]
		[DefaultValue(40)]
		[Category("Protection")]
		[DisplayName("Shield Wall Health")]
		[Description("Shield Wall will be used when your health drops below this value")]
		public int WarriorProtShieldWallHealth { get; set; }
    }
}