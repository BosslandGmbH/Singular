using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

using Singular.Settings;
using Styx.WoWInternals;
using Styx.Common.Helpers;

namespace Singular.ClassSpecific.Monk
{
    public class Windwalker
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MonkSettings MonkSettings { get { return SingularSettings.Instance.Monk(); } }
        public static bool HasTalent(MonkTalents tal) { return TalentManager.IsSelected((int)tal); }

        // delay casting instant ranged abilities if we just cast Roll/FSK
        private readonly static WaitTimer RollTimer = new WaitTimer(TimeSpan.FromMilliseconds(1500));

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Normal )]
        public static Composite CreateWindwalkerMonkPullNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(FaceDuring.Yes),

                // close distance if at range
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

#if OLD_ROLL_LOGIC
                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed && !MonkSettings.DisableRoll && !Me.CurrentTarget.IsAboveTheGround() && Me.CurrentTarget.SpellDistance() > 10,
                            new Throttle( 1,
                                new Sequence(
                                    new PrioritySelector(
                                        Spell.Cast("Flying Serpent Kick", ret => TalentManager.HasGlyph("Flying Serpent Kick")),
                                        Spell.Cast("Roll", ret =>  !Me.HasAura("Flying Serpent Kick"))
                                        ),
                                    new Action( r => RollTimer.Reset() )
                                    )
                                )
                            ),
#else
                        Common.CreateMonkCloseDistanceBehavior( ),
