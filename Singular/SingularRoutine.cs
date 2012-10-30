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
using System.Reflection;
using System.Windows.Forms;
using Singular.GUI;
using Singular.Helpers;
using Singular.Managers;
using Singular.Utilities;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;
using System.Drawing;
using Styx.WoWInternals;

namespace Singular
{
    public partial class SingularRoutine : CombatRoutine
    {
        public SingularRoutine()
        {
            Instance = this;

            // Do this now, so we ensure we update our context when needed.
            BotEvents.Player.OnMapChanged += e =>
                {
                    // Don't run this handler if we're not the current routine!
                    if (RoutineManager.Current.Name != Name)
                        return;

                    // Only ever update the context. All our internal handlers will use the context changed event
                    // so we're not reliant on anything outside of ourselves for updates.
                    UpdateContext();
                };
        }

        public static SingularRoutine Instance { get; private set; }

        public override string Name { get { return "Singular v3"; } }

        public override WoWClass Class { get { return StyxWoW.Me.Class; } }

        public override bool WantButton { get { return true; } }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        private static bool IsMounted
        {
            get
            {
                switch (StyxWoW.Me.Shapeshift)
                {
                    case ShapeshiftForm.FlightForm:
                    case ShapeshiftForm.EpicFlightForm:
                        return true;
                }
                return StyxWoW.Me.Mounted;
            }
        }

        public override void OnButtonPress()
        {
            DialogResult dr = new ConfigurationForm().ShowDialog();
            if (dr == DialogResult.OK || dr == DialogResult.Yes)
            {
                Logger.WriteDebug(Color.LightGreen, "Settings saved, rebuilding behaviors...");
                RebuildBehaviors();
            }
        }

        public override void Pulse()
        {
            // No pulsing if we're loading or out of the game.
            if (!StyxWoW.IsInGame || !StyxWoW.IsInWorld)
                return;

            // Update the current context, check if we need to rebuild any behaviors.
            UpdateContext();

            // Double cast shit
            Spell.DoubleCastPreventionDict.RemoveAll(t => DateTime.UtcNow.Subtract(t).TotalMilliseconds >= 2500);

            //Only pulse for classes with pets
            switch (StyxWoW.Me.Class)
            {
                case WoWClass.Hunter:
                case WoWClass.DeathKnight:
                case WoWClass.Warlock:
                case WoWClass.Mage:
                    PetManager.Pulse();
                    break;
            }

            if (HealerManager.NeedHealTargeting)
                HealerManager.Instance.Pulse();

            if (Group.MeIsTank && CurrentWoWContext != WoWContext.Battlegrounds &&
                (Me.GroupInfo.IsInParty || Me.GroupInfo.IsInRaid))
                TankManager.Instance.Pulse();
        }

        public override void Initialize()
        {
            Logger.Write("Starting Singular v" + Assembly.GetExecutingAssembly().GetName().Version);

            // save some support info in case we need
            Logger.WriteFile("{0:F1} days since Windows was restarted", TimeSpan.FromMilliseconds(Environment.TickCount).TotalHours / 24.0);
            Logger.WriteFile("{0} FPS currently in WOW", GetFPS());
            Logger.WriteFile("{0} ms of Latency in WOW", StyxWoW.WoWClient.Latency);

            Logger.Write("Determining talent spec.");
            try
            {
                TalentManager.Update();
            }
            catch (Exception e)
            {
                StopBot(e.ToString());
            }
            Logger.Write("Current spec is " + TalentManager.CurrentSpec.ToString().CamelToSpaced());

            // Update the current WoWContext, and fire an event for the change.
            UpdateContext();

            // NOTE: Hook these events AFTER the context update.
            OnWoWContextChanged += (orig, ne) =>
                {
                    Logger.Write(Color.LightGreen, "Context changed, re-creating behaviors");
                    RebuildBehaviors();
                };
            RoutineManager.Reloaded += (s, e) =>
                {
                    Logger.Write("Routines were reloaded, re-creating behaviors");
                    RebuildBehaviors();
                };

            // create silently since will creating again right after this
            if (!RebuildBehaviors(true))
            {
                return;
            }
            Logger.WriteDebug("Verified behaviors can be created!");

            // When we actually need to use it, we will.
            EventHandlers.Init();
            MountManager.Init();
            //Logger.Write("Combat log event handler started.");

            // create silently since Start button will create a context change (at least first Start)
            // .. which will build behaviors again
            Instance.RebuildBehaviors(true);
            Logger.WriteDebug("Behaviors created!");

            Logger.Write("Initialization complete!");
        }

        private static void StopBot(string reason)
        {
            Logger.Write(reason);
            TreeRoot.Stop();
        }

        private static uint GetFPS()
        {
            try
            {
                return (uint)Lua.GetReturnVal<float>("return GetFramerate()", 0);
            }
            catch
            {

            }

            return 0;
        }

    }
}