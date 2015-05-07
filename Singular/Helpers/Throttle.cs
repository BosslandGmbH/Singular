using System;
using System.Collections.Generic;
using System.Linq;

using Singular.Managers;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals;
using CommonBehaviors.Actions;
using System.Diagnostics;

namespace Singular.Helpers
{
    /// <summary>
    ///   Implements a 'throttle' composite. This composite limits the number of times the child 
    ///   will be run within a given time span.  Returns cappedStatus if limit reached, otherwise
    ///   Returns result of child
    /// </summary>
    /// <remarks>
    ///   Created 10/28/2012.
    /// </remarks>
    public class ThrottlePasses : Decorator
    {
        private DateTime _end;
        private int _count;
        private RunStatus _limitStatus;

        private static Composite ChildComposite(params Composite[] children)
        {
            if (children.GetLength(0) == 1)
                return children[0];
            return new PrioritySelector(children);
        }

        /// <summary>
        /// time span that Limit child Successes can occur
        /// </summary>
        public TimeSpan TimeFrame { get; set; }
        /// <summary>
        /// maximum number of child Successes that can occur within TimeFrame
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        ///   Implements a 'throttle' composite. This composite limits the number of times the child 
        ///   will be run within a given time span.  Returns cappedStatus for attempts after limit reached, 
        ///   otherwise returns result of child
        /// </summary>
        /// <param name = "limit">max number of occurrences</param>
        /// <param name = "timeFrame">time span for occurrences</param>
        /// <param name="limitStatus">RunStatus to return when limit reached</param>
        /// <param name = "child">composite children to tick (run)</param>
        public ThrottlePasses(int limit, TimeSpan timeFrame, RunStatus limitStatus, params Composite[] children)
            : base(ChildComposite(children))
        {
            TimeFrame = timeFrame;
            Limit = limit;

            _end = DateTime.MinValue;
            _count = 0;
            _limitStatus = limitStatus;
        }

        /// <summary>
        ///   Implements a 'throttle' composite. This composite limits the number of times the child 
        ///   to running once within a given time span.  Returns Failure if attempted to run after
        ///   limit reached in timeframe, otherwise returns result of child
        /// </summary>
        /// <param name = "timeFrame">wait TimeSpan after child success before another attempt</param>
        /// <param name = "child">composite children to tick (run)</param>
        public ThrottlePasses(TimeSpan timeFrame, params Composite[] children)
            : this(1, timeFrame, RunStatus.Failure, ChildComposite(children))
        {
        }

        /// <summary>
        ///   Implements a 'throttle' composite. This composite limits the number of times the child 
        ///   will be run within a given time span.  Returns Failure for attempts after limit reached, 
        ///   otherwise returns result of child
        /// </summary>
        /// <param name = "Limit">max number of occurrences</param>
        /// <param name = "timeFrame">time span for occurrences in seconds</param>
        /// <param name = "child">composite children to tick (run)</param>
        public ThrottlePasses(int Limit, int timeSeconds, params Composite[] children)
            : this(Limit, TimeSpan.FromSeconds(timeSeconds), RunStatus.Failure, ChildComposite(children))
        {

        }

        public ThrottlePasses(int Limit, TimeSpan ts, params Composite[] children)
            : this(Limit, ts, RunStatus.Failure, ChildComposite(children))
        {

        }

        /// <summary>
        ///   Implements a 'throttle' composite. This composite limits the number of times the child 
        ///   will be run within a given time span.  Returns Failure if limit reached, otherwise
        ///   Returns result of child
        /// </summary>
        /// <param name = "timeFrame">time span for occurrences in seconds</param>
        /// <param name = "child">composite children to tick (run)</param>
        public ThrottlePasses(int timeSeconds, params Composite[] children)
            : this(1, TimeSpan.FromSeconds(timeSeconds), RunStatus.Failure, ChildComposite(children))
        {

        }

