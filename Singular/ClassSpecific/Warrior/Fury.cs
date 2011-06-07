using Singular.Dynamics;
using Singular.Helpers;
using Singular.Lists;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;

using TreeSharp;
using Styx.Logic.Combat;

namespace Singular.ClassSpecific.Warrior
{
    public class Fury
    {
        private static string[] _slows;
        [Spec(TalentSpec.FuryWarrior)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateFuryCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom" };
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // LOS check
                Movement.CreateMoveToLosBehavior(),
                // Auto Attack
                Common.CreateAutoAttack(false),
                // Low level support
                new Decorator(
                    ret => StyxWoW.Me.Level < 30,
                    new PrioritySelector(                        
                        Spell.Cast("Victory Rush"),
                        Spell.Cast("Execute"),
                        Spell.Buff("Rend"),
                        Spell.Cast("Overpower"),
                        Spell.Cast("Bloodthirst"),
                        //rage dump
                        Spell.Cast("Thunder Clap", ret => StyxWoW.Me.RagePercent > 50 && Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) > 3),
                        Spell.Cast("Heroic Strike", ret => StyxWoW.Me.RagePercent > 60),
                        Movement.CreateMoveToTargetBehavior(true, 5f))),
                //30-50 support
                Spell.BuffSelf("Berserker Stance", ret => StyxWoW.Me.Level > 30 && StyxWoW.Me.Level < 50),
                // ranged interupt
                Spell.Buff("Intimidating Shout", ret => StyxWoW.Me.CurrentTarget.Distance < 8 && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.IsCasting),
                // ranged slow
                Spell.Buff("Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer && !Unit.HasAnyAura(StyxWoW.Me.CurrentTarget, _slows)),
                // melee slow
                Spell.Buff("Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer && !Unit.HasAnyAura(StyxWoW.Me.CurrentTarget, _slows)),
                // Intercept
                Spell.Cast("Intercept", ret => StyxWoW.Me.CurrentTarget.Distance > 10),
                //Heroic Leap
                Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.CurrentTarget.Distance > 9 && !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Intercept", 1)),
                // Fury of angerforge
                new Decorator(
                    ret => Unit.HasAura(StyxWoW.Me, "Raw Fury", 5) &&
                           StyxWoW.Me.Inventory.Equipped.Trinket1 != null &&
                           StyxWoW.Me.Inventory.Equipped.Trinket1.ItemInfo.Id.Equals(59461) &&
                           StyxWoW.Me.Inventory.Equipped.Trinket1.Cooldown <= 0,
                    new Action(ret => StyxWoW.Me.Inventory.Equipped.Trinket1.Use())),
                new Decorator(
                    ret => Unit.HasAura(StyxWoW.Me, "Raw Fury", 5) &&
                           StyxWoW.Me.Inventory.Equipped.Trinket2 != null &&
                           StyxWoW.Me.Inventory.Equipped.Trinket2.ItemInfo.Id.Equals(59461) &&
                           StyxWoW.Me.Inventory.Equipped.Trinket2.Cooldown <= 0,
                    new Action(ret => StyxWoW.Me.Inventory.Equipped.Trinket2.Use())),
                //Interupts
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsCasting,
                    new PrioritySelector(
                        Spell.Cast("Pummel"),
                        Spell.Cast("Arcane Torrent"),
                        Spell.Cast("War Stomp"))),
                //Heal up in mele
                Spell.Cast("Victory Rush", ret => StyxWoW.Me.HealthPercent < 80),
                // AOE
                new Decorator(
                    ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3,
                    new PrioritySelector(
                        Spell.BuffSelf("Recklessness"),
                        Spell.Cast("Whirlwind"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Raging Blow"),
                        Spell.Cast("Bloodthirst"))),                
                // Engineering gloves bug out if you do not have engineering enchant on the glove
                //Item.UseEquippedItem(9),
                //Rotation under 20%
                Spell.Buff("Colossus Smash"),
                Spell.Cast("Execute"),
                //Rotation over 20%
                Spell.Cast("Heroic Strike", ret => Unit.HasAura(StyxWoW.Me, "Incite", 1) || StyxWoW.Me.RagePercent > 60),
                Spell.Cast("Raging Blow"),
                Spell.Buff("Bloodthirst"),
                Spell.Cast("Slam", ret => Unit.HasAura(StyxWoW.Me, "Bloodsurge", 1)),
                //Move to Melee
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }

        [Spec(TalentSpec.FuryWarrior)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateFuryPull()
        {
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                //face target
                Movement.CreateFaceTargetBehavior(),
                // LOS check
                Movement.CreateMoveToLosBehavior(),
                // Auto Attack
                Common.CreateAutoAttack(false),
                //Shoot flying targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Spell.Cast("Heroic Throw"),
                        Spell.Cast("Shoot"),
                        Spell.Cast("Throw")
                    )),
                //low level support
                new Decorator(
                    ret => StyxWoW.Me.Level < 50,
                    new PrioritySelector(
                        Spell.BuffSelf("Battle Stance"),
                        Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance > 10 && StyxWoW.Me.CurrentTarget.Distance <= 25),
                        Spell.Cast("Heroic Throw", ret => !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun")),
                        Movement.CreateMoveToTargetBehavior(true, 5f))),
                //Close gap
                FuryCloseGap()
                );
        }

