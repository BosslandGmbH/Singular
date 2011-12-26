using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public class Common
    {
        public static ShapeshiftForm WantedDruidForm { get; set; }

        [Class(WoWClass.Druid)]
        [Behavior(BehaviorType.OutOfCombat)]
        [Spec(TalentSpec.BalanceDruid)]
        [Spec(TalentSpec.FeralDruid)]
        [Spec(TalentSpec.FeralTankDruid)]
        [Spec(TalentSpec.RestorationDruid)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]

        public static Composite CreateDruidOutOfCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Mark of the Wild")
                );
        }

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
                   ret =>
                   (Unit.NearbyFriendlyPlayers.Any(
                       unit =>
                       !unit.Dead && !unit.IsGhost && unit.IsInMyPartyOrRaid &&
                       !unit.HasAnyAura("Mark of the Wild", "Embrace of the Shale Spider", "Blessing of Kings")))
                       || !StyxWoW.Me.HasAnyAura("Mark of the Wild", "Embrace of the Shale Spider", "Blessing of Kings"))
               );
        }
    }
}
