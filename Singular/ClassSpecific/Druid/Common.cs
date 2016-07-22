#region

using System;
using System.Collections.Generic;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.CommonBot;
using Singular.Managers;
using CommonBehaviors.Actions;
using System.Drawing;
using Styx.CommonBot.POI;
using Styx.CommonBot.Routines;

#endregion

namespace Singular.ClassSpecific.Druid
{
	public class Common
	{
		public static ShapeshiftForm WantedDruidForm { get; set; }
		private static DruidSettings DruidSettings => SingularSettings.Instance.Druid();
		private static LocalPlayer Me => StyxWoW.Me;
		public static bool HasTalent(DruidTalents talent) => TalentManager.IsSelected((int)talent);

		public const WoWSpec DruidAllSpecs = (WoWSpec) int.MaxValue;

		private const int DREAM_OF_CENARIUS_GUARDIAN_PROC = 145162;
		private const int PREDATORY_SWIFTNESS_PROC = 69369;

		[Behavior(BehaviorType.Initialize, WoWClass.Druid)]
		public static Composite CreateDruidInitialize()
		{
			if (SingularRoutine.CurrentWoWContext == WoWContext.Normal ||
			    SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
			{
				if (TalentManager.CurrentSpec == WoWSpec.DruidBalance || TalentManager.CurrentSpec == WoWSpec.DruidRestoration)
				{
					Kite.CreateKitingBehavior(null, null, null);
				}
			}

			return null;
		}


		#region PreCombat Buffs

		[Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid)]
		public static Composite CreateDruidPreCombatBuff()
		{
			// Cast motw if player doesn't have it or if in instance/bg, out of combat and 'Buff raid with Motw' is true or if in instance and in combat and both CatRaidRebuff and 'Buff raid with Motw' are true
			return new PrioritySelector(

				PartyBuff.BuffGroup("Mark of the Wild", ret => !Me.HasAura("Prowl") && !Me.Combat),
				Spell.BuffSelf("Mark of the Wild", ret => !Me.HasAura("Prowl") && !Me.IsInGroup())
				);
		}

		#endregion

		[Behavior(BehaviorType.LossOfControl, WoWClass.Druid)]
		public static Composite CreateDruidLossOfControlBehavior()
		{
			return new Decorator(
				ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
				new PrioritySelector(
					new Sequence(
						Spell.BuffSelf("Barkskin"),
						new Action(r => Logger.Write(Color.LightCoral, "Loss of Control - BARKSKIN!!!!"))
						)
					)
				);
		}


		#region Combat Buffs

		[Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, DruidAllSpecs, WoWContext.Normal)]
		public static Composite CreateDruidCombatBuffsNormal()
		{
			return new PrioritySelector(
				new Decorator(
					req => !Me.CurrentTarget.IsTrivial(),
					new PrioritySelector(
						Spell.Cast("Barkskin", ctx => Me,
							ret => Me.HealthPercent < DruidSettings.Barkskin || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),
						Spell.Cast("Disorenting Roar", ctx => Me,
							ret => Me.HealthPercent < 40 || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),

						// will root only if can cast in form, or already left form for some other reason
						new PrioritySelector(
							ctx => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(
								u =>
									(Me.Shapeshift == ShapeshiftForm.Normal || Me.Shapeshift == ShapeshiftForm.Moonkin ||
									 Me.HasAura("Predatory Swiftness"))
									&& Me.CurrentTargetGuid != u.Guid
									&& u.SpellDistance() > 15
									&& u.IsMelee()
									&& !u.IsCrowdControlled()
									&& !u.HasAnyAura("Sunfire", "Moonfire")
								),
							Spell.Buff("Entangling Roots",
								on => (WoWUnit) on,
								req =>
								{
									if (Me.HasAura("Starfall") || !Spell.CanCastHack("Entangling Roots", (WoWUnit) req))
										return false;
									Logger.Write(LogColor.Hilite, "^Entangling Roots: root melee add at range");
									return true;
								})
							),

						// combat buffs - make sure we have target and in range and other checks
						// ... to avoid wasting cooldowns
						new Decorator(
							ret => Me.GotTarget()
							       && (Me.IsMelee() ? Me.CurrentTarget.IsWithinMeleeRange : Me.CurrentTarget.SpellDistance() < 40)
							       &&
							       (Me.CurrentTarget.IsPlayer ||
							        Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= (Me.Specialization == WoWSpec.DruidGuardian ? 4 : 3))
							       && Me.CurrentTarget.InLineOfSpellSight
							       && Me.IsSafelyFacing(Me.CurrentTarget),
							new PrioritySelector(
								Spell.BuffSelf("Celestial Alignment"),
								Spell.HandleOffGCD(Spell.Cast("Force of Nature",
									req => TalentManager.CurrentSpec != WoWSpec.DruidRestoration && Me.CurrentTarget.TimeToDeath() > 8)),
								Spell.BuffSelf("Incarnation: Chosen of Elune"),
								Spell.BuffSelf("Incarnation: King of the Jungle"),
								Spell.BuffSelf("Incarnation: Son of Ursoc"),
								Spell.BuffSelf("Nature's Vigil")
								)
							)
						)
					)
				);
		}

		[Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Battlegrounds)]
		[Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance,
			WoWContext.Instances | WoWContext.Battlegrounds)]
		[Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian,
			WoWContext.Instances | WoWContext.Battlegrounds)]
		[Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidRestoration,
			WoWContext.Instances | WoWContext.Battlegrounds)]
		public static Composite CreateDruidCombatBuffsInstance()
		{
			return new PrioritySelector(

				CreateRebirthBehavior(
					ctx =>
						Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead) ?? Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead)),

				CreateRootBreakShapeshift(),

				Spell.Cast("Barkskin", ctx => Me,
					ret => Me.HealthPercent < DruidSettings.Barkskin || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),
				Spell.Cast("Disorenting Roar", ctx => Me,
					ret => Me.HealthPercent < 40 || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),

				// combat buffs - make sure we have target and in range and other checks
				// ... to avoid wastine cooldowns
				new Decorator(
					ret => Me.GotTarget()
					       && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss())
					       &&
					       Me.SpellDistance(Me.CurrentTarget) <
					       ((TalentManager.CurrentSpec == WoWSpec.DruidFeral || TalentManager.CurrentSpec == WoWSpec.DruidGuardian)
						       ? 8
						       : 40)
					       && Me.CurrentTarget.InLineOfSight
					       && Me.IsSafelyFacing(Me.CurrentTarget),
					new PrioritySelector(
						Spell.BuffSelf("Celestial Alignment",
							ret => Spell.GetSpellCooldown("Celestial Alignment") == TimeSpan.Zero && PartyBuff.WeHaveBloodlust),
						new Sequence(
							Spell.HandleOffGCD(Spell.Cast("Force of Nature",
								req => TalentManager.CurrentSpec != WoWSpec.DruidRestoration && Me.CurrentTarget.TimeToDeath() > 8)),
							new ActionAlwaysFail()
							),
						// to do:  time ICoE at start of eclipse
						Spell.BuffSelf("Incarnation: Chosen of Elune"),
						Spell.BuffSelf("Nature's Vigil")
						)
					)
				);
		}

