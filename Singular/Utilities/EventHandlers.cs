#region

using System;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Routines;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Common = Singular.ClassSpecific.Druid.Common;
using Singular.Settings;
using System.Globalization;
using Styx.Common;

#endregion

namespace Singular.Utilities
{
    public static class EventHandlers
    {
        private static bool _combatLogAttached;

        public static void Init()
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds &&
                !StyxWoW.Me.CurrentMap.IsRaid)
                AttachCombatLogEvent();

            SingularRoutine.OnWoWContextChanged += HandleContextChanged;
        }

        internal static void HandleContextChanged(object sender, WoWContextEventArg e)
        {
            // Since we hooked this in ctor, make sure we are the selected CC
            if (RoutineManager.Current.Name != SingularRoutine.Instance.Name)
                return; 
            
            if (e.CurrentContext == WoWContext.Battlegrounds || StyxWoW.Me.CurrentMap.IsRaid)
                DetachCombatLogEvent();
            else
                AttachCombatLogEvent();
        }

        private static void AttachCombatLogEvent()
        {
            if (_combatLogAttached)
                return;

            // DO NOT EDIT THIS UNLESS YOU KNOW WHAT YOU'RE DOING!
            // This ensures we only capture certain combat log events, not all of them.
            // This saves on performance, and possible memory leaks. (Leaks due to Lua table issues.)
            Lua.Events.AttachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLog);
            if (
                !Lua.Events.AddFilter(
                    "COMBAT_LOG_EVENT_UNFILTERED",
                    "return args[2] == 'SPELL_CAST_SUCCESS' or args[2] == 'SPELL_AURA_APPLIED' or args[2] == 'SPELL_MISSED' or args[2] == 'RANGE_MISSED' or args[2] == 'SWING_MISSED' or args[2] == 'SPELL_CAST_FAILED'"))
            {
                Logger.Write(
                    "ERROR: Could not add combat log event filter! - Performance may be horrible, and things may not work properly!");
            }

            Logger.WriteDebug("Attached combat log");
            _combatLogAttached = true;
        }
        
        private static void DetachCombatLogEvent()
        {
            if (!_combatLogAttached)
                return;

            Logger.WriteDebug("Detached combat log");
            Lua.Events.DetachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLog);
            _combatLogAttached = false;
        }

        private static void HandleCombatLog(object sender, LuaEventArgs args)
        {
            var e = new CombatLogEventArgs(args.EventName, args.FireTimeStamp, args.Args);

            if (e.SourceGuid != StyxWoW.Me.Guid)
                return;

            // Logger.WriteDebug("[CombatLog] " + e.Event + " - " + e.SourceName + " - " + e.SpellName);

            switch (e.Event)
            {
                default:
                    Logger.WriteDebug("[CombatLog] filter out this event -- " + e.Event + " - " + e.SourceName + " - " + e.SpellName);
                    break;

                case "SPELL_CAST_FAILED":
                    if (SingularSettings.Instance.EnableDebugLogging)
                    {
                        Logger.WriteDebug("[CombatLog] {0}:{1} cast of {2}#{3} failed: '{6}'",
                            e.SourceName,
                            e.SourceGuid,
                            e.SpellName,
                            e.SpellId,
                            e.Args[14]
                            );
                    }
                    break;

                case "SPELL_AURA_APPLIED":
                case "SPELL_CAST_SUCCESS":
                    if (e.SourceGuid != StyxWoW.Me.Guid)
                    {
                        return;
                    }

                    // Update the last spell we cast. So certain classes can 'switch' their logic around.
                    Spell.LastSpellCast = e.SpellName;
                    //Logger.WriteDebug("Successfully cast " + Spell.LastSpellCast);

                    // Force a wait for all summoned minions. This prevents double-casting it.
                    if (StyxWoW.Me.Class == WoWClass.Warlock && e.SpellName.StartsWith("Summon "))
                    {
                        StyxWoW.SleepForLagDuration();
                    }
                    break;

                case "SWING_MISSED":
                    if (e.Args[11].ToString() == "EVADE")
                    {
                        Logger.Write("Mob is evading swing. Blacklisting it!");
                        Blacklist.Add(e.DestGuid, TimeSpan.FromMinutes(30));
                        if (StyxWoW.Me.CurrentTargetGuid == e.DestGuid)
                        {
                            StyxWoW.Me.ClearTarget();
                        }

                        BotPoi.Clear("Blacklisting evading mob");
                        StyxWoW.SleepForLagDuration();
                    }
                    else if (e.Args[11].ToString() == "IMMUNE")
                    {
                        WoWUnit unit = e.DestUnit;
                        if (unit != null && !unit.IsPlayer)
                        {
                            Logger.WriteDebug("{0} is immune to {1} spell school", unit.Name, e.SpellSchool);
                            SpellImmunityManager.Add(unit.Entry, e.SpellSchool);
                        }
                    }
                    break;

                case "SPELL_MISSED":
                case "RANGE_MISSED":
                    // DoT casting spam can occur when running on test dummy with low +hit
                    //  ..  and multiple misses occurring. this should help troubleshoot
                    //  ..  false reports of flawed rotation
                    if (SingularSettings.Instance.EnableDebugLogging)
                    {
                        Logger.WriteFile(
                            "[CombatLog] {0} {1}#{2} {3}",
                            e.Event,
                            e.SpellName,
                            e.SpellId,
                            e.Args[14]
                            );
                    }

                    if (e.Args[14].ToString() == "EVADE")
                    {
                        Logger.Write("Mob is evading ranged attack. Blacklisting it!");
                        Blacklist.Add(e.DestGuid, TimeSpan.FromMinutes(30));
                        if (StyxWoW.Me.CurrentTargetGuid == e.DestGuid)
                        {
                            StyxWoW.Me.ClearTarget();
                        }

                        BotPoi.Clear("Blacklisting evading mob");
                        StyxWoW.SleepForLagDuration();
                    }
                    else if (e.Args[14].ToString() == "IMMUNE")
                    {
                        WoWUnit unit = e.DestUnit;
                        if (unit != null && !unit.IsPlayer)
                        {
                            Logger.WriteDebug("{0} is immune to {1} spell school", unit.Name, e.SpellSchool);
                            SpellImmunityManager.Add(unit.Entry, e.SpellSchool);
                        }
                    }
                    break;
            }
        }
    }
}