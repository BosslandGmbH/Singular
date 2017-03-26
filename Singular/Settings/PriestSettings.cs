
using System;
using System.ComponentModel;
using System.IO;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using Styx;
using System.Drawing;

namespace Singular.Settings
{
    internal class PriestSettings : Styx.Helpers.Settings
    {
        public PriestSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "Priest.xml"))
        {
        }

        #region Holy Healing Context Late Loading Wrappers

        private HolyPriestHealSettings _holybattleground;
        private HolyPriestHealSettings _holyinstance;
        private HolyPriestHealSettings _holyraid;

        [Browsable(false)]
        public HolyPriestHealSettings HolyBattleground { get { return _holybattleground ?? (_holybattleground = new HolyPriestHealSettings(HealingContext.Battlegrounds)); } }

        [Browsable(false)]
        public HolyPriestHealSettings HolyInstance { get { return _holyinstance ?? (_holyinstance = new HolyPriestHealSettings(HealingContext.Instances)); } }

        [Browsable(false)]
        public HolyPriestHealSettings HolyRaid { get { return _holyraid ?? (_holyraid = new HolyPriestHealSettings(HealingContext.Raids)); } }

        [Browsable(false)]
        public HolyPriestHealSettings HolyHeal { get { return HolyHealLookup(Singular.SingularRoutine.CurrentWoWContext); } }

        public HolyPriestHealSettings HolyHealLookup(WoWContext ctx)
        {
            if (ctx == WoWContext.Instances)
                return StyxWoW.Me.CurrentMap.IsRaid ? HolyRaid : HolyInstance;
            return HolyBattleground;
        }

        #endregion


        #region Disc Healing Context Late Loading Wrappers

        private DiscPriestHealSettings _discbattleground;
        private DiscPriestHealSettings _discinstance;
        private DiscPriestHealSettings _discraid;

        [Browsable(false)]
        public DiscPriestHealSettings DiscBattleground { get { return _discbattleground ?? (_discbattleground = new DiscPriestHealSettings(HealingContext.Battlegrounds)); } }

        [Browsable(false)]
        public DiscPriestHealSettings DiscInstance { get { return _discinstance ?? (_discinstance = new DiscPriestHealSettings(HealingContext.Instances)); } }

        [Browsable(false)]
        public DiscPriestHealSettings DiscRaid { get { return _discraid ?? (_discraid = new DiscPriestHealSettings(HealingContext.Raids)); } }

        [Browsable(false)]
        public DiscPriestHealSettings DiscHeal { get { return DiscHealLookup(Singular.SingularRoutine.CurrentWoWContext); } }

        public DiscPriestHealSettings DiscHealLookup(WoWContext ctx)
        {
            if (ctx == WoWContext.Instances)
                return StyxWoW.Me.CurrentMap.IsRaid ? DiscRaid : DiscInstance;
            return DiscBattleground;
        }

        #endregion

        #region Shadow

        [Setting]
        [DefaultValue(30)]
        [Category("Shadow")]
        [DisplayName("Shadow Mend %")]
        [Description("Health % for Shadow Mend for shadow spec.  Used only if all attackers within 40 yds are crowd controlled")]
        public int ShadowHeal { get; set; }

        [Setting]
        [DefaultValue(0)]
        [Category("Shadow")]
        [DisplayName("Prayer of Mending %")]
        [Description("Health % for Prayer of Mending for shadow spec.  Used only if all attackers within 40 yds are crowd controlled")]
        public int PrayerOfMending { get; set; }

        [Setting]
        [DefaultValue(25)]
        [Category("Shadow")]
        [DisplayName("Mind Flay Mana")]
        [Description("Will only use Mind Flay while manapercent is above this value")]
        public int MindFlayManaPct { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Shadow")]
        [DisplayName("Vampiric Embrace Health")]
        [Description("Health % for Vampiric Embrace")]
        public int VampiricEmbracePct { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Shadow")]
        [DisplayName("Vampiric Embrace Mob Count")]
        [Description("Count of players for Vampiric Embrace in Instances (Solo and Dungeon checks only characters health")]
        public int VampiricEmbraceCount { get; set; }


/*
        [Setting]
        [DefaultValue(15)]
        [Category("Shadow")]
        [DisplayName("Mind Blast Timer")]
        [Description("Casts mind blast anyway after this many seconds if we haven't got 3 shadow orbs")]
        public int MindBlastTimer { get; set; }
*/
/*
        [Setting]
        [DefaultValue(false)]
        [Category("Shadow")]
        [DisplayName("Devouring Plague First")]
        [Description("Casts devouring plague before anything else, useful for farming low hp mobs")]
        public bool DevouringPlagueFirst { get; set; }
/*
        [Setting]
        [DefaultValue(false)]
        [Category("Shadow")]
        [DisplayName("Archangel on 5")]
        [Description("Always archangel on 5 dark evangelism, ignoring mana %")]
        public bool AlwaysArchangel5 { get; set; }
*/

        [Setting]
        [DefaultValue(20)]
        [Category("Shadow")]
        [DisplayName("Dispersion Mana")]
        [Description("Dispersion will be used when mana percentage is less than this value")]
        public int DispersionMana { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Shadow")]
        [DisplayName("Healing Spells Health")]
        [Description("Won't attempt to use healing spells unless below this health percent")]
        public int DontHealPercent { get; set; }

        #endregion

        #region Common

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Shield Pre-Pull")]
        [Description("Use PW:Shield pre-pull.")]
        public bool UseShieldPrePull { get; set; }

        [Setting]
        [DefaultValue(90)]
        [Category("Common")]
        [DisplayName("Shield Health Percent")]
        [Description("Use PW:Shield below this %")]
        public int PowerWordShield { get; set; }

        [Setting]
        [DefaultValue(00)]
        [Category("Healing")]
        [DisplayName("Count Mass Dispel")]
        [Description("Min number of players dispelled (0: disable Mass Dispel)")]
        public int CountMassDispel { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Fade")]
        [Description("True: cast Fade on aggro -or- snare if Phantasm talent taken")]
        public bool UseFade { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Levitate")]
        [Description("True: cast Levitate when falling")]
        public static bool UseLevitate { get; set; }

        /*
                                [Setting]
                                [DefaultValue(true)]
                                [Category("Common")]
                                [DisplayName("Use Shadow Protection")]
                                [Description("Use Shadow Protection buff")]
                                public bool UseShadowProtection { get; set; }
                        */
        [Setting]
        [DefaultValue(50)]
        [Category("Common")]
        [DisplayName("Crowd Control: Health %")]
        [Description("If health falls below this, use Shackle Undead / Psychic Horror")]
        public int CrowdControlHealth { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Psychic Scream: Allow")]
        [Description("")]
        public bool PsychicScreamAllow { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Common")]
        [DisplayName("Psychic Scream: Count")]
        [Description("Use Psychic Scream when fighting this # of mobs; if Glyphed value is 2")]
        public int PsychicScreamAddCount { get; set; }

        [Setting]
        [DefaultValue(25)]
        [Category("Common")]
        [DisplayName("Psychic Scream: Health %")]
        [Description("Use Psychic Scream as a hail mary to get some heals if health falls below")]
        public int PsychicScreamHealth { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Fear Ward")]
        [Description("Use Fear Ward buff")]
        public bool UseFearWard { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Allow Kiting")]
        [Description("Allow Kiting mob to minimize damage taken")]
        public bool AllowKiting { get; set; }

/*
                [Setting]
                [DefaultValue(75)]
                [Category("Common")]
                [DisplayName("Archangel Mana")]
                [Description("Archangel will be used at this value")]
                public int ArchangelMana { get; set; }
        */

        [Setting]
        [DefaultValue(75)]
        [Category("Common")]
        [DisplayName("Shadowfiend: Mana %")]
        [Description("% Mana to cast Shadowfiend or Mindbender (target must survive Time To Death setting)")]
        public int ShadowfiendMana { get; set; }

        [Setting]
        [DefaultValue(10)]
        [Category("Common")]
        [DisplayName("Shadowfiend: Time to Death (secs)")]
        [Description("Current target must be expected to survive longer than this (seconds)")]
        public int ShadowfiendTimeToDeath { get; set; }
        /*
                        [Setting]
                        [DefaultValue(50)]
                        [Category("Common")]
                        [DisplayName("Mindbender Mana")]
                        [Description("Mindbender will be used at this value")]
                        public int MindbenderMana { get; set; }
                */
        [Setting]
        [DefaultValue(30)]
        [Category("Common")]
        [DisplayName("Desperate Prayer Health %")]
        [Description("Desperate Prayer used at this Health %")]
        public int DesperatePrayerHealth { get; set; }


        //[Setting]
        //[DefaultValue(false)]
        //[Category("Common")]
        //[DisplayName("Use Wand")]
        //[Description("Uses wand if we're oom")]
        //public bool UseWand { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Speed Buff")]
        [Description("Cast Tier 2 movement buff when running and not in combat")]
        public bool UseSpeedBuff { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Speed Buff on Tank")]
        [Description("Cast Tier 2 movement buff on Tank when they are not in combat and moving")]
        public bool UseSpeedBuffOnTank { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Leap of Faith")]
        [Description("Leap of Faith on group members dying in bad stuff or near death from melee enemies")]
        public bool UseLeapOfFaith { get; set; }

        #endregion

        #region Discipline
        /*
                [Setting]
                [DefaultValue(80)]
                [Category("Discipline")]
                [DisplayName("Spirit Shell")]
                [Description("Spirit Shell will be used at this value along with Prayer of Healing")]
                public int SpiritShell { get; set; }

                [Setting]
                [DefaultValue(30)]
                [Category("Discipline")]
                [DisplayName("Penance Health")]
                [Description("Penance will be used at this value")]
                public int Penance { get; set; }

                [Setting]
                [DefaultValue(40)]
                [Category("Discipline")]
                [DisplayName("Flash Heal Health")]
                [Description("Flash Heal will be used at this value")]
                public int FlashHeal { get; set; }

                [Setting]
                [DefaultValue(50)]
                [Category("Discipline")]
                [DisplayName("Heal Health")]
                [Description("Heal will be used at this value")]
                public int Heal { get; set; }

                [Setting]
                [DefaultValue(30)]
                [Category("Discipline")]
                [DisplayName("Pain Suppression Health")]
                [Description("Pain Suppression will be used at this value")]
                public int PainSuppression { get; set; }

                [Setting]
                [DefaultValue(50)]
                [Category("Discipline")]
                [DisplayName("Prayer of Healing Health")]
                [Description("Prayer of Healing will be used at this value")]
                public int PrayerOfHealing { get; set; }

                [Setting]
                [DefaultValue(40)]
                [Category("Discipline")]
                [DisplayName("Power Word: Shield Health")]
                [Description("Power Word: Shield will be used at this value")]
                public int PowerWordShield { get; set; }

                [Setting]
                [DefaultValue(3)]
                [Category("Discipline")]
                [DisplayName("Prayer of Healing Count")]
                [Description("Prayer of Healing will be used when count of players whom health is below PoH Health setting mets this value")]
                public int PrayerOfHealingCount { get; set; }

                [Setting]
                [DefaultValue(70)]
                [Category("Discipline")]
                [DisplayName("Dps Mana")]
                [Description("Dps while mana is above this value (Used while in a party)")]
                public int DpsMana { get; set; }
        */
        #endregion

        #region Holy
        /*
                [Setting]
                [DefaultValue(95)]
                [Category("Holy")]
                [DisplayName("Heal Health")]
                [Description("Heal will be used at this value")]
                public int HolyHealPercent { get; set; }

                [Setting]
                [DefaultValue(50)]
                [Category("Holy")]
                [DisplayName("Heal Health")]
                [Description("Heal will be used at this value")]
                public int HolyHeal { get; set; }

                [Setting]
                [DefaultValue(25)]
                [Category("Holy")]
                [DisplayName("Flash Heal Health")]
                [Description("Flash Heal will be used at this value")]
                public int HolyFlashHeal { get; set; }

                [Setting]
                [DefaultValue(40)]
                [Category("Holy")]
                [DisplayName("Divine Hymn Health")]
                [Description("Divine Hymn will be used at this value")]
                public int DivineHymnHealth { get; set; }

                [Setting]
                [DefaultValue(6)]
                [Category("Holy")]
                [DisplayName("Divine Hymn Count")]
                [Description("Divine Hymn will be used when this many heal targets below the Divine Hymn Health percent")]
                public int DivineHymnCount { get; set; }

                [Setting]
                [DefaultValue(80)]
                [Category("Holy")]
                [DisplayName("Prayer of Healing with Serendipity Health")]
                [Description("Prayer of Healing with Serendipity will be used at this value")]
                public int PrayerOfHealingSerendipityHealth { get; set; }

                [Setting]
                [DefaultValue(4)]
                [Category("Holy")]
                [DisplayName("Prayer of Healing with Serendipity Count")]
                [Description("Prayer of Healing with Serendipity will be used when this many heal targets below the Prayer of Healing with Serendipity Health percent")]
                public int PrayerOfHealingSerendipityCount { get; set; }

                [Setting]
                [DefaultValue(95)]
                [Category("Holy")]
                [DisplayName("Circle of Healing Health")]
                [Description("Circle of Healing will be used at this value")]
                public int CircleOfHealingHealth { get; set; }

                [Setting]
                [DefaultValue(5)]
                [Category("Holy")]
                [DisplayName("Circle of Healing Count")]
                [Description("Circle of Healing will be used when this many heal targets below the Circle of Healing Health percent")]
                public int CircleOfHealingCount { get; set; }

        */


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

        #region Category: Artifact Weapon (Holy)

        [Setting]
        [DefaultValue(50)]
        [Category("Artifact Weapon Usage (Holy)")]
        [DisplayName("Light of T'uure %")]
        [Description("Health % to cast this ability at on tank. Set to 0 to disable.")]
        public int LightOfTuure { get; set; }

        #endregion

    }

    internal class HolyPriestHealSettings : Singular.Settings.HealerSettings
    {
        private HolyPriestHealSettings()
            : base("", HealingContext.None)
        {
        }

        public HolyPriestHealSettings(HealingContext ctx)
            : base("Priest-Holy", ctx)
        {

            // we haven't created a settings file yet,
            //  ..  so initialize values for various heal contexts

            if (!SavedToFile)
            {
                if (ctx == Singular.HealingContext.Battlegrounds)
                {
                    Renew = 95;
                    PrayerOfMending = 94;
                    Heal = 0;
                    FlashHeal = 80;
                    BindingHeal = 55;
                    HolyLevel90Talent = 85;
                    HolyWordSanctify = 95;
                    HolyWordSerenity = 50;
                    CircleOfHealing = 90;
                    PrayerOfHealing = 70;
                    DivineHymn = 75;
                    GuardianSpirit = 35;
                    CountHolyWordSanctify = 3;
                    CountLevel90Talent = 3;
                    CountCircleOfHealing = 3;
                    CountPrayerOfHealing = 3;
                    CountDivineHymn = 4;
                }
                else if (ctx == Singular.HealingContext.Instances)
                {
                    Renew = 95;
                    PrayerOfMending = 85;
                    Heal = 65;
                    FlashHeal = 80;
                    BindingHeal = 35;
                    HolyLevel90Talent = 80;
                    HolyWordSanctify = 0;
                    HolyWordSerenity = 60;
                    CircleOfHealing = 93;
                    PrayerOfHealing = 70;
                    DivineHymn = 75;
                    GuardianSpirit = 20;
                    CountHolyWordSanctify = 3;
                    CountLevel90Talent = 3;
                    CountCircleOfHealing = 3;
                    CountPrayerOfHealing = 3;
                    CountDivineHymn = 4;
                }
                else if (ctx == Singular.HealingContext.Raids)
                {
                    PowerWordShield = 100;
                    Renew = 95;
                    PrayerOfMending = 94;
                    Heal = 40;
                    FlashHeal = 70;
                    BindingHeal = 30;
                    HolyLevel90Talent = 80;
                    HolyWordSanctify = 92;
                    HolyWordSerenity = 50;
                    CircleOfHealing = 93;
                    PrayerOfHealing = 85;
                    DivineHymn = 75;
                    GuardianSpirit = 20;
                    CountHolyWordSanctify = 3;
                    CountLevel90Talent = 4;
                    CountCircleOfHealing = 4;
                    CountPrayerOfHealing = 3;
                    CountDivineHymn = 5;
                }
                // omit case for WoWContext.Normal and let it use DefaultValue() values
            }

            // adjust Flash Heal if we have not previously
            if (!FlashHealAdjusted && StyxWoW.Me.Level >= 34 && (ctx == HealingContext.Instances || ctx == HealingContext.Raids))
            {
                if (SavedToFile)
                    Logger.Write( LogColor.Hilite, "Flash Heal % changed from {0} to {1} for {2}.  Visit Class Config and Save to make permanent.", FlashHeal, GuardianSpirit + 1, ctx.ToString());

                FlashHeal = GuardianSpirit + 1;
                FlashHealAdjusted = true;
            }

            SavedToFile = true;
        }

        #region Saved Conversion State

        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool SavedToFile { get; set; }

        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool FlashHealAdjusted { get; set; }

        #endregion

        [Setting]
        [DefaultValue(100)]
        [Category("Health %")]
        [DisplayName("% Power Word: Shield")]
        [Description("Health % to cast this ability on Tanks. Set to 0 to disable.")]
        public int PowerWordShield { get; set; }

        [Setting]
        [DefaultValue(95)]
        [Category("Health %")]
        [DisplayName("% Renew")]
        [Description("Health % to cast this ability on Tanks. Set to 0 to disable.")]
        public int Renew { get; set; }

        [Setting]
        [DefaultValue(90)]
        [Category("Health %")]
        [DisplayName("% Prayer of Mending")]
        [Description("Health % to cast this ability on Tanks. Set to 0 to disable.")]
        public int PrayerOfMending { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Health %")]
        [DisplayName("% Heal")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int Heal { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Health %")]
        [DisplayName("% Flash Heal")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int FlashHeal { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Health %")]
        [DisplayName("% Binding Heal")]
        [Description("Health % to cast this ability at.Set to 0 to disable.")]
        public int BindingHeal { get; set; }

        [Setting]
        [DefaultValue(75)]
        [Category("Health %")]
        [DisplayName("% Level 90 Talent")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HolyLevel90Talent { get; set; }

        [Setting]
        [DefaultValue(92)]
        [Category("Health %")]
        [DisplayName("% Holy Word: Sanctify")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HolyWordSanctify { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Health %")]
        [DisplayName("% Holy Word: Serenity")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HolyWordSerenity { get; set; }

        [Setting]
        [DefaultValue(93)]
        [Category("Health %")]
        [DisplayName("% Circle of Healing")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int CircleOfHealing { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Health %")]
        [DisplayName("% Prayer of Healing")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int PrayerOfHealing { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Health %")]
        [DisplayName("% Divine Hymn")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int DivineHymn { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Health %")]
        [DisplayName("% Guardian Spirit")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int GuardianSpirit { get; set; }

        #region Counts

        [Setting]
        [DefaultValue(3)]
        [Category("Target Minimum")]
        [DisplayName("Count Holy Word: Sanctify")]
        [Description("Min number of players below Holy Word: Sanctify % in area")]
        public int CountHolyWordSanctify { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Target Minimum")]
        [DisplayName("Count Level 90 Talent")]
        [Description("Min number of players healed")]
        public int CountLevel90Talent { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Target Minimum")]
        [DisplayName("Count Circle of Healing")]
        [Description("Min number of players healed")]
        public int CountCircleOfHealing { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Target Minimum")]
        [DisplayName("Count Prayer of Healing")]
        [Description("Min number of players healed")]
        public int CountPrayerOfHealing { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Target Minimum")]
        [DisplayName("Count Divine Hymn")]
        [Description("Min number of players healed")]
        public int CountDivineHymn { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Target Minimum")]
        [DisplayName("Count Prayer of Mending")]
        [Description("Min number of players healed")]
        public int CountPrayerOfMending { get; set; }

        #endregion
    }

    internal class DiscPriestHealSettings : Singular.Settings.HealerSettings
    {
        private DiscPriestHealSettings()
            : base("", HealingContext.None)
        {
        }

        public DiscPriestHealSettings(HealingContext ctx)
            : base("Priest-Discipline", ctx)
        {

            // we haven't created a settings file yet,
            //  ..  so initialize values for various heal contexts

            if (!SavedToFile)
            {
                if (ctx == Singular.HealingContext.Battlegrounds)
                {
                    Penance = 50;
                    Plea = 80;
                    PrayerOfMending = 90;
                    Heal = 0;
                    FlashHeal = 80;
                    PowerWordRadiance = 90;
                    PainSuppression = 0;
                    DiscLevel90Talent = 85;
                    PowerWordBarrier = 40;
                    CountLevel90Talent = 3;
                    CountPowerWordRadiance = 3;
                    CountPrayerOfMending = 2;
                    CountPowerWordBarrier = 3;
                    AtonementAbovePercent = 90;
                    AtonementAboveCount = 1;
                }
                else if (ctx == Singular.HealingContext.Instances)
                {
                    Penance = 50;
                    Plea = 80;
                    PrayerOfMending = 90;
                    Heal = 60;
                    FlashHeal = 90;
                    PowerWordRadiance = 90;
                    PainSuppression = 0;
                    DiscLevel90Talent = 85;
                    PowerWordBarrier = 0;
                    CountLevel90Talent = 3;
                    CountPowerWordRadiance = 3;
                    CountPrayerOfMending = 2;
                    CountPowerWordBarrier = 3;
                    AtonementAbovePercent = 90;
                    AtonementAboveCount = 1;
                }
                else if (ctx == Singular.HealingContext.Raids)
                {
                    Penance = 50;
                    Plea = 80;
                    PrayerOfMending = 90;
                    Heal = 50;
                    FlashHeal = 70;
                    PowerWordRadiance = 89;
                    PainSuppression = 49;
                    DiscLevel90Talent = 89;
                    PowerWordBarrier = 80;
                    CountLevel90Talent = 3;
                    CountPowerWordRadiance = 3;
                    CountPrayerOfMending = 3;
                    CountPowerWordBarrier = 3;
                    AtonementAbovePercent = 90;
                    AtonementAboveCount = 1;
                }
                // omit case for WoWContext.Normal and let it use DefaultValue() values
            }

            SavedToFile = true;
        }

        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool SavedToFile { get; set; }

        [Setting]
        [DefaultValue(100)]
        [Category("Healing")]
        [DisplayName("% Power Word: Shield")]
        [Description("Health % to cast this ability. Set to 0 to disable.")]
        public int PowerWordShield { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Healing")]
        [DisplayName("% Penance")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int Penance { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Healing")]
        [DisplayName("% Plea")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int Plea { get; set; }

        [Setting]
        [DefaultValue(90)]
        [Category("Healing")]
        [DisplayName("% Prayer of Mending")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int PrayerOfMending { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Healing")]
        [DisplayName("% Heal")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int Heal { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Healing")]
        [DisplayName("% Flash Heal")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int FlashHeal { get; set; }

        [Setting]
        [DefaultValue(89)]
        [Category("Healing")]
        [DisplayName("% Power Word Radiance")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int PowerWordRadiance { get; set; }

        [Setting]
        [DefaultValue(89)]
        [Category("Healing")]
        [DisplayName("% Holy Nova")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HolyNova { get; set; }

        [Setting]
        [DefaultValue(0)]
        [Category("Healing")]
        [DisplayName("% Pain Suppression")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int PainSuppression { get; set; }

        [Setting]
        [DefaultValue(89)]
        [Category("Health %")]
        [DisplayName("% Level 90 Talent")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int DiscLevel90Talent { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Health %")]
        [DisplayName("% Power Word Barrier")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int PowerWordBarrier { get; set; }

        [Setting]
        [DefaultValue(75)]
        [Category("Health %")]
        [DisplayName("% Spirit Shell")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int SpiritShell { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Health %")]
        [DisplayName("% Shadow Mend")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int ShadowMend { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Target Minimum")]
        [DisplayName("Count Level 90 Talent")]
        [Description("Min number of players healed")]
        public int CountLevel90Talent { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Target Minimum")]
        [DisplayName("Count Power Word Radiance")]
        [Description("Min number of players healed")]
        public int CountPowerWordRadiance { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Target Minimum")]
        [DisplayName("Count Holy Nova")]
        [Description("Min number of players healed")]
        public int CountHolyNova { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Target Minimum")]
        [DisplayName("Count Prayer of Mending")]
        [Description("Min number of players healed")]
        public int CountPrayerOfMending { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Target Minimum")]
        [DisplayName("Count Power Word: Barrier")]
        [Description("Min number of players shielded")]
        public int CountPowerWordBarrier { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Target Minimum")]
        [DisplayName("Count Spirit Shell")]
        [Description("Min number of players below health %")]
        public int CountSpiritShell { get; set; }

        [Setting]
        [DefaultValue(90)]
        [Category("Discipline")]
        [DisplayName("Atonement Only Above %")]
        [Description("Only Atonement Healing done above this Health %")]
        public int AtonementAbovePercent { get; set; }

        [Setting]
        [DefaultValue(1)]
        [Category("Healing")]
        [DisplayName("Atonement Only Count")]
        [Description("Count of Heal Targets below Atonement Only % which allows other heal spells")]
        public int AtonementAboveCount { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Discipline")]
        [DisplayName("Atonement Cancel Health %")]
        [Description("Cancel Atonement Healing if any below this Health %")]
        public int AtonementCancelBelowHealthPercent { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Discipline")]
        [DisplayName("Atonement Cancel Mana %")]
        [Description("Cancel Atonement Healing if any below this Mana %")]
        public int AtonementCancelBelowManaPercent { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Discipline")]
        [DisplayName("Atonement Use Smite")]
        [Description("Use Smite as part of Atonement spell priority")]
        public bool AtonementUseSmite { get; set; }

    }
}