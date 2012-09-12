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
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class ShamanSettings : Styx.Helpers.Settings
    {
        public ShamanSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, SingularSettings.SettingsPath + "Shaman.xml"))
        {
        }

        #region Category: Enhancement
        [Setting]
        [DefaultValue(CastOn.All)]
        [Category("Enhancement")]
        [DisplayName("Feral Spirit")]
        [Description("Selecet on what type of fight you would like to cast Feral Spirit")]
        public CastOn CastOn  { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Enhancement")]
        [DisplayName("Enhancement Heal")]
        public bool EnhancementHeal { get; set; }

        #endregion

        #region Category: Elemental

        [Setting]
        [DefaultValue(true)]
        [Category("Elemental")]
        [DisplayName("Enable AOE Support")]
        public bool IncludeAoeRotation { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Elemental")]
        [DisplayName("Elemental Heal")]
        public bool ElementalHeal { get; set; }

        #endregion

        #region Category: Restoration

        [Setting]
        [DefaultValue(45)]
        [Category("Restoration")]
        [DisplayName("Heal % Ascendance")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealAscendance { get; set; }

        [Setting]
        [DefaultValue(15)]
        [Category("Restoration")]
        [DisplayName("Heal % Oh Shoot!")]
        [Description("Health % to cast Oh Shoot Heal (Ancestral Swiftness + Greater Healing Wave).  Disabled if set to 0, on cooldown, or talent not selected.")]
        public int HealAncestralSwiftness { get; set; }

        [Setting]
        [DefaultValue(16)]
        [Category("Restoration")]
        [DisplayName("Heal % Healing Surge")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealHealingSurge { get; set; }

        [Setting]
        [DefaultValue(49)]
        [Category("Restoration")]
        [DisplayName("Heal % Unleash Elements")]
        [Description("Health % to cast this ability at. Set to 0 to disable as direct heal, but may still be cast as a buff.")]
        public int HealUnleashElements { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("Heal % Greater Healing Wave")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealGreaterHealingWave { get; set; }

        [Setting]
        [DefaultValue(48)]
        [Category("Restoration")]
        [DisplayName("Heal % Spirit Link Totem")]
        [Description("Health % to cast this ability at.  Only valid in a group. Set to 0 to disable.")]
        public int HealSpiritLinkTotem { get; set; }

        [Setting]
        [DefaultValue(47)]
        [Category("Restoration")]
        [DisplayName("Heal % Healing Tide Totem")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealHealingTideTotem { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("Heal % Healing Wave")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealHealingWave { get; set; }

        [Setting]
        [DefaultValue(85)]
        [Category("Restoration")]
        [DisplayName("Heal % Healing Stream Totem")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealHealingStreamTotem { get; set; }

        [Setting]
        [DefaultValue(92)]
        [Category("Restoration")]
        [DisplayName("Heal % Chain Heal")]
        [Description("Health % to cast this ability at. Must heal minimum 2 people in party, 3 in a raid. Set to 0 to disable.")]
        public int HealChainHeal { get; set; }

        [Setting]
        [DefaultValue(91)]
        [Category("Restoration")]
        [DisplayName("Heal % Healing Rain")]
        [Description("Health % to cast this ability at. Must heal minimum of 3 people in party, 4 in a raid. Set to 0 to disable.")]
        public int HealHealingRain { get; set; }

        #endregion

    }
}