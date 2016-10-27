
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
        [DefaultValue(50)]
        [Category("Blood")]
        [DisplayName("Dancing Rune Weapon Percent")]
        [Description("Cast when our Health % falls below this")]
        public int DancingRuneWeaponPercent { get; set; }

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

		#region Category: Defensives

		[Setting]
		[DefaultValue(50)]
		[Category("Defensives")]
		[DisplayName("Death Strike")]
		[Description("Cast when our Health % is at or below this")]
		public int DeathStrikePercent { get; set; }

		[Setting]
		[DefaultValue(100)]
		[Category("Defensives")]
		[DisplayName("Death Strike (Dark Succor)")]
		[Description("When Dark Succor is procced, cast Death Strike when our Health % is at or below this")]
		public int DeathStrikeSuccorPercent { get; set; }

		[Setting]
		[DefaultValue(50)]
		[Category("Defensives")]
		[DisplayName("Icebound Fortitude")]
		[Description("Cast when our Health % is at or below this")]
		public int IceboundFortitudePercent { get; set; }

		[Setting]
		[DefaultValue(50)]
		[Category("Defensives")]
		[DisplayName("Unholy: Corpse Shield")]
		[Description("Cast when our Health % is at or below this")]
		public int CorpseShieldPercent { get; set; }

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
		[Category("Unholy")]
		[DisplayName("Epidemic Add Count")]
		[Description("Will use Epidemic when agro mob count is equal to or higher then this value.")]
		public int EpidemicCount { get; set; }

        #endregion

        #region Category: Artifact Weapon
        [Setting]
        [DefaultValue(UseDPSArtifactWeaponWhen.OnCooldown)]
        [Category("Artifact Weapon Usage")]
        [DisplayName("Use When...")]
        [Description("Toggle when the artifact weapon ability should be used. NOTE: Setting AtHighestDPSOpportunity will only make it cast at 8 Festering Wounds.  OnCooldown abides by the Festering Wounds Count setting.")]
        public UseDPSArtifactWeaponWhen UseDPSArtifactWeaponWhen { get; set; }

        [Setting]
        [DefaultValue(7)]
        [Category("Artifact Weapon Usage")]
        [DisplayName("Unholy: Festering Wounds Count")]
        [Description("This is only used if UseWhen is set to OnCooldown! This is how many stacks of Festering Wounds must be on our current target before Apocalypse is used.")]
        public int FesteringWoundsCount { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Artifact Weapon Usage")]
        [DisplayName("Use Only In AoE")]
        [Description("If set to true, this will make the artifact waepon only be used when more than one mob is attacking us.")]
        public bool UseArtifactOnlyInAoE { get; set; }
        #endregion

    }
}