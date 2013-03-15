
using System.ComponentModel;
using System.IO;
using System.Linq;
using Styx;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using Singular.Managers;
using System.Reflection;
using System;
using System.Collections.Generic;

namespace Singular.Settings
{
    enum AllowMovementType
    {
        None,
        ClassSpecificOnly,
        All,
        Auto
        
    }

    enum InterruptType
    {
        None = 0,
        Target,
        All
    }

    enum DispelStyle
    {
        None = 0,
        LowPriority,
        HighPriority
    }

    enum TargetingStyle
    {
        None = 0,
        Enable,
        Auto
    }


    internal class SingularSettings : Styx.Helpers.Settings
    {
        private static SingularSettings _instance;

        public SingularSettings()
            : base(Path.Combine(CharacterSettingsPath, "SingularSettings.xml"))
        {
            CleanseBlacklist = new Dictionary<int,string>() {
                { 96328, "Toxic Torment (Green Cauldron)" },
                { 96325, "Frostburn Formula (Blue Cauldron)" },
                { 96326, "Burning Blood (Red Cauldron)" },
                { 92876, "Blackout (10man)" },
                { 92878, "Blackout (25man)" },
                { 30108, "(Warlock) Unstable Affliction" },
                { 8050,  "(Shaman) Flame Shock" },
                { 3600,  "(Shaman) Earthbind" },
                { 34914, "(Priest) Vampiric Touch" },
                { 104050, "Torrent of Frost" },
                { 103962, "Torrent of Frost" },
                { 103904, "Torrent of Frost" }
            };
        }

        public static string CharacterSettingsPath
        {
            get
            {
                string settingsDirectory = Path.Combine(Styx.Common.Utilities.AssemblyDirectory, "Settings");
                return Path.Combine(Path.Combine(settingsDirectory, StyxWoW.Me.RealmName), StyxWoW.Me.Name);
            }
        }


        public static string SettingsPath
        {
            get
            {
                string settingsDirectory = Path.Combine(Styx.Common.Utilities.AssemblyDirectory, "Settings");
                return Path.Combine(Path.Combine(Path.Combine(settingsDirectory, StyxWoW.Me.RealmName), StyxWoW.Me.Name), "Singular");
            }
        }

        public static SingularSettings Instance 
        { 
            get { return _instance ?? (_instance = new SingularSettings()); }
            set { _instance = value; }
        }

        public static bool IsTrinketUsageWanted(TrinketUsage usage)
        {
            return usage == SingularSettings.Instance.Trinket1Usage
                || usage == SingularSettings.Instance.Trinket2Usage;
        }

        /// <summary>
        /// Write all Singular Settings in effect to the Log file
        /// </summary>
        public void LogSettings()
        {
            Logger.WriteFile("");

            // reference the internal references so we can display only for our class
            LogSettings("Singular", SingularSettings.Instance);
            LogSettings("HotkeySettings", Hotkeys());
            if (StyxWoW.Me.Class == WoWClass.DeathKnight )  LogSettings("DeathKnightSettings", DeathKnight());
            if (StyxWoW.Me.Class == WoWClass.Druid )        LogSettings("DruidSettings", Druid());
            if (StyxWoW.Me.Class == WoWClass.Hunter )       LogSettings("HunterSettings", Hunter());
            if (StyxWoW.Me.Class == WoWClass.Mage )         LogSettings("MageSettings", Mage());
            if (StyxWoW.Me.Class == WoWClass.Monk )         LogSettings("MonkSettings", Monk());
            if (StyxWoW.Me.Class == WoWClass.Paladin )      LogSettings("PaladinSettings", Paladin());
            if (StyxWoW.Me.Class == WoWClass.Priest )       LogSettings("PriestSettings", Priest());
            if (StyxWoW.Me.Class == WoWClass.Rogue )        LogSettings("RogueSettings", Rogue());

            if (StyxWoW.Me.Class == WoWClass.Shaman)
            {
                LogSettings("ShamanSettings", Shaman());
                if (StyxWoW.Me.Specialization == WoWSpec.ShamanRestoration)
                {
                    LogSettings("Shaman.Heal.Battleground", Shaman().Battleground);
                    LogSettings("Shaman.Heal.Instance", Shaman().Instance);
                    LogSettings("Shaman.Heal.Raid", Shaman().Raid);
                }
            }
            
            if (StyxWoW.Me.Class == WoWClass.Warlock )      LogSettings("WarlockSettings", Warlock());
            if (StyxWoW.Me.Class == WoWClass.Warrior )      LogSettings("WarriorSettings", Warrior());
        }

