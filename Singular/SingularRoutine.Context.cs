using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Routines;
using Styx.WoWInternals.DBC;
using System.Drawing;
using Singular.Helpers;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using Styx.Common;

namespace Singular
{
    #region Nested type: WoWContextEventArg

    public class WoWContextEventArg : EventArgs
    {
        public readonly WoWContext CurrentContext;
        public readonly WoWContext PreviousContext;

        public WoWContextEventArg(WoWContext currentContext, WoWContext prevContext)
        {
            CurrentContext = currentContext;
            PreviousContext = prevContext;
        }
    }

    #endregion
    partial class SingularRoutine
    {
        public static event EventHandler<WoWContextEventArg> OnWoWContextChanged;
        private static WoWContext _lastContext = WoWContext.None;
        private static uint _lastMapId = 0;

        internal static WoWContext ForcedContext { get; set; }

        internal static bool IsQuestBotActive { get; set; }
        internal static bool IsBgBotActive { get; set; }
        internal static bool IsDungeonBuddyActive { get; set; }
        internal static bool IsPokeBuddyActive { get; set; }
        internal static bool IsManualMovementBotActive { get; set; }

        internal static WoWContext CurrentWoWContext
        {
            get
            {
                return DetermineCurrentWoWContext;
            }
        }

        internal static HealingContext CurrentHealContext
        {
            get
            {
                WoWContext ctx = CurrentWoWContext;
                if (ctx == WoWContext.Instances && Me.GroupInfo.IsInRaid)
                    return HealingContext.Raids;

                return (HealingContext) ctx;
            }
        }

        private static WoWContext DetermineCurrentWoWContext
        {
            get
            {
                if (!StyxWoW.IsInGame)
                    return WoWContext.None;

                if (ForcedContext != WoWContext.None)
                    return ForcedContext;

                Map map = StyxWoW.Me.CurrentMap;

                if (map.IsBattleground || map.IsArena)
                {
                    return WoWContext.Battlegrounds;
                }

                if (Me.IsInGroup())
                {
                    if (Me.IsInInstance)
                    {
                        return WoWContext.Instances;
                    }

                    if (Group.Tanks.Any())
                    {
                        return WoWContext.Instances;
                    }
                }

                return WoWContext.Normal;
            }
        }

        public static WoWContext TrainingDummyBehaviors { get; set; }

        private bool _contextEventSubscribed;
        private void UpdateContext()
        {
            // Subscribe to the map change event, so we can automatically update the context.
            if (!_contextEventSubscribed)
            {
                // Subscribe to OnBattlegroundEntered. Just 'cause.
                BotEvents.Battleground.OnBattlegroundEntered += e => UpdateContext();
                SingularRoutine.OnBotEvent += (src, arg) =>
                {
                    if (arg.Event == SingularBotEvent.BotStart || arg.Event == SingularBotEvent.BotChanged)
                    {
                        // check if any of the bot detection values have changed which we use to 
                        // .. conditionally build trees
                        if (UpdateContextStateValues())
                        {
                            // DescribeContext();
                            RebuildBehaviors();
                        }
                    }
                };
                _contextEventSubscribed = true;
            }

            var current = DetermineCurrentWoWContext;

            // Can't update the context when it doesn't exist.
            if (current == WoWContext.None)
                return;

            if(current != _lastContext && OnWoWContextChanged!=null)
            {
                // store values that require scanning lists
                UpdateContextStateValues();
                DescribeContext();
                try
                {
                    OnWoWContextChanged(this, new WoWContextEventArg(current, _lastContext));
                }
                catch
                {
                    // Eat any exceptions thrown.
                }

                _lastContext = current;
                _lastMapId = Me.MapId;
            }
            else if (_lastMapId != Me.MapId)
            {
                DescribeContext();
                _lastMapId = Me.MapId;
            }
        }

        private static bool Changed( bool currVal, ref bool storedVal)
        {
            if (( currVal && storedVal) || (!currVal && !storedVal))
                return false;

            storedVal = currVal;
            return true;
        }

