using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;

namespace Singular
{
    class PetManager
    {
        public static bool HavePet
        {
            get { return StyxWoW.Me.GotAlivePet; }
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
                    if (SpellManager.CanCast("Frost Elemental"))
                    {
                        Logging.Write("[Singular][Pet] Calling out Frost Elemental");
                        return SpellManager.Cast("Frost Elemental");
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
