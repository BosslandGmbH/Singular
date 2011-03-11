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

using CommonBehaviors.Actions;

using Singular.Composites;

using System.Linq;

using Styx;
using Styx.Logic;
using Styx.Logic.Inventory;
using Styx.Logic.Pathing;

using TreeSharp;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using System.Threading;

namespace Singular
{
    partial class SingularRoutine
    {
        public Composite CreateDefaultRestComposite(int minHealth, int minMana)
        {
			return new PrioritySelector(
                // Don't rest if the leader is in combat. Ever.
				//new Decorator(ret=> Me.IsInParty,
				//    new Decorator(ret=>RaFHelper.Leader != null && RaFHelper.Leader.Combat,
				//        new ActionAlwaysFail())),

				// Make sure we wait out res sickness. Fuck the classes that can deal with it. :O
				// At least try to do something if we are in combat :)
				new Decorator(
					ret => Me.HasAura("Resurrection Sickness"),
					new ActionAlwaysSucceed()),

				// Wait while cannibalizing
				new Decorator(
					ret => Me.CastingSpell != null && Me.CastingSpell.Name == "Cannibalize" && 
						   (Me.HealthPercent < 95 || (Me.PowerType == WoWPowerType.Mana && Me.ManaPercent < 95)),
					new Sequence(
						new ActionLogMessage(false, "Waiting for Cannibalize"),
						new ActionAlwaysSucceed())),

				// Cannibalize support goes before drinking/eating
				new Decorator(
					ret => (Me.HealthPercent <= minHealth || (Me.PowerType == WoWPowerType.Mana && Me.ManaPercent <= minMana)) && 
						   CanCast("Cannibalize", Me, false) && CorpseAround,
					new Sequence(
						new Action(ret => Navigator.PlayerMover.MoveStop()),
						new Action(ret => StyxWoW.SleepForLagDuration()),
						new Action(ret => SpellManager.Cast("Cannibalize")),
						new Action(ret => Thread.Sleep(1000)))),
						   

				// Check if we're allowed to eat (and make sure we have some food. Don't bother going further if we have none.
				new Decorator(
					ret => Me.HealthPercent <= minHealth && !Me.HasAura("Food") && Consumable.GetBestFood(false) != null,
					new PrioritySelector(
						new ActionLogMessage(true, "Checking movement for food."),
						new Decorator(
							ret => Me.IsMoving,
							new Action(ret => Navigator.PlayerMover.MoveStop())),
						new Action(ret =>
							{
								Styx.Logic.Common.Rest.FeedImmediate();
								StyxWoW.SleepForLagDuration();
							})
						)),
				// Make sure we're a class with mana, if not, just ignore drinking all together! Other than that... same for food.
				new Decorator(
					ret =>
					Me.PowerType == WoWPowerType.Mana && Me.ManaPercent <= minMana && !Me.HasAura("Drink") && Consumable.GetBestDrink(false) != null,
					new PrioritySelector(
						new ActionLogMessage(true, "Checking movement for water."),
						new Decorator(
							ret => Me.IsMoving,
							new Action(ret => Navigator.PlayerMover.MoveStop())),
						new Action(ret =>
							{
								Styx.Logic.Common.Rest.DrinkImmediate();
								StyxWoW.SleepForLagDuration();
							})
						)),
				// This is to ensure we STAY SEATED while eating/drinking. No reason for us to get up before we have to.
				new Decorator(
					ret =>
					(Me.HasAura("Food") && Me.HealthPercent < 95) || (Me.HasAura("Drink") && Me.PowerType == WoWPowerType.Mana && Me.ManaPercent < 95),
					new ActionAlwaysSucceed()),

				new Decorator(
					ret => (Me.PowerType == WoWPowerType.Mana && Me.ManaPercent <= minMana) || Me.HealthPercent <= minHealth,
					new Action(ret => Logger.Write("We have no food/drink. Waiting to recover our health/mana back")))

				);
        }

		private bool CorpseAround
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>(true, false).Any(
					u => u.Distance < 5 && u.Dead && 
						 (u.CreatureType == WoWCreatureType.Humanoid || u.CreatureType == WoWCreatureType.Undead));
			}
		}
    }
}