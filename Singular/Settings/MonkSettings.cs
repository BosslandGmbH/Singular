
using System.ComponentModel;
using System.IO;
using Singular.ClassSpecific.Rogue;
using Styx.Helpers;

namespace Singular.Settings
{
    internal class MonkSettings : Styx.Helpers.Settings
    {
        public MonkSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "Monk.xml")) { }

        #region Spheres

        [Setting]
        [Styx.Helpers.DefaultValue(true)]
        [Category("Spheres")]
        [DisplayName("Move to Spheres")]
        [Description("Allow moving to spheres for Chi and Health")]
        public bool MoveToSpheres { get; set; }

        [Setting]
        [Styx.Helpers.DefaultValue(15)]
        [Category("Spheres")]
        [DisplayName("Max Range at Rest")]
        [Description("Max distance willing to move when resting")]
        public int SphereDistanceAtRest { get; set; }

        [Setting]
        [Styx.Helpers.DefaultValue(5)]
        [Category("Spheres")]
        [DisplayName("Max Range in Combat")]
        [Description("Max distance willing to move during combat")]
        public int SphereDistanceInCombat { get; set; }

        [Setting]
        [Styx.Helpers.DefaultValue(45)]
        [Category("Spheres")]
        [DisplayName("Rest Healing Sphere Health")]
        [Description("Min Resting Health % to cast Healing Sphere")]
        public int RestHealingSphereHealth { get; set; }

        #endregion

        #region Common

        [Setting]
        [Styx.Helpers.DefaultValue(60)]
        [Category("Common")]
        [DisplayName("Fortifying Brew Percent")]
        [Description("Fortifying Brew is used when health percent is at or below this value")]
        public float FortifyingBrewPercent { get; set; }

        [Setting]
        [Styx.Helpers.DefaultValue(70)]
        [Category("Common")]
        [DisplayName("Chi Wave Percent")]
        [Description("Chi Wave is used when health percent is at or below this value")]
        public float ChiWavePercent { get; set; }

        #endregion

        #region Brewmaster

        [Setting]
        [Styx.Helpers.DefaultValue(70)]
        [Category("Brewmaster")]
        [DisplayName("Avert Harm Group Health Percent")]
        [Description("Avert Harm is used when the averge health percent of group falls below this value")]
        public float AvertHarmGroupHealthPercent { get; set; }

        [Setting]
        [Styx.Helpers.DefaultValue(true)]
        [Category("Brewmaster")]
        [DisplayName("Use Avert Harm")]
        public bool UseAvertHarm { get; set; }

        [Setting]
        [Styx.Helpers.DefaultValue(6)]
        [Category("Brewmaster")]
        [DisplayName("Elusive Brew Min. Stack")]
        [Description("Elusive Brew is used when player has this many stacks of Elusive Brew or more")]
        public float ElusiveBrewMinumumStackCount { get; set; }

        [Setting]
        [Styx.Helpers.DefaultValue(true)]
        [Category("Brewmaster")]
        [DisplayName("Use Elusive Brew")]
        public bool UseElusiveBrew { get; set; }
        #endregion


    }
}