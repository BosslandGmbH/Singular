
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
        [Category("Common")]
        [DisplayName("Pet")]
        [Description("The Pet to use. Auto will auto select.  Voidwalker used if choice not available.")]
        public WarlockPet Pet { get; set; }

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
        [DisplayName("Switch to Caster %")]
        [Description("Change to Caster when below this Demonic Fury %")]
        public int FurySwitchToCaster { get; set; }

        [Setting]
        [DefaultValue(900)]
        [Category("Demonology")]
        [DisplayName("Switch to Demon %")]
        [Description("Change to Demon when above this Demonic Fury %")]
        public int FurySwitchToDemon { get; set; }

#region Setting Helpers


#endregion

    }
}