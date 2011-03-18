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

using System;
using System.Collections.Generic;
using System.Linq;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWCache;
using Styx.WoWInternals.WoWObjects;

namespace Singular
{
    public enum PetType
    {
        // These are CreatureFamily IDs. See 'CurrentPet' for usage.
        None = 0,
        Imp = 23,
        Felguard = 29,
        Voidwalker = 16,
        Felhunter = 15,
        Succubus = 17,
    }

    internal class PetManager
    {
        private static readonly WaitTimer CallPetTimer = WaitTimer.OneSecond;

        private static ulong _petGuid = 0;
        private static List<WoWPetSpell> _petSpells = new List<WoWPetSpell>();
        internal static void Pulse()
        {
            // TODO: Remove this stuff upon the next HB release.
            if (!StyxWoW.Me.GotAlivePet)
            {
                _petSpells.Clear();
                return;
            }

            if (_petGuid != StyxWoW.Me.Pet.Guid)
            {
                Logger.Write("Pet changed. Rebuilding actions mapping.");
                _petGuid = StyxWoW.Me.Pet.Guid;

                // Too lazy to rebase to 0x1000
                const uint PET_SPELLS_PTR = 0x00E0A388 - 0x400000; // lua_GetPetActionInfo - 2nd DWORD - used as an array of vals
                _petSpells.Clear();
                var spellMasks = ObjectManager.Wow.ReadStructArrayRelative<uint>(PET_SPELLS_PTR, 10);
                for (int i = 0; i < 10; i++)
                {
                    var spell = new WoWPetSpell(spellMasks[i], i);
                    Logger.Write("Adding pet spell " + spell + " at button #" + spell.ActionBarIndex);
                    _petSpells.Add(spell);
                }
            }
        }

        static PetManager()
        {
            // NOTE: This is a bit hackish. This fires VERY OFTEN in major cities. But should prevent us from summoning right after dismounting.
            Lua.Events.AttachEvent("COMPANION_UPDATE", (s, e) => CallPetTimer.Reset());
        }

        public static PetType CurrentPetType
        {
            get
            {
                WoWUnit myPet = StyxWoW.Me.Pet;
                if (myPet == null)
                {
                    return PetType.None;
                }
                WoWCache.CreatureCacheEntry c;
                myPet.GetCachedInfo(out c);
                return (PetType)c.FamilyID;
            }
        }

        public static bool HavePet { get { return StyxWoW.Me.GotAlivePet; } }

        public static bool CanCastPetAction(string action)
        {
            var spell = _petSpells.First(p => p.ToString() == action);
            if (spell == null)
                return false;

            return !spell.Cooldown;
        }

        public static void CastPetAction(string action)
        {
            Logger.Write(string.Format("[Pet] Casting {0}", action));
            Lua.DoString("CastPetAction({0})", _petSpells.First(p => p.ToString() == action).ActionBarIndex+1);
        }

        public static void CastPetAction(string action, WoWUnit on)
        {
            Logger.Write(string.Format("[Pet] Casting {0} on {1}", action, on.SafeName()));
            StyxWoW.Me.SetFocus(on);
            Lua.DoString("CastPetAction({0}, 'focus')", _petSpells.First(p => p.ToString() == action).ActionBarIndex+1);
            StyxWoW.Me.SetFocus(0);
        }

        /// <summary>
        ///   Calls a pet by name, if applicable.
        /// </summary>
        /// <remarks>
        ///   Created 2/7/2011.
        /// </remarks>
        /// <param name = "petName">Name of the pet. This parameter is ignored for mages. Warlocks should pass only the name of the pet. Hunters should pass which pet (1, 2, etc)</param>
        /// <returns>true if it succeeds, false if it fails.</returns>
        public static bool CallPet(string petName)
        {
            if (!CallPetTimer.IsFinished)
            {
                return false;
            }

            switch (StyxWoW.Me.Class)
            {
                case WoWClass.Warlock:
                    if (SpellManager.CanCast("Summon " + petName))
                    {
                        Logger.Write(string.Format("[Pet] Calling out my {0}", petName));
                        return SpellManager.Cast("Summon " + petName);
                    }
                    break;

                case WoWClass.Mage:
                    if (SpellManager.CanCast("Summon Water Elemental"))
                    {
                        Logger.Write("[Pet] Calling out Water Elemental");
                        return SpellManager.Cast("Summon Water Elemental");
                    }
                    break;

                case WoWClass.Hunter:
                    if (SpellManager.CanCast("Call Pet " + petName))
                    {
                        if (!StyxWoW.Me.GotAlivePet)
                        {
                            Logger.Write(string.Format("[Pet] Calling out pet #{0}", petName));
                            return SpellManager.Cast("Call Pet " + petName);
                        }
                    }
                    break;
            }
            return false;
        }
    }


}

