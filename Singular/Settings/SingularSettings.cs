
using System.ComponentModel;
using System.IO;
using Styx;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    enum AllowMovementType
    {
        None,
        ClassSpecificOnly,
        All,
        Auto
        
    }

    internal class SingularSettings : Styx.Helpers.Settings
    {
        private static SingularSettings _instance;

        public SingularSettings()
            : base(Path.Combine(CharacterSettingsPath, "SingularSettings.xml"))
        {
        }

        private static string CharacterSettingsPath
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

        public static SingularSettings Instance { get { return _instance ?? (_instance = new SingularSettings()); } }

        public static bool IsTrinketUsageWanted(TrinketUsage usage)
        {
            return usage == SingularSettings.Instance.Trinket1Usage
                || usage == SingularSettings.Instance.Trinket2Usage;
        }

        // this is the old setting.  kept for compatability until removed from code
        [Browsable(false)]
        public bool DisableAllMovement
        {
            get
            {
                if (AllowMovement != AllowMovementType.Auto)
                    return AllowMovement == AllowMovementType.None;

                return SingularRoutine.IsBotInUse("LazyRaider");
            }
        }

        public bool IsCombatRoutineMovementAllowed()
        {
            if (AllowMovement != AllowMovementType.Auto)
                return AllowMovement >= AllowMovementType.ClassSpecificOnly;

            return !SingularRoutine.IsBotInUse("LazyRaider");
        }

        [Browsable(false)]
        public static bool Debug
        {
            get
            {
                return SingularSettings.Instance.EnableDebugLogging || (GlobalSettings.Instance.LogLevel > Styx.Common.LogLevel.Normal);
            }
        }


        #region Category: General

        [Setting]
        [DefaultValue(AllowMovementType.Auto)]
        [Category("Movement")]
        [DisplayName("Allow Movement")]
        [Description("Controls movement allowed within the CC. None: prevent all movement; ClassSpecificOnly: only Charge/HeroicThrow/Blink/Disengage/etc; All: all movement allowed; Auto: same as None if LazyRaider used, otherwise same as All")]
        public AllowMovementType AllowMovement { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("General")]
        [DisplayName("Use Instance Rotation (Needs a restart !)")]
        [Description("When this is set to true, Singular will always use Instance rotations no matter what the current Context is.")]
        public bool UseInstanceRotation { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Targeting")]
        [DisplayName("Disable Targeting")]
        [Description("Disable all Targeting within the CC. This will NOT stop it from casting spells/heals on units other than your target. Only changing actual targets will be disabled.")]
        public bool DisableAllTargeting { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("General")]
        [DisplayName("Wait For Res Sickness")]
        [Description("Wait for resurrection sickness to wear off.")]
        public bool WaitForResSickness { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("General")]
        [DisplayName("Min Health")]
        [Description("Minimum health to eat at.")]
        public int MinHealth { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("General")]
        [DisplayName("Min Mana")]
        [Description("Minimum mana to drink at.")]
        public int MinMana { get; set; }
        [Setting]
        [DefaultValue(30)]
        [Category("General")]
        [DisplayName("Potion Health")]
        [Description("Health % to use a health pot/trinket/stone at.")]
        public int PotionHealth { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("General")]
        [DisplayName("Potion Mana")]
        [Description("Mana % to use a mana pot/trinket at. (used for all energy forms)")]
        public int PotionMana { get; set; }

        #endregion

        #region Category: Misc

        [Setting]
        [DefaultValue(false)]
        [Category("Misc")]
        [DisplayName("Debug Logging")]
        [Description("Enables debug logging from Singular. This will cause quite a bit of spam. Use it for diagnostics only.")]
        public bool EnableDebugLogging { get; set; }

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

        #region Category: Healing

        [Setting]
        [DefaultValue(95)]
        [Category("Healing")]
        [DisplayName("Ignore Targets Health")]
        [Description("Ignore healing targets when their health is above this value.")]
        public int IgnoreHealTargetsAboveHealth { get; set; }

        [Setting]
        [DefaultValue(75)]
        [Category("Healing")]
        [DisplayName("Max Heal Target Range")]
        [Description("Max distance that we will see a heal target (max value: 100)")]
        public int MaxHealTargetRange { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Healing")]
        [DisplayName("Use Bandages")]
        [Description("Use bandages in inventory to heal")]
        public bool UseBandages { get; set; }

        #endregion

        #region Category: Items

        [Setting]
        [DefaultValue(true)]
        [Category("Items")]
        [DisplayName("Use Flasks")]
        [Description("Uses Flask of the North or Flask of Enhancement.")]
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
        [DisplayName("Disable Targeting")]
        public bool DisableTankTargetSwitching { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Tanking")]
        [DisplayName("Enable Taunting for tanks")]
        public bool EnableTaunting { get; set; }

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

        [Browsable(false)]
        public DeathKnightSettings DeathKnight { get { return _dkSettings ?? (_dkSettings = new DeathKnightSettings()); } }

        [Browsable(false)]
        public DruidSettings Druid { get { return _druidSettings ?? (_druidSettings = new DruidSettings()); } }

        [Browsable(false)]
        public HunterSettings Hunter { get { return _hunterSettings ?? (_hunterSettings = new HunterSettings()); } }

        [Browsable(false)]
        public MageSettings Mage { get { return _mageSettings ?? (_mageSettings = new MageSettings()); } }
		
		[Browsable(false)]
        public MonkSettings Monk { get { return _monkSettings ?? (_monkSettings = new MonkSettings()); } }
		
        [Browsable(false)]
        public PaladinSettings Paladin { get { return _pallySettings ?? (_pallySettings = new PaladinSettings()); } }

        [Browsable(false)]
        public PriestSettings Priest { get { return _priestSettings ?? (_priestSettings = new PriestSettings()); } }

        [Browsable(false)]
        public RogueSettings Rogue { get { return _rogueSettings ?? (_rogueSettings = new RogueSettings()); } }

        [Browsable(false)]
        public ShamanSettings Shaman { get { return _shamanSettings ?? (_shamanSettings = new ShamanSettings()); } }

        [Browsable(false)]
        public WarlockSettings Warlock { get { return _warlockSettings ?? (_warlockSettings = new WarlockSettings()); } }

        [Browsable(false)]
        public WarriorSettings Warrior { get { return _warriorSettings ?? (_warriorSettings = new WarriorSettings()); } }

        #endregion
    }
}