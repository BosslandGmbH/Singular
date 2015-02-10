using System;
using System.Linq;
using System.Threading;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.Helpers;

using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using System.Drawing;
using CommonBehaviors.Actions;
using System.Collections.Generic;

namespace Singular.ClassSpecific.Hunter
{
    public class Marksman
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WoWUnit Pet { get { return StyxWoW.Me.Pet; } }
        private static HunterSettings HunterSettings { get { return SingularSettings.Instance.Hunter(); } }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterMarksmanship, WoWContext.Normal)]
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

                        Spell.Cast("Tranquilizing Shot", req => Me.GetAllAuras().Any(a => a.Spell.DispelType == WoWDispelType.Enrage)),

                        Spell.Buff("Concussive Shot",
                            ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid
                                && Me.CurrentTarget.Distance > Spell.MeleeRange),

                        // Defensive Stuff
                        Spell.Cast("Intimidation",
                            ret => Me.GotTarget()
                                && Me.CurrentTarget.IsAlive
                                && Me.GotAlivePet
                                && (!Me.CurrentTarget.GotTarget() || Me.CurrentTarget.CurrentTarget == Me)),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && Me.GotTarget() && !(Me.CurrentTarget.IsBoss() || Me.CurrentTarget.IsPlayer) && Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3,
                            new PrioritySelector(
                                ctx => Unit.NearbyUnitsInCombatWithUsOrOurStuff.Where(u => u.InLineOfSpellSight).OrderByDescending(u => (uint) u.HealthPercent).FirstOrDefault(),
                                Spell.Cast("Kill Shot", onUnit => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u)), req => Me.HealthPercent < 85),
                                Spell.Cast("Multi-Shot", ctx => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u)), ClusterType.Radius, 8f)),
                                Common.CreateHunterTrapBehavior("Explosive Trap", true, on => Me.CurrentTarget, req => true),
                                Common.CastSteadyShot(on => Me.CurrentTarget)
                                )
                            ),

                        // Single Target Rotation
                        Spell.Cast("Kill Shot", ctx => Me.GotTarget() && Me.CurrentTarget.HealthPercent < 20),
                        Spell.Cast("Chimaera Shot"),
                        Spell.Cast("Aimed Shot", ret => Me.CurrentFocus > 60),
                        Common.CastSteadyShot(on => Me.CurrentTarget)
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 35f, 30f)
                );
        }

        #endregion

		#region Instance Rotation

		[Behavior(BehaviorType.Pull, WoWClass.Hunter, WoWSpec.HunterMarksmanship, WoWContext.Instances)]
	    public static Composite CreateMarksmanHunterInstancePull()
	    {
		    return new PrioritySelector();
	    }

		[Behavior(BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterMarksmanship, WoWContext.Instances)]
		public static Composite CreateMarksmanHunterInstanceCombat()
		{
			return new PrioritySelector(
				Common.CreateHunterEnsureReadyToAttackFromLongRange(),

				Spell.WaitForCastOrChannel(),

				Helpers.Common.CreateInterruptBehavior(),
				
				CreateMarksmanDiagnosticOutputBehavior(),
								
				// We don't really want to do any of those below in raids
				new Decorator(ret => !Me.CurrentMap.IsRaid,
					new PrioritySelector(
						Common.CreateMisdirectionBehavior(),

						Common.CreateHunterAvoidanceBehavior(null, null),

						Common.CreateHunterNormalCrowdControl(),

						Spell.Cast("Tranquilizing Shot", req => Me.GetAllAuras().Any(a => a.Spell.DispelType == WoWDispelType.Enrage)),

						Spell.Buff("Concussive Shot",
							ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid
								&& Me.CurrentTarget.Distance > Spell.MeleeRange),

						// Defensive Stuff
						Spell.Cast("Intimidation",
							ret => Me.GotTarget()
								&& Me.CurrentTarget.IsAlive
								&& Me.GotAlivePet
								&& (!Me.CurrentTarget.GotTarget() || Me.CurrentTarget.CurrentTarget == Me))
								)
							),
							
				Spell.Cast("Chimaera Shot"),
				Spell.Cast("Kill Shot", ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < KillShotPercentage && u.DistanceSqr < 40 * 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u))),
				Spell.Cast("Rapid Fire"),
				Spell.Cast("Aimed Shot", ret => Me.HasAura("Rapid Fire") || Me.CurrentTarget.HealthPercent > 80),
				Spell.Cast("A Murder of Crows"),
				Spell.Cast("Stampede"),
				Spell.Cast("Glaive Toss"),
				Spell.Cast("Multi-Shot", req => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 6),
				Spell.Cast("Aimed Shot", ret => Me.HasAura("Thrill of the Hunt")),

                Spell.Cast(
                    "Barrage",
                    on =>
                    {
                        if (!Spell.UseAOE)
                            return null;

                        // Does not require CurrentTarget to be non=null
                        WoWPoint loc = WoWPoint.RayCast(Me.Location, Me.RenderFacing, 30f);
                        IEnumerable<WoWUnit> ienum = Clusters.GetConeCluster(loc, 60f, 42f, Unit.UnfriendlyUnits(50));
                        int cntCC = 0;
                        int cntTarget = 0;
                        int cntNeutral = 0;
                        WoWUnit target = null;

                        foreach (WoWUnit u in ienum)
                        {
                            cntTarget++;
                            if (u.IsCrowdControlled())
                                cntCC++;
                            if (!u.Combat && !u.IsTrivial() && !u.Aggro && !u.PetAggro && !(u.IsTargetingMeOrPet || u.IsTargetingMyRaidMember))
                                cntNeutral++;
                            if (target == null)
                                target = u;
                            if (Me.CurrentTargetGuid == u.Guid)
                                target = u;
                        }

                        if (cntNeutral > 0)
                        {
                            Logger.WriteDebug("Barrage: skipping, {0} additional targets would be pulled", cntNeutral);
                            return null;
                        }

                        if (cntCC > 0)
                        {
                            Logger.WriteDebug("Barrage: skipping, {0} crowd controlled targets", cntCC);
                            return null;
                        }

                        if (cntTarget == 0)
                        {
                            Logger.WriteDebug("Barrage: skipping, no targets would be hit");
                            return null;
                        }

                        return target;
                    }
                    ),

                Spell.Cast("Steady Shot", req => TalentManager.IsSelected(10) && Me.HasKnownAuraExpired("Steady Focus", 2)),
				Common.CreateHunterTrapBehavior("Explosive Trap", true, onUnit => Me.CurrentTarget, req => Unit.UnfriendlyUnitsNearTarget(10f).Any()),
				Spell.Cast("Aimed Shot"),
				Spell.Cast("Focusing Shot"),
				Spell.Cast("Steady Shot"),

				Movement.CreateMoveToUnitBehavior(on => StyxWoW.Me.CurrentTarget, 35f, 30f)
				);
		}

		private static double KillShotPercentage { get { return SpellManager.HasSpell("Enhanced Kill Shot") ? 35d : 20d; } }

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

                        Spell.Cast("Tranquilizing Shot", req => Me.GetAllAuras().Any(a => a.Spell.DispelType == WoWDispelType.Enrage)),

                        Spell.Buff("Concussive Shot",
                            ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid
                                && !Me.CurrentTarget.IsWithinMeleeRange),

                        // Defensive Stuff
                        Spell.Cast("Intimidation",
                            ret => Me.GotTarget()
                                && Me.CurrentTarget.IsAlive
                                && Me.GotAlivePet
                                && (!Me.CurrentTarget.GotTarget() || Me.CurrentTarget.CurrentTarget == Me)),

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
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses( 1,
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
                );
        }


    }
}
