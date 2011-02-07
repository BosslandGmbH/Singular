using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using TreeSharp;

namespace Singular.Composites
{
    class ActionLogMessage : TreeSharp.Action
    {
        private string _message;

        private bool _debug;
        public ActionLogMessage(bool debug, string message)
        {
            _message = message;
            _debug = debug;
        }

        protected override TreeSharp.RunStatus Run(object context)
        {
            if (_debug)
                Logger.WriteDebug(_message);
            else
                Logger.Write(_message);

            if (Parent is Selector)
            {
                return RunStatus.Failure;
            }
            return RunStatus.Success;
        }
    }
}