#endif
                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        // Spell.Cast(sp => "Crackling Jade Lightning", mov => true, on => Me.CurrentTarget, req => !Me.CurrentTarget.IsWithinMeleeRange && Me.CurrentTarget.SpellDistance() < 40, cancel => false),
                        Spell.Cast("Provoke", ret => !Me.CurrentTarget.IsPlayer && !Me.CurrentTarget.Combat && Me.CurrentTarget.SpellDistance().Between(20, 40)),

                        Spell.Cast("Blackout Kick", ret => Me.CurrentChi == Me.MaxChi || Me.HasAura("Combo Breaker: Blackout Kick")),
                        Spell.Cast("Tiger Palm", ret => (Me.CurrentChi > 0 && Me.HasKnownAuraExpired( "Tiger Power")) || Me.HasAura("Combo Breaker: Tiger Palm")),
                        Spell.Cast( "Expel Harm", on => Common.BestExpelHarmTarget(), ret => Me.CurrentChi < (Me.MaxChi-2) && Me.HealthPercent < 80 && Me.CurrentTarget.Distance < 10 ),
                        Spell.Cast("Jab", ret => Me.CurrentChi < Me.MaxChi)
                        )
                    ),

                //Shoot flying targets
                new Decorator(
                    ret => Me.CurrentTarget.IsAboveTheGround(),
                    new PrioritySelector(
                        new Action(r =>
                        {
                            Logger.WriteDebug("Target appears airborne: flying={0} aboveground={1}",
                                Me.CurrentTarget.IsFlying.ToYN(),
                                Me.CurrentTarget.IsAboveTheGround().ToYN()
                                );
                            return RunStatus.Failure;
                        }),
                        Spell.Cast(sp => "Crackling Jade Lightning", mov => true, on => Me.CurrentTarget, req => !Me.CurrentTarget.IsWithinMeleeRange && Me.CurrentTarget.SpellDistance() < 40, cancel => false),
                        Movement.CreateMoveToUnitBehavior(on => StyxWoW.Me.CurrentTarget, 27f, 22f)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.All)]
        public static Composite CreateMonkPreCombatBuffs()
        {
            return new PrioritySelector(
				new Decorator(ret => !StyxWoW.Me.HasAnyAura("Drink", "Food", "Refreshment"),
					PartyBuff.BuffGroup("Legacy of the White Tiger"))
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateWindwalkerMonkCombatBuffs()
        {
            return new PrioritySelector(

                Spell.BuffSelfAndWait( sp=>"Stance of the Fierce Tiger", req => !Me.GetAllAuras().Any(a => a.ApplyAuraType == WoWApplyAuraType.ModShapeshift && a.IsPassive && a.Name == "Stance of the Fierce Tiger")),

                new Decorator(
                    req => !Unit.IsTrivial(Me.CurrentTarget),
                    new PrioritySelector(
                        Spell.Buff("Touch of Karma",
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(
                                u => u.IsTargetingMeOrPet
                                    && (u.IsPlayer || SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds)
                                    && (u.IsWithinMeleeRange || (u.Distance < 20 && TalentManager.HasGlyph("Touch of Karma")))),
                            ret => Me.HealthPercent < 70),

                        Spell.Cast("Tigereye Brew", ctx => Me, ret => Me.HasAura("Tigereye Brew", 10)),
                        Spell.Cast("Energizing Brew", ctx => Me, ret => Me.CurrentEnergy < 40),
                        Spell.Cast("Chi Brew", ctx => Me, ret => Me.CurrentChi == 0),
                        Spell.Cast("Fortifying Brew", ctx => Me, ret => Me.HealthPercent <= SingularSettings.Instance.Monk().FortifyingBrewPct),
                        Spell.BuffSelf("Zen Sphere", ctx => Me.HealthPercent < 90 && HasTalent(MonkTalents.ZenSphere)),

                        Spell.Cast(
                            "Invoke Xuen, the White Tiger",
                            ret =>
                            {
                                if (Me.GotTarget())
                                {
                                    if (!Me.IsMoving && Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 10) >= 3)
                                        return true;
                                    if (Me.CurrentTarget.IsPlayer && Me.CurrentTarget.IsHostile && Me.CurrentTarget.IsWithinMeleeRange)
                                        return true;
                                }
                                return false;
                            }
                            )
                        )
                    )
                );
        }

	    private static bool HoldForTouchOfDeath
	    {
		    get
		    {
				return Me.CurrentTarget != null && Me.CurrentTarget.TimeToDeath(long.MaxValue) < 10L && SpellManager.HasSpell("Touch of Death") &&
			           Spell.GetSpellCooldown("Touch of Death").TotalSeconds < 8d && Me.MaxChi - Me.CurrentChi >= 2;
		    }
	    }
		
		[Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Instances)]
	    public static Composite CreateWindwalkerMonkCombatInstances()
	    {
			return new PrioritySelector(
				Helpers.Common.EnsureReadyToAttackFromMelee(),

				CreateWindwalkerDiagnosticBehavior(),

				Helpers.Common.CreateInterruptBehavior(),

				Common.CastTouchOfDeath(),

				Spell.WaitForCastOrChannel(FaceDuring.Yes, LagTolerance.No),

                Common.CreateMonkCloseDistanceBehavior(),

                Spell.Cast("Tigereye Brew", ctx => Me, ret => Me.HasAura("Tigereye Brew", 10)),
                Spell.Cast("Energizing Brew", ctx => Me, ret => Me.CurrentEnergy < 40),
                Spell.Cast("Chi Brew", ctx => Me, ret => Me.MaxChi - Me.CurrentChi >= 2),
                Spell.Cast("Fortifying Brew", ctx => Me, ret => Me.HealthPercent <= SingularSettings.Instance.Monk().FortifyingBrewPct),
                Spell.BuffSelf("Zen Sphere", ctx => HasTalent(MonkTalents.ZenSphere)),

				Spell.Cast(
					"Invoke Xuen, the White Tiger",
					req => !Me.IsMoving && Me.CurrentTarget.IsBoss() && Me.CurrentTarget.IsWithinMeleeRange),
					

				new Decorator(ctx => MonkSettings.UseSef,
					new PrioritySelector(
						// Cancel SEF from mobs that we should no longer be SEFing
						ctx => 
							Unit.NearbyUnitsInCombatWithUsOrOurStuff
								.FirstOrDefault(u => u.HasMyOrMyStuffsAura("Storm, Earth, and Fire") && 
													(u == Me.CurrentTarget || u.IsImmune(WoWSpellSchool.Physical) || u.IsCrowdControlled())),
						Spell.Cast("Storm, Earth, and Fire", onUnit => (WoWUnit)onUnit),

						new PrioritySelector(
							ctx => 
								Unit.NearbyUnitsInCombatWithUsOrOurStuff
									.Where(u => u != Me.CurrentTarget && !u.IsImmune(WoWSpellSchool.Physical) && 
												!u.IsCrowdControlled() && !u.HasMyOrMyStuffsAura("Storm, Earth, and Fire"))
									.OrderBy(u => u.DistanceSqr)
									.FirstOrDefault(),
							Spell.Cast("Storm, Earth, and Fire", onUnit => (WoWUnit)onUnit, req => !Me.HasMyOrMyStuffsAura("Storm, Earth, and Fire", 2))
							))),

                //-- prior to this cast without line of sight or facing or currenttarget --
                Movement.WaitForLineOfSpellSight(),

                //-- prior to this cast without facing or currenttarget --
                Movement.WaitForFacing(),

                //-- following have line of sight and facing (even when disabled) --

                Spell.Cast("Serenity", ret => Me.HasAura("Tiger Power") && Me.CurrentTarget.HasMyAura("Rising Sun Kick")),

				new Decorator(ret => !HoldForTouchOfDeath,
					new PrioritySelector(
						Spell.Cast("Tiger Palm", ret => Me.HasAuraExpired("Tiger Palm", "Tiger Power", 4)),
						Spell.Cast("Rising Sun Kick", 
							ret => !SpellManager.HasSpell("Chi Explosion") || 
									Me.CurrentTarget.HasAuraExpired("Rising Sun Kick") ||
									Unit.NearbyUnfriendlyUnits.Count(u => u.Location.DistanceSqr(Me.CurrentTarget.Location) <= 8 * 8) <= 1),
						Spell.Cast("Fists of Fury", ret => !Me.HasAuraExpired("Tiger Palm", "Tiger Power", 4) && !Me.CurrentTarget.HasAuraExpired("Rising Sun Kick", 4)),
						Spell.Cast("Chi Explosion", 
							ret => Me.CurrentChi >= 4 && Unit.NearbyUnfriendlyUnits.Count(u => u.Location.DistanceSqr(Me.CurrentTarget.Location) <= 8 * 8) >= 2 &&
									(!SpellManager.HasSpell("Fists of Fury") || SpellManager.Spells["Fists of Fury"].CooldownTimeLeft.TotalSeconds > 4)),
						Spell.Cast("Chi Explosion", 
							ret => Me.HasAura("Combo Breaker: Chi Explosion") && Me.CurrentChi >= 2 &&
									Unit.NearbyUnfriendlyUnits.Count(u => u.Location.DistanceSqr(Me.CurrentTarget.Location) <= 8 * 8) <= 1 &&
									(!SpellManager.HasSpell("Fists of Fury") || SpellManager.Spells["Fists of Fury"].CooldownTimeLeft.TotalSeconds > 2)),
						Spell.Cast("Blackout Kick", ret => Me.HasAura("Combo Breaker: Blackout Kick") || Me.HasAura("Serenity")),
						Spell.Cast("Tiger Palm", ret => Me.HasAura("Combo Breaker: Tiger Palm")),
						Spell.Cast("Chi Wave"),
						Spell.Cast("Zen Sphere", onUnit => Unit.NearbyFriendlyPlayers.OrderBy(p => p.DistanceSqr).FirstOrDefault()),
						Spell.Cast("Chi Explosion", 
							ret => Me.CurrentChi >= 3 && Unit.NearbyUnfriendlyUnits.Count(u => u.Location.DistanceSqr(Me.CurrentTarget.Location) <= 8 * 8) <= 1 &&
									(!SpellManager.HasSpell("Fists of Fury") || SpellManager.Spells["Fists of Fury"].CooldownTimeLeft.TotalSeconds > 4)),
						Spell.Cast("Blackout Kick", ret => !SpellManager.HasSpell("Chi Explosion") && Me.MaxChi - Me.CurrentChi < 2)
						)
				),

				new Decorator(
					ctx => Me.MaxChi - Me.CurrentChi >= 1 && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= 3,
					new PrioritySelector(
						Spell.Cast("Rushing Jade Wind"),
						Spell.Cast("Spinning Crane Kick", ret => !SpellManager.HasSpell("Rushing Jade Wind"))
						)),

				new Decorator(
					req => Me.MaxChi - Me.CurrentChi >= 2,
					new PrioritySelector(
						Spell.Cast("Expel Harm", ret => StyxWoW.Me.HealthPercent < 95),
						Spell.Cast("Jab"))
					),

				Movement.CreateMoveToMeleeBehavior(true)
				);
	    }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Normal)]
        public static Composite CreateWindwalkerMonkCombatNormal()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => StyxWoW.Me.HasAura("Fists of Fury")
                        && !Unit.NearbyUnfriendlyUnits.Any(u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)),
                    new Action(ret =>
                    {
                        Logger.WriteDebug("cancelling Fists of Fury - no targets within range");
                        SpellManager.StopCasting();
                        return RunStatus.Success;
                    })
                    ),

                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        CreateWindwalkerDiagnosticBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForLineOfSpellSight(),
                        Movement.WaitForFacing(),

