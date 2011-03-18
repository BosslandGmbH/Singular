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

using TreeSharp;

namespace Singular.Composites
{
    public delegate string LogMessageRetriever();

    internal class ActionLogMessage : Action
    {
        private readonly bool _debug;

        private readonly string _message;
        private readonly LogMessageRetriever _messageGrabber;

        public ActionLogMessage(bool debug, string message)
        {
            _message = message;
            _debug = debug;
        }

        public ActionLogMessage(bool debug, LogMessageRetriever message)
        {
            _messageGrabber = message;
            _debug = debug;
        }

        protected override RunStatus Run(object context)
        {
            if (_debug)
            {
                Logger.WriteDebug(_messageGrabber != null ? _messageGrabber() : _message);
            }
            else
            {
                Logger.Write(_messageGrabber != null ? _messageGrabber() : _message);
            }

            if (Parent is Selector)
            {
                return RunStatus.Failure;
            }
            return RunStatus.Success;
        }
    }
}