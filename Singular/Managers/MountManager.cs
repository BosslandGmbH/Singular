using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using System;
using Singular.Settings;
using Singular.Helpers;
using System.Drawing;

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
            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds && PVP.PrepTimeLeft > 5)
            {
                e.Cancel = true;
                return;
            }

            if (e.Destination == WoWPoint.Zero)
                return;

            if (Spell.GcdActive || StyxWoW.Me.IsCasting || StyxWoW.Me.ChanneledSpell != null )
                return;

            if (e.Destination.Distance(StyxWoW.Me.Location) < Styx.Helpers.CharacterSettings.Instance.MountDistance && (!Battlegrounds.IsInsideBattleground || !PVP.IsPrepPhase) && !Utilities.EventHandlers.IsShapeshiftSuppressed)
            {
                if (StyxWoW.Me.Class == WoWClass.Shaman && SpellManager.HasSpell("Ghost Wolf") && SingularSettings.Instance.Shaman().UseGhostWolf)
                {
                    e.Cancel = true;
                    if (!StyxWoW.Me.HasAura("Ghost Wolf"))
                    {
                        Logger.Write("Using Ghost Wolf instead of mounting");
                        SpellManager.Cast("Ghost Wolf");
                    }
                }
                else if (StyxWoW.Me.Class == WoWClass.Druid && SpellManager.HasSpell("Travel Form") && SingularSettings.Instance.Druid().UseTravelForm && StyxWoW.Me.IsOutdoors)
                {
                    e.Cancel = true;

                    if (!StyxWoW.Me.HasAura("Travel Form"))
                    {
                        Logger.Write("Using Travel Form instead of mounting.");
                        SpellManager.Cast("Travel Form");
                    }
                }
            }

            if (StyxWoW.Me.Class == WoWClass.Shaman && SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds && ClassSpecific.Shaman.Totems.NeedToRecallTotems )
            {
                Logger.WriteDiagnostic("OnMountUp: recalling totems since about to mount");
                ClassSpecific.Shaman.Totems.RecallTotems();
            }

			// Note: This causes issues if another delegate called after Singular's cancels mount up resulting in shuttering.
            // help HB out and stop immediately if we allow mount to proceed
			//if (e.Cancel == false && StyxWoW.Me.IsMoving && Mount.CanMount())
			//{
			//	Logger.WriteDebug( Color.White, "OnMountUp: stopping to help HB mount quicker (fewer failed mount attempts)");
			//	StopMoving.Now();
			//}
        }
    }
}