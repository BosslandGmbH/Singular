using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular
{
    internal class CombatLogEventArgs : LuaEventArgs
    {
        public CombatLogEventArgs(string eventName, uint fireTimeStamp, object[] args)
            : base(eventName, fireTimeStamp, args)
        {
        }

        public double Timestamp { get { return (double)Args[0]; } }

        public string Event { get { return Args[1].ToString(); } }

        public ulong SourceGuid { get { return ulong.Parse(Args[2].ToString().Replace("0x", ""), NumberStyles.HexNumber); } }

        public WoWUnit SourceUnit
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, true).FirstOrDefault(
                        o => o.IsValid && (o.Guid == SourceGuid || o.DescriptorGuid == SourceGuid));
            }
        }

        public string SourceName { get { return Args[3].ToString(); } }

        public int SourceFlags { get { return (int)(double)Args[4]; } }

        public ulong DestGuid { get { return ulong.Parse(Args[5].ToString().Replace("0x", ""), NumberStyles.HexNumber); } }

        public WoWUnit DestUnit
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, true).FirstOrDefault(
                        o => o.IsValid && (o.Guid == DestGuid || o.DescriptorGuid == DestGuid));
            }
        }

        public string DestName { get { return Args[6].ToString(); } }

        public int DestFlags { get { return (int)(double)Args[7]; } }

        public int SpellId { get { return (int)(double)Args[8]; } }

        public WoWSpell Spell { get { return WoWSpell.FromId(SpellId); } }

        public string SpellName { get { return Args[9].ToString(); } }

        public WoWSpellSchool SpellSchool { get { return (WoWSpellSchool)(int)(double)Args[10]; } }

        public object[] SuffixParams
        {
            get
            {
                var args = new List<object>();
                for (int i = 10; i < Args.Length; i++)
                {
                    if (Args[i] != null)
                    {
                        args.Add(args[i]);
                    }
                }
                return args.ToArray();
            }
        }
    }
}