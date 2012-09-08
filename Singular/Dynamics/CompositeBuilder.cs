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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;

using Styx.TreeSharp;

namespace Singular.Dynamics
{
    public static class CompositeBuilder
    {
        private static List<MethodInfo> _methods = new List<MethodInfo>();

        public static Composite GetComposite(WoWClass wowClass, WoWSpec spec, BehaviorType behavior, WoWContext context, out int behaviourCount)
        {
            behaviourCount = 0;
            if (_methods.Count <= 0)
            {
                Logger.Write("Building method list");
                foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
                {
                    // All behavior methods should not be generic, and should have zero parameters, with their return types being of type Composite.
                    _methods.AddRange(
                        type.GetMethods(BindingFlags.Static | BindingFlags.Public).Where(
                            mi => !mi.IsGenericMethod && mi.GetParameters().Length == 0).Where(
                                mi => mi.ReturnType.IsAssignableFrom(typeof (Composite))));
                }
                Logger.Write("Added " + _methods.Count + " methods");
            }
            var matchedMethods = new Dictionary<int, Composite>();

            foreach (MethodInfo mi in _methods)
            {
                // If the behavior is set as ignore. Don't use it? Duh?
                if (mi.GetCustomAttributes(typeof(IgnoreBehaviorCountAttribute), false).Any())
                    continue;

                // If there's no behavior attrib, then move along.
                foreach (var a in mi.GetCustomAttributes(typeof(BehaviorAttribute), false))
                {
                    var attribute = a as BehaviorAttribute;
                    if (attribute == null)
                        continue;

                    // Check if our behavior matches with what we want. If not, don't add it!
                    if (IsMatchingMethod(attribute, wowClass, spec, behavior, context))
                    {
                        Logger.Write(string.Format("Matched {0} to behavior {1} for {2} {3} with priority {4}", mi.Name,
                            behavior, wowClass.ToString().CamelToSpaced(), spec.ToString().CamelToSpaced(),
                            attribute.PriorityLevel));

                        matchedMethods.Add(attribute.PriorityLevel, mi.Invoke(null, null) as Composite);
                    }
                }
            }
            // If we found no methods, rofls!
            if (matchedMethods.Count <= 0)
            {
                return null;
            }

            var result = new PrioritySelector();
            foreach (var kvp in matchedMethods.OrderByDescending(mm => mm.Key))
            {
                result.AddChild(kvp.Value);
                behaviourCount++;
            }

            return result;
        }


        private static bool IsMatchingMethod(BehaviorAttribute attribute, WoWClass wowClass, WoWSpec spec, BehaviorType behavior, WoWContext context)
        {
            if (attribute.SpecificClass != wowClass)
                return false;
            if ((attribute.Type & behavior) != 0)
                return false;
            if ((attribute.SpecificContext & context) == 0)
                return false;
            if (attribute.SpecificSpec != (WoWSpec)int.MaxValue && attribute.SpecificSpec != spec)
                return false;

            return true;
        }
    }
}