#if USE_OLD_ROLL
                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed && !MonkSettings.DisableRoll && !Me.CurrentTarget.IsAboveTheGround() && Me.CurrentTarget.SpellDistance() > 10,
                            new Throttle(1,
                                new Sequence(
                                    new PrioritySelector(
                                        Spell.Cast("Flying Serpent Kick", ret => TalentManager.HasGlyph("Flying Serpent Kick")),
                                        Spell.Cast("Roll", ret => !Me.HasAura("Flying Serpent Kick"))
                                        ),
                                    new Action(r => RollTimer.Reset())
                                    )
                                )
                            ),
#else
                        Common.CreateMonkCloseDistanceBehavior(),
#endif

                        Spell.Cast("Leg Sweep", ret => Spell.UseAOE && MonkSettings.StunMobsWhileSolo && SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.CurrentTarget.IsWithinMeleeRange),



                        Common.CastTouchOfDeath(),

                        // AoE behavior
                        Spell.Cast("Paralysis", 
                            onu => Unit.NearbyUnfriendlyUnits
                                .FirstOrDefault( u => u.IsCasting && u.Distance.Between( 9, 20) && Me.IsSafelyFacing(u) )),

                        Spell.Cast("Rising Sun Kick"),

                        Spell.Cast("Fists of Fury", 
                            ret => Unit.NearbyUnfriendlyUnits.Count( u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)) >= 2),

                        Spell.Cast("Rushing Jade Wind", ctx => HasTalent(MonkTalents.RushingJadeWind) && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= 4),
                        Spell.Cast("Spinning Crane Kick", ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= MonkSettings.SpinningCraneKickCnt),

                        Spell.Cast("Tiger Palm", ret => Me.CurrentChi > 0 && Me.HasKnownAuraExpired( "Tiger Power")),

                        // chi dump
                        Spell.Cast("Blackout Kick", ret => Me.CurrentChi == Me.MaxChi),

                        // free Tiger Palm or Blackout Kick... do before Jab
                        Spell.Cast("Blackout Kick", ret => Me.HasAura("Combo Breaker: Blackout Kick")),
                        Spell.Cast("Tiger Palm", ret => Me.HasAura("Combo Breaker: Tiger Palm")),

                        Spell.Buff( "Expel Harm", on => Common.BestExpelHarmTarget(), req => Me.CurrentChi < (Me.MaxChi-2) && Me.HealthPercent < 80),

                        Spell.Cast("Jab", ret => Me.CurrentChi < Me.MaxChi),

                        new Decorator(
                            req => Me.CurrentChi <= 3,
                            new PrioritySelector(
                                Spell.Buff("Expel Harm", on => Common.BestExpelHarmTarget()),
                                Spell.Cast("Jab")
                                )
                            ),

                        new Decorator(
                            req => Me.CurrentChi > 0,
                            new PrioritySelector(
                                Spell.Cast("Tiger Palm", ret => Me.HasKnownAuraExpired( "Tiger Power")),
                                Spell.Cast("Rising Sun Kick"),
                                Spell.Cast("Fists of Fury", 
                                    ret => Unit.NearbyUnfriendlyUnits.Count( u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)) >= 2),

                                Spell.Cast("Blackout Kick", ret => Me.HasAura("Combo Breaker: Blackout Kick")),
                                Spell.Cast("Tiger Palm", ret => Me.HasAura("Combo Breaker: Tiger Palm")),
                                Spell.Cast("Blackout Kick")
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Battlegrounds  )]
        public static Composite CreateWindwalkerMonkPullBattlegrounds()
        {
            // replace with battleground specific logic 
            return CreateWindwalkerMonkPullNormal();
        }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Battlegrounds)]
        public static Composite CreateWindwalkerMonkCombatBattlegrounds()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                new Decorator(
                    ret => StyxWoW.Me.HasAura("Fists of Fury")
                        && !Unit.NearbyUnfriendlyUnits.Any(u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)),
                    new Action(ret =>
                    {
                        Logger.WriteDebug("cancelling Fists of Fury - no targets within range");
                        SpellManager.StopCasting();
                        return RunStatus.Success;
                    })
                    ),

                Spell.WaitForCastOrChannel(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        CreateWindwalkerDiagnosticBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        // ranged attack on the run when chasing

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Spell.Cast("Leg Sweep", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.IsWithinMeleeRange && !u.IsCrowdControlled())),

                        Common.CastTouchOfDeath(),

                        Spell.Buff("Paralysis",
                            onu => Unit.NearbyUnfriendlyUnits
                                .FirstOrDefault(u => u.IsCasting && u.Distance.Between(9, 20) && Me.IsSafelyFacing(u))),

                        Spell.Buff("Spear Hand Strike",
                            onu => Unit.NearbyUnfriendlyUnits
                                .FirstOrDefault(u => u.IsCasting && u.IsWithinMeleeRange && Me.IsSafelyFacing(u))),

                        Spell.Cast("Rising Sun Kick"),

                        Spell.Cast("Fists of Fury",
                            ret => Unit.NearbyUnfriendlyUnits.Any(u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u))),

                        Spell.Cast("Rushing Jade Wind", ctx => HasTalent(MonkTalents.RushingJadeWind) && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= 4),
                        Spell.Cast("Spinning Crane Kick", ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= MonkSettings.SpinningCraneKickCnt),

                        Spell.Cast("Tiger Palm", ret => Me.CurrentChi > 0 && Me.HasKnownAuraExpired("Tiger Power")),
                                    
                        // chi dump
                        Spell.Cast("Blackout Kick", ret => Me.CurrentChi == Me.MaxChi),

                        // free Tiger Palm or Blackout Kick... do before Jab
                        Spell.Cast("Blackout Kick", ret => Me.HasAura("Combo Breaker: Blackout Kick")),
                        Spell.Cast("Tiger Palm", ret => Me.HasAura("Combo Breaker: Tiger Palm")),

                        Spell.Cast("Jab", ret => Me.CurrentChi < Me.MaxChi),

                        // close distance if at range
                        Movement.CreateFaceTargetBehavior(10f, false),
                        //new Decorator(
                        //    ret => !Me.IsSafelyFacing( Me.CurrentTarget, 10f),
                        //    new Action( ret => {
                        //        // Logger.WriteDebug("WindWalkerMonk: Facing because turned more than 10 degrees");
                        //        StyxWoW.Me.CurrentTarget.Face();
                        //        return RunStatus.Failure;
                        //        }) 
                        //    ),
