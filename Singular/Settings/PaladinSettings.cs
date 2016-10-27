﻿
using System;
using System.ComponentModel;
using System.IO;
using Singular.ClassSpecific.Paladin;

using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using Styx.CommonBot;
using Styx;

namespace Singular.Settings
{

    public enum PaladinBlessings
    {
        Auto,
        Kings,
        Might
    }

    public enum PaladinSeal
    {
        None = 0,
        Auto = 1,
        Command,
        Truth,
        Insight,
        Righteousness,
        Justice
    }

    internal class PaladinSettings : Styx.Helpers.Settings
    {
        public PaladinSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "Paladin.xml"))
        {
        }


        #region Common
        /*
        [Setting]
        [DefaultValue(PaladinAura.Auto)]
        [Category("Common")]
        [DisplayName("Aura")]
        [Description("The aura to be used while not mounted. Set this to Auto to allow the CC to automatically pick the aura depending on spec.")]
        public PaladinAura Aura { get; set; }
        */

        [Setting]
        [DefaultValue(90)]
        [Category("Common")]
        [DisplayName("Holy Light Health")]
        [Description("Holy Light will be used at this value")]
        public int HolyLightHealth { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Hammer of Justice while Solo")]
        [Description("Stun mobs while Solo. Will use on cooldown to reduce damage taken")]
        public bool StunMobsWhileSolo { get; set; }



        #endregion

        #region Self-Heal

        [Setting]
        [DefaultValue(25)]
        [Category("Self-Healing")]
        [DisplayName("Lay on Hand Health %")]
        [Description("Lay on Hands will be used at this value")]
        public int SelfLayOnHandsHealth { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Self-Healing")]
        [DisplayName("Flash of Light Health %")]
        [Description("Flash of Light will be used at this value")]
        public int SelfFlashOfLightHealth { get; set; }

        #endregion

        #region Holy

        [Setting]
        [DefaultValue(80)]
        [Category("Holy")]
        [DisplayName("Divine Protection Health")]
        [Description("Divine Protection will be used at this health %")]
        public int DivineProtectionHealth { get; set; }

        [Setting]
        [DefaultValue(25)]
        [Category("Holy")]
        [DisplayName("Lay on Hand Health %")]
        [Description("Lay on Hands will be used at this value")]
        public int LayOnHandsHealth { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Holy")]
        [DisplayName("Flash of Light Health %")]
        [Description("Flash of Light will be used at this value")]
        public int FlashOfLightHealth { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Holy")]
        [DisplayName("Light of Dawn Health %")]
        [Description("Light of Dawn will be used at this value")]
        public int LightOfDawnHealth { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Holy")]
        [DisplayName("Tyr's Deliverance Health %")]
        [Description("Tyr's Deliverance will be used at this value")]
        public int TyrsDeliveranceHealth { get; set; }

		[Setting]
        [DefaultValue(3)]
        [Category("Holy")]
        [DisplayName("Tyr's Deliverance Count")]
        [Description("Tyr's Deliverance will be used when there are more then that many players with lower health then LoD Health setting")]
        public int TyrsDeliveranceCount { get; set; }

		[Setting]
        [DefaultValue(2)]
        [Category("Holy")]
        [DisplayName("Light of Dawn Count")]
        [Description("Light of Dawn will be used when there are more then that many players with lower health then LoD Health setting")]
        public int LightOfDawnCount { get; set; }

        [Setting]
        [DefaultValue(90)]
        [Category("Holy")]
        [DisplayName("Bestow Faith Health %")]
        [Description("Bestow Faith will be used at this value")]
        public int BestowFaithHealth { get; set; }

        [Setting]
        [DefaultValue(95)]
        [Category("Holy")]
        [DisplayName("Holy Shock Health %")]
        [Description("Holy Shock will be used at this value")]
        public int HolyShockHealth { get; set; }

		[Setting]
        [DefaultValue(80)]
        [Category("Holy")]
        [DisplayName("Beacon of Virtue Health %")]
        [Description("Beacon of Virtue will be used at this value")]
        public int BeaconVirtueHealth { get; set; }

		[Setting]
        [DefaultValue(3)]
        [Category("Holy")]
        [DisplayName("Beacon of Virtue Count")]
        [Description("Beacon of Virtue will be used when there are more then that many players with lower health then BoV Health setting")]
        public int BeaconVirtueCount { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("Holy")]
        [DisplayName("Avenging Wrath Health %")]
        [Description("Beacon of Virtue will be used at this value")]
        public int AvengingHealth { get; set; }

		[Setting]
        [DefaultValue(3)]
        [Category("Holy")]
        [DisplayName("Avenging Wrath Count")]
        [Description("Beacon of Virtue will be used when there are more then that many players with lower health then AW Health setting")]
        public int AvengingCount { get; set; }

		[Setting]
        [DefaultValue(55)]
        [Category("Holy")]
        [DisplayName("Aura Mastery Health %")]
        [Description("Aura Mastery will be used at this value")]
        public int AuraMasteryHealth { get; set; }

		[Setting]
        [DefaultValue(3)]
        [Category("Holy")]
        [DisplayName("Aura Mastery Count")]
        [Description("Aura Mastery will be used when there are more then that many players with lower health then AM Health setting")]
        public int AuraMasteryCount { get; set; }

        #endregion

        #region Protection
        [Setting]
        [DefaultValue(true)]
        [Category("Protection")]
        [DisplayName("Use Divine Steed")]
        [Description("Toggle if we should use Divine Steed or not.")]
        public bool UseDivineSteed { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Protection")]
        [DisplayName("Guardian of Ancient Kings Health")]
        [Description("Guardian of Ancient Kings will be used at this value")]
        public int GoAKHealth { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Protection")]
        [DisplayName("Ardent Defender Health")]
        [Description("Ardent Defender will be used at this value")]
        public int ArdentDefenderHealth { get; set; }

        [Setting]
        [DefaultValue(20)]
        [Category("Protection")]
        [DisplayName("Divine Shield Health")]
        [Description("Divine Shield will be used at this health %")]
        public int DivineShieldHealthProt { get; set; }
        #endregion

        #region Retribution
        [Setting]
        [DefaultValue(85)]
        [Category("Retribution")]
        [DisplayName("Word of Glory Health %")]
        [Description("Health % to use Word of Glory")]
        public int SelfWordOfGloryHealth { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Retribution")]
        [DisplayName("Eye for an Eye Health %")]
        [Description("Health % to use Eye for an Eye")]
        public int EyeForAndEyeHealth { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("Retribution")]
        [DisplayName("Shield of Vengeance Health %")]
        [Description("Health % to use Shield of Vengeance")]
        public int ShieldOfVengeanceHealth { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Retribution")]
        [DisplayName("Use Avenging Wrath")]
        [Description("True: Automatically use Avenging Wrath.  False: you will have to cast manually.")]
        public bool RetAvengAndGoatK { get; set; }

        #endregion

        #region Category: Artifact Weapon
        [Setting]
        [DefaultValue(UseDPSArtifactWeaponWhen.OnCooldown)]
        [Category("Artifact Weapon Usage")]
        [DisplayName("Use When...")]
        [Description("Toggle when the artifact weapon ability should be used. NOTE: Protection Specialization artifact is only affected by the 'None' setting.")]
        public UseDPSArtifactWeaponWhen UseDPSArtifactWeaponWhen { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Artifact Weapon Usage")]
        [DisplayName("Protection: Health Percent to Use")]
        [Description("Once our hearth percent falls below this number, we will use the artifact weapon's ability.")]
        public int ArtifactHealthPercent { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Artifact Weapon Usage")]
        [DisplayName("Use Only In AoE")]
        [Description("If set to true, this will make the artifact waepon only be used when more than one mob is attacking us. NOTE: Protection Specialization artifact is not affected by this.")]
        public bool UseArtifactOnlyInAoE { get; set; }
        #endregion
    }
}