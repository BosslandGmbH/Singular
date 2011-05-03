using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx.WoWInternals.WoWObjects;

namespace Singular.Helpers
{
    public enum ClusterType
    {
        Radius,
        Chained
    }

    public static class Clusters
    {
        public static int GetClusterCount(WoWUnit target, IEnumerable<WoWUnit> otherUnits, ClusterType type, float clusterRange)
        {
            switch (type)
            {
                case ClusterType.Radius:
                    return GetRadiusClusterCount(target, otherUnits, clusterRange);
                case ClusterType.Chained:
                    return GetChainedClusterCount(target, otherUnits, clusterRange);
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }

        public static WoWUnit GetBestUnitForCluster(IEnumerable<WoWUnit> units, ClusterType type, float clusterRange)
        {
            switch (type)
            {
                case ClusterType.Radius:
                    return (from u in units
                            select new { Count = GetRadiusClusterCount(u, units, clusterRange), Unit = u }).OrderByDescending(a => a.Count).
                        FirstOrDefault().Unit;
                case ClusterType.Chained:
                    return (from u in units
                            select new { Count = GetChainedClusterCount(u, units, clusterRange), Unit = u }).OrderByDescending(a => a.Count).
                        FirstOrDefault().Unit;
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }


        private static int GetRadiusClusterCount(WoWUnit target, IEnumerable<WoWUnit> otherUnits, float radius)
        {
            var targetLoc = target.Location;
            return otherUnits.Count(u => u.Location.DistanceSqr(targetLoc) <= radius * radius);
        }

        private static int GetChainedClusterCount(WoWUnit target, IEnumerable<WoWUnit> otherUnits, float chainRange)
        {
            var unitCounters = otherUnits.Select(u => GetUnitsChainWillJumpTo(target, otherUnits.ToList(), chainRange).Count);

            return unitCounters.Max() + 1;
        }

        private static List<WoWUnit> GetUnitsChainWillJumpTo(WoWUnit target, List<WoWUnit> otherUnits, float chainRange)
        {
            var targetLoc = target.Location;
            var targetGuid = target.Guid;
            for (int i = otherUnits.Count - 1; i >= 0; i--)
            {
                if (otherUnits[i].Guid == targetGuid || otherUnits[i].Location.DistanceSqr(targetLoc) > chainRange)
                    otherUnits.RemoveAt(i);
            }
            return otherUnits;
        }
    }
}
