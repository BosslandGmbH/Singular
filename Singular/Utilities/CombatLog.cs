
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular.Utilities
{
    internal class CombatLogEventArgs : LuaEventArgs
    {
        public CombatLogEventArgs(string eventName, uint fireTimeStamp, object[] args)
            : base(eventName, fireTimeStamp, args)
        {
        }

        public double Timestamp => (double)Args[0];

        public string Event => Args[1].ToString();

        // Is this a string? bool? what? What the hell is it even used for?
        // it's a boolean, and it doesn't look like it has any real impact codewise apart from maybe to break old addons? - exemplar 4.1
        public string HideCaster => Args[2].ToString();

        public WoWGuid SourceGuid => ArgToGuid(Args[3]);

        public WoWUnit SourceUnit
        {
            get
            {
                WoWGuid cachedSourceGuid = SourceGuid;
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, true).FirstOrDefault(
                        o => o.IsValid && (o.Guid == cachedSourceGuid || o.Guid == cachedSourceGuid));
            }
        }

        public string SourceName => Args[4].ToString();

        public int SourceFlags => (int)(double)Args[5];

        public WoWGuid DestGuid => ArgToGuid(Args[7]);

        public WoWUnit DestUnit
        {
            get
            {
                WoWGuid cachedDestGuid = DestGuid;
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, true).FirstOrDefault(
                        o => o.IsValid && (o.Guid == cachedDestGuid || o.Guid == cachedDestGuid));
            }
        }

        public string DestName => Args[8].ToString();

        public int DestFlags => (int)(double)Args[9];

        public int SpellId => (int)(double)Args[11];

        public WoWSpell Spell => WoWSpell.FromId(SpellId);

        public string SpellName => Args[12].ToString();

        public WoWSpellSchool SpellSchool => (WoWSpellSchool)(int)(double)Args[13];

        public object[] SuffixParams
        {
            get
            {
                var args = new List<object>();
                for (int i = 11; i < Args.Length; i++)
                {
                    if (Args[i] != null)
                    {
                        args.Add(args[i]);
                    }
                }
                return args.ToArray();
            }
        }

        private static WoWGuid ArgToGuid(object o)
        {
	        string s = o.ToString();
	        WoWGuid guid;
	        if (!WoWGuid.TryParseFriendly(s, out guid))
		        guid = WoWGuid.Empty;

	        return guid;
        }
    }
}