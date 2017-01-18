
using System.ComponentModel;
using System.IO;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using System.Collections.Generic;

using System.Xml.Serialization;
// using System.IO;
// using System.Reflection;

namespace Singular.Settings
{
    /* removed in WoD
    public enum MageArmor
    {
        None = 0,
        Auto = 1,
        Frost,
        Mage,
        Molten
    }
    */

    internal class MageSettings : Styx.Helpers.Settings
    {
        public MageSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "Mage.xml"))
        {
            // bit of a hack -- SavedToFile setting tracks if we have ever saved
            // .. these settings.  this is needed because we can't use the DefaultValue
            // .. attribute for a multi values setting
            if (!SavedToFile)
            {
                SavedToFile = true;
            }
        }


        // hidden setting to track if we have ever saved this settings file before
        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool SavedToFile { get; set; }

        #region Category: Common

        [Setting]
        [Styx.Helpers.DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Summon Table If In A Party")]
        [Description("Summons a food table instead of using conjured food if in a party")]
        public bool SummonTableIfInParty { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Common")]
        [DisplayName("Mirror Image Count")]
        [Description("If this many mobs are attacking us, Mirror Image will be used.")]
        public int MirrorImageCount { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Use Time Warp")]
        [Description("Time Warp when appropriate (never when movement disabled)")]
        public bool UseTimeWarp { get; set; }

        /*
        [Setting]
        [DefaultValue(MageArmor.Auto)]
        [Category("Common")]
        [DisplayName("Armor Buff")]
        [Description("Which Armor Buff to cast (None: user controlled, Auto: best choice)")]
        public MageArmor Armor { get; set; }
        */

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Slow Fall")]
        [Description("True: Cast Slow Fall if falling")]
        public bool UseSlowFall { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Polymorph on Adds")]
        public bool UsePolymorphOnAdds { get; set; }

        /*
                        [Setting]
                        [DefaultValue(40)]
                        [Category("Common")]
                        [DisplayName("Heal Water Elemental %")]
                        [Description("Pet Health % which we cast Frost Bolt to Heal")]
                        public int HealWaterElementalPct { get; set; }
                */

        #endregion

        #region  Category: Arcance

        [Setting]
        [DefaultValue(30)]
        [Category("Arcane")]
        [DisplayName("Evocation Mana %")]
        [Description("Mana % to cast this ability")]
        public int EvocationManaPct { get; set; }

        #endregion

        #region  Category: Fire

        [Setting]
        [DefaultValue(2)]
        [Category("Fire")]
        [DisplayName("Cinderstorm Mob Count")]
        [Description("How many mobs should be near our target in order for us to use Cinderstorm. This does not count our current target, only the mobs around it.")]
        public int CinderstormCount { get; set; }

        #endregion

        #region Category: Talents
        [Setting]
        [DefaultValue(35)]
        [Category("Talents")]
        [DisplayName("Cold Snap %")]
        [Description("Health % to cast this ability")]
        public int ColdSnapHealthPct { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Talents")]
        [DisplayName("Alter Time Mob Count")]
        [Description("Enemy Count attacking to trigger initial cast of this ability")]
        public int AlterTimeMobCount { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Talents")]
        [DisplayName("Alter Time Player Count")]
        [Description("Attacking mob count to trigger casting of this ability")]
        public int AlterTimePlayerCount { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Talents")]
        [DisplayName("Alter Time Health Ratio")]
        [Description("Percentage of Health saved by Alter Time which will trigger second cast.  If Alter Time cast when health is 80% and Ratio is 40%, second cast will occur when health falls below 32% (80 * 40%)")]
        public int AlterTimeHealthPct { get; set; }

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
    }
}
