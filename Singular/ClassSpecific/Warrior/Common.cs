using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.Common.Helpers;
using Styx.Helpers;

namespace Singular.ClassSpecific.Warrior
{
    static class Common
    {
        private static readonly WaitTimer ChargeTimer = new WaitTimer(TimeSpan.FromMilliseconds(2000));

        public static bool PreventDoubleCharge
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
    }
}
