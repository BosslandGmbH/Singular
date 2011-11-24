using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using TreeSharp;

namespace Singular.ClassSpecific.Mage
{
    public class Lowbie
    {
        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        public static Composite CreateLowbieMageCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true, "Frostbolt", "Fireball", "Fire Blast"),
                Common.CreateMagePolymorphOnAddBehavior(),
                Spell.Cast("Arcane Missiles", ret => StyxWoW.Me.Auras.ContainsKey("Arcane Missiles!")),
                Spell.Cast("Fireball", ret => !SpellManager.HasSpell("Frostbolt")),
                Spell.Cast("Fire Blast", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 10),
                Spell.Cast("Frostbolt"),
                Helpers.Common.CreateUseWand(),
                Movement.CreateMoveToTargetBehavior(true, 25f)
                );
        }
    }
}
