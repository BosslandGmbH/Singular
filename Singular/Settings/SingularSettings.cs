
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
using Singular.Helpers;
using Styx.CommonBot;
using Styx.WoWInternals;

namespace Singular.Settings
{
    enum AllowMovementType
    {
        None,
        ClassSpecificOnly,
        All,
        Auto       
    }

    enum CheckTargets
    {
        None = 0,
        Current,
        All
    }

    enum PurgeAuraFilter
    {
        None = 0,
        Whitelist,
        All
    }

    enum RelativePriority
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

    enum SelfRessurectStyle
    {
        None = 0,
        Enable,
        Auto
    }

    enum CombatRezTarget
    {
        None = 0,
        All = Tank | Healer | DPS,
        Tank = 1,
        Healer = 2,
        TankOrHealer = Tank | Healer,
        DPS = 4
    }

    internal class SingularSettings : Styx.Helpers.Settings
    {
        private static int entrycount = 0;
        private static SingularSettings _instance;

        public SingularSettings()
            : base(Path.Combine(CharacterSettingsPath, "SingularSettings.xml"))
        {
            entrycount++;
            if (entrycount != 1)
            {
                Logger.WriteFile("Settings: unexpected reentrant call in Settings: {0}", entrycount );
            }

            if (_instance != null)
            {
                if (_instance != this)
                {
                    Logger.WriteFile("Settings: unexpected singleton error");
                }
            }
            else
            {
                _instance = this;

                bool fileExists = ConfigVersion != null;
                Version current = SingularRoutine.GetSingularVersion();
                Version cfgver = null;

                // if version is null, set to current value
                if (ConfigVersion == null)
                    ConfigVersion = current.ToString();

                try
                {
                    cfgver = new Version(ConfigVersion);
                }
                catch
                {
                    cfgver = current;
                }

                if (cfgver < current)
                {
                    Logger.WriteFile("Settings: updating config file from verion {0}", ConfigVersion.ToString());
                    if (new Version("3.0.0.3080") < current)
                    {
                        Logger.WriteFile("Settings: applying {0} related changes", new Version("3.0.0.3080").ToString());
                        UseFrameLock = true;
                        DisableInQuestVehicle = false;
                    }

                    if (new Version("3.0.0.3173") < current)
                    {
                        Logger.WriteFile("Settings: applying {0} related changes", new Version("3.0.0.3173").ToString());
                        if ( MinHealth == 65)
                            MinHealth = 60;
                        if (MinMana == 65)
                            MinMana = 50;
                    }

                    ConfigVersion = current.ToString();
                    Logger.WriteFile("Settings: config file upgrade to {0} complete", ConfigVersion.ToString());

                    // now handle any calculated default values
                    if (!fileExists)
                    {
                        if (StyxWoW.Me.Class == WoWClass.DeathKnight)
                        {
                            MinHealth = 50;
                            Logger.WriteFile("Settings: applying Death Knight specific Default MinHealth = {0}", MinHealth);
                        }
                    }
                }
            }

            entrycount--;
        }

        public static string GlobalSettingsPath
        {
            get
            {
                return Path.Combine(Styx.Common.Utilities.AssemblyDirectory, "Settings");
            }
        }


        public static string CharacterSettingsPath
        {
            get
            {
                string settingsDirectory = Path.Combine(Styx.Common.Utilities.AssemblyDirectory, "Settings");
                return Path.Combine(Path.Combine(settingsDirectory, StyxWoW.Me.RealmName), StyxWoW.Me.Name);
            }
        }


        public static string SingularSettingsPath
        {
            get
            {
                string settingsDirectory = Path.Combine(Styx.Common.Utilities.AssemblyDirectory, "Settings");
                return Path.Combine(Path.Combine(Path.Combine(settingsDirectory, StyxWoW.Me.RealmName), StyxWoW.Me.Name), "Singular");
            }
        }

        public static void Initialize()
        {
            if (_instance == null)
                _instance = new SingularSettings();
        }

        public static SingularSettings Instance 
        { 
            get { return _instance ?? (_instance = new SingularSettings()); }
            set { _instance = value; }
        }

