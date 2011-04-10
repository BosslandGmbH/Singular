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
        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateLowbieDeathKnightCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateAutoAttack(true),
                CreateFaceUnit(),
                CreateSpellCast("Death Grip", ret => Me.CurrentTarget.Distance > 15),
                CreateSpellCast("Death Coil", false),
                CreateSpellCast("Icy Touch", false),
                CreateMoveToAndFace(5f, ret => Me.CurrentTarget),
                CreateSpellCast("Blood Strike"),
                CreateSpellCast("Plague Strike")
                );
        }
    }
}