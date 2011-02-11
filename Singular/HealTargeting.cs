﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Styx;
using Styx.Helpers;
using Styx.Logic;
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

    internal class HealTargeting : Targeting
    {
        static HealTargeting()
        {
            // Make sure we have a singleton instance!
            Instance = new HealTargeting();
        }

        public new static HealTargeting Instance { get; private set; }

        public List<WoWPlayer> HealList { get { return ObjectList.ConvertAll(o => o.ToPlayer()); } }

        protected override List<WoWObject> GetInitialObjectList()
        {
            // Targeting requires a list of WoWObjects - so it's not bound to any specific type of object. Just casting it down to WoWObject will work fine.
            return ObjectManager.GetObjectsOfType<WoWPlayer>(true, true).Cast<WoWObject>().ToList();
        }

        protected override void DefaultIncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
        {
            bool foundMe = false;
            foreach (var incomingUnit in incomingUnits)
            {
                if (incomingUnit.IsMe)
                    foundMe = true;
                outgoingUnits.Add(incomingUnit);
            }
            if (!foundMe)
                outgoingUnits.Add(StyxWoW.Me);
        }

        protected override void DefaultRemoveTargetsFilter(List<WoWObject> units)
        {
            bool isHorde = StyxWoW.Me.IsHorde;
            for (int i = units.Count - 1; i >= 0; i--)
            {
                WoWObject o = units[i];
                if (!(o is WoWPlayer))
                {
                    units.RemoveAt(i);
                    continue;
                }

                WoWPlayer p = o.ToPlayer();

                // Make sure we ignore dead/ghost players. If we need res logic, they need to be in the class-specific area.
                if (p.Dead || p.IsGhost)
                {
                    units.RemoveAt(i);
                    continue;
                }

                // Check if they're hostile first. This should remove enemy players, but it's more of a sanity check than anything.
                if (p.IsHostile)
                {
                    units.RemoveAt(i);
                    continue;
                }

                // If we're horde, and they're not, fuggin ignore them!
                if (p.IsHorde != isHorde)
                {
                    units.RemoveAt(i);
                    continue;
                }

                // They're not in our party/raid. So ignore them. We can't heal them anyway.
                if (!p.IsInMyPartyOrRaid)
                {
                    units.RemoveAt(i);
                    continue;
                }
            }
        }

        protected override void DefaultTargetWeight(List<TargetPriority> units)
        {
            var tanks = GetMainTankGuids();
            foreach (TargetPriority prio in units)
            {
                prio.Score = 500f;
                WoWPlayer p = prio.Object.ToPlayer();

                // The more health they have, the lower the score.
                // This should give -500 for units at 100%
                // And -50 for units at 10%
                prio.Score -= p.HealthPercent * 5;

                // If they're out of range, give them a bit lower score.
                if (p.DistanceSqr > 40 * 40)
                {
                    prio.Score -= 50f;
                }

                // If they're out of LOS, again, lower score!
                if (!p.InLineOfSight)
                {
                    prio.Score -= 100f;
                }

                // Give tanks more weight. If the tank dies, we all die. KEEP HIM UP.
                if (tanks.Equals(p.Guid))
                {
                    //Logger.Write(p.Name + " is a tank!");
                    prio.Score += 100f;
                }
            }
        }

        private static readonly WaitTimer _tankReset = WaitTimer.ThirtySeconds;

        private static ulong _tankGuid;
        private static ulong GetMainTankGuids()
        {
            if (!_tankReset.IsFinished)
            {
                return _tankGuid;
            }
            _tankReset.Reset();

            for (int i = 1; i <= StyxWoW.Me.PartyMemberGuids.Count(); i++)
            {
                string memberRole = Lua.GetReturnVal<string>("return UnitGroupRolesAssigned(\"party" + i + "\")", 0);
                if (memberRole == "TANK")
                {
                    string tankGuidString = Lua.GetReturnVal<string>("return UnitGUID(\"party" + i + "\")", 0);
                    _tankGuid = ulong.Parse(tankGuidString.Replace("0x", string.Empty), NumberStyles.HexNumber);
                    return _tankGuid;
                }
            }
            _tankGuid = 0;
            return 0;
            //List<WoWPartyMember> infos = null;
            //if (StyxWoW.Me.IsInRaid)
            //{
            //    infos = StyxWoW.Me.RaidMemberInfos;
            //}
            //else
            //{
            //    infos = (from g in StyxWoW.Me.PartyMemberGuids
            //             select new WoWPartyMember(g)).ToList();
            //}


            //return new HashSet<ulong>(
            //    from pi in infos
            //    where pi.Role == WoWPartyMember.GroupRole.Tank
            //    select pi.Guid);
        }

        public /*override*/ void Pulse()
        {
            //Logger.Write("Pulsing!");
            //base.Pulse();
        }
    }
}