        public static bool IsTrinketUsageWanted(TrinketUsage usage)
        {
            return usage == SingularSettings.Instance.Trinket1Usage
                || usage == SingularSettings.Instance.Trinket2Usage
                || usage == SingularSettings.Instance.GloveUsage;
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
            if (StyxWoW.Me.Class == WoWClass.Shaman)        LogSettings("ShamanSettings", Shaman());
            if (StyxWoW.Me.Class == WoWClass.Warlock)       LogSettings("WarlockSettings", Warlock());
            if (StyxWoW.Me.Class == WoWClass.Warrior)       LogSettings("WarriorSettings", Warrior());

            if (StyxWoW.Me.Specialization == WoWSpec.ShamanRestoration)
            {
                LogSettings("Shaman.Heal.Battleground", Shaman().Battleground);
                LogSettings("Shaman.Heal.Instance", Shaman().Instance);
                LogSettings("Shaman.Heal.Raid", Shaman().Raid);
            }

            if (StyxWoW.Me.Specialization == WoWSpec.PriestHoly)
            {
                LogSettings("Priest.Holy.Heal.Battleground", Priest().HolyBattleground);
                LogSettings("Priest.Holy.Heal.Instance", Priest().HolyInstance);
                LogSettings("Priest.Holy.Heal.Raid", Priest().HolyRaid);
            }

            Logger.WriteFile("====== Evaluated/Dynamic Settings ======");
            Logger.WriteFile("  {0}: {1}", "Debug", SingularSettings.Debug.ToYN());
            Logger.WriteFile("  {0}: {1}", "DisableAllMovement", SingularSettings.Instance.DisableAllMovement.ToYN());
            Logger.WriteFile("  {0}: {1}", "DisableAllTargeting", SingularSettings.DisableAllTargeting.ToYN());
            Logger.WriteFile("  {0}: {1}", "TrivialHealth", Unit.TrivialHealth());
            Logger.WriteFile("  {0}: {1}", "NeedTankTargeting", TankManager.NeedTankTargeting);
            Logger.WriteFile("  {0}: {1}", "NeedHealTargeting", HealerManager.NeedHealTargeting);
            Logger.WriteFile("");

            if (DisableSpellsWithCooldown == 0)
                Logger.WriteFile("No spells blocked by DisableSpellsWithCooldown");
            else if (!Debug)
                Logger.WriteFile("Spells with cooldown more than {0} secs Blocked by DisableSpellsWithCooldown", DisableSpellsWithCooldown);
            else
            {
                int maxcd = DisableSpellsWithCooldown * 1000;
                Logger.WriteFile("====== Spells Blocked by DisableSpellsWithCooldown  ======");

                using (StyxWoW.Memory.AcquireFrame())
                {
                    foreach (WoWSpell spell in SpellManager.Spells.OrderBy(s => s.Key).Select(s => s.Value))
                    {
                        int cd = Spell.GetBaseCooldown(spell);
                        if (cd >= maxcd)
                        {
                            Logger.WriteFile("  {0} {1}", (cd / 1000).ToString().AlignRight(4), spell.Name);
                        }
                    }
                }
                Logger.WriteFile(" ");
            }
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

                return SingularRoutine.IsManualMovementBotActive;
            }
        }

        [Browsable(false)]
        public static bool Debug
        {
            get
            {
                return GlobalSettings.Instance.LogLevel >= Styx.Common.LogLevel.Verbose && !Instance.DisableDebugLogging;
            }
        }

        [Browsable(false)]
        public static bool DebugSpellCanCast
        {
            get
            {
                return GlobalSettings.Instance.LogLevel >= Styx.Common.LogLevel.Diagnostic && !Instance.DisableDebugLogging;
            }
        }

        [Browsable(false)]
        public static bool Trace
        {
            get
            {
                return Debug && SingularSettings.Instance.EnableDebugTrace;
            }
        }

        [Browsable(false)]
        public static bool DisableAllTargeting
        {
            get
            {
                if (SingularSettings.Instance.TypeOfTargeting != TargetingStyle.Auto)
                    return SingularSettings.Instance.TypeOfTargeting == TargetingStyle.None;

                return SingularRoutine.IsManualMovementBotActive || SingularRoutine.IsDungeonBuddyActive;
            }
        }

        #region Category: Hidden

        [Browsable(false)]
        [Setting]
        public string ConfigVersion { get; set; }
            
        #endregion 

        #region Category: Debug

        [Browsable(false)]
        [Setting]
        [DefaultValue(false)]
        [Category("Debug")]
        [DisplayName("Debug Logging GCD")]
        [Description("Enables logging of GCD/Casting in Singular. Debug Logging setting must also be true")]
        public bool EnableDebugLoggingGCD { get; set; }

