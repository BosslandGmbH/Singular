using System.Linq;

using Singular.Composites;

using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
	partial class SingularRoutine
	{
		[Class(WoWClass.DeathKnight)]
		[Spec(TalentSpec.Lowbie)]
		[Behavior(BehaviorType.Combat)]
		[Context(WoWContext.All)]
		public Composite CreateLowbieDeathKnightCombat()
		{
			return new PrioritySelector(
				CreateEnsureTarget(),
				CreateAutoAttack(true),
				CreateSpellCast("Death Grip", ret => Me.CurrentTarget.Distance > 15),
				CreateRangeAndFace(5f, ret => Me.CurrentTarget),
				CreateSpellCast("Death Coil", false),
				CreateSpellCast("Icy Touch", false),
				CreateSpellCast("Blood Strike"),
				CreateSpellCast("Plague Strike")
				);
		}
	}
}