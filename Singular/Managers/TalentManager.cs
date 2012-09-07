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
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals;

namespace Singular.Managers
{
    public enum TalentSpec
    {
        // Just a 'spec' for low levels
        Lowbie = 0,
        // A value representing any spec
        Any = int.MaxValue,
        // Here's how this works
        // In the 2nd byte we store the class for the spec
        // Low byte stores the index of the spec. (0-2 for a total of 3)
        // We can retrieve the class easily by doing spec & 0xFF00 to get the high-byte (or you can just shift right 8bits)
        // And the spec can be retrieved via spec & 0xFF - Simple enough
        // Extra flags are stored in the 3rd byte

        BloodDeathKnight = ((int)WoWClass.DeathKnight << 8) + 0,
        FrostDeathKnight = ((int)WoWClass.DeathKnight << 8) + 1,
        UnholyDeathKnight = ((int)WoWClass.DeathKnight << 8) + 2,

        BalanceDruid = ((int)WoWClass.Druid << 8) + 0,
        FeralDruid = ((int)WoWClass.Druid << 8) + 1,
        //FeralTankDruid = TalentManager.TALENT_FLAG_ISEXTRASPEC + ((int)WoWClass.Druid << 8) + 1,
        RestorationDruid = ((int)WoWClass.Druid << 8) + 2,

        BeastMasteryHunter = ((int)WoWClass.Hunter << 8) + 0,
        MarksmanshipHunter = ((int)WoWClass.Hunter << 8) + 1,
        SurvivalHunter = ((int)WoWClass.Hunter << 8) + 2,

        ArcaneMage = ((int)WoWClass.Mage << 8) + 0,
        FireMage = ((int)WoWClass.Mage << 8) + 1,
        FrostMage = ((int)WoWClass.Mage << 8) + 2,

        HolyPaladin = ((int)WoWClass.Paladin << 8) + 0,
        ProtectionPaladin = ((int)WoWClass.Paladin << 8) + 1,
        RetributionPaladin = ((int)WoWClass.Paladin << 8) + 2,

        DisciplinePriest = ((int)WoWClass.Priest << 8) + 0,
        //DisciplineHealingPriest = TalentManager.TALENT_FLAG_ISEXTRASPEC + ((int)WoWClass.Priest << 8) + 0,
        HolyPriest = ((int)WoWClass.Priest << 8) + 1,
        ShadowPriest = ((int)WoWClass.Priest << 8) + 2,

        AssasinationRogue = ((int)WoWClass.Rogue << 8) + 0,
        CombatRogue = ((int)WoWClass.Rogue << 8) + 1,
        SubtletyRogue = ((int)WoWClass.Rogue << 8) + 2,

        ElementalShaman = ((int)WoWClass.Shaman << 8) + 0,
        EnhancementShaman = ((int)WoWClass.Shaman << 8) + 1,
        RestorationShaman = ((int)WoWClass.Shaman << 8) + 2,

        AfflictionWarlock = ((int)WoWClass.Warlock << 8) + 0,
        DemonologyWarlock = ((int)WoWClass.Warlock << 8) + 1,
        DestructionWarlock = ((int)WoWClass.Warlock << 8) + 2,

        ArmsWarrior = ((int)WoWClass.Warrior << 8) + 0,
        FuryWarrior = ((int)WoWClass.Warrior << 8) + 1,
        ProtectionWarrior = ((int)WoWClass.Warrior << 8) + 2,

        // BrewmasterMonk = ((int)WoWClass.Monk << 8) + 0,
        // MistweaverMonk = ((int)WoWClass.Monk << 8) + 1,
        // WindwalkerMonk = ((int)WoWClass.Monk << 8) + 2,
    }

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

        public static TalentSpec CurrentSpec { get; private set; }

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
            TalentSpec oldSpec = CurrentSpec;

            Update();

            if (CurrentSpec != oldSpec)
            {
                Logger.Write("Your spec has been changed. Rebuilding behaviors");
                SingularRoutine.Instance.CreateBehaviors();
            }
        }

        public static void Update()
        {
            WoWClass myClass = StyxWoW.Me.Class;
            int specBuild = 0;
            int specClassMask = ((int)StyxWoW.Me.Class << 8);

            // Keep the frame stuck so we can do a bunch of injecting at once.
            using (StyxWoW.Memory.AcquireFrame())
            {
                string s = Lua.GetReturnVal<string>("return GetSpecialization()", 0);
                if (String.IsNullOrEmpty(s) || !Int32.TryParse( s, out specBuild))
                {
                    CurrentSpec = TalentSpec.Lowbie;
                    return;
                }

                CurrentSpec = (TalentSpec)(specClassMask + specBuild - 1);

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