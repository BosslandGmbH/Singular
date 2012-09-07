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
        [DefaultValue(PoisonType.Instant)]
        [Category("Common")]
        [DisplayName("Main Hand Poison")]
        [Description("Main Hand Poison")]
        public PoisonType MHPoison { get; set; }

        [Setting]
        [DefaultValue(PoisonType.Deadly)]
        [Category("Common")]
        [DisplayName("Off Hand Poison")]
        [Description("Off Hand Poison")]
        public PoisonType OHPoison { get; set; }

        [Setting]
        [DefaultValue(PoisonType.Wound)]
        [Category("Common")]
        [DisplayName("Thrown Poison")]
        [Description("Thrown Poison")]
        public PoisonType ThrownPoison { get; set; }


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
    }
}