        public override void Start(object context)
        {
            base.Start(context);
        }

        public override void Stop(object context)
        {
            base.Stop(context);
        }

        protected override IEnumerable<RunStatus> Execute(object context)
        {
            if (DateTime.UtcNow < _end && _count >= Limit)
            {
                yield return _limitStatus;
                yield break;
            }

            DecoratedChild.Start(context);

            RunStatus childStatus;
            while ((childStatus = DecoratedChild.Tick(context)) == RunStatus.Running)
            {
                yield return RunStatus.Running;
            }

            DecoratedChild.Stop(context);

            if (DateTime.UtcNow > _end)
            {
                _count = 0;
                _end = DateTime.UtcNow + TimeFrame;
            }

            _count++;

            if (DecoratedChild.LastStatus == RunStatus.Failure)
            {
                yield return RunStatus.Failure;
                yield break;
            }

            yield return RunStatus.Success;
            yield break;
        }
    }



    /// <summary>
    ///   Implements a 'throttle' composite. This composite limits the number of times the child 
    ///   returns RunStatus.Success within a given time span.  Returns cappedStatus if limit reached, 
    ///   otherwise returns result of child
    /// </summary>
    /// <remarks>
    ///   Created 10/28/2012.
    /// </remarks>
    public class Throttle : Decorator
    {
        private DateTime _end;
        private int _count;
        private RunStatus _limitStatus;

        private static Composite ChildComposite(params Composite[] children)
        {
            if (children.GetLength(0) == 1)
                return children[0];
            return new PrioritySelector(children);
        }

        /// <summary>
        /// time span that Limit child Successes can occur
        /// </summary>
        public TimeSpan TimeFrame { get; set; }
        /// <summary>
        /// maximum number of child Successes that can occur within TimeFrame
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        ///   Creates a 'throttle' composite. This composite limits the number of times the child 
        ///   returns RunStatus.Success within a given time span.  Returns cappedStatus if limit reached, 
        ///   otherwise returns result of child
        /// </summary>
        /// <param name = "limit">max number of occurrences</param>
        /// <param name = "timeFrame">time span for occurrences</param>
        /// <param name="limitStatus">RunStatus to return when limit reached</param>
        /// <param name = "child">composite children to tick (run)</param>
        public Throttle(int limit, TimeSpan timeFrame, RunStatus limitStatus, params Composite[] children)
            : base(ChildComposite(children))
        {
            TimeFrame = timeFrame;
            Limit = limit;

            _end = DateTime.MinValue;
            _count = 0;
            _limitStatus = limitStatus;
        }

        /// <summary>
        ///   Creates a 'throttle' composite. This composite limits the number of times the child 
        ///   returns RunStatus.Success within a given time span.  Returns Failure if limit reached, 
        ///   otherwise returns result of child
        /// </summary>
        /// <param name = "timeFrame">time span for occurrences</param>
        /// <param name = "child">composite children to tick (run)</param>
        public Throttle(TimeSpan timeFrame, params Composite[] children)
            : this(1, timeFrame, RunStatus.Failure, ChildComposite(children))
        {
        }

        /// <summary>
        ///   Creates a 'throttle' composite. This composite limits the number of times the child 
        ///   returns RunStatus.Success within a given time span.  Returns Failure if limit reached, 
        ///   otherwise returns result of child
        /// </summary>
        /// <param name = "timeFrame">time span for occurrences</param>
        /// <param name = "child">composite children to tick (run)</param>
        public Throttle(int Limit, TimeSpan timeFrame, params Composite[] children )
            : this(Limit, timeFrame, RunStatus.Failure, ChildComposite(children))
        {
        }

