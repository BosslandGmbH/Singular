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

namespace Singular.ClassSpecific.Warrior
{
    static class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior; } }


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior)]
        public static Composite CreateWarriorNormalPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf(SelectedStance.ToString().CamelToSpaced(), ret => StyxWoW.Me.Shapeshift != (ShapeshiftForm)SelectedStance),
                    Spell.BuffSelf(Common.SelectedShout)
                    );
        }


        public static string SelectedShout
        {
            get { return SingularSettings.Instance.Warrior.Shout.ToString().CamelToSpaced(); }
        }

        public static WarriorStance  SelectedStance
        {
            get
            {
                var stance = SingularSettings.Instance.Warrior.Stance;
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
                            ret => SingularSettings.Instance.IsCombatRoutineMovementAllowed()
                                && Me.CurrentTarget.Distance >= 10 && Me.CurrentTarget.Distance < (TalentManager.HasGlyph("Long Charge") ? 30f : 25f)
                                && WarriorSettings.UseWarriorCloser),

                        Spell.CastOnGround("Heroic Leap",
                            ret => Me.CurrentTarget.Location,
                            ret => SingularSettings.Instance.IsCombatRoutineMovementAllowed()
                                && Me.CurrentTarget.Distance > 9 && !Me.CurrentTarget.HasAura("Charge Stun", 1)
                                && WarriorSettings.UseWarriorCloser),

                        Spell.Cast("Heroic Throw",
                            ret => !Unit.HasAura(Me.CurrentTarget, "Charge Stun"))
                        )
                    )
                );
        }


    }
}
