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
        [DefaultValue(89)]
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
        [DefaultValue(75)]
        [Category("RAF Group Heal")]
        [DisplayName("Greater Healing Wave Health")]
        [Description("Health % ")]
        public int RAF_GreaterHealingWave_Health { get; set; }

        [Setting]
        [DefaultValue(74)]
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


    }
}