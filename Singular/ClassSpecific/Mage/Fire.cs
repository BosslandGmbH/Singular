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

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        public Composite CreateFireMageCombat()
        {
            return new PrioritySelector(
                CreateSpellCast(
                    "Scorch",
                    ret =>
                    !Me.CurrentTarget.HasAura("Critical Mass") || Me.CurrentTarget.Auras["Critical Mass"].TimeLeft.TotalSeconds < 3 ||
                    // If we have the Firestarter buff, we can cast scorch on the move. Do so please!
                    (Me.IsMoving && TalentManager.GetCount(2, 15) != 0)),
                CreateSpellCast("Pyroblast", ret => Me.HasAura("Hot Streak")),
                CreateSpellCast("Fire Blast", ret => Me.HasAura("Impact")),
                CreateSpellBuff("Living Bomb"),
                CreateSpellCast("Blast Wave"),
                CreateSpellCast("Fireball")
                );
        }
    }
}