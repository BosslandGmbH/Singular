
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
    internal class MageSettings : Styx.Helpers.Settings
    {
        public MageSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "Mage.xml"))
        {
            // bit of a hack -- SavedToFile setting tracks if we have ever saved
            // .. these settings.  this is needed because we can't use the DefaultValue
            // .. attribute for a multi values setting
            if (!SavedToFile)
            {
                SavedToFile = true;
                SpellStealList = new uint[]
                { 
                    // list (possibly outdated at:) http://www.wowwiki.com/List_of_magic_effects
                    1022,   //  Paladin - Hand of Protection
                    1044,   //  Paladin - Hand of Freedom
                    974,    //  Shaman - Earth Shield
                    2825,   //  Shaman - Bloodlust
                    32182,  //  Shaman - Heroism
                    80353   //  Mage - Time Warp
                };
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
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Use Time Warp")]
        [Description("Time Warp when appropriate (never when movement disabled)")]
        public bool UseTimeWarp { get; set; }

        #endregion

        #region Category: Spellsteal

        [Setting]
        [DefaultValue(WatchTargetForCast.Current)]
        [Category("Spellsteal")]
        [DisplayName("Which Targets")]
        [Description("None: disabled, Current: our target only, Other: enemies in range we are facing")]
        public WatchTargetForCast SpellStealTarget { get; set; }

        [Setting]
        [Category("Spellsteal")]
        [DisplayName("Spell List")]
        [Description("True: check enemies for spell in list to steal")]
        public uint[] SpellStealList { get; set; }

        #endregion

    }
}
