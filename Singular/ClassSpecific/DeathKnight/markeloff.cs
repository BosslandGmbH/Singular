#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author$
// $Date$
// $HeadURL$
// $LastChangedBy$
// $LastChangedDate$
// $LastChangedRevision$
// $Revision$

#endregion

using System.Linq;

using Singular.Composites;

using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        #region Combat

        #region Unholy

        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.UnholyDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateUnholyDeathKnightCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                //Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateRangeAndFace(5f, ret => Me.CurrentTarget),
                CreateAutoAttack(true),
                CreateSpellCast("Raise Dead", ret => !Me.GotAlivePet),
                CreateSpellCast("Rune Strike"),
                CreateSpellCast("Mind Freeze", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
                CreateSpellCast("Strangulate", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
                CreateSpellCast("Unholy Frenzy", ret => Me.HealthPercent >= 80),
                CreateSpellCast("Death Strike", ret => Me.HealthPercent < 80),
                CreateSpellCast("Outbreak", ret => Me.CurrentTarget.HasAura("Frost Fever") || Me.CurrentTarget.HasAura("Blood Plague")),
                CreateSpellCast("Icy Touch"),
                CreateSpellCast("Plague Strike", ret => Me.CurrentTarget.HasAura("Blood Plague")),
                CreateSpellCast(
                    "Pestilence", ret => Me.CurrentTarget.HasAura("Blood Plague") && Me.CurrentTarget.HasAura("Frost Fever") &&
                                         (NearbyUnfriendlyUnits.Where(
                                             add => !add.HasAura("Blood Plague") && !add.HasAura("Frost Fever") && add.Distance < 10)).Count() > 0),
                new Decorator(
                    ret => SpellManager.CanCast("Death and Decay") && NearbyUnfriendlyUnits.Count(a => a.Distance < 8) > 1,
                    new Action(
                        ret =>
                            {
                                SpellManager.Cast("Death and Decay");
                                LegacySpellManager.ClickRemoteLocation(Me.CurrentTarget.Location);
                            })),
                CreateSpellCast("Summon Gargoyle"),
                CreateSpellCast("Dark Transformation", ret => Me.GotAlivePet && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
                CreateSpellCast("Scourge Strike", ret => Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2),
                CreateSpellCast("Festering Strike", ret => Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2),
                CreateSpellCast("Death Coil", ret => Me.ActiveAuras.ContainsKey("Sudden Doom") || Me.CurrentRunicPower >= 80),
                CreateSpellCast("Scourge Strike"),
                CreateSpellCast("Festering Strike"),
                CreateSpellCast("Death Coil"));
        }

        #endregion

        #region Blood

        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.BloodDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateBloodDeathKnightCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                //Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateRangeAndFace(5f, ret => Me.CurrentTarget),
                CreateAutoAttack(true),
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

        #endregion

        #region Frost

        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.FrostDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateFrostDeathKnightCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                //Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateRangeAndFace(5f, ret => Me.CurrentTarget),
                CreateAutoAttack(true),
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

        #endregion

        #region Lowbie

        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateLowbieDeathKnightCombat()
        {
            return new PrioritySelector(
				CreateEnsureTarget(),
				CreateLosAndFace(ret => Me.CurrentTarget, 70),
                CreateSpellCast("Death Coil", false),
                CreateSpellCast("Icy Touch", false),
				CreateRangeAndFace(5f, ret => Me.CurrentTarget),
                CreateSpellCast("Blood Strike"),
                CreateSpellCast("Plague Strike")
                );
        }

        #endregion

        #endregion

        #region Pull

        [Class(WoWClass.DeathKnight)]
        [Behavior(BehaviorType.Pull)]
		[Spec(TalentSpec.BloodDeathKnight)]
		[Spec(TalentSpec.FrostDeathKnight)]
        [Spec(TalentSpec.UnholyDeathKnight)]
		[Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]
        public Composite CreateDeathKnightPull()
        {
            return
                new PrioritySelector(
					CreateLosAndFace(ret => Me.CurrentTarget),
                    CreateSpellCast("Death Grip", ret => Me.CurrentTarget.Distance > 15, false),
                    CreateSpellCast("Howling Blast", false),
                    CreateSpellCast("Icy Touch", false),
					CreateRangeAndFace(5f, ret => Me.CurrentTarget)
                    );
        }

        #endregion

        #region PreCombatBuffs

		[Class(WoWClass.DeathKnight)]
		[Behavior(BehaviorType.PreCombatBuffs)]
		[Spec(TalentSpec.BloodDeathKnight)]
		[Spec(TalentSpec.FrostDeathKnight)]
		[Spec(TalentSpec.UnholyDeathKnight)]
		[Spec(TalentSpec.Lowbie)]
		[Context(WoWContext.All)]
		public Composite CreateDeathKnightPreCombatBuffs()
		{
			return
				new PrioritySelector(
					CreateSpellBuffOnSelf(
						"Frost Presence",
						ret => TalentManager.CurrentSpec == TalentSpec.Lowbie),
					CreateSpellBuffOnSelf(
					    "Blood Presence",
					    ret => TalentManager.CurrentSpec == TalentSpec.BloodDeathKnight),
					CreateSpellBuffOnSelf(
					    "Unholy Presence",
					    ret => TalentManager.CurrentSpec == TalentSpec.UnholyDeathKnight || TalentManager.CurrentSpec == TalentSpec.FrostDeathKnight),
					CreateSpellBuffOnSelf(
					    "Horn of Winter",
					    ret => !Me.HasAura("Horn of Winter") && !Me.HasAura("Battle Shout") && !Me.HasAura("Roar of Courage"))
					);
		}

        #endregion
    }
}