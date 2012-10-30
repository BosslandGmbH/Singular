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
using Styx.Helpers;

namespace Singular
{
    public static class Logger
    {
        /// <summary>
        /// write message to log window and file
        /// </summary>
        /// <param name="message">message text</param>
        public static void Write(string message)
        {
            Write(Color.Green, message);
        }

        /// <summary>
        /// write message to log window and file
        /// </summary>
        /// <param name="message">message text with embedded parameters</param>
        /// <param name="args">replacement parameter values</param>
        public static void Write(string message, params object[] args)
        {
            Write(Color.Green, message, args);
        }

        /// <summary>
        /// write message to log window and file
        /// </summary>
        /// <param name="clr">color of message in window</param>
        /// <param name="message">message text with embedded parameters</param>
        /// <param name="args">replacement parameter values</param>
        public static void Write(Color clr, string message, params object[] args)
        {
            System.Windows.Media.Color newColor = System.Windows.Media.Color.FromArgb(clr.A, clr.R, clr.G, clr.B);
            if (GlobalSettings.Instance.LogLevel >= LogLevel.Normal)
                Logging.Write(newColor, "[Singular] " + message, args);
            else if (GlobalSettings.Instance.LogLevel == LogLevel.Quiet)
                Logging.WriteToFileSync( LogLevel.Normal, "[Singular] " + message, args);
        }

        /// <summary>
        /// write message to log window if Singular Debug Enabled setting true
        /// </summary>
        /// <param name="message">message text</param>
        public static void WriteDebug(string message)
        {
            WriteDebug(Color.Orange, message);
        }

        /// <summary>
        /// write message to log window if Singular Debug Enabled setting true
        /// </summary>
        /// <param name="message">message text with embedded parameters</param>
        /// <param name="args">replacement parameter values</param>
        public static void WriteDebug(string message, params object[] args)
        {
            WriteDebug(Color.Orange, message, args);
        }

        /// <summary>
        /// write message to log window if Singular Debug Enabled setting true
        /// </summary>
        /// <param name="clr">color of message in window</param>
        /// <param name="message">message text with embedded parameters</param>
        /// <param name="args">replacement parameter values</param>
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

        /// <summary>
        /// write message to log file
        /// </summary>
        /// <param name="message">message text</param>
        public static void WriteFile(string message)
        {
            WriteFile(LogLevel.Verbose, message);
        }

        /// <summary>
        /// write message to log file
        /// </summary>
        /// <param name="message">message text with replaceable parameters</param>
        /// <param name="args">replacement parameter values</param>
        public static void WriteFile(string message, params object[] args)
        {
            WriteFile(LogLevel.Verbose, message, args);
        }

        /// <summary>
        /// write message to log file
        /// </summary>
        /// <param name="ll">level to code entry with (doesn't control if written)</param>
        /// <param name="message">message text with replaceable parameters</param>
        /// <param name="args">replacement parameter values</param>
        public static void WriteFile( LogLevel ll, string message, params object[] args)
        {
            if ( GlobalSettings.Instance.LogLevel >= LogLevel.Quiet)
                Logging.WriteToFileSync( ll, "[Singular] " + message, args);
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