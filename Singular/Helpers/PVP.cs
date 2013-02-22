using System.Linq;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System;

namespace Singular.Helpers
{
    internal static class PVP
    {
        /// <summary>
        /// determines if you are inside a battleground/arena prior to start.  this was previously
        /// known as the preparation phase easily identified by a Preparation or Arena Preparation
        /// buff, however those auras were removed in MoP
        /// </summary>
        /// <returns>true if in Battleground/Arena prior to start, false otherwise</returns>
        public static bool IsPrepPhase
        {
            get
            {
                return Battlegrounds.IsInsideBattleground && PrepTimeLeft > 0;
            }
        }

        public static int PrepTimeLeft
        {
            get
            {
                return Math.Max(0, (int)(BattlegroundStart - DateTime.Now).TotalSeconds);
            }
        }

        public static DateTime BattlegroundStart
        {
            get;
            private set;
        }


        //public static bool IsCrowdControlled(this WoWUnit unit)
        //{
        //    return unit.GetAllAuras().Any(a => a.IsHarmful &&
        //        (a.Spell.Mechanic == WoWSpellMechanic.Shackled ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Polymorphed ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Horrified ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Rooted ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Frozen ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Stunned ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Fleeing ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Banished ||
        //        a.Spell.Mechanic == WoWSpellMechanic.Sapped));
        //}

        public static bool IsStunned(this WoWUnit unit)
        {
            return unit.HasAuraWithMechanic(WoWSpellMechanic.Stunned, WoWSpellMechanic.Incapacitated);
        }

        public static bool IsRooted(this WoWUnit unit)
        {
            return unit.HasAuraWithMechanic(WoWSpellMechanic.Rooted, WoWSpellMechanic.Shackled);
        }

        public static bool IsSilenced(WoWUnit unit)
        {
            return unit.GetAllAuras().Any(a => a.IsHarmful &&
                (a.Spell.Mechanic == WoWSpellMechanic.Interrupted || 
                a.Spell.Mechanic == WoWSpellMechanic.Silenced));
        }


#region Battleground Start Timer

        private static bool _startTimerAttached;

        public static void AttachStartTimer()
        {
            if (_startTimerAttached)
                return;

            Lua.Events.AttachEvent("START_TIMER", HandleStartTimer);
            SingularRoutine.OnWoWContextChanged += HandleContextChanged;           
            _startTimerAttached = true;
        }

        public static void DetachStartTimer()
        {
            if (!_startTimerAttached)
                return;

            _startTimerAttached = false;
            Lua.Events.DetachEvent("START_TIMER", HandleStartTimer);
        }

        private static void HandleStartTimer(object sender, LuaEventArgs args)
        {
            int secondsUntil = Int32.Parse(args.Args[1].ToString());
            DateTime prevStart = BattlegroundStart;
            BattlegroundStart = DateTime.Now + TimeSpan.FromSeconds(secondsUntil);

            if (!(BattlegroundStart - prevStart).TotalSeconds.Between( -1, 1))
            {
                Logger.WriteDebug("Start_Timer: Battleground starts in {0} seconds", secondsUntil);
            }
        }

        internal static void HandleContextChanged(object sender, WoWContextEventArg e)
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds)
                BattlegroundStart = DateTime.Now;
            else
                BattlegroundStart = DateTime.Now + TimeSpan.FromSeconds(120);   // just add enough for now... accurate time set by event handler
        }

#endregion

    }
}
