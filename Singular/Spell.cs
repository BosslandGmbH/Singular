using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular
{
    public delegate WoWUnit UnitSelectionDelegate(object context);
    public delegate bool SimpleBooleanDelegate(object context);

    class Spell
    {
        public static Composite Cast(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new Decorator(
                ret => requirements(ret) && onUnit(ret) != null && SpellManager.CanCast(name, onUnit(ret), true),
                new Action(
                    ret =>
                        {
                            Logger.Write("Casting " + name + " on " + onUnit(ret).SafeName());
                            SpellManager.Cast(name);
                        })
                );
        }
    }
}
