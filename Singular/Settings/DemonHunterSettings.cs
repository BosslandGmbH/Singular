
using System;
using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{

    public enum UseArtifactWeaponWhen
    {
        OnCooldown,
        AtHighestDPSOpportunity,
        None
    }

    internal class DemonHunterSettings : Styx.Helpers.Settings
    {
        public DemonHunterSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "DemonHunter.xml"))
        {
        }

        #region Category: OutOfCombat

        [Setting]
        [DefaultValue(true)]
        [Category("Out of Combat")]
        [DisplayName("Soul Fragment Heal")]
        [Description("While at rest and outside of combat, setting this to true will make the bot use Soul Fragments to heal.")]
        public bool OOCUseSoulFragments { get; set; }

        [Setting]
        [DefaultValue(90)]
        [Category("Out of Combat")]
        [DisplayName("Soul Fragment Health%")]
        [Description("If heal with soul fragments is set to true, this is how low our health percent must be for us to engage the Soul Fragmets.")]
        public int OOCSoulFragmentHealthPercent { get; set; }

        [Setting]
        [DefaultValue(25)]
        [Category("Out of Combat")]
        [DisplayName("Soul Fragment Range")]
        [Description("If heal with soul fragments is set to true, this is how far away the bot will go to get the Soul Fragments.")]
        public float OOCSoulFragmentRange { get; set; }

        #endregion

        #region Category: Havoc

        [Setting]
        [DefaultValue(true)]
        [Category("Havoc")]
        [DisplayName("Fel Rush - DPS")]
        [Description("Fel Rush is apart of the Havoc DPS rotation. If it's causing navigation issues for you, you can turn it off here.")]
        public bool DPSWithFelRush { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Havoc")]
        [DisplayName("Fel Rush - Pull")]
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

        #region HavocSoulFragments
        [Setting]
        [DefaultValue(true)]
        [Category("Havoc - Soul Fragments")]
        [DisplayName("Use Soul Fragments")]
        [Description("Use Soul Fragments to heal or to generate fury while in combat.")]
        public bool HavocUseSoulFragments { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("Havoc - Soul Fragments")]
        [DisplayName("Soul Fragment Health%")]
        [Description("If heal with soul fragments is set to true, this is how low our health percent must be for us to engage the Soul Fragmets.")]
        public int HavocSoulFragmentHealthPercent { get; set; }

        [Setting]
        [DefaultValue(20)]
        [Category("Havoc - Soul Fragments")]
        [DisplayName("Soul Fragment Fury")]
        [Description("If our fury goes under this amount, we can use Soul Fragments to regen Fury.")]
        public int HavocSoulFragmentFuryPercent { get; set; }

        [Setting]
        [DefaultValue(20)]
        [Category("Havoc - Soul Fragments")]
        [DisplayName("Soul Fragment Range")]
        [Description("This is how far away the bot will go out of its way to get the Soul Fragments.")]
        public float HavocSoulFragmentRange { get; set; }
        #endregion

        #region Category: Vengeance

        [Setting]
        [DefaultValue(65)]
        [Category("Vengeance")]
        [DisplayName("Soul Cleave Health %")]
        [Description("Soul Cleave is used to avoid Pain capping, however you can set this so that it's also used to heal when your health gets to this percentage.")]
        public int SoulCleaveHealthPercent { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Vengeance")]
        [DisplayName("Infernal Strike - DPS")]
        [Description("Infernal Strike can be used as an AoE damage ability. By default the bot will AoE with this spell if we have at least two charges of it. Setting this to false will prevent usage of completely.")]
        public bool DPSInfernalStrike { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Vengeance")]
        [DisplayName("Infernal Strike - Pull")]
        [Description("Use Infernal Strike to pull mobs.  Only used if we're engaging mobs that aren't yet fighting us.")]
        public bool PullInfernalStrike { get; set; }

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
        [Description("This will make the bot cast Sigil of Flame if this many mobs or more are attacking us.")]
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

        #region Category: Artifact Weapon
        [Setting]
        [DefaultValue(UseArtifactWeaponWhen.OnCooldown)]
        [Category("Artifact Weapon Usage")]
        [DisplayName("Use When...")]
        [Description("Toggle when the artifact weapon ability should be used.")]
        public UseArtifactWeaponWhen UseArtifactWeaponWhen { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Artifact Weapon Usage")]
        [DisplayName("Use Only In AoE")]
        [Description("If set to true, this will make the artifact waepon only be used when more than one mob is attacking us.")]
        public bool UseArtifactOnlyInAoE { get; set; }
        #endregion

    }
}