using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {

        public Composite CreateElementalShamanCombat()
        {
            return new PrioritySelector(
                CreateRangeAndFace(39, ret => Me.CurrentTarget),
                CreateWaitForCast(),

                CreateSpellCast("Elemental Mastery"),
                CreateSpellBuff("Flame Shock"),
                CreateSpellCast("Unleash Elements"),
                CreateSpellCast("Lava Burst"),
                CreateSpellBuff("Searing Totem"),
                CreateSpellCast("Earth Shock", ret => HasAuraStacks("Lightning Shield", 6)),
                CreateSpellCast("Lightning Bolt")

                );
        }

        public Composite CreateElementalShamanBuffs()
        {
            return new PrioritySelector(
                CreateSpellBuff("Lightning Shield"),

                CreateSpellCast("Flametongue Weapon", ret => Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Name != "Flametongue Weapon")

                );
        }
    }
}