        private static bool UpdateContextStateValues()
        {
            bool questBot= IsBotInUse("Quest");
            bool bgBot= IsBotInUse("BGBuddy", "BG Bot");
            bool dungeonBot= IsBotInUse("DungeonBuddy");
            bool petHack = IsPluginActive("Pokébuddy", "Pokehbuddy");
            bool manualBot = IsBotInUse("LazyRaider", "Raid Bot");

            bool changed = false;

            if (questBot != IsQuestBotActive )
            {
                changed = true;
                IsQuestBotActive = questBot;
            }

            if (bgBot != IsBgBotActive )
            {
                changed = true;
                IsBgBotActive = bgBot ;
            }

            if (dungeonBot != IsDungeonBuddyActive )
            {
                changed = true;
                IsDungeonBuddyActive = dungeonBot;
            }

            if ( petHack != IsPokeBuddyActive)
            {
                changed = true;
                IsPokeBuddyActive = petHack;
            }

            if (manualBot != IsManualMovementBotActive)
            {
                changed = true;
                IsManualMovementBotActive = manualBot;
            }

            return changed;
        } 

        static void DescribeContext()
        {
            string sRace = Me.Race.ToString().CamelToSpaced();
            if (Me.Race == WoWRace.Pandaren)
                sRace = " " + Me.FactionGroup.ToString() + sRace;

            Logging.Write(" "); // spacer before prior log text

            Logger.Write(Color.LightGreen, "Your Level {0}{1} {2} {3} Build is", Me.Level, sRace, SpecializationName(), Me.Class.ToString() );

            Logger.Write(Color.LightGreen, "... running the {0} bot in {1} {2}",
                 GetBotName(),
                 Me.RealZoneText, 
                 !Me.IsInInstance || Battlegrounds.IsInsideBattleground ? "" : "[" + GetInstanceDifficultyName() + "]"
                );

            Logger.WriteFile("   MapId            = {0}", Me.MapId);
            Logger.WriteFile("   ZoneId           = {0}", Me.ZoneId);
/*
            if (Me.CurrentMap != null && Me.CurrentMap.IsValid)
            {
                Logger.WriteFile("   AreaTableId      = {0}", Me.CurrentMap.AreaTableId);
                Logger.WriteFile("   InternalName     = {0}", Me.CurrentMap.InternalName);
                Logger.WriteFile("   IsArena          = {0}", Me.CurrentMap.IsArena.ToYN());
                Logger.WriteFile("   IsBattleground   = {0}", Me.CurrentMap.IsBattleground.ToYN());
                Logger.WriteFile("   IsContinent      = {0}", Me.CurrentMap.IsContinent.ToYN());
                Logger.WriteFile("   IsDungeon        = {0}", Me.CurrentMap.IsDungeon.ToYN());
                Logger.WriteFile("   IsInstance       = {0}", Me.CurrentMap.IsInstance.ToYN());
                Logger.WriteFile("   IsRaid           = {0}", Me.CurrentMap.IsRaid.ToYN());
                Logger.WriteFile("   IsScenario       = {0}", Me.CurrentMap.IsScenario.ToYN());
                Logger.WriteFile("   MapDescription   = {0}", Me.CurrentMap.MapDescription);
                Logger.WriteFile("   MapDescription2  = {0}", Me.CurrentMap.MapDescription2);
                Logger.WriteFile("   MapType          = {0}", Me.CurrentMap.MapType);
                Logger.WriteFile("   MaxPlayers       = {0}", Me.CurrentMap.MaxPlayers);
                Logger.WriteFile("   Name             = {0}", Me.CurrentMap.Name);
            }
*/
            string sRunningAs = "";

            if (Me.CurrentMap == null)
                sRunningAs = "Unknown";
            else if (Me.CurrentMap.IsArena)
                sRunningAs = "Arena";
            else if (Me.CurrentMap.IsBattleground)
                sRunningAs = "Battleground";
            else if (Me.CurrentMap.IsScenario)
                sRunningAs = "Scenario";
            else if (Me.CurrentMap.IsRaid)
                sRunningAs = "Raid";
            else if (Me.CurrentMap.IsDungeon)
                sRunningAs = "Dungeon";
            else if (Me.CurrentMap.IsInstance)
                sRunningAs = "Instance";
            else
                sRunningAs = "Zone: " + Me.CurrentMap.Name;

            Logger.Write(Color.LightGreen, "... {0} using my {1} Behaviors",
                 sRunningAs,
                 CurrentWoWContext == WoWContext.Normal ? "SOLO" : CurrentWoWContext.ToString().ToUpper());

            if (CurrentWoWContext != WoWContext.Battlegrounds && Me.IsInGroup())
            {
                Logger.Write(Color.LightGreen, "... in a group as {0} role with {1} of {2} players", 
                    (Me.Role & (WoWPartyMember.GroupRole.Tank | WoWPartyMember.GroupRole.Healer | WoWPartyMember.GroupRole.Damage)).ToString().ToUpper(),
                     Me.GroupInfo.NumRaidMembers, 
                     (int) Math.Max(Me.CurrentMap.MaxPlayers, Me.GroupInfo.GroupSize)
                    );
            }

            Item.WriteCharacterGearAndSetupInfo();

            Logger.WriteFile(" ");
            Logger.WriteFile("My Current Dynamic Info");
            Logger.WriteFile("=======================");
            Logger.WriteFile("Combat Reach:    {0:F4}", Me.CombatReach);
            Logger.WriteFile("Bounding Height: {0:F4}", Me.BoundingHeight );
            Logger.WriteFile(" ");

#if LOG_GROUP_COMPOSITION
            if (CurrentWoWContext == WoWContext.Instances)
            {
                int idx = 1;
                Logger.WriteFile(" ");
                Logger.WriteFile("Group Comprised of {0} members as follows:", Me.GroupInfo.NumRaidMembers);
                foreach (var pm in Me.GroupInfo.RaidMembers )
                {
                    string role = (pm.Role & ~WoWPartyMember.GroupRole.None).ToString().ToUpper() + "      ";
                    role = role.Substring( 0, 6);                   
                    
                    Logger.WriteFile( "{0} {1} {2} {3} {4} {5}",
                        idx++, 
                        role,
                        pm.IsOnline ? "online " : "offline",
                        pm.Level,
                        pm.HealthMax,
                        pm.Specialization
                        );
                }

                Logger.WriteFile(" ");
            }
#endif

            if (Styx.CommonBot.Targeting.PullDistance < 25)
                Logger.Write(Color.White, "your Pull Distance is {0:F0} yds which is low for any class!!!", Styx.CommonBot.Targeting.PullDistance);
        }

