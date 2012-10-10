using System.Linq;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System;

namespace Singular.Helpers
{
    internal static class PVP
    {
        /// <summary>
        /// determines if you are inside a battleground/arena prior to start.  this was previously
        /// known as the preparation phase easily identified by a Preparation or Arena Preparation
        /// buff, however those auras were removed in MoP
        /// </summary>
        /// <returns>true if in Battleground/Arena prior to start, false otherwise</returns>
        public static bool IsBattlegroundPrep()
        {
            return Battlegrounds.IsInsideBattleground && DateTime.Now < Battlegrounds.BattlefieldStartTime;
        }

        //public static bool IsCrowdControlled(this WoWUnit unit)
        //{
        //    return unit.GetAllAuras().Any(a => a.IsHarmful &&
        //        (a.Spell.Mechanic == WoWSpellMechanic.Shackled ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Polymorphed ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Horrified ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Rooted ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Frozen ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Stunned ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Fleeing ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Banished ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Sapped));
        //}

        public static bool IsStunned(this WoWUnit unit)
        {
            return unit.HasAuraWithMechanic(WoWSpellMechanic.Stunned, WoWSpellMechanic.Incapacitated);
        }

        public static bool IsRooted(this WoWUnit unit)
        {
            return unit.HasAuraWithMechanic(WoWSpellMechanic.Rooted, WoWSpellMechanic.Shackled);
        }

        public static bool IsSilenced(WoWUnit unit)
        {
            return unit.GetAllAuras().Any(a => a.IsHarmful &&
                (a.Spell.Mechanic == WoWSpellMechanic.Interrupted || 
                a.Spell.Mechanic == WoWSpellMechanic.Silenced));
        }
    }
}
