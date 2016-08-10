
using System;
using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class DemonHunterSettings : Styx.Helpers.Settings
    {
        public DemonHunterSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "DemonHunter.xml"))
        {
        }

        #region Category: Havoc

        [Setting]
        [DefaultValue(true)]
        [Category("Havoc")]
        [DisplayName("DPS with Fel Rush")]
        [Description("Fel Rush is apart of the Havoc DPS rotation. If it's causing navigation issues for you, you can turn it off here.")]
        public bool DPSWithFelRush { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Havoc")]
        [DisplayName("Engage with Fel Rush")]
        [Description("Use Fel Rush to get towards a mob when first engaging the mob.")]
        public bool EngageWithFelRush { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Havoc")]
        [DisplayName("Use Vengeful Retreat")]
        [Description("Vengeful Retreat is apart of the Havoc DPS rotation. If it's causing navigation issues for you, you can turn it off here.")]
        public bool UseVengefulRetreat { get; set; }

        [Setting]
        [DefaultValue(90)]
        [Category("Havoc")]
        [DisplayName("Metamorphosis Health %")]
        [Description("Use Metamorphosis when you get at or below this health percent.")]
        public int HavocMetamorphosisHealthPercent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Havoc")]
        [DisplayName("Blur Health %")]
        [Description("Use Blur when you get at or below this health percent.")]
        public int BlurHealthPercent { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Havoc")]
        [DisplayName("Darkness Health %")]
        [Description("Use Darnkess when you get at or below this health percent.")]
        public int HavocDarknessHealthPercent { get; set; }

        #endregion

        #region Category: Vengeance

        [Setting]
        [DefaultValue(65)]
        [Category("Vengeance")]
        [DisplayName("Soul Cleave Health %")]
        [Description("Sould Cleave is used to avoid Pain capping, however you can set this so that it's also used to heal when your health gets to this percentage.")]
        public int SoulCleaveHealthPercent { get; set; }

        [Setting]
        [DefaultValue(45)]
        [Category("Vengeance")]
        [DisplayName("Metamorphosis Health %")]
        [Description("Use Metamorphosis when you get at or below this health percent.")]
        public int VengeanceMetamorphosisHealthPercent { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Vengeance")]
        [DisplayName("Sigil of Flame Count")]
        [Description("This will set the routine to cast SigiL of Flame if this many mobs or more are attacking us.")]
        public int SigilOfFlameCount { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("Vengeance")]
        [DisplayName("Demon Spikes Health %")]
        [Description("By default, the bot will only use one charge of Demon Spikes.  With this setting, the bot will keep both charges on cooldown once your health drops below this percentage.")]
        public int DemonSpikesHealthPercent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Vengeance")]
        [DisplayName("Darkness Health %")]
        [Description("Use Darnkess when you get at or below this health percent.")]
        public int VengeanceDarknessHealthPercent { get; set; }

        [Setting]
        [DefaultValue(35)]
        [Category("Vengeance")]
        [DisplayName("Fiery Brand Health %")]
        [Description("Use Fiery Brand if we go below this health percent. Note: If you have the Burning Alive talent, Fiery Brand will be used as an AoE damage ability regardless of this setting.")]
        public int FieryBrandHealthPercent { get; set; }

        #endregion

    }
}