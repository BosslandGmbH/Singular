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
    internal class WarriorSettings : Styx.Helpers.Settings
    {
        public WarriorSettings()
            : base(SingularSettings.SettingsPath + "_Warrior.xml")
        {
        }

        [Setting]
        [Styx.Helpers.DefaultValue(50)]
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

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use Damage Cooldowns")]
        [Description("True / False if you would like the cc to use damage cooldowns")]
        public bool UseWarriorDpsCooldowns { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use Interupts")]
        [Description("True / False if you would like the cc to use Interupts")]
        public bool UseWarriorInterupts { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Slows")]
        [Description("True / False if you would like the cc to use slows ie. Hammstring, Piercing Howl")]
        public bool UseWarriorSlows { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("DPS")]
        [DisplayName("Basic Rotation Only")]
        [Description("True / False if you would like the cc to use just the basic DPS rotation")]
        public bool UseWarriorBasicRotation { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use AOE")]
        [Description("True / False if you would like the cc to use AOE with more than 3 mobs")]
        public bool UseWarriorAOE { get; set; }
    }
}