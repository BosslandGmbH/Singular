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
using System.Drawing;
using System.Collections.Generic;

#endregion

namespace Singular.Utilities
{
    public static class EventHandlers
    {
        private static bool _combatLogAttached;

        public static void Init()
        {
            if (SingularSettings.Debug || (SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds && !StyxWoW.Me.CurrentMap.IsRaid))
                AttachCombatLogEvent();

            SingularRoutine.OnWoWContextChanged += HandleContextChanged;
        }

        internal static void HandleContextChanged(object sender, WoWContextEventArg e)
        {
            // Since we hooked this in ctor, make sure we are the selected CC
            if (RoutineManager.Current.Name != SingularRoutine.Instance.Name)
                return;

            if (SingularSettings.Debug || (SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds && !StyxWoW.Me.CurrentMap.IsRaid))
                AttachCombatLogEvent();
            else
                DetachCombatLogEvent();
        }

        /// <summary>
        /// time of last "Target not in line of sight" spell failure.
        /// Used by movement functions for situations where the standard
        /// LoS and LoSS functions are true but still fails in WOW.
        /// See CreateMoveToLosBehavior() for usage
        /// </summary>
        public static DateTime LastLineOfSightError { get; set; }
        public static DateTime LastUnitNotInfrontError { get; set; }
        public static DateTime LastShapeshiftError { get; set; }

        /// <summary>
        /// the value of localized values for testing certain types of spell failures
        /// </summary>
        private static string LocalizedLineOfSightError;
        private static string LocalizedUnitNotInfrontError;
        private static HashSet<string> LocalizedShapeshiftErrors;

        private static void AttachCombatLogEvent()
        {
            if (_combatLogAttached)
                return;

            // DO NOT EDIT THIS UNLESS YOU KNOW WHAT YOU'RE DOING!
            // This ensures we only capture certain combat log events, not all of them.
            // This saves on performance, and possible memory leaks. (Leaks due to Lua table issues.)
            Lua.Events.AttachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLog);

            string filterCriteria =
                "return args[4] == UnitGUID('player')"
                + " and (args[2] == 'SPELL_MISSED'"
                + " or args[2] == 'RANGE_MISSED'"
                + " or args[2] == 'SWING_MISSED'"
                + " or args[2] == 'SPELL_CAST_FAILED')";

            if (!Lua.Events.AddFilter("COMBAT_LOG_EVENT_UNFILTERED", filterCriteria ))
            {
                Logger.Write( "ERROR: Could not add combat log event filter! - Performance may be horrible, and things may not work properly!");
            }

            // get localized copies of spell failure error messages
            LocalizedLineOfSightError = Lua.GetReturnVal<string>("return SPELL_FAILED_LINE_OF_SIGHT", 0);
            LocalizedUnitNotInfrontError = Lua.GetReturnVal<string>("return SPELL_FAILED_UNIT_NOT_INFRONT", 0);

            LocalizedShapeshiftErrors = new HashSet<string>();
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return ERR_CANT_INTERACT_SHAPESHIFTED", 0));
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return ERR_MOUNT_SHAPESHIFTED", 0));
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return ERR_NOT_WHILE_SHAPESHIFTED", 0));
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return ERR_NO_ITEMS_WHILE_SHAPESHIFTED", 0));
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return ERR_SHAPESHIFT_FORM_CANNOT_EQUIP", 0));
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return ERR_TAXIPLAYERSHAPESHIFTED", 0));
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return SPELL_FAILED_CUSTOM_ERROR_125", 0));
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return SPELL_FAILED_CUSTOM_ERROR_99", 0));
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return SPELL_FAILED_NOT_SHAPESHIFT", 0));
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return SPELL_FAILED_NO_ITEMS_WHILE_SHAPESHIFTED", 0));
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return SPELL_NOT_SHAPESHIFTED", 0));
            LocalizedShapeshiftErrors.Add(Lua.GetReturnVal<string>("return SPELL_NOT_SHAPESHIFTED_NOSPACE", 0));

            LastLineOfSightError = DateTime.MinValue;
            LastUnitNotInfrontError = DateTime.MinValue;
            LastShapeshiftError = DateTime.MinValue;
                      
            Logger.WriteDebug("Attached combat log");
            _combatLogAttached = true;
        }
        
