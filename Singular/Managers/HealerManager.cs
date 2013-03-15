
using System.Collections.Generic;
using System.Linq;

using Singular.Settings;

using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular.Managers
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

    internal class HealerManager : Targeting
    {
        private static readonly WaitTimer _tankReset = WaitTimer.ThirtySeconds;

        // private static ulong _tankGuid;

        static HealerManager()
        {
            // Make sure we have a singleton instance!
            Instance = new HealerManager();
        }

        public new static HealerManager Instance { get; private set; }

        public static bool NeedHealTargeting { get; set; }

        public List<WoWUnit> HealList { get { return ObjectList.ConvertAll(o => o.ToUnit()); } }

        protected override List<WoWObject> GetInitialObjectList()
        {
            // Targeting requires a list of WoWObjects - so it's not bound to any specific type of object. Just casting it down to WoWObject will work fine.
            return ObjectManager.ObjectList.Where(o => o is WoWPlayer).ToList();
        }

        protected override void DefaultIncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
        {
            bool foundMe = false;
            bool isHorde = StyxWoW.Me.IsHorde;

            foreach (WoWObject incomingUnit in incomingUnits)
            {
                if (incomingUnit.IsMe)
                    foundMe = true;

                if (incomingUnit.ToPlayer().IsHorde != isHorde)
                    continue;

                outgoingUnits.Add(incomingUnit);
                if (SingularSettings.Instance.IncludePetsAsHealTargets && incomingUnit is WoWPlayer && incomingUnit.ToPlayer().GotAlivePet)
                    outgoingUnits.Add(incomingUnit.ToPlayer().Pet);
            }

            if (!foundMe)
            {
                outgoingUnits.Add(StyxWoW.Me);
                if (SingularSettings.Instance.IncludePetsAsHealTargets && StyxWoW.Me.GotAlivePet)
                    outgoingUnits.Add(StyxWoW.Me.Pet);
            }

            if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.IsFriendly && !StyxWoW.Me.CurrentTarget.IsPet && !StyxWoW.Me.CurrentTarget.IsPlayer)
                outgoingUnits.Add(StyxWoW.Me.CurrentTarget);

            if (StyxWoW.Me.FocusedUnit != null && StyxWoW.Me.FocusedUnit.IsFriendly && !StyxWoW.Me.FocusedUnit.IsPet && !StyxWoW.Me.FocusedUnit.IsPlayer)
                outgoingUnits.Add(StyxWoW.Me.FocusedUnit);
        }

        protected override void DefaultRemoveTargetsFilter(List<WoWObject> units)
        {
            bool isHorde = StyxWoW.Me.IsHorde;
            int maxHealRangeSqr = SingularSettings.Instance.MaxHealTargetRange * SingularSettings.Instance.MaxHealTargetRange;

            for (int i = units.Count - 1; i >= 0; i--)
            {
                WoWUnit unit = units[i].ToUnit();
                if (unit == null || !unit.IsValid || unit.IsDead || unit.IsHostile)
                {
                    units.RemoveAt(i);
                    continue;
                }

                WoWPlayer p = null;
                if (unit.IsPet)
                    p = unit.OwnedByRoot.ToPlayer();
                else if (unit is WoWPlayer)
                    p = unit.ToPlayer();

                if (p != null)
                {
                    // Make sure we ignore dead/ghost players. If we need res logic, they need to be in the class-specific area.
                    if (p.IsGhost)
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
/*
                    if (!p.Combat && p.HealthPercent >= SingularSettings.Instance.IgnoreHealTargetsAboveHealth)
                    {
                        units.RemoveAt(i);
                        continue;
                    }
 */ 
                }

                // If we have movement turned off, ignore people who aren't in range.
                // Almost all healing is 40 yards, so we'll use that. If in Battlegrounds use a slightly larger value to expane our 
                // healing range, but not too large that we are running all over the bg zone 
                // note: reordered following tests so only one floating point distance comparison done due to evalution of DisableAllMovement
                if ((MovementManager.IsMovementDisabled && unit.DistanceSqr > 40*40) || unit.DistanceSqr > maxHealRangeSqr)
                {
                    units.RemoveAt(i);
                    continue;
                }
            }
        }

        protected override void DefaultTargetWeight(List<TargetPriority> units)
        {
            var tanks = GetMainTankGuids();
            var inBg = Battlegrounds.IsInsideBattleground;
            var amHolyPally = StyxWoW.Me.Specialization == WoWSpec.PaladinHoly;

            foreach (TargetPriority prio in units)
            {
                WoWUnit u = prio.Object.ToUnit();
                if (u == null || !u.IsValid)
                {
                    prio.Score = -9999f;
                    continue;
                }

                prio.Score = u.IsAlive ? 500f : -500f;

                // The more health they have, the lower the score.
                // This should give -500 for units at 100%
                // And -50 for units at 10%
                prio.Score -= u.HealthPercent * 5;

                // If they're out of range, give them a bit lower score.
                if (u.DistanceSqr > 40 * 40)
                {
                    prio.Score -= 50f;
                }

                // If they're out of LOS, again, lower score!
                if (!u.InLineOfSpellSight)
                {
                    prio.Score -= 100f;
                }

                // Give tanks more weight. If the tank dies, we all die. KEEP HIM UP.
                if (tanks.Contains(u.Guid) && u.HealthPercent != 100 && 
                    // Ignore giving more weight to the tank if we have Beacon of Light on it.
                    (!amHolyPally || !u.Auras.Any(a => a.Key == "Beacon of Light" && a.Value.CreatorGuid == StyxWoW.Me.Guid)))
                {
                    prio.Score += 100f;
                }

                // Give flag carriers more weight in battlegrounds. We need to keep them alive!
                if (inBg && u.IsPlayer && u.Auras.Keys.Any(a => a.ToLowerInvariant().Contains("flag")))
                {
                    prio.Score += 100f;
                }
            }
        }

        private static HashSet<ulong> GetMainTankGuids()
        {
            var infos = StyxWoW.Me.GroupInfo.RaidMembers;

            return new HashSet<ulong>(
                from pi in infos
                where (pi.Role & WoWPartyMember.GroupRole.Tank) != 0
                select pi.Guid);
        }
    }
}