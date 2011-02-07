using System.Drawing;
using System.Reflection;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

namespace Singular
{
    public partial class SingularRoutine : CombatRoutine
    {
        public override string Name { get { return "Singular"; } }

        public override WoWClass Class { get { return StyxWoW.Me.Class; } }

        public LocalPlayer Me { get { return StyxWoW.Me; } }

        public string LastSpellCast { get; set; }

        public void Log(string message)
        {
            Logging.Write(Color.Orange, message);
        }

        public override void Initialize()
        {
            Logger.Write("Starting Singular v" + Assembly.GetExecutingAssembly().GetName().Version);
            AttachEventHandlers();
        }
    }
}