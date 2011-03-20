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
                CreateMoveToAndFace(30, ret => Me.CurrentTarget),
                CreateWaitForCast(true),
                CreateDiscHealOnlyBehavior(true),
                CreateSpellBuff("Vampiric Touch", ret => !Me.IsMoving, true),
                CreateSpellBuff("Devouring Plague"),
                CreateSpellBuff("Shadow Word: Pain"),
                CreateSpellBuff("Archangel", ret => HasAuraStacks("Evangelism", 5) && Me.ManaPercent <= 75),
                CreateSpellCast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent < 25),
                CreateSpellCast("Shadow Fiend", ret => Me.ManaPercent < 50),
                CreateSpellCast("Mind Blast", ret => Me.HasAura("Shadow Orb") && !Me.HasAura("Empowered Shadow")),
                CreateSpellCast("Mind Flay", ret => !Me.IsMoving)
                );
        }
    }
}