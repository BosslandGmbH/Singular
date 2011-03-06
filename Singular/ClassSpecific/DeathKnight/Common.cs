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