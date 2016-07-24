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
using System;
using System.Collections.Generic;
using System.Drawing;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Unholy
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static DeathKnightSettings DeathKnightSettings => SingularSettings.Instance.DeathKnight();

	    #region INIT

        [Behavior(BehaviorType.Initialize, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy)]
        public static Composite CreateUnholyDeathKnightInitialize()
        {
            PetManager.NeedsPetSupport = true;
            return null;
        }

        #endregion

        #region Rest

        [Behavior(BehaviorType.Rest, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy)]
        public static Composite CreateUnholylDeathKnightRest()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Decorator(
                            ret => !Rest.IsEatingOrDrinking,
                            new PrioritySelector(
                                UnholyCastRaiseDead(),
                                Spell.Cast("Death Coil", on => Me.Pet, req => Me.GotAlivePet && Me.Pet.HealthPercent < 85)
                                )
                            ),

                        Singular.Helpers.Rest.CreateDefaultRestBehaviour()
                        )
                    )
                );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy)]
        public static Composite CreateDeathKnightUnholyCombat()
        {
            return new PrioritySelector(
				UnholyCastRaiseDead(),
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

                        Common.CreateDeathGripBehavior(),


						new Decorator(
							ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= DeathKnightSettings.DeathAndDecayCount || 
									Common.HasTalent(DeathKnightTalents.Epidemic) && Unit.NearbyUnfriendlyUnits.Count() >= DeathKnightSettings.EpidemicCount,
							new PrioritySelector(
								Spell.Cast("Summon Gargoyle", ret => Me.CurrentTarget.IsStressful() && DeathKnightSettings.UseSummonGargoyle),
								Spell.Cast("Outbreak", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.IsWithinMeleeRange && u.GetAuraTimeLeft("Virulent Plague").TotalSeconds < 1.8)),
								Spell.BuffSelf("Dark Transformation", ret => Me.GotAlivePet),
								Spell.CastOnGround("Death and Decay", on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f).Location),
								Spell.Cast("Scourge Strike"),
								Spell.Cast("Epidemic", 
									ret => Spell.IsSpellOnCooldown("Death and Decay") || Spell.IsSpellOnCooldown("Defile") ||
											Unit.NearbyUnfriendlyUnits.Count() >= DeathKnightSettings.EpidemicCount && 
											Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) < DeathKnightSettings.DeathAndDecayCount),
								Spell.Cast("Festering Strike", ret => Me.CurrentTarget.GetAuraStacks("Festering Wound") < 5),
								Spell.Cast("Death Coil", ret => Me.RunicPowerPercent > 90)
								)
							),

						new Decorator(
							ret => Common.HasTalent(DeathKnightTalents.DarkArbiter),
							CreateDarkArbiterRotation()
							),

						new Decorator(
							ret => Common.HasTalent(DeathKnightTalents.Defile),
							CreateDefileRotation()
							),

						new Decorator(
							ret => Common.HasTalent(DeathKnightTalents.SoulReaper),
							CreateSoulReaperRotation()
							)
						)
					),

                Movement.CreateMoveToMeleeBehavior(true)
                );
		}
		private static Composite CreateDefileRotation()
		{
			return new PrioritySelector(
				Spell.Cast("Summon Gargoyle", ret => Me.CurrentTarget.IsStressful() && DeathKnightSettings.UseSummonGargoyle),
				Spell.Cast("Outbreak", ret => Me.CurrentTarget.GetAuraTimeLeft("Virulent Plague").TotalSeconds < 1.8),
				Spell.BuffSelf("Dark Transformation", ret => Me.GotAlivePet),
				Spell.CastOnGround("Defile", on => Me.Location, ret => Unit.NearbyUnfriendlyUnits.Any(u => u.IsWithinMeleeRange)),
				Spell.Cast("Festering Strike", ret => Me.CurrentTarget.GetAuraStacks("Festering Wound") < 5),
				Spell.Cast("Scourge Strike", ret => Me.CurrentTarget.GetAuraStacks("Festering Wound") >= 1),
				Spell.Cast("Death Coil", ret => Me.RunicPowerPercent > 90)
				);
		}

		private static Composite CreateDarkArbiterRotation()
		{
			return new PrioritySelector(
				Spell.Cast("Summon Gargoyle", ret => Me.CurrentTarget.IsStressful() && DeathKnightSettings.UseSummonGargoyle),
				Spell.Cast("Outbreak", ret => Me.CurrentTarget.GetAuraTimeLeft("Virulent Plague").TotalSeconds < 1.8),
				Spell.BuffSelf("Dark Transformation", ret => Me.GotAlivePet),
				Spell.Cast("Dark Arbiter", ret => Me.RunicPowerPercent > 90),
				Spell.Cast("Death Coil", ret => Me.HasActiveAura("Dark Arbiter")),
				Spell.Cast("Festering Strike", ret => Me.CurrentTarget.GetAuraStacks("Festering Wound") < 5),
				Spell.Cast("Scourge Strike", ret => Me.CurrentTarget.GetAuraStacks("Festering Wound") >= 1),
				Spell.Cast("Death Coil", req => Common.RunicPowerDeficit < 10)
				);
		}

		private static Composite CreateSoulReaperRotation()
		{
			return new PrioritySelector(
				Spell.Cast("Scourge Strike", ret => Me.HasActiveAura("Soul Reaper") && Me.GetAuraStacks("Soul Reaper") < 3),
				Spell.Cast("Summon Gargoyle", ret => Me.CurrentTarget.IsStressful() && DeathKnightSettings.UseSummonGargoyle),
				Spell.Cast("Outbreak", ret => Me.CurrentTarget.GetAuraTimeLeft("Virulent Plague").TotalSeconds < 1.8),
				Spell.BuffSelf("Dark Transformation", ret => Me.GotAlivePet),
				Spell.Cast("Festering Strike", ret => Me.CurrentTarget.GetAuraStacks("Festering Wound") < 5),
				Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.GetAuraStacks("Festering Wound") >= 3),
				Spell.Cast("Scourge Strike", ret => Me.CurrentTarget.GetAuraStacks("Festering Wound") >= 1),
				Spell.Cast("Death Coil", ret => Common.RunicPowerDeficit < 10)
				);
		}

		public static Composite UnholyCastRaiseDead()
		{
			return Spell.BuffSelf(
				"Raise Dead",
				req => PetManager.IsPetSummonAllowed
					&& !Me.Mounted
					&& !Me.OnTaxi
					);
		}

		#endregion
    }
}