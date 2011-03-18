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

using System.Collections.Generic;
using System.Linq;

using Styx;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular
{
    /*
     * Targeting works like so, in order of being called
     * 
     * GetInitialObjectList - Return a list of initial objects for the targeting to use.
     * RemoveTargetsFilter - Remove anything that doesn't belong in the list.
     * IncludeTargetsFilter - If you want to include units regardless of the remove filter
     * WeighTargetsFilter - Weigh each target in the list.     
     *
     */

    internal class TankTargeting : Targeting
    {
        static TankTargeting()
        {
            Instance = new TankTargeting();
            Instance.NeedToTaunt = new List<WoWUnit>();
        }

        public new static TankTargeting Instance { get; set; }
        public List<WoWUnit> NeedToTaunt { get; private set; }

        protected override List<WoWObject> GetInitialObjectList()
        {
            return ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Cast<WoWObject>().ToList();
        }

        protected override void DefaultRemoveTargetsFilter(List<WoWObject> units)
        {
            for (int i = units.Count - 1; i >= 0; i--)
            {
                if (!units[i].IsValid)
                {
                    units.RemoveAt(i);
                    continue;
                }

                WoWUnit u = units[i].ToUnit();

                if (u.IsFriendly || u.Dead || u.IsPet() || !u.Combat || IsCrowdControlled(u))
                {
                    units.RemoveAt(i);
                    continue;
                }

                if (u.CurrentTarget != null)
                {
                    WoWUnit tar = u.CurrentTarget;
                    if (tar.IsPlayer && tar.IsHostile)
                    {
                        units.RemoveAt(i);
                        continue;
                    }
                }
            }
        }

        protected override void DefaultIncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
        {
            foreach (WoWObject i in incomingUnits)
            {
                outgoingUnits.Add(i);
            }
        }

        protected override void DefaultTargetWeight(List<TargetPriority> units)
        {
            NeedToTaunt.Clear();
            List<WoWPlayer> members = StyxWoW.Me.IsInRaid ? StyxWoW.Me.RaidMembers : StyxWoW.Me.PartyMembers;
            foreach (TargetPriority p in units)
            {
                WoWUnit u = p.Object.ToUnit();

                // I have 1M threat -> nearest party has 990k -> leaves 10k difference. Subtract 10k
                // I have 1M threat -> nearest has 400k -> Leaves 600k difference -> subtract 600k
                // The further the difference, the less the unit is weighted.
                // If they have MORE threat than I do, the number is -10k -> which subtracted = +10k weight.
                int aggroDiff = GetAggroDifferenceFor(u, members);
                p.Score -= aggroDiff;

                // If we have NO threat on the mob. Taunt the fucking thing.
                // Don't taunt fleeing mobs!
                if (aggroDiff < 0 && !u.Fleeing)
                {
                    NeedToTaunt.Add(u);
                }
            }
        }

        private bool IsCrowdControlled(WoWUnit unit)
        {
            Dictionary<string, WoWAura>.ValueCollection auras = unit.Auras.Values;

            return auras.Any(
                a => a.Spell.Mechanic == WoWSpellMechanic.Banished ||
                     a.Spell.Mechanic == WoWSpellMechanic.Charmed ||
                     a.Spell.Mechanic == WoWSpellMechanic.Horrified ||
                     a.Spell.Mechanic == WoWSpellMechanic.Incapacitated ||
                     a.Spell.Mechanic == WoWSpellMechanic.Polymorphed ||
                     a.Spell.Mechanic == WoWSpellMechanic.Sapped ||
                     a.Spell.Mechanic == WoWSpellMechanic.Shackled ||
                     a.Spell.Mechanic == WoWSpellMechanic.Asleep);
        }

        private int GetAggroDifferenceFor(WoWUnit unit, IEnumerable<WoWPlayer> partyMembers)
        {
            uint myThreat = unit.ThreatInfo.ThreatValue;
            uint highestParty = (from p in partyMembers
                                 let tVal = unit.GetThreatInfoFor(p).ThreatValue
                                 orderby tVal descending
                                 select tVal).FirstOrDefault();

            int result = (int)myThreat - (int)highestParty;
            return result;
        }
    }
}