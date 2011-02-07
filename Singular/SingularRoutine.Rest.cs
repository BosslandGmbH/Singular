using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

	    private Composite _restBehavior;

        public Composite CreateDefaultRestComposite()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => Me.HealthPercent >= 95 && Me.ManaPercent >= 95 && (Me.HasAura("Food") || Me.HasAura("Drink")),
                    new Action(ret => Lua.DoString("SitStandOrDescendStart()"))),

                new Decorator(
                    ret => Me.HealthPercent <= 50 && !Me.HasAura("Food"),
                    new PrioritySelector(
                        new Decorator(
                            ret => Me.IsMoving,
                            new Action(ret => Navigator.PlayerMover.MoveStop())),
                        new Decorator(
                            ret => Consumable.GetBestFood(false) != null,
                            new Action(ret => Styx.Logic.Common.Rest.FeedImmediate()))
                        )),


                new Decorator(
                    ret => Me.ManaPercent <= 50 && !Me.HasAura("Drink"),
                    new PrioritySelector(
                        new Decorator(
                            ret => Me.IsMoving,
                            new Action(ret => Navigator.PlayerMover.MoveStop())),
                        new Decorator(
                            ret => Consumable.GetBestDrink(false) != null,
                            new Action(ret => Styx.Logic.Common.Rest.DrinkImmediate()))
                        ))

                );
        }
	}
}
