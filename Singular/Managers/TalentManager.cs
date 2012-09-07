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

using Styx;
using Styx.WoWInternals;

namespace Singular.Managers
{
    internal static class TalentManager
    {
        //public const int TALENT_FLAG_ISEXTRASPEC = 0x10000;

        static TalentManager()
        {
            Talents = new List<Talent>();
            Glyphs = new HashSet<string>();
            Lua.Events.AttachEvent("CHARACTER_POINTS_CHANGED", UpdateTalentManager);
            Lua.Events.AttachEvent("GLYPH_UPDATED", UpdateTalentManager);
            Lua.Events.AttachEvent("ACTIVE_TALENT_GROUP_CHANGED", UpdateTalentManager);
        }

        public static WoWSpec CurrentSpec { get; private set; }

        public static List<Talent> Talents { get; private set; }

        public static HashSet<string> Glyphs { get; private set; }

        public static int GetCount(int tab, int index)
        {
            return GetCount(index);
        }

        public static int GetCount(int index)
        {
            return Talents.FirstOrDefault(t => t.Index == index).Count;
        }

        /// <summary>
        ///   Checks if we have a glyph or not
        /// </summary>
        /// <param name = "glyphName">Name of the glyph without "Glyph of". i.e. HasGlyph("Aquatic Form")</param>
        /// <returns></returns>
        public static bool HasGlyph(string glyphName)
        {
            return Glyphs.Count > 0 && Glyphs.Contains(glyphName);
        }

        private static void UpdateTalentManager(object sender, LuaEventArgs args)
        {
            var oldSpec = CurrentSpec;

            Update();

            if (CurrentSpec != oldSpec)
            {
                Logger.Write("Your spec has been changed. Rebuilding behaviors");
                SingularRoutine.Instance.CreateBehaviors();
            }
        }

        public static void Update()
        {
            // Keep the frame stuck so we can do a bunch of injecting at once.
            using (StyxWoW.Memory.AcquireFrame())
            {
                CurrentSpec = StyxWoW.Me.Specialization;
                Logger.Write("TalentManager - looks like a {0}", CurrentSpec.ToString());

                Talents.Clear();

                var numTalents = Lua.GetReturnVal<int>("return GetNumTalents()", 0);
                for (int index = 1; index <= numTalents; index++)
                {
                    var selected = Lua.GetReturnVal<int>(string.Format("return GetTalentInfo({0})", index), 4);
                    var t = new Talent { Index = index, Count = selected };
                    Talents.Add(t);
                }

                Glyphs.Clear();

                var glyphCount = Lua.GetReturnVal<int>("return GetNumGlyphSockets()", 0);

                if (glyphCount != 0)
                {
                    for (int i = 1; i <= glyphCount; i++)
                    {
                        List<string> glyphInfo = Lua.GetReturnValues(String.Format("return GetGlyphSocketInfo({0})", i));

                        if (glyphInfo != null && glyphInfo[3] != "nil" && !string.IsNullOrEmpty(glyphInfo[3]))
                        {
                            Glyphs.Add(WoWSpell.FromId(int.Parse(glyphInfo[3])).Name.Replace("Glyph of ", ""));
                        }
                    }
                }
            }

        }

        #region Nested type: Talent

        public struct Talent
        {
            public int Count;
            public int Index;
        }

        #endregion
    }
}