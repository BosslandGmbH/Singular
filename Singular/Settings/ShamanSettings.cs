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
    internal class ShamanSettings : Styx.Helpers.Settings
    {
        public ShamanSettings()
            : base(SingularSettings.SettingsPath + "_Shaman.xml")
        {
        }

        #region GROUP_HEALING
        #region PVP_GROUP_HEALING

        [Setting]
        [DefaultValue(90)]
        [Category("PVP Group Heal")]
        [DisplayName("Healing Wave Health")]
        [Description("Health % ")]
        public int PVP_HealingWave_Health { get; set; }

        [Setting]
        [DefaultValue(89)]
        [Category("PVP Group Heal")]
        [DisplayName("Riptide Health")]
        [Description("Health % ")]
        public int PVP_Riptide_Health { get; set; }

        [Setting]
        [DefaultValue(88)]
        [Category("PVP Group Heal")]
        [DisplayName("Chain Heal Health")]
        [Description("Health % ")]
        public int PVP_ChainHeal_Health { get; set; }

        [Setting]
        [DefaultValue(75)]
        [Category("PVP Group Heal")]
        [DisplayName("Greater Healing Wave Health")]
        [Description("Health % ")]
        public int PVP_GreaterHealingWave_Health { get; set; }

        [Setting]
        [DefaultValue(74)]
        [Category("PVP Group Heal")]
        [DisplayName("Unleash Elements Health")]
        [Description("Health % ")]
        public int PVP_UnleashElements_Health { get; set; }

        [Setting]
        [DefaultValue(45)]
        [Category("PVP Group Heal")]
        [DisplayName("Healing Surge Health")]
        [Description("Health % ")]
        public int PVP_HealingSurge_Health { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("PVP Group Heal")]
        [DisplayName("Oh Shoot Health")]
        [Description("Health % ")]
        public int PVP_OhShoot_Health { get; set; }

        #endregion 

        #region RAF_GROUP_HEALING

        [Setting]
        [DefaultValue(90)]
        [Category("RAF Group Heal")]
        [DisplayName("Healing Wave Health")]
        [Description("Health % ")]
        public int RAF_HealingWave_Health { get; set; }

        [Setting]
        [DefaultValue(0)]
        [Category("RAF Group Heal")]
        [DisplayName("Riptide Health")]
        [Description("Health % ")]
        public int RAF_Riptide_Health { get; set; }

        [Setting]
        [DefaultValue(88)]
        [Category("RAF Group Heal")]
        [DisplayName("Chain Heal Health")]
        [Description("Health % ")]
        public int RAF_ChainHeal_Health { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("RAF Group Heal")]
        [DisplayName("Greater Healing Wave Health")]
        [Description("Health % ")]
        public int RAF_GreaterHealingWave_Health { get; set; }

        [Setting]
        [DefaultValue(69)]
        [Category("RAF Group Heal")]
        [DisplayName("Unleash Elements Health")]
        [Description("Health % ")]
        public int RAF_UnleashElements_Health { get; set; }

        [Setting]
        [DefaultValue(45)]
        [Category("RAF Group Heal")]
        [DisplayName("Healing Surge Health")]
        [Description("Health % ")]
        public int RAF_HealingSurge_Health { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("RAF Group Heal")]
        [DisplayName("Oh Shoot Health")]
        [Description("Health % ")]
        public int RAF_OhShoot_Health { get; set; }

        #endregion 
#endregion

        #region Totems

        [Setting]
        [DefaultValue(WoWTotem.None)]
        [Category("Totems")]
        [Description("The totem to use for this slot. Select 'None' for automatic usage.")]
        public WoWTotem FireTotem { get; set; }
        [Setting]
        [DefaultValue(WoWTotem.None)]
        [Category("Totems")]
        [Description("The totem to use for this slot. Select 'None' for automatic usage.")]
        public WoWTotem EarthTotem { get; set; }
        [Setting]
        [DefaultValue(WoWTotem.None)]
        [Category("Totems")]
        [Description("The totem to use for this slot. Select 'None' for automatic usage.")]
        public WoWTotem WaterTotem { get; set; }
        [Setting]
        [DefaultValue(WoWTotem.None)]
        [Category("Totems")]
        [Description("The totem to use for this slot. Select 'None' for automatic usage.")]
        public WoWTotem AirTotem { get; set; }

        #endregion
		
		#region Elemental
		
		[Setting]
        [DefaultValue(50)]
        [Category("Elemental")]
        [DisplayName("Healing Surge Health")]
        [Description("Health % ")]
        public int Elemental_HealingSurge_Health { get; set; }
		
		#endregion
    }
}