#if USE_OLD_ROLL        
                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed && Me.IsSafelyFacing(Me.CurrentTarget, 10f) && Me.CurrentTarget.SpellDistance() > 10,
                            new PrioritySelector(
                                Spell.Cast("Flying Serpent Kick",  ret => TalentManager.HasGlyph("Flying Serpent Kick")),
                                Spell.Cast("Roll", ret =>  !MonkSettings.DisableRoll && Me.CurrentTarget.SpellDistance() > 10 && !Me.HasAura("Flying Serpent Kick"))
                                )
                            )
#else
                        Common.CreateMonkCloseDistanceBehavior()
#endif
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Instances )]
        public static Composite CreateWindwalkerMonkPullInstances()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(FaceDuring.Yes),

#if USE_OLD_ROLL
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && !MonkSettings.DisableRoll && Me.CurrentTarget.Distance > 15)
                        )
                    ),
#else
                Common.CreateMonkCloseDistanceBehavior(),
#endif
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Normal | WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateWindwalkerMonkHeal()
        {
            return new PrioritySelector(

                Common.CreateMonkDpsHealBehavior()

                );
        }

        private static Composite CreateWindwalkerDiagnosticBehavior()
        {
            return new ThrottlePasses(1, 1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        Logger.WriteDebug(".... health={0:F1}%, energy={1}%, chi={2}, tpower={3}, tptime={4}, tgt={5:F1} @ {6:F1}, ",
                            Me.HealthPercent,
                            Me.CurrentEnergy,
                            Me.CurrentChi,
                            Me.HasAura("Tiger Power"),
                            Me.GetAuraTimeLeft("Tiger Power", true).TotalMilliseconds,
                            !Me.GotTarget() ? 0f : Me.CurrentTarget.HealthPercent,
                            (Me.CurrentTarget ?? Me).Distance
                            );
                        return RunStatus.Failure;
                    })
                    )
                );

        }

    }
}