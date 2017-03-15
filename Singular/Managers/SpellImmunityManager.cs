using System.Collections.Generic;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular.Managers
{
    static class SpellImmunityManager
    {

        private static readonly HashSet<int> s_ignoredSpells = new HashSet<int>()
        {
            // Mages spells with slow/freeze component that causes immunity error when used against slow/freeze immune mobs
            116, // Frostbolt
            120, // Cone of cold
            122, // Frost Nova
            44614, // Flurry
            84714, // Frozen Orb
            190356, // Blizazrd
            199786 // Glacial Spike
        };


        // This dictionary uses Unit.Entry as key and WoWSpellSchool as value.
        static readonly Dictionary<uint, WoWSpellSchool> ImmuneNpcs = new Dictionary<uint, WoWSpellSchool>();

        public static void Add(uint mobId, WoWSpellSchool school, WoWSpell spell = null)
        {
            if (spell != null && s_ignoredSpells.Contains(spell.Id))
                return;

            if (!ImmuneNpcs.ContainsKey(mobId))
                ImmuneNpcs.Add(mobId, school);
        }

        public static bool IsImmune(this WoWUnit unit, WoWSpellSchool school)
        {
            return unit != null && ImmuneNpcs.ContainsKey(unit.Entry) && (ImmuneNpcs[unit.Entry] & school) > 0;
        }
    }
}
