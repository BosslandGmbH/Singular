using System.Collections.Generic;
using System.Linq;

using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular.Helpers
{
    public static class Unit
    {
        /// <summary>
        ///   Gets the nearby friendly players within 40 yards.
        /// </summary>
        /// <value>The nearby friendly players.</value>
        public static List<WoWPlayer> NearbyFriendlyPlayers
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWPlayer>(true, true).Where(p => p.DistanceSqr <= 40 * 40 && p.IsFriendly).ToList(); 
            }
        }

        /// <summary>
        ///   Gets the nearby unfriendly units within 40 yards.
        /// </summary>
        /// <value>The nearby unfriendly units.</value>
        public static List<WoWUnit> NearbyUnfriendlyUnits
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Where(p => p.IsHostile && !p.Dead && !p.IsPet && p.DistanceSqr <= 40 * 40).ToList();
            }
        }

        public static bool HasAura(WoWUnit unit, string aura)
        {
            return HasAura(unit, aura, 0);
        }

        public static bool HasAura(WoWUnit unit, string aura, int stacks)
        {
            var auras = unit.GetAllAuras();
            return (from a in auras
                    where a.Name == aura
                    select a.StackCount >= stacks).FirstOrDefault();
        }

        public static bool HasAuraWithMechanic(WoWUnit unit, params WoWSpellMechanic[] mechanics)
        {
            var auras = unit.GetAllAuras();
            foreach (var a in auras)
            {
                if (mechanics.Contains(a.Spell.Mechanic))
                    return true;
            }
            return false;
        }
    }
}
