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
        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.SurvivalHunter)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateSurvivalCombat()
        {
            WantedPet = "1";
            return new PrioritySelector(
                new Decorator(
                    ret => !Me.GotAlivePet,
                    new Action(ret => PetManager.CallPet(WantedPet))),
                CreateEnsureTarget(),
                CreateHunterBackPedal(),
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateMoveToAndFace(35f, ret => Me.CurrentTarget),
                // Always keep it up on our target!
                CreateSpellBuff("Hunter's Mark"),
                // Heal pet when below 70
                CreateSpellCast("Mend Pet", ret => Me.Pet.HealthPercent < 70 && !Me.Pet.HasAura("Mend Pet")),
                CreateSpellCast(
                    "Concussive Shot",
                    ret => Me.CurrentTarget.CurrentTarget == null || Me.CurrentTarget.CurrentTarget == Me),
                //Rapid fire on elite 
                CreateSpellBuff("Rapid Fire", ret => CurrentTargetIsElite),
                //Cast when mob Hp below 20
                CreateSpellCast("Kill Shot", ret => Me.CurrentTarget.HealthPercent < 19),
                new Decorator(
                    ret => !Me.HasAura("Lock and Load"),
                    new PrioritySelector(
                        // The extra here 'flips' the explosive usage.
                        CreateSpellCast("Kill Command", ret => Me.FocusPercent == 100),
                        CreateSpellCast("Explosive Shot", ret => LastSpellCast != "Explosive Shot"),
                        CreateSpellCast("Steady Shot", ret => LastSpellCast != "Steady Shot"))),
                // Refresh when it wears off.
                CreateSpellBuff("Serpent Sting", ret => !Me.CurrentTarget.HasAura("Serpent Sting")),
                // Whenever it's not on CD
                CreateSpellCast("Explosive Shot"),
                // Whenever its not on CD
                CreateSpellCast("Black Arrow"),
                // Main DPS filler
                CreateSpellCast("Steady Shot"),
                CreateSpellCast("Arcane Shot"),
                CreateAutoAttack(true)
                );
        }
    }
}