#region TO BE REMOVED IN THE NEXT HB RELEASE

namespace Styx.Logic.Combat
{
    /// <summary>Defines a pet "action" spell. (From the action bar. All known pet actions.)</summary>
    /// <remarks>Created 3/18/2011.</remarks>
    public class WoWPetSpell
    {
        public enum PetSpellType
        {
            Unknown,
            Spell,
            Action,
            Stance
        }

        public enum PetAction
        {
            None = -1,
            Wait = 0,
            Follow,
            Attack,
            Dismiss,
            MoveTo
        }

        public enum PetStance
        {
            None = -1,
            Passive = 0,
            Defensive = 1,
            Aggressive = 2,
        }

        /// <summary>Gets the actual spell, if SpellType is "Spell"</summary>
        /// <value>The spell.</value>
        public WoWSpell Spell { get; private set; }

        /// <summary>Returns the type of spell this <see cref="WoWPetSpell"/> is for.</summary>
        /// <value>The type of the spell.</value>
        public PetSpellType SpellType { get; private set; }

        /// <summary>Gets the action type.</summary>
        /// <value>The action.</value>
        public PetAction Action { get; private set; }

        /// <summary>Gets the stance this spell sets the pet into..</summary>
        /// <value>The stance.</value>
        public PetStance Stance { get; private set; }

        /// <summary>Gets a value indicating whether the spell is on cooldown. Only valid if SpellType is "Spell".</summary>
        /// <value>true if cooldown, false if not.</value>
        public bool Cooldown { get { return Spell != null && !Spell.Cooldown; } }

        /// <summary>Gets the zero-based index of the action bar, where this spell resides.</summary>
        /// <value>The action bar index.</value>
        public int ActionBarIndex { get; private set; }

        private WoWPetSpell()
        {
            ActionBarIndex = -1;
            SpellType = PetSpellType.Unknown;
            Action = PetAction.None;
            Stance = PetStance.None;
        }
        internal WoWPetSpell(uint spellMask, int index)
            : this()
        {
            ActionBarIndex = index;
            var spellId = spellMask & 0xFFFFFF;
            switch ((spellMask >> 24) & 0x3F)
            {
                case 1u:
                case 8u:
                case 9u:
                case 10u:
                case 11u:
                case 12u:
                case 13u:
                case 14u:
                case 15u:
                case 16u:
                case 17u:
                    SpellType = PetSpellType.Spell;
                    Spell = WoWSpell.FromId((int)spellId);
                    break;

                case 6u:
                    SpellType = PetSpellType.Stance;
                    Stance = (PetStance)(spellMask & 0xFFFFFF);
                    break;

                case 7u:
                    SpellType = PetSpellType.Action;
                    Action = (PetAction)(spellMask & 0xFFFFFF);
                    break;
                default:
                    SpellType = PetSpellType.Unknown;
                    break;
            }
        }

        public override string ToString()
        {
            switch (SpellType)
            {
                case PetSpellType.Unknown:
                    return "Unknown";
                    break;
                case PetSpellType.Spell:
                    return Spell != null ? Spell.Name : "Unknown";
                    break;
                case PetSpellType.Action:
                    if (Action == PetAction.MoveTo)
                        return "Move To";
                    return Action.ToString();
                    break;
                case PetSpellType.Stance:
                    return Stance.ToString();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}

#endregion