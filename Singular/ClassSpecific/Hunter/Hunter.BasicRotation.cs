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
using Styx.Logic.Pathing;
using Styx.Helpers;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Hunter)]
		[Spec(TalentSpec.BeastMasteryHunter)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateBeastMasterCombat()
        {
            return new PrioritySelector(
				CreateEnsureTarget(),
				CreateHunterBackPedal(),
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateRangeAndFace(35f, ret => Me.CurrentTarget),
                CreateAutoAttack(true),
                // Always keep it up on our target!
                CreateSpellBuff("Hunter's Mark"),
                // Heal pet when below 70
                CreateSpellCast("Mend Pet", ret => Me.Pet.HealthPercent < 70 && !Me.Pet.HasAura("Mend Pet")),
                //Rapid fire on elite 
                CreateSpellBuffOnSelf("Rapid Fire", ret => CurrentTargetIsElite),
                CreateSpellBuff("Serpent Sting"),
                // Ignore these two when our pet is raging
                CreateSpellCast("Focus Fire", ret => !Me.Pet.HasAura("Bestial Wrath")),
                CreateSpellCast("Kill Shot", ret => !Me.Pet.HasAura("Bestial Wrath")),
                // Basically, cast it whenever its up.
                CreateSpellCast("Kill Command"),
                // Only really cast this when we need a sting refresh.
                CreateSpellCast(
                    "Cobra Shot",
                    ret => Me.CurrentTarget.HasAura("Serpent Sting") && Me.CurrentTarget.Auras["Serpent Sting"].TimeLeft.TotalSeconds < 3),
                // Focus dump on arcane shot, unless our pet has bestial wrath, then we use it for better DPS
                CreateSpellCast("Arcane Shot")
                );
        }

        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.SurvivalHunter)]
        [Behavior(BehaviorType.Combat)]
		[Behavior(BehaviorType.Pull)]
        public Composite CreateSurvivalCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
				CreateHunterBackPedal(),
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateRangeAndFace(35f, ret => Me.CurrentTarget),
                CreateAutoAttack(true),
                // Always keep it up on our target!
                CreateSpellBuff("Hunter's Mark"),
                // Heal pet when below 70
                CreateSpellCast("Mend Pet", ret => Me.Pet.HealthPercent < 70 && !Me.Pet.HasAura("Mend Pet")),
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
				CreateSpellCast("Arcane Shot")
                );
        }
    }
}