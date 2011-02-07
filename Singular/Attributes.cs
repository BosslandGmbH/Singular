using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx.Combat.CombatRoutine;

namespace Singular
{
    [Flags]
    public enum WoWContext
    {
        Normal = 0x1,
        Instances = 0x2,
        Battlegrounds = 0x4
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    internal sealed class ClassSpecificAttribute : Attribute
    {
        public WoWClass SpecificClass { get; private set; }
        public ClassSpecificAttribute(WoWClass specificClass)
        {
            SpecificClass = specificClass;
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    internal sealed class SpecSpecificAttribute : Attribute
    {
        
    }
}
