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
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        public Composite CreateArmsWarriorCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                //Move to range
                CreateFaceUnit(),
                CreateAutoAttack(true),
                CreateSpellCast("Battle Shout", ret => Me.RagePercent < 20 || !Me.HasAura("Battle Shout")),
                CreateSpellCast(
                    "Piercing Howl", ret => Me.CurrentTarget.Distance < 10 &&
                                            Me.CurrentTarget.IsPlayer &&
                                            (!Me.CurrentTarget.HasAura("Hamstring") ||
                                             !Me.CurrentTarget.HasAura("Piercing Howl") ||
                                             !Me.CurrentTarget.HasAura("Slowing Poison"))),
                CreateSpellCast(
                    "Intimidating Shout", ret => Me.CurrentTarget.Distance < 8 &&
                                                 Me.CurrentTarget.IsPlayer &&
                                                 Me.CurrentTarget.IsCasting),
                CreateSpellCast("Charge", ret => Me.CurrentTarget.Distance.Between(8f, TalentManager.HasGlyph("Long Charge") ? 30f : 25f)),
                CreateSpellCast("Victory Rush", ret => Me.HealthPercent < 80),
                //Important: Sudden Death (reset Collossus Smash, use it or lose it! Very quick Proc therfore needs to be first)
                CreateSpellCast("Colossus Smash", Me.HasAura("Sudden Death")),
                CreateSpellCast("Heroic Throw", ret => Me.CurrentTarget.IsPlayer),
                CreateSpellCast("Enraged Regeneration", ret => Me.HealthPercent < 60),
                new Decorator(
                    ret => SpellManager.CanCast("Heroic Leap") && Me.CurrentTarget.Distance > 13,
                    new Action(
                        ret =>
                            {
                                SpellManager.Cast("Heroic Leap");
                                LegacySpellManager.ClickRemoteLocation(Me.CurrentTarget.Location);
                            })),
                //Move to melee			
                CreateMoveToAndFace(ret => Me.CurrentTarget),
                CreateSpellCast(
                    "Hamstring", ret => Me.CurrentTarget.IsPlayer &&
                                        (!Me.CurrentTarget.HasAura("Hamstring") ||
                                         !Me.CurrentTarget.HasAura("Piercing Howl") ||
                                         !Me.CurrentTarget.HasAura("Slowing Poison"))),
                new Decorator(
                    ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 6) > 3,
                    new PrioritySelector(
                        CreateSpellCast("Rend", ret => !Me.CurrentTarget.HasAura("Rend")),
                        CreateSpellCast("Thunderclap"),
                        CreateSpellCast("Sweeping Strikes"),
                        CreateSpellCast("Bladestorm"),
                        CreateSpellCast("Cleave"),
                        CreateSpellCast("Whirlwind")
                        )),
                new Decorator(
                    ret =>
                    Me.CurrentTarget.Distance > 5 &&
                    !WoWMathHelper.IsFacing(Me.CurrentTarget.Location, Me.CurrentTarget.Rotation, Me.Location, (float)Math.PI) &&
                    Me.CurrentTarget.IsMoving && Me.CurrentTarget.MovementInfo.RunSpeed > Me.MovementInfo.RunSpeed,
                    new PrioritySelector(
                        CreateSpellCast("Darkflight")
                        )),
                CreateSpellCast("War Stomp", ret => Me.CurrentTarget.IsCasting),
                CreateSpellCast("Pummel", ret => Me.CurrentTarget.IsCasting),
                CreateSpellCast("Arcane Torrent", ret => Me.CurrentTarget.IsCasting),
                CreateSpellCast("Rend", ret => !Me.CurrentTarget.HasAura("Rend")),
                //Will cast Collosus smash if Sudden death is not active, and is back to at its default priority
                CreateSpellCast("Colossus Smash"),
                CreateSpellCast("Execute", ret => Me.CurrentTarget.HealthPercent < 20),
                CreateSpellCast("Mortal Strike"),
                CreateSpellCast("Overpower", ret => !HasAuraStacks("Overpower", 1)),
                CreateSpellCast("Slam", ret => Me.RagePercent > 30),
                CreateSpellCast("Heroic Strike", ret => Me.RagePercent > 75 || HasAuraStacks("Incite", 1)),
                // Again; movement comes last in melee. We have spells we can use at long-range, and should do so when the opportunity
                // presents itself.
                // If none of our short-range attacks are... in range... then just move forward.
                CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
                );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateArmsWarriorPull()
        {
            return
                new PrioritySelector(
                    CreateEnsureTarget(),
                    CreateAutoAttack(true),
                    CreateFaceUnit(),
                    CreateSpellBuffOnSelf("Battle Stance"),
                    CreateSpellCast("Charge", ret => Me.CurrentTarget.Distance >= 9),
                    CreateSpellCast("Heroic Throw"),
                    new Decorator(
                        ret => SpellManager.CanCast("Heroic Leap") && Me.CurrentTarget.Distance > 8,
                        new Action(
                            ret =>
                                {
                                    SpellManager.Cast("Heroic Leap");
                                    LegacySpellManager.ClickRemoteLocation(Me.CurrentTarget.Location);
                                })),
                    // Ensure we are in proper range for melee attacks
                    CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
                    );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.CombatBuffs)]
        public Composite CreateArmsWarriorCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellCast("Lifeblood", ret => Me.HealthPercent < 70),
                    CreateSpellCast("Gift of the Naaru", ret => Me.HealthPercent < 50),
                    CreateSpellCast("Berserking"),
                    CreateSpellCast(
                        "Throwdown",
                        ret => Me.HasAura("Deadly Calm") || Me.HasAura("Recklessness")),
                    CreateSpellCast(
                        "Recklessness",
                        ret => (Me.CurrentTarget.MaxHealth > Me.MaxHealth &&
                                Me.CurrentTarget.HealthPercent < 95 &&
                                Me.RagePercent > 50) ||
                               (Me.CurrentTarget.HealthPercent > Me.HealthPercent &&
                                Me.HealthPercent > 15 &&
                                Me.HealthPercent < 75)),
                    CreateSpellCast(
                        "Deadly Calm",
                        ret => (Me.CurrentTarget.MaxHealth > Me.MaxHealth &&
                                Me.CurrentTarget.HealthPercent < 95) ||
                               (Me.CurrentTarget.HealthPercent > Me.HealthPercent &&
                                Me.HealthPercent > 10 &&
                                Me.HealthPercent < 80)),
                    //Deadly Calm and Inner Rage don't stack
                    CreateSpellCast(
                        "Inner Rage",
                        ret => !Me.HasAura("Deadly Calm") &&
                               ((Me.HasAura("Recklessness") ||
                                 Me.RagePercent > 80))),
                    CreateSpellBuffOnSelf(
                        "Berserker Rage",
                        ret => Me.Auras.Any(
                            aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Fleeing ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Sapped ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Incapacitated ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Horrified)),
                    CreateSpellBuffOnSelf("Stoneform", ret => Me.HealthPercent < 60),
                    CreateSpellBuffOnSelf("Shadowmeld", ret => Me.HealthPercent < 20),
                    CreateSpellBuffOnSelf("Blood Fury"),
                    CreateSpellBuffOnSelf(
                        "Every Man for Himself",
                        ret => Me.Auras.Any(
                            aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Asleep ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Stunned ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted)),
                    CreateSpellBuffOnSelf(
                        "Will of the Forsaken",
                        ret => Me.Auras.Any(
                            aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Charmed ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Asleep ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Horrified ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Fleeing)),
                    CreateSpellBuffOnSelf(
                        "Escape Artist",
                        ret => Me.Auras.Any(
                            aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Slowed ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted)),
                    CreateSpellBuffOnSelf(
                        "Death Wish",
                        ret => (Me.CurrentTarget.MaxHealth > Me.MaxHealth &&
                                Me.CurrentTarget.HealthPercent < 95 &&
                                Me.RagePercent > 50) ||
                               (Me.CurrentTarget.HealthPercent > Me.HealthPercent &&
                                Me.HealthPercent > 10 &&
                                Me.HealthPercent < 75)),
                    //Do not need to be enraged
                    //ret => !Me.Auras.Any(
                    //aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Enraged)),
                    CreateSpellBuffOnSelf(
                        "Battle Shout", ret => !Me.HasAura("Horn of the Winter") &&
                                               !Me.HasAura("Roar of Courage") &&
                                               !Me.HasAura("Strength of Earth Totem"))
                    );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        public Composite CreateArmsWarriorPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellBuffOnSelf("Battle Stance"),
                    CreateSpellCast("Battle Shout")
                    );
        }
    }
}