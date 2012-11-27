
using System.Linq;
using System.Text;
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System.Collections.Generic;
using Styx.Pathing;
using Singular.Helpers;

namespace Singular
{
    internal static class Extensions
    {

        public static bool Between(this double distance, double min, double max)
        {
            return distance >= min && distance <= max;
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

        public static bool ShowPlayerNames { get; set; }

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

                name = ShowPlayerNames ? ((WoWPlayer)obj).Name : ((WoWPlayer)obj).Class.ToString();
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
        /// checks if my aura is missing or has less than 3 seconds left. useful
        /// to test for DoTs and HoTs you need to renew and already verified the
        /// spell that applys the aura is known
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="aura">true if less than 3 secs left, otherwise false</param>
        /// <returns></returns>
        public static bool MyAuraMissing(this WoWUnit u, string aura)
        {
            return u.MyAuraMissing(aura, 3);
        }

        public static bool MyAuraMissing(this WoWUnit u, string aura, int seconds)
        {
            return u.GetAuraTimeLeft(aura, true).TotalSeconds < seconds;
        }

        /// <summary>
        /// checks if we know the spell that applies the aura and if the aura is missing.  
        /// useful for cases testing to renew a DoT or HoT where the spell and the aura
        /// have the same name.  allows single check without having to separately check
        /// if you have learned the spell yet
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="s">true if known and less than 3 secs left, otherwise false</param>
        /// <returns></returns>
        public static bool MyKnownSpellAuraMissing(this WoWUnit u, string spell)
        {
            return u.MyKnownSpellAuraMissing(spell, spell);
        }

        /// <summary>
        /// checks if we know the spell that applies the aura and if the aura is missing.  
        /// useful for cases testing to renew a DoT or HoT where the spell and the aura
        /// have different names.  allows single check without having to separately check
        /// if you have learned the spell yet
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="s">true if known and less than 3 secs left, otherwise false</param>
        /// <returns></returns>
        public static bool MyKnownSpellAuraMissing(this WoWUnit u, string spell, string aura)
        {
            return SpellManager.HasSpell(spell) && u.GetAuraTimeLeft(aura, true).TotalSeconds < 3;
        }


    }
}