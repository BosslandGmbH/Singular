using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.TreeSharp;
using System.Collections.Generic;
using Styx.CommonBot;
using Singular.Settings;

namespace Singular.ClassSpecific.Monk
{
    public class Lowbie
    {
		[Behavior(BehaviorType.Combat | BehaviorType.Pull, WoWClass.Monk, 0)]
		public static Composite CreateLowbieMonkCombat()
		{
			return new PrioritySelector(
				Helpers.Common.EnsureReadyToAttackFromMelee(),
				Spell.WaitForCastOrChannel(),
				new Decorator(                    
					req => !Spell.IsGlobalCooldown(),
					new PrioritySelector(
						Helpers.Common.CreateInterruptBehavior(),

						Movement.WaitForFacing(),
						Movement.WaitForLineOfSpellSight(),

						Spell.BuffSelf("Effuse", req => StyxWoW.Me.HealthPercent < 60),
						Spell.Cast("Blackout Kick"),
						Spell.Cast("Tiger Palm"),
						//Only roll to get to the mob quicker. 
						Common.CreateCloseDistanceBehavior()
						)
					)
				);
		}
    }
     
}