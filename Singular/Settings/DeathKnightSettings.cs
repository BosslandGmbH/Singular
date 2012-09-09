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
            : base(Path.Combine(SingularSettings.SettingsPath, "DeathKnight.xml"))
        {
        }

        #region Common

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Icebound Fortitude")]
        public bool UseIceboundFortitude { get; set; }

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
        [DefaultValue(60)]
        [Category("Common")]
        [DisplayName("Conversion Percent")]
        [Description("Health percent when to use Conversion for healing.")]
        public int ConversionPercent { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Common")]
        [DisplayName("Min. Conversion RunicPower")]
        [Description("Use Conversion only if runic power is at or above this value.")]
        public int MinimumConversionRunicPowerPrecent { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Common")]
        [DisplayName("Death Strike Emergency Percent")]
        public int DeathStrikeEmergencyPercent { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Death and Decay")]
        public bool UseDeathAndDecay { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Common")]
        [DisplayName("Death and Decay Add Count")]
        [Description("Will use Death and Decay when agro mob count is equal to or higher then this value. This basicly determines AoE rotation")]
        public int DeathAndDecayCount { get; set; }
        
        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Army of the Dead")]
        public bool UseArmyOfTheDead { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Use Ghoul As Dps CoolDown")]
        [Description("Use Ghoul As Dps CoolDown [Blood/Frost]")]
        public bool UseGhoulAsDpsCoolDown { get; set; }
        #endregion

        #region Category: Blood

        [Setting]
        [DefaultValue(60)]
        [Category("Blood")]
        [DisplayName("Rune Tap Percent")]
        [Description("Health percent when to use Rune Tap for healing.")]
        public int RuneTapPercent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Blood")]
        [DisplayName("Vampiric Blood Percent")]
        public int VampiricBloodPercent { get; set; }

        /*
        [Setting]
        [DefaultValue(20)]
        [Category("Blood")]
        [DisplayName("Army of the Dead Percent")]
        public int ArmyOfTheDeadPercent { get; set; }

         */
        #endregion

        #region Category: Frost
        /*
        [Setting]
        [DefaultValue(true)]
        [Category("Frost")]
        [DisplayName("Pillar of Frost")]
        public bool UsePillarOfFrost { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Frost")]
        [DisplayName("Raise Dead")]
        public bool UseRaiseDead { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Frost")]
        [DisplayName("Empower Rune Weapon")]
        public bool UseEmpowerRuneWeapon { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Frost")]
        [DisplayName("Use Necrotic Strike - Frost")]
        public bool UseNecroticStrike { get; set; }
        */
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