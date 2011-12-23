using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx.Combat.CombatRoutine;
using TreeSharp;

namespace Singular.ClassSpecific.Shaman
{
    class Lowbie
    {
        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs | BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateShamanLowbieBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Lightning Shield"));
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateShamanLowbiePull()
        {
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.WaitForCast(true),
                    Spell.Cast("Lightning Bolt"),
                    Movement.CreateMoveToTargetBehavior(true, 20f));
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbieCombat()
        {
            return 
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.WaitForCast(true),
                    Common.CreateAutoAttack(true),
                    Restoration.CreateRestoShamanHealingOnlyBehavior(true),
                    Spell.Cast("Earth Shock"),
                    Spell.Cast("Lightning Bolt"),
                    // Should use melee when out of mana
                    Spell.Cast("Primal Strike"),
                    Movement.CreateMoveToTargetBehavior(true, 20f)
                    );
        }
    }
}
