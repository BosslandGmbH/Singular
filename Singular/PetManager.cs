using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular
{
    class PetManager
    {
        public static bool HavePet
        {
            get { return StyxWoW.Me.GotAlivePet; }
        }

        public static bool CallPet(string petName)
        {
            switch (StyxWoW.Me.Class)
            {
                case WoWClass.Warlock:
                    break;

                case WoWClass.Mage:
                    if (SpellManager.CanCast("Frost Elemental"))
                    {
                        Logging.Write("[Singular][Pet] Calling out Frost Elemental");
                        return SpellManager.Cast("Frost Elemental");
                    }
                    break;

                case WoWClass.Hunter:
                    break;
            }
            return false;
        }

        private WoWPlayer CT;
        public Composite ApproachToCast(string spellName)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => CT.IsValid && (CT.Distance > Math.Max(SpellManager.Spells[spellName].MaxRange - 2f, 4f) || !CT.InLineOfSight),
                    new Action(ret => Navigator.MoveTo(CT.Location))),
                new Action(ret => Navigator.PlayerMover.MoveStop()));
        }
    }
}