        public void LogSettings(string desc, Styx.Helpers.Settings set)
        {
            if (set == null)
                return;

            Logger.WriteFile("====== {0} Settings ======", desc);
            foreach (var kvp in set.GetSettings())
            {
                Logger.WriteFile("  {0}: {1}", kvp.Key, kvp.Value.ToString());
            }

            Logger.WriteFile("");
        }

        /// <summary>
        /// Obsolete:  Almost all code should reference MovementManager.IsMovementDisabled
        /// .. which will handle Hotkey processing and any context sensitive bot behavior.
        /// .. This setting only retrieves the user setting which typically is insufficient.
        /// </summary>
        [Browsable(false)]
        public bool DisableAllMovement
        {
            get
            {
                if (AllowMovement != AllowMovementType.Auto)
                    return AllowMovement == AllowMovementType.None;

                return MovementManager.IsManualMovementBotActive;
            }
        }

        [Browsable(false)]
        public static bool Debug
        {
            get
            {
                return SingularSettings.Instance.EnableDebugLogging || (GlobalSettings.Instance.LogLevel > Styx.Common.LogLevel.Normal);
            }
        }

        [Browsable(false)]
        public static bool DisableAllTargeting
        {
            get
            {
                if (SingularSettings.Instance.TypeOfTargeting != TargetingStyle.Auto)
                    return SingularSettings.Instance.TypeOfTargeting == TargetingStyle.None;

                return MovementManager.IsManualMovementBotActive || SingularRoutine.IsDungeonBuddyActive;
            }
        }

        [Browsable(false)]
        internal static Dictionary<int, string> CleanseBlacklist = new Dictionary<int, string>();


        #region Category: Debug

