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
		[Spec(TalentSpec.BloodDeathKnight)]
		[Behavior(BehaviorType.Combat)]
		[Context(WoWContext.All)]
		public Composite CreateBloodDeathKnightCombat()
		{
			NeedTankTargeting = true;
			return new PrioritySelector(
				CreateEnsureTarget(),
				CreateAutoAttack(true),
				CreateLosAndFace(ret => Me.CurrentTarget),
				CreateSpellCast("Death Grip", ret => Me.CurrentTarget.Distance > 15),
				//Make sure we're in range, and facing the damned target. (LOS check as well)
				CreateRangeAndFace(5f, ret => Me.CurrentTarget),
				CreateSpellBuffOnSelf("Bone Shield"),
				CreateSpellCast("Rune Strike"),
				CreateSpellCast("Mind Freeze", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
				CreateSpellCast("Strangulate", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
				CreateSpellBuffOnSelf("RuneTap", ret => Me.HealthPercent <= 60),
				CreateSpellCast(
					"Pestilence", ret => Me.CurrentTarget.HasAura("Blood Plague") && Me.CurrentTarget.HasAura("Frost Fever") &&
										 (from add in NearbyUnfriendlyUnits
										  where !add.HasAura("Blood Plague") && !add.HasAura("Frost Fever") && add.Distance < 10
										  select add).Count() > 0),
				new Decorator(
					ret => SpellManager.CanCast("Death and Decay") && NearbyUnfriendlyUnits.Count(a => a.Distance < 8) > 1,
					new Action(
						ret =>
						{
							SpellManager.Cast("Death and Decay");
							LegacySpellManager.ClickRemoteLocation(Me.CurrentTarget.Location);
						})),
				CreateSpellCast("Icy Touch"),
				CreateSpellCast("Plague Strike", ret => Me.CurrentTarget.HasAura("Blood Plague")),
				CreateSpellCast("Death Strike", ret => Me.HealthPercent < 80),
				CreateSpellCast("Blood Boil", ret => NearbyUnfriendlyUnits.Count(a => a.Distance < 8) > 1),
				CreateSpellCast("Heart Strike"),
				CreateSpellCast("Death Coil"));
		}
	}
}