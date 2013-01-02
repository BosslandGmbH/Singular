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

                if (map.IsDungeon)
                {
                    return WoWContext.Instances;
                }

                if (Me.IsInGroup())
                {
                    // return SingularSettings.Instance.WorldGroupBehaviors;
                    return WoWContext.Instances;
                }

                // return SingularSettings.Instance.WorldSoloBehaviors;
                return WoWContext.Normal;
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
                sRunningAs = "Solo";
            else
            {
                if (Me.CurrentMap.IsArena)
                    sRunningAs = " Arena ";
                else if (Me.CurrentMap.IsBattleground)
                    sRunningAs = " Battleground ";
                else if (Me.CurrentMap.IsScenario)
                    sRunningAs = " Scenario ";
                else if (Me.CurrentMap.IsDungeon)
                    sRunningAs = " Dungeon ";
                else if (Me.CurrentMap.IsRaid)
                    sRunningAs = " Raid ";
                else if (Me.CurrentMap.IsInstance)
                    sRunningAs = " Instance ";

                if (!Me.IsInGroup())
                    sRunningAs = "Solo " + sRunningAs;
                else
                    sRunningAs = string.Format("{0}m {1}", (int) Math.Max(Me.CurrentMap.MaxPlayers, Me.GroupInfo.GroupSize), sRunningAs);
            }

            Logger.Write(Color.LightGreen, "... running the {0} bot {1}in {2}",
                 GetBotName(),
                 sRunningAs,
                 Me.RealZoneText
                );

            Item.WriteCharacterGearAndSetupInfo();

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
                        if (mmb.SecondaryBot.RequirementsMet)
                            return mmb.SecondaryBot != null ? "Mixed:" + mmb.SecondaryBot.Name : "Mixed:[secondary null]";
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

    }
}
