
using System;
using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    public enum DeathKnightPresence
    {
        None = 0,
        Auto,
        Blood,
        Frost,
        Unholy
    }

    internal class DeathKnightSettings : Styx.Helpers.Settings
    {
        public DeathKnightSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "DeathKnight.xml"))
        {
        }

        #region Common

        [Setting]
        [DefaultValue(DeathKnightPresence.Auto)]
        [Category("Common")]
        [DisplayName("Presence")]
        [Description("Auto: best presence for Spec/Role/Context, None: user controlled")]
        public DeathKnightPresence Presence { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Army of the Dead")]
        public bool UseArmyOfTheDead { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Path of Frost")]
        public bool UsePathOfFrost { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Common")]
        [DisplayName("Conversion Percent")]
        [Description("Health percent when to use Conversion for healing.")]
        public int ConversionPercent { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Common")]
        [DisplayName("Conversion RunicPower Percent")]
        [Description("Use Conversion only if runic power is at or above this value.")]
        public int MinimumConversionRunicPowerPrecent { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Common")]
        [DisplayName("Death and Decay Add Count")]
        [Description("Will use Death and Decay when agro mob count is equal to or higher then this value. This basicly determines AoE rotation")]
        public int DeathAndDecayCount { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Common")]
        [DisplayName("Death Pact Percent")]
        [Description("Health percent when to use Death Pact for healing.")]
        public int DeathPactPercent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Common")]
        [DisplayName("Death Siphon Percent")]
        [Description("Health percent when to use Death Siphon for healing.")]
        public int DeathSiphonPercent { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Common")]
        [DisplayName("Death Strike Emergency Percent [DPS]")]
        public int DeathStrikeEmergencyPercent { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Common")]
        [DisplayName("Icebound Fortitude Percent")]
        public int IceboundFortitudePercent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Common")]
        [DisplayName("Lichborne Percent")]
        [Description("Health percent when to use Lichborne + Death Coil for healing.")]
        public int LichbornePercent { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Raise Ally")]
        [Description("If set to true, it will battle rez via Raise Ally while in combat.")]
        public bool UseRaiseAlly { get; set; }


        #endregion

        #region Category: Blood

        [Setting]
        [DefaultValue(60)]
        [Category("Blood")]
        [DisplayName("Army Of The Dead Percent")]
        public int ArmyOfTheDeadPercent { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Blood")]
        [DisplayName("Blood Boil Count")]
        [Description("Use Bloodboil when there are at least this many nearby enemies.")]
        public int BloodBoilCount { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("BoneShield Exclusive")]
        public bool BoneShieldExclusive { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("Death Pact Exclusive")]
        public bool DeathPactExclusive { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Blood")]
        [DisplayName("Empower Rune Weapon Percent")]
        public int EmpowerRuneWeaponPercent { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("Icebound Fortitude Exclusive")]
        public bool IceboundFortitudeExclusive { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("Lichborne Exclusive")]
        [Description("Use Lichborne only if these not active: Bone Shield, Vampiric Blood, Dancing Rune Weapon, Icebound Fortitude")]
        public bool LichborneExclusive { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Blood")]
        [DisplayName("Rune Tap Percent")]
        [Description("Health percent when to use Rune Tap for healing.")]
        public int RuneTapPercent { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Blood")]
        [DisplayName("Summon Ghoul Percent")]
        public int SummonGhoulPercentBlood { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("Use Ghoul As Dps CoolDown")]
        [Description("Use Ghoul As Dps CoolDown ")]
        public bool UseGhoulAsDpsCdBlood { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Blood")]
        [DisplayName("Vampiric Blood Percent")]
        public int VampiricBloodPercent { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("Vampiric Blood Exclusive")]
        public bool VampiricBloodExclusive { get; set; } 
        #endregion

        #region Category: Frost
        [Setting]
        [DefaultValue(false)]
        [Category("Frost")]
        [DisplayName("Use Ghoul As Dps CoolDown")]
        [Description("Use Ghoul As Dps CoolDown ")]
        public bool UseGhoulAsDpsCdFrost { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Frost")]
        [DisplayName("Summon Ghoul Percent")]
        public int SummonGhoulPercentFrost { get; set; }
        
        #endregion

        #region Category: Unholy

        [Setting]
        [DefaultValue(true)]
        [Category("Unholy")]
        [DisplayName("Summon Gargoyle")]
        public bool UseSummonGargoyle { get; set; }

        #endregion

    }
}