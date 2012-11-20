#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $LastChangedBy$
// $LastChangedDate$
// $Revision$

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Singular.Managers;
using Singular.Settings;
using Styx;


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
                Logger.WriteDebug("Added " + _methods.Count + " methods");
            }
            var matchedMethods = new Dictionary<BehaviorAttribute, Composite>();

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
                        Logger.WriteDebug(string.Format("Matched {0} to behavior {1} for {2} {3} with priority {4}", mi.Name,
                            behavior, wowClass.ToString().CamelToSpaced(), spec.ToString().CamelToSpaced(),
                            attribute.PriorityLevel));

                        // if it blows up here, you defined a method with the exact same attribute and priority as one already found
                        matchedMethods.Add(attribute, mi.Invoke(null, null) as Composite);
                    }
                }
            }
            // If we found no methods, rofls!
            if (matchedMethods.Count <= 0)
            {
                return null;
            }

            var result = new PrioritySelector();
            foreach (var kvp in matchedMethods.OrderByDescending(mm => mm.Key.PriorityLevel))
            {
                result.AddChild(kvp.Value);
                behaviourCount++;
            }

            return result;
        }


        private static bool IsMatchingMethod(BehaviorAttribute attribute, WoWClass wowClass, WoWSpec spec, BehaviorType behavior, WoWContext context)
        {
            if (attribute.SpecificClass != wowClass && attribute.SpecificClass != WoWClass.None)
                return false;
            if ((attribute.Type & behavior) == 0)
                return false;
            if ((attribute.SpecificContext & context) == 0)
                return false;
            if (attribute.SpecificSpec != (WoWSpec)int.MaxValue && attribute.SpecificSpec != spec)
                return false;

            Logger.WriteDebug("IsMatchingMethod({0}, {1}, {2}, {3}) - {4}, {5}, {6}, {7}, {8}", wowClass, spec, behavior,
                context, attribute.SpecificClass, attribute.SpecificSpec, attribute.Type, attribute.SpecificContext,
                attribute.PriorityLevel);
            return true;
        }
    }
}