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

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.POI;
using Styx.WoWInternals;

namespace Singular
{
    partial class SingularRoutine
    {
        public readonly HashSet<string> _sleepAfterSuccessSpells = new HashSet<string>();

        public void AttachEventHandlers()
        {
            BotEvents.Player.OnMapChanged += Player_OnMapChanged;

            // DO NOT EDIT THIS UNLESS YOU KNOW WHAT YOU'RE DOING!
            // This ensures we only capture certain combat log events, not all of them.
            // This saves on performance, and possible memory leaks. (Leaks due to Lua table issues.)
            Lua.Events.AttachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLog);
            if (
                !Lua.Events.AddFilter(
                    "COMBAT_LOG_EVENT_UNFILTERED",
                    "return args[2] == 'SPELL_CAST_SUCCESS' or args[2] == 'SPELL_AURA_APPLIED' or args[2] == 'SPELL_MISSED'"))
            {
                Logger.Write("ERROR: Could not add combat log event filter! - Performance may be horrible, and things may not work properly!");
            }
        }

        /// <summary>
        ///   Adds a spell to the succeed wait list. When this spell is successfully cast, the event log handler will forcibly sleep the thread
        ///   for your lag duration. (This is mostly to prevent double-casts due to slow updating of buffs.)
        /// </summary>
        /// <remarks>
        ///   Created 3/4/2011.
        /// </remarks>
        /// <param name = "spellName">Name of the spell.</param>
        protected void AddSpellSucceedWait(string spellName)
        {
            if (!_sleepAfterSuccessSpells.Contains(spellName))
            {
                _sleepAfterSuccessSpells.Add(spellName);
            }
        }

        private void HandleCombatLog(object sender, LuaEventArgs args)
        {
            var e = new CombatLogEventArgs(args.EventName, args.FireTimeStamp, args.Args);
            //Logger.WriteDebug("[CombatLog] " + e.Event + " - " + e.SourceName + " - " + e.SpellName);
            switch (e.Event)
            {
                case "SPELL_AURA_APPLIED":
                case "SPELL_CAST_SUCCESS":
                    if (e.SourceGuid != Me.Guid)
                    {
                        return;
                    }
                    // Update the last spell we cast. So certain classes can 'switch' their logic around.
                    LastSpellCast = e.SpellName;
                    Logger.WriteDebug("Successfully cast " + LastSpellCast);

                    if (_sleepAfterSuccessSpells.Contains(e.SpellName))
                    {
                        StyxWoW.SleepForLagDuration();
                    }

                    // Force a wait for all summoned minions. This prevents double-casting it.
                    if (myClass == WoWClass.Warlock && e.SpellName.StartsWith("Summon "))
                    {
                        StyxWoW.SleepForLagDuration();
                    }
                    break;

                case "SPELL_MISSED":
                    //Logger.Write(e.Args.ToRealString());
                    if (e.Args[11].ToString() == "EVADE")
                    {
                        Logger.Write("Mob is evading. Blacklisting it!");
                        Blacklist.Add(e.DestGuid, TimeSpan.FromMinutes(30));
                        if (StyxWoW.Me.CurrentTargetGuid == e.DestGuid)
                        {
                            StyxWoW.Me.ClearTarget();
                        }

                        BotPoi.Clear("Blacklisting evading mob");
                        StyxWoW.SleepForLagDuration();
                    }
                    break;
            }
        }

        private void Player_OnMapChanged(BotEvents.Player.MapChangedEventArgs args)
        {
            //Why would we create same behaviors all over ?
            if (lastContext == CurrentWoWContext)
            {
                return;
            }

            Logger.Write("Context changed. New context: " + CurrentWoWContext + ". Rebuilding behaviors.");
            CreateBehaviors();
        }
    }
}