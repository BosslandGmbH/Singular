using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx;
using Styx.Helpers;
using Styx.WoWInternals;

namespace Singular
{
    partial class SingularRoutine
    {
        public void AttachEventHandlers()
        {
            BotEvents.Player.OnMapChanged += Player_OnMapChanged;

            Lua.Events.AttachEvent("COMBAT_LOG_UNFILTERED", HandleCombatLog);
        }

        protected void AddSpellSucceedWait(string spellName)
        {
            if (!_sleepAfterSuccessSpells.Contains(spellName))
                _sleepAfterSuccessSpells.Add(spellName);
        }

        public HashSet<string> _sleepAfterSuccessSpells = new HashSet<string>();
        private void HandleCombatLog(object sender, LuaEventArgs args)
        {
            var e = new CombatLogEventArgs(args.EventName, args.FireTimeStamp, args.Args);

            switch (e.Event)
            {
                case "SPELL_CAST_SUCCESS":
                    if (e.SourceGuid != Me.Guid)
                        return;

                    if (_sleepAfterSuccessSpells.Contains(e.SpellName))
                        StyxWoW.SleepForLagDuration();
                    break;
            }
        }

        void Player_OnMapChanged(BotEvents.Player.MapChangedEventArgs args)
        {
            Logger.Write("Map changed. New context: " + CurrentWoWContext + ". Rebuilding behaviors.");
            CreateBehaviors();
        }
    }
}
