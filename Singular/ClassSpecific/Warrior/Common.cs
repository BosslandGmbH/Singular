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
                Spell.BuffSelf("Enraged Regeneration", ret => Me.Stunned && StyxWoW.Me.HealthPercent < 60)
                );
        }
        

        public static string SelectedShout
        {
            get { return SingularSettings.Instance.Warrior().Shout.ToString().CamelToSpaced().Substring(1); }
        }

        public static WarriorStance  SelectedStance
        {
            get
            {
                var stance = SingularSettings.Instance.Warrior().Stance;
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
       
        public static Composite CreateChargeBehavior()
        {
            if (_singletonChargeBehavior == null)
            {
                _singletonChargeBehavior = new Throttle(TimeSpan.FromMilliseconds(1500),
                    new Decorator(
                        ret => Me.CurrentTarget != null,

                        new PrioritySelector(
                            Spell.Cast("Charge",
                                ret => MovementManager.IsClassMovementAllowed
                                    && !Me.CurrentTarget.HasMyAura("Charge Stun")
                                    && Me.CurrentTarget.SpellDistance() >= 10 && Me.CurrentTarget.SpellDistance() < (TalentManager.HasGlyph("Long Charge") ? 30f : 25f)
                                    && WarriorSettings.UseWarriorCloser),

                            Spell.CastOnGround("Heroic Leap",
                                on => Me.CurrentTarget,
                                req => MovementManager.IsClassMovementAllowed
                                    && !Me.HasAura("Charge")
                                    && Me.CurrentTarget.SpellDistance() > 9
                                    && !Me.CurrentTarget.HasMyAura("Charge Stun")
                                    && WarriorSettings.UseWarriorCloser,
                                false),

                            Spell.Cast("Heroic Throw",
                                ret => !Me.CurrentTarget.HasMyAura("Charge Stun")
                                    && !Me.HasAura("Charge")
                                )
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
                int enemies = Unit.NearbyUnfriendlyUnits.Count();

                if (friends < 3 || enemies < 3)
                    return false;

                int readyfriends = Unit.NearbyFriendlyPlayers.Count(f => f.IsAlive);
                if (readyfriends < 3)
                    return false;

                int diff = Math.Abs(friends - enemies);
                return diff <= ((friends / 3) + 1);
            }
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
