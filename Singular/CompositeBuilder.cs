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
                        classMatches = true;
                    }
                    else if (ca is SpecAttribute)
                    {
                        var attrib = ca as SpecAttribute;
                        if (attrib.SpecificSpec != spec)
                        {
                            continue;
                        }
                        specMatches = true;
                    }
                    else if (ca is BehaviorAttribute)
                    {
                        var attrib = ca as BehaviorAttribute;
                        if (attrib.Type != behavior)
                        {
                            continue;
                        }
                        behaviorMatches = true;
                    }
                    else if (ca is ContextAttribute)
                    {
                        var attrib = ca as ContextAttribute;
                        if ((attrib.SpecificContext & context) != 0)
                        {
                            continue;
                        }
                        contextMatches = true;
                    }
                }

                // If all our attributes match, then mark it as wanted!
                if (classMatches && specMatches && behaviorMatches && contextMatches)
                {
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