using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.WoWInternals;

namespace Singular
{
    partial class SingularRoutine
    {
        public void AttachEventHandlers()
        {
            BotEvents.Player.OnMapChanged += Player_OnMapChanged;

            Lua.Events.AttachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLog);
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
            //Logger.WriteDebug("[CombatLog] " + e.Event + " - " + e.SourceName + " - " + e.SpellName);
            switch (e.Event)
            {
                case "SPELL_AURA_APPLIED":
                case "SPELL_CAST_SUCCESS":
                    if (e.SourceGuid != Me.Guid)
                        return;
                    // Update the last spell we cast. So certain classes can 'switch' their logic around.
                    LastSpellCast = e.SpellName;
                    Logger.Write("Successfully cast " + LastSpellCast);

                    if (_sleepAfterSuccessSpells.Contains(e.SpellName))
                        StyxWoW.SleepForLagDuration();

                    // Force a wait for all summoned minions. This prevents double-casting it.
                    if (Class == WoWClass.Warlock && e.SpellName.StartsWith("Summon "))
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
