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
            MethodInfo[] methods = createFrom.GetType().GetMethods();
            MethodInfo bestMatch = null;
            foreach (MethodInfo mi in
                methods.Where(mi => !mi.IsGenericMethod && mi.GetParameters().Length == 0).Where(mi => mi.ReturnType == typeof(Composite) || mi.ReturnType.IsSubclassOf(typeof(Composite))))
            {
                //Logger.WriteDebug("[CompositeBuilder] Checking attributes on " + mi.Name);
                bool classMatches = false, specMatches = false, behaviorMatches = false, contextMatches = false;
                foreach (object ca in mi.GetCustomAttributes(false))
                {
                    if (ca is ClassAttribute)
                    {
                        var attrib = ca as ClassAttribute;
                        if (attrib.SpecificClass != wowClass)
                        {
                            continue;
                        }
                        Logger.WriteDebug(mi.Name + " has my class");
                        classMatches = true;
                    }
                    else if (ca is SpecAttribute)
                    {
                        var attrib = ca as SpecAttribute;
                        if (attrib.SpecificSpec != spec)
                        {
                            continue;
                        }
                        Logger.WriteDebug(mi.Name + " has my spec");
                        specMatches = true;
                    }
                    else if (ca is BehaviorAttribute)
                    {
                        var attrib = ca as BehaviorAttribute;
                        if (attrib.Type != behavior)
                        {
                            continue;
                        }
                        Logger.WriteDebug(mi.Name + " has my behavior");
                        behaviorMatches = true;
                    }
                    else if (ca is ContextAttribute)
                    {
                        var attrib = ca as ContextAttribute;
                        if ((attrib.SpecificContext & context) == 0)
                        {
                            continue;
                        }
                        Logger.WriteDebug(mi.Name + " has my context");
                        contextMatches = true;
                    }
                }

                // If all our attributes match, then mark it as wanted!
                if (classMatches && specMatches && behaviorMatches && contextMatches)
                {
                    Logger.WriteDebug(mi.Name + " is a match!");
                    bestMatch = mi;
                }
            }
            if (bestMatch == null)
            {
                return null;
            }

            return (Composite)bestMatch.Invoke(createFrom, null);
        }
    }
}