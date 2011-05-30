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
    public class Arms
    {
        private static string[] _slows;
        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        public static Composite CreateArmsCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom" };
            return new PrioritySelector(
                //Ensure Target
                Safers.EnsureTarget(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                //LOS check
                Movement.CreateMoveToLosBehavior(),
                // Auto Attack
                Common.CreateAutoAttack(false),
                // Ranged interupt on players
                Spell.Buff("Intimidating Shout", ret => StyxWoW.Me.CurrentTarget.Distance < 8 && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.IsCasting),

                // Dispel Bubbles
                Spell.Cast("Shattering Throw", ret => StyxWoW.Me.CurrentTarget.IsPlayer && Unit.HasAnyAura(StyxWoW.Me.CurrentTarget, "Ice Block", "Hand of Protection", "Divine Shield")),

                //Rocket belt!
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.Distance > 20, 
                    Item.UseEquippedItem(10)),

                // ranged slow
                Spell.Buff("Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer && !Unit.HasAnyAura(StyxWoW.Me.CurrentTarget, _slows)),
                //Charge
                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25),
                //Heroic Leap
                Spell.CastOnGround("Heroic Leap",ret=>StyxWoW.Me.CurrentTarget.Location,ret=>StyxWoW.Me.CurrentTarget.Distance > 9 && !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Charge Stun", 1)),

                //use it or lose it
                Spell.Cast("Colossus Smash", ret => Unit.HasAura(StyxWoW.Me, "Sudden Death", 1)),

                // Melee slow
                Spell.Cast("Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer && !Unit.HasAnyAura(StyxWoW.Me.CurrentTarget, _slows)),

                // Fury of angerforge
                // NOTE: Should wortk with non-english clients. Only tested on EN clients.
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

                //Mele Heal
                Spell.Cast("Victory Rush", ret => StyxWoW.Me.HealthPercent < 80),

                // AOE
                new Decorator(
                    ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3,
                    new PrioritySelector(
                        // recklessness gets to be used in any stance soon
                        Spell.BuffSelf("Recklessness"),
                        Spell.BuffSelf("Sweeping Strikes"),
                        Spell.Cast("Bladestorm"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Mortal Strike"))),

                //Interupts
                new Decorator(ret=>StyxWoW.Me.CurrentTarget.IsCasting,
                    new PrioritySelector(
                        Spell.Cast("Pummel"),
                        Spell.Cast("Arcane Torrent"),
                        Spell.Cast("War Stomp"),
                        // Only pop TD on elites/players
                        Spell.Cast("Throwdown", ret=>StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite))),

                //Rage Dump
                Spell.Cast("Heroic Strike", ret => Unit.HasAura(StyxWoW.Me, "Incite", 1) || StyxWoW.Me.RagePercent > 60),

                //Use Engineering Gloves Bugged if you dont have engineering
                //Item.UseEquippedItem(9),

                //Execute under 20%
                Spell.Cast("Execute", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 20),

                //Default Rotatiom
                Spell.Buff("Rend"),
                Spell.Cast("Colossus Smash"),                
                Spell.Cast("Mortal Strike"),
                //Bladestorm after dots and MS if against player
                Spell.Cast("Bladestorm", ret => StyxWoW.Me.CurrentTarget.IsPlayer),
                Spell.Cast("Overpower"),
                Spell.Cast("Slam", ret => StyxWoW.Me.RagePercent > 40),
                //ensure were in melee
                Movement.CreateMoveToTargetBehavior(true, 5f)
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
                        Waiters.WaitForCast(),
                        Spell.Cast("Heroic Throw"),
                        Spell.Cast("Shoot"),
                        Spell.Cast("Throw")
                    )),
                //Buff up
                Spell.BuffSelf("Battle Shout", ret => StyxWoW.Me.RagePercent < 20),
                //Close gap
                ArmsCloseGap()
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
                    "Retaliation", ret => StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite),
                //Deadly calm + gank protection
                Spell.BuffSelf(
                    "Deadly Calm",
                        ret => (StyxWoW.Me.CurrentTarget.MaxHealth > StyxWoW.Me.MaxHealth &&
                                StyxWoW.Me.CurrentTarget.HealthPercent < 95) ||
                               (StyxWoW.Me.CurrentTarget.HealthPercent > StyxWoW.Me.HealthPercent &&
                                StyxWoW.Me.HealthPercent > 10 &&
                                StyxWoW.Me.HealthPercent < 80) || StyxWoW.Me.CurrentTarget.IsPlayer),
                //Inner Rage
                Spell.BuffSelf("Inner Rage", ret => StyxWoW.Me.RagePercent > 90),
                // Remove cc
                ArmsRemoveCC(),
                // Dwarf Racial
                Spell.BuffSelf("Stoneform", ret => StyxWoW.Me.HealthPercent < 60),
                //Night elf racial
                Spell.BuffSelf("Shadowmeld", ret => StyxWoW.Me.HealthPercent < 22),
                //Orc Racial
                Spell.BuffSelf("Blood Fury"),
                // Buff up
                Spell.BuffSelf("Battle Shout", ret => !Unit.HasAnyAura(StyxWoW.Me, "Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout"))
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
                    ret => StyxWoW.Me.CurrentTarget.Distance < 10 || StyxWoW.Me.CurrentTarget.Distance > 25,
                    new PrioritySelector(
                        Movement.CreateMoveToTargetBehavior(true, 5f)
                        )),
                //Charge
                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance >= 9 && StyxWoW.Me.CurrentTarget.Distance < 25),
                //Heroic Leap
                Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.CurrentTarget.Distance > 9 && !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Charge Stun", 1)),
                //Heroic Throw if not already charging
                Spell.Cast("Heroic Throw", ret => !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Charge Stun")),
                //Worgen Racial
                Spell.BuffSelf("Darkflight", ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.Distance > 15),
                //Move to mele and face
                Movement.CreateMoveToTargetBehavior(true, 5f)
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