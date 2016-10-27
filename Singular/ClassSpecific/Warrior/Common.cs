﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.Common.Helpers;
using Styx.Helpers;
using Styx.TreeSharp;
using Singular.Helpers;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;
using Styx;
using Singular.Managers;
using Singular.Dynamics;
using Styx.CommonBot;
using CommonBehaviors.Actions;
using System.Drawing;
using System.Numerics;
using Styx.Pathing;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals;
using Singular.Utilities;
using Styx.Common;

namespace Singular.ClassSpecific.Warrior
{
    static class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }
        public static bool HasTalent(WarriorTalents tal) { return TalentManager.IsSelected((int)tal); }
        public static WarriorTalents GetTierTalent(int tier) { return (WarriorTalents) TalentManager.GetSelectedForTier(tier); }

        public static bool Tier14TwoPieceBonus { get { return Me.HasAura("Item - Warrior T14 DPS 2P Bonus"); } }
        public static bool Tier14FourPieceBonus { get { return Me.HasAura("Item - Warrior T14 DPS 4P Bonus"); } }

        public static float DistanceChargeBehavior { get; set; }
        public static float VictoryRushDistance { get; set; }
        public static int VictoryRushHealth { get; set; }

        public static float HeroicLeapDistance { get; set; }

        public static int _DistanceWindAndThunder { get; set; }

        [Behavior(BehaviorType.Initialize, WoWClass.Warrior)]
        public static Composite CreateWarriorInitialize()
        {
            // removed combatreach because of # of missed Charges
            DistanceChargeBehavior = 25f;

            string spellVictory = "Victory Rush";
            VictoryRushHealth = 90;
            if (SpellManager.HasSpell("Impending Victory"))
            {
                VictoryRushHealth = 80;
                spellVictory = "Impending Victory";
                Logger.Write(LogColor.Init, "impending victory talent: [{0}] at {1}%", spellVictory, VictoryRushHealth);
            }
            else if (TalentManager.HasGlyph("Victory Rush"))
            {
                VictoryRushHealth = 85;
                Logger.Write(LogColor.Init, "glyph of victory rush: [{0}] at {1}%", spellVictory, VictoryRushHealth);
            }
            else
            {
                Logger.Write(LogColor.Init, "victory rush: [{0}] at {1}%", spellVictory, VictoryRushHealth);
            }


            if (WarriorSettings.VictoryRushOnCooldown)
            {
                Logger.Write(LogColor.Init, "victory rush on cooldown: [{0}] cast on cooldown", spellVictory);
                VictoryRushHealth = 100;
            }

            VictoryRushDistance = 5f;
            if (TalentManager.HasGlyph("Victorious Throw"))
            {
                VictoryRushDistance = 15f;
                Logger.Write(LogColor.Init, "glyph of victorious throw: [{0}] at {1:F1} yds", spellVictory, VictoryRushDistance);
            }

            HeroicLeapDistance = 40f;
            if (TalentManager.HasGlyph("Death From Above"))
            {
                HeroicLeapDistance = 25f;
                Logger.Write(LogColor.Init, "glyph of death from above: [Heroic Leap] at {0:F1} yds", HeroicLeapDistance);
            }

            _DistanceWindAndThunder = 0;
            if (TalentManager.HasGlyph("Wind and Thunder"))
            {
                _DistanceWindAndThunder = 4;
                Logger.Write(LogColor.Init, "glyph of wind and thunder: [Whirlwind] and [Thunder Clap] +4 yds");
            }

            DistanceChargeBehavior -= 0.2f;    // should not be needed, but is  -- based on log files and observations we need this adjustment

            return null;
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Warrior)]
        public static Composite CreateWarriorLossOfControlBehavior()
        {
            return new PrioritySelector(
                Spell.BuffSelfAndWait(
                    "Berserker Rage",
                    req =>
                    {
                        if (Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Turned))
                            return true;
                        if (Me.Fleeing)
                            return true;
                        return false;
                    },
                    gcd: HasGcd.No
                    ),

                CreateWarriorEnragedRegeneration()
                );
        }

        public static Composite CreateWarriorCombatPullMore()
        {
            return new Throttle(
                2,
                new Decorator(
                    req => SingularRoutine.CurrentWoWContext == WoWContext.Normal
                        && Me.GotTarget()
                        && !Me.CurrentTarget.IsPlayer
                        && !Me.CurrentTarget.IsTagged
                        && !Me.HasAura("Charge")
                        && !Me.CurrentTarget.HasAnyOfMyAuras("Charge Stun", "Warbringer")
                        && !Me.CurrentTarget.IsWithinMeleeRange,
                    new PrioritySelector(
                        Common.CreateChargeBehavior(),
                        Spell.Cast("Heroic Throw", req => Me.IsSafelyFacing(Me.CurrentTarget) && !Me.CurrentTarget.IsWithinMeleeRange),
                        Spell.Cast("Taunt", req => SingularSettings.Instance.EnableTaunting),
                        new Decorator(
                            req => SpellManager.HasSpell("Throw") && Me.CurrentTarget.SpellDistance().Between(6, 27),
                            new Sequence(
                                new Action( r => StopMoving.Now() ),
                                new Wait( 1, until => !Me.IsMoving, new ActionAlwaysSucceed()),
                                Spell.Cast( s => "Throw", mov => true, on => Me.CurrentTarget, req => true, cancel => false ),
                                new WaitContinue( 1, until => !Me.IsCasting, new ActionAlwaysSucceed())
                                )
                            )
                        )
                    )
                );
        }

        public static Composite CreateWarriorEnragedRegeneration()
        {
            return new Decorator(
                req => Me.HealthPercent < WarriorSettings.WarriorEnragedRegenerationHealth && !Spell.IsSpellOnCooldown("Enraged Regeneration"),
                new PrioritySelector(
                    Spell.BuffSelf("Enraged Regeneration", req => true, 0, HasGcd.No)
                    )
                );
        }

        public static string SelectedShoutAsSpellName
        {
            get { return WarriorSettings.Shout.ToString().CamelToSpaced().Substring(1); }
        }

        /// <summary>
        /// keep a single copy of Charge Behavior so the wrapping Throttle will account for 
        /// uses across multiple behaviors that reference this method
        /// </summary>
        private static Composite _singletonChargeBehavior = null;

        public static Composite CreateChargeBehavior()
        {
            if (!WarriorSettings.UseWarriorCloser)
                return new ActionAlwaysFail();

            if (_singletonChargeBehavior == null)
            {
                _singletonChargeBehavior = new Throttle(TimeSpan.FromMilliseconds(1500),
                    new PrioritySelector(
                        ctx => Me.CurrentTarget,
                        new Decorator(
                            req => (req as WoWUnit).IsGapCloserAllowed(),
                            new PrioritySelector(
                                CreateChargeCloser(),
                                CreateHeroicLeapCloser()
                                )
                            )
                        )
                    );
            }

            return _singletonChargeBehavior;
        }

        private static bool IsGapCloserAllowed(this WoWUnit unit)
        {
            return unit != null
                && MovementManager.IsClassMovementAllowed
                && Spell.UseAOE
                && !unit.IsPathErrorTarget()
                && !Me.HasAura("Charge")
                && !unit.HasAnyOfMyAuras("Charge Stun", "Warbringer")
                && Movement.InLineOfSpellSight(unit);  
        }

        public static Composite CreateChargeCloser()
        {
                    // note: use Distance here -- even though to a WoWUnit, hitbox does not come into play for all mobs
	        return
		        Spell.Cast("Charge",
			        req =>
				        Me.CurrentTarget.IsGapCloserAllowed() && Me.CurrentTarget.Distance.Between(8, DistanceChargeBehavior));
        }
        
        public static Composite CreateHeroicLeapCloser()
        {
            const float JUMP_MIN = 8.4f;

            if (!SpellManager.HasSpell("Heroic Leap"))
                return new ActionAlwaysFail();

            //  Leap to close distance
            // note: use Distance rather than SpellDistance since spell is to point on ground
            return new PrioritySelector(
                ctx => Me.CurrentTarget,
                new Decorator(
                    req => (req as WoWUnit).IsGapCloserAllowed(),
                    Spell.CastOnGround(
                        "Heroic Leap",
                        loc =>
                        {
                            WoWUnit unit = loc as WoWUnit;
                            if (unit != null)
                            {
                                Vector3 pt = unit.Location;
                                float distToMob = Me.Location.Distance(pt);
                                float distToMobReach = distToMob - unit.CombatReach;
                                float distToJump = distToMobReach;
                                string comment = "hitbox of";

                                if (distToJump < JUMP_MIN)
                                {
                                    comment = "too close, now location of";
                                    distToJump = distToMob;
                                    if (distToJump < JUMP_MIN)
                                    {
                                        return Vector3.Zero;
                                    }
                                }

                                if (distToMob >= HeroicLeapDistance)
                                {
                                    distToJump = distToMobReach - 7; // allow for damage radius
                                    comment = "too far, now 7 yds before hitbox of";
                                    if (distToJump >= HeroicLeapDistance)
                                    {
                                        return Vector3.Zero;
                                    }
                                }

                                float neededFacing = Styx.Helpers.WoWMathHelper.CalculateNeededFacing(Me.Location, pt);
                                Vector3 ptJumpTo = Me.Location.RayCast(neededFacing, distToJump);
                                Logger.WriteDiagnostic("HeroicLeap: jump target is {0} {1}", comment, unit.SafeName());
                                float h = unit.HeightOffTheGround();
                                float m = unit.MeleeDistance();
                                if (h > m)
                                {
                                    Logger.WriteDiagnostic("HeroicLeap: aborting, target is {0:F3} off ground and melee is {1:F3}", h, m);
                                    return Vector3.Zero;
                                }
                                else if (h < -1)
                                {
                                    Logger.WriteDiagnostic("HeroicLeap: aborting, target appears to be {0:F3} off ground @ {1}", h, ptJumpTo);
                                    return Vector3.Zero;
                                }

                                Vector3 ptNew = new Vector3();
                                ptNew.X = ptJumpTo.X;
                                ptNew.Y = ptJumpTo.Y;
                                ptNew.Z = ptJumpTo.Z - h;
                                Logger.WriteDiagnostic("HeroicLeap: adjusting dest, target @ {0} is {1:F3} above ground @ {2}", ptJumpTo, h, ptNew);
                                ptJumpTo = ptNew;

                                return ptNew;
                            }

                            return Vector3.Zero;
                        },
                        req =>
                        {
                            if (!MovementManager.IsClassMovementAllowed)
                                return false;

                            if (req == null)
                                return false;

                            if (Spell.IsSpellOnCooldown("Heroic Leap"))
                                return false;

                            if (Me.SpellDistance(req as WoWUnit) > (HeroicLeapDistance + 7))
                                return false;

                            return true;
                        },
                        false,
                        desc => string.Format("on {0} @ {1:F1}%", (desc as WoWUnit).SafeName(), (desc as WoWUnit).HealthPercent)
                        )
                    )
                );

        }

        public static Composite CreateSpellReflectBehavior()
        {
            return Spell.Cast(
                "Spell Reflection",
                on =>
                {
                    bool isPummelOnCD = Spell.IsSpellOnCooldown("Pummel");
                    return Unit.UnitsInCombatWithUsOrOurStuff(40)
                        .FirstOrDefault(u => u.IsCasting && (!u.CanInterruptCurrentSpellCast || isPummelOnCD || !Spell.CanCastHack("Pummel", u)));
                });
        }

        public static Composite CreateVictoryRushBehavior()
        {
            // health below determined %
            // user wants to cast on cooldown without regard to health
            // we have aura AND (target is about to die OR aura expires in less than 3 secs)
            return new Throttle( 
                new Decorator(
                    ret => 
                        Me.GotTarget()
                        && ( 
                            WarriorSettings.VictoryRushOnCooldown
                            || Me.HealthPercent <= VictoryRushHealth
                            || (Me.HasAura("Victorious") && (Me.HasAuraExpired("Victorious", 3) || Me.CurrentTarget.TimeToDeath() < 7))
                           )
                        && Me.CurrentTarget.InRangeForVictoryRush(),
                    new PrioritySelector(
                        Spell.Cast("Impending Victory"),
                        Spell.Cast("Victory Rush", ret => Me.HasAura("Victorious"))
                        )
                    )
                );
        }

        private static bool InRangeForVictoryRush( this WoWUnit unit)
        {
            return (VictoryRushDistance <= 5f ? unit.IsWithinMeleeRange : unit.SpellDistance() < VictoryRushDistance);
        }
        
        public static bool IsEnraged 
        { 
            get 
            { 
                return StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Enraged); 
            } 
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
                    req => !Me.CurrentTarget.IsWithinMeleeRange ,
                    new Sequence(
                        new PrioritySelector(
                            Spell.Cast("Taunt", req => SingularSettings.Instance.EnableTaunting),
                            Spell.Cast("Heroic Throw"),
                            Spell.Cast("Whirlwind", req => !Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8 + _DistanceWindAndThunder),
                            Spell.Cast("Shockwave", req => !Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 10 && Me.IsSafelyFacing(Me.CurrentTarget, 60)),
                            Spell.Cast("Dragon Roar", req => !Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8),
                            Spell.Cast("Thunder Clap", req => !Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8 + _DistanceWindAndThunder),
                            Spell.Cast("Storm Bolt"),
                            new Sequence(
                                new PrioritySelector(
                                    Movement.CreateEnsureMovementStoppedBehavior( 27f, on => Me.CurrentTarget, reason: "To cast Throw"),
                                    new ActionAlwaysSucceed()
                                    ),
                                new Wait( 1, until => !Me.IsMoving, new ActionAlwaysSucceed()),
                                Spell.Cast("Throw")
                                )
                            ),
                        new Action( r => 
                        {
                            if (Me.CurrentTarget.TimeToDeath(99) < 40)
                            {
                                SingularRoutine.TargetTimeoutTimer.Reset();
                            }
                        })
                        )
                    )
                );
        }

        public static Composite CheckIfWeShouldCancelBladestorm()
        {
            if (!HasTalent(WarriorTalents.Bladestorm))
                return new ActionAlwaysFail();

            return new ThrottlePasses(
                1, 
                TimeSpan.FromSeconds(1),
                RunStatus.Failure,
                new Decorator(
                    req => StyxWoW.Me.HasAura("Bladestorm"),
                    new Action(r =>
                    {
                        // check if it makes sense to cancel to resume normal speed movement and charge
                        if (!Me.Combat || !Unit.UnfriendlyUnits(20).Any(u => u.Aggro || (u.IsPlayer && u.IsHostile) || u.IsTargetingUs()))
                        {
                            Logger.WriteDebug("Bladestorm: cancel since out of combat or no targets within 20 yds");
                            Me.CancelAura("Bladestorm");
                            return RunStatus.Success;
                        }
                        return RunStatus.Failure;
                    })
                    )
                );
        }

        public const int SUDDEN_DEATH_PROC = 52437;
        public static Composite CreateExecuteOnSuddenDeath(UnitSelectionDelegate onUnit = null, SimpleBooleanDelegate requirements = null)
        {

            if (onUnit == null)
                onUnit = on => Me.CurrentTarget;

            if (requirements == null)
                requirements = req => true;

            return Spell.Cast("Execute", onUnit, req => Me.HasAura(SUDDEN_DEATH_PROC) && requirements(req));
        }

        public static Composite CreateDieByTheSwordBehavior()
        {
            return Spell.HandleOffGCD(Spell.BuffSelf("Die by the Sword", req => Me.Combat && Me.HealthPercent <= WarriorSettings.DieByTheSwordHealth, 0, HasGcd.No));
        }

        public static bool IsSlowNeeded(WoWUnit unit)
        {
            if (!WarriorSettings.UseWarriorSlows || unit == null || !unit.IsPlayer)
                return false;
            return !unit.IsCrowdControlled() && !unit.IsSlowed(50) && !unit.HasAura("Hand of Freedom");
        }

        public static int DistanceWindAndThunder( int range)
        {
            return _DistanceWindAndThunder + range;
        }
    }

    enum WarriorTalents
    {
        None = 0,

        Dauntless = 1,
        Overpower,
        SweepingStrikes,

        WarMachine = Dauntless,
        EndlessRage = Overpower,
        FreshMeat = SweepingStrikes,

        ShockwaveProtection = Dauntless,
        StormBoltProtection = Overpower,
        Warbringer = SweepingStrikes,


        Shockwave = 4,
        StormBolt,
        DoubleTime,

        ImpendingVictory = Shockwave,
        InspiringPresence = StormBolt,
        Safeguard = DoubleTime,


        FervorOfBattle = 7,
        Rend,
        Avatar,

        WreckingBall = FervorOfBattle,
        Outburst = Rend,
        
        RenewedFury = FervorOfBattle,
        Ultimatum = Rend,


        SecondWind = 10,
        BoundingStride,
        DefensiveStance,

        FuriousCharge = SecondWind,
        Warpaint = DefensiveStance,

        WarlordsChallenge = SecondWind,
        CracklingThunder = DefensiveStance,


        InForTheKill = 13,
        MortalCombo,
        FocusedRage,

        Massacre = InForTheKill,
        FrothingBerserker = MortalCombo,
        Carnage = FocusedRage,

        BestServedCold = InForTheKill,
        NeverSurrender = MortalCombo,
        Indomitable = FocusedRage,


        DeadlyCalm = 16,
        Trauma,
        TitanicMight,

        Bloodbath = DeadlyCalm,
        Frenzy = Trauma,
        InnerRage = TitanicMight,

        Vengeance = DeadlyCalm,
        IntroTheFray = Trauma,
        BoomingVoice = TitanicMight,


        AngerManagement = 19,
        OpportunityStrikes,
        Ravager,

        Bladestorm = AngerManagement,
        RecklessAbandon = OpportunityStrikes,
        DragonRoar = AngerManagement,

        HeavyRepercussions = OpportunityStrikes
    }
}
