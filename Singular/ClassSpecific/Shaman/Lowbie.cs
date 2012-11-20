using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.TreeSharp;
using Singular.Settings;

namespace Singular.ClassSpecific.Shaman
{
    class Lowbie
    {
        [Behavior(BehaviorType.PreCombatBuffs | BehaviorType.CombatBuffs, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbiePreCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Lightning Shield"));
        }
        [Behavior(BehaviorType.Pull, WoWClass.Shaman, 0)]
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
        [Behavior(BehaviorType.Heal, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbieHeal()
        {
            return
                new PrioritySelector(
                    Spell.Heal("Healing Surge", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 60)
                    );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbieCombat()
        {
            return 
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.WaitForCast(true),
                    Helpers.Common.CreateAutoAttack(true),
                    Spell.Cast("Earth Shock"),      // always use
                    Spell.Cast("Primal Strike"),    // always use
                    Spell.Cast("Lightning Bolt"),                   
                    Movement.CreateMoveToTargetBehavior(true, 25f)
                    );
        }
    }
}
