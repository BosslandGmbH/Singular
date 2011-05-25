using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public static class Common
    {
        public static ShapeshiftForm WantedDruidForm { get; set; }

        [Class(WoWClass.Druid)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Spec(TalentSpec.BalanceDruid)]
        [Spec(TalentSpec.FeralDruid)]
        [Spec(TalentSpec.FeralTankDruid)]
        [Spec(TalentSpec.RestorationDruid)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]
        public static Composite CreateDruidBuffComposite()
        {
            return new PrioritySelector(
                Spell.Cast(
                    "Mark of the Wild",
                    ret => StyxWoW.Me,
                    ret => Unit.NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && u.IsInMyPartyOrRaid && CanCastMotWOn(u)))
                // TODO: Have it buff MotW when nearby party/raid members are missing the buff.
                );
        }

        public static bool CanCastMotWOn(WoWUnit unit)
        {
            return !unit.HasAura("Mark of the Wild") &&
                   !unit.HasAura("Embrace of the Shale Spider") &&
                   !unit.HasAura("Blessing of Kings");
        }
    }
}
