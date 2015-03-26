using System;

using Singular.Helpers;
using Styx.TreeSharp;
using CommonBehaviors.Actions;

using Action = Styx.TreeSharp.Action;


namespace Singular
{
    partial class SingularRoutine
    {
        public static Composite TestDynaWait()
        {
            return new PrioritySelector(
                    new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWait(ts => TimeSpan.FromSeconds(2), until => false, new ActionAlwaysSucceed(), true),
                            new Action(r => { Logger.Write("1. RunStatus.Success - TEST FAILED"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("1. RunStatus.Failure - Test Succeeded!"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    ),
                new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWait(ts => TimeSpan.FromSeconds(2), until => true, new ActionAlwaysSucceed(), true),
                            new Action(r => { Logger.Write("2. RunStatus.Success - Test Succeeded!"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("2. RunStatus.Failure - TEST FAILED"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    ),

                new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWait(ts => TimeSpan.FromSeconds(2), until => true, new ActionAlwaysFail(), true),
                            new Action(r => { Logger.Write("3. RunStatus.Success - TEST FAILED"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("3. RunStatus.Failure - Test Succeeded!"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    ),
                new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWaitContinue(ts => TimeSpan.FromSeconds(2), until => false, new ActionAlwaysSucceed(), true),
                            new Action(r => { Logger.Write("4. RunStatus.Success - Test Succeeded!"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("4. RunStatus.Failure - TEST FAILED"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    ),
                new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWaitContinue(ts => TimeSpan.FromSeconds(2), until => true, new ActionAlwaysSucceed(), true),
                            new Action(r => { Logger.Write("5. RunStatus.Success - Test Succeeded!"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("5. RunStatus.Failure - TEST FAILED"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    ),

                new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWaitContinue(ts => TimeSpan.FromSeconds(2), until => true, new ActionAlwaysFail(), true),
                            new Action(r => { Logger.Write("6. RunStatus.Success - TEST FAILED"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("6. RunStatus.Failure - Test Succeeded!"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    )
                );
        }

        public static Composite TestThrottle()
        {
            return new PrioritySelector(
/*
                new TestTrace( "TEST 1 - THROTTLE 1 SUCCESS PER SECOND",
                    new PrioritySelector( 
                        new Throttle( 1, new Action( r => Logger.Write( "1. SUCCESS - 1 PER SECOND")),
                        new Action( r => { Logger.Write( "1. FAILURE"); return RunStatus.Failure; } )
                        )
                    ),

                new TestTrace( "TEST 2 - THROTTLE 2 SUCCESS PER SECOND",
                    new PrioritySelector( 
                        new Throttle( 2, 1, new Action( r => Logger.Write( "2. SUCCESS - 2 PER SECOND")),
                        new Action( r => { Logger.Write( "1. FAILURE"); return RunStatus.Failure; } )
                        )
                    ),
*/
                new PrioritySelector( 
                    new ThrottlePasses( 
                        2, TimeSpan.FromSeconds(1), RunStatus.Failure,
                        new Action( r => { Logger.Write( "3. PASS - 1 PASS PER SECOND"); return RunStatus.Failure; } )
                        ),
                    new Action( r => { Logger.Write( "3. FAILURE"); return RunStatus.Success; } )
                    )

                );
        }

        public class TestTrace : CallTrace
        {

            public TestTrace(string name, params Composite[] children)
                : base(name, children)
            {
                TraceActive = true;
                TraceEnter = true;
                TraceExit = true;
            }


        }
    }
}