        /// <summary>
        ///   Creates a 'throttle' composite. This composite limits the number of times the child 
        ///   returns RunStatus.Success within a given time span.  Returns Failure if limit reached, 
        ///   otherwise returns result of child
        /// </summary>
        /// <param name = "Limit">max number of occurrences</param>
        /// <param name = "timeFrame">time span for occurrences in seconds</param>
        /// <param name = "child">composite children to tick (run)</param>
        public Throttle(int Limit, int timeSeconds, params Composite[] children)
            : this(Limit, TimeSpan.FromSeconds(timeSeconds), RunStatus.Failure, ChildComposite(children))
        {
            
        }

        /// <summary>
        ///   Creates a 'throttle' composite. This composite limits the number of times the child 
        ///   returns RunStatus.Success within a given time span.  Returns Failure if limit reached, 
        ///   otherwise returns result of child
        /// </summary>
        /// <param name = "timeFrame">wait in seconds after child success before another attempt</param>
        /// <param name = "child">composite children to tick (run)</param>
        public Throttle(int timeSeconds, params Composite[] children)
            : this(1, TimeSpan.FromSeconds(timeSeconds), RunStatus.Failure, ChildComposite(children))
        {
            
        }

        /// <summary>
        ///   Creates a 'throttle' composite. This composite limits the number of times the child 
        ///   returns RunStatus.Success to once per 250ms.  Returns Failure if limit reached, 
        ///   otherwise returns result of child
        /// </summary>
        /// <param name = "timeFrame">time span for occurrences in seconds</param>
        /// <param name = "child">composite children to tick (run)</param>
        public Throttle(params Composite[] children)
            : this(1, TimeSpan.FromMilliseconds(250), RunStatus.Failure, ChildComposite(children))
        {

        }

        public override void Start(object context)
        {
            base.Start(context);
        }

        public override void Stop(object context)
        {
            base.Stop(context);
        }

        protected override IEnumerable<RunStatus> Execute(object context)
        {
            if (DateTime.UtcNow < _end && _count >= Limit)
            {
                yield return _limitStatus;
                yield break;
            }

            // check not present in Decorator, but adding here
            if (DecoratedChild == null)
            {
                yield return RunStatus.Failure;
                yield break;
            }

            DecoratedChild.Start(context);

            RunStatus childStatus;
            while ((childStatus = DecoratedChild.Tick(context)) == RunStatus.Running)
            {
                yield return RunStatus.Running;
            }

            DecoratedChild.Stop(context);

            if (DecoratedChild.LastStatus == RunStatus.Failure)
            {
                yield return RunStatus.Failure;
                yield break;
            }

            if (DateTime.UtcNow > _end)
            {
                _count = 0;
                _end = DateTime.UtcNow + TimeFrame;
            }

            _count++;

            yield return RunStatus.Success;
            yield break;
        }
    }


    public class DynaWait : Decorator
    {

        private bool _measure;
        private DateTime _begin;
        private DateTime _end;
        private SimpleTimeSpanDelegate _span;

        /// <summary>
        ///   Creates a new Wait decorator using the specified timeout, run delegate, and child composite.
        /// </summary>
        /// <param name = "timeoutSeconds"></param>
        /// <param name = "runFunc"></param>
        /// <param name = "child"></param>
        public DynaWait(SimpleTimeSpanDelegate span, CanRunDecoratorDelegate runFunc, Composite child, bool measure = false)
            : base(runFunc, child)
        {
            _span = span;
            _measure = measure;

        }

        public override void Start(object context)
        {
            _begin = DateTime.UtcNow;
            _end = DateTime.UtcNow + _span(context);
            base.Start(context);
        }

        public override void Stop(object context)
        {
            _end = DateTime.MinValue;
            base.Stop(context);

            if (_measure)
            {
                Logger.Write("Duration: {0:F0} ms", (DateTime.UtcNow - _begin).TotalMilliseconds);
            }
        }

