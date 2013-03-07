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
        private static WoWContext _lastContext;

        internal static bool IsQuesting { get; set; }

        internal static WoWContext CurrentWoWContext
        {
            get
            {
                if(!StyxWoW.IsInGame)
                    return WoWContext.None;

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

        private bool _contextEventSubscribed;
        private void UpdateContext()
        {
            // Subscribe to the map change event, so we can automatically update the context.
            if(!_contextEventSubscribed)
            {
                // Subscribe to OnBattlegroundEntered. Just 'cause.
                BotEvents.Battleground.OnBattlegroundEntered += e => UpdateContext();
                _contextEventSubscribed = true;
            }

            var current = CurrentWoWContext;

            // Can't update the context when it doesn't exist.
            if (current == WoWContext.None)
                return;

            if(current != _lastContext && OnWoWContextChanged!=null)
            {
                IsQuesting = IsBotInUse("Quest");

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
            }
                
        }

        public static void DescribeContext()
        {
            string sRace = Me.Race.ToString().CamelToSpaced();
            if (Me.Race == WoWRace.Pandaren)
                sRace = " " + Me.FactionGroup.ToString() + sRace;

            Logger.Write(Color.LightGreen, "Your Level {0}{1} {2} {3} Build is", Me.Level, sRace, SpecializationName(), Me.Class.ToString() );

            string sRunningAs = "";

            if (Me.CurrentMap == null)
                sRunningAs = "Unknown";
            else if (Me.CurrentMap.IsArena)
                sRunningAs = " Arena ";
            else if (Me.CurrentMap.IsBattleground)
                sRunningAs = " Battleground ";
            else if (Me.CurrentMap.IsScenario)
                sRunningAs = " Scenario ";

            Logger.Write(Color.LightGreen, "... running the {0} bot in {1} {2}",
                 GetBotName(),
                 Me.RealZoneText,
                 !Me.IsInInstance || Battlegrounds.IsInsideBattleground ? "" : GetInstanceDifficultyName()
                );

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

        private static int GetInstanceDifficulty( )
        {
            int diffidx = Lua.GetReturnVal<int>("return GetInstanceDifficulty()", 0);
            return diffidx;
        }


        private static string[] _InstDiff = new string[] 
        {
            /* 0*/  "Unknown Difficulty",
            /* 1*/  "None; not in an Instance",
            /* 2*/  "5-player Normal",
            /* 3*/  "5-player Heroic",
            /* 4*/  "10-player Raid",
            /* 5*/  "25-player Raid",
            /* 6*/  "10-player Heroic Raid",
            /* 7*/  "25-player Heroic Raid",
            /* 8*/  "LFR Raid Instance",
            /* 9*/  "Challenge Mode Raid",
            /* 10*/  "40-player Raid"
        };

        private static string GetInstanceDifficultyName( )
        {
            int diff = GetInstanceDifficulty();
            if (diff < _InstDiff.GetLowerBound(0) || diff > _InstDiff.GetUpperBound(0))
                return string.Format("Difficulty {0} Undefined", diff);

            return _InstDiff[diff];
        }

    }
}
