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

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    public enum WarriorStance
    {
        BattleStance,
        BerserkerStance,
        DefensiveStance
    }

    public enum WarriorShout
    {
        CommandingShout,
        BattleShout
    }

    internal class WarriorSettings : Styx.Helpers.Settings
    {
        public WarriorSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "Warrior.xml"))
        {
        }

        #region Protection
        [Setting]
        [DefaultValue(50)]
        [Category("Protection")]
        [DisplayName("Enraged Regeneration Health")]
        [Description("Enrage Regeneration will be used when your health drops below this value")]
        public int WarriorEnragedRegenerationHealth { get; set; }



        [Setting]
        [DefaultValue(20)]
        [Category("Protection")]
        [DisplayName("Shield Wall Health")]
        [Description("Shield Wall will be used when your health drops below this value")]
        public int WarriorShieldWallHealth { get; set; }


        [Setting]
        [DefaultValue(40)]
        [Category("Protection")]
        [DisplayName("Last Stand Health")]
        [Description("Last Stand will be used when your health drops below this value")]
        public int WarriorLastStandHealth  { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Protection")]
        [DisplayName("Shield Block Health")]
        [Description("Shield Block will be used when your health drops below this value")]
        public int WarriorShieldBlockHealth { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Protection")]
        [DisplayName("Shield Barrier Health")]
        [Description("Shield Barrier will be used when your health drops below this value")]
        public int WarriorShieldBarrierHealth { get; set; }

        #endregion

        #region DPS

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use Interupts")]
        [Description("True / False if you would like the cc to use Interupts")]
        public bool UseWarriorInterrupts { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("true for Battle Shout, false for Commanding")]
        [Description("True / False if you would like the cc to use Battleshout/Commanding")]
        public bool UseWarriorShouts { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Slows")]
        [Description("True / False if you would like the cc to use slows ie. Hammstring, Piercing Howl")]
        public bool UseWarriorSlows { get; set; }
        
        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use Charge/Intercept/Heroic Leap?")]
        [Description("True / False if you would like the cc to use any gap closers")]
        public bool UseWarriorCloser { get; set; }
        #endregion

        [Setting]
        [DefaultValue(WarriorShout.BattleShout)]
        [Category("General")]
        [DisplayName("Warrior Shout")]
        [Description("The shout to use to keep the buff up, or use for low-rage situations.")]
        public WarriorShout UseShout { get; set; }

        [Setting]
        [DefaultValue(WarriorStance.BattleStance)]
        [Category("DPS")]
        [DisplayName("Warrior DPS Stance")]
        [Description("The stance to use while DPSing. Battle stance if there is little incoming damage, Berserker otherwise. Protection will always use Defensive stance.")]
        public WarriorStance WarriorDpsStance { get; set; }
    }
}