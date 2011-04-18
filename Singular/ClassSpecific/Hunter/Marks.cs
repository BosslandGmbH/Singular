#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author: Nuok $
// $Date: 2011-03-18 16:36:36 +0000 (Fri, 18 Mar 2011) $
// $HeadURL: http://svn.apocdev.com/singular/trunk/Singular/ClassSpecific/Hunter/Marksman.cs $
// $LastChangedBy: Nuok $

#endregion

using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.MarksmanshipHunter)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateMarksmanshipCombat()
        {
            WantedPet = "1";
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateWaitForCast(true),
				CreateAutoAttack(true),
                CreateHunterBackPedal(),
                CreateMoveToAndFace(35f, ret => Me.CurrentTarget),
                CreateSpellCast("Raptor Strike", ret => Me.CurrentTarget.DistanceSqr < 5 * 5),
				//Interupt
				CreateSpellCast("Silencing Shot", ret => Me.CurrentTarget.IsCasting),
                // Always keep it up on our target!
                CreateSpellBuff("Hunter's Mark"),
                // Heal pet when below 70
                CreateSpellCast("Mend Pet", ret => Me.Pet.HealthPercent < 70 && !Me.Pet.HasAura("Mend Pet")),
                CreateSpellCast(
                    "Concussive Shot",
                    ret => Me.CurrentTarget.CurrentTarget == null || Me.CurrentTarget.CurrentTarget == Me),
				// Cast only when close to mob	to try and gain aggro with pet
				CreateSpellCast("Kill Command", ret => Me.CurrentTarget.DistanceSqr < 5 * 5),
				CreateSpellBuff("Serpent Sting"),
				CreateSpellCast("Chimera Shot", ret => Me.CurrentTarget.HasAura("Serpent Sting")),
				CreateSpellCast("Aimed Shot", ret => Me.CurrentTarget.HealthPercent > 80 || Me.Auras["Ready, Set, Aim..."].StackCount == 5),
				CreateSpellCast("Arcane Shot"),
                CreateSpellCast("Steady Shot")
                );
        }
		[Class(WoWClass.Hunter)]
        [Spec(TalentSpec.MarksmanshipHunter)]
		[Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateMarksmanshipPull()
		{
            WantedPet = "1";
		    return new PrioritySelector(
		        CreateEnsureTarget(),
		        CreateWaitForCast(true),
		        CreateMoveToAndFace(35f, ret => Me.CurrentTarget),
		        CreateSpellBuff("Hunter's Mark"),
		        new Decorator(
		            ret => !SpellManager.CanCast("Aimed Shot"),
		            CreateAutoAttack(true)),
		        CreateSpellCast("Aimed Shot")
		        );
		}			
    }
}