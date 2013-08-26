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

namespace Singular.ClassSpecific.Warrior
{
    static class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }
        public static bool HasTalent(WarriorTalents tal) { return TalentManager.IsSelected((int)tal); }

        public static bool Tier14TwoPieceBonus { get { return Me.HasAura("Item - Warrior T14 DPS 2P Bonus"); } }
        public static bool Tier14FourPieceBonus { get { return Me.HasAura("Item - Warrior T14 DPS 4P Bonus"); } }      

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior)]
        public static Composite CreateWarriorNormalPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf(SelectedStance.ToString().CamelToSpaced(), ret => StyxWoW.Me.Shapeshift != (ShapeshiftForm)SelectedStance),
                    Spell.BuffSelf(Common.SelectedShout)
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


        public static Composite CreateWarriorEnragedRegeneration()
        {
            return new Decorator(
                req => Me.HealthPercent < WarriorSettings.WarriorEnragedRegenerationHealth && !Spell.IsSpellOnCooldown("Enraged Regeneration"),
                new Sequence(
                    new PrioritySelector(
                        Spell.BuffSelf("Berserker Rage"),
                        new ActionAlwaysSucceed()
                        ),
                    Spell.BuffSelf("Enraged Regeneration")
                    )
                );
        }

        public static string SelectedShout
        {
            get { return WarriorSettings.Shout.ToString().CamelToSpaced().Substring(1); }
        }

        public static WarriorStance  SelectedStance
        {
            get
            {
                var stance = WarriorSettings.Stance;
                if (stance == WarriorStance.Auto)
                {
                    switch (Me.Specialization)
                    {
                        case WoWSpec.WarriorArms:
                            stance = WarriorStance.BattleStance;
                            break;
                        case WoWSpec.WarriorFury:
                            stance = WarriorStance.BerserkerStance;
                            break;
                        default:
                        case WoWSpec.WarriorProtection:
                            stance = WarriorStance.DefensiveStance;
                            break;
                    }
                }

                return stance ;
            }
        }

        /// <summary>
        /// keep a single copy of Charge Behavior so the wrapping Throttle will account for 
        /// uses across multiple behaviors that reference this method
        /// </summary>
        private static Composite _singletonChargeBehavior = null;
        private static float _distanceChargeBehavior { get; set; }
       
        public static Composite CreateChargeBehavior()
        {
            _distanceChargeBehavior = Me.CombatReach + (TalentManager.HasGlyph("Long Charge") ? 30f : 25f);
            if (_singletonChargeBehavior == null)
            {
                _singletonChargeBehavior = new Throttle(TimeSpan.FromMilliseconds(1500),
                    new Decorator(
                        ret => MovementManager.IsClassMovementAllowed && Me.CurrentTarget != null && Me.CurrentTargetGuid != Singular.Utilities.EventHandlers.LastNoPathTarget,

                        new PrioritySelector(
                            // Charge to close distance
                            // note: use SpellDistance since charge is to a wowunit
                            Spell.Cast("Charge",
                                ret => !Me.CurrentTarget.HasAnyOfMyAuras("Charge Stun", "Warbringer")
                                    && Me.CurrentTarget.Distance.Between( 8, _distanceChargeBehavior) 
                                    && WarriorSettings.UseWarriorCloser),

                            //  Leap to close distance
                            // note: use Distance rather than SpellDistance since spell is to point on ground
                            Spell.CastOnGround("Heroic Leap",
                                on => Me.CurrentTarget,
                                req => !Me.HasAura("Charge")
                                    && Me.CurrentTarget.Distance.Between( 8, 40)
                                    && !Me.CurrentTarget.HasAnyOfMyAuras("Charge Stun", "Warbringer")
                                    && WarriorSettings.UseWarriorCloser,
                                false)
                            )
                        )
                    );
            }

            return _singletonChargeBehavior;
        }

        private static int _VictoryRushHealth = 0;

        public static Composite CreateVictoryRushBehavior()
        {
            int prevVRH = _VictoryRushHealth;

            _VictoryRushHealth = 90;
            if (SpellManager.HasSpell("Impending Victory"))
                _VictoryRushHealth = 80;
            if (TalentManager.HasGlyph("Victory Rush"))
                _VictoryRushHealth -= (100 - _VictoryRushHealth) / 2;
            if (WarriorSettings.VictoryRushOnCooldown)
                _VictoryRushHealth = 100;

            if (_VictoryRushHealth != prevVRH)
            {
                Logger.WriteDebug("VictoryRush: will cast if health <= {0}%", _VictoryRushHealth);
            }

            // health below determined %
            // user wants to cast on cooldown without regard to health
            // we have aura AND (target is about to die OR aura expires in less than 3 secs)
            return new Throttle( 
                new Decorator(
                    ret => WarriorSettings.VictoryRushOnCooldown
                        || Me.HealthPercent <= _VictoryRushHealth
                        || Me.HasAura("Victorious") && (Me.HasAuraExpired("Victorious", 3) || (Me.GotTarget && Me.CurrentTarget.TimeToDeath() < 7)),
                    new PrioritySelector(
                        Spell.Cast("Impending Victory"),
                        Spell.Cast("Victory Rush", ret => Me.HasAura("Victorious"))
                        )
                    )
                );
        }

        public static Composite CreateDisarmBehavior()
        {
            if ( !WarriorSettings.UseDisarm )
                return new ActionAlwaysFail();

            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
            {
                return new Throttle(15, 
                    Spell.Cast("Disarm", on => {
                        if (Spell.IsSpellOnCooldown("Disarm"))
                            return null;

                        WoWUnit unit = Unit.NearbyUnfriendlyUnits.FirstOrDefault(
                            u => u.IsWithinMeleeRange
                                && (u.IsMelee() || u.Class == WoWClass.Hunter)
                                && !Me.CurrentTarget.Disarmed 
                                && !Me.CurrentTarget.IsCrowdControlled()
                                && Me.IsSafelyFacing(u, 150)
                                );
                        return unit;
                        })
                    );
            }

            return new Throttle( 15, Spell.Cast("Disarm", req => !Me.CurrentTarget.Disarmed && !Me.CurrentTarget.IsCrowdControlled()));
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
                int friends = Unit.NearbyFriendlyPlayers.Count(f => f.IsAlive);
                if (friends < 3)
                    return false;

                int enemies = Unit.NearbyUnfriendlyUnits.Count();
                if (enemies < 3)
                    return false;

                int diff = Math.Abs(friends - enemies);
                return diff <= ((friends / 3) + 1);
            }
        }


        public static Composite CreateAttackFlyingOrUnreachableMobs()
        {
            return new Decorator(
                ret =>
                {
                    if (!Me.GotTarget)
                        return false;

                    if (Me.CurrentTarget.IsPlayer)
                        return false;

                    if (Me.CurrentTarget.IsFlying)
                    {
                        Logger.Write(Color.White, "Ranged Attack: {0} is Flying! using Ranged attack....", Me.CurrentTarget.SafeName());
                        return true;
                    }

                    if ((DateTime.Now - Singular.Utilities.EventHandlers.LastNoPathFailure).TotalSeconds < 1f)
                    {
                        Logger.Write(Color.White, "Ranged Attack: No Path Available error just happened, so using Ranged attack ....", Me.CurrentTarget.SafeName());
                        return true;
                    }
/*
                    if (Me.CurrentTarget.IsAboveTheGround())
                    {
                    Logger.Write(Color.White, "{0} is {1:F1) yds above the ground! using Ranged attack....", Me.CurrentTarget.SafeName(), Me.CurrentTarget.HeightOffTheGround());
                    return true;
                    }
*/
                    double heightCheck = Me.CurrentTarget.MeleeDistance();
                    if (Me.CurrentTarget.Distance2DSqr < heightCheck * heightCheck && Math.Abs(Me.Z - Me.CurrentTarget.Z) >= heightCheck )
                    {
                        Logger.Write(Color.White, "Ranged Attack: {0} appears to be off the ground! using Ranged attack....", Me.CurrentTarget.SafeName());
                        return true;
                    }
                    
                    WoWPoint dest = Me.CurrentTarget.Location;
                    if (!Me.CurrentTarget.IsWithinMeleeRange && !Styx.Pathing.Navigator.CanNavigateFully(Me.Location, dest))
                    {
                        Logger.Write(Color.White, "Ranged Attack: {0} is not Fully Pathable! using ranged attack....", Me.CurrentTarget.SafeName());
                        return true;
                    }

                    return false;
                },
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
                );
        }
    }

    enum WarriorTalents
    {
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
    }
}