/*
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Instances, 1)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral , WoWContext.Instances, 1)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian , WoWContext.Instances, 1)]
        public static Composite CreateNonRestoDruidInstanceCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    new Decorator(
                        ret => HasTalent(DruidTalents.DreamOfCenarius) && !Me.HasAura("Dream of Cenarius"),
                        new PrioritySelector(
                            Spell.Heal("Healing Touch", ret => Me.ActiveAuras.ContainsKey("Predatory Swiftness")),
                            CreateNaturesSwiftnessHeal(on => GetBestHealTarget())
                            )
                        )
                    )
                );
        }
*/

		#endregion

		#region Heal

		[Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral)]
		[Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian)]
		[Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidBalance)]
		[Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidRestoration, WoWContext.Normal)]
		public static Composite CreateDpsDruidHealBehavior()
		{
			return new PrioritySelector(

				CreateRootBreakShapeshift(),

				#region Self-Heal: NON-COMBAT

				// self-healing Non-Combat
				new Decorator(
					ret => !Me.Combat
					       && (!Me.IsMoving || IsHealingTouchInstant() || Spell.HaveAllowMovingWhileCastingAura())
					       && Me.HealthPercent <= 85 // not redundant... this eliminates unnecessary GetPredicted... checks
					       && SpellManager.HasSpell("Healing Touch")
					       && Me.PredictedHealthPercent(includeMyHeals: true) < 85,
					new PrioritySelector(
						new Sequence(
							ctx => (float) Me.HealthPercent,
							new Action(
								r =>
									Logger.WriteDebug("Healing Touch: {0:F1}% Predict:{1:F1}% and moving:{2}, cancast:{3}", (float) r,
										Me.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving,
										Spell.CanCastHack("Healing Surge", Me, skipWowCheck: false))),
							Spell.Cast(
								"Healing Touch",
								mov => true,
								on => Me,
								req => true,
								cancel => Me.HealthPercent > 90
								),
							new WaitContinue(TimeSpan.FromMilliseconds(500),
								until => !Me.IsCasting && Me.HealthPercent > (1.1*((float) until)), new ActionAlwaysSucceed()),
							new Action(
								r =>
									Logger.WriteDebug("Healing Touch: After Heal Attempted: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent,
										Me.PredictedHealthPercent(includeMyHeals: true)))
							),
						new Action(
							r =>
								Logger.WriteDebug("Healing Touch: After Heal Skipped: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent,
									Me.PredictedHealthPercent(includeMyHeals: true)))
						)
					),

				#endregion

				#region Self-Heal: COMBAT

				// self-healing Combat
				new Decorator(
					req => Me.Combat && (SingularRoutine.CurrentWoWContext != WoWContext.Instances || !Group.AnyHealerNearby),
					new PrioritySelector(

						// defensive abilities and those we can cast in form
						Spell.BuffSelf("Renewal", req => Me.HealthPercent < DruidSettings.SelfRenewalHealth),
						Spell.BuffSelf("Cenarion Ward", req => Me.HealthPercent < DruidSettings.SelfCenarionWardHealth),
						Spell.Cast("Incapacitating Roar",
							ret =>
								Me.HealthPercent <= DruidSettings.DisorientingRoarHealth &&
								DruidSettings.DisorientingRoarCount <=
								Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro || (u.Combat && u.IsTargetingMeOrPet))),
						Spell.BuffSelf("Barkskin", ret => Me.HealthPercent < DruidSettings.Barkskin),

						// keep rejuv up 
						Spell.BuffSelf(
							"Rejuvenation",
							req =>
							{
								if (!Me.HasAuraExpired("Rejuvenation", 1))
									return false;

								if (Me.HealthPercent > DruidSettings.SelfRejuvInFormHealth ||
								    Me.PredictedHealthPercent(includeMyHeals: true) > DruidSettings.SelfRejuvInFormHealth)
									return false;

								// check whether we can cast without breaking form
								if (TalentManager.CurrentSpec == WoWSpec.DruidGuardian && Me.HasAura("Heart of the Wild"))
									return true;
								if (TalentManager.CurrentSpec == WoWSpec.DruidFeral && Me.HasAura("Enhanced Rejuvenation") &&
								    Me.HasAnyShapeshift(ShapeshiftForm.Bear, ShapeshiftForm.Cat))
									return true;
								if (ShapeshiftForm.Moonkin == Me.Shapeshift)
									return true;
								if (ShapeshiftForm.Normal == Me.Shapeshift)
								{
									Logger.WriteDiagnostic("Rejuvenation: allow since current Shapeshift={0}", Me.Shapeshift);
									return true;
								}

								// now check if worth casting while breaking form
								if (!Group.MeIsTank && Me.HealthPercent < DruidSettings.SelfRejuvenationHealth &&
								    Me.PredictedHealthPercent(includeMyHeals: true) < DruidSettings.SelfRejuvenationHealth)
									return true;

								return false;
							}),

						Spell.Cast(
							"Healing Touch",
							on =>
							{
								WoWUnit target = null;
								TimeSpan timeLeft = TimeSpan.Zero;

								if (StyxWoW.Me.Specialization == WoWSpec.DruidFeral)
									timeLeft = Me.GetAuraTimeLeft(PREDATORY_SWIFTNESS_PROC);
								else if (StyxWoW.Me.Specialization == WoWSpec.DruidGuardian)
									timeLeft = Me.GetAuraTimeLeft(DREAM_OF_CENARIUS_GUARDIAN_PROC);

								if (timeLeft > TimeSpan.Zero)
								{
									// pvp: heal as needed
									if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
										target =
											HealerManager.Instance.TargetList.FirstOrDefault(
												p =>
													p.IsAlive && p.PredictedHealthPercent() < DruidSettings.PredSwiftnessPvpHeal &&
													Spell.CanCastHack("Healing Touch", p));
									// pve: about to expire? heal anyone that needs it
									else if (timeLeft.TotalMilliseconds < 2000)
										target =
											HealerManager.Instance.TargetList.FirstOrDefault(
												p =>
													p.IsAlive && p.HealthPercent < DruidSettings.PredSwiftnessHealingTouchHealth &&
													Spell.CanCastHack("Healing Touch", p));
									// otherwise save until i need it
									else if (Me.HealthPercent < DruidSettings.PredSwiftnessHealingTouchHealth && Spell.CanCastHack("Healing Touch", Me))
										target = Me;

									if (target != null)
									{
										Logger.Write(LogColor.Hilite, "^Instant Heal: proc with {0:F1} secs used for {1} @ {2:F1}%",
											timeLeft.TotalSeconds, target.SafeName(), target.HealthPercent);
									}
								}
								return target;
							}),

						// for Balance or a lowbie Feral or a Bear not serving as Tank in a group
						new Decorator(
							ret =>
								Me.HealthPercent < DruidSettings.SelfHealingTouchHealth && !SpellManager.HasSpell("Predatory Swiftness") &&
								!Group.MeIsTank && SingularRoutine.CurrentWoWContext != WoWContext.Instances,
							new PrioritySelector(
								Spell.BuffSelf("Rejuvenation"),
								Spell.Cast(
									"Healing Touch",
									on => Me,
									req => true,
									cancel => Me.HealthPercent > Math.Max(90, DruidSettings.SelfHealingTouchHealth + 10)
									)
								)
							)
						)
					),

				#endregion

				#region Off-Healing

				CreateDpsDruidOffHealBehavior()

				#endregion

				);
		}

		private static bool IsHealingTouchInstant()
		{
			const int DREAM_OF_CENARIUS_GUARDIAN_PROC = 145162;

			SpellFindResults sfr;
			if (SpellManager.FindSpell("Healing Touch", out sfr))
				return (sfr.Override ?? sfr.Original).IsInstantCast();

			if (Me.GetAuraTimeLeft("Predatory Swiftness").TotalMilliseconds > 100)
				return true;
			if (Me.GetAuraTimeLeft(DREAM_OF_CENARIUS_GUARDIAN_PROC).TotalMilliseconds > 100)
				return true;

			return false;
		}

		#region DPS Off Heal

		private static WoWUnit _moveToHealUnit = null;

		public static Composite CreateDpsDruidOffHealBehavior()
		{
			if (!SingularSettings.Instance.DpsOffHealAllowed)
				return new ActionAlwaysFail();

			HealerManager.NeedHealTargeting = true;
			PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();
			int cancelHeal =
				(int) Math.Max(SingularSettings.Instance.IgnoreHealTargetsAboveHealth, DruidSettings.OffHealSettings.HealingTouch);

			bool moveInRange = (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds);

			Logger.WriteDebugInBehaviorCreate("Druid Healing: will cancel cast of direct heal if health reaches {0:F1}%",
				cancelHeal);


			#region Save the Group

			// Tank: Rebirth
			if (Helpers.Common.CombatRezTargetSetting != CombatRezTarget.None)
			{
				behavs.AddBehavior(HealerManager.HealthToPriority(100) + 400, "Rebirth Tank/Healer", "Rebirth",
					Helpers.Common.CreateCombatRezBehavior("Rebirth", filter => true, requirements => Me.Combat)
					);
			}

			if (DruidSettings.OffHealSettings.HeartOfTheWild != 0)
			{
				behavs.AddBehavior(HealerManager.HealthToPriority(100) + 300,
					"Heart of the Wild @ " + DruidSettings.OffHealSettings.HeartOfTheWild + "% MinCount: " +
					DruidSettings.OffHealSettings.CountHeartOfTheWild, "Heart of the Wild",
					new Decorator(
						ret => Me.Combat && StyxWoW.Me.IsInGroup(),
						Spell.BuffSelf(
							"Heart of the Wild",
							req => ((WoWUnit) req).HealthPercent < DruidSettings.OffHealSettings.HeartOfTheWild
							       && DruidSettings.OffHealSettings.CountHeartOfTheWild <= HealerManager.Instance.TargetList
								       .Count(
									       p =>
										       p.IsAlive && p.HealthPercent <= DruidSettings.OffHealSettings.HeartOfTheWild &&
										       p.Location.DistanceSqr(((WoWUnit) req).Location) <= 30*30)
							)
						)
					);
			}

			#endregion

			#region Tank Buffing

			// Tank: Rejuv if Lifebloom not trained yet
			if (DruidSettings.OffHealSettings.Rejuvenation != 0)
			{
				behavs.AddBehavior(HealerManager.HealthToPriority(100) + 200, "Rejuvenation - Tank", "Rejuvenation",
					Spell.Buff(
						"Rejuvenation",
						on =>
						{
							WoWUnit unit = Resto.GetBestTankTargetFor("Rejuvenation");
							if (unit != null && Spell.CanCastHack("Rejuvenation", unit, skipWowCheck: true))
							{
								Logger.WriteDebug("Buffing Rejuvenation ON TANK: {0}", unit.SafeName());
								return unit;
							}
							return null;
						},
						req => Me.Combat
						)
					);
			}

			if (DruidSettings.OffHealSettings.CenarionWard != 0)
			{
				if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
					behavs.AddBehavior(HealerManager.HealthToPriority(DruidSettings.OffHealSettings.CenarionWard) + 200,
						"Cenarion Ward @ " + DruidSettings.OffHealSettings.CenarionWard + "%", "Cenarion Ward",
						Spell.Buff("Cenarion Ward", on => (WoWUnit) on,
							req => Me.Combat && ((WoWUnit) req).HealthPercent < DruidSettings.OffHealSettings.CenarionWard)
						);
				else
					behavs.AddBehavior(HealerManager.HealthToPriority(99) + 200, "Cenarion Ward - Tanks", "Cenarion Ward",
						Spell.Buff("Cenarion Ward", on => Resto.GetLifebloomTarget(), req => Me.Combat)
						);
			}

			if (DruidSettings.OffHealSettings.NaturesVigil != 0)
			{
				if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
					behavs.AddBehavior(HealerManager.HealthToPriority(DruidSettings.OffHealSettings.NaturesVigil) + 200,
						"Nature's Vigil @ " + DruidSettings.OffHealSettings.NaturesVigil + "%", "Nature's Vigil",
						Spell.Buff("Nature's Vigil", on => (WoWUnit) on,
							req => Me.Combat && ((WoWUnit) req).HealthPercent < DruidSettings.OffHealSettings.NaturesVigil)
						);
				else
					behavs.AddBehavior(HealerManager.HealthToPriority(DruidSettings.OffHealSettings.NaturesVigil) + 200,
						"Nature's Vigil - Tank @ " + DruidSettings.OffHealSettings.NaturesVigil + "%", "Nature's Vigil",
						Spell.Buff("Nature's Vigil",
							on =>
								Group.Tanks.FirstOrDefault(
									u => u.IsAlive && u.HealthPercent < DruidSettings.OffHealSettings.NaturesVigil && !u.HasAura("Nature's Vigil")),
							req => Me.Combat)
						);
			}

			#endregion

			#region Direct Heals

			behavs.AddBehavior(HealerManager.HealthToPriority(DruidSettings.OffHealSettings.Rejuvenation),
				"Rejuvenation @ " + DruidSettings.OffHealSettings.Rejuvenation + "%", "Rejuvenation",
				new PrioritySelector(
					Spell.Buff("Rejuvenation",
						1,
						on => (WoWUnit) on,
						req => ((WoWUnit) req).HealthPercent < DruidSettings.OffHealSettings.Rejuvenation
						)
					)
				);

			bool healingTouchKnown = SpellManager.HasSpell("Healing Touch");

			if (DruidSettings.OffHealSettings.HealingTouch != 0)
			{
				behavs.AddBehavior(HealerManager.HealthToPriority(DruidSettings.OffHealSettings.HealingTouch),
					"Healing Touch @ " + DruidSettings.OffHealSettings.HealingTouch + "%", "Healing Touch",
					new PrioritySelector(
						Spell.Cast("Healing Touch",
							mov => true,
							on => (WoWUnit) on,
							req => ((WoWUnit) req).HealthPercent < DruidSettings.OffHealSettings.HealingTouch,
							cancel => ((WoWUnit) cancel).HealthPercent > cancelHeal
							)
						)
					);
			}

			#endregion

			#region Lowest Priority Healer Tasks

			behavs.AddBehavior(3, "Rejuvenation while Moving @ " + SingularSettings.Instance.IgnoreHealTargetsAboveHealth + "%",
				"Rejuvenation",
				new Decorator(
					req => Me.IsMoving,
					Spell.Buff("Rejuvenation",
						on =>
							HealerManager.Instance.TargetList.FirstOrDefault(
								h =>
									h.IsAlive && h.HealthPercent < SingularSettings.Instance.IgnoreHealTargetsAboveHealth &&
									!h.HasMyAura("Rejuvenation") && Spell.CanCastHack("Rejuvenation", h, true)),
						req => Me.Combat
						)
					)
				);

			#endregion


			behavs.OrderBehaviors();

			if (Singular.Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Heal)
				behavs.ListBehaviors();

			return new Decorator(
				ret => HealerManager.ActingAsOffHealer,
				new PrioritySelector(
					ctx => HealerManager.FindLowestHealthTarget(), // HealerManager.Instance.FirstUnit,
					behavs.GenerateBehaviorTree(),
					new Decorator(
						ret => moveInRange,
						new Sequence(
							new Action(r => _moveToHealUnit = (WoWUnit) r),
							new PrioritySelector(
								Movement.CreateMoveToLosBehavior(on => _moveToHealUnit),
								Movement.CreateMoveToUnitBehavior(on => _moveToHealUnit, 30f, 25f)
								)
							)
						)
					)
				);
		}

		#endregion

		public static Composite CreateNaturesSwiftnessHeal(SimpleBooleanDelegate requirements = null)
		{
			return CreateNaturesSwiftnessHeal(on => Me, requirements);
		}

		public static Composite CreateNaturesSwiftnessHeal(UnitSelectionDelegate onUnit,
			SimpleBooleanDelegate requirements = null)
		{
			return new Decorator(
				ret => onUnit != null && onUnit(ret) != null && requirements != null && requirements(ret),
				new Sequence(
					Spell.BuffSelf("Nature's Swiftness"),
					new Wait(TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Nature's Swiftness"), new ActionAlwaysSucceed()),
					Spell.Cast("Healing Touch", ret => false, onUnit, req => true)
					)
				);
		}

		public static WoWUnit GetBestHealTarget()
		{
			if (SingularRoutine.CurrentWoWContext == WoWContext.Normal || Me.HealthPercent < 40)
				return Me;

			return Unit.NearbyFriendlyPlayers.Where(p => p.IsAlive).OrderBy(k => k.PredictedHealthPercent()).FirstOrDefault();
		}

		#endregion

		#region Rest

		[Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidBalance)]
		[Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidFeral)]
		[Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidGuardian)]
		public static Composite CreateNonRestoDruidRest()
		{
			return new PrioritySelector(
/*
                new Decorator(
                    ret => !Me.HasAnyAura("Drink", "Food", "Refreshment")
                        && Me.PredictedHealthPercent(includeMyHeals: true) < (Me.Shapeshift == ShapeshiftForm.Normal ? 85 : SingularSettings.Instance.MinHealth)
                        && ((Me.HasAuraExpired("Rejuvenation", 1) && Spell.CanCastHack("Rejuvenation", Me)) || Spell.CanCastHack("Healing Touch", Me)),
                    new PrioritySelector(
                        Movement.CreateEnsureMovementStoppedBehavior( reason:"to heal"),
                        new Action(r => { Logger.WriteDebug("Rest Heal @ actual:{0:F1}% predict:{1:F1}% and moving:{2} in form:{3}", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving, Me.Shapeshift ); return RunStatus.Failure; }),
                        Spell.BuffSelf("Rejuvenation", req => !SpellManager.HasSpell("Healing Touch")),
                        Spell.Cast(
                            "Healing Touch",
                            mov => true,
                            on => Me,
                            req => true,
                            cancel => Me.HealthPercent > 92
                            )
                        )
                    ),
*/
				new Decorator(
					ret => !Rest.IsEatingOrDrinking,
					Common.CreateDpsDruidOffHealBehavior()
					),

				Rest.CreateDefaultRestBehaviour(null, "Revive"),
				CreateDruidMovementBuff()
				);
		}

		#endregion

		public static Composite CastHurricaneBehavior(UnitSelectionDelegate onUnit, SimpleBooleanDelegate cancel = null)
		{
			if (cancel == null)
			{
				cancel = u =>
				{
					if (Me.HealthPercent < 30)
					{
						Logger.Write(LogColor.Cancel, "/cancel Hurricane since my health at {0:F1}%", Me.HealthPercent);
						return true;
					}
					return false;
				};
			}

			return new Sequence(
				ctx => onUnit(ctx),

				Spell.CastOnGround("Hurricane", on => (WoWUnit) on,
					req => Me.HealthPercent > 40 && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3, false),

				new Wait(
					TimeSpan.FromMilliseconds(1000),
					until => Spell.IsCastingOrChannelling() && Unit.NearbyUnfriendlyUnits.Any(u => u.HasMyAura("Hurricane")),
					new ActionAlwaysSucceed()
					),
				new Wait(
					TimeSpan.FromSeconds(10),
					until =>
					{
						if (!Spell.IsCastingOrChannelling())
						{
							Logger.WriteDiagnostic("Hurricane: cast complete or interrupted");
							return true;
						}
						int cnt = Unit.NearbyUnfriendlyUnits.Count(u => u.HasMyAura("Hurricane"));
						if (cnt < 3)
						{
							Logger.Write(LogColor.Cancel, "/cancel Hurricane since only {0} targets effected", cnt);
							return true;
						}
						if (cancel(until))
						{
							// message should be output by cancel delegate
							SpellManager.StopCasting();
							return true;
						}

						return false;
					},
					new ActionAlwaysSucceed()
					),
				new DecoratorContinue(
					req => Spell.IsChannelling(),
					new Action(r => SpellManager.StopCasting())
					),
				new WaitContinue(
					TimeSpan.FromMilliseconds(500),
					until => !Spell.IsChannelling(),
					new ActionAlwaysSucceed()
					)
				)
				;
		}


		internal static Composite CreateProwlBehavior(SimpleBooleanDelegate requirements = null)
		{
			if (DruidSettings.Prowl == ProwlMode.Never)
				return new ActionAlwaysFail();

			requirements = requirements ?? (req => true);

			BehaviorType createdByBehavior = Dynamics.CompositeBuilder.CurrentBehaviorType;
			SimpleBooleanDelegate needProwl =
				req =>
				{
					bool isProwlAllowed = false;
					if (DruidSettings.Prowl == ProwlMode.Always)
						isProwlAllowed = true;
					else if (DruidSettings.Prowl == ProwlMode.Auto &&
					         (createdByBehavior == BehaviorType.Pull || createdByBehavior == BehaviorType.PullBuffs))
						isProwlAllowed = true;
					else if (DruidSettings.Prowl == ProwlMode.PVP)
					{
						if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
							isProwlAllowed = true;
						else if (StyxWoW.Me.GotTarget() && StyxWoW.Me.CurrentTarget.IsPlayer && Unit.ValidUnit(StyxWoW.Me.CurrentTarget))
							isProwlAllowed = true;
						else if (BotPoi.Current.Type == PoiType.Kill && BotPoi.Current.AsObject is WoWPlayer)
							isProwlAllowed = true;
					}

					if (isProwlAllowed)
					{
						if (!Me.Combat && requirements(req) && !Me.HasAura("Prowl") && !Me.GetAllAuras().Any(a => a.IsHarmful))
						{
							return true;
						}
					}
					return false;
				};

			string createdBy = createdByBehavior.ToString() + " " + Dynamics.CompositeBuilder.CurrentBehaviorName;
			return new Sequence(
				ctx => needProwl(ctx),

				//// no Prowl? then throttle message
				//new DecoratorContinue(
				//    req => ! (bool) req && !Me.Mounted && !AreProwlAbilitiesAvailable,
				//    new SeqDbg( 1, s => string.Format("CreateProwlBehavior: need = {0}, called by {1}", ((bool)s).ToYN(), createdBy))
				//    ),

				Spell.BuffSelf(
					"Prowl",
					req =>
					{
						bool need = (bool) req;
						if (!need)
							return false;

						// yes Prowl? message throttled by virtue of buff logic
						Logger.WriteDebug("CreateProwlBehavior: need = {0}, called by {1}", need.ToYN(), createdBy);
						if (!Spell.CanCastHack("Prowl"))
							return false;
						return true;
					}),
				// now wait until we can Sap, Pick Pocket, etc...
				new Wait(TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Prowl"), new ActionAlwaysSucceed())
				);
		}

		public static Composite CreateRebirthBehavior(UnitSelectionDelegate onUnit)
		{
			if (TalentManager.CurrentSpec == WoWSpec.DruidGuardian)
				return Helpers.Common.CreateCombatRezBehavior("Rebirth",
					on => ((WoWUnit) on).SpellDistance() < 40 && ((WoWUnit) on).InLineOfSpellSight, requirements => true);

			return Helpers.Common.CreateCombatRezBehavior("Rebirth", filter => true,
				reqd => !Me.HasAnyAura("Nature's Swiftness", "Predatory Swiftness"));
		}

		public static Composite CreateFaerieFireBehavior(UnitSelectionDelegate onUnit = null,
			SimpleBooleanDelegate Required = null)
		{
			if (onUnit == null)
				onUnit = on => Me.CurrentTarget;

			if (Required == null)
				Required = req => true;

			// Fairie Fire has a 1.5 sec GCD, Faerie Swarm 0.0.  Handle both here
			return new ThrottlePasses(1, TimeSpan.FromMilliseconds(500),
				new Sequence(
					new PrioritySelector(
						Spell.Buff("Faerie Swarm", on => onUnit(on), ret => Required(ret)),
						Spell.Buff("Faerie Fire", on => onUnit(on), ret => Required(ret))
						)
					)
				);
		}

		private static bool IsBotPoiWithinMovementBuffRange()
		{
			int minDistKillPoi = 10;
			int minDistOtherPoi = 10;
			int maxDist = Styx.Helpers.CharacterSettings.Instance.MountDistance;

			if (Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull ||
			    Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.PullBuffs)
				maxDist = Math.Max(100, Styx.Helpers.CharacterSettings.Instance.MountDistance);

			if (!Me.IsMelee())
				minDistKillPoi += 40;

			if (BotPoi.Current == null || BotPoi.Current.Type == PoiType.None)
				return false;

			double dist = -1;
			if (BotPoi.Current.Type != PoiType.Kill || BotPoi.Current.AsObject == null || !(BotPoi.Current.AsObject is WoWUnit))
			{
				dist = Me.Location.Distance(BotPoi.Current.Location);
				if (dist < minDistOtherPoi)
					return false;
			}
			else
			{
				WoWUnit unit = BotPoi.Current.AsObject.ToUnit();
				if (unit.SpellDistance() < minDistKillPoi)
					return false;
			}

			// always speedbuff if indoors and cannot mount
			if (Me.IsIndoors && !Mount.CanMount())
				return true;

			// always speedbuff if riding not trained yet
			if (Me.GetSkill(SkillLine.Riding).CurrentValue == 0)
				return true;

			// calc distance if we havent already
			if (dist == -1)
				dist = Me.Location.Distance(BotPoi.Current.Location);

			// speedbuff if dist within maxdist
			if (dist <= maxDist)
				return true;

			// otherwise no speedbuff wanted
			return false;
		}

		public static Composite CreateDruidMovementBuff()
		{

			return new Throttle(5,
				new Decorator(
					req => !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown()
					       && MovementManager.IsClassMovementAllowed
					       && SingularRoutine.CurrentWoWContext != WoWContext.Instances
					       && Me.IsMoving
					       && Me.IsAlive
					       && !Me.OnTaxi
					       && !Me.InVehicle
					       && !Me.IsOnTransport
					       && !Utilities.EventHandlers.IsShapeshiftSuppressed
					       && BotPoi.Current != null
					       && BotPoi.Current.Type != PoiType.None
					       && BotPoi.Current.Type != PoiType.Hotspot
					       && !Me.IsAboveTheGround()
					,
					new Sequence(
						new PrioritySelector(
							new Decorator(
								ret => DruidSettings.UseTravelForm
								       && !Me.Mounted
								       && !Me.IsSwimming
								       && !Me.HasAnyShapeshift(ShapeshiftForm.Travel, ShapeshiftForm.FlightForm, ShapeshiftForm.EpicFlightForm)
								       && !Me.HasAura("Darkflight")
								       && SpellManager.HasSpell("Cat Form")
								       && IsBotPoiWithinMovementBuffRange(),
								new Sequence(
/*
                                    new Action(r => 
                                        Logger.WriteDebug("DruidMoveBuff: poitype={0} poidist={1:F1} indoors={2} canmount={3} riding={4} form={5}",
                                            BotPoi.Current.Type,
                                            BotPoi.Current.Location.Distance(Me.Location),
                                            Me.IsIndoors.ToYN(),
                                            Mount.CanMount().ToYN(),
                                            Me.GetSkill(SkillLine.Riding).CurrentValue,
                                            Me.Shapeshift.ToString()
                                        )),
 */ 
									new PrioritySelector(
										Common.CastForm(ShapeshiftForm.Travel,
											req =>
											{
												if (!Me.IsOutdoors || BotPoi.Current.Type == PoiType.Kill)
													return false;
												WoWUnit possibleAggro =
													Unit.UnfriendlyUnits(40).FirstOrDefault(u => u.IsHostile && (!u.Combat || u.CurrentTargetGuid != Me.Guid));
												if (possibleAggro != null && !Me.IsInsideSanctuary)
												{
													Logger.WriteDiagnostic("DruidMoveBuff: suppressing Travel Form since hostile {0} is {1:F1} yds away",
														possibleAggro.SafeName(), possibleAggro.SpellDistance());
													return false;
												}
												return true;
											}),
										Common.CastForm(ShapeshiftForm.Cat,
											req => !Me.IsOutdoors
											       || (Me.Specialization != WoWSpec.DruidBalance && Me.Specialization != WoWSpec.DruidRestoration)
											       || SingularRoutine.CurrentWoWContext == WoWContext.Instances
											)
										)
									)
								),
							new Decorator(
								req => AllowAquaticForm
								       && BotPoi.Current.Location.Distance(Me.Location) >= 10,
								Common.CastForm(ShapeshiftForm.Aqua)
								)
							),

						Helpers.Common.CreateWaitForLagDuration()
						)
					)
				);
		}

		public static bool AllowAquaticForm
		{
			get
			{
				const int ABYSSAL_SEAHORSE = 75207;
				const int SUBDUED_SEAHORSE = 98718;
				const int SEA_TURTLE = 64731;

				if (!DruidSettings.UseAquaticForm)
					return false;

				if (!Me.IsSwimming)
					return false;

				if (Me.Shapeshift != ShapeshiftForm.Aqua)
				{
					if (Me.Combat)
						return false;

					if (!SpellManager.HasSpell("Travel Form"))
						return false;

					MirrorTimerInfo breath = StyxWoW.Me.GetMirrorTimerInfo(MirrorTimerType.Breath);
					if (!breath.IsVisible)
					{
						if (Me.Mounted &&
						    (Me.MountDisplayId == ABYSSAL_SEAHORSE || Me.MountDisplayId == SUBDUED_SEAHORSE ||
						     Me.MountDisplayId == SEA_TURTLE))
							return false;
					}

					if (!Spell.CanCastHack("Travel Form", Me))
						return false;

					Logger.WriteDebug("DruidSwimBuff: breath={0} canmount={1} mounted={2} mountdispid={3}",
						breath.IsVisible.ToYN(),
						Mount.CanMount().ToYN(),
						Me.Mounted.ToYN(),
						Me.MountDisplayId
						);
				}

				return true;
			}
		}

		public static Composite CreateMoveBehindTargetWhileProwling()
		{
			return new Decorator(
				req => MovementManager.IsMoveBehindAllowed
				       && DruidSettings.MoveBehindTargets
				       && Me.HasAura("Prowl"),
				Movement.CreateMoveBehindTargetBehavior()
				);
		}

		public static Composite CreateRootBreakShapeshift()
		{
			return new Decorator(
				req => Me.IsRooted()
				       && !Me.Stunned
				       && !Me.HasAnyShapeshift(ShapeshiftForm.Travel, ShapeshiftForm.FlightForm, ShapeshiftForm.EpicFlightForm)
				       && SpellManager.HasSpell("Cat Form") && SpellManager.HasSpell("Bear Form")
				       && (Me.Specialization != WoWSpec.DruidGuardian || !Me.IsInInstance),
				new ThrottlePasses(
					1, TimeSpan.FromSeconds(1), RunStatus.Failure,
					new Sequence(
						new Action(r =>
						{
							string spellName = Me.Shapeshift != ShapeshiftForm.Bear ? "Bear Form" : "Cat Form";
							if (!Spell.CanCastHack(spellName, Me))
								return RunStatus.Failure;
							return RunStatus.Success;
						}),
						new SeqLog(1, LogColor.Hilite, s => string.Format("^Rooted: attempting root-break shapeshift")),
						new PrioritySelector(
							CastForm(ShapeshiftForm.Bear, req => Me.Shapeshift != ShapeshiftForm.Bear),
							CastForm(ShapeshiftForm.Cat, req => Me.Shapeshift == ShapeshiftForm.Bear)
							),
						new Wait(TimeSpan.FromMilliseconds(500), until => !Me.IsRooted(), new ActionAlwaysSucceed())
						)
					)
				);
		}



		/// <summary>
		/// creates a Druid specific avoidance behavior based upon settings.  will check for safe landing
		/// zones before using WildCharge or rocket jump.  will additionally do a running away or jump turn
		/// attack while moving away from attacking mob if behaviors provided
		/// </summary>
		/// <param name="nonfacingAttack">behavior while running away (back to target - instants only)</param>
		/// <param name="jumpturnAttack">behavior while facing target during jump turn (instants only)</param>
		/// <returns></returns>
		public static Composite CreateDruidAvoidanceBehavior(Composite slowAttack, Composite nonfacingAttack,
			Composite jumpturnAttack)
		{
			if (Me.Specialization == WoWSpec.DruidBalance)
				return Avoidance.CreateAvoidanceBehavior("Wild Charge", 20, Disengage.Direction.Backwards,
					slowAttack ?? new ActionAlwaysSucceed());

			if (Me.Specialization == WoWSpec.DruidRestoration)
				return Avoidance.CreateAvoidanceBehavior(null, 0, Disengage.Direction.None, slowAttack ?? new ActionAlwaysSucceed());

			return new ActionAlwaysFail();
		}


		private static WoWGuid _CrowdControlGuid;
		private static WoWUnit _CrowdControlTarget;

		/// <summary>
		/// Crowd Control all targets within 25 yds that are attacking (if possible.)
		/// if not, attempt to Kite
		/// </summary>
		/// <returns></returns>
		public static Composite CreateDruidCrowdControl()
		{
			return new PrioritySelector(

				#region Crowd Control to Self-Heal

				// Incapacitating Roar - 10 yds, all 3 secs
				// Mighty Bash - Melee, 5 secs
				// Cyclone - 20 yds, 6 secds

				new Action(r =>
				{
					_CrowdControlTarget = null;
					if (_CrowdControlGuid != WoWGuid.Empty)
					{
						_CrowdControlTarget = ObjectManager.GetObjectByGuid<WoWUnit>(_CrowdControlGuid);
						if (_CrowdControlTarget != null)
						{
							if (Spell.DoubleCastContainsAny(_CrowdControlTarget, "Incapacitating Roar", "Mighty Bash", "Cyclone"))
							{
							}
							else if (_CrowdControlTarget.IsCrowdControlled())
							{
							}
							else if (_CrowdControlTarget.IsMelee() && _CrowdControlTarget.SpellDistance() > 25)
							{
								_CrowdControlGuid = WoWGuid.Empty;
								_CrowdControlTarget = null;
							}
						}
					}

					_CrowdControlTarget = Unit.UnitsInCombatWithUsOrOurStuff(25)
						.Where(u => u.CurrentTargetGuid == Me.Guid
						            && u.Guid != _CrowdControlGuid
						            && u.Combat && !u.IsCrowdControlled())
						.OrderByDescending(k => k.IsPlayer)
						.ThenBy(k => k.Guid == Me.CurrentTargetGuid)
						.ThenBy(k => k.DistanceSqr)
						.FirstOrDefault();

					_CrowdControlGuid = _CrowdControlTarget == null ? WoWGuid.Empty : _CrowdControlTarget.Guid;
					return RunStatus.Failure;
				}),

				new Decorator(
					ret => _CrowdControlTarget != null,
					new PrioritySelector(
						Spell.Buff("Incapacitating Roar", on => _CrowdControlTarget,
							req => Unit.UnfriendlyUnits(10).Count(u => u.CurrentTargetGuid == Me.Guid) > 1),
						Spell.Buff("Mighty Bash", on => _CrowdControlTarget, req => Me.IsSafelyFacing(_CrowdControlTarget)),
						new Sequence(
							Spell.Cast("Cyclone", on => _CrowdControlTarget,
								req => !Unit.NearbyUnfriendlyUnits.Any(u => u.HasMyAura("Cyclone")), cancel => false),
							new Action(r => Spell.UpdateDoubleCast("Cyclone", _CrowdControlTarget))
							),
						Spell.Buff("Disorienting Roar", on => _CrowdControlTarget,
							req => Unit.UnfriendlyUnits(10).Count(u => u.CurrentTargetGuid == Me.Guid) > 0)
						)
					),

				#endregion

				#region Avoidance

				// attackers within 8 yds and we need heal? try to knock them away
				Spell.Cast(
					"Typhoon",
					req =>
					{
						if (!Spell.CanCastHack("Typhoon"))
							return false;
						int attackers = Unit
							.UnfriendlyUnits(15)
							.Count(u => u.CurrentTargetGuid == Me.Guid
							            && u.Combat
							            && !u.IsCrowdControlled()
							            && (u.IsMelee() || u.CastingSpellId == 1949 /*Hellfire*/|| u.HasAura("Immolation Aura"))
							            && Me.IsSafelyFacing(u, 90f));
						if (attackers < 1)
							return false;
						Logger.Write(LogColor.Hilite, "^Typhoon: knock-back and daze {0} attackers", attackers);
						return true;
					}),

				// no knock back? lets root and move away if settings allow
				new Decorator(
					ret => Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any(u => u.SpellDistance() < 8),
					CreateDruidAvoidanceBehavior(CreateDruidSlowMeleeBehavior(), null, null)
					)

				#endregion

				);

		}

		public static Composite CreateDruidSlowMeleeBehavior()
		{
			return new PrioritySelector(
				ctx => SafeArea.NearestEnemyMobAttackingMe,
				new Decorator(
					ret => ret != null,
					new PrioritySelector(
						new Throttle(2,
							new PrioritySelector(
								new Decorator(
									req => (req as WoWUnit).IsCrowdControlled(),
									new Action(r => Logger.WriteDebug("SlowMelee: closest mob already crowd controlled"))
									),
								Spell.CastOnGround("Ursol's Vortex", on => (WoWUnit) on, req => Me.GotTarget(), false),
								Spell.Buff("Disorienting Roar", onUnit => (WoWUnit) onUnit, req => true),
								Spell.Buff("Mass Entanglement", onUnit => (WoWUnit) onUnit, req => true),
								Spell.Buff("Mighty Bash", onUnit => (WoWUnit) onUnit, req => true),
								new Throttle(1, Spell.Buff("Faerie Swarm", onUnit => (WoWUnit) onUnit, req => true)),
								new Throttle(2,
									Spell.Buff("Entangling Roots", false, on => (WoWUnit) on,
										req => Unit.NearbyUnitsInCombatWithUsOrOurStuff.Any(u => u.Guid != (req as WoWUnit).Guid))),
								new Sequence(
									Spell.Cast("Typhoon", mov => false, on => (WoWUnit) on,
										req => (req as WoWUnit).SpellDistance() < 28 && Me.IsSafelyFacing((WoWUnit) req, 60)),
									new WaitContinue(TimeSpan.FromMilliseconds(500), until => (until as WoWUnit).SpellDistance() > 30,
										new ActionAlwaysSucceed()),
									new ActionAlwaysFail()
									)
								/*
                                                new Sequence(                                   
                                                    Spell.CastOnGround("Wild Mushroom",
                                                        on => (WoWUnit) on,
                                                        req => req != null && !Spell.IsSpellOnCooldown("Wild Mushroom: Detonate")
                                                        ),
                                                    new Action( r => Logger.WriteDebug( "SlowMelee: waiting for Mushroom to appear")),
                                                    new WaitContinue( TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling() && MushroomCount > 0, new ActionAlwaysSucceed()),
                                                    new Action(r => Logger.WriteDebug("SlowMelee: found {0} mushrooms", MushroomCount)),
                                                    Spell.Cast("Wild Mushroom: Detonate")
                                                    )
                */
								)
							)
						)
					)
				);
		}


		public static Composite CastForm(ShapeshiftForm shape, SimpleBooleanDelegate requirements = null)
		{
			string spellName = string.Empty;
			int spellId = 0;
			switch (shape)
			{
				case ShapeshiftForm.Cat: //  1,
					spellName = "Cat Form";
					break;
				case ShapeshiftForm.Travel: //  3,
					spellName = "Travel Form";
					break;
				case ShapeshiftForm.Aqua: //  4,
					spellName = "Travel Form";
					break;
				case ShapeshiftForm.Bear: //  5,
					spellName = "Bear Form";
					break;
				case ShapeshiftForm.Moonkin: //  31,
					spellName = "Moonkin Form";
					break;

				default:
					Logger.Write(LogColor.Diagnostic, "Programming Error: shape shift {0} not supported in Singular Druid");
					return new ActionAlwaysFail();
			}

			SpellFindResults sfr;
			if (!SpellManager.FindSpell(spellName, out sfr))
			{
				Logger.WriteDiagnostic("CastForm: disabled for [{0}], spell not learned yet", spellName);
				return new ActionAlwaysFail();
			}

			spellId = sfr.Original.Id;
			if (sfr.Override != null)
			{
				Logger.WriteDiagnostic("CastForm: using [{0}] as override for [{1}] spell and aura names", sfr.Override.Name,
					spellName);
				spellName = sfr.Override.Name;
				spellId = sfr.Override.Id;
			}

			return new Decorator(
				req => Me.Shapeshift != shape && !Me.HasShapeshiftAura(spellName) && (requirements == null || requirements(req)),
				new Sequence(
					new Action(
						r =>
							Logger.WriteDiagnostic("CastForm: changing to form='{0}', current='{1}', using spell '{2}'", shape, Me.Shapeshift,
								spellName)),
					Spell.BuffSelfAndWait(spellId, requirements)
					)
				);
		}

		public static Composite CreateAttackFlyingOrUnreachableMobs()
		{
			return new Decorator(
				ret =>
				{
					if (!Me.GotTarget())
						return false;

					return Me.CurrentTarget.IsFlyingOrUnreachableMob();
				},
				new Decorator(
					req => !Me.CurrentTarget.IsWithinMeleeRange,
					new Sequence(
						new PrioritySelector(
							Spell.Cast("Growl"),
							Spell.Buff("Moonfire",
								req => Me.Specialization == WoWSpec.DruidFeral && HasTalent(DruidTalents.LunarInspiration)),
							CreateFaerieFireBehavior(),
							Spell.Buff("Moonfire"),
							new Sequence(
								new PrioritySelector(
									Movement.CreateEnsureMovementStoppedBehavior(27f, on => Me.CurrentTarget, reason: "To cast Wrath"),
									new ActionAlwaysSucceed()
									),
								new Wait(1, until => !Me.IsMoving, new ActionAlwaysSucceed()),
								Spell.Cast("Wrath")
								)
							),
						// otherwise cant reach and 
						new Action(r =>
						{
							if (Me.CurrentTarget.TimeToDeath(99) < 40 && Movement.InLineOfSpellSight(Me.CurrentTarget, 5000))
							{
								SingularRoutine.TargetTimeoutTimer.Reset();
							}
						})
						)
					)
				);
		}
	}

	public enum DruidTalents
	{
		ForceOfNature = 1,
		WarriorOfElune,
		Starlord,

		Predator = ForceOfNature,
		BloodScent = WarriorOfElune,
		LunarInspiration = Starlord,

		Brambles = ForceOfNature,
		BristlingFur = WarriorOfElune,
		BloodFrenzy = Starlord,

		Prosperity = ForceOfNature,
		CenarionWard = WarriorOfElune,
		Abundance = Starlord,

		Renewal = 4,
		DisplacerBeast,
		WildCharge,

		GutturalRoars = Renewal,

		FeralAffinityBalance = 7,
		GuardianAffinity,
		RestorationAffinity,

		BalanceAffinity = FeralAffinityBalance,
		FeralAffinityGuardian = GuardianAffinity,
		FeralAffinityRestoration = GuardianAffinity,

		MightyBash = 10,
		MassEntanglement,
		Typhoon,

		SoulOfTheForest = 13,
		IncarnationChosenOfElune,
		StellarFlare,
		
		IncarnationKingOfTheJungle = IncarnationChosenOfElune,
		SavageRoar = StellarFlare,
		
		IncarnationGuardianOfUrsoc = IncarnationChosenOfElune,
		GalacticGuardian = StellarFlare,

		IncarnationTreeOfLife = IncarnationChosenOfElune,
		Cultivation = StellarFlare,

		ShootingStars = 16,
		AstralCommunion,
		BlessingOfTheAncients,

		SaberTooth = ShootingStars,
		JaggedWounds = AstralCommunion,
		ElunesGuidance = BlessingOfTheAncients,

		Earthwarden = ShootingStars,
		GuardianOfElune = AstralCommunion,
		SurvivalOfTheFittest = BlessingOfTheAncients,

		SpringBlossoms = ShootingStars,
		InnerPeace = AstralCommunion,
		Germination = BlessingOfTheAncients,

		FuryOfElune = 19,
		StellarDrift,
		NaturesBalance,

		BrutalSlash = FuryOfElune,
		Bloodtalons = StellarDrift,
		MomentOfClarityFeral = NaturesBalance,

		RendAndTear = FuryOfElune,
		LunarBeam = StellarDrift,
		Pulverize = NaturesBalance,

		MomentOfClarityRestoration = FuryOfElune,
		Stonebark = StellarDrift,
		Flourish = NaturesBalance,
	}
}