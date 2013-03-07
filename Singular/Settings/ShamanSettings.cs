
using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using Styx;
using System.Drawing;

namespace Singular.Settings
{
    internal class ShamanSettings : Styx.Helpers.Settings
    {
        public ShamanSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "Shaman.xml"))
        {
        }

        #region Context Late Loading Wrappers

        private ShamanHealSettings _battleground;
        private ShamanHealSettings _instance;
        private ShamanHealSettings _raid;
        private ShamanHealSettings _normal;

        [Browsable(false)]
        public ShamanHealSettings Battleground { get { return _battleground ?? (_battleground = new ShamanHealSettings( HealingContext.Battlegrounds )); } }

        [Browsable(false)]
        public ShamanHealSettings Instance { get { return _instance ?? (_instance = new ShamanHealSettings(HealingContext.Instances)); } }

        [Browsable(false)]
        public ShamanHealSettings Raid { get { return _raid ?? (_raid = new ShamanHealSettings(HealingContext.Raids)); } }

        [Browsable(false)]
        public ShamanHealSettings Normal { get { return _normal ?? (_normal = new ShamanHealSettings(HealingContext.Normal)); } }

        [Browsable(false)]
        public ShamanHealSettings Heal { get { return HealLookup(Singular.SingularRoutine.CurrentWoWContext); } }
        
        public ShamanHealSettings HealLookup( WoWContext ctx)
        {
            if (ctx == WoWContext.Battlegrounds)
                return Battleground;
            if (ctx == WoWContext.Instances)
                return StyxWoW.Me.CurrentMap.IsRaid ? Raid : Instance;
            return Normal;
        }

        #endregion


        #region Category: Common

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Ghost Wolf")]
        [Description("Cast Ghost Wolf while running on foot or indoors")]
        public bool UseGhostWolf { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Off-heal Allowed")]
        [Description("Off-heal anyone below 30% or no healers nearby (never in raids)")]
        public bool AllowOffHealHeal { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Use Bloodlust/Heroism")]
        [Description("Lust when appropriate (never when movement disabled)")]
        public bool UseBloodlust { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Weapon Imbues")]
        [Description("True: Automatically select and apply weapon imbues, False: automatic cast of imbues prevented")]
        public bool UseWeaponImbues { get; set; }

        #endregion

        #region Category: Enhancement
        [Setting]
        [DefaultValue(CastOn.All)]
        [Category("Enhancement")]
        [DisplayName("Feral Spirit")]
        [Description("Selecet on what type of fight you would like to cast Feral Spirit")]
        public CastOn FeralSpiritCastOn  { get; set; }

        #endregion

        #region Category: Restoration

        [Setting]
        [DefaultValue(85)]
        [Category("Totems")]
        [DisplayName("Healing Stream Totem %")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealHealingStreamTotem { get; set; }

        #endregion

        #region Talents

        [Setting]
        [DefaultValue(47)]
        [Category("Talents")]
        [DisplayName("Astral Shift %")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int AstralShiftPercent { get; set; }

        [Setting]
        [DefaultValue(47)]
        [Category("Talents")]
        [DisplayName("Healing Tide Totem %")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingTideTotemPercent { get; set; }

        [Setting]
        [DefaultValue(47)]
        [Category("Talents")]
        [DisplayName("Stone Bulwark Totem %")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int StoneBulwarkTotemPercent { get; set; }

        #endregion
    }

    internal class ShamanHealSettings : Singular.Settings.HealerSettings
    {
        private ShamanHealSettings()
            : base("", HealingContext.None)
        {
        }

        public ShamanHealSettings(HealingContext ctx)
            : base("Shaman", ctx)
        {

            if (!HealingSurgeAdjusted && StyxWoW.Me.Level >= 60)
            {
                if ( SavedToFile )
                    Logger.Write(Color.White, "Adjusting saved Healing Surge % from {0} to 21 for Instances.  Visit Class Config and Save to make permanent.", HealingSurge);

                HealingSurge = 21;
                HealingSurgeAdjusted = true;
            }

            // bit of a hack.  using SavedToFile setting to catch if we have
            // .. written settings yet.  if not, do context specific initialization 
            // .. here since we don't want same DefaultValue() for every context
            if (SavedToFile)
                return;

            SavedToFile = true;
            if (ctx == Singular.HealingContext.Battlegrounds)
            {
                HealingWave = 0;
                ChainHeal = 94;
                HealingRain = 93;
                GreaterHealingWave = 70;
                Ascendance = 40;
                SpiritLinkTotem = 48;
                HealingSurge = 80;
                AncestralSwiftness = 35;
                HealingStreamTotem = 87;
                HealingTideTotemPercent = 49;

                RollRiptideCount = 0;
                MinHealingRainCount = 4;
                MinChainHealCount = 3;
                MinHealingTideCount = 3;
            }
            else if (ctx == Singular.HealingContext.Instances)
            {
                HealingWave = 90;
                ChainHeal = 89;
                HealingRain = 88;
                GreaterHealingWave = 70;
                Ascendance = 40;
                SpiritLinkTotem = 48;
                HealingSurge = 60;
                AncestralSwiftness = 20;
                HealingStreamTotem = 87;
                HealingTideTotemPercent = 49;

                RollRiptideCount = 0;
                MinHealingRainCount = 4;
                MinChainHealCount = 3;
                MinHealingTideCount = 2;
            }
            else if (ctx == Singular.HealingContext.Raids)
            {
                HealingWave = 97;
                ChainHeal = 96;
                HealingRain = 95;
                GreaterHealingWave = 65;
                Ascendance = 40;
                SpiritLinkTotem = 48;
                HealingSurge = 60;
                AncestralSwiftness = 20;
                HealingStreamTotem = 87;
                HealingTideTotemPercent = 49;

                RollRiptideCount = 3;
                MinHealingRainCount = 3;
                MinChainHealCount = 2;
                MinHealingTideCount = 2;
            }
            // omit case for WoWContext.Normal and let it use DefaultValue() values
        }

        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool SavedToFile { get; set; }

        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool HealingSurgeAdjusted { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("% Healing Wave")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingWave { get; set; }

        [Setting]
        [DefaultValue(92)]
        [Category("Restoration")]
        [DisplayName("% Chain Heal")]
        [Description("Health % to cast this ability at. Must heal minimum 2 people in party, 3 in a raid. Set to 0 to disable.")]
        public int ChainHeal { get; set; }

        [Setting]
        [DefaultValue(91)]
        [Category("Restoration")]
        [DisplayName("% Healing Rain")]
        [Description("Health % to cast this ability at. Must heal minimum of 3 people in party, 4 in a raid. Set to 0 to disable.")]
        public int HealingRain { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("% Greater Healing Wave")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int GreaterHealingWave { get; set; }

        [Setting]
        [DefaultValue(45)]
        [Category("Restoration")]
        [DisplayName("% Ascendance")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int Ascendance { get; set; }

        [Setting]
        [DefaultValue(16)]
        [Category("Restoration")]
        [DisplayName("% Healing Surge")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingSurge { get; set; }

        [Setting]
        [DefaultValue(15)]
        [Category("Restoration")]
        [DisplayName("% Oh Shoot!")]
        [Description("Health % to cast Oh Shoot Heal (Ancestral Swiftness + Greater Healing Wave).  Disabled if set to 0, on cooldown, or talent not selected.")]
        public int AncestralSwiftness { get; set; }

        [Setting]
        [DefaultValue(48)]
        [Category("Restoration")]
        [DisplayName("% Spirit Link Totem")]
        [Description("Health % to cast this ability at.  Only valid in a group. Set to 0 to disable.")]
        public int SpiritLinkTotem { get; set; }

        [Setting]
        [DefaultValue(95)]
        [Category("Restoration")]
        [DisplayName("% Healing Stream Totem")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingStreamTotem { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Talents")]
        [DisplayName("Healing Tide Totem %")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingTideTotemPercent { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Restoration")]
        [DisplayName("Roll Riptide Max Count")]
        [Description("Maximum number of players to roll Riptide on")]
        public int RollRiptideCount { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Restoration")]
        [DisplayName("Healing Rain Min Count")]
        [Description("Minimum number of players below Healing Rain % in area")]
        public int MinHealingRainCount { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Restoration")]
        [DisplayName("Chain Heal Min Count")]
        [Description("Minimum number of players healead")]
        public int MinChainHealCount { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Restoration")]
        [DisplayName("Chain Heal Min Count")]
        [Description("Minimum number of players healead")]
        public int MinHealingTideCount { get; set; }

    }
}