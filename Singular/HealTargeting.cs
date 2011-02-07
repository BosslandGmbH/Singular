using System.Collections.Generic;
using System.Linq;

using Styx;
using Styx.Logic;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular
{
    /// <summary>Values that represent a role in our current group. [Retrieved from Me.RaidMemberInfos]</summary>
    /// <remarks>
    ///  TODO: Push this into the HB core as an enum instead of an integer return
    /// </remarks>
    public enum GroupRole
    {
        Tank = 0,
        Healer = 1,
        Damage = 2,
        None = 3,
    }

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

        protected override List<WoWObject> GetInitialObjectList()
        {
            // Targeting requires a list of WoWObjects - so it's not bound to any specific type of object. Just casting it down to WoWObject will work fine.
            return ObjectManager.GetObjectsOfType<WoWPlayer>(true, true).Cast<WoWObject>().ToList();
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
            HashSet<ulong> tanks = GetMainTankGuids();
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
                if (tanks.Contains(p.Guid))
                {
                    prio.Score += 150f;
                }
            }
        }

        private static HashSet<ulong> GetMainTankGuids()
        {
            return new HashSet<ulong>(
                from pi in StyxWoW.Me.RaidMemberInfos
                where pi.Role == (uint)GroupRole.Tank ||
                      pi.IsMainTank
                select pi.Guid);
        }
    }
}