        [Browsable(false)]
        [Setting]
        [DefaultValue(false)]
        [Category("Debug")]
        [DisplayName("Debug Trace")]
        [Description("EXTREMELY VERBOSE!! Enables logging of entry/exit into each behavior. Only use if instructed or you prefer slower response times!")]
        public bool EnableDebugTrace { get; set; }

        [Browsable(false)]
        [Setting]
        [DefaultValue(false)]
        [Category("Debug")]
        [DisplayName("Disable Debug Logging")]
        [Description("Disables debug logging by Singular. Will suppress debug output even when enabled by other means")]
        public bool DisableDebugLogging { get; set; }

        #endregion

        #region Window Layout

        [Browsable(false)]
        [Setting]
        [DefaultValue(337)]
        public int FormHeight { get; set; }

        [Browsable(false)]
        [Setting]
        [DefaultValue(378)]
        public int FormWidth { get; set; }

        [Browsable(false)]
        [Setting]
        [DefaultValue(0)]
        public int FormTabIndex { get; set; }


        #endregion

        #region Category: Movement

        [Setting]
        [DefaultValue(AllowMovementType.Auto)]
        [Category("Movement")]
        [DisplayName("Allow Movement")]
        [Description("Controls movement allowed within the CC. None: prevent all movement; ClassSpecificOnly: only Charge/HeroicThrow/Blink/Disengage/etc; All: all movement allowed; Auto: same as None if LazyRaider used, otherwise same as All")]
        public AllowMovementType AllowMovement { get; set; }

