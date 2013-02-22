
using System;
using System.Reflection;
using System.Linq;
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
using System.IO;
using System.Collections.Generic;
using Styx.Common;
using Singular.Settings;

using Styx.Common.Helpers;

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

            // install botevent handler so we can consolidate validation on whether 
            // .. local botevent handlers should be called or not
            SingularBotEventInitialize();
        }

        public static SingularRoutine Instance { get; private set; }

        public override string Name { get { return "Singular"; } }

        public override WoWClass Class { get { return StyxWoW.Me.Class; } }

        public override bool WantButton { get { return true; } }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        private static bool IsMounted
        {
            get
            {
                if (StyxWoW.Me.Class == WoWClass.Druid)
                {
                    switch (StyxWoW.Me.Shapeshift)
                    {
                        case ShapeshiftForm.FlightForm:
                        case ShapeshiftForm.EpicFlightForm:
                            return true;
                    }
                }

                return StyxWoW.Me.Mounted;
            }
        }

        private ConfigurationForm _configForm;
        public override void OnButtonPress()
        {
            if(_configForm == null || _configForm.IsDisposed || _configForm.Disposing)
                _configForm = new ConfigurationForm();

            _configForm.Show();
        }

        private static ulong _guidLastTarget = 0;
        private static WaitTimer _timerLastTarget = new WaitTimer(TimeSpan.FromSeconds(5));

        public override void Pulse()
        {
            // No pulsing if we're loading or out of the game.
            if (!StyxWoW.IsInGame || !StyxWoW.IsInWorld)
                return;

            // check and output casting state information
            UpdateDiagnosticCastingState();

            // Update the current context, check if we need to rebuild any behaviors.
            UpdateContext();

            // Double cast shit
            Spell.DoubleCastPreventionDict.RemoveAll(t => DateTime.UtcNow > t);

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

            HotkeyDirector.Pulse();
        }

        public override void Initialize()
        {
            WriteSupportInfo();

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

            // write current settings to log file... only written at startup and when Save press in Settings UI
            SingularSettings.Instance.LogSettings();

            // Update the current WoWContext, and fire an event for the change.
            UpdateContext();

            // NOTE: Hook these events AFTER the context update.
            OnWoWContextChanged += (orig, ne) =>
                {
                    Logger.Write(Color.White, "Context changed, re-creating behaviors");
                    RebuildBehaviors();
                    Spell.GcdInitialize();
                };
            RoutineManager.Reloaded += (s, e) =>
                {
                    Logger.Write(Color.White, "Routines were reloaded, re-creating behaviors");
                    RebuildBehaviors();
                    Spell.GcdInitialize();
                };

            // create silently since will creating again right after this
            if (!RebuildBehaviors(true))
            {
                return;
            }
            Logger.WriteDebug(Color.White, "Verified behaviors can be created!");

            // When we actually need to use it, we will.
            Spell.GcdInitialize();

            EventHandlers.Init();
            MountManager.Init();
            HotkeyDirector.Init();
            MovementManager.Init();
            SoulstoneManager.Init();
            Dispelling.Init();

            //Logger.Write("Combat log event handler started.");

            // create silently since Start button will create a context change (at least first Start)
            // .. which will build behaviors again
            Instance.RebuildBehaviors(true);

            Logger.Write("Initialization complete!");
        }

        private static void WriteSupportInfo()
        {
            string singularName = "Singular v" + GetSingularVersion();
            Logger.Write("Starting " + singularName);

            // save some support info in case we need
            Logger.WriteFile("{0:F1} days since Windows was restarted", TimeSpan.FromMilliseconds(Environment.TickCount).TotalHours / 24.0);
            Logger.WriteFile("{0} FPS currently in WOW", GetFPS());
            Logger.WriteFile("{0} ms of Latency in WOW", StyxWoW.WoWClient.Latency);
            Logger.WriteFile("{0} local system time", DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss"));

            // verify installed source integrity 
            try
            {
                string singularFolder = GetSingularSourcePath();

                FileCheckList actual = new FileCheckList();
                actual.Generate(singularFolder);

                FileCheckList certified = new FileCheckList();
                certified.Load(Path.Combine(singularFolder, "singular.xml"));

                List<FileCheck> err = certified.Compare(actual);

                List<FileCheck> fcerrors = FileCheckList.Test(GetSingularSourcePath());
                if (!fcerrors.Any())
                    Logger.Write("Installation: integrity verified for {0}", GetSingularVersion());
                else
                {
                    Logger.Write(Color.HotPink, "Installation: modified by user - forum support may not available", singularName);
                    Logger.WriteFile("=== following {0} files with issues ===", fcerrors.Count);
                    foreach (var fc in fcerrors)
                    {
                        if ( !File.Exists( fc.Name ))
                            Logger.WriteFile(Styx.Common.LogLevel.Diagnostic, "   deleted: {0} {1}", fc.Size, fc.Name);
                        else if ( certified.Filelist.Any( f => 0 == String.Compare( f.Name, fc.Name, true)))
                            Logger.WriteFile(Styx.Common.LogLevel.Diagnostic, "   changed: {0} {1}", fc.Size, fc.Name);
                        else
                            Logger.WriteFile(Styx.Common.LogLevel.Diagnostic, "   inserted {0} {1}", fc.Size, fc.Name);
                    }
                    Logger.WriteFile(Styx.Common.LogLevel.Diagnostic, "");
                }
            }
            catch (FileNotFoundException e)
            {
                Logger.Write(Color.HotPink, "Installation: file missing - forum support not available");
                Logger.Write(Color.HotPink, "missing file: {0}", e.FileName );
            }
            catch (Exception e)
            {
                Logger.Write(Color.HotPink, "Installation: verification error - forum support not available");
                Logger.WriteFile(e.ToString());
            }
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

        public static string GetSingularSourcePath()
        {
            // bit of a hack, but source code folder for the assembly is only 
            // .. available from the filename of the .dll
            FileCheck fc = new FileCheck();
            Assembly singularDll = Assembly.GetExecutingAssembly();
            FileInfo fi = new FileInfo(singularDll.Location);
            int len = fi.Name.LastIndexOf("_");
            string folderName = fi.Name.Substring(0, len);

            folderName = Path.Combine(Styx.Helpers.GlobalSettings.Instance.CombatRoutinesPath, folderName);

            // now check if relative path and if so, append to honorbuddy folder
            if (!Path.IsPathRooted(folderName))
                folderName = Path.Combine(GetHonorBuddyFolder(), folderName);

            return folderName;
        }

        public static string GetHonorBuddyFolder()
        {
            string hbpath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            return hbpath;
        }

        private static bool _lastIsGCD = false;
        private static bool _lastIsCasting = false;
        private static bool _lastIsChanneling = false;

        public static bool UpdateDiagnosticCastingState( bool retVal = false)
        {
            if (SingularSettings.Debug && SingularSettings.Instance.EnableDebugLoggingGCD)
            {
                if (_lastIsGCD != Spell.IsGlobalCooldown())
                {
                    _lastIsGCD = Spell.IsGlobalCooldown();
                    Logger.WriteDebug("CastingState:  GCD={0} GCDTimeLeft={1}", _lastIsGCD, (int)Spell.GcdTimeLeft.TotalMilliseconds);
                }
                if (_lastIsCasting != Spell.IsCasting())
                {
                    _lastIsCasting = Spell.IsCasting();
                    Logger.WriteDebug("CastingState:  Casting={0} CastTimeLeft={1}", _lastIsCasting, (int)Me.CurrentCastTimeLeft.TotalMilliseconds);
                }
                if (_lastIsChanneling != Spell.IsChannelling())
                {
                    _lastIsChanneling = Spell.IsChannelling();
                    Logger.WriteDebug("ChannelingState:  Channeling={0} ChannelTimeLeft={1}", _lastIsChanneling, (int)Me.CurrentChannelTimeLeft.TotalMilliseconds);
                }
            }
            return retVal;
        }
    }
}