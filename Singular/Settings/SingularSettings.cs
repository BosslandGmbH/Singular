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

using Styx;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class SingularSettings : Styx.Helpers.Settings
    {
        private static SingularSettings _instance;

        public SingularSettings() : base(SettingsPath + ".xml")
        {
        }

        public static string SettingsPath { get { return string.Format("{0}\\Settings\\SingularSettings_{1}", Logging.ApplicationPath, StyxWoW.Me.Name); } }

        public static SingularSettings Instance { get { return _instance ?? (_instance = new SingularSettings()); } }

        [Setting]
        [DefaultValue(50)]
		[Category("Rest")]
		[DisplayName("Rest Health")]
		[Description("Your character will eat when your health drops below this value")]
        public int DefaultRestHealth { get; set; }

        [Setting]
        [DefaultValue(50)]
		[Category("Rest")]
		[DisplayName("Rest Mana")]
		[Description("Your character will drink when your mana drops below this value")]
        public int DefaultRestMana { get; set; }

        #region Class Late-Loading Wrappers

        // Do not change anything within this region.
        // It's written so we ONLY load the settings we're going to use.
        // There's no reason to load the settings for every class, if we're only executing code for a Druid.

        private DeathKnightSettings _dkSettings;

        private DruidSettings _druidSettings;

        private HunterSettings _hunterSettings;

        private MageSettings _mageSettings;

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