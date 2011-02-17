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

using System.Drawing;

using Styx.Helpers;

namespace Singular
{
    internal class Logger
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
            {
                return;
            }

            Logging.WriteDebug(Color.Green, "[Singular-DEBUG] " + message);
        }
    }
}