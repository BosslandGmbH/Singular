
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

namespace Singular
{
    public partial class SingularRoutine : CombatRoutine
    {
        public static Version GetSingularVersion()
        {
            // HB Build Process is overwriting AssemblyInfo.cs contents,
            // ... so manage version here instead of reading assembly
            // --------------------------------------------------------

            // return Assembly.GetExecutingAssembly().GetName().Version;
            return new Version("3.0.0.1271");
        }
    }
}