        private static void DetachCombatLogEvent()
        {
            if (!_combatLogAttached)
                return;

            Logger.WriteDebug("Removed combat log filter");
            Lua.Events.RemoveFilter("COMBAT_LOG_EVENT_UNFILTERED");
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
                    if ( SingularSettings.Debug )
                        Logger.WriteDebug("[CombatLog] filter out this event -- " + e.Event + " - " + e.SourceName + " - " + e.SpellName);
                    break;

                // spell_cast_failed only passes filter in Singular debug mode
                case "SPELL_CAST_FAILED":
                    Logger.WriteDebug(
                        "[CombatLog] {0} {1}#{2} failure: '{3}'",
                        e.Event,
                        e.Spell.Name,
                        e.SpellId,
                        e.Args[14]
                        );

                    if ( e.Args[14].ToString() == LocalizedLineOfSightError )
                    {
                        LastLineOfSightError = DateTime.Now;
                        Logger.WriteFile("[CombatLog] cast fail due to los reported at {0}", LastLineOfSightError.ToString("HH:mm:ss.fff"));
                    }
                    else if ( StyxWoW.Me.Class == WoWClass.Druid && SingularRoutine.IsQuesting)
                    {
                        if (LocalizedShapeshiftErrors.Contains(e.Args[14].ToString()))
                        {
                            LastShapeshiftError = DateTime.Now;
                            Logger.WriteFile("[CombatLog] cast fail due to shapeshift error while questing reported at {0}", LastShapeshiftError.ToString("HH:mm:ss.fff"));
                        }
                    }   
                    break;

#if SOMEONE_USES_LAST_SPELL_AT_SOME_POINT
                case "SPELL_AURA_APPLIED":
                case "SPELL_CAST_SUCCESS":
                    if (e.SourceGuid != StyxWoW.Me.Guid)
                    {
                        return;
                    }

                    // Update the last spell we cast. So certain classes can 'switch' their logic around.
                    Spell.LastSpellCast = e.SpellName;
                    //Logger.WriteDebug("Successfully cast " + Spell.LastSpellCast);

                    // following commented block should not be needed since rewrite of Pet summon
                    //
                    //// Force a wait for all summoned minions. This prevents double-casting it.
                    //if (StyxWoW.Me.Class == WoWClass.Warlock && e.SpellName.StartsWith("Summon "))
                    //{
                    //    StyxWoW.SleepForLagDuration();
                    //}
                    break;
#endif

                case "SWING_MISSED":
                    if (e.Args[11].ToString() == "EVADE")
                    {
                        HandleEvadeBuggedMob(args, e);
                    }
                    else if (e.Args[11].ToString() == "IMMUNE")
                    {
                        WoWUnit unit = e.DestUnit;
                        if (unit != null && !unit.IsPlayer)
                        {
                            Logger.WriteDebug("{0} is immune to Physical spell school", unit.Name);
                            SpellImmunityManager.Add(unit.Entry, WoWSpellSchool.Physical );
                        }
                    }
                    break;

                case "SPELL_MISSED":
                case "RANGE_MISSED":
                    // Why log misses?  Because users of classes with DoTs testing on training dummies
                    // .. that they don't have enough +Hit for will get DoT spam.  This allows easy
                    // .. diagnosis of false reports of rotation issues where a user simply isn't geared
                    // .. this happens more at the beginning of an expansion especially
                    if (SingularSettings.Debug)
                    {
                        Logger.WriteDebug(
                            "[CombatLog] {0} {1}#{2} {3}",
                            e.Event,
                            e.Spell.Name,
                            e.SpellId,
                            e.Args[14]
                            );
                    }

                    if (e.Args[14].ToString() == "EVADE")
                    {
                        HandleEvadeBuggedMob(args, e);
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

        private static void HandleEvadeBuggedMob(LuaEventArgs args, CombatLogEventArgs e)
        {
            WoWUnit unit = e.DestUnit;
            ulong guid = e.DestGuid;

            if (unit == null && StyxWoW.Me.CurrentTarget != null)
            {
                unit = StyxWoW.Me.CurrentTarget;
                guid = StyxWoW.Me.CurrentTargetGuid;
                Logger.WriteDebug("Evade: bugged mob guid:{0}, so assuming current target instead", args.Args[7]);
            }

            if (unit != null)
            {
                Logger.Write("Mob {0}is evading, [{1}]. Blacklisting it! {2}", unit.SafeName(), e.Event, unit.Guid );
                Blacklist.Add(unit.Guid, BlacklistFlags.Combat, TimeSpan.FromMinutes(30));

                if (!Blacklist.Contains(unit.Guid, BlacklistFlags.Combat))
                {
                    Logger.Write(Color.Pink, "error: blacklist does not contain entry for {0} just added {1}", unit.SafeName(), unit.Guid);
                }
                
                if (BotPoi.Current.Guid == unit.Guid)
                {
                    BotPoi.Clear("Blacklisting evading mob");
                }

                if (StyxWoW.Me.CurrentTargetGuid == guid)
                {
                    Logger.WriteDebug("Evade: clear target");
                    StyxWoW.Me.ClearTarget();
                }

            }

            /// line below was originally in Evade logic, but commenting to avoid Sleeps
            // StyxWoW.SleepForLagDuration();
        }
    }
}