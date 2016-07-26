using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;
using System.Drawing;
using Styx.WoWInternals;
using CommonBehaviors.Actions;
using System.Collections.Generic;

namespace Singular.ClassSpecific.Hunter
{
    public class Survival
    {
        private static LocalPlayer Me => StyxWoW.Me;

	    #region Normal Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterSurvival)]
        public static Composite CreateHunterSurvivalPullAndCombat()
		{
	        SpellFindResults sfr;
	        SpellManager.FindSpell("Mongoose Bite", out sfr);
	        SpellChargeInfo mongooseBiteChargeInfo = (sfr.Override ?? sfr.Original)?.GetChargeInfo();

			return new PrioritySelector(
				Helpers.Common.EnsureReadyToAttackFromMelee(),

				Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(
                        CreateSurvivalDiagnosticOutputBehavior(),
						
                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Helpers.Common.CreateInterruptBehavior(),
						
						Spell.BuffSelf("Aspect of the Eagle", ret => Me.GetAuraTimeLeft("Mongoose Fury", false).TotalSeconds > 10),
						Spell.Cast("Mongoose Bite", req => SpellManager.HasSpell("Aspect of the Eagle") && Spell.GetSpellCooldown("Aspect of the Eagle").TotalSeconds <= 0 && !Me.HasActiveAura("Mongoose Fury")),
						Spell.BuffSelf("Snake Hunter", ret => Me.HasAura("Aspect of the Eagle") && mongooseBiteChargeInfo?.ChargesLeft <= 0),
						Spell.Cast("Butchery", ret => Unit.UnfriendlyUnits(8).Count() >= 2),
						Spell.Cast("Explosive Trap"),
						Spell.Cast("Dragonsfire Grenade"),
						Spell.CastOnGround("Steel Trap", on => Me.CurrentTarget.Location, ret => Me.CurrentTarget.TimeToDeath() > 5),
						Spell.Cast("Raptor Strike", 
							ret => Common.HasTalent(HunterTalents.WayOfTheMokNathal) && (Me.GetAuraTimeLeft("Mok'Nathal Tactics").TotalSeconds < 1.8 || Me.GetAuraStacks("Mok'Nathal Tactics") < 4)),
						Spell.Cast("Lacerate"),
						Spell.Cast("Mongoose Bite", 
							ret => mongooseBiteChargeInfo != null && mongooseBiteChargeInfo.ChargesLeft == 2 && mongooseBiteChargeInfo.TimeUntilNextCharge.TotalSeconds < 1.8),
						Spell.Cast("Throwing Axes"),
						Spell.CastOnGround("Caltrops", on => Me.CurrentTarget.Location),
						Spell.Cast("Spitting Cobra"),
						// Avoiding getting focus capped with Flanking Strike
						Spell.Cast("Flanking Strike", ret => Common.FocusDeficit < 20 && Unit.UnfriendlyUnits(8).Count() <= 3),
						Spell.Cast("Carve", ret => Common.FocusDeficit < 20 && Unit.UnfriendlyUnits(8).Count() >= 4),
						Spell.Cast("Carve", ret => Common.HasTalent(HunterTalents.SerpentString) && Unit.UnfriendlyUnits(8).Count(u => u.GetAuraTimeLeft("Serpent String").TotalSeconds < 1.8) >= 3),
						Spell.Cast("Mongoose Bite", ret => !SpellManager.HasSpell("Aspect of the Eagle") || Spell.GetSpellCooldown("Aspect of the Eagle").TotalSeconds > 12)
                        )
                    )
                );
        }

        #endregion

		private static Composite CreateSurvivalDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses( 1,
                new Action(ret =>
                {
	                var sMsg = $".... h={Me.HealthPercent:F1}%, focus={Me.CurrentFocus:F1}, moving={Me.IsMoving}";

                    if ( !Me.GotAlivePet)
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
