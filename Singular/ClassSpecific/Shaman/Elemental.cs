using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Shaman
{
    class Elemental
    {
        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Behavior(BehaviorType.PullBuffs)]
        //[Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateElementalPullBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Lightning Shield"),
                Spell.Cast("Flametongue Weapon", ret => !Item.HasWeapoinImbue(WoWInventorySlot.MainHand, "Flametongue"))
                //new LogMessage("Flametongue done!"),
                );
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateShamanBuffs()
        {
            return new PrioritySelector(
                Spell.Cast("Ghost Wolf", ret=> !CharacterSettings.Instance.UseMount && StyxWoW.Me.Shapeshift == ShapeshiftForm.Normal)
                );
        }

        private static WoWSpell LavaBurst { get { return SpellManager.Spells["Lava Burst"]; } }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateElementalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Spell.WaitForCast(true),
                CreateElementalPullBuffs(),
                Spell.Cast("Call of the Elements", ret => StyxWoW.Me.Totems.Count(t =>t != null && t.Unit != null && t.Unit.Distance < 20) == 0),
                Spell.Cast("Wind Shear", ret => StyxWoW.Me.CurrentTarget.IsCasting),

                Spell.Cast("Thunderstorm", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 10) > 2),
                Spell.Cast("Thunderstorm", ret => StyxWoW.Me.ManaPercent < 40),

                // Ensure Searing is nearby
                Spell.Cast("Searing Totem", ret => StyxWoW.Me.Totems.Count(t => t.WoWTotem == WoWTotem.Searing && t.Unit.Distance < 13) == 0),

                Spell.Cast("Earth Shock", ret => StyxWoW.Me.HasAura("Lightning Shield", 9)),

                // Clip the last tick of FS if we can.
                Spell.Buff("Flame Shock", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds < 3),

                Spell.Cast("Fire Nova", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 10) > 2),

                Spell.Cast("Unleash Elements", ret => Item.HasWeapoinImbue(WoWInventorySlot.MainHand, "Flametongue") && !LavaBurst.Cooldown),
                Spell.Cast("Elemental Mastery", ret=> StyxWoW.Me.IsMoving && !LavaBurst.Cooldown),
                // Pretty much no matter what it is, just use on CD
                Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                Movement.CreateEnsureMovementStoppedBehavior(),
                Spell.Cast("Lava Burst"),

                // Ignore this, its useless and a DPS loss. Its ont he GCD and gains nothing from our SP, crit, or any other modifiers. 
                //Spell.Cast("Rocket Barrage"),
                Spell.Cast("Lightning Bolt"),

                Movement.CreateMoveToTargetBehavior(true, 38f)
                );
        }
    }
}
