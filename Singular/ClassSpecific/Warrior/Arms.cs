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
    public static class Arms
    {
        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        public static Composite CreateArmsCombat()
        {
            return new PrioritySelector(
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Ranged interupt on players
                Spell.Buff(
                    "Intimidating Shout", ret => StyxWoW.Me.CurrentTarget.Distance < 8 &&
                                                 StyxWoW.Me.CurrentTarget.IsPlayer &&
                                                 StyxWoW.Me.CurrentTarget.IsCasting),
                // Dispel Bubbles
                Spell.Cast(
                    "Shattering Throw", ret => StyxWoW.Me.CurrentTarget.IsPlayer &&
                                               (StyxWoW.Me.CurrentTarget.HasAura("Ice Block") ||
                                               StyxWoW.Me.CurrentTarget.HasAura("Hand of Protection") ||
                                               StyxWoW.Me.CurrentTarget.HasAura("Divine Shield"))),
                //Rocket belt!
                new Decorator(
                    ret =>
                        StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.Distance > 20,
                        new PrioritySelector(
                            Item.UseEquippedItem(10)
                        )),
                // ranged slow
                Spell.Buff(
                    "Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 &&
                                            StyxWoW.Me.CurrentTarget.IsPlayer &&
                                            (!StyxWoW.Me.CurrentTarget.HasAura("Hamstring") ||
                                             !StyxWoW.Me.CurrentTarget.HasAura("Piercing Howl") ||
                                             !StyxWoW.Me.CurrentTarget.HasAura("Slowing Poison") ||
                                             !StyxWoW.Me.CurrentTarget.HasAura("Hand of Freedom"))),
                //Charge
                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 &&
                                            StyxWoW.Me.CurrentTarget.Distance <= 25),
                //Heroic Leap
                new Decorator(
                    ret => SpellManager.CanCast("Heroic Leap") && StyxWoW.Me.CurrentTarget.Distance > 9 && !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Charge Stun", 1),
                    new Action(
                            ret =>
                            {
                                SpellManager.Cast("Heroic Leap");
                                LegacySpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                            })),
                //use it or lose it
                new Decorator(
                    ret => Unit.HasAura(StyxWoW.Me, "Sudden Death", 1),
                    new PrioritySelector(
                        Spell.Cast("Colossus Smash"))),
                // Mele slow
                Spell.Cast(
                    "Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer &&
                                        (!StyxWoW.Me.CurrentTarget.HasAura("Hamstring") ||
                                         !StyxWoW.Me.CurrentTarget.HasAura("Piercing Howl") ||
                                         !StyxWoW.Me.CurrentTarget.HasAura("Slowing Poison") ||
                                         !StyxWoW.Me.CurrentTarget.HasAura("Hand of Freedom"))),
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
                //Mele Heal
                Spell.Cast("Victory Rush", ret => StyxWoW.Me.HealthPercent < 80),
                //Interupts
                Spell.Cast(
                    "Pummel", ret => StyxWoW.Me.CurrentTarget.IsCasting ||
                                     StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                Spell.Cast(
                    "Arcane Torrent", ret => StyxWoW.Me.CurrentTarget.IsCasting ||
                                             StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                Spell.Cast(
                    "War Stomp", ret => StyxWoW.Me.CurrentTarget.IsCasting ||
                                        StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                //Interupt / Stun elite / knockdown player
                Spell.Cast(
                    "Throwdown", ret => StyxWoW.Me.CurrentTarget.Elite || 
                                        StyxWoW.Me.CurrentTarget.IsPlayer || 
                                        StyxWoW.Me.CurrentTarget.IsCasting),
                //Rage Dump
                // use incite or dump rage
                new Decorator(
                    ret => Unit.HasAura(StyxWoW.Me, "Incite", 1) || StyxWoW.Me.RagePercent > 60,
                    new PrioritySelector(
                        Spell.Cast("Heroic Strike"))),
                //Use Engineering Gloves
                Item.UseEquippedItem(9),
                //Execute under 20%
                Spell.Cast("Execute", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                //Default Rotatiom
                new Decorator(
                    ret => !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Rend", 1),
                    new PrioritySelector(
                        Spell.Buff("Rend"))),
                Spell.Cast("Colossus Smash"),                
                Spell.Cast("Mortal Strike"),
                //Bladestorm after dots and MS if against player
                Spell.Cast("Bladestorm", ret => StyxWoW.Me.CurrentTarget.IsPlayer),
                Spell.Cast("Overpower"),
                Spell.Cast("Slam", ret => StyxWoW.Me.RagePercent > 40),
                //ensure were in melee
                Movement.CreateMoveToTargetBehavior(true, 4f)
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        public static Composite CreateArmsPull()
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
                ArmsCloseGap(),
                //Move to mele and face
                Movement.CreateMoveToTargetBehavior(true, 4f)
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        public static Composite CreateArmsCombatBuffs()
        {
            return new PrioritySelector(
                //Check Heal
                ArmsHeal(),
                //Troll Racial
                Spell.BuffSelf("Berserking"),
                //Retaliation if fighting elite or targeting player
                Spell.Buff(
                    "Retaliation", ret => StyxWoW.Me.CurrentTarget.IsPlayer ||
                                          StyxWoW.Me.CurrentTarget.Elite),
                //Deadly calm + gank protection
                Spell.BuffSelf(
                    "Deadly Calm",
                        ret => (StyxWoW.Me.CurrentTarget.MaxHealth > StyxWoW.Me.MaxHealth &&
                                StyxWoW.Me.CurrentTarget.HealthPercent < 95) ||
                               (StyxWoW.Me.CurrentTarget.HealthPercent > StyxWoW.Me.HealthPercent &&
                                StyxWoW.Me.HealthPercent > 10 &&
                                StyxWoW.Me.HealthPercent < 80) || StyxWoW.Me.CurrentTarget.IsPlayer),
                //Inner Rage
                Spell.BuffSelf(
                    "Inner Rage",
                        ret => !StyxWoW.Me.HasAura("Deadly Calm") &&
                                StyxWoW.Me.RagePercent > 70),
                // Remove cc
                //CreateArmsRemoveCC(),
                // Dwarf Racial
                Spell.BuffSelf("Stoneform", ret => StyxWoW.Me.HealthPercent < 60),
                //Night elf racial
                Spell.BuffSelf("Shadowmeld", ret => StyxWoW.Me.HealthPercent < 20),
                //Orc Racial
                Spell.BuffSelf("Blood Fury"),
                // Buff up
                Spell.BuffSelf(
                    "Battle Shout", ret => !StyxWoW.Me.HasAura("Horn of the Winter") &&
                                           !StyxWoW.Me.HasAura("Roar of Courage") &&
                                           !StyxWoW.Me.HasAura("Strength of Earth Totem") &&
                                           !StyxWoW.Me.HasAura("Battle Shout"))
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        public static Composite CreateArmsPreCombatBuffs()
        {
            return new PrioritySelector(
                //Buff up
                Spell.BuffSelf("Battle Shout")
                );
        }




        public static Composite ArmsCloseGap()
        {
            return new PrioritySelector(
                //Moves to target if you are too close or far
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.Distance < 10 || StyxWoW.Me.CurrentTarget.Distance > 40,
                    new PrioritySelector(
                        Movement.CreateMoveToTargetBehavior(true, 4f)
                        )),
                //Charge
                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance >= 9 &&
                                            StyxWoW.Me.CurrentTarget.Distance < 25),
                //Heroic Leap
                new Decorator(
                    ret => SpellManager.CanCast("Heroic Leap") && StyxWoW.Me.CurrentTarget.Distance > 9 && !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Charge Stun", 1),
                    new Action(
                            ret =>
                            {
                                SpellManager.Cast("Heroic Leap");
                                LegacySpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                            })),
                 //Heroic Throw if not already charging
                 new Decorator(
                     ret => Unit.HasAura(StyxWoW.Me.CurrentTarget, "Charge Stun", 1),
                     new PrioritySelector(
                         Spell.Cast("Heroic Throw"))),
                 //Worgen Racial
                 Spell.BuffSelf(
                     "Darkflight", ret => StyxWoW.Me.CurrentTarget.IsPlayer &&
                                          StyxWoW.Me.CurrentTarget.Distance > 15),
                 //Move to mele and face
                 Movement.CreateMoveToTargetBehavior(true, 4f)
              );
        }

        public static Composite ArmsHeal()
        {
            return new PrioritySelector(
                // get enraged to heal up
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HealthPercent < 70),
                //Herbalist Heal
                Spell.Buff("Lifeblood", ret => StyxWoW.Me.HealthPercent < 70),
                //Draenai Heal
                Spell.Buff("Gift of the Naaru", ret => StyxWoW.Me.HealthPercent < 50),
                //Heal
                Spell.Buff("Enraged Regeneration", ret => StyxWoW.Me.HealthPercent < 60)
                );
        }

        public static Composite ArmsRemoveCC()
        {
            return new PrioritySelector(
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