        [Setting]
        [DefaultValue(false)]
        [Category("Debug")]
        [DisplayName("Debug Logging")]
        [Description("Enables debug logging from Singular. This will cause quite a bit of spam. Use it for diagnostics only.")]
        public bool EnableDebugLogging { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Debug")]
        [DisplayName("Debug Logging GCD")]
        [Description("Enables logging of GCD/Casting in Singular. Debug Logging setting must also be true")]
        public bool EnableDebugLoggingGCD { get; set; }

        #endregion

        #region Category: Movement

        [Setting]
        [DefaultValue(AllowMovementType.Auto)]
        [Category("Movement")]
        [DisplayName("Allow Movement")]
        [Description("Controls movement allowed within the CC. None: prevent all movement; ClassSpecificOnly: only Charge/HeroicThrow/Blink/Disengage/etc; All: all movement allowed; Auto: same as None if LazyRaider used, otherwise same as All")]
        public AllowMovementType AllowMovement { get; set; }

        [Setting]
        [DefaultValue(25)]
        [Category("Movement")]
        [DisplayName("Questing Ranged Pull Override")]
        [Description("Pull Distance we force to 40 for Ranged characters when using Questing BotBase")]
        public int PullDistanceOverride { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Movement")]
        [DisplayName("Use Cast While Moving Buffs")]
        [Description("True: attempting to use a non-instant while moving will first cast Spiritwalker's Grace, Ice Floes, Kil'Jaedan's Cunning, etc.")]
        public bool UseCastWhileMovingBuffs { get; set; }

        #endregion 

        #region Category: Consumables

        [Setting]
        [DefaultValue(65)]
        [Category("Consumables")]
        [DisplayName("Eat at Health %")]
        [Description("Minimum health to eat at.")]
        public int MinHealth { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("Consumables")]
        [DisplayName("Drink at Mana %")]
        [Description("Minimum mana to drink at.")]
        public int MinMana { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Consumables")]
        [DisplayName("Potion at Health %")]
        [Description("Health % to use a health pot/trinket/stone at.")]
        public int PotionHealth { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Consumables")]
        [DisplayName("Potion at Mana %")]
        [Description("Mana % to use a mana pot/trinket at. (used for all energy forms)")]
        public int PotionMana { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Consumables")]
        [DisplayName("Use Bandages")]
        [Description("Use bandages in inventory to heal")]
        public bool UseBandages { get; set; }

        #endregion

        #region Category: Misc

        [Setting]
        [DefaultValue(true)]
        [Category("General")]
        [DisplayName("Wait For Res Sickness")]
        [Description("Wait for resurrection sickness to wear off.")]
        public bool WaitForResSickness { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Misc")]
        [DisplayName("Disable Non Combat Behaviors")]
        [Description("Enabling that will disable non combat behaviors. (Rest, PreCombat buffs)")]
        public bool DisableNonCombatBehaviors { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Misc")]
        [DisplayName("Disable Pet usage")]
        [Description("Enabling that will disable pet usage")]
        public bool DisablePetUsage { get; set; }

        #endregion

        #region Category: Group Healing / Support

        [Setting]
        [DefaultValue(95)]
        [Category("Group Healing/Support")]
        [DisplayName("Ignore Targets Health")]
        [Description("Ignore healing targets with health % above; also cancels casts in progress at this value.")]
        public int IgnoreHealTargetsAboveHealth { get; set; }

        [Setting]
        [DefaultValue(75)]
        [Category("Group Healing/Support")]
        [DisplayName("Max Heal Target Range")]
        [Description("Max distance that we will see a heal target (max value: 100)")]
        public int MaxHealTargetRange { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Group Healing/Support")]
        [DisplayName("Stay near Tank")]
        [Description("Move within Healing Range of Tank if nobody needs healing")]
        public bool StayNearTank { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Group Healing/Support")]
        [DisplayName("Include Pets as Heal Targets")]
        [Description("True: Include Pets as Healing targets (does not affect Owner healing of Pet)")]
        public bool IncludePetsAsHealTargets { get; set; }

        [Setting]
        [DefaultValue(DispelStyle.HighPriority)]
        [Category("Group Healing/Support")]
        [DisplayName("Dispel Debufs")]
        [Description("Dispel harmful debuffs")]
        public DispelStyle DispelDebuffs { get; set; }

        #endregion

        #region Category: Healing

        #endregion

        #region Category: Items

        [Setting]
        [DefaultValue(true)]
        [Category("Items")]
        [DisplayName("Use Flasks")]
        [Description("Uses Alchemist Flasks (of the North, of Enhancement...)")]
        public bool UseAlchemyFlasks { get; set; }

        [Setting]
        [DefaultValue(TrinketUsage.Never)]
        [Category("Items")]
        [DisplayName("Trinket 1 Usage")]
        public TrinketUsage Trinket1Usage { get; set; }

        [Setting]
        [DefaultValue(TrinketUsage.Never)]
        [Category("Items")]
        [DisplayName("Trinket 2 Usage")]
        public TrinketUsage Trinket2Usage { get; set; }

        #endregion

        #region Category: Racials

        [Setting]
        [DefaultValue(true)]
        [Category("Racials")]
        [DisplayName("Use Racials")]
        public bool UseRacials { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Racials")]
        [DisplayName("Gift of the Naaru HP")]
        [Description("Uses Gift of the Naaru when HP falls below this %.")]
        public int GiftNaaruHP { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Racials")]
        [DisplayName("Shadowmeld Threat Drop")]
        [Description("When in a group (and not a tank), uses shadowmeld as a threat drop.")]
        public bool ShadowmeldThreatDrop { get; set; }
        
        #endregion

        #region Category: Tanking

        [Setting]
        [DefaultValue(false)]
        [Category("Tanking")]
        [DisplayName("Disable Target Switching")]
        [Description("True: stay on current target while Tanking.  False: switch targets based upon threat differential with group")]
        public bool DisableTankTargetSwitching { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Tanking")]
        [DisplayName("Enable Taunting for tanks")]
        public bool EnableTaunting { get; set; }

        #endregion

        #region Category: Targeting

        [Setting]
        [DefaultValue(TargetingStyle.Auto)]
        [Category("Targeting")]
        [DisplayName("Targeting by Singular")]
        [Description("None: disabled, Enable: intelligent switching; Auto: disable for DungeonBuddy/manual Assist Bots, otherwise intelligent switching.")]
        public TargetingStyle TypeOfTargeting { get; set; }

        [Setting]
        [DefaultValue(InterruptType.All)]
        [Category("Targeting")]
        [DisplayName("Interrupt Target")]
        [Description("Select which targets should we interrupt.")]
        public InterruptType InterruptTarget { get; set; }

        #endregion

        #region Category: World Behaviors
        /*
        [Setting]
        [DefaultValue(WoWContext.Instances)]
        [Category("World Behaviors")]
        [DisplayName("Use Group Behaviors (Needs a restart !)")]
        [Description("Behaviors when in Group outside of Instances/Battlegrounds")]
        public WoWContext WorldGroupBehaviors { get; set; }

        [Setting]
        [DefaultValue(WoWContext.Normal)]
        [Category("World Behaviors")]
        [DisplayName("Use Solo Behaviors (Needs a restart !)")]
        [Description("Behaviors when Solo outside of Instances/Battlegrounds")]
        public WoWContext WorldSoloBehaviors { get; set; }
*/
        #endregion

        #region Class Late-Loading Wrappers

        // Do not change anything within this region.
        // It's written so we ONLY load the settings we're going to use.
        // There's no reason to load the settings for every class, if we're only executing code for a Druid.

        private DeathKnightSettings _dkSettings;

        private DruidSettings _druidSettings;

        private HunterSettings _hunterSettings;

        private MageSettings _mageSettings;
		
		private MonkSettings _monkSettings;
		
        private PaladinSettings _pallySettings;

        private PriestSettings _priestSettings;

        private RogueSettings _rogueSettings;

        private ShamanSettings _shamanSettings;

        private WarlockSettings _warlockSettings;

        private WarriorSettings _warriorSettings;

        private HotkeySettings _hotkeySettings;

        // late-binding interfaces 
        // -- changed from readonly properties to methods as GetProperties() in SaveToXML() was causing all classes configs to load
        // -- this was causing Save to write a DeathKnight.xml file for all non-DKs for example
        internal DeathKnightSettings DeathKnight() { return _dkSettings ?? (_dkSettings = new DeathKnightSettings()); } 
        internal DruidSettings Druid() { return _druidSettings ?? (_druidSettings = new DruidSettings()); }
        internal HunterSettings Hunter() { return _hunterSettings ?? (_hunterSettings = new HunterSettings()); }
        internal MageSettings Mage() { return _mageSettings ?? (_mageSettings = new MageSettings()); }
        internal MonkSettings Monk() { return _monkSettings ?? (_monkSettings = new MonkSettings()); }
        internal PaladinSettings Paladin() { return _pallySettings ?? (_pallySettings = new PaladinSettings()); }
        internal PriestSettings Priest() { return _priestSettings ?? (_priestSettings = new PriestSettings()); }
        internal RogueSettings Rogue() { return _rogueSettings ?? (_rogueSettings = new RogueSettings()); }
        internal ShamanSettings Shaman() { return _shamanSettings ?? (_shamanSettings = new ShamanSettings()); }
        internal WarlockSettings Warlock() { return _warlockSettings ?? (_warlockSettings = new WarlockSettings()); }
        internal WarriorSettings Warrior() { return _warriorSettings ?? (_warriorSettings = new WarriorSettings()); }
        internal HotkeySettings Hotkeys() { return _hotkeySettings ?? (_hotkeySettings = new HotkeySettings()); }

        #endregion
    }

}