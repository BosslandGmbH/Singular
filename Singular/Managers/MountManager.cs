using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using System;
using Singular.Settings;

namespace Singular.Managers
{
    // This class is here to deal with Ghost Wolf/Travel Form usage for shamans and druids
    internal static class MountManager
    {
        internal static void Init()
        {
            Mount.OnMountUp += Mount_OnMountUp;
        }

        private static void Mount_OnMountUp(object sender, MountUpEventArgs e)
        {
            if (e.Destination == WoWPoint.Zero)
                return;

            if (SpellManager.GlobalCooldown || StyxWoW.Me.IsCasting || StyxWoW.Me.IsChanneling )
                return;

            if (e.Destination.Distance(StyxWoW.Me.Location) < Styx.Helpers.CharacterSettings.Instance.MountDistance && (!Battlegrounds.IsInsideBattleground || DateTime.Now > Battlegrounds.BattlefieldStartTime))
            {
                if (SpellManager.HasSpell("Ghost Wolf") && SingularSettings.Instance.Shaman.UseGhostWolf)
                {
                    e.Cancel = true;
                    if (!StyxWoW.Me.HasAura("Ghost Wolf"))
                    {
                        Logger.Write("Using Ghost Wolf instead of mounting");
                        SpellManager.Cast("Ghost Wolf");
                    }
                }
                else if (SpellManager.HasSpell("Travel Form"))
                {
                    e.Cancel = true;

                    if (!StyxWoW.Me.HasAura("Travel Form"))
                    {
                        Logger.Write("Using Travel Form instead of mounting.");
                        SpellManager.Cast("Travel Form");
                    }
                }
                else if (StyxWoW.Me.IsMoving && SpellManager.HasSpell("Angelic Feathers") && SingularSettings.Instance.Priest.UseSpeedBuff)
                {
                    Logger.Write("Using Angelic Feathers instead of mounting");
                    SpellManager.Cast("Angelic Feathers");
                    SpellManager.ClickRemoteLocation(StyxWoW.Me.Location);
                    Lua.DoString("SpellStopTargeting()");
                }
                else if (SpellManager.HasSpell("Sprint") && SingularSettings.Instance.Rogue.UseSprint )
                {
                    Logger.Write("Using Sprint instead of mounting");
                    SpellManager.Cast("Sprint");
                }
            }
        }
    }
}