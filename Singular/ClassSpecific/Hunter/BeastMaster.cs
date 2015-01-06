using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.Common;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using System.Drawing;

namespace Singular.ClassSpecific.Hunter
{
    public class BeastMaster
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WoWUnit Pet { get { return StyxWoW.Me.Pet; } }
        private static HunterSettings HunterSettings { get { return SingularSettings.Instance.Hunter(); } }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull|BehaviorType.Combat,WoWClass.Hunter,WoWSpec.HunterBeastMastery,WoWContext.Normal | WoWContext.Instances )]
        public static Composite CreateBeastMasterHunterNormalPullAndCombat()
        {
            return new PrioritySelector(

                Common.CreateHunterEnsureReadyToAttackFromLongRange(),
                
                Spell.WaitForCastOrChannel(),
            
                new Decorator(

                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        CreateBeastMasteryDiagnosticOutputBehavior(),

                        Common.CreateMisdirectionBehavior(),

                        Common.CreateHunterAvoidanceBehavior(null, null),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateHunterNormalCrowdControl(),

                        Spell.Cast("Tranquilizing Shot", req => Me.GetAllAuras().Any(a => a.Spell.DispelType == WoWDispelType.Enrage)),

                        Spell.Buff("Concussive Shot",
                            ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid 
                                && Me.CurrentTarget.Distance > Spell.MeleeRange),

                        // Defensive Stuff
                        Spell.Cast( "Intimidation", 
                            ret => Me.GotTarget() 
                                && Me.CurrentTarget.IsAlive 
                                && Me.GotAlivePet 
                                && (!Me.CurrentTarget.GotTarget() || Me.CurrentTarget.CurrentTarget == Me)),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && Me.GotTarget() && !(Me.CurrentTarget.IsBoss() || Me.CurrentTarget.IsPlayer) && Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3,
                            new PrioritySelector(
                                ctx => Unit.NearbyUnitsInCombatWithUsOrOurStuff.Where(u => u.InLineOfSpellSight).OrderByDescending(u => (uint)u.HealthPercent).FirstOrDefault(),
                                Spell.Cast("Kill Shot", onUnit => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u))),
                                Common.CreateHunterTrapBehavior("Explosive Trap", true, on => Me.CurrentTarget, req => !TalentManager.HasGlyph("Explosive Trap")),
                                Spell.Cast("Multi-Shot", ctx => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u)), ClusterType.Radius, 8f), req => Me.CurrentFocus > 60 || Me.HasAura("Thrill of the Hunt")),
                                Spell.Cast( "Cobra Shot", on => (WoWUnit) on),
                                Common.CastSteadyShot(on => (WoWUnit)on, ret => !SpellManager.HasSpell("Cobra Shot"))
                                )
                            ),

                        // Single Target Rotation
                        Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent < 20),
                        Spell.CastHack("Kill Command", on => Me.Pet == null ? null : Me.Pet.CurrentTarget, req => Me.GotAlivePet && Me.Pet.SpellDistance(Pet.CurrentTarget) < 25f),
                        Spell.BuffSelf("Focus Fire", ctx => Me.HasAura("Frenzy", 5)),
                        Spell.Cast("Arcane Shot", ret => Me.CurrentFocus > 60 || Me.HasAura("Thrill of the Hunt")),
                        Spell.Cast("Cobra Shot"),

                        Common.CastSteadyShot( on => Me.CurrentTarget, ret => !SpellManager.HasSpell("Cobra Shot"))
                        )
                    )
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterBeastMastery, WoWContext.Battlegrounds)]
        public static Composite CreateBeastMasterHunterPvPPullAndCombat()
        {
            return new PrioritySelector(

                Common.CreateHunterEnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateBeastMasteryDiagnosticOutputBehavior(),

                        Common.CreateHunterAvoidanceBehavior(null, null),

                        // Helpers.Common.CreateInterruptBehavior(),
                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateHunterPvpCrowdControl(),

                        Spell.Cast("Tranquilizing Shot", req => Me.GetAllAuras().Any(a => a.Spell.DispelType == WoWDispelType.Enrage)),

                        Spell.Buff("Concussive Shot",
                            ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid
                                && Me.CurrentTarget.Distance > Spell.MeleeRange),

                        // Common.CreateHunterTrapOnAddBehavior("Explosive Trap"),

                        // Defensive Stuff
                        Spell.Cast("Intimidation",
                            ret => Me.GotTarget()
                                && Me.CurrentTarget.IsAlive
                                && Me.GotAlivePet
                                && (!Me.CurrentTarget.GotTarget() || Me.CurrentTarget.CurrentTarget == Me)),

                        // snipe kills if possible
                        Spell.Cast("Kill Shot", onUnit => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 5 && u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u))),

                        Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent < 20),

                        // Single Target Rotation (no AoE in PVP)
                        Spell.CastHack("Kill Command", ctx => Me.Pet == null ? null : Me.Pet.CurrentTarget, ret => Me.GotAlivePet && Pet.GotTarget() && Pet.Location.Distance(Pet.CurrentTarget.Location) < (Pet.MeleeDistance(Pet.CurrentTarget) + 20f)),
                        Spell.BuffSelf("Focus Fire", ctx => Me.HasAura("Frenzy", 5) && !Me.HasAura("The Beast Within")),

                        Spell.Cast("Arcane Shot", ret => Me.CurrentFocus > 60 || Me.HasAnyAura("Thrill of the Hunt", "The Beast Within")),
                        Spell.Cast("Cobra Shot"),
                        Common.CastSteadyShot(on => Me.CurrentTarget, ret => !SpellManager.HasSpell("Cobra Shot"))
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 35f, 30f)
                );
        }

        #endregion

        private static Composite CreateBeastMasteryDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses( 1,
                new Action(ret =>
                {
                    string sMsg;
                    sMsg = string.Format(".... h={0:F1}%, focus={1:F1}, moving={2}",
                        Me.HealthPercent,
                        Me.CurrentFocus,
                        Me.IsMoving
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
