﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Singular.Composites;

using Styx;
using Styx.Logic.Inventory;
using Styx.Logic.Pathing;
using Styx.WoWInternals;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular
{
	partial class SingularRoutine
	{
	    public event EventHandler OnRestBeforeEat;

	    public void InvokeOnRestBeforeEat(object sender, EventArgs eventArgs)
	    {
	        EventHandler handler = OnRestBeforeEat;
	        if (handler != null)
	        {
	            handler(this, eventArgs);
	        }
	    }

	    public event EventHandler OnRestBeforeDrink;

	    public void InvokeOnRestBeforeDrink(object sender, EventArgs eventArgs)
	    {
	        EventHandler handler = OnRestBeforeDrink;
	        if (handler != null)
	        {
	            handler(this, eventArgs);
	        }
	    }
        
        public Composite CreateDefaultRestComposite()
        {
            return new PrioritySelector(

                //new Decorator(
                //    ret => Me.HealthPercent >= 95 && Me.ManaPercent >= 95 && (Me.HasAura("Food") || Me.HasAura("Drink")),
                //    new Action(ret => Lua.DoString("SitStandOrDescendStart()"))),

                new Decorator(ret => Me.HealthPercent <= 60 && !Me.HasAura("Food"),
                    new PrioritySelector(
                        new ActionLogMessage(true, "Checking movement for food."),
                        new Decorator(
                            ret => Me.IsMoving,
                            new Action(ret => Navigator.PlayerMover.MoveStop())),
                        new ActionLogMessage(true, "Checking for food and eating if we have some."),
                        new Decorator(
                            ret => Consumable.GetBestFood(false) != null,
                            new Action(ret => Styx.Logic.Common.Rest.FeedImmediate()))
                        )),


                // Make sure we're a class with mana, if not, just ignore drinking all together!
                new Decorator(ret => Me.PowerType == WoWPowerType.Mana && Me.ManaPercent <= 60 && !Me.HasAura("Drink"),
                    new PrioritySelector(
                        new ActionLogMessage(true, "Checking movement for water."),
                        new Decorator(
                            ret => Me.IsMoving,
                            new Action(ret => Navigator.PlayerMover.MoveStop())),
                        new ActionLogMessage(true, "Checking for water and drinking if we have some."),
                        new Decorator(
                            ret => Consumable.GetBestDrink(false) != null,
                            new Action(ret => Styx.Logic.Common.Rest.DrinkImmediate()))
                        ))

                );
        }
	}
}
