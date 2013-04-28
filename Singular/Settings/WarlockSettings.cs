
using System;
using System.ComponentModel;
using System.IO;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using Styx;
using Styx.CommonBot;
using Singular.Managers;


namespace Singular.Settings
{
    public enum WarlockPet
    {
        None        = 0,        
        Auto        = 1,
        Imp         = 23,       // Pet.CreatureFamily.Id
        Voidwalker  = 16,
        Succubus    = 17,
        Felhunter   = 15,
        Felguard    = 29
    }

    public enum Soulstone
    {
        None = 0,
        Auto,
        Self,
        Ressurect
    }

    internal class WarlockSettings : Styx.Helpers.Settings
    {

        public WarlockSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "Warlock.xml"))
        {
        }

        [Setting]
        [DefaultValue(WarlockPet.Auto)]
        [Category("Pet")]
        [DisplayName("Pet to Summon")]
        [Description("Auto: will automatically select best pet.")]
        public WarlockPet Pet { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Pet")]
        [DisplayName("Health Funnel at %")]
        [Description("Pet Health % to begin Health Funnel in combat")]
        public int HealthFunnelCast { get; set; }

        [Setting]
        [DefaultValue(95)]
        [Category("Pet")]
        [DisplayName("Health Funnel cancel at %")]
        [Description("Pet Health % to cancel Health Funnel in combat")]
        public int HealthFunnelCancel { get; set; }

        [Setting]
        [DefaultValue(75)]
        [Category("Pet")]
        [DisplayName("Health Funnel resting below %")]
        [Description("Pet Health % to cast Health Funnel while resting")]
        public int HealthFunnelRest { get; set; }

        [Setting]
        [DefaultValue(20)]
        [Category("Common")]
        [DisplayName("Drain Life%")]
        [Description("Health % which we should Drain Life")]
        public int DrainLifePercentage { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Fear")]
        [Description("Use Fear when low health or controlling adds")]
        public bool UseFear { get; set; }

        [Setting]
        [DefaultValue(Soulstone.Auto)]
        [Category("Common")]
        [DisplayName("Use Soulstone")]
        [Description("Controls usage -- Auto: Instances=Ressurect, Normal/Battleground=Self, Disabled Movement=None")]
        public Soulstone UseSoulstone { get; set; }

        [Setting]
        [DefaultValue(750)]
        [Category("Demonology")]
        [DisplayName("Switch to Caster Fury Level")]
        [Description("Go Caster at this Demonic Fury value (0 - 1000)")]
        public int FurySwitchToCaster { get; set; }

        [Setting]
        [DefaultValue(900)]
        [Category("Demonology")]
        [DisplayName("Switch to Demon Fury Level")]
        [Description("Go Demon above this Demonic Fury value (0 - 1000)")]
        public int FurySwitchToDemon { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Demonology")]
        [DisplayName("Use Demonic Leap")]
        [Description("Demonic Leap to disengage from melee")]
        public bool UseDemonicLeap { get; set; }


#region Setting Helpers


#endregion

    }
}