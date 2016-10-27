﻿
using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using Styx;
using System.Drawing;
using System;

namespace Singular.Settings
{
    internal class ShamanSettings : Styx.Helpers.Settings
    {
        public ShamanSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "Shaman.xml"))
        {
        }

        #region Context Late Loading Wrappers

        private ShamanRestoHealSettings _restobattleground;
        private ShamanRestoHealSettings _restoinstance;
        private ShamanRestoHealSettings _restoraid;
        private ShamanOffHealSettings _offhealbattleground;
        private ShamanOffHealSettings _offhealpve;

        [Browsable(false)]
        public ShamanRestoHealSettings RestoBattleground { get { return _restobattleground ?? (_restobattleground = new ShamanRestoHealSettings(HealingContext.Battlegrounds)); } }

        [Browsable(false)]
        public ShamanRestoHealSettings RestoInstance { get { return _restoinstance ?? (_restoinstance = new ShamanRestoHealSettings(HealingContext.Instances)); } }

        [Browsable(false)]
        public ShamanRestoHealSettings RestoRaid { get { return _restoraid ?? (_restoraid = new ShamanRestoHealSettings(HealingContext.Raids)); } }

        [Browsable(false)]
        public ShamanOffHealSettings OffhealBattleground { get { return _offhealbattleground ?? (_offhealbattleground = new ShamanOffHealSettings(HealingContext.Battlegrounds)); } }

        [Browsable(false)]
        public ShamanOffHealSettings OffhealPVE { get { return _offhealpve ?? (_offhealpve = new ShamanOffHealSettings(HealingContext.Instances)); } }

        [Browsable(false)]
        public ShamanRestoHealSettings RestoHealSettings { get { return RestoHealSettingsLookup(Singular.SingularRoutine.CurrentWoWContext); } }

        public ShamanRestoHealSettings RestoHealSettingsLookup(WoWContext ctx)
        {
            if (ctx == WoWContext.Instances)
                return StyxWoW.Me.GroupInfo.IsInRaid ? RestoRaid : RestoInstance;

            if (ctx == WoWContext.Battlegrounds)
                return RestoBattleground;

            return RestoInstance;
        }

        [Browsable(false)]
        public ShamanOffHealSettings OffHealSettings { get { return OffHealSettingsLookup(Singular.SingularRoutine.CurrentWoWContext); } }

        public ShamanOffHealSettings OffHealSettingsLookup(WoWContext ctx)
        {
            if (ctx == WoWContext.Battlegrounds)
                return OffhealBattleground;

            return OffhealPVE;
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
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Use Bloodlust/Heroism")]
        [Description("Lust when appropriate (never when movement disabled)")]
        public bool UseBloodlust { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Ascendance")]
        [Description("True: Automatically cast Ascendance as needed, False: never cast Ascendance (left for User Control)")]
        public bool UseAscendance { get; set; }

        [Setting]
        [DefaultValue(15)]
        [Category("Common")]
        [DisplayName("Twist Water Shield Mana %")]
        [Description("Water Shield if below this Mana %")]
        public int TwistWaterShield { get; set; }


        private int _TwistDamageShield;

        [Setting]
        [DefaultValue(25)]
        [Category("Common")]
        [DisplayName("Twist Lghtng/Earth Shield % Mana")]
        [Description("Lightning (DPS) or Earth (Resto) Shield above this % Mana.  Note: muat be minimum of Water Shield % + 10")]
        public int TwistDamageShield
        {
            get { return Math.Max(_TwistDamageShield, TwistWaterShield + 10); }
            set { _TwistDamageShield = value; }
        }

        [Setting]
        [DefaultValue(70)]
        [Category("Common")]
        [DisplayName("Shamanistic Rage %")]
        [Description("Shamanistic Rage if below this Health %")]
        public int ShamanisticRagePercent { get; set; }

        #endregion


        #region Category: Elemental

        [Setting]
        [DefaultValue(40)]
        [Category("Elemental")]
        [DisplayName("Earth Elemental Health %")]
        [Description("Cast Earth Elemental if our health falls at or below this percent.")]
        public int EarthElementalHealthPercent { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Elemental")]
        [DisplayName("Earthquake Count")]
        [Description("Cast Earthquake on CurrentTarget if this many enemies at that location")]
        public int EarthquakeCount { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Elemental")]
        [DisplayName("Earthquake Maelstrom %")]
        [Description("Do not cast Earthquake if Maelstrom below this percent and cast is not free")]
        public int EarthquakeMaelPercent { get; set; }

        #endregion


        #region Category: Enhancement
        [Setting]
        [DefaultValue(65)]
        [Category("Enhancement")]
        [DisplayName("Ascendance Health %")]
        [Description("Health % to cast this ability at. Set to 100 to cast on cooldown, Set to 0 to disable.")]
        public int AscendanceHealthPercent { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Enhancement")]
        [DisplayName("Feral Lunge")]
        [Description("Use Feral Lunge while pulling or gaping closer to mobs.")]
        public bool UseFeralLunge { get; set; }

        [Setting]
        [DefaultValue(CastOn.All)]
        [Category("Enhancement")]
        [DisplayName("Feral Spirit")]
        [Description("Selecet on what type of fight you would like to cast Feral Spirit")]
        public CastOn FeralSpiritCastOn { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Enhancement")]
        [DisplayName("Maelstrom Healing Surge (Health%)")]
        [Description("Health % to cast this ability at. Set to 100 to cast on cooldown, Set to 0 to disable.")]
        public int MaelHealingSurge { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Enhancement")]
        [DisplayName("Maelstrom Healing Surge (Maelstrom%)")]
        [Description("Maelstrom % to cast this ability at.")]
        public int MaelPercentHealingSurge { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Enhancement")]
        [DisplayName("Disable Maelstrom DPS Casts")]
        [Description("Disable Lightning Bolt, Chain Lightning, and Hex")]
        public bool AvoidMaelstromDamage { get; set; }

        [Setting]
        [DefaultValue(35)]
        [Category("Enhancement")]
        [DisplayName("Maelstrom PVP Off-Heal %")]
        [Description("Health % to cast this ability at. Set to 100 to cast on cooldown, Set to 0 to disable.")]
        public int MaelPvpOffHeal { get; set; }

        #endregion

        #region Category: Self-Healing

        [Setting]
        [DefaultValue(80)]
        [Category("Self-Heal")]
        [DisplayName("Rainfall %")]
        [Description("Health % to cast this ability at. Set to 100 to cast on cooldown, Set to 0 to disable.")]
        public int Rainfall { get; set; }

        [Setting]
        [DefaultValue(85)]
        [Category("Self-Heal")]
        [DisplayName("Healing Stream Totem %")]
        [Description("Health % to cast this ability at. Set to 100 to cast on cooldown, Set to 0 to disable.")]
        public int SelfHealingStreamTotem { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Self-Heal")]
        [DisplayName("Healing Surge %")]
        [Description("Health % to cast this ability at. Set to 100 to cast on cooldown, Set to 0 to disable.")]
        public int SelfHealingSurge { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("Self-Heal")]
        [DisplayName("Ancestral Guidance %")]
        [Description("Health % to cast this ability at. Set to 100 to cast on cooldown, Set to 0 to disable.")]
        public int SelfAncestralGuidance { get; set; }

        [Setting]
        [DefaultValue(25)]
        [Category("Self-Heal")]
        [DisplayName("Ancestral Swiftness Heal %")]
        [Description("Health % to cast this ability at. Set to 100 to cast on cooldown, Set to 0 to disable.")]
        public int SelfAncestralSwiftnessHeal { get; set; }

        #endregion

        #region Category: Totems

        [Setting]
        [DefaultValue(80)]
        [Category("Totems")]
        [DisplayName("Mana Tide Totem %")]
        [Description("Mana % to cast this ability at. Set to 0 to disable.")]
        public int ManaTideTotemPercent { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Totems")]
        [DisplayName("Use Lightning Surge Totem")]
        [Description("Setting this to true will make the bot use Lightning Surge Totem while in solo and during AoE combat.")]
        public bool UseLightningSurgeTotem { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Totems")]
        [DisplayName("Lightning Surge Totem AoE Count")]
        [Description("How many mobs must be attacking us before we consider using Lightning Surge Totem.")]
        public int LightningSurgeTotemCount { get; set; }

        #endregion

        #region Category: Artifact Weapon
        [Setting]
        [DefaultValue(UseDPSArtifactWeaponWhen.OnCooldown)]
        [Category("Artifact Weapon Usage")]
        [DisplayName("Use When...")]
        [Description("Toggle when the artifact weapon ability should be used.")]
        public UseDPSArtifactWeaponWhen UseDPSArtifactWeaponWhen { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Artifact Weapon Usage")]
        [DisplayName("Use Only In AoE")]
        [Description("If set to true, this will make the artifact waepon only be used when more than one mob is attacking us.")]
        public bool UseArtifactOnlyInAoE { get; set; }
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
        [DisplayName("Stone Bulwark Totem %")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int StoneBulwarkTotemPercent { get; set; }

        #endregion
    }

    internal class ShamanRestoHealSettings : Singular.Settings.HealerSettings
    {
        private ShamanRestoHealSettings()
            : base("", HealingContext.None)
        {
        }

        public ShamanRestoHealSettings(HealingContext ctx)
            : base("ShamanResto", ctx)
        {

            // we haven't created a settings file yet,
            //  ..  so initialize values for various heal contexts

            if (!SavedToFile)
            {
                if (ctx == Singular.HealingContext.Battlegrounds)
                {
                    ChainHeal = 90;
                    HealingRain = 93;
					GiftoftheQueen = 90;
                    HealingWave = 0;
                    Ascendance = 49;
                    SpiritLinkTotem = 50;
                    HealingSurge = 85;
                    AncestralSwiftness = 35;
                    HealingStreamTotem = 100;
                    CloudburstTotem = 100;
                    HealingTideTotem = 55;

                    RollRiptideCount = 0;
                    MinHealingRainCount = 4;
					MinGiftoftheQueenCount = 2;
                    MinChainHealCount = 3;
                    MinHealingTideCount = 2;
                }
                else if (ctx == Singular.HealingContext.Instances)
                {
                    ChainHeal = 92;
                    HealingRain = 91;
					GiftoftheQueen = 90;
                    HealingWave = 90;
                    Ascendance = 50;
                    SpiritLinkTotem = 49;
                    HealingSurge = 70;
                    AncestralSwiftness = 30;
                    HealingStreamTotem = 100;
                    CloudburstTotem = 100;
                    HealingTideTotem = 65;

                    RollRiptideCount = 1;
                    MinHealingRainCount = 2;
					MinGiftoftheQueenCount = 2;
                    MinChainHealCount = 3;
                    MinHealingTideCount = 2;
                }
                else if (ctx == Singular.HealingContext.Raids)
                {
                    ChainHeal = 90;
                    HealingRain = 95;
					GiftoftheQueen = 90;
                    HealingWave = 50;
                    Ascendance = 50;
                    SpiritLinkTotem = 48;
                    HealingSurge = 21;
                    AncestralSwiftness = 20;
                    HealingStreamTotem = 100;
                    CloudburstTotem = 100;
                    HealingTideTotem = 70;

                    RollRiptideCount = 5;
                    MinHealingRainCount = 3;
					MinGiftoftheQueenCount = 5;
                    MinChainHealCount = 2;
                    MinHealingTideCount = 4;
                }
                // omit case for WoWContext.Normal and let it use DefaultValue() values
            }

            // adjust Healing Surge if we have not previously
            if (!HealingSurgeAdjusted && StyxWoW.Me.Level >= 60 && (ctx == HealingContext.Instances || ctx == HealingContext.Raids))
            {
                if (SavedToFile)
                    Logger.Write(LogColor.Hilite, "Healing Surge % changed from {0} to {1} for {2}.  Visit Class Config and Save to make permanent.", HealingSurge, AncestralSwiftness + 1, ctx.ToString());

                HealingSurge = AncestralSwiftness + 1;
                HealingSurgeAdjusted = true;
            }

            SavedToFile = true;
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
        [DefaultValue(92)]
        [Category("Restoration")]
        [DisplayName("% Chain Heal")]
        [Description("Health % to cast this ability at. Must heal Min 2 people in party, 3 in a raid. Set to 0 to disable.")]
        public int ChainHeal { get; set; }

        [Setting]
        [DefaultValue(91)]
        [Category("Restoration")]
        [DisplayName("% Healing Rain")]
        [Description("Health % to cast this ability at. Must heal Min of 3 people in party, 4 in a raid. Set to 0 to disable.")]
        public int HealingRain { get; set; }
		
		[Setting]
        [DefaultValue(90)]
        [Category("Restoration")]
        [DisplayName("% Gift of the Queen")]
        [Description("Health % to cast this ability at. Must heal Min of 3 people in party, 4 in a raid. Set to 0 to disable.")]
        public int GiftoftheQueen { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("% Healing Wave")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingWave { get; set; }

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
        [Description("Health % to cast Oh Shoot Heal (Ancestral Swiftness + Healing Wave).  Disabled if set to 0, on cooldown, or talent not selected.")]
        public int AncestralSwiftness { get; set; }

        [Setting]
        [DefaultValue(48)]
        [Category("Restoration")]
        [DisplayName("% Spirit Link Totem")]
        [Description("Health % to cast this ability at.  Only valid in a group. Set to 0 to disable.")]
        public int SpiritLinkTotem { get; set; }

        [Setting]
        [DefaultValue(100)]
        [Category("Restoration")]
        [DisplayName("% Healing Stream Totem")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingStreamTotem { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Talents")]
        [DisplayName("Healing Tide Totem %")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingTideTotem { get; set; }

        [Setting]
        [DefaultValue(100)]
        [Category("Talents")]
        [DisplayName("Cloudburst Totem %")]
        [Description("Health % to cast this ability at. Set to 100 to cast on cooldown, Set to 0 to disable.")]
        public int CloudburstTotem { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Restoration")]
        [DisplayName("Roll Riptide Max Count")]
        [Description("Max number of players to roll Riptide on (always Roll on tanks, and tanks are included in count)")]
        public int RollRiptideCount { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Restoration")]
        [DisplayName("Healing Rain Min Count")]
        [Description("Min number of players below Healing Rain % in area")]
        public int MinHealingRainCount { get; set; }
		
		[Setting]
        [DefaultValue(3)]
        [Category("Restoration")]
        [DisplayName("Gift of the Queen Min Count")]
        [Description("Min number of players below Healing Rain % in area")]
        public int MinGiftoftheQueenCount { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Restoration")]
        [DisplayName("Chain Heal Min Count")]
        [Description("Min number of players healed")]
        public int MinChainHealCount { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Restoration")]
        [DisplayName("Healing Tide Min Count")]
        [Description("Min number of players healed")]
        public int MinHealingTideCount { get; set; }

        [Setting]
        [DefaultValue(1)]
        [Category("Restoration")]
        [DisplayName("Spirit Link Min Count")]
        [Description("Min number of players healed")]
        public int MinSpiritLinkCount { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Restoration")]
        [DisplayName("Ascendance Min Count")]
        [Description("Min number of players healed")]
        public int MinAscendanceCount { get; set; }

        [Setting]
        [DefaultValue(90)]
        [Category("Restoration")]
        [DisplayName("Telluric Cast Health %")]
        [Description("Group Health % we can cast a Lightning Bolt")]
        public int TelluricHealthCast { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Restoration")]
        [DisplayName("Telluric Cancel Health %")]
        [Description("Group Health % where we cancel a Lightning Bolt in progress")]
        public int TelluricHealthCancel { get; set; }

    }

    internal class ShamanOffHealSettings : Singular.Settings.HealerSettings
    {
        private ShamanOffHealSettings()
            : base("", HealingContext.None)
        {
        }

        public ShamanOffHealSettings(HealingContext ctx)
            : base("ShamanOffheal", ctx)
        {

            // we haven't created a settings file yet,
            //  ..  so initialize values for various heal contexts

            if (!SavedToFile)
            {
                if (ctx == Singular.HealingContext.Battlegrounds)
                {
                    HealingRain = 93;
					GiftoftheQueen = 90;
                    HealingSurge = 85;
                    AncestralSwiftness = 40;
                    HealingStreamTotem = 90;

                    MinHealingRainCount = 3;
					MinGiftoftheQueenCount = 2;
                    MinHealingTideCount = 2;
                }
                else // use group/companion healing
                {
                    HealingRain = 93;
					GiftoftheQueen = 90;
                    HealingSurge = 80;
                    AncestralSwiftness = 35;
                    HealingStreamTotem = 90;

                    MinHealingRainCount = 4;
					MinGiftoftheQueenCount = 2;
                    MinHealingTideCount = 2;
                }
            }

            SavedToFile = true;
        }

        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool SavedToFile { get; set; }

        [Setting]
        [DefaultValue(91)]
        [Category("OffHeal")]
        [DisplayName("% Healing Rain")]
        [Description("Health % to cast this ability at. Must heal Min of 3 people in party. Set to 0 to disable.")]
        public int HealingRain { get; set; }
		
		[Setting]
        [DefaultValue(90)]
        [Category("OffHeal")]
        [DisplayName("% Gift of the Queen")]
        [Description("Health % to cast this ability at. Must heal Min of 3 people in party, 4 in a raid. Set to 0 to disable.")]
        public int GiftoftheQueen { get; set; }

        [Setting]
        [DefaultValue(16)]
        [Category("OffHeal")]
        [DisplayName("% Healing Surge")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingSurge { get; set; }

        [Setting]
        [DefaultValue(15)]
        [Category("OffHeal")]
        [DisplayName("% Oh Shoot!")]
        [Description("Health % to cast Oh Shoot Heal (Ancestral Swiftness + Healing Surge).  Disabled if set to 0, on cooldown, or talent not selected.")]
        public int AncestralSwiftness { get; set; }

        [Setting]
        [DefaultValue(95)]
        [Category("OffHeal")]
        [DisplayName("% Healing Stream Totem")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingStreamTotem { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("OffHeal")]
        [DisplayName("Healing Rain Min Count")]
        [Description("Min number of players below Healing Rain % in area")]
        public int MinHealingRainCount { get; set; }
		
		[Setting]
        [DefaultValue(3)]
        [Category("OffHeal")]
        [DisplayName("Gift of the Queen Min Count")]
        [Description("Min number of players below Healing Rain % in area")]
        public int MinGiftoftheQueenCount { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("OffHeal")]
        [DisplayName("Healing Tide Min Count")]
        [Description("Min number of players healed")]
        public int MinHealingTideCount { get; set; }

    }

}