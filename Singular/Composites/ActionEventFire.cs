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

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular.Composites
{
    internal class ActionEventFire<T> : Action where T : class
    {
        private readonly T _eventInvoker;

        public ActionEventFire(T eventInvoker)
        {
            if (!typeof(T).IsSubclassOf(typeof(EventHandler)))
            {
                throw new Exception("Type T must be a type of EventHandler");
            }

            _eventInvoker = eventInvoker;
        }

        protected override RunStatus Run(object context)
        {
            try
            {
                (_eventInvoker as EventHandler).Invoke(this, context as EventArgs);
                return RunStatus.Success;
            }
            catch
            {
                return RunStatus.Failure;
            }
        }
    }
}