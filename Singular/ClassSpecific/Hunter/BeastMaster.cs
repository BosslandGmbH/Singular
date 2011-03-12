using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Combat.CombatRoutine;

using TreeSharp;
using CommonBehaviors.Actions;
using Styx;
using System.Threading;

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
            WantedPet = "1";
            return new PrioritySelector(
                new Decorator(
                    ret => !Me.GotAlivePet,
                    new Action(ret => PetManager.CallPet(WantedPet))),
                CreateEnsureTarget(),
                CreateHunterBackPedal(),
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateMoveToAndFace(35f, ret => Me.CurrentTarget),
                //Intimidation
                CreateSpellCast("Intimidation", ret => Me.CurrentTarget.IsAlive && Me.GotAlivePet &&
                    (Me.CurrentTarget.CurrentTarget == null || Me.CurrentTarget.CurrentTarget == Me)),
                // Always keep it up on our target!
                CreateSpellBuff("Hunter's Mark"),
                // Heal pet when below 70
                CreateSpellCast("Mend Pet", ret => Me.Pet.HealthPercent < 70 && !Me.Pet.HasAura("Mend Pet")),
                CreateSpellCast("Concussive Shot",
                    ret => Me.CurrentTarget.CurrentTarget == null || Me.CurrentTarget.CurrentTarget == Me),
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
                CreateSpellCast("Arcane Shot"),
                // For when we have no Focus
                CreateSpellCast("Steady Shot"),
                CreateSpellCast("Raptor Strike", ret => Me.CurrentTarget.DistanceSqr < 5 * 5),
                CreateAutoAttack(true)
                );
        }
    }

}