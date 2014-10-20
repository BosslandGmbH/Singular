using System;
using System.Linq;
using System.Threading;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;

using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using System.Drawing;

namespace Singular.ClassSpecific.Hunter
{
    public class Marksman
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WoWUnit Pet { get { return StyxWoW.Me.Pet; } }
        private static HunterSettings HunterSettings { get { return SingularSettings.Instance.Hunter(); } }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull|BehaviorType.Combat,WoWClass.Hunter,WoWSpec.HunterMarksmanship,WoWContext.Normal | WoWContext.Instances )]
        public static Composite CreateMarksmanHunterNormalPullAndCombat()
        {
            return new PrioritySelector(
                Common.CreateHunterEnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(

                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        CreateMarksmanDiagnosticOutputBehavior(),

                        Common.CreateMisdirectionBehavior(),

                        Common.CreateHunterAvoidanceBehavior(null, null),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateHunterNormalCrowdControl(),

                        Spell.Cast("Tranquilizing Shot", ctx => Me.CurrentTarget.HasAura("Enraged")),

                        Spell.Buff("Concussive Shot",
                            ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid
                                && Me.CurrentTarget.Distance > Spell.MeleeRange),

                        // Defensive Stuff
                        Spell.Cast("Intimidation",
                            ret => Me.GotTarget
                                && Me.CurrentTarget.IsAlive
                                && Me.GotAlivePet
                                && (!Me.CurrentTarget.GotTarget || Me.CurrentTarget.CurrentTarget == Me)),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && !(Me.CurrentTarget.IsBoss() || Me.CurrentTarget.IsPlayer) && Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3,
                            new PrioritySelector(
                                ctx => Unit.NearbyUnitsInCombatWithUsOrOurStuff.Where(u => u.InLineOfSpellSight).OrderByDescending(u => (uint) u.HealthPercent).FirstOrDefault(),
                                Spell.Cast("Kill Shot", onUnit => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u)), req => Me.HealthPercent < 85),
                                Spell.Cast("Multi-Shot", ctx => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u)), ClusterType.Radius, 8f)),
                                Common.CastSteadyShot(on => Me.CurrentTarget)
                                )
                            ),

                        // Single Target Rotation
                        Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent < 20),
                        Spell.Cast("Chimaera Shot"),
                        Spell.Cast("Aimed Shot", ret => Me.CurrentFocus > 60),
                        Common.CastSteadyShot(on => Me.CurrentTarget)
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 35f, 30f)
                );
        }

        #endregion

        #region Battleground Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterMarksmanship, WoWContext.Battlegrounds)]
        public static Composite CreateMarksmanHunterPvPPullAndCombat()
        {
            return new PrioritySelector(
                Common.CreateHunterEnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(

                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        CreateMarksmanDiagnosticOutputBehavior(),

                        Common.CreateHunterAvoidanceBehavior(null, null),

                        Helpers.Common.CreateInterruptBehavior(),
                // Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateHunterPvpCrowdControl(),

                        Spell.Cast("Tranquilizing Shot", ctx => Me.CurrentTarget.HasAura("Enraged")),

                        Spell.Buff("Concussive Shot",
                            ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid
                                && !Me.CurrentTarget.IsWithinMeleeRange),

                        // Defensive Stuff
                        Spell.Cast("Intimidation",
                            ret => Me.GotTarget
                                && Me.CurrentTarget.IsAlive
                                && Me.GotAlivePet
                                && (!Me.CurrentTarget.GotTarget || Me.CurrentTarget.CurrentTarget == Me)),

                        // Single Target Rotation
                        Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent < 20),
                        Spell.Cast("Chimaera Shot"),
                        Spell.Cast("Aimed Shot", ret => Me.HasAura("Fire!")),

                        // don't use long casts in PVP
                // Spell.Cast("Aimed Shot", ret => Me.CurrentFocus > 60),
                        Common.CastSteadyShot(on => Me.CurrentTarget)
                        )
                    ),

                Movement.CreateMoveToUnitBehavior(on => StyxWoW.Me.CurrentTarget, 35f, 30f)
                );
        }

        #endregion


        private static uint _castId;
        private static int _steadyCount;
        private static bool _doubleSteadyCast;
        private static bool DoubleSteadyCast
        {
            get
            {
                if (_steadyCount > 1)
                {
                    _castId = 0;
                    _steadyCount = 0;
                    _doubleSteadyCast = false;
                    return _doubleSteadyCast;
                }
                
                return _doubleSteadyCast;
            }
            set 
            {
                if (_doubleSteadyCast && StyxWoW.Me.CurrentCastId == _castId)
                    return;

                _castId = StyxWoW.Me.CurrentCastId;
                _steadyCount++;
                _doubleSteadyCast = value;
            }
        }

        private static Composite CreateMarksmanDiagnosticOutputBehavior()
        {
            return new Decorator(
                ret => SingularSettings.Debug,
                new Throttle(1,
                    new Action(ret =>
                    {
                        string sMsg;
                        sMsg = string.Format(".... h={0:F1}%, focus={1:F1}, moving={2}, sfbuff={3}",
                            Me.HealthPercent,
                            Me.CurrentFocus,
                            Me.IsMoving,
                            Me.GetAuraTimeLeft(53224, false).TotalSeconds
                            );

                        if (!Me.GotAlivePet)
                            sMsg += ", no pet";
                        else
                            sMsg += string.Format(", peth={0:F1}%", Me.Pet.HealthPercent);

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            sMsg += string.Format(
                                ", {0}, {1:F1}%, {2:F1} yds, loss={3}",
                                target.SafeName(),
                                target.HealthPercent,
                                target.Distance,
                                target.InLineOfSpellSight
                                );
                        }

                        Logger.WriteDebug(Color.LightYellow, sMsg);
                        return RunStatus.Failure;
                    })
                    )
                );
        }


    }
}
