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
    public static class Fury
    {
        [Spec(TalentSpec.FuryWarrior)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        public static Composite CreateFuryCombat()
        {
            return new PrioritySelector(
                // face target
                Movement.CreateFaceTargetBehavior(),                
                // ranged interupt
                Spell.Buff(
                    "Intimidating Shout", ret => StyxWoW.Me.CurrentTarget.Distance < 8 &&
                                                 StyxWoW.Me.CurrentTarget.IsPlayer &&
                                                 StyxWoW.Me.CurrentTarget.IsCasting),
                //ranged deeps
                Spell.Cast("Heroic Throw", ret => StyxWoW.Me.CurrentTarget.IsPlayer),
                // ranged slow
                Spell.Buff("Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 &&
                                                   StyxWoW.Me.CurrentTarget.IsPlayer &&
                                                  (!StyxWoW.Me.CurrentTarget.HasAura("Hamstring") ||
                                                   !StyxWoW.Me.CurrentTarget.HasAura("Piercing Howl") ||
                                                   !StyxWoW.Me.CurrentTarget.HasAura("Slowing Poison") ||
                                                   !StyxWoW.Me.CurrentTarget.HasAura("Hand of Freedom"))),
                // melee slow
                Spell.Buff("Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer &&
                                               (!StyxWoW.Me.CurrentTarget.HasAura("Hamstring") ||
                                               !StyxWoW.Me.CurrentTarget.HasAura("Piercing Howl") ||
                                               !StyxWoW.Me.CurrentTarget.HasAura("Slowing Poison") ||
                                               !StyxWoW.Me.CurrentTarget.HasAura("Hand of Freedom"))),
                
                // Intercept
                Spell.Cast("Intercept", ret => StyxWoW.Me.CurrentTarget.Distance > 10),
                //Heroic Leap
                new Decorator(
                ret => SpellManager.CanCast("Heroic Leap") && StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura("Intercept"),
                new Action(
                        ret =>
                        {
                            SpellManager.Cast("Heroic Leap");
                            LegacySpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                        })),
                // Fury of angerforge
                new Decorator(
                    ret => Unit.HasAura(StyxWoW.Me, "Raw Fury", 5) &&
                           StyxWoW.Me.Inventory.Equipped.Trinket1 != null &&
                           StyxWoW.Me.Inventory.Equipped.Trinket1.Name.Contains("Fury of Angerforge") &&
                           StyxWoW.Me.Inventory.Equipped.Trinket1.Cooldown <= 0,
                    new Action(
                        ret =>
                        {
                            StyxWoW.Me.Inventory.Equipped.Trinket1.Use();
                        })),
                new Decorator(
                    ret => Unit.HasAura(StyxWoW.Me, "Raw Fury", 5) &&
                           StyxWoW.Me.Inventory.Equipped.Trinket2 != null &&
                           StyxWoW.Me.Inventory.Equipped.Trinket2.Name.Contains("Fury of Angerforge") &&
                           StyxWoW.Me.Inventory.Equipped.Trinket2.Cooldown <= 0,
                    new Action(
                        ret =>
                        {
                            StyxWoW.Me.Inventory.Equipped.Trinket2.Use();
                        })),
                // interupts
                Spell.Cast(
                    "Pummel", ret => StyxWoW.Me.CurrentTarget.IsCasting ||
                                     StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                Spell.Cast(
                    "Arcane Torrent", ret => StyxWoW.Me.CurrentTarget.IsCasting ||
                                             StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                Spell.Cast(
                    "War Stomp", ret => StyxWoW.Me.CurrentTarget.IsCasting ||
                                        StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                //Heal up in mele
                Spell.Cast("Victory Rush", ret => StyxWoW.Me.HealthPercent < 80),
                // use incite or dump rage
                new Decorator(
                    ret => Unit.HasAura(StyxWoW.Me, "Incite", 1) || StyxWoW.Me.RagePercent > 60,
                    new PrioritySelector(
                        Spell.Cast("Heroic Strike"))),
                // eng gloves
                Item.UseEquippedItem(9),
                //Rotation under 20%
                Spell.Buff("Colossus Smash"),
                Spell.Cast("Execute", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                //Rotation over 20%
                Spell.Cast("Raging Blow"),
                Spell.Buff("Bloodthirst"),
                new Decorator(
                    ret => Unit.HasAura(StyxWoW.Me, "Bloodsurge", 1),
                    new PrioritySelector(
                        Spell.Cast("Slam"))),
                Movement.CreateMoveToTargetBehavior(true, 4f)
                );
        }

        [Spec(TalentSpec.FuryWarrior)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        public static Composite CreateFuryPull()
        {
            return new PrioritySelector(
                //face target
                Movement.CreateFaceTargetBehavior(),
                //Dismount
                new Decorator(ret => StyxWoW.Me.Mounted,
                    new Action(o => Styx.Logic.Mount.Dismount())),
                //Shoot flying targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying,
                    new PrioritySelector(
                        Spell.Cast("Shoot"),
                        Spell.Cast("Throw")
                    )),
                //Buff up
                Spell.BuffSelf("Battle Shout", ret => StyxWoW.Me.RagePercent < 20),
                //Close gap
                FuryCloseGap(),
                //Move to mele and face
                Movement.CreateMoveToTargetBehavior(true, 4f)
                );
        }

        [Spec(TalentSpec.FuryWarrior)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
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
                //FuryRemoveCC(),
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
                new Decorator(
                    ret => !Unit.HasAura(StyxWoW.Me, "Enrage", 1) &&
                           !Unit.HasAura(StyxWoW.Me, "Berserker Rage", 1) &&
                           !Unit.HasAura(StyxWoW.Me, "Death Wish", 1),
                    new PrioritySelector(
                        Spell.BuffSelf("Berserker Rage"))),
                //Battleshout Check
                Spell.BuffSelf(
                    "Battle Shout", ret => !StyxWoW.Me.HasAura("Horn of the Winter") &&
                                           !StyxWoW.Me.HasAura("Roar of Courage") &&
                                           !StyxWoW.Me.HasAura("Strength of Earth Totem"))
                );
        }

        [Spec(TalentSpec.FuryWarrior)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
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
                    Movement.CreateMoveToTargetBehavior(true, 4f)
                    )),
                // Heroic fury
                Spell.Cast("Heroic Fury", ret => SpellManager.Spells["Intercept"].Cooldown),
                //Intercept
                Spell.Cast("Intercept", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 &&
                                               StyxWoW.Me.CurrentTarget.Distance <= 25),
                //Heroic Leap
                new Decorator(
                ret => SpellManager.CanCast("Heroic Leap") && StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura("Intercept"),
                new Action(
                        ret =>
                        {
                            SpellManager.Cast("Heroic Leap");
                            LegacySpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                        })),
                //Heroic Throw if not already Intercepting
                Spell.Cast("Heroic Throw", ret => !StyxWoW.Me.CurrentTarget.HasAura("Intercept")),
                //Worgen Racial
                Spell.Cast(
                    "Darkflight", ret => StyxWoW.Me.CurrentTarget.IsPlayer &&
                                         StyxWoW.Me.CurrentTarget.Distance > 15),
                //Move to mele and face
                Movement.CreateMoveToTargetBehavior(true, 4f)
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
                // TODO: Heroic Fury  
                //Spell.Buff(
                //    "Every Man for Himself",
                //    ret => StyxWoW.Me.Auras.Any(
                //        aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Asleep ||
                //                aura.Value.Spell.Mechanic == WoWSpellMechanic.Stunned ||
                //                aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted))
                // TODO: Human Racial
                // TODO: Undead Racial
                // TODO: Gnome Racial
                // TODO: Berserker rage to get out of fear
                );
        }


    }
}
