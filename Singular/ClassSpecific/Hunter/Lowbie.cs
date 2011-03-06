using Styx.Combat.CombatRoutine;

using TreeSharp;
using Styx.Logic.Pathing;
using Styx.Helpers;

namespace Singular
{
	partial class SingularRoutine
	{
		[Class(WoWClass.Hunter)]
		[Spec(TalentSpec.Lowbie)]
		[Behavior(BehaviorType.Combat)]
		[Behavior(BehaviorType.Pull)]
		[Context(WoWContext.All)]
		public Composite CreateLowbieCombat()
		{
            WantedPet = "1";
			return new PrioritySelector(
				CreateEnsureTarget(),
				CreateWaitForCast(true),
				CreateHunterBackPedal(),
				CreateMoveToAndFace(35f, ret => Me.CurrentTarget),
				CreateAutoAttack(true),
				CreateSpellCast("Raptor Strike", ret => Me.CurrentTarget.DistanceSqr < 5 * 5),
				// Always keep it up on our target!
				CreateSpellBuff("Hunter's Mark"),
				// Heal pet when below 70
				CreateSpellCast("Mend Pet", ret => Me.Pet.HealthPercent < 70 && !Me.Pet.HasAura("Mend Pet")),
				CreateSpellCast("Concussive Shot", 
					ret => Me.CurrentTarget.CurrentTarget == null || Me.CurrentTarget.CurrentTarget == Me),
				CreateSpellCast("Arcane Shot"),
				CreateSpellCast("Steady Shot")
				);
		}
	}
}