        [Spec(TalentSpec.FuryWarrior)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateFuryCombatBuffs()
        {
            return new PrioritySelector(
                //Check Heal
                FuryHeal(),
                //Troll Racial
                Spell.BuffSelf("Berserking"),
                //Recklessness if low on hp or have Deathwish up or as gank protection
                Spell.BuffSelf("Recklessness",
                        ret => StyxWoW.Me.HasAura("Death Wish") && 
                               StyxWoW.Me.HealthPercent > 20 ||
                               StyxWoW.Me.CurrentTarget.IsPlayer),
                //Inner rage to dump rage
                Spell.BuffSelf("Inner Rage", ret => StyxWoW.Me.RagePercent > 80),
                //Remove Croud Control Effects
                FuryRemoveCC(),
                //Dwarf Racial
                Spell.BuffSelf("Stoneform", ret => StyxWoW.Me.HealthPercent < 60),
                //Night Elf Racial
                Spell.BuffSelf("Shadowmeld", ret => StyxWoW.Me.HealthPercent < 20),
                //Orc Racial
                Spell.BuffSelf("Blood Fury"),
                //Deathwish, for both grinding and gank protection
                Spell.BuffSelf(
                    "Death Wish", ret => (StyxWoW.Me.CurrentTarget.MaxHealth > StyxWoW.Me.MaxHealth &&
                                          StyxWoW.Me.CurrentTarget.HealthPercent < 95 &&
                                          StyxWoW.Me.RagePercent > 50) ||
                                         (StyxWoW.Me.CurrentTarget.MaxHealth > StyxWoW.Me.MaxHealth &&
                                          StyxWoW.Me.HealthPercent > 10 && StyxWoW.Me.HealthPercent < 75) ||
                                          StyxWoW.Me.CurrentTarget.IsPlayer),
                //Berserker rage to stay enraged
                Spell.BuffSelf("Berserker Rage", ret => !Unit.HasAnyAura(StyxWoW.Me, "Enrage", "Berserker Rage", "Death Wish")),
                //Battleshout Check
                Spell.BuffSelf("Battle Shout", ret => !Unit.HasAnyAura(StyxWoW.Me, "Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout"))
                );
        }

        [Spec(TalentSpec.FuryWarrior)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateFuryPreCombatBuffs()
        {
            return new PrioritySelector(
                //Buff up
                Spell.BuffSelf("Battle Shout")
                );
        }




        public static Composite FuryCloseGap()
        {
            return new PrioritySelector(
                //Moves to target if you are too close or far
                new Decorator(
                ret => StyxWoW.Me.CurrentTarget.Distance < 10 || StyxWoW.Me.CurrentTarget.Distance > 40,
                new PrioritySelector(
                    Movement.CreateMoveToTargetBehavior(true, 5f)
                    )),
                // Heroic fury
                Spell.Cast("Heroic Fury", ret => SpellManager.Spells["Intercept"].Cooldown),
                //Intercept
                Spell.Cast("Intercept", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25),
                //Heroic Leap
                Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.CurrentTarget.Distance > 9 && !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Intercept", 1)),
                //Heroic Throw if not already Intercepting
                Spell.Cast("Heroic Throw", ret => !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Intercept", 1)),
                //Worgen Racial
                Spell.Cast("Darkflight", ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.Distance > 15),
                //Move to melee
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }

        public static Composite FuryHeal()
        {
            return new PrioritySelector(
                //Herbalist Heal
                Spell.Buff("Lifeblood", ret => StyxWoW.Me.HealthPercent < 70),
                //Draenai Heal
                Spell.Buff("Gift of the Naaru", ret => StyxWoW.Me.HealthPercent < 50),
                //Heal
                Spell.Buff("Enraged Regeneration", ret => StyxWoW.Me.HealthPercent < 60)
                );
        }
        
        public static Composite FuryRemoveCC()
        {
            return new PrioritySelector(
                // Heroic Fury
                Spell.BuffSelf(
                    "Heroic Fury",
                    ret => Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Rooted)),
                // Human Racial
                Spell.BuffSelf(
                    "Every Man for Himself",
                    ret => Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Asleep, 
                                                                WoWSpellMechanic.Stunned, 
                                                                WoWSpellMechanic.Rooted)),
                // Undead Racial
                Spell.BuffSelf(
                    "Will of the Forsaken", 
                    ret => Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Charmed,
                                                                WoWSpellMechanic.Asleep,
                                                                WoWSpellMechanic.Horrified,
                                                                WoWSpellMechanic.Fleeing)),
                // Gnome Racial
                Spell.BuffSelf(
                    "Escape Artist",
                    ret => Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Slowed,
                                                                WoWSpellMechanic.Rooted)),
                // Fear Remover
                Spell.BuffSelf(
                    "Berserker Rage",
                    ret => Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Fleeing,
                                                                WoWSpellMechanic.Sapped,
                                                                WoWSpellMechanic.Incapacitated,
                                                                WoWSpellMechanic.Horrified))
                );
        }


    }
}
