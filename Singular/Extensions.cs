
using System.Linq;
using System.Text;
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System.Collections.Generic;
using Styx.Pathing;
using Singular.Helpers;
using Singular.Settings;

namespace Singular
{
    internal static class Extensions
    {

        public static bool Between(this double distance, double min, double max)
        {
            return distance >= min && distance <= max;
        }

        public static bool Between(this int value, int min, int max)
        {
            return value >= min && value <= max;
        }

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

        private static string Right(string s, int c)
        {
            return s.Substring(c > s.Length ? 0 : s.Length - c);
        }
        public static string UnitID(ulong guid)
        {
            return Right(string.Format("{0:X4}", guid), 4);
        }

        public static bool ShowPlayerNames { get; set; }

        public static string SafeName(this WoWObject obj)
        {
            if (obj.IsMe)
            {
                return "Me";
            }

            string name;
            if (obj is WoWPlayer)
            {
                if (RaFHelper.Leader == obj)
                    return "Tank";

                name = ShowPlayerNames ? ((WoWPlayer)obj).Name : ((WoWPlayer)obj).Class.ToString();
            }
            else if (obj is WoWUnit && obj.ToUnit().IsPet)
            {
                name =  obj.ToUnit().OwnedByRoot.SafeName()  + ":Pet";
            }
            else
            {
                name = obj.Name;
            }

            if (SingularSettings.Debug)
                return name + "." + UnitID(obj.Guid);

            return name;
        }

        public static bool IsWanding(this LocalPlayer me)
        {
            return StyxWoW.Me.AutoRepeatingSpellId == 5019;
        }


        /// <summary>
        /// determines if a target is off the ground far enough that you can
        /// reach it with melee spells if standing directly under.
        /// </summary>
        /// <param name="u">unit</param>
        /// <returns>true if above melee reach</returns>
        public static bool IsAboveTheGround(this WoWUnit u)
        {
            float height = HeightOffTheGround(u);
            if ( height == float.MaxValue )
                return false;   // make this true if better to assume aerial 

            if (height > Spell.MeleeRange)
                return true;

            return false;
        }

        /// <summary>
        /// calculate a unit's vertical distance (height) above ground level (mesh).  this is the units position
        /// relative to the ground and is independent of any other character.  
        /// </summary>
        /// <param name="u">unit</param>
        /// <returns>float.MinValue if can't determine, otherwise distance off ground</returns>
        public static float HeightOffTheGround(this WoWUnit u)
        {
            var unitLoc = new WoWPoint( u.Location.X, u.Location.Y, u.Location.Z);         
            var listMeshZ = Navigator.FindHeights( unitLoc.X, unitLoc.Y).Where( h => h <= unitLoc.Z);
            if (listMeshZ.Any())
                return unitLoc.Z - listMeshZ.Max();
            
            return float.MaxValue;
        }

        /// <summary>
        /// converts bool to Y or N string
        /// </summary>
        /// <param name="b">bool to convert</param>
        /// <returns></returns>
        public static string ToYN(this bool b)
        {
            return b ? "Y" : "N";
        }
    }
}