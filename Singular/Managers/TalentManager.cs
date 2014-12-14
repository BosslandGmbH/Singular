
using System;
using System.Collections.Generic;
using System.Linq;

using Styx;
using Styx.WoWInternals;
using System.Drawing;
using Styx.CommonBot;
using Styx.Common.Helpers;
using Styx.CommonBot.Routines;
using Styx.CommonBot.CharacterManagement;

namespace Singular.Managers
{
    internal static class TalentManager
    {
        //public const int TALENT_FLAG_ISEXTRASPEC = 0x10000;

        static TalentManager()
        {
        }

        public static void Init()
        {
            Talents = new List<Talent>();
            TalentId = new int[6];
            Glyphs = new HashSet<string>();
            GlyphId = new int[6];

            Lua.Events.AttachEvent("PLAYER_LEVEL_UP", UpdateTalentManager);
            Lua.Events.AttachEvent("CHARACTER_POINTS_CHANGED", UpdateTalentManager);
            Lua.Events.AttachEvent("GLYPH_UPDATED", UpdateTalentManager);
            Lua.Events.AttachEvent("ACTIVE_TALENT_GROUP_CHANGED", UpdateTalentManager);
            Lua.Events.AttachEvent("PLAYER_SPECIALIZATION_CHANGED", UpdateTalentManager);
            Lua.Events.AttachEvent("LEARNED_SPELL_IN_TAB", UpdateTalentManager);

            Update();
        }

        public static WoWSpec CurrentSpec 
        { 
            get; 
            private set; 
        }

        public static List<Talent> Talents { get; private set; }

        private static int[] TalentId { get; set; }

        public static HashSet<string> Glyphs { get; private set; }

        private static int[] GlyphId { get; set; }

        private static uint SpellCount = 0;
        private static uint SpellBookSignature = 0;

        private static WaitTimer EventRebuildTimer = new WaitTimer(TimeSpan.FromSeconds(1));
        private static WaitTimer SpecChangeTestTimer = new WaitTimer(TimeSpan.FromSeconds(3));

        private static bool _Rebuild = false;
        private static bool RebuildNeeded 
        {
            get 
            {
                return _Rebuild;
            }
            set
            {
                _Rebuild = value;
                EventRebuildTimer.Reset();
            }
        }

        public static bool IsSelected(int index)
        {
            // return Talents.FirstOrDefault(t => t.Index == index).Selected;
            int tier = (index-1) / 3;
            if (tier.Between(0, 5))
                return TalentId[tier] == index;
            return false;
        }

        /// <summary>
        ///   Checks if we have a glyph or not
        /// </summary>
        /// <param name = "glyphName">Name of the glyph without "Glyph of". i.e. HasGlyph("Aquatic Form")</param>
        /// <returns></returns>
        public static bool HasGlyph(string glyphName)
        {
            return Glyphs.Any() && Glyphs.Contains(glyphName);
        }

        /// <summary>
        /// event handler for messages which should cause behaviors to be rebuilt
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private static void UpdateTalentManager(object sender, LuaEventArgs args)
        {
            // Since we hooked this in ctor, make sure we are the selected CC
            if (RoutineManager.Current.Name != SingularRoutine.Instance.Name)
                return;

            var oldSpec = CurrentSpec;
            int[] oldTalent = TalentId;
            int[] oldGlyph = GlyphId;
            uint oldSig = SpellBookSignature;
            uint oldSpellCount = SpellCount;

            Logger.WriteDebug("{0} Event Fired!", args.EventName);

            Update();

            if (args.EventName == "PLAYER_SPECIALIZATION_CHANGED")
            {
                SpecChangeTestTimer.Reset();
                Logger.WriteDiagnostic("TalentManager: receive a {0} event, currently {1} -- queueing check for new spec!", args.EventName, CurrentSpec);
            }

            if (args.EventName == "PLAYER_LEVEL_UP")
            {
                RebuildNeeded = true;
                Logger.Write( LogColor.Hilite, "TalentManager: Your character has leveled up! Now level {0}", args.Args[0]);
            }

            if (CurrentSpec != oldSpec)
            {
                RebuildNeeded = true;
                Logger.Write( LogColor.Hilite, "TalentManager: Your spec has been changed.");
            }

            int i;
            for (i = 0; i < 6; i++)
            {
                if (oldTalent[i] != TalentId[i])
                {
                    RebuildNeeded = true;
                    Logger.Write( LogColor.Hilite, "TalentManager: Your talents have changed.");
                    break;
                }
            }

            for (i = 0; i < 6; i++)
            {
                if (oldGlyph[i] != GlyphId[i])
                {
                    RebuildNeeded = true;
                    Logger.Write( LogColor.Hilite, "TalentManager: Your glyphs have changed.");
                    break;
                }
            }

            if (SpellBookSignature != oldSig || SpellCount != oldSpellCount)
            {
                RebuildNeeded = true;
                Logger.Write(LogColor.Hilite, "TalentManager: Your available Spells have changed.");
            }

            Logger.WriteDebug(LogColor.Hilite, "TalentManager: RebuildNeeded={0}", RebuildNeeded);
        }

