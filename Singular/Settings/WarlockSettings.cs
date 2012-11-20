#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $LastChangedBy$
// $LastChangedDate$
// $Revision$

#endregion

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
        Imp         = 688,
        Voidwalker  = 697,
        Succubus    = 712,
        Felhunter   = 691,
        Felguard    = 30146
    }

    internal class WarlockSettings : Styx.Helpers.Settings
    {

        public WarlockSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "Warlock.xml"))
        {
        }

        [Setting]
        [DefaultValue(WarlockPet.Auto)]
        [Category("General")]
        [DisplayName("Pet")]
        [Description("The Pet to use. Auto will auto select.  Voidwalker used if choice not available.")]
        public WarlockPet Pet { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Fear")]
        [Description("Use Fear when low health or controlling adds")]
        public bool UseFear { get; set; }


#region Setting Helpers


#endregion

    }
}