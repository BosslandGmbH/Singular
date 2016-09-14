
using System.ComponentModel;
using System.IO;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    public enum ThrowPull
    {
        HeroicThrow,
        StormBolt,
        Auto,
        None
    }
    public enum WarriorStance
    {
        Auto,
        BattleStance        = Styx.ShapeshiftForm.BattleStance ,
        DefensiveStance     = Styx.ShapeshiftForm.DefensiveStance,
        GladiatorStance     = 33
    }

    public enum WarriorShout
    {
        CommandingShout,
        BattleShout
    }

    internal class WarriorSettings : Styx.Helpers.Settings
    {
        public WarriorSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "Warrior.xml"))
        {
        }

        public enum SpellPriority
        {
            Noxxic = 1,
            IcyVeins = 2,
            ElitistJerks = 3
        }
#if MULTIPLE_SUPPORTED
        [Setting]
        [DefaultValue(SpellPriority.Noxxic)]
        [Category("Arms")]
        [DisplayName("Spell Priority Selection")]
        public SpellPriority ArmsSpellPriority { get; set; }
#endif
        #region Protection

        [Setting]
        [DefaultValue(30)]
        [Category("Protection")]
        [DisplayName("Shield Wall Health")]
        [Description("Shield Wall will be used when your health drops below this value")]
        public int WarriorShieldWallHealth { get; set; }


        [Setting]
        [DefaultValue(20)]
        [Category("Protection")]
        [DisplayName("Last Stand Health")]
        [Description("Last Stand will be used when your health drops below this value")]
        public int WarriorLastStandHealth  { get; set; }

        [Setting]
        [DefaultValue(50)]
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
        [DisplayName("Avatar on Cooldown - AOE")]
        [Description("This will make the bot use Avatar anytime it is off cooldown while fighting multiple mobs.")]
        public bool AvatarOnCooldownAOE { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("DPS")]
        [DisplayName("Avatar on Cooldown - Single Target")]
        [Description("This will make the bot use Avatar anytime it is off cooldown while fighting a single mob.")]
        public bool AvatarOnCooldownSingleTarget { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("DPS")]
        [DisplayName("Enraged Regeneration Health")]
        [Description("Enrage Regeneration will be used when your health drops below this value")]
        public int WarriorEnragedRegenerationHealth { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use Interupts")]
        [Description("True / False if you would like the cc to use Interupts")]
        public bool UseWarriorInterrupts { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Slows")]
        [Description("True / False if you would like the cc to use slows ie. Hammstring, Piercing Howl")]
        public bool UseWarriorSlows { get; set; }

        #endregion

        #region Pull
        [Setting]
        [DefaultValue(ThrowPull.None)]
        [Category("Pull")]
        [DisplayName("Throw Pull")]
        [Description("Which throw ability the bot should use when pulling.")]
        public ThrowPull ThrowPull { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Pull - Gap Closer")]
        [DisplayName("Use Charge/Heroic Leap?")]
        [Description("Setting this to true will make the bot charge or leap at mobs that are away from us or while engaging/pulling mobs.")]
        public bool UseWarriorCloser { get; set; }

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

        [Setting]
        [DefaultValue(WarriorShout.BattleShout)]
        [Category("General")]
        [DisplayName("Warrior Shout")]
        [Description("The shout to use to keep the buff up, or use for low-rage situations.")]
        public WarriorShout Shout { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("General")]
        [DisplayName("Victory Rush on Cooldown")]
        [Description("True: use Victory Rush/Impending Victory on cooldown regardless of current health %")]
        public bool VictoryRushOnCooldown { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Defensive")]
        [DisplayName("Die by the Sword %")]
        [Description("Health % to cast this ability")]
        public int DieByTheSwordHealth { get; set; }

    }
}