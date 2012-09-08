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
using System.Diagnostics;
using System.IO;
using Singular.Settings;
using Styx.Common;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Color = System.Drawing.Color;

namespace Singular
{
    public static class Logger
    {
        public static void Write(string message)
        {
            Write(Color.Green, message);
        }

        public static void Write(string message, params object[] args)
        {
            Write(Color.Green, message, args);
        }

        public static void Write(Color clr, string message, params object[] args)
        {
            System.Windows.Media.Color newColor = System.Windows.Media.Color.FromArgb(clr.A, clr.R, clr.G, clr.B);

            Logging.Write(newColor, "[Singular] " + message, args);
        }

        public static void WriteDebug(string message)
        {
            WriteDebug(Color.Orange, message);
        }

        public static void WriteDebug(string message, params object[] args)
        {
            WriteDebug(Color.Orange, message, args);
        }

        public static void WriteDebug(Color clr, string message, params object[] args)
        {
            System.Windows.Media.Color newColor = System.Windows.Media.Color.FromArgb(clr.A, clr.R, clr.G, clr.B);

            if (SingularSettings.Instance.EnableDebugLogging)
            {
                Logging.Write(newColor, "[Singular-DEBUG] " + message, args);
            }
            else
            {
                Logging.Write(LogLevel.Diagnostic, newColor, "[Singular-DEBUG] " + message, args);
            }
        }

        public static void PrintStackTrace(string reason = "Debug")
        {
            WriteDebug("Stack trace for " + reason);
            var stackTrace = new StackTrace(true);
            StackFrame[] stackFrames = stackTrace.GetFrames();
            // Start at frame 1 (just before this method entrance)
            for (int i = 1; i < Math.Min(stackFrames.Length, 10); i++)
            {
                StackFrame frame = stackFrames[i];
                WriteDebug(string.Format("\tCaller {0}: {1} in {2} line {3}", i, frame.GetMethod().Name, Path.GetFileName(frame.GetFileName()), frame.GetFileLineNumber()));
            }
        }
    }

    public class LogMessage : Action
    {
        private readonly string message;

        public LogMessage(string message)
        {
            this.message = message;
        }

        protected override RunStatus Run(object context)
        {
            Logger.Write(message);

            if (Parent is Selector)
                return RunStatus.Failure;
            return RunStatus.Success;
        }
    }
}