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

using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular
{
    public class CompositeBuilder
    {
        public static Composite GetComposite(object createFrom, WoWClass wowClass, TalentSpec spec, BehaviorType behavior, WoWContext context)
        {
            Random rand = new Random();
            MethodInfo[] methods = createFrom.GetType().GetMethods();
            var matchedMethods = new Dictionary<int, RandomSelector>();
            foreach (MethodInfo mi in
                methods.Where(
                    mi =>
                    !mi.IsGenericMethod &&
                    mi.GetParameters().Length == 0)
                    .Where(
                        mi =>
                        mi.ReturnType == typeof(Composite) ||
                        mi.ReturnType.IsSubclassOf(typeof(Composite))))
            {
                //Logger.WriteDebug("[CompositeBuilder] Checking attributes on " + mi.Name);
                bool classMatches = false, specMatches = false, behaviorMatches = false, contextMatches = false;
                int thePriority = 0;
                foreach (object ca in mi.GetCustomAttributes(false))
                {
                    if (ca is ClassAttribute)
                    {
                        var attrib = ca as ClassAttribute;
                        if (attrib.SpecificClass != wowClass)
                        {
                            continue;
                        }
                        //Logger.WriteDebug(mi.Name + " has my class");
                        classMatches = true;
                    }
                    else if (ca is SpecAttribute)
                    {
                        var attrib = ca as SpecAttribute;
                        if (attrib.SpecificSpec != spec)
                        {
                            continue;
                        }
                        //Logger.WriteDebug(mi.Name + " has my spec");
                        specMatches = true;
                    }
                    else if (ca is BehaviorAttribute)
                    {
                        var attrib = ca as BehaviorAttribute;
                        if (attrib.Type != behavior)
                        {
                            continue;
                        }
                        //Logger.WriteDebug(mi.Name + " has my behavior");
                        behaviorMatches = true;
                    }
                    else if (ca is ContextAttribute)
                    {
                        var attrib = ca as ContextAttribute;
                        if ((attrib.SpecificContext & context) == 0)
                        {
                            continue;
                        }
                        //Logger.WriteDebug(mi.Name + " has my context");
                        contextMatches = true;
                    }
                    else if (ca is PriorityAttribute)
                    {
                        var attrib = ca as PriorityAttribute;
                        thePriority = attrib.PriorityLevel;
                    }
                }

                // If all our attributes match, then mark it as wanted!
                if (classMatches && specMatches && behaviorMatches && contextMatches)
                {
                    Logger.WriteDebug(string.Format("{0} is a match!", mi.Name));
                    Logger.Write(string.Format("Using {0} for {1} - {2} (Priority: {3})", mi.Name, spec.ToString().CamelToSpaced().Trim(), behavior, thePriority));
                    var matched = (Composite)mi.Invoke(createFrom, null);
                    if (matchedMethods.ContainsKey(thePriority))
                    {
                        matchedMethods[thePriority].AddChild(matched);
                    }
                    else
                    {
                        matchedMethods.Add(thePriority, new RandomSelector(matched));
                    }
                }
            }
            // If we found no methods, rofls!
            if (matchedMethods.Count <= 0)
            {
                return null;
            }
            // Create 
            // Return a sorted list of our randomselectors
            return new PrioritySelector(matchedMethods.OrderByDescending(mm => mm.Key).Select(mm => mm.Value).ToArray());
        }
    }
}