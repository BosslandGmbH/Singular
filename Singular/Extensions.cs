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

using System.Text;
using Styx;
using Styx.Logic;
using Styx.WoWInternals.WoWObjects;

namespace Singular
{
    internal static class Extensions
    {
        /// <summary>
        ///   A string extension method that turns a Camel-case string into a spaced string. (Example: SomeCamelString -> Some Camel String)
        /// </summary>
        /// <remarks>
        ///   Created 2/7/2011.
        /// </remarks>
        /// <param name = "str">The string to act on.</param>
        /// <returns>.</returns>
        public static string CamelToSpaced(this string str)
        {
            var sb = new StringBuilder();
            foreach (char c in str)
            {
                if (char.IsUpper(c))
                {
                    sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static string SafeName(this WoWObject obj)
        {
            if (obj.IsMe)
            {
                return "Myself";
            }

            string name;
            if (obj is WoWPlayer)
            {
                if (RaFHelper.Leader == obj)
                    return "Tank";

                name = ((WoWPlayer)obj).Class.ToString();
            }
            else if (obj is WoWUnit && obj.ToUnit().IsPet)
            {
                name = "Pet";
            }
            else
            {
                name = obj.Name;
            }

            return name;
        }

        public static bool IsBoss(this WoWUnit unit)
        {
            return Lists.BossList.BossIds.Contains(unit.Entry);
        }

        public static bool IsWanding(this LocalPlayer me)
        {
            return StyxWoW.Me.AutoRepeatingSpellId == 5019;
        }
    }
}