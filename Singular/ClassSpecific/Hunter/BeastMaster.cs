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
        private static LocalPlayer Me => StyxWoW.Me;

	    #region Normal Rotation

        [Behavior(BehaviorType.Pull|BehaviorType.Combat,WoWClass.Hunter,WoWSpec.HunterBeastMastery)]
        public static Composite CreateBeastMasterHunterNormalPullAndCombat()
        {
            return new PrioritySelector(

                Common.CreateHunterEnsureReadyToAttackFromLongRange(),
                
                Spell.WaitForCastOrChannel(),
            
                new Decorator(

                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        CreateBeastMasteryDiagnosticOutputBehavior(),

                        Common.CreateMisdirectionBehavior(),

                        Common.CreateHunterAvoidanceBehavior(null, null),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateHunterNormalCrowdControl(),

                        Spell.Buff("Concussive Shot",
                            ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid 
                                && Me.CurrentTarget.Distance > Spell.MeleeRange),

                        // Defensive Stuff
                        Spell.Cast("Intimidation", 
                            ret => Me.GotTarget() 
                                && Me.CurrentTarget.IsAlive 
                                && Me.GotAlivePet 
                                && (!Me.CurrentTarget.GotTarget() || Me.CurrentTarget.CurrentTarget == Me)),
						
						new Decorator(
							ret => Me.CurrentTarget.IsStressful() || PartyBuff.WeHaveBloodlust,
							new PrioritySelector(
								Spell.Cast("Aspect of the Wild", ret => Me.HasAura("Bestial Wrath")),
								Spell.Cast("Bestial Wrath", ret => Me.CurrentFocus > 90 && Spell.GetSpellCooldown("Kill Command").TotalSeconds < 3),
								Spell.Cast("A Murder of Crows"),
								Spell.Cast("Stampede"))),
						
						new Decorator(
							ret => Unit.NearbyUnfriendlyUnits.Count() > 1,
							new PrioritySelector(
								new Throttle(1, Spell.BuffSelf("Volley")),
								Spell.Cast("Multi Shot", ret => Me.GotAlivePet && Me.Pet.GetAuraTimeLeft("Beast Cleave", false).TotalSeconds < 1.5),
								Spell.Cast("Barrage"),
								Spell.Cast("Kill Command"),
                                new Decorator(ret => Spell.GetSpellCooldown("Bestial Wrath").TotalSeconds > 15, 
                                    new PrioritySelector(
								        Spell.Cast("Dire Beast", ret => !Common.HasTalent(HunterTalents.DireFrenzy)),
                                        Spell.Cast("Dire Frenzy", ret => Me.GotAlivePet)
                                    )),
                                Spell.Cast("Cobra Shot", ret => Me.CurrentFocus > 90 && Me.GotAlivePet && Me.Pet.GetAuraTimeLeft("Beast Cleave", false).TotalSeconds > 1.5),
								Spell.Cast("Multi Shot")
								)),
							
						new Decorator(
							ret => Me.HasActiveAura("Volley"), 
							new Throttle(1, new Action(ret => Me.CancelAura("Volley")))),

						Spell.Cast("Kill Command"),
						Spell.Cast("Chimaera Shot"),
						Spell.Cast("Dire Beast", ret => Spell.GetSpellCooldown("Bestial Wrath").TotalSeconds > 15),
						Spell.Cast("Barrage"),
						Spell.Cast("Cobra Shot", ret => Me.CurrentFocus > 90)
                        )
                    )
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
	                string sMsg = $".... h={Me.HealthPercent:F1}%, focus={Me.CurrentFocus:F1}, moving={Me.IsMoving}";

                    if (!Me.GotAlivePet)
                        sMsg += ", no pet";
                    else
                        sMsg += $", peth={Me.Pet.HealthPercent:F1}%";

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        sMsg +=
	                        $", {target.SafeName()}, {target.HealthPercent:F1}%, {target.Distance:F1} yds, loss={target.InLineOfSpellSight}";
                    }

                    Logger.WriteDebug(Color.LightYellow, sMsg);
                    return RunStatus.Failure;
                })
                );
        }

    }
}
