using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

using Styx.Helpers;

namespace Singular
{
    class Logger
    {
        static Logger()
        {
            WriteDebugMessages = true;
        }

        public static bool WriteDebugMessages { get; set; }

        public static void Write(string message)
        {
            Logging.Write(Color.Green, "[Singular] " + message);
        }

        public static void WriteDebug(string message)
        {
            if (!WriteDebugMessages)
                return;

            Logging.WriteDebug(Color.Green, "[Singular-DEBUG] " + message);
        }
    }
}
