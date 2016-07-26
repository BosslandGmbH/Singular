using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot;
using System.Drawing;
using System;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Frost
	{
		private static LocalPlayer Me => StyxWoW.Me;
		private static DeathKnightSettings DeathKnightSettings => SingularSettings.Instance.DeathKnight();

	    #region Normal Rotations
		
        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost)]
        public static Composite CreateDeathKnightFrostCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateDeathKnightPullMore(),
						
                        // Cooldowns
                        Spell.BuffSelf("Pillar of Frost", req => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && Me.CurrentTarget.IsStressful()),
						Spell.Cast("Empower Rune Weapon", 
							ret =>  Me.CurrentRunes <= 0 && Common.RunicPowerDeficit >= 35 && 
									(!Common.HasTalent(DeathKnightTalents.BreathOfSindragosa) || Me.HasActiveAura("Breath of Sindragosa"))),

						new Decorator(ret => Me.Level < 100,
							CreateLowLevelRotation()
							),

						new Decorator(
							ret => Common.HasTalent(DeathKnightTalents.Obliteration),
							CreateObliterationRotation()
							),

						new Decorator(
							ret => Common.HasTalent(DeathKnightTalents.BreathOfSindragosa),
							CreateBreathOfSindragosaRotation()
							),

						new Decorator(
							ret => Common.HasTalent(DeathKnightTalents.GlacialAdvance),
							CreateGlacialAdvanceRotation()
							)
						)
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

	    private static Composite CreateLowLevelRotation()
	    {
		    return new PrioritySelector(
				Spell.Cast("Howling Blast", ret => Me.CurrentTarget.GetAuraTimeLeft("Frost Fever").TotalSeconds < 1.8d),
				Spell.Cast("Remorseless Winter", ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= 2),
				Spell.Cast("Frostscythe", ret => Spell.UseAOE && Clusters.GetClusterCount(Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8) >= 3),
				Spell.Cast("Howling Blast", ret => Me.HasActiveAura("Rime")),
				Spell.Cast("Obliterate", ret => Common.RunicPowerDeficit >= 10),
				Spell.Cast("Frost Strike", ret => Common.RunicPowerDeficit < 35),
				Spell.Cast("Horn of Winter", ret => Me.CurrentRunes < 4 && Common.RunicPowerDeficit >= 20)
				);
	    }

	    private static Composite CreateObliterationRotation()
	    {
		    return new PrioritySelector(
				Spell.Cast("Howling Blast", ret => Me.CurrentTarget.GetAuraTimeLeft("Frost Fever").TotalSeconds < 1.8d),
				new Decorator(ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= 2,
					new PrioritySelector(
						Spell.Cast("Remorseless Winter"),
						Spell.Cast("Frostscythe", 
							ret => (Common.RunicPowerDeficit >= 10 || Me.HasActiveAura("Killing Machine") && Me.HasActiveAura("Obliteration"))))),
				Spell.Cast("Obliterate", ret => Common.RunicPowerDeficit >= 10 || Me.HasActiveAura("Killing Machine") && Me.HasActiveAura("Obliteration")),
				Spell.Cast("Howling Blast", ret => Me.HasActiveAura("Rime")),
				Spell.BuffSelf("Obliteration", ret => Me.CurrentRunicPower > 50 && !Me.HasActiveAura("Killing Machine")),
				Spell.Cast("Frost Strike", ret => Common.RunicPowerDeficit < 35 || Me.HasActiveAura("Obliteration")),
				Spell.Cast("Horn of Winter", ret => Me.CurrentRunes < 4 && Common.RunicPowerDeficit >= 20)
				);
	    }

		private static Composite CreateBreathOfSindragosaRotation()
		{
			return new PrioritySelector(
				Spell.Cast("Breath of Sindragosa", ret => Me.RunicPowerPercent > 80),
				Spell.Cast("Howling Blast", ret => Me.CurrentTarget.GetAuraTimeLeft("Frost Fever").TotalSeconds < 1.8d),
				Spell.Cast("Remorseless Winter", ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= 2),
				Spell.Cast("Howling Blast", ret => Me.HasActiveAura("Rime")),
				Spell.Cast("Frostscythe", ret => Me.HasActiveAura("Killing Machine")),
				Spell.Cast("Obliterate", ret => Common.RunicPowerDeficit >= 10),
				Spell.Cast("Frost Strike", 
					ret => Common.RunicPowerDeficit < 35 && !Me.HasActiveAura("Breath of Sindragosa") &&
							(Spell.GetSpellCooldown("Breath of Sindragosa").TotalSeconds > 10 || Me.RunicPowerPercent >= 100)),
				Spell.Cast("Horn of Winter", ret => Me.CurrentRunes < 4 && Common.RunicPowerDeficit >= 20 || Me.HasActiveAura("Breath of Sindragosa"))
				);
		}

		private static Composite CreateGlacialAdvanceRotation()
		{
			return new PrioritySelector(
				Spell.Cast("Howling Blast", ret => Me.CurrentTarget.GetAuraTimeLeft("Frost Fever").TotalSeconds < 1.8d),
				Spell.Cast("Remorseless Winter", ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= 2),
				Spell.CastOnGround("Glacial Advance", ret => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f).Location, ret => Spell.UseAOE),
				Spell.Cast("Frostscythe", ret => Spell.UseAOE && Clusters.GetClusterCount(Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8) >= 3),
				Spell.Cast("Obliterate", ret => Common.RunicPowerDeficit >= 10 && (!Spell.UseAOE || !Unit.UnfriendlyUnitsNearTarget(8).Any())),
				Spell.Cast("Howling Blast", ret => Me.HasActiveAura("Rime")),
				Spell.Cast("Frost Strike", ret => Common.RunicPowerDeficit < 35),
				Spell.Cast("Horn of Winter", ret => Me.CurrentRunes < 4 && Common.RunicPowerDeficit >= 20)
				);
		}

		#endregion
	}
}