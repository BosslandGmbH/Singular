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
		[Spec(TalentSpec.FrostDeathKnight)]
		[Behavior(BehaviorType.Combat)]
		[Context(WoWContext.All)]
		public Composite CreateFrostDeathKnightCombat()
		{
			return new PrioritySelector(
				CreateEnsureTarget(),
				CreateAutoAttack(true),
				CreateLosAndFace(ret => Me.CurrentTarget),
				CreateSpellCast("Death Grip", ret => Me.CurrentTarget.Distance > 15),
				//Make sure we're in range, and facing the damned target. (LOS check as well)
				CreateRangeAndFace(5f, ret => Me.CurrentTarget),
				CreateSpellCast("Raise Dead", ret => !Me.GotAlivePet),
				CreateSpellCast("Rune Strike"),
				CreateSpellCast("Mind Freeze", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
				CreateSpellCast("Strangulate", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
				CreateSpellCast("Death Strike", ret => Me.HealthPercent < 80),
				CreateSpellCast("Pillar of Frost"),
				CreateSpellCast("Howling Blast", ret => Me.HasAura("Freezing Fog") || !Me.CurrentTarget.HasAura("Frost Fever")),
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
				CreateSpellCast("Outbreak", ret => Me.CurrentTarget.HasAura("Frost Fever") || Me.CurrentTarget.HasAura("Blood Plague")),
				CreateSpellCast("Plague Strike", ret => Me.CurrentTarget.HasAura("Blood Plague")),
				CreateSpellCast(
					"Obliterate",
					ret => (Me.FrostRuneCount == 2 && Me.UnholyRuneCount == 2) || Me.DeathRuneCount == 2 || Me.HasAura("Killing Machine")),
				CreateSpellCast("Blood Strike", ret => Me.BloodRuneCount == 2),
				CreateSpellCast("Frost Strike", ret => Me.HasAura("Freezing Fog") || Me.CurrentRunicPower == Me.MaxRunicPower),
				CreateSpellCast("Blood Tap", ret => Me.BloodRuneCount < 2),
				CreateSpellCast("Obliterate"),
				CreateSpellCast("Blood Strike"),
				CreateSpellCast("Frost Strike"));
		}
	}
}
