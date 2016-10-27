using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using System.Linq;
using System.Numerics;
using Styx.Common;

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

            if (!e.MoveDistance.HasValue)
                return;

            if (Spell.GcdActive || StyxWoW.Me.IsCasting || StyxWoW.Me.ChanneledSpell != null )
                return;

            if ((!Battlegrounds.IsInsideBattleground || !PVP.IsPrepPhase) && !Utilities.EventHandlers.IsShapeshiftSuppressed)
            {
                if (e.MoveDistance.Value < 60)
                {
                    if (StyxWoW.Me.Class == WoWClass.Shaman && SpellManager.HasSpell("Ghost Wolf") && SingularSettings.Instance.Shaman().UseGhostWolf)
                    {
                        e.Cancel = true;

                        if (!StyxWoW.Me.HasAura("Ghost Wolf"))
                        {
                            Logger.Write( LogColor.Hilite, "^Ghost Wolf instead of mounting");
                            Spell.LogCast("Ghost Wolf", StyxWoW.Me);
                            Spell.CastPrimative("Ghost Wolf");
                        }
                    }
                    else if (StyxWoW.Me.Class == WoWClass.Druid && SingularSettings.Instance.Druid().UseTravelForm && SpellManager.HasSpell("Travel Form") && StyxWoW.Me.IsOutdoors)
                    {
                        e.Cancel = true;

                        if (!StyxWoW.Me.HasAura("Travel Form"))
                        {
                            WoWAura aura = StyxWoW.Me.GetAllAuras().FirstOrDefault(a => a.Spell.Name.Substring(a.Name.Length - 5).Equals(" Form"));
                            Logger.Write(LogColor.Hilite, "^Travel Form instead of mounting.");
                            Logger.WriteDiagnostic("MountManager: changing to form='{0}',  current='{1}',  hb-says='{2}'",
                                "Travel Form", aura == null ? "-none-" : aura.Name, StyxWoW.Me.Shapeshift.ToString()
                                ); Spell.LogCast("Travel Form", StyxWoW.Me);
                            Spell.CastPrimative("Travel Form");
                        }
                    }
                }
                else if (StyxWoW.Me.Class == WoWClass.Druid && ClassSpecific.Druid.Common.AllowAquaticForm)
                {
                    e.Cancel = true;

                    if (!StyxWoW.Me.HasAnyShapeshift(ShapeshiftForm.Aqua, ShapeshiftForm.Travel, ShapeshiftForm.FlightForm, ShapeshiftForm.EpicFlightForm))  // check flightform in case we jump cast it at water surface
                    {
                        WoWAura aura = StyxWoW.Me.GetAllAuras().FirstOrDefault(a => a.Spell.Name.Substring(a.Name.Length - 5).Equals(" Form"));
                        Logger.WriteDiagnostic("MountManager: changing to form='{0}',  current='{1}',  hb-says='{2}'",
                            "Travel Form", aura == null ? "-none-" : aura.Name, StyxWoW.Me.Shapeshift.ToString()
                            );
                        Logger.Write(LogColor.Hilite, "^Aquatic Form instead of mounting.");
                        Spell.LogCast("Travel Form", StyxWoW.Me);
                        Spell.CastPrimative("Travel Form");
                    }
                }
            }

            if (StyxWoW.Me.Class == WoWClass.Shaman && SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds && ClassSpecific.Shaman.Totems.NeedToRecallTotems )
            {
                Logger.WriteDiagnostic("OnMountUp: recalling totems since about to mount");
                ClassSpecific.Shaman.Totems.RecallTotems();
            }
        }
    }
}