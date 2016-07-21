
using System;
using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class DeathKnightSettings : Styx.Helpers.Settings
    {
        public DeathKnightSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "DeathKnight.xml"))
        {
        }

        #region Common
		
        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Path of Frost")]
        public bool UsePathOfFrost { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Common")]
        [DisplayName("Death and Decay Add Count")]
        [Description("Will use Death and Decay when agro mob count is equal to or higher then this value. This basicly determines AoE rotation")]
        public int DeathAndDecayCount { get; set; }

        #endregion

        #region Category: Blood

        [Setting]
        [DefaultValue(2)]
        [Category("Blood")]
        [DisplayName("Blood Boil Count")]
        [Description("Use Bloodboil when there are at least this many nearby enemies.")]
        public int BloodBoilCount { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Blood")]
        [DisplayName("Rune Tap Percent")]
        [Description("Cast when our Health % falls below this")]
        public int RuneTapPercent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Blood")]
        [DisplayName("Vampiric Blood Percent")]
        [Description("Cast when our Health % falls below this")]
        public int VampiricBloodPercent { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("Vampiric Blood Exclusive")]
        [Description("False: cast if needed, True: cast if needed and no active Vampiric Blood, Dancing Rune Weapon, Lichborne, Icebound Fortitude")]
        public bool VampiricBloodExclusive { get; set; }

		[Setting]
		[DefaultValue(60)]
		[Category("Blood")]
		[DisplayName("Blooddrinker Percent")]
		[Description("Cast when our Health % falls below this")]
		public int BloodDrinkerPercent { get; set; }

		[Setting]
		[DefaultValue(80)]
		[Category("Blood")]
		[DisplayName("Mark of Blood Percent")]
		[Description("Cast when our Health % falls below this")]
		public int MarkOfBloodPercent { get; set; }

		[Setting]
		[DefaultValue(40)]
		[Category("Blood")]
		[DisplayName("Tombstone Percent")]
		[Description("Cast when our Health % falls below this")]
		public int TombstonePercent { get; set; }
		
		[Setting]
		[DefaultValue(3)]
		[Category("Blood")]
		[DisplayName("Tombstone Bone Shield Charges")]
		[Description("Cast when Bone Shield charges equal or greater then this")]
		public int TombstoneBoneShieldCharges { get; set; }

		[Setting]
		[DefaultValue(3)]
		[Category("Blood")]
		[DisplayName("Bonestorm Count")]
		[Description("Use Bonestorm when there are at least this many nearby enemies.")]
		public int BonestormCount { get; set; }

		[Setting]
		[DefaultValue(50)]
		[Category("Blood")]
		[DisplayName("Bonestorm Runic Power Percent")]
		[Description("Cast Bonestorm when Runic Power % is above this")]
		public int BonestormRunicPowerPercent { get; set; }

		#endregion

		#region Category: Frost


        #endregion

        #region Category: Unholy

        [Setting]
        [DefaultValue(true)]
        [Category("Unholy")]
        [DisplayName("Summon Gargoyle")]
        [Description("False: do not cast, True: cast when a long cooldown is appropriate (Boss, PVP, stressful solo fight)")]
        public bool UseSummonGargoyle { get; set; }

		[Setting]
		[DefaultValue(2)]
		[Category("Common")]
		[DisplayName("Epidemic Add Count")]
		[Description("Will use Epidemic when agro mob count is equal to or higher then this value.")]
		public int EpidemicCount { get; set; }

		#endregion

	}
}