        private static uint CalcSpellBookSignature()
        {
            uint sig = 0;
            foreach (var sp in SpellManager.Spells)
            {
                sig ^= (uint) sp.Value.Id;
            }
            return sig;
        }

        /// <summary>
        /// loads WOW Talent and Spec info into cached list
        /// </summary>
        public static void Update()
        {
            // Keep the frame stuck so we can do a bunch of injecting at once.
            using (StyxWoW.Memory.AcquireFrame())
            {
                CurrentSpec = StyxWoW.Me.Specialization;

                var GlobalClassProfile = CharacterManager.ClassProfiles.FirstOrDefault( prf => prf.GetSpec() == CurrentSpec);
                if (GlobalClassProfile != null)
                    CharacterManager.SetClassProfile(GlobalClassProfile); 

                Talents.Clear();
                TalentId = new int[7];

                // Always 21 talents. 7 rows of 3 talents.
	            for (int row = 0; row < 7; row++)
	            {
		            for (int col = 0; col < 3; col++)
		            {
						var selected = Lua.GetReturnVal<bool>(string.Format("local t = select(4, GetTalentInfo({0}, {1}, GetActiveSpecGroup())) if t then return 1 end return nil", row + 1, col + 1), 0);
			            int index = 1 + row * 3 + col;
						var t = new Talent { Index = index, Selected = selected };
						Talents.Add(t);

						if (selected)
							TalentId[row] = index;
		            }
	            }

                Glyphs.Clear();
                GlyphId = new int[7];

                // 6 glyphs all the time. Plain and simple!
                for (int i = 1; i <= 6; i++)
                {
                    List<string> glyphInfo = Lua.GetReturnValues(String.Format("return GetGlyphSocketInfo({0})", i));

                    // add check for 4 members before access because empty sockets weren't returning 'nil' as documented
                    if (glyphInfo != null && glyphInfo.Count >= 4 && glyphInfo[3] != "nil" &&
                        !string.IsNullOrEmpty(glyphInfo[3]))
                    {
                        GlyphId[i-1] = int.Parse(glyphInfo[3]);
                        Glyphs.Add(WoWSpell.FromId(GlyphId[i-1]).Name.Replace("Glyph of ", ""));
                    }
                }

                SpellCount = (uint) SpellManager.Spells.Count;
                SpellBookSignature = CalcSpellBookSignature();
            }

        }

        public static bool Pulse()
        {
            if (SpecChangeTestTimer.IsFinished)
            {
                if (StyxWoW.Me.Specialization != CurrentSpec)
                {
                    CurrentSpec = StyxWoW.Me.Specialization;
                    RebuildNeeded = true;
                    Logger.Write( LogColor.Hilite, "TalentManager: spec is now to {0}", SingularRoutine.SpecName());
                }
            }

            if (RebuildNeeded && EventRebuildTimer.IsFinished)
            {
                RebuildNeeded = false;
                Logger.Write( LogColor.Hilite, "TalentManager: Rebuilding behaviors due to changes detected.");
                Update();   // reload talents just in case
                SingularRoutine.DescribeContext();
                SingularRoutine.Instance.RebuildBehaviors();
                return true;
            }

            return false;
        }


        #region Nested type: Talent

        public struct Talent
        {
            public bool Selected;
            public int Index;
        }

        #endregion
    }
}