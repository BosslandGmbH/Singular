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
                    "return args[2] == 'SPELL_CAST_SUCCESS' or args[2] == 'SPELL_AURA_APPLIED' or args[2] == 'SPELL_DAMAGE' or args[2] == 'SPELL_AURA_REFRESH' or args[2] == 'SPELL_AURA_REMOVED'or args[2] == 'SPELL_MISSED' or args[2] == 'RANGE_MISSED' or args[2] =='SWING_MISSED'"))
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
            //Logger.WriteDebug("[CombatLog] " + e.Event + " - " + e.SourceName + " - " + e.SpellName);
            switch (e.Event)
            {
                case "SPELL_AURA_APPLIED":
                    if (e.SourceGuid == StyxWoW.Me.Guid)
                    {
                        if (e.SpellId == 1822)
                        {
                            Common.RakeMultiplier = 1;
                            //TF
                            if (StyxWoW.Me.HasAura(5217))
                                Common.RakeMultiplier = Common.RakeMultiplier*1.15;
                            //Savage Roar
                            if (StyxWoW.Me.HasAura(127538))
                                Common.RakeMultiplier = Common.RakeMultiplier*1.3;
                            //Doc
                            if (StyxWoW.Me.HasAura(108373))
                                Common.RakeMultiplier = Common.RakeMultiplier*1.25;
                        }
                        if (e.SpellId == 1079)
                        {
                            Common.ExtendedRip = 0;
                            Common.RipMultiplier = 1;
                            //TF
                            if (StyxWoW.Me.HasAura(5217))
                                Common.RipMultiplier = Common.RipMultiplier*1.15;
                            //Savage Roar
                            if (StyxWoW.Me.HasAura(127538))
                                Common.RipMultiplier = Common.RipMultiplier*1.3;
                            //Doc
                            if (StyxWoW.Me.HasAura(108373))
                                Common.RipMultiplier = Common.RipMultiplier*1.25;
                        }
                    }
                    break;

                case "SPELL_AURA_REFRESH":
                    if (e.SourceGuid == StyxWoW.Me.Guid)
                    {
                        if (e.SpellId == 1822)
                        {
                            Common.RakeMultiplier = 1;
                            //TF
                            if (StyxWoW.Me.HasAura(5217))
                                Common.RakeMultiplier = Common.RakeMultiplier*1.15;
                            //Savage Roar
                            if (StyxWoW.Me.HasAura(127538))
                                Common.RakeMultiplier = Common.RakeMultiplier*1.3;
                            //Doc
                            if (StyxWoW.Me.HasAura(108373))
                                Common.RakeMultiplier = Common.RakeMultiplier*1.25;
                        }
                        if (e.SpellId == 1079)
                        {
                            Common.ExtendedRip = 0;
                            Common.RipMultiplier = 1;
                            //TF
                            if (StyxWoW.Me.HasAura(5217))
                                Common.RipMultiplier = Common.RipMultiplier*1.15;
                            //Savage Roar
                            if (StyxWoW.Me.HasAura(127538))
                                Common.RipMultiplier = Common.RipMultiplier*1.3;
                            //Doc
                            if (StyxWoW.Me.HasAura(108373))
                                Common.RipMultiplier = Common.RipMultiplier*1.25;
                        }
                    }
                    break;

                case "SPELL_DAMAGE":
                    if (e.SourceGuid == StyxWoW.Me.Guid)
                    {
                        if (e.SpellId == 5221 || e.SpellId == 114236 || e.SpellId == 102545)
                            //Normal Shred, Glyphed Shred, Ravage
                            Common.ExtendedRip = Common.ExtendedRip + 1;
                    }
                    break;

                case "SPELL_AURA_REMOVED":
                    if (e.SourceGuid == StyxWoW.Me.Guid)
                    {
                        if (e.SpellId == 1822)
                        {
                            Common.RakeMultiplier = 0;
                        }
                        if (e.SpellId == 1079)
                        {
                            Common.ExtendedRip = 0;
                            Common.RipMultiplier = 0;
                        }
                    }
                    break;
                case "SPELL_CAST_SUCCESS":
                    if (e.SourceGuid != StyxWoW.Me.Guid)
                    {
                        return;
                    }
                    if (StyxWoW.Me.Class == WoWClass.Druid)
                        Common.prevSwift = e.SpellId == 132158;

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