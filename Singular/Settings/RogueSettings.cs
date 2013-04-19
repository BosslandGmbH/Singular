
using System.ComponentModel;
using System.IO;
using Singular.ClassSpecific.Rogue;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class RogueSettings : Styx.Helpers.Settings
    {
        public RogueSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "Rogue.xml"))
        {
        }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Use Sprint")]
        [Description("Sprint to close destinations or when unable to mount")]
        public bool UseSprint { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Common")]
        [DisplayName("Recuperate Health%")]
        [Description("Health % to Recuperate at during Combat, 0 to disable")]
        public int RecuperateHealth { get; set; }

        [Setting]
        [DefaultValue(LethalPoisonType.Auto)]
        [Category("Common")]
        [DisplayName("Lethal Poison")]
        [Description("Lethal Poison")]
        public LethalPoisonType LethalPoison { get; set; }

        [Setting]
        [DefaultValue(NonLethalPoisonType.Auto)]
        [Category("Common")]
        [DisplayName("Non Lethal Poison")]
        [Description("Non Lethal Poison")]
        public NonLethalPoisonType NonLethalPoison { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Stealth Always")]
        [Description("Stealth at all times out of combat. Does not disable mounting (you can in HB Settings if desired)")]
        public bool StealthAlways { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Common")]
        [DisplayName("Fan of Knives Count")]
        [Description("Use FoK as Combo Point builder at this enemy count")]
        public int FanOfKnivesCount { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Common")]
        [DisplayName("AOE Spell Priority Count")]
        [Description("Use AOE Spell Priorities at this enemy count")]
        public int AoeSpellPriorityCount { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Interrupt Spells")]
        [Description("Interrupt Spells")]
        public bool InterruptSpells { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use TotT")]
        [Description("Use TotT")]
        public bool UseTricksOfTheTrade { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Pick Pocket")]
        [Description("Requires AutoLoot ON; pick pocket mob before opener")]
        public bool UsePickPocket { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Pick Lock")]
        [Description("Requires AutoLoot ON; unlock boxes in bags during rest")]
        public bool UsePickLock { get; set; }


        [Setting]
        [DefaultValue(true)]
        [Category("Combat Spec")]
        [DisplayName("Use Rupture Finisher")]
        [Description("Use Rupture Finisher")]
        public bool CombatUseRuptureFinisher { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Combat Spec")]
        [DisplayName("Use Expose Armor")]
        [Description("Use Expose Armor")]
        public bool UseExposeArmor { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Speed Buff")]
        [Description("Cast Burst of Speed when running out of combat")]
        public bool UseSpeedBuff { get; set; }

    }
}