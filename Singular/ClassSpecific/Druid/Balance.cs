using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Lists;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.Common;
using System.Drawing;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using Styx.Pathing;
using Styx.Common.Helpers;
using Styx.CommonBot.Routines;

namespace Singular.ClassSpecific.Druid
{
    public class Balance
    {
        # region Properties & Fields
        private static DruidSettings DruidSettings => SingularSettings.Instance.Druid();
		private static LocalPlayer Me => StyxWoW.Me;
	    private static long AstralPowerDeficit => Me.MaxAstralPower - Me.CurrentAstralPower;

        #endregion
		
        #region Heal
		
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidBalance)]
        public static Composite CreateDruidBalanceHeal()
        {
            return new PrioritySelector(
                new Decorator(
                    req => Me.Combat 
                        && Me.HealthPercent < DruidSettings.SelfHealingTouchHealth && Me.GetPredictedHealthPercent(true) < DruidSettings.SelfHealingTouchHealth,
                    Common.CreateDruidCrowdControl()
                    )

                );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidBalance)]
        public static Composite CreateDruidBalancePull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),
						
						Spell.Cast("Lunar Strike"),
						Spell.Cast("Solar Wrath")
						)
					)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidBalance)]
        public static Composite CreateDruidBalanceCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.Instance.HealBehavior,
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),
						
                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),
						
                        Helpers.Common.CreateInterruptBehavior(),

						Common.CastForm(ShapeshiftForm.Moonkin, req => Me.Shapeshift != ShapeshiftForm.Moonkin && !Utilities.EventHandlers.IsShapeshiftSuppressed),

						// Avoid getting capped
						Spell.Cast("Astral Communion", ret => AstralPowerDeficit >= 75 + 5 && (!Common.HasTalent(DruidTalents.FuryOfElune) || Me.HasActiveAura("Fury of Elune"))),
						Spell.Cast("Blessing of Elune"),
						Spell.CastOnGround("Force of Nature", on => Me.CurrentTarget.Location),
						Spell.BuffSelf("Celestial Alignment", ret => Me.CurrentTarget.IsStressful()),
						Spell.Cast("Moonfire", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.GetAuraTimeLeft("Moonfire").TotalSeconds < 1.8 && u.TimeToDeath(int.MaxValue) > 4)),
						Spell.Cast("Sunfire", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.GetAuraTimeLeft("Sunfire").TotalSeconds < 1.8 && u.TimeToDeath(int.MaxValue) > 4)),
						Spell.Cast("Stellar Flare", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.GetAuraTimeLeft("Stellar Flare").TotalSeconds < 1.8 && u.TimeToDeath(int.MaxValue) > 12)),

						Spell.CastOnGround("Starfall", on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 15).Location, ret => Unit.NearbyUnfriendlyUnits.Count() >= 3),
						Spell.Cast("Starsurge", ret => Me.GetAuraStacks("Lunar Empowerment") < 3 && Me.GetAuraStacks("Solar Empowerment") < 3),

						Spell.Cast("Solar Wrath", ret => Me.GetAuraStacks("Solar Empowerment") > 0),
						Spell.Cast("Warrior of Elune", ret => Me.GetAuraStacks("Lunar Empowerment") >= 2),
						Spell.Cast("Lunar Strike", ret => Me.GetAuraStacks("Lunar Empowerment") > 0),
						Spell.Cast("Solar Wrath", ret => !Unit.UnfriendlyUnitsNearTarget(5f).Any()),
						Spell.Cast("Lunar Strike", ret => Unit.UnfriendlyUnitsNearTarget(5f).Any())
						)
					)
                );
        }

        #endregion
    }

}
