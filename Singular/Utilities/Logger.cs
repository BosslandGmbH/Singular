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

using Singular.Settings;

using Styx.Helpers;

namespace Singular
{
    internal class Logger
    {
        public static void Write(string message)
        {
            Write( Color.Green, message);
        }

        public static void Write( Color clr, string message)
        {
            Logging.Write( clr, "[Singular] " + message);
        }

        public static void WriteDebug(string message)
        {
            if (!SingularSettings.Instance.EnableDebugLogging)
            {
                return;
            }

            Logging.WriteDebug(Color.Green, "[Singular-DEBUG] " + message);
        }
    }
}