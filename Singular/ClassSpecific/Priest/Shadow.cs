using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateShadowPriestCombat()
        {
            return new PrioritySelector(

                CreateSpellBuff("Shadow Word: Pain"),
                CreateSpellBuff("Devouring Plague"),
                CreateSpellBuff("Vampiric Touch"),
                CreateSpellBuff("Archangel", ret => HasAuraStacks("Evangelism", 5)),
                CreateSpellCast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent < 25),
                CreateSpellCast("Shadow Fiend"),
                CreateSpellCast("Mind Blast"),
                CreateSpellCast("Mind Flay")

                );
        }
    }
}
