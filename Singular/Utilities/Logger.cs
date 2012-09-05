﻿#region Revision Info

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
using Styx.TreeSharp;

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
            Logging.Write(clr, "[Singular] " + message, args);
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
            if (SingularSettings.Instance.EnableDebugLogging)
            {
                Logging.Write(clr, "[Singular-DEBUG] " + message, args);
            }
            else
            {
                Logging.WriteDebug(clr, "[Singular-DEBUG] " + message, args);
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