        private static string SpecializationName()
        {
            if (Me.Specialization == WoWSpec.None)
                return "Lowbie";

            string spec = Me.Specialization.ToString().CamelToSpaced();
            int idxLastSpace = spec.LastIndexOf(' ');
            if (idxLastSpace >= 0 && ++idxLastSpace < spec.Length)
                spec = spec.Substring(idxLastSpace);

            return spec;
        }

        public static string GetBotName()
        {
            BotBase bot = null;

            if (TreeRoot.Current != null)
            {
                if (!(TreeRoot.Current is NewMixedMode.MixedModeEx))
                    bot = TreeRoot.Current;
                else
                {
                    NewMixedMode.MixedModeEx mmb = (NewMixedMode.MixedModeEx)TreeRoot.Current;
                    if (mmb != null)
                    {
                        if (mmb.SecondaryBot != null && mmb.SecondaryBot.RequirementsMet)
                            return "Mixed:" + mmb.SecondaryBot.Name;
                        return mmb.PrimaryBot != null ? "Mixed:" + mmb.PrimaryBot.Name : "Mixed:[primary null]";
                    }
                }
            }

            return bot.Name;
        }

        public static bool IsBotInUse(params string[] nameSubstrings)
        {
            string botName = GetBotName().ToUpper();
            return nameSubstrings.Any( s => botName.Contains(s.ToUpper()));
        }

        public static bool IsPluginActive(params string[] nameSubstrings)
        {
            var lowerNames = nameSubstrings.Select(s => s.ToLowerInvariant()).ToList();
            bool res = Styx.Plugins.PluginManager.Plugins.Any(p => p.Enabled && lowerNames.Contains(p.Name.ToLowerInvariant()));
            return res;
        }

        private static int GetInstanceDifficulty( )
        {
			int diffidx = Lua.GetReturnVal<int>("local _,_,d=GetInstanceInfo() if d ~= nil then return d end return 1", 0);
            return diffidx;
        }


        private static readonly string[] InstDiff = new[] 
        {
            /* 0*/  "None; not in an Instance",
            /* 1*/  "5-player Normal",
            /* 2*/  "5-player Heroic",
            /* 3*/  "10-player Raid",
            /* 4*/  "25-player Raid",
            /* 5*/  "10-player Heroic Raid",
            /* 6*/  "25-player Heroic Raid",
            /* 7*/  "LFR Raid Instance",
            /* 8*/  "Challenge Mode Raid",
            /* 9*/  "40-player Raid"
        };

        private static string GetInstanceDifficultyName( )
        {
            int diff = GetInstanceDifficulty();
            if (diff >= InstDiff.Length)
                return string.Format("Difficulty {0} Undefined", diff);

            return InstDiff[diff];
        }

        public bool InCinematic()
        {
            bool inCin = Lua.GetReturnVal<bool>("return InCinematic()", 0);
            return inCin;
        }
    }
}
