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

namespace Singular.ClassSpecific.Warrior
{
    static class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }

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
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.Buff("Berserker Rage", ret => Me.Fleeing),
                    Spell.Buff("Enraged Regeneration", ret => Me.Stunned && StyxWoW.Me.HealthPercent < 60)
                    )
                );
        }
        

        public static string SelectedShout
        {
            get { return SingularSettings.Instance.Warrior().Shout.ToString().CamelToSpaced(); }
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

        public static Composite CreateChargeBehavior()
        {
            return new Throttle(TimeSpan.FromMilliseconds(500),
                new Decorator(
                    ret => Me.CurrentTarget != null,

                    new PrioritySelector(
                        Spell.Cast("Charge",
                            ret => MovementManager.IsClassMovementAllowed
                                && Me.CurrentTarget.SpellDistance() >= 10 && Me.CurrentTarget.SpellDistance() < (TalentManager.HasGlyph("Long Charge") ? 30f : 25f)
                                && WarriorSettings.UseWarriorCloser),

                        Spell.CastOnGround("Heroic Leap",
                            ret => Me.CurrentTarget.Location,
                            ret => MovementManager.IsClassMovementAllowed
                                && Me.CurrentTarget.SpellDistance() > 9 && !Me.CurrentTarget.HasAura("Charge Stun", 1)
                                && WarriorSettings.UseWarriorCloser),

                        Spell.Cast("Heroic Throw",
                            ret => !Unit.HasAura(Me.CurrentTarget, "Charge Stun"))
                        )
                    )
                );
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
    }
}
