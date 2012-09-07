using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.TreeSharp;


namespace Singular.ClassSpecific.Paladin
{
    public class Lowbie
    {
        [Class(WoWClass.Paladin)]
        [Spec(WoWSpec.None)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbiePaladinCombat()
        {
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateAutoAttack(true),
                    Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                    Spell.Cast("Crusader Strike"),
                    Spell.Cast("Judgment"),
                    Movement.CreateMoveToTargetBehavior(true, 5f)
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(WoWSpec.None)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbiePaladinPull()
        {
            return
                new PrioritySelector(
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateAutoAttack(true),
                    Spell.Cast("Judgment"),
                    Movement.CreateMoveToTargetBehavior(true, 5f)
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(WoWSpec.None)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbiePaladinHeal()
        {
            return
                new PrioritySelector(
                    Spell.Heal("Word of Glory", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 50),
                    Spell.Heal("Holy Light", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 40)
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(WoWSpec.None)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbiePaladinPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Seal of Righteousness")
                    //Spell.BuffSelf("Devotion Aura")
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(WoWSpec.None)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbiePaladinCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Seal of Righteousness")
                    //Spell.BuffSelf("Devotion Aura")
                    );
        }
    }
}
