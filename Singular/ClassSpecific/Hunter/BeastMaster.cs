using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Hunter
{
    public class BeastMaster
    {
        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.BeastMasteryHunter)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateBeastMasterCombat()
        {
            PetManager.WantedPet = "1";
            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.GotAlivePet,
                    new Action(ret => PetManager.CallPet(PetManager.WantedPet))),
                Safers.EnsureTarget(),
                Helpers.Common.CreateAutoAttack(true),
                Movement.CreateMoveToLosBehavior(),
                Common.CreateHunterBackPedal(),
                Movement.CreateFaceTargetBehavior(),

                //Intimidation
                Spell.Cast(
                    "Intimidation", ret => StyxWoW.Me.CurrentTarget.IsAlive && StyxWoW.Me.GotAlivePet &&
                                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null || StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me)),
                // Always keep it up on our target!
                Spell.Buff("Hunter's Mark"),
                Common.CreateHunterTrapOnAddBehavior(),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.DistanceSqr < 5 * 5,
                    new PrioritySelector(
                        Spell.BuffSelf("Disengage"),
                        Spell.Cast("Raptor Strike")
                        )),
                // Heal pet when below 70
                Spell.Cast(
                    "Mend Pet",
                    ret =>
                    (StyxWoW.Me.Pet.HealthPercent < 70 || (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet"))) && !StyxWoW.Me.Pet.HasAura("Mend Pet")),
                Spell.Cast(
                    "Concussive Shot",
                    ret => StyxWoW.Me.CurrentTarget.CurrentTarget == null || StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me),
                //Rapid fire on elite 
                Spell.BuffSelf("Rapid Fire", ret => StyxWoW.Me.CurrentTarget.Elite),
                Spell.Buff("Serpent Sting"),
                // Ignore these two when our pet is raging
                Spell.Cast("Focus Fire", ret => !StyxWoW.Me.Pet.HasAura("Bestial Wrath")),
                Spell.Cast("Kill Shot", ret => !StyxWoW.Me.Pet.HasAura("Bestial Wrath")),
                // Basically, cast it whenever its up.
                Spell.Cast("Kill Command"),
                // Only really cast this when we need a sting refresh.
                Spell.Cast(
                    "Cobra Shot",
                    ret => StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting") && StyxWoW.Me.CurrentTarget.Auras["Serpent Sting"].TimeLeft.TotalSeconds < 3),
                // Focus dump on arcane shot, unless our pet has bestial wrath, then we use it for better DPS
                Spell.Cast("Arcane Shot"),
                // For when we have no Focus
                Spell.Cast("Steady Shot"),
                Movement.CreateMoveToTargetBehavior(true,35f)
                );
        }
    }
}
