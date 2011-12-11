using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using TreeSharp;
using Action = TreeSharp.Action;
using Singular.Lists;
using Styx.WoWInternals.WoWObjects;

namespace Singular.ClassSpecific.Shaman
{
    class Enhancement
    {
        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.EnhancementShaman)]
        [Behavior(BehaviorType.PullBuffs)]
        [Context(WoWContext.All)]
        [Priority(500)]
        public static Composite CreateEnhancementShamanPullBuffs()
        {
            return new PrioritySelector(
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Lightning Shield"),

                //Removes the weapon enchant if the imbune is the wrong one and then attempts to rebuff
                //MainHand
                new Decorator(ret => !Item.HasWeapoinImbue(WoWInventorySlot.MainHand, "Windfury") &&
                    StyxWoW.Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id != 0,
                        new Action(ret =>
                        {
                            Logger.WriteDebug("Canceling " + StyxWoW.Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Name + " Main Hand Imbune");
                            Lua.DoString("CancelItemTempEnchantment(1)");
                        })),
                Spell.Cast("Windfury Weapon", ret => !Item.HasWeapoinImbue(WoWInventorySlot.MainHand, "Windfury")),

                //Offhand
                new Decorator(ret => !Item.HasWeapoinImbue(WoWInventorySlot.OffHand, "Flametongue") &&
                    StyxWoW.Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id != 0,
                        new Action(ret =>
                        {
                            Logger.WriteDebug("Canceling " + StyxWoW.Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Name + " OffHand Imbune");
                            Lua.DoString("CancelItemTempEnchantment(2)");
                        })),
                Spell.Cast("Flametongue Weapon", ret => !Item.HasWeapoinImbue(WoWInventorySlot.OffHand, "Flametongue"))
                );
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.EnhancementShaman)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        [Priority(500)]
        public static Composite CreateEnhancementShaman()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateFaceTargetBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Spell.WaitForCast(true),
                CreateEnhancementShamanPullBuffs(),
                Common.CreateAutoAttack(false),
                Totems.CreateSetTotems(3),

                // Only call if we're missing more than 2 totems. 

                Spell.Cast("Call of the Elements", ret => Totems.TotemsInRangeOf(StyxWoW.Me.CurrentTarget) < 3),

                Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                //Self Heals, can be turned off
                Spell.Cast("Feral Spirit", ret => SingularSettings.Instance.Shaman.CastOn != CastOn.Never &&
                    SingularSettings.Instance.Shaman.EnhancementHeal &&
                    StyxWoW.Me.HealthPercent <= 50),

                Spell.StopAndCast("Healing Surge", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent <= 50 &&
                    SingularSettings.Instance.Shaman.EnhancementHeal),

               //Aoe
                Spell.Cast("Chain Lightning",
                    ret => !StyxWoW.Me.CurrentTarget.IsNeutral &&
                        Clusters.GetClusterCount(StyxWoW.Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Chained, 10f) >= 2 &&
                        StyxWoW.Me.Auras["Maelstrom Weapon"].StackCount > 4),

                Spell.Cast("Fire Nova",
                    ret => Clusters.GetClusterCount(StyxWoW.Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 10f) >= 2 &&
                           Unit.NearbyUnfriendlyUnits.Count(u => u.HasMyAura("Flame Shock")) != 0),


                // Ensure Searing is nearby
                Spell.Cast("Searing Totem", ret => StyxWoW.Me.Totems.Count(t => t.WoWTotem == WoWTotem.Searing && t.Unit.Distance < 13) == 0),

                Movement.CreateMoveBehindTargetBehavior(),

                Spell.Cast("Stormstrike"),
                Spell.Cast("Lava Lash"),
                Spell.Cast("Unleash Elements"),

                // Cast if we have unleash flame buff or if we dont know the spell
                //cast if the target dosnt have the aura or has less than 4 seconds left
                Spell.Cast("Flame Shock",
                ret =>
                    (StyxWoW.Me.HasAura("Unleash Flame") || !SpellManager.HasSpell("Unleash Elements")) &&
                    (StyxWoW.Me.CurrentTarget.HasMyAura("Flame Shock") || StyxWoW.Me.GetAuraTimeLeft("Flame Shock", true).TotalSeconds < 4)),


                Spell.Cast("Lightning Bolt", ret => StyxWoW.Me.Auras["Maelstrom Weapon"].StackCount > 4),


                // Clip the last tick of FS if we can.
                Spell.Buff("Flame Shock", ret => StyxWoW.Me.HasAura("Unleash Flame") || !SpellManager.HasSpell("Unleash Elements")),


                Spell.Cast("Earth Shock",
                    ret => SpellManager.Spells["Unleash Elements"].Cooldown ||
                    !SpellManager.HasSpell("Unleash Elements")),

                //User selects when to cast
                Spell.Cast("Feral Spirit", ret =>
                    SingularSettings.Instance.Shaman.CastOn == CastOn.All ||
                    SingularSettings.Instance.Shaman.CastOn == CastOn.Bosses && BossList.BossIds.Contains(StyxWoW.Me.CurrentTarget.Entry) ||
                    SingularSettings.Instance.Shaman.CastOn == CastOn.Players && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.IsHostile),


                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

    }
}
