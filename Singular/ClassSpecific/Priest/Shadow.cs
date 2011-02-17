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

using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateShadowPriestCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateRangeAndFace(30, ret => Me.CurrentTarget),
                CreateWaitForCast(),
                CreateSpellBuff("Shadow Word: Pain"),
                CreateSpellBuff("Devouring Plague"),
                CreateSpellBuff("Vampiric Touch"),
                CreateSpellBuff("Archangel", ret => HasAuraStacks("Evangelism", 5)),
                CreateSpellCast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent < 25),
                CreateSpellCast("Shadow Fiend"),
                CreateSpellCast("Mind Blast"),
                CreateSpellCast("Mind Flay")
                );
        }
    }
}