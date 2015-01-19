using System;
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
using Styx.Pathing;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals;

namespace Singular.ClassSpecific.Warrior
{
    static class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }
        public static bool HasTalent(WarriorTalents tal) { return TalentManager.IsSelected((int)tal); }

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

            if (TalentManager.HasGlyph("Long Charge"))
            {
                DistanceChargeBehavior = 30f;
                Logger.Write(LogColor.Init, "glyph of long charge: [Charge] at {0:F1} yds", DistanceChargeBehavior);
            }

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


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, priority: 999)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, priority: 999)]
        public static Composite CreateWarriorNormalPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelfAndWait(
                    sp => SelectedStanceAsSpellName, 
                    req => 
                    {
                        if (StyxWoW.Me.Shapeshift == (ShapeshiftForm)SelectedStance)
                            return false;
                        return true;
                    }),
                PartyBuff.BuffGroup(Common.SelectedShoutAsSpellName),
                CreateSpellReflectBehavior()
                );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Warrior)]
        public static Composite CreateWarriorLossOfControlBehavior()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Berserker Rage", ret => Me.Fleeing || (Me.Stunned && Me.HasAuraWithMechanic(Styx.WoWInternals.WoWSpellMechanic.Sapped))),
                // StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified)),

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
                        Spell.Cast("Taunt"),
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
                    Spell.HandleOffGCD( Spell.BuffSelf("Berserker Rage", req => true, 0, HasGcd.No) ),
                    Spell.BuffSelf("Enraged Regeneration", req => true, 0, HasGcd.No)
                    )
                );
        }

        public static string SelectedShoutAsSpellName
        {
            get { return WarriorSettings.Shout.ToString().CamelToSpaced().Substring(1); }
        }

        public static WarriorStance SelectedStance
        {
            get
            {
                var stance = WarriorSettings.Stance;
                if (stance == WarriorStance.Auto)
                {
                    switch (TalentManager.CurrentSpec)
                    {
                        case WoWSpec.WarriorArms:
                            stance = WarriorStance.BattleStance;
                            break;
                        case WoWSpec.WarriorFury:
                            stance = WarriorStance.BattleStance;
                            break;
                        default:
                        case WoWSpec.WarriorProtection:
                            if (!Protection.HasShieldInOffHand)
                                stance = Warrior.Protection.talentGladiator ? WarriorStance.GladiatorStance : WarriorStance.BattleStance;
                            else if (!Warrior.Protection.talentGladiator)
                                stance = WarriorStance.DefensiveStance;
                            else if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                                stance = WarriorStance.GladiatorStance;
                            else if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                                stance = WarriorStance.GladiatorStance;
                            else if (Me.Role != WoWPartyMember.GroupRole.Tank)
                                stance = WarriorStance.GladiatorStance;
                            else 
                                stance = WarriorStance.DefensiveStance;
                            break;
                    }
                }

                return stance ;
            }
        }

        public static string SelectedStanceAsSpellName
        {
            get 
            {
                return SelectedStance.ToString().CamelToSpaced().Substring(1);
            }
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
                            req => MovementManager.IsClassMovementAllowed
                                && req != null 
                                && ((req as WoWUnit).Guid != Singular.Utilities.EventHandlers.LastNoPathTarget || Singular.Utilities.EventHandlers.LastNoPathFailure < DateTime.Now - TimeSpan.FromMinutes(15))
                                && !Me.HasAura("Charge")
                                && !(req as WoWUnit).HasAnyOfMyAuras( "Charge Stun", "Warbringer")
                                && (req as WoWUnit).InLineOfSight,

                            new PrioritySelector(
                                // note: use Distance here -- even though to a WoWUnit, hitbox does not come into play
                                Spell.Cast("Charge", req => (req as WoWUnit).Distance.Between(8, DistanceChargeBehavior) && Me.IsSafelyFacing(req as WoWUnit)),

                                //  Leap to close distance
                                // note: use Distance rather than SpellDistance since spell is to point on ground
                                CreateHeroicLeapCloser()
                                )
                            )
                        )
                    );
            }

            return _singletonChargeBehavior;
        }

        public static Composite CreateSpellReflectBehavior()
        {
            return Spell.Cast(
                "Spell Reflect",
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

        /// <summary>
        /// checks if in a relatively balanced fight where atleast 3 of your
        /// teammates will benefti from long cooldowns.  fight must be atleast 3 v 3
        /// and size difference between factions nearby in fight cannot be greater
        /// than size / 3 + 1.  For example:
        /// 
        /// Yes:  3 v 3, 3 v 4, 3 v 5, 6 v 9, 9 v 13
        /// No :  2 v 3, 3 v 6, 4 v 7, 6 v 10, 9 v 14
        /// </summary>
        public static bool IsPvpFightWorthBanner
        {
            get 
            {
                /*
                int friends = Unit.NearbyFriendlyPlayers.Count(f => f.IsAlive);
                if (friends < 3)
                    return false;

                int enemies = Unit.NearbyUnfriendlyUnits.Count();
                if (enemies < 3)
                    return false;

                int diff = Math.Abs(friends - enemies);
                return diff <= ((friends / 3) + 1);
                 */
                return false;
            }
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

                    if (Me.CurrentTarget.IsPlayer)
                        return false;

                    if (Me.CurrentTarget.IsFlying)
                    {
                        float heightOffGround = Me.CurrentTarget.HeightOffTheGround();
                        float meleeDist = Me.CurrentTarget.MeleeDistance();
                        if (heightOffGround > meleeDist)
                        {
                            Logger.Write(LogColor.Hilite, "Ranged Attack: {0} is Flying {1:F3} yds off ground! using Ranged attack since can only reach {2:F3} yds....", Me.CurrentTarget.SafeName(), heightOffGround, meleeDist);
                            return true;
                        }
                    }

                    if ((DateTime.Now - Singular.Utilities.EventHandlers.LastNoPathFailure).TotalSeconds < 1f)
                    {
                        Logger.Write( LogColor.Hilite, "Ranged Attack: No Path Available error just happened, so using Ranged attack ....", Me.CurrentTarget.SafeName());
                        return true;
                    }
/*
                    if (Me.CurrentTarget.IsAboveTheGround())
                    {
                    Logger.Write( LogColor.Hilite, "{0} is {1:F1) yds above the ground! using Ranged attack....", Me.CurrentTarget.SafeName(), Me.CurrentTarget.HeightOffTheGround());
                    return true;
                    }
*/
                    double heightCheck = Me.CurrentTarget.MeleeDistance();
                    if (Me.CurrentTarget.Distance2DSqr < heightCheck * heightCheck && Math.Abs(Me.Z - Me.CurrentTarget.Z) >= heightCheck )
                    {
                        Logger.Write( LogColor.Hilite, "Ranged Attack: {0} appears to be off the ground! using Ranged attack....", Me.CurrentTarget.SafeName());
                        return true;
                    }
                    
                    WoWPoint dest = Me.CurrentTarget.Location;
                    if (!Me.CurrentTarget.IsWithinMeleeRange && !Styx.Pathing.Navigator.CanNavigateFully(Me.Location, dest))
                    {
                        Logger.Write( LogColor.Hilite, "Ranged Attack: {0} is not Fully Pathable! using ranged attack....", Me.CurrentTarget.SafeName());
                        return true;
                    }

                    return false;
                },
                new Decorator( 
                    req => !Me.CurrentTarget.IsWithinMeleeRange ,
                    new PrioritySelector(
                        Spell.Cast("Heroic Throw"),
                        new Sequence(
                            new PrioritySelector(
                                Movement.CreateEnsureMovementStoppedBehavior( 27f, on => Me.CurrentTarget, reason: "To cast Throw"),
                                new ActionAlwaysSucceed()
                                ),
                            new Wait( 1, until => !Me.IsMoving, new ActionAlwaysSucceed()),
                            Spell.Cast("Throw")
                            )
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
                        if (!Me.Combat || !Unit.UnfriendlyUnits(20).Any(u => u.Aggro || (u.IsPlayer && u.IsHostile)))
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

        public static Composite CreateExecuteOnSuddenDeath( UnitSelectionDelegate onUnit = null, SimpleBooleanDelegate requirements = null)
        {
            const int SUDDEN_DEATH_PROC = 52437;

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

        public static Composite CreateVigilanceBehavior()
        {
            UnitSelectionDelegate onUnit;

            if (WarriorSettings.VigilanceHealth <= 0)
                return new ActionAlwaysFail();

            if ( SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                onUnit = on => Unit.GroupMembers
                    .Where( m => m.PredictedHealthPercent() <= WarriorSettings.VigilanceHealth)
                    .OrderBy( m => m.HealthPercent)
                    .FirstOrDefault();
            else 
                onUnit = on => (Group.MeIsTank || !Group.AnyTankNearby ? Group.Healers : Group.Tanks)
                    .Where( m => m.PredictedHealthPercent() <= WarriorSettings.VigilanceHealth)
                    .OrderBy( m => m.HealthPercent)
                    .FirstOrDefault();

            return Spell.Buff( "Vigilance",  onUnit);
        }

        public static bool IsSlowNeeded(WoWUnit unit)
        {
            if (!WarriorSettings.UseWarriorSlows || unit == null || !unit.IsPlayer)
                return false;
            return !unit.IsCrowdControlled() && !unit.IsSlowed(50) && !unit.HasAura("Hand of Freedom");
        }

        public static Composite CreateHeroicLeapCloser()
        {
            const float JUMP_MIN = 8.4f;

            return new PrioritySelector(
                ctx => Me.CurrentTarget,
                Spell.CastOnGround(
                    "Heroic Leap", 
                    loc => 
                    {
                        WoWUnit unit = loc as WoWUnit;
                        if (unit != null)
                        {
                            WoWPoint pt = unit.Location;
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
                                    return WoWPoint.Empty;
                                }
                            }

                            if (distToMob >= HeroicLeapDistance)
                            {
                                distToJump = distToMobReach - 7; // allow for damage radius
                                comment = "too far, now 7 yds before hitbox of";
                                if (distToJump >= HeroicLeapDistance)
                                {
                                    return WoWPoint.Empty;
                                }
                            }

                            float neededFacing = Styx.Helpers.WoWMathHelper.CalculateNeededFacing(Me.Location, pt);
                            WoWPoint ptJumpTo = WoWPoint.RayCast(Me.Location, neededFacing, distToJump);
                            Logger.WriteDiagnostic("HeroicLeap: jump target is {0} {1}", comment, unit.SafeName());
                            float h = unit.HeightOffTheGround();
                            float m = unit.MeleeDistance();
                            if (h > m)
                            {
                                Logger.WriteDiagnostic("HeroicLeap: aborting, target is {0:F3} off ground and melee is {1:F3}", h, m);
                                return WoWPoint.Empty;
                            }
                            else if ( h < -1)
                            {
                                Logger.WriteDiagnostic("HeroicLeap: aborting, target appears to be {0:F3} off ground @ {1}", h, ptJumpTo);
                                return WoWPoint.Empty;
                            }

                            WoWPoint ptNew = new WoWPoint();
                            ptNew.X = ptJumpTo.X;
                            ptNew.Y = ptJumpTo.Y;
                            ptNew.Z = ptJumpTo.Z - h;
                            Logger.WriteDiagnostic("HeroicLeap: adjusting dest, target @ {0} is {1:F3} above ground @ {2}", ptJumpTo, h, ptNew);
                            ptJumpTo = ptNew;

                            return ptNew;
                        }

                        return WoWPoint.Empty;
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
                );

        }

        public static int DistanceWindAndThunder( int range)
        {
            return _DistanceWindAndThunder + range;
        }
    }

    enum WarriorTalents
    {
#if PRE_WOD
        None = 0,
        Juggernaut,
        DoubleTime,
        Warbringer,
        EnragedRegeneration,
        SecondWind,
        ImpendingVictory,
        StaggeringShout,
        PiercingHowl,
        DisruptingShout,
        Bladestorm,
        Shockwave,
        DragonRoar,
        MassSpellReflection,
        Safeguard,
        Vigilance,
        Avatar,
        Bloodbath,
        StormBolt
#else

        Juggernaut = 1,
        DoubleTime,
        Warbringer,

        EnragedRegeneration,
        SecondWind,
        ImpendingVictory,

        TasteForBlood,
        FuriousStrikes = TasteForBlood,
        HeavyRepercussions = TasteForBlood,
        SuddenDeath,
        Slam,
        UnquenchableThirst = Slam,
        UnyieldingStrikes = Slam,

        StormBolt,
        Shockwave,
        DragonRoar,

        MassSpellReflection,
        Safeguard,
        Vigilance,

        Avatar,
        Bloodbath,
        Bladestorm,

        AngerManagement,
        Ravager,
        Siegebreaker,
        GladiatorsResolve = Siegebreaker


#endif
    }
}
