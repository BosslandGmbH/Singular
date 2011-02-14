using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx;
using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        public ShapeshiftForm WantedDruidForm { get; set; }

        [Class(WoWClass.Druid)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Spec(TalentSpec.BalanceDruid)]
        [Spec(TalentSpec.FeralDruid)]
        [Spec(TalentSpec.FeralTankDruid)]
        [Spec(TalentSpec.RestorationDruid)]
        [Context(WoWContext.All)]
        public Composite CreateDruidBuffComposite()
        {
            return new PrioritySelector(
                CreateSpellBuffOnSelf("Mark of the Wild")

                // TODO: Have it buff MotW when nearby party/raid members are missing the buff.
                );
        }
    }
}
