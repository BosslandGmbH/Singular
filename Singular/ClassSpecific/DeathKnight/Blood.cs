using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

namespace Singular.ClassSpecific.DeathKnight
{
    public static class Blood
    {
        private static DeathKnightSettings DeathKnightSettings => SingularSettings.Instance.DeathKnight();
	    private static LocalPlayer Me => StyxWoW.Me;

	    const int CrimsonScourgeProc = 81141;

        #region CombatBuffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood)]
        public static Composite CreateDeathKnightBloodCombatBuffs()
        {
            return new Decorator(
                req => !Me.GotTarget() || !Me.CurrentTarget.IsTrivial(),
                new PrioritySelector(

                    // *** Defensive Cooldowns ***
                    // Anti-magic shell - no cost and doesnt trigger GCD 
                    Spell.BuffSelf("Anti-Magic Shell",
                        ret => Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == StyxWoW.Me.Guid)),
					
                    Spell.Cast("Dancing Rune Weapon",
                        ret => Unit.NearbyUnfriendlyUnits.Count() > 2),

                    Spell.BuffSelf("Vampiric Blood",
                        ret => Me.HealthPercent < DeathKnightSettings.VampiricBloodPercent
                            && (!DeathKnightSettings.VampiricBloodExclusive || !Me.HasAnyAura("Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude")))
                    )
                );
        }

		#endregion

		#region Normal Rotation
		
		[Behavior(BehaviorType.Pull, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood, WoWContext.All, 1)]
		public static Composite CreateDeathKnightBloodPull()
		{
			return
				new PrioritySelector(
					Spell.Cast("Death's Caress")
					);
		}

		[Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood)]
        public static Composite CreateDeathKnightBloodCombat()
		{
			TankManager.NeedTankTargeting = (SingularRoutine.CurrentWoWContext == WoWContext.Instances);

			return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateDeathKnightPullMore(),

						new Decorator(
							ret => SingularSettings.Instance.EnableTaunting && SingularRoutine.CurrentWoWContext == WoWContext.Instances
								&& TankManager.Instance.NeedToTaunt.Any()
								&& TankManager.Instance.NeedToTaunt.FirstOrDefault().InLineOfSpellSight,
							new Throttle(TimeSpan.FromMilliseconds(1500),
								new PrioritySelector(
									// Direct Taunt
									Spell.Cast("Dark Command",
										ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
										ret => true),

									new Decorator(
										ret => TankManager.Instance.NeedToTaunt.Any()   /*recheck just before referencing member*/
											&& Me.SpellDistance(TankManager.Instance.NeedToTaunt.FirstOrDefault()) > 10,
										
										// use DG if we have to (be sure to stop movement)
										Common.CreateDeathGripBehavior()
									)
								)
							)
						),

						Common.CreateDeathGripBehavior(),

						// Talents
						Spell.Cast("Blooddrinker", ret => Me.HealthPercent <= DeathKnightSettings.BloodDrinkerPercent),
						Spell.Buff("Mark of Blood", ret => Me.HealthPercent <= DeathKnightSettings.MarkOfBloodPercent),
						Spell.Cast("Tombstone", ret => Me.HealthPercent <= DeathKnightSettings.TombstonePercent && Me.GetAuraStacks("Bone Shield") >= DeathKnightSettings.TombstoneBoneShieldCharges),
						Spell.Cast("Rune Tap", ret => Me.HealthPercent <= DeathKnightSettings.RuneTapPercent),
						Spell.Cast("Bonestorm", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() < 8) >= DeathKnightSettings.BonestormCount && Me.RunicPowerPercent >= DeathKnightSettings.BonestormRunicPowerPercent),

                        // refresh diseases if possible
                        Spell.Cast("Blood Boil", 
							req => Unit.NearbyUnfriendlyUnits.Any(u => !u.HasMyAura("Blood Plague") && u.SpellDistance() < 10)),

                        // Start AoE section
                        new Decorator(
                            ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.MeleeDistance() < 10) > 1,
                            new PrioritySelector(
                                Spell.CastOnGround("Death and Decay", on => StyxWoW.Me.CurrentTarget, ret => Unit.UnfriendlyUnitsNearTarget(15).Count() >= DeathKnightSettings.DeathAndDecayCount, false),
								Spell.Cast("Marrowrend", ret => Me.GetAuraStacks("Bone Shield") < (Unit.NearbyUnfriendlyUnits.Count(u => u.MeleeDistance() < 10) >= 4 ? 5 : 1)),
								Spell.Cast("Death Strike"),
								Spell.Cast("Heart Strike", ret => Me.GetAuraStacks("Bone Shield") >= (Unit.NearbyUnfriendlyUnits.Count(u => u.MeleeDistance() < 10) >= 4 ? 5 : 1) && Me.CurrentRunes > 0),
								Spell.Cast("Blood Boil", ret => Me.CurrentRunes <= 0 && Spell.UseAOE && Unit.UnfriendlyUnits(10).Count() >= DeathKnightSettings.BloodBoilCount)
                                )
                            ),
						
						// Single target rotation
						Spell.CastOnGround("Death and Decay", ret => Me.CurrentTarget, ret => Me.HasAura(CrimsonScourgeProc)),
						Spell.Cast("Marrowrend", ret => Me.GetAuraStacks("Bone Shield") < 5),
						Spell.Cast("Death Strike"),
						new Decorator(ret => Me.GetAuraStacks("Bone Shield") >= 5 && Me.CurrentRunes > 0,
							new PrioritySelector(
								Spell.CastOnGround("Death and Decay", ret => Me.CurrentTarget.Location),
								Spell.Cast("Heart Strike")
								)
							),
						Spell.Cast("Blood Boil", ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Any(u => u.MeleeDistance() < 10))
						)
					)
                );
        }

        #endregion
		
    }
}