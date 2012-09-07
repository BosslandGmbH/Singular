using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.TreeSharp;

namespace Singular.ClassSpecific.Shaman
{
    class Lowbie
    {
        [Class(WoWClass.Shaman)]
        [Spec(WoWSpec.None)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateShamanLowbiePreCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Lightning Shield"));
        }

        [Class(WoWClass.Shaman)]
        [Spec(WoWSpec.None)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateShamanLowbieCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Lightning Shield"));
        }

        [Class(WoWClass.Shaman)]
        [Spec(WoWSpec.None)]
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
        [Spec(WoWSpec.None)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.All)]
        public static Composite CreateShamanLowbieHeal()
        {
            return
                new PrioritySelector(
                    Spell.Heal("Healing Wave", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 60)
                    );
        }

        [Class(WoWClass.Shaman)]
        [Spec(WoWSpec.None)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateShamanLowbieCombat()
        {
            return 
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.WaitForCast(true),
                    Common.CreateAutoAttack(true),
                    Spell.Cast("Earth Shock"),      // always use
                    Spell.Cast("Primal Strike"),    // always use
                    Spell.Cast("Lightning Bolt"),                   
                    Movement.CreateMoveToTargetBehavior(true, 20f)
                    );
        }
    }
}
