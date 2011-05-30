using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using TreeSharp;

namespace Singular.ClassSpecific.Hunter
{
    public class Marksman
    {
        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.MarksmanshipHunter)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateMarksmanshipCombat()
        {
            PetManager.WantedPet = "1";
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Common.CreateHunterBackPedal(),
                Movement.CreateFaceTargetBehavior(),
                Waiters.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Spell.Cast("Raptor Strike", ret => StyxWoW.Me.CurrentTarget.DistanceSqr < 5 * 5),
                //Interupt
                Spell.Cast("Silencing Shot", ret => StyxWoW.Me.CurrentTarget.IsCasting),
                // Always keep it up on our target!
                Spell.Buff("Hunter's Mark"),
                // Heal pet when below 70
                Spell.Cast("Mend Pet", ret => StyxWoW.Me.Pet.HealthPercent < 70 && !StyxWoW.Me.Pet.HasAura("Mend Pet")),
                Spell.Cast(
                    "Concussive Shot",
                    ret => StyxWoW.Me.CurrentTarget.CurrentTarget == null || StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me),
                // Cast only when close to mob	to try and gain aggro with pet
                Spell.Cast("Kill Command", ret => StyxWoW.Me.CurrentTarget.DistanceSqr < 5 * 5),
                Spell.Buff("Serpent Sting"),
                Spell.Cast("Chimera Shot", ret => StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting")),
                Spell.Cast("Aimed Shot", ret => StyxWoW.Me.CurrentTarget.HealthPercent > 80 || StyxWoW.Me.Auras["Ready, Set, Aim..."].StackCount == 5),
                Spell.Cast("Arcane Shot"),
                Spell.Cast("Steady Shot"),
                Movement.CreateMoveToTargetBehavior(true,35f)
                );
        }

        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.MarksmanshipHunter)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateMarksmanshipPull()
        {
            PetManager.WantedPet = "1";
            return new PrioritySelector(
                Waiters.WaitForCast(true),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.Buff("Hunter's Mark"),
                new Decorator(
                    ret => !SpellManager.CanCast("Aimed Shot"),
                    Helpers.Common.CreateAutoAttack(true)),
                Spell.Cast("Aimed Shot"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }			
    }
}