        [Setting]
        [DefaultValue(12)]
        [Category("Movement")]
        [DisplayName("Melee Dismount Range")]
        [Description("Distance from target that melee should dismount")]
        public int MeleeDismountRange { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Movement")]
        [DisplayName("Use Cast While Moving Buffs")]
        [Description("True: attempting to use a non-instant while moving will first cast Spiritwalker's Grace, Ice Floes, Kil'Jaedan's Cunning, etc.")]
        public bool UseCastWhileMovingBuffs { get; set; }

        #endregion 

        #region Category: Consumables

        [Setting]
        [DefaultValue(60)]
        [Category("Consumables")]
        [DisplayName("Eat at Health %")]
        [Description("Minimum health to eat at.")]
        public int MinHealth { get; set; }

        [Setting]
        [DefaultValue(50)]
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

        #region Category: Avoidance

        [Setting]
        [DefaultValue(true)]
        [Category("Avoidance")]
        [DisplayName("Disengage Allowed")]
        [Description("Allow use of Disengage, Blink, Rocket Jump, Balance-Wild Charge, or equivalent spell to quickly jump away")]
        public bool DisengageAllowed { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Avoidance")]
        [DisplayName("Disengage at Health %")]
        [Description("Disengage (or equiv) if health below this % and mob in melee range")]
        public int DisengageHealth { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Avoidance")]
        [DisplayName("Disengage at mob count")]
        [Description("Disengage (or equiv) if this many mobs in melee range")]
        public int DisengageMobCount { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Avoidance")]
        [DisplayName("Kiting Allowed")]
        [Description("Allow kiting of mobs.")]
        public bool KiteAllow { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Avoidance")]
        [DisplayName("Kite below Health %")]
        [Description("Kite if health below this % and mob in melee range")]
        public int KiteHealth { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Avoidance")]
        [DisplayName("Kite at mob count")]
        [Description("Kite if this many mobs in melee range")]
        public int KiteMobCount { get; set; }

        [Setting]
        [DefaultValue(8)]
        [Category("Avoidance")]
        [DisplayName("Avoid Distance")]
        [Description("Only mobs within this distance that are attacking you count towards Disengage/Kite mob counts")]
        public int AvoidDistance { get; set; }

        [Browsable(false)]
        [Setting]
        [DefaultValue(false)]
        [Category("Avoidance")]
        [DisplayName("Jump Turn while Kiting")]
        [Description("Perform jump turn attack while kiting (only supported by Hunter presently)")]
        public bool JumpTurnAllow { get; set; }

        #endregion

        #region Category: General

        [Setting]
        [DefaultValue(true)]
        [Category("General")]
        [DisplayName("Use Framelock in Singular")]
        [Description("Force use of Framelock in Singular.  Primarily for use with Botbases that do not support Framelock")]
        public bool UseFrameLock { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("General")]
        [DisplayName("Wait For Res Sickness")]
        [Description("Wait for resurrection sickness to wear off.")]
        public bool WaitForResSickness { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("General")]
        [DisplayName("Disable Non Combat Behaviors")]
        [Description("Enabling that will disable non combat behaviors. (Rest, PreCombat buffs)")]
        public bool DisableNonCombatBehaviors { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("General")]
        [DisplayName("Disable in Quest Vehicle")]
        [Description("True: Singular ignore calls from Questing Bot if in a Quest Vehicle; False: Singular tries to fight/heal when Questing Bot asks it to.")]
        public bool DisableInQuestVehicle { get; set; }

        [Setting]
        [DefaultValue(0)]
        [Category("General")]
        [DisplayName("Disable Spells with Cooldown (secs)")]
        [Description("Prevent Singular from casting any spell with this cooldown or greater; set to 0 to allow Singular to cast all spells")]
        public int DisableSpellsWithCooldown { get; set; }

        [Setting]
        [DefaultValue(SelfRessurectStyle.Auto )]
        [Category("General")]
        [DisplayName("Self-Ressurect")]
        [Description("Auto: Self-Ressurect (Ankh/Soulstone) unless Movement is disabled, Enable: Always Self-Ressurect if available, None: never Self-Ressurect")]
        public SelfRessurectStyle SelfRessurect { get; set; }

        #endregion

        #region Category: Pets

        [Setting]
        [DefaultValue(false)]
        [Category("Pets")]
        [DisplayName("Disable Pet usage")]
        [Description("Enabling that will disable pet usage")]
        public bool DisablePetUsage { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Pets")]
        [DisplayName("Solo: Pet will Tanks Adds")]
        [Description("True: when Solo, switch Pet target to pickup those attacking player")]
        public bool PetTankAdds { get; set; }

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
        [DefaultValue(25)]
        [Category("Group Healing/Support")]
        [DisplayName("Stay Near Tank Range")]
        [Description("Max distance from Tank before we move towards it (max value: 100)")]
        public int StayNearTankRange { get; set; }

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
        [DefaultValue(RelativePriority.LowPriority)]
        [Category("Group Healing/Support")]
        [DisplayName("Dispel Debufs")]
        [Description("Dispel harmful debuffs")]
        public RelativePriority DispelDebuffs { get; set; }

        [Setting]
        [DefaultValue(CombatRezTarget.TankOrHealer)]
        [Category("Group Healing/Support")]
        [DisplayName("Combat Rez Target")]
        [Description("None: disable Combat Rez; other setting limits rez to target with that role set")]
        public CombatRezTarget CombatRezTarget { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Group Healing/Support")]
        [DisplayName("Combat Rez Delay")]
        [Description("Wait (seconds) before casting Battle Rez on group member")]
        public int CombatRezDelay { get; set; }

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

        [Setting]
        [DefaultValue(TrinketUsage.OnCooldownInCombat)]
        [Category("Items")]
        [DisplayName("Glove Enchant Usage")]
        public TrinketUsage GloveUsage { get; set; }

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

        #endregion

        #region Category: Enemy Control

        [Setting]
        [DefaultValue(CheckTargets.Current)]
        [Category("Enemy Control")]
        [DisplayName("Purge Targets")]
        [Description("None: disabled, Current: our target only, All: enemies in range we are facing")]
        public CheckTargets PurgeTargets { get; set; }

        [Setting]
        [DefaultValue(PurgeAuraFilter.Whitelist)]
        [Category("Enemy Control")]
        [DisplayName("Purge Buffs")]
        [Description("False: disabled, Current: our target only, Other: enemies in range we are facing")]
        public PurgeAuraFilter PurgeBuffs { get; set; }

        [Setting]
        [DefaultValue(CheckTargets.All)]
        [Category("Enemy Control")]
        [DisplayName("Interrupt Targets")]
        [Description("None: disabled, Current: our target only, All: any enemy in range.")]
        public CheckTargets InterruptTarget { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Enemy Control")]
        [DisplayName("Trivial Mob Max Health %")]
        [Description("Mob with Max Health less than % of your Max Health considered trivial")]
        public int TrivialMaxHealthPcnt { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Enemy Control")]
        [DisplayName("Use AOE Attacks")]
        [Description("True: use multi-target damage spells when necessary; False: single target spells on current target only")]
        public bool AllowAOE { get; set; }

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