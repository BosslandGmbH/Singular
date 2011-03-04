#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author$ nuok
// $Date$
// $HeadURL$
// $LastChangedBy$
// $LastChangedDate$
// $LastChangedRevision$
// $Revision$

#endregion

using System.Linq;

using Styx.Combat.CombatRoutine;

using TreeSharp;
using Styx;
using Styx.Logic.Combat;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        public Composite CreateLowbieWarriorCombat()
        {
            return new PrioritySelector(
					CreateEnsureTarget(),
					CreateAutoAttack(true),
					CreateMoveToAndFace(5f, ret => Me.CurrentTarget),
				CreateSpellCast("Charge"),
				CreateSpellCast("Victory Rush"),
				CreateSpellCast("Strike")
                );
        }

		[Class(WoWClass.Warrior)]
		[Spec(TalentSpec.Lowbie)]
		[Context(WoWContext.All)]
		[Behavior(BehaviorType.Pull)]
		public Composite CreateLowbieWarriorPull()
		{
			return 
					new PrioritySelector(
					CreateEnsureTarget(),
					CreateAutoAttack(true),
					CreateMoveToAndFace(5f, ret => Me.CurrentTarget),
					CreateSpellCast("Charge"),
					CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
					);
		}

		[Class(WoWClass.Warrior)]
		[Spec(TalentSpec.Lowbie)]
		[Context(WoWContext.All)]
		[Behavior(BehaviorType.CombatBuffs)]
		public Composite CreateLowbieWarriorCombatBuffs()
		{
			return
				new PrioritySelector(
					CreateSpellBuffOnSelf("Berserker Rage",
							ret => Me.Auras.Any(
								aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Fleeing ||
										aura.Value.Spell.Mechanic == WoWSpellMechanic.Sapped ||
										aura.Value.Spell.Mechanic == WoWSpellMechanic.Incapacitated ||
										aura.Value.Spell.Mechanic == WoWSpellMechanic.Horrified)),
					CreateSpellBuffOnSelf("Battle Shout", ret => !Me.HasAura("Horn of the Winter") &&
																 !Me.HasAura("Roar of Courage") &&
																 !Me.HasAura("Strength of Earth Totem"))
				);
		}

		[Class(WoWClass.Warrior)]
		[Spec(TalentSpec.Lowbie)]
		[Context(WoWContext.All)]
		[Behavior(BehaviorType.PreCombatBuffs)]
		public Composite CreateLowbieWarriorPreCombatBuffs()
		{
			return
				new PrioritySelector(
					CreateSpellBuffOnSelf("Battle Stance")
				);
		}
    }
}