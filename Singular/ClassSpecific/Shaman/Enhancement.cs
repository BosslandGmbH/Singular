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
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public static Composite CreateEnhanceShamanRest()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => !StyxWoW.Me.HasAura("Drink") && !StyxWoW.Me.HasAura("Food"),
                        CreateEnhanceShamanHeal()),
                    Rest.CreateDefaultRestBehaviour(),
                    Spell.Resurrect("Ancestral Spirit")
                    );
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.EnhancementShaman)]
        [Behavior(BehaviorType.PullBuffs)]
        [Context(WoWContext.All)]
        [Priority(500)]
        public static Composite CreateEnhancementShamanPullBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Lightning Shield"),

                //Removes the weapon enchant if the imbune is the wrong one and then attempts to rebuff
                //MainHand
                new Decorator(
                    ret => SpellManager.HasSpell("Windfury Weapon") &&
                           !Item.HasWeaponImbue(WoWInventorySlot.MainHand, "Windfury") &&
                           StyxWoW.Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id != 0,
                    new Action(
                        ret =>
                            {
                                Logger.WriteDebug(
                                    "Canceling " + StyxWoW.Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Name + " Main Hand Imbune");
                                Lua.DoString("CancelItemTempEnchantment(1)");
                            })),
                Spell.Cast("Windfury Weapon", ret => !Item.HasWeaponImbue(WoWInventorySlot.MainHand, "Windfury")),
                // Low level support
                Spell.Cast(
                    "Flametongue Weapon",
                    ret => !SpellManager.HasSpell("Windfury Weapon") &&
                           !Item.HasWeaponImbue(WoWInventorySlot.MainHand, "Flametongue")),

                // Ignore off-hand if its not a weapon.
                new Decorator(
                    ret => StyxWoW.Me.Inventory.Equipped.OffHand.ItemInfo.ItemClass == WoWItemClass.Weapon,
                    new PrioritySelector(
                        //Offhand
                        new Decorator(
                            ret => !Item.HasWeaponImbue(WoWInventorySlot.OffHand, "Flametongue") &&
                                   StyxWoW.Me.Inventory.Equipped.OffHand != null &&
                                   StyxWoW.Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id != 0,
                            new Action(
                                ret =>
                                    {
                                        Logger.WriteDebug(
                                            "Canceling " + StyxWoW.Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Name + " OffHand Imbune");
                                        Lua.DoString("CancelItemTempEnchantment(2)");
                                    })),

                        Spell.Cast("Flametongue Weapon", ret => !Item.HasWeaponImbue(WoWInventorySlot.OffHand, "Flametongue"))
                        )));
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.EnhancementShaman)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.All)]
        public static Composite CreateEnhanceShamanHeal()
        {
            return
                new Decorator(
                    ret => SingularSettings.Instance.Shaman.EnhancementHeal,
                    new PrioritySelector(
                        // Heal the party in dungeons if the healer is dead
                        new Decorator(
                            ret => StyxWoW.Me.CurrentMap.IsDungeon && !StyxWoW.Me.IsInRaid &&
                                   (Group.Healer == null || !Group.Healer.IsAlive),
                            Restoration.CreateRestoShamanHealingOnlyBehavior()),

                        // This will work for both solo play and battlegrounds
                        new Decorator(
                            ret => !StyxWoW.Me.IsInParty || Group.Healer == null || !Group.Healer.IsAlive,
                            new PrioritySelector(
                                Spell.Heal("Healing Wave",
                                    ret => StyxWoW.Me,
                                    ret => !SpellManager.HasSpell("Healing Surge") && StyxWoW.Me.HealthPercent <= 60),

                                Spell.Heal("Healing Surge",
                                    ret => StyxWoW.Me,
                                    ret => StyxWoW.Me.HealthPercent <= 60)))
                        ));
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
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                CreateEnhancementShamanPullBuffs(),
                Common.CreateAutoAttack(false),
                Totems.CreateSetTotems(3),

                // Only call if we're missing more than 2 totems. 

                Spell.Cast("Call of the Elements", 
                    ret => StyxWoW.Me.CurrentTarget.Level > StyxWoW.Me.Level - 10 &&
                           StyxWoW.Me.CurrentTarget.Distance < 15 && 
                           Totems.TotemsInRangeOf(StyxWoW.Me.CurrentTarget) < 3),

                Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                //Self Heals, can be turned off
                Spell.Cast("Feral Spirit", ret => SingularSettings.Instance.Shaman.CastOn != CastOn.Never &&
                    SingularSettings.Instance.Shaman.EnhancementHeal &&
                    StyxWoW.Me.HealthPercent <= 50),

               //Aoe
                Spell.Cast("Chain Lightning",
                    ret => !StyxWoW.Me.CurrentTarget.IsNeutral &&
                        Clusters.GetClusterCount(StyxWoW.Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Chained, 10f) >= 2 &&
                        StyxWoW.Me.Auras["Maelstrom Weapon"].StackCount > 4),

                Spell.Cast("Fire Nova",
                    ret => Clusters.GetClusterCount(StyxWoW.Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 10f) >= 2 &&
                           Unit.NearbyUnfriendlyUnits.Count(u => u.HasMyAura("Flame Shock")) != 0),


                // Ensure Searing is nearby
                Spell.Cast("Searing Totem", 
                             ret => StyxWoW.Me.CurrentTarget.Distance < 15 && 
                                    StyxWoW.Me.Totems.Count(t => t.WoWTotem == WoWTotem.Searing && t.Unit.Distance < 13) == 0),

                Movement.CreateMoveBehindTargetBehavior(),

                Spell.Cast("Primal Strike", ret => !SpellManager.HasSpell("Stormstrike")),
                Spell.Cast("Stormstrike"),
                Spell.Cast("Lava Lash"),
                Spell.Cast("Unleash Elements"),

                // Cast if we have unleash flame buff or if we dont know the spell
                //cast if the target dosnt have the aura or has less than 4 seconds left
                Spell.Cast("Flame Shock",
                    ret => StyxWoW.Me.HasAura("Unleash Flame") || StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds < 4),


                Spell.Cast("Lightning Bolt", ret => StyxWoW.Me.HasMyAura("Maelstrom Weapon", 5)),


                Spell.Cast("Earth Shock",
                    ret => !SpellManager.HasSpell("Unleash Elements") || SpellManager.Spells["Unleash Elements"].Cooldown),

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
