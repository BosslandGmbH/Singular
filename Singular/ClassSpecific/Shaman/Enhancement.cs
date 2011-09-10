using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular.ClassSpecific.Shaman
{
    class Enhancement
    {
        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.EnhancementShaman)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateElementalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Spell.WaitForCast(true),
                // Only call if we're missing more than 2 totems. 
                Spell.Cast("Call of the Elements", ret => Totems.TotemsInRange < 3),
                Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Ensure Searing is nearby
                Spell.Cast("Searing Totem", ret => StyxWoW.Me.Totems.Count(t => t.WoWTotem == WoWTotem.Searing && t.Unit.Distance < 13) == 0 && !StyxWoW.Me.Totems.Any(t => t.WoWTotem == WoWTotem.FireElemental)),
                // Pop the ele on bosses
                Spell.Cast("Fire Elemental Totem", ret => StyxWoW.Me.CurrentTarget.IsBoss()),

                Spell.Cast("Stormstrike"),
                Spell.Cast("Lava Lash"),

                Spell.Cast("Lightning Bolt", ret=>StyxWoW.Me.HasAura("Maelstrom Weapon", 5)),

                // Clip the last tick of FS if we can.
                Spell.Buff("Flame Shock", ret => StyxWoW.Me.HasAura("Unleash Flame")),

                Spell.Cast("Unleash Elements"),
                Spell.Cast("Earth Shock"),
                Spell.Cast("Feral Spirit"),

                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }
    }
}
