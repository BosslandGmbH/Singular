#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author$
// $Date$
// $HeadURL$
// $LastChangedBy$
// $LastChangedDate$
// $LastChangedRevision$
// $Revision$

#endregion

using System;
using System.Linq;

using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        [Behavior(BehaviorType.Combat)]
        public Composite CreateArmsWarriorCombat()
        {
            return new PrioritySelector(
                // Make sure we have target
                CreateEnsureTarget(),
                // Face target
                CreateFaceUnit(),
                //Make sure were attacking
                CreateAutoAttack(true),
                // Ranged interupt on players
                CreateSpellCast(
                    "Intimidating Shout", ret => Me.CurrentTarget.Distance < 8 &&
                                                 Me.CurrentTarget.IsPlayer &&
                                                 Me.CurrentTarget.IsCasting),
                // Dispel Bubbles
                CreateSpellCast(
                    "Shattering Throw", ret => Me.CurrentTarget.IsPlayer &&
                                               (Me.CurrentTarget.HasAura("Ice Block") ||
                                               Me.CurrentTarget.HasAura("Hand of Protection") ||
                                               Me.CurrentTarget.HasAura("Divine Shield"))),
                // close gap
                CreateArmsCloseGap(),
                //Rocket belt!
                new Decorator(
                    ret =>
                        Me.CurrentTarget.IsPlayer && Me.CurrentTarget.Distance > 20,
                        new PrioritySelector(
                            CreateUseEquippedItem(10)
                        )),
                // ranged slow
                CreateSpellCast(
                    "Piercing Howl", ret => Me.CurrentTarget.Distance < 10 &&
                                            Me.CurrentTarget.IsPlayer &&
                                            (!Me.CurrentTarget.HasAura("Hamstring") ||
                                             !Me.CurrentTarget.HasAura("Piercing Howl") ||
                                             !Me.CurrentTarget.HasAura("Slowing Poison") ||
                                             !Me.CurrentTarget.HasAura("Hand of Freedom"))),

                //Move to melee			
                CreateMoveToAndFace(ret => Me.CurrentTarget),
                //use it or lose it
                CreateSpellCast("Colossus Smash", ret => Me.HasAura("Sudden Death")),
                // Mele slow
                CreateSpellCast(
                    "Hamstring", ret => Me.CurrentTarget.IsPlayer &&
                                        (!Me.CurrentTarget.HasAura("Hamstring") ||
                                         !Me.CurrentTarget.HasAura("Piercing Howl") ||
                                         !Me.CurrentTarget.HasAura("Slowing Poison") ||
                                         !Me.CurrentTarget.HasAura("Hand of Freedom"))),
                // AOE
                new Decorator(
                    ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 6) > 3,
                    new PrioritySelector(
                        CreateSpellCast("Rend", ret => !Me.CurrentTarget.HasAura("Rend")),
                        CreateSpellCast("Thunderclap"),
                        CreateSpellCast("Sweeping Strikes"),
                        CreateSpellCast("Bladestorm"),
                        CreateSpellCast("Retaliation"),
                        CreateSpellCast("Cleave")
                        )),
                //Mele Heal
                CreateSpellCast("Victory Rush", ret => Me.HealthPercent < 80),
                //Interupts
                CreateSpellCast("Pummel", ret => Me.CurrentTarget.IsCasting),
                CreateSpellCast("War Stomp", ret => Me.CurrentTarget.IsCasting),                
                CreateSpellCast("Arcane Torrent", ret => Me.CurrentTarget.IsCasting),
                //Interupt / Stun elite / knockdown player
                CreateSpellCast(
                    "Throwdown", ret => CurrentTargetIsElite || 
                                        Me.CurrentTarget.IsPlayer || 
                                        Me.CurrentTarget.IsCasting),
                //Rage Dump
                CreateSpellCast(
                    "Heroic Strike", ret => Me.RagePercent > 75 || 
                                            HasAuraStacks("Incite", 1)),
                //Use Engineering Gloves
                CreateUseEquippedItem(9),
                //Execute under 20%
                CreateSpellCast("Execute", ret => Me.CurrentTarget.HealthPercent < 20),
                //Default Rotatiom
                CreateSpellCast("Rend", ret => !Me.CurrentTarget.HasAura("Rend")),
                CreateSpellCast("Colossus Smash"),                
                CreateSpellCast("Mortal Strike"),
                //Bladestorm after dots and MS if against player
                CreateSpellCast("Bladestorm", ret => Me.CurrentTarget.IsPlayer),
                CreateSpellCast("Overpower", ret => !HasAuraStacks("Overpower", 1)),
                CreateSpellCast("Slam", ret => Me.RagePercent > 30),
                //ensure were in melee
                CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
                );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateArmsWarriorPull()
        {
            return
                new PrioritySelector(
                    //Make sure we have a target
                    CreateEnsureTarget(),
                    // start auto attack
                    CreateAutoAttack(true),
                    //Face target
                    CreateFaceUnit(),
                    //Dismount
                    new Decorator(ret => IsMounted,
                        new Action(o => Styx.Logic.Mount.Dismount())),
                    //Shoot flying targets
                    new Decorator(
                        ret => Me.CurrentTarget.IsFlying,
                        new PrioritySelector(
                            CreateFireRangedWeapon()
                        )),
                    //close gap
                    CreateArmsCloseGap(),
                    //move to melee
                    CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
                    );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        [Behavior(BehaviorType.CombatBuffs)]
        public Composite CreateArmsWarriorCombatBuffs()
        {
            return
                new PrioritySelector(
                    //Heal up
                    CreateArmsHeal(),
                    //Troll Racial
                    CreateSpellCast("Berserking"),
                    //Retaliation if fighting elite or targeting player
                    CreateSpellCast(
                        "Retaliation", ret => Me.CurrentTarget.IsPlayer || 
                                              CurrentTargetIsElite),
                    //Deadly calm + gank protection
                    CreateSpellCast(
                        "Deadly Calm",
                            ret => (Me.CurrentTarget.MaxHealth > Me.MaxHealth &&
                                    Me.CurrentTarget.HealthPercent < 95) ||
                                   (Me.CurrentTarget.HealthPercent > Me.HealthPercent &&
                                    Me.HealthPercent > 10 &&
                                    Me.HealthPercent < 80) || Me.CurrentTarget.IsPlayer),
                    //Inner Rage
                    CreateSpellCast(
                        "Inner Rage",
                            ret => !Me.HasAura("Deadly Calm") &&
                                    Me.RagePercent > 70),
                    //Remove cc
                    CreateArmsRemoveCC(),
                    //Dwarf Racial
                    CreateSpellBuffOnSelf("Stoneform", ret => Me.HealthPercent < 60),
                    //Night elf racial
                    CreateSpellBuffOnSelf("Shadowmeld", ret => Me.HealthPercent < 20),
                    //Orc Racial
                    CreateSpellBuffOnSelf("Blood Fury"),       
                    // Buff up
                    CreateSpellBuffOnSelf(
                        "Battle Shout", ret => !Me.HasAura("Horn of the Winter") &&
                                               !Me.HasAura("Roar of Courage") &&
                                               !Me.HasAura("Strength of Earth Totem") &&
                                               !Me.HasAura("Battle Shout"))
                    );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        public Composite CreateArmsWarriorPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    //keep in proper stance
                    CreateSpellBuffOnSelf("Battle Stance"),
                    // keep buffed
                    CreateSpellCast("Battle Shout")
                    );
        }

        private Composite CreateArmsCloseGap()
        {
            return
                new PrioritySelector(
                    //Moves to target if you are too close(Fixes pull bug)
                    new Decorator(
                    ret => Me.CurrentTarget.Distance < 10 || Me.CurrentTarget.Distance > 25,
                    new PrioritySelector(
                        CreateMoveToAndFace(ret => Me.CurrentTarget)
                        )),
                    //Charge
                    CreateSpellCast("Charge", ret => Me.CurrentTarget.Distance >= 9),
                    //Heroic Leap
                    new Decorator(
                    ret => SpellManager.CanCast("Heroic Leap") && Me.CurrentTarget.Distance > 9 && !Me.CurrentTarget.HasAura("Charge Stun"),
                    new Action(
                            ret =>
                            {
                                SpellManager.Cast("Heroic Leap");
                                LegacySpellManager.ClickRemoteLocation(Me.CurrentTarget.Location);
                            })),
                    //Heroic Throw if not already Intercepting
                    CreateSpellCast("Heroic Throw", ret => !Me.CurrentTarget.HasAura("Charge Stun")),
                    //Worgen Racial
                    CreateSpellCast(
                        "Darkflight", ret => Me.CurrentTarget.IsPlayer &&
                                             Me.CurrentTarget.Distance > 15),
                    //Move to mele and face
                    CreateMoveToAndFace(ret => Me.CurrentTarget)
                    );
        }

        private Composite CreateArmsHeal()
        {
            return
                new PrioritySelector(
                    //Herbalist Heal
                    CreateSpellCast("Lifeblood", ret => Me.HealthPercent < 70),
                    //Draenai Heal
                    CreateSpellCast("Gift of the Naaru", ret => Me.HealthPercent < 50),
                    //Get enraged so we can use enraged regen
                    CreateSpellCast("Berserker Rage", ret => Me.HealthPercent < 65),
                    //Heal
                    CreateSpellCast("Enraged Regeneration", ret => Me.HealthPercent < 60)
                    );
        }

        private Composite CreateArmsRemoveCC()
        {
            return
                new PrioritySelector(
                    //Human Racial
                    CreateSpellBuffOnSelf(
                        "Every Man for Himself", ret => Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Asleep ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Stunned ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted)),
                    //Undead Racial
                    CreateSpellBuffOnSelf(
                        "Will of the Forsaken", ret => Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Charmed ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Asleep ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Horrified ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Fleeing)),
                    //Gnome Racial
                    CreateSpellBuffOnSelf(
                        "Escape Artist", ret => Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Slowed ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted)),
                    //Get out of fear
                    CreateSpellBuffOnSelf(
                        "Berserker Rage", ret => Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Fleeing ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Sapped ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Incapacitated ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Horrified))
                    );
        }
    }
}