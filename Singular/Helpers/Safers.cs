using System.Linq;
using CommonBehaviors.Actions;

using Singular.Settings;

using Styx;
using Styx.Logic;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Singular.Managers;
using Action = TreeSharp.Action;

namespace Singular.Helpers
{
    internal static class Safers
    {
        /// <summary>
        ///  This behavior SHOULD be called at top of the combat behavior. This behavior won't let the rest of the combat behavior to be called
        /// if you don't have a target. Also it will find a proper target, if the current target is dead or you don't have a target and still in combat.
        /// Tank targeting is also dealed in this behavior.
        /// </summary>
        /// <returns></returns>
        public static Composite EnsureTarget()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        // DisableTankTargeting is a user-setting. NeedTankTargeting is an internal one. Make sure both are turned on.
                        ret => !SingularSettings.Instance.DisableTankTargetSwitching && TankManager.NeedTankTargeting 
                            && TankManager.TargetingTimer.IsFinished && StyxWoW.Me.Combat &&
                               TankManager.Instance.FirstUnit != null && StyxWoW.Me.CurrentTarget != TankManager.Instance.FirstUnit,
                        new Action(
                            ret =>
                            {
                                Logger.WriteDebug("Targeting first unit of TankTargeting");
                                TankManager.Instance.FirstUnit.Target();
                                StyxWoW.SleepForLagDuration();
                                TankManager.TargetingTimer.Reset();
                            })),
                    new Decorator(
                        ret => StyxWoW.Me.CurrentTarget == null || StyxWoW.Me.CurrentTarget.Dead,
                        new PrioritySelector(
                            ctx =>
                            {
                                // If we have a RaF leader, then use its target.
                                if (RaFHelper.Leader != null && RaFHelper.Leader.Combat)
                                {
                                    return RaFHelper.Leader.CurrentTarget;
                                }

                                // Does the target list have anything in it? And is the unit in combat?
                                // Make sure we only check target combat, if we're NOT in a BG. (Inside BGs, all targets are valid!!)
                                if (Targeting.Instance.FirstUnit != null && StyxWoW.Me.Combat)
                                {
                                    return Targeting.Instance.FirstUnit;
                                }
                                // Cache this query, since we'll be using it for 2 checks. No need to re-query it.
                                var units =
                                    ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Where(
                                        p => p.IsHostile && !p.IsOnTransport && !p.Dead && p.DistanceSqr <= 70 * 70 && p.Combat);

                                if (StyxWoW.Me.Combat && units.Any())
                                {
                                    // Return the closest one to us
                                    return units.OrderBy(u => u.DistanceSqr).FirstOrDefault();
                                }

                                // And there's nothing left, so just return null, kthx.
                                return null;
                            },
                            // Make sure the target is VALID. If not, then ignore this next part. (Resolves some silly issues!)
                            new Decorator(
                                ret => ret != null,
                                new Sequence(
                                    new Action(ret => Logger.Write("Target is invalid. Switching to " + ((WoWUnit)ret).SafeName() + "!")),
                                    new Action(ret => ((WoWUnit)ret).Target()))),
                            // In order to resolve getting "stuck" on a target, we'll clear it if there's nothing viable.
                            new Action(
                                ret =>
                                {
                                    StyxWoW.Me.ClearTarget();
                                    // Force a failure, just so we can move down the branch. to the log message
                                    return RunStatus.Failure;
                                }),
                            new Action(ret => Logger.Write("No viable target! NOT GOOD!")),
                            new ActionAlwaysSucceed())));
        }
    }
}
