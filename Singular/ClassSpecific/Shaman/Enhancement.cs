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
        [Behavior(BehaviorType.PullBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateEnhancementShamanPullBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Lightning Shield"),
                Spell.Cast("Windfury Weapon", ret => !Item.HasWeapoinImbue(WoWInventorySlot.MainHand, "Windfury")),
                Spell.Cast("Flametongue Weapon", ret => !Item.HasWeapoinImbue(WoWInventorySlot.OffHand, "Flametongue"))
                );
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.EnhancementShaman)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateEnhancementShaman()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateFaceTargetBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Spell.WaitForCast(true),
                CreateEnhancementShamanPullBuffs(),
                Common.CreateAutoAttack(false),
                // Only call if we're missing more than 2 totems. 
                Spell.Cast("Call of the Elements", ret => Totems.TotemsInRangeOf(StyxWoW.Me.CurrentTarget) < 3),
                Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                //Aoe
                Spell.Cast("Chain Lightning",
                    ret => Clusters.GetClusterCount(StyxWoW.Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Chained, 10f) >= 2 &&
                           StyxWoW.Me.Auras["Maelstrom Weapon"].StackCount > 4 && StyxWoW.Me.ActiveAuras.ContainsKey("Maelstrom Weapon")),
                Spell.Cast("Fire Nova",
                    ret => Clusters.GetClusterCount(StyxWoW.Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 10f) >= 2 &&
                           StyxWoW.Me.CurrentTarget.HasMyAura("Flame Shock")),

                // Ensure Searing is nearby
                Spell.Cast("Searing Totem", ret => StyxWoW.Me.Totems.Count(t => t.WoWTotem == WoWTotem.Searing && t.Unit.Distance < 13) == 0 && !StyxWoW.Me.Totems.Any(t => t.WoWTotem == WoWTotem.FireElemental)),
                // Pop the ele on bosses
                Spell.Cast("Fire Elemental Totem", ret => StyxWoW.Me.CurrentTarget.IsBoss()),

                Spell.Cast("Stormstrike"),
                Spell.Cast("Lava Lash"),

                Spell.Cast("Lightning Bolt", ret => StyxWoW.Me.Auras["Maelstrom Weapon"].StackCount > 4 && StyxWoW.Me.ActiveAuras.ContainsKey("Maelstrom Weapon")),

                // Clip the last tick of FS if we can.
                Spell.Buff("Flame Shock", ret => StyxWoW.Me.HasAura("Unleash Flame") || !Styx.Logic.Combat.SpellManager.HasSpell("Unleash Elements")),

                Spell.Cast("Unleash Elements"),

                Spell.Cast("Earth Shock"),
                Spell.Cast("Feral Spirit"),

                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }
    }
}
