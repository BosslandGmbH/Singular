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
        #region Pull

        // All DKs should be throwing death grip when not in intances. It just speeds things up, and makes a mess for PVP :)
        [Class(WoWClass.DeathKnight)]
        [Behavior(BehaviorType.Pull)]
		[Spec(TalentSpec.BloodDeathKnight)]
		[Spec(TalentSpec.FrostDeathKnight)]
        [Spec(TalentSpec.UnholyDeathKnight)]
		[Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.Battlegrounds|WoWContext.Normal)]
        public Composite CreateDeathKnightPvpNormalPull()
        {
            return
                new PrioritySelector(
					CreateFaceUnit(),
                    CreateSpellCast("Death Grip", ret => Me.CurrentTarget.Distance > 15, false),
                    CreateSpellCast("Howling Blast", false),
                    CreateSpellCast("Icy Touch", false),
					CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
                    );
        }

        // Non-blood DKs shouldn't be using Death Grip in instances. Only tanks should!
        // You also shouldn't be a blood DK if you're DPSing. Thats just silly. (Like taking a prot war as DPS... you just don't do it)
        [Class(WoWClass.DeathKnight)]
        [Behavior(BehaviorType.Pull)]
        [Spec(TalentSpec.FrostDeathKnight)]
        [Spec(TalentSpec.UnholyDeathKnight)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.Instances)]
        public Composite CreateDeathKnightInstancePull()
        {
            return
                new PrioritySelector(
                    CreateFaceUnit(),
                    CreateSpellCast("Howling Blast", false),
                    CreateSpellCast("Icy Touch", false),
                    CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
                    );
        }

        // Blood DKs should be DG'ing everything it can when pulling. ONLY IN INSTANCES.
        [Class(WoWClass.DeathKnight)]
        [Behavior(BehaviorType.Pull)]
        [Spec(TalentSpec.BloodDeathKnight)]
        [Context(WoWContext.Instances)]
        public Composite CreateBloodDeathKnightInstancePull()
        {
            return
                new PrioritySelector(
                    CreateFaceUnit(),
                    CreateSpellCast("Death Grip", ret => Me.CurrentTarget.Distance > 15, false),
                    CreateSpellCast("Howling Blast", false),
                    CreateSpellCast("Icy Touch", false),
                    CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
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
            // Note: This is one of few places where this is slightly more valid than making multiple functions.
            // Since this type of stuff is shared, we are safe to do this. Jus leave as-is.
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