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
            AddSpellSucceedWait("Vampiric Touch");
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateMoveToAndFace(30, ret => Me.CurrentTarget),
                CreateWaitForCast(true),
                CreateSpellBuff("Shadow Word: Pain"),
                CreateSpellBuff("Devouring Plague"),
                CreateSpellBuff("Vampiric Touch", ret => !Me.IsMoving),
                CreateSpellBuff("Archangel", ret => HasAuraStacks("Evangelism", 5) && Me.ManaPercent <= 75),
                CreateSpellCast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent < 25),
                CreateSpellCast("Shadow Fiend", ret => Me.ManaPercent < 50),
                CreateSpellCast("Mind Blast", ret => Me.HasAura("Empowered Shadows")),
                CreateSpellCast("Mind Flay", ret => !Me.IsMoving)
                );
        }
    }
}