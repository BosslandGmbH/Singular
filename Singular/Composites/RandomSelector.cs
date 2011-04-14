using System;
using System.Collections.Generic;
using System.Linq;

namespace TreeSharp
{
    public class RandomSelector : Selector
    {
        public RandomSelector(params Composite[] children)
            : base(children)
        {
        }

        protected override IEnumerable<RunStatus> Execute(object context)
        {
            lock (Locker)
            {
                foreach (Composite node in Children.OrderBy(ret => new Random().Next()))
                {
                    node.Start(context);
                    while (node.Tick(context) == RunStatus.Running)
                    {
                        Selection = node;
                        yield return RunStatus.Running;
                    }
                    Selection = null;
                    node.Stop(context);
                    if (node.LastStatus == RunStatus.Success)
                    {
                        yield return RunStatus.Success;
                        yield break;
                    }
                }
                yield return RunStatus.Failure;
                yield break;
            }
        }
    }
}
