using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateLowbieMageCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateMoveToAndFace(30f, ret => Me.CurrentTarget),
                CreateAutoAttack(true),
                CreateWaitForCast(true),
                CreateSpellCast("Arcane Missiles", ret => Me.Auras.ContainsKey("Arcane Missiles!")),
                CreateSpellCast("Fireball", ret => !SpellManager.HasSpell("Frostbolt")),
                CreateSpellBuff("Fire Blast", ret => SpellManager.CanCast("Fire Blast") && Me.CurrentTarget.HealthPercent < 10),
                CreateSpellCast("Frostbolt")
                );
        }
    }
}