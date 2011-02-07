using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWCache;
using Styx.WoWInternals.WoWObjects;

namespace Singular
{
    public enum PetAction
    {
        // Yep
        Attack = 1,
        Follow = 2,
        MoveTo = 3,

        // Felguard
        LegionStrike = 4,
        Pursuit = 5,
        AxeToss = 6,

        // Felpup
        DevourMagic = 4,
        FelIntelligence = 5,
        ShadowBite = 6,
        SpellLock = 7,

        // Succubus
        LashOfPain = 4,
        Seduction = 5,
        Whiplash = 6,
        LesserInvisibility = 7,

        // Void
        Sacrifice = 4,
        Torment = 5,
        Suffering = 6,
        ConsumeShadows = 7,

        // Imp
        Firebolt = 4,
        BloodPact = 5,
        Flee = 7,


        // Infernal
        Immolation = 4, // Needs update

        // Doomguard
        RainOfFire = 4, // Needs update
        DispelMagic = 5, // Needs update
        WarStomp = 6, // Needs update
        Cripple = 7, // Needs update


        // Water Elemental
        Freeze = 4,
        Waterbolt = 5,

        // TODO: Shaman pets


        // Stances
        Aggressive = 8,
        Defensive = 9,
        Passive = 10,
    }
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
    class PetManager
    {
        public static PetType CurrentPetType
        {
            get
            {
                var myPet = StyxWoW.Me.Pet;
                if (myPet == null)
                    return PetType.None;
                WoWCache.CreatureCacheEntry c;
                myPet.GetCachedInfo(out c);
                return (PetType)c.FamilyID;
            }
        }
        
        public static bool HavePet
        {
            get { return StyxWoW.Me.GotAlivePet; }
        }

        public static void CastPetAction(PetAction action)
        {
            Logger.Write("[Pet] Casting " + action.ToString().CamelToSpaced());
            Lua.DoString("CastPetAction({0})", (int)action);
        }

        public static void CastPetAction(PetAction action, WoWUnit on)
        {
            Logger.Write("[Pet] Casting " + action.ToString().CamelToSpaced());
            StyxWoW.Me.SetFocus(on);
            Lua.DoString("CastPetAction({0}, 'focus')", (int)action);
            StyxWoW.Me.SetFocus(0);
        }

        /// <summary>Calls a pet by name, if applicable.</summary>
        /// <remarks>Created 2/7/2011.</remarks>
        /// <param name="petName">Name of the pet. This parameter is ignored for mages. Warlocks should pass only the name of the pet. Hunters should pass which pet (1, 2, etc)</param>
        /// <returns>true if it succeeds, false if it fails.</returns>
        public static bool CallPet(string petName)
        {
            switch (StyxWoW.Me.Class)
            {
                case WoWClass.Warlock:
                    if (SpellManager.CanCast("Summon " + petName))
                    {
                        Logging.Write("[Singular][Pet] Calling out my " + petName);
                        return SpellManager.Cast("Summon " + petName);
                    }
                    break;

                case WoWClass.Mage:
                    if (SpellManager.CanCast("Summon Water Elemental"))
                    {
                        Logging.Write("[Singular][Pet] Calling out Water Elemental");
                        return SpellManager.Cast("Summon Water Elemental");
                    }
                    break;

                case WoWClass.Hunter:
                    if (SpellManager.CanCast("Call Pet " + petName))
                    {
                        Logging.Write("[Singular][Pet] Calling out pet #" + petName);
                        return SpellManager.Cast("Call Pet " + petName);
                    }
                    break;
            }
            return false;
        }
    }
}