        protected override IEnumerable<RunStatus> Execute(object context)
        {
            while (DateTime.UtcNow < _end)
            {
                if (Runner != null)
                {
                    if (Runner(context))
                    {
                        break;
                    }
                }
                else
                {
                    if (CanRun(context))
                    {
                        break;
                    }
                }

                yield return RunStatus.Running;
            }

            if (DateTime.UtcNow > _end)
            {
                yield return RunStatus.Failure;
                yield break;
            }

            DecoratedChild.Start(context);
            while (DecoratedChild.Tick(context) == RunStatus.Running)
            {
                yield return RunStatus.Running;
            }

            DecoratedChild.Stop(context);
            if (DecoratedChild.LastStatus == RunStatus.Failure)
            {
                yield return RunStatus.Failure;
                yield break;
            }

            yield return RunStatus.Success;
            yield break;
        }
    }


    public class DynaWaitContinue : Decorator
    {
        private bool _measure;
        private DateTime _begin;
        private DateTime _end;
        private SimpleTimeSpanDelegate _span;

        /// <summary>
        ///   Creates a new Wait decorator using the specified timeout, run delegate, and child composite.
        /// </summary>
        /// <param name = "timeoutSeconds"></param>
        /// <param name = "runFunc"></param>
        /// <param name = "child"></param>
        public DynaWaitContinue(SimpleTimeSpanDelegate span, CanRunDecoratorDelegate runFunc, Composite child, bool measure = false)
            : base(runFunc, child)
        {
            _span = span;
            _measure = measure;
        }

        public override void Start(object context)
        {
            _begin = DateTime.UtcNow;
            _end = DateTime.UtcNow + _span(context);
            base.Start(context);
        }

        public override void Stop(object context)
        {
            _end = DateTime.MinValue;
            base.Stop(context);
            if (_measure)
            {
                Logger.Write("Duration: {0:F0} ms", (DateTime.UtcNow - _begin).TotalMilliseconds);
            }
        }

        protected override IEnumerable<RunStatus> Execute(object context)
        {
            while (DateTime.UtcNow < _end)
            {
                if (Runner != null)
                {
                    if (Runner(context))
                    {
                        break;
                    }
                }
                else
                {
                    if (CanRun(context))
                    {
                        break;
                    }
                }

                yield return RunStatus.Running;
            }

            if (DateTime.UtcNow > _end)
            {
                yield return RunStatus.Success;
                yield break;
            }

            DecoratedChild.Start(context);
            while (DecoratedChild.Tick(context) == RunStatus.Running)
            {
                yield return RunStatus.Running;
            }

            DecoratedChild.Stop(context);
            if (DecoratedChild.LastStatus == RunStatus.Failure)
            {
                yield return RunStatus.Failure;
                yield break;
            }

            yield return RunStatus.Success;
            yield break;
        }
    }

    public class DecoratorIfElse : GroupComposite        
    {
        protected CanRunDecoratorDelegate Runner { get; private set; }

        public Composite ChildIf { get { return Children[0]; } }
        public Composite ChildElse { get { return Children[1]; } }


        public DecoratorIfElse(CanRunDecoratorDelegate runFunc, params Composite []children)  
            : base(children)
        {
            Debug.Assert(runFunc != null);
            Debug.Assert(children.Count() == 2);
            Runner = runFunc;
        }

        protected virtual bool CanRun(object context)
        {
            return true;
        }

        public override void Start(object context)
        {
            if (Children.Count != 2)
            {
                throw new ApplicationException("DecoratorIfElse must have exactly two children.");
            }
            base.Start(context);
        }

        protected override IEnumerable<RunStatus> Execute(object context)
        {
            bool runIf = Runner(context);
            Composite compBranch = runIf 
                ? ChildIf
                : ChildElse;

            compBranch.Start(context);
            while (compBranch.Tick(context) == RunStatus.Running)
            {
                yield return RunStatus.Running;
            }

            compBranch.Stop(context);
            if (compBranch.LastStatus == RunStatus.Failure)
            {
                yield return RunStatus.Failure;
                yield break;
            }

            yield return RunStatus.Success;
            yield break;
        }
    }
}
