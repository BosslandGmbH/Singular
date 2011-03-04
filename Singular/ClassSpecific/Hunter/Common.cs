﻿using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Combat.CombatRoutine;

using TreeSharp;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
		[Class(WoWClass.Hunter)]
        [Spec(TalentSpec.BeastMasteryHunter)]
        [Spec(TalentSpec.SurvivalHunter)]
        [Spec(TalentSpec.MarksmanshipHunter)]
		[Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateHunterBuffs()
        {
			return new PrioritySelector(
				new Decorator(
					ctx => Me.CastingSpell != null && Me.CastingSpell.Name == "Revive " + WantedPet && Me.GotAlivePet,
					new Action(ctx => SpellManager.StopCasting())),

				CreateWaitForCast(),
				CreateSpellBuffOnSelf("Aspect of the Hawk", ret => !Me.HasAura("Aspect of the Hawk")),
				//new ActionLogMessage(false, "Checking for pet"),
				new Decorator(
					ret => !Me.GotAlivePet,
					new Action(ret => PetManager.CallPet(WantedPet)))
					);
        }
        protected Composite CreateHunterBackPedal()
        {
            return
                new Decorator(
                    ret => Me.CurrentTarget.Distance <= 7 && Me.CurrentTarget.IsAlive &&
                           (Me.CurrentTarget.CurrentTarget == null || Me.CurrentTarget.CurrentTarget != Me),
                    new Action(ret =>
                    {
                        WoWPoint moveTo = WoWMathHelper.CalculatePointFrom(Me.Location, Me.CurrentTarget.Location, 10f);

                        if (Navigator.CanNavigateFully(Me.Location, moveTo))
                            Navigator.MoveTo(moveTo);
                    }));
        }
	
    }
}