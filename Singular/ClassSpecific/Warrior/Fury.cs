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
using Styx;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.FuryWarrior)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        [Behavior(BehaviorType.Combat)]
        public Composite CreateWarriorFuryCombat()
        {
            return new PrioritySelector(
                // Make sure you have target
                CreateEnsureTarget(),
                //Face target if not facing already
                CreateFaceUnit(),
                //autoattack
                CreateAutoAttack(true),
                //low level support
                new Decorator(
                    ret => Me.Level < 30,
                    new PrioritySelector(
                        CreateSpellCast("Charge"),
                        CreateSpellCast("Victory Rush"),
                        CreateSpellCast("Rend", ret => !Me.CurrentTarget.HasAura("Rend")),
                        CreateSpellCast("Bloodthirst"),
                        CreateMoveToAndFace(ret => Me.CurrentTarget))),
                //Ranged Attack if pvping
                CreateSpellCast("Heroic Throw", ret => Me.CurrentTarget.IsPlayer),             
                //Use fear to interupt casters at range
                CreateSpellCast(
                    "Intimidating Shout", ret => Me.CurrentTarget.Distance < 8 &&
                                                 Me.CurrentTarget.IsPlayer &&
                                                 Me.CurrentTarget.IsCasting),
                //Close Gap to target
                CreateFuryCloseGap(),
                //Rocket belt!
                new Decorator(
                    ret =>
                        Me.CurrentTarget.IsPlayer && Me.CurrentTarget.Distance > 20,
                        new PrioritySelector(
                            CreateUseEquippedItem(10)
                        )),
                //Slow Players at range
                CreateSpellCast(
                    "Piercing Howl", ret => Me.CurrentTarget.Distance < 10 &&
                                            Me.CurrentTarget.IsPlayer &&
                                            (!Me.CurrentTarget.HasAura("Hamstring") ||
                                             !Me.CurrentTarget.HasAura("Piercing Howl") ||
                                             !Me.CurrentTarget.HasAura("Slowing Poison") ||
                                             !Me.CurrentTarget.HasAura("Hand of Freedom"))),
                //Move to melee			
				CreateMoveToAndFace(),
                //Slow Players once in mele range
                CreateSpellCast(
                    "Hamstring", ret => Me.CurrentTarget.IsPlayer &&
                                        (!Me.CurrentTarget.HasAura("Hamstring") ||
                                         !Me.CurrentTarget.HasAura("Piercing Howl") ||
                                         !Me.CurrentTarget.HasAura("Slowing Poison") ||
                                         !Me.CurrentTarget.HasAura("Hand of Freedom"))),
                // slow runners
                new Decorator(
                    ret => Me.CurrentTarget.IsPlayerBehind || Me.CurrentTarget.Mounted,
                        new PrioritySelector(
                            CreateSpellCast("Hamstring", ret => !Me.CurrentTarget.IsPlayer &&
                                                                !Me.CurrentTarget.HasAura("Hamstring")))),
                //Aoe when more than 3 around
                new Decorator(
                    ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 6) > 3,
                    new PrioritySelector(
                        CreateSpellCast("Cleave"),
                        CreateSpellCast("Whirlwind"),
                        CreateSpellCast("Raging Blow"),
                        CreateSpellCast("Bloodthirst")
                        )),
                // Fury of angerforge
                new Decorator(
                    ret => HasAuraStacks("Raw Fury", 5) &&
                           StyxWoW.Me.Inventory.Equipped.Trinket1 != null &&
                           StyxWoW.Me.Inventory.Equipped.Trinket1.Name.Contains("Fury of Angerforge") &&
                           StyxWoW.Me.Inventory.Equipped.Trinket1.Cooldown <= 0,
                    new Action(
                        ret =>
                        {
                            StyxWoW.Me.Inventory.Equipped.Trinket1.Use();
                        })),
                new Decorator(
                    ret => HasAuraStacks("Raw Fury", 5) &&
                           StyxWoW.Me.Inventory.Equipped.Trinket2 != null &&
                           StyxWoW.Me.Inventory.Equipped.Trinket2.Name.Contains("Fury of Angerforge") &&
                           StyxWoW.Me.Inventory.Equipped.Trinket2.Cooldown <= 0,
                    new Action(
                        ret =>
                        {
                            StyxWoW.Me.Inventory.Equipped.Trinket2.Use();
                        })),
                //Interupts
                CreateSpellCast(
                    "Pummel", ret => Me.CurrentTarget.IsCasting || 
                                     Me.CurrentTarget.ChanneledCastingSpellId != 0),
                CreateSpellCast(
                    "Arcane Torrent", ret => Me.CurrentTarget.IsCasting || 
                                             Me.CurrentTarget.ChanneledCastingSpellId != 0),
                CreateSpellCast(
                    "War Stomp", ret => Me.CurrentTarget.IsCasting || 
                                        Me.CurrentTarget.ChanneledCastingSpellId != 0),
                //Heal up in mele
				CreateSpellCast("Victory Rush", ret => Me.HealthPercent < 80),
                //Use Incite or dump rage
				CreateSpellCast(
                    "Heroic Strike", ret => Me.RagePercent > 60 || 
                                            HasAuraStacks("Incite", 1)),                
                //Use Engineering Gloves
                CreateUseEquippedItem(9),
                //Rotation under 20%
                CreateSpellCast("Colossus Smash"),
                CreateSpellCast("Execute", ret => Me.CurrentTarget.HealthPercent < 20),
                //Rotation over 20%
                CreateSpellCast("Raging Blow"),
                CreateSpellCast("Bloodthirst"),
                CreateSpellCast("Slam", ret => HasAuraStacks("Bloodsurge", 1)),
                //Mele range check
                CreateMoveToAndFace(ret => Me.CurrentTarget)
                );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.FuryWarrior)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateWarriorFuryPull()
        {
            return
                new PrioritySelector(
                    //Make sure we have a target
                    CreateEnsureTarget(),
                    //Start autoattack
                    CreateAutoAttack(true),
                    //face target
                    CreateFaceUnit(),
                    //low level support
                    new Decorator(
                        ret => Me.Level < 30,
                        new PrioritySelector(
                            CreateSpellCast("Charge", ret => Me.CurrentTarget.Distance > 10),
                            CreateMoveToAndFace(ret => Me.CurrentTarget))),
                    //Dismount
                    new Decorator(ret => IsMounted,
                        new Action(o => Styx.Logic.Mount.Dismount())),
                    //Shoot flying targets
                    new Decorator(
                        ret => Me.CurrentTarget.IsFlying,
                        new PrioritySelector(
                            CreateFireRangedWeapon()
                        )),
                    //Buff up
                    CreateSpellCast("Battle Shout", ret => Me.RagePercent < 20),
                    //Close gap
                    CreateFuryCloseGap(),
                    //Move to mele and face
                    CreateMoveToAndFace(ret => Me.CurrentTarget)
                    );
        }

        //Instance Combat Buffs
        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.FuryWarrior)]
        [Context(WoWContext.Instances)]
        [Behavior(BehaviorType.CombatBuffs)]
        public Composite CreateWarriorFuryInstanceCombatBuffs()
        {
            return
                new PrioritySelector(
                    //Check Heal
                    CreateFuryHeal(),
                    //Troll Racial
                    CreateSpellCast("Berserking"),
                    //Recklessness if low on hp or have Deathwish up
                    CreateSpellCast("Recklessness",
                            ret => Me.HasAura("Death Wish") && Me.HealthPercent > 20),
                    //Inner rage with recklessness, deathwish or dump rage
                    CreateSpellCast(
                        "Inner Rage", ret => Me.HasAura("Recklessness") ||
                                             Me.HasAura("Death Wish") ||
                                             Me.RagePercent > 90),
                    //Remove Croud Control Effects
                    CreateFuryRemoveCC(),
                    //Dwarf Racial
                    CreateSpellBuffOnSelf("Stoneform", ret => Me.HealthPercent < 60),
                    //Night Elf Racial
                    CreateSpellBuffOnSelf("Shadowmeld", ret => Me.HealthPercent < 20),
                    //Orc Racial
                    CreateSpellBuffOnSelf("Blood Fury"),
                    //Deathwish
                    CreateSpellBuffOnSelf(
                        "Death Wish", ret => (Me.CurrentTarget.MaxHealth > Me.MaxHealth &&
                                        Me.CurrentTarget.HealthPercent < 95 &&
                                        Me.RagePercent > 50) ||
                                        (Me.CurrentTarget.MaxHealth > Me.MaxHealth &&
                                        Me.HealthPercent > 10 && Me.HealthPercent < 75)),
                    //Berserker rage to stay enraged(Key to good dps)
                    CreateSpellBuffOnSelf("Berserker Rage",
                            ret => !Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Enraged)),
                    //Battleshout Check
                    CreateSpellBuffOnSelf(
                        "Battle Shout", ret => !Me.HasAura("Horn of the Winter") &&
                                               !Me.HasAura("Roar of Courage") &&
                                               !Me.HasAura("Strength of Earth Totem") ||
                                               Me.RagePercent < 15)
                );
        }

        //Normal Combat Buffs
        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.FuryWarrior)]
        [Context(WoWContext.Normal)]
        [Behavior(BehaviorType.CombatBuffs)]
        public Composite CreateWarriorFuryCombatBuffs()
        {
            return
                new PrioritySelector(
                    //Check Heal
                    CreateFuryHeal(),
                    //Troll Racial
                    CreateSpellCast("Berserking"),
                    //Recklessness if low on hp or have Deathwish up or as gank protection
                    CreateSpellCast("Recklessness",
                            ret => Me.HasAura("Death Wish") && Me.HealthPercent > 20 ||
                                   Me.CurrentTarget.IsPlayer),
                    //Inner rage to dump rage
                    CreateSpellCast("Inner Rage", ret => Me.RagePercent > 80),
                    //Remove Croud Control Effects
                    CreateFuryRemoveCC(),
                    //Dwarf Racial
                    CreateSpellBuffOnSelf("Stoneform", ret => Me.HealthPercent < 60),
                    //Night Elf Racial
                    CreateSpellBuffOnSelf("Shadowmeld", ret => Me.HealthPercent < 20),
                    //Orc Racial
                    CreateSpellBuffOnSelf("Blood Fury"),
                    //Deathwish, for both grinding and gank protection
                    CreateSpellBuffOnSelf(
                        "Death Wish", ret => (Me.CurrentTarget.MaxHealth > Me.MaxHealth &&
                                        Me.CurrentTarget.HealthPercent < 95 &&
                                        Me.RagePercent > 50) ||
                                        (Me.CurrentTarget.MaxHealth > Me.MaxHealth &&
                                        Me.HealthPercent > 10 && Me.HealthPercent < 75) ||
                                        Me.CurrentTarget.IsPlayer),
                    //Berserker rage to stay enraged(Key to good dps btw)
                    CreateSpellBuffOnSelf("Berserker Rage",
                            ret => !Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Enraged)),
                    //Battleshout Check
                    CreateSpellBuffOnSelf(
                        "Battle Shout", ret => !Me.HasAura("Horn of the Winter") &&
                                               !Me.HasAura("Roar of Courage") &&
                                               !Me.HasAura("Strength of Earth Totem"))
                );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.FuryWarrior)]
        [Context(WoWContext.Normal | WoWContext.Instances)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        public Composite CreateWarriorFuryPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    //Keep Proper stance
                    new Decorator(
                        ret => Me.Level > 30,
                        new PrioritySelector(
                            CreateSpellBuffOnSelf("Berserker Stance"))),
                    //Buff up
                    CreateSpellCast("Battle Shout")
                    );
        }

        private Composite CreateFuryCloseGap()
        {
            return
                new PrioritySelector(
                    //Moves to target if you are too close(Fixes pull bug)
                    new Decorator(
                    ret => Me.CurrentTarget.Distance < 10 || Me.CurrentTarget.Distance > 40,
                    new PrioritySelector(
                        CreateMoveToAndFace(ret => Me.CurrentTarget)
                        )),
                    // Heroic fury
                    CreateSpellCast("Heroic Fury", ret => SpellManager.Spells["Intercept"].Cooldown),
                    //Intercept
                    CreateSpellCast("Intercept", ret => Me.CurrentTarget.Distance >= 10),
                    //Heroic Leap
                    new Decorator(
                    ret => SpellManager.CanCast("Heroic Leap") && Me.CurrentTarget.Distance > 9 && !Me.CurrentTarget.HasAura("Intercept"),
                    new Action(
                            ret =>
                            {
                                SpellManager.Cast("Heroic Leap");
                                LegacySpellManager.ClickRemoteLocation(Me.CurrentTarget.Location);
                            })),
                    //Heroic Throw if not already Intercepting
                    CreateSpellCast("Heroic Throw", ret => !Me.CurrentTarget.HasAura("Intercept")),
                    //Worgen Racial
                    CreateSpellCast(
                        "Darkflight", ret => Me.CurrentTarget.IsPlayer &&
                                             Me.CurrentTarget.Distance > 15),
                    //Move to mele and face
                    CreateMoveToAndFace(ret => Me.CurrentTarget)
                    );
        }

        private Composite CreateFuryHeal()
        {
            return
                new PrioritySelector(
                    //Herbalist Heal
                    CreateSpellCast("Lifeblood", ret => Me.HealthPercent < 70),
                    //Draenai Heal
                    CreateSpellCast("Gift of the Naaru", ret => Me.HealthPercent < 50),
                    //Heal
                    CreateSpellCast("Enraged Regeneration", ret => Me.HealthPercent < 60)
                    );
        }

        private Composite CreateFuryRemoveCC()
        {
            return
                new PrioritySelector(
                    // Heroic Fury
                    CreateSpellBuffOnSelf(
                        "Heroic Fury", ret => Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted)),
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