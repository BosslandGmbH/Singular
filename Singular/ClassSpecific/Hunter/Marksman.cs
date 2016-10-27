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
using System.Numerics;

namespace Singular.ClassSpecific.Hunter
{
    public class Marksman
    {
        private static LocalPlayer Me => StyxWoW.Me;
        private static HunterSettings HunterSettings => SingularSettings.Instance.Hunter();

        #region Normal Rotation

	    private static SpellChargeInfo GetSidewindersChargeInfo()
	    {
			SpellFindResults sfr;
			SpellManager.FindSpell("Sidewinders", out sfr);
			return (sfr.Override ?? sfr.Original)?.GetChargeInfo();
		}

	    [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterMarksmanship)]
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

					    Movement.WaitForFacing(),
					    Movement.WaitForLineOfSpellSight(),

					    Helpers.Common.CreateInterruptBehavior(),

					    Common.CreateHunterNormalCrowdControl(),
                        
                        // Artifact Weapon
                        new Decorator(
                            ret => HunterSettings.UseArtifactOnlyInAoE && Unit.UnfriendlyUnitsNearTarget(15).Count() > 1,
                            new PrioritySelector(
                                Spell.Cast("Windburst", ret => HunterSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None)
                                )
                        ),
                        Spell.Cast("Windburst", ret => HunterSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None),

					    Spell.Buff("Concussive Shot",
						    ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid
						           && Me.CurrentTarget.Distance > Spell.MeleeRange),

					    Spell.BuffSelf("Trueshot", ret => Me.CurrentTarget.IsStressful() || PartyBuff.WeHaveBloodlust),

					    Spell.Cast("Sentinel",
						    ret =>
							    !Me.HasActiveAura("Marking Targets") && Unit.NearbyUnitsInCombatWithUsOrOurStuff.All(u => !u.HasMyAura("Hunter's Mark"))),
					    // Detonate the missile
					    Spell.Cast("Explosive Shot",
						    ret =>
							    WoWMissile.InFlightMissiles.Any(
								    m =>
									    m.Caster != null && m.Caster.IsMe && m.Spell?.Name == "Explosive Shot" &&
									    m.Position.Distance(Me.CurrentTarget.Location) < 8f)),


					    Spell.Cast("Arcane Shot",
						    ret => Common.HasTalent(HunterTalents.SteadyFocus) && !Common.HasTalent(HunterTalents.Sidewinders) &&
						           Me.GetAuraTimeLeft("Steady Focus").TotalSeconds < 4.2d),
					    Spell.Cast("Marked Shot",
						    on =>
							    Unit.NearbyUnitsInCombatWithUsOrOurStuff.FirstOrDefault(u => u.HasMyAura("Vulnerable") && u.TimeToDeath(int.MaxValue) < 2)),
					    Spell.Cast("Marked Shot", ret => !Me.CurrentTarget.HasMyAura("Vulnerable")),
					    // Cast Marked Shot if Vulnerable buff will expire until we can get a Aimed Shot out
					    Spell.Cast("Marked Shot",
						    ret => Me.CurrentTarget.GetAuraTimeLeft("Vulnerable") < Spell.GetSpellCastTime("Aimed Shot")),
					    // Also cover focus needed to cast Aimed shot
					    Spell.Cast("Marked Shot",
						    ret =>
							    Me.CurrentFocus +
							    (Me.GetPowerRegenInterrupted(WoWPowerType.Focus)*
							     (Me.CurrentTarget.GetAuraTimeLeft("Vulnerable").TotalSeconds)) < 50),
					    Spell.Cast("Piercing Shot", ret => Me.CurrentFocus >= 100),
					    Spell.Cast("Barrage"),
					    // Fire the missile
					    Spell.Cast("Explosive Shot",
						    ret =>
							    !WoWMissile.InFlightMissiles.Any(m => m.Caster != null && m.Caster.IsMe && m.Spell?.Name == "Explosive Shot")),
					    Spell.Cast("Aimed Shot", ret => Me.CurrentTarget.GetAuraTimeLeft("Vulnerable").TotalSeconds > 2d),
					    Spell.Cast("Black Arrow"),

						new Decorator(ret => Common.HasTalent(HunterTalents.Sidewinders),
							new PrioritySelector(
								Spell.Cast("Sidewinders", ret => Common.HasTalent(HunterTalents.SteadyFocus) && !Me.HasActiveAura("Steady Focus")),
								Spell.Cast("Sidewinders", ret => Me.HasActiveAura("Marking Targets")),
								Spell.Cast("Sidewinders",
									ret => Spell.GetCharges("Sidewinders") >= 2 ||
											Spell.GetCharges("Sidewinders") == 1 && GetSidewindersChargeInfo().TimeUntilNextCharge.TotalSeconds < 1.8)
								)
							),
						new Decorator(ret => !Common.HasTalent(HunterTalents.Sidewinders),
							new PrioritySelector(
								Spell.Cast("Multi-Shot", ret => Unit.NearbyUnitsInCombatWithUsOrOurStuff.Count() >= 2),
								Spell.Cast("Arcane Shot", ret => Unit.NearbyUnitsInCombatWithUsOrOurStuff.Count() < 2)
								)
							)
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 35f, 30f)
                );
        }

        #endregion

        private static Composite CreateMarksmanDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(
                1,
                TimeSpan.FromSeconds(1),
                RunStatus.Failure,
                new Action(ret =>
                {
	                var sMsg =
		                $".... h={Me.HealthPercent:F1}%, focus={Me.CurrentFocus:F1}, moving={Me.IsMoving}, sfbuff={Me.GetAuraTimeLeft(53224, false).TotalSeconds}";

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
