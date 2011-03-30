using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx;
using Styx.Helpers;
using Styx.Plugins.PluginClass;

namespace Singular
{
    public class Logout : HBPlugin
    {
        private DateTime _end = DateTime.Now.AddHours(5);
        public override void Pulse()
        {
            if (StyxWoW.Me.Level == 68)
                InactivityDetector.ForceLogout(true);
            if (DateTime.Now > _end)
                InactivityDetector.ForceLogout(true);
        }

        public override string Name { get { return "Logout"; } }

        public override string Author { get { return "Apoc"; } }

        public override Version Version { get { return new Version(0, 0, 0, 1); } }
    }
}
