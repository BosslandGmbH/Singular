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

using System.Linq;
using Styx.Combat.CombatRoutine;
using Singular.Settings;
using TreeSharp;
using Styx;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Helpers;
using System;
using Action = TreeSharp.Action;


namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.FuryWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        public Composite CreateWarriorFuryCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateMoveToAndFace(ret => Me.CurrentTarget),
                CreateAutoAttack(true),
                CreateSpellCast("Piercing Howl", ret => Me.CurrentTarget.Distance < 10 &&
                                                        Me.CurrentTarget.IsPlayer &&
                                                        (!Me.CurrentTarget.HasAura("Hamstring") ||
                                                        !Me.CurrentTarget.HasAura("Piercing Howl") ||
                                                        !Me.CurrentTarget.HasAura("Slowing Poison"))),
                CreateSpellCast("Intimidating Shout", ret => Me.CurrentTarget.Distance < 8 &&
                                                             Me.CurrentTarget.IsPlayer &&
                                                             Me.CurrentTarget.IsCasting),
                CreateSpellCast("Intercept", ret => Me.CurrentTarget.Distance > 10),
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
                CreateSpellCast("Hamstring", ret => Me.CurrentTarget.IsPlayer &&
                                                    (!Me.CurrentTarget.HasAura("Hamstring") ||
                                                    !Me.CurrentTarget.HasAura("Piercing Howl") ||
                                                    !Me.CurrentTarget.HasAura("Slowing Poison"))),
                new Decorator(
                    ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 6) > 3,
                    new PrioritySelector(
                        CreateSpellCast("Cleave"),
                        CreateSpellCast("Whirlwind"),
                        CreateSpellCast("Raging Blow"),
                        CreateSpellCast("Bloodthirst")
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
                CreateSpellCast("Colossus Smash"),
                CreateUseEquippedItem(9),
                CreateSpellCast("Execute", ret => Me.CurrentTarget.HealthPercent < 20),
                CreateSpellCast("Raging Blow"),
                CreateSpellCast("Bloodthirst"),
                CreateSpellCast("Slam", ret => HasAuraStacks("Bloodsurge", 1)),
                CreateSpellCast("Heroic Strike", ret => Me.RagePercent > 60),
                CreateSpellCast("Victory Rush", ret => Me.HealthPercent < 80)
                );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.FuryWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateWarriorFuryPull()
        {
            return
                new PrioritySelector(
                    CreateEnsureTarget(),
                    CreateAutoAttack(true),
                    CreateMoveToAndFace(ret => Me.CurrentTarget),
                    CreateSpellCast("Battle Shout", ret => Me.RagePercent < 20),
                    CreateSpellCast("Intercept", ret => Me.CurrentTarget.Distance > 9),
                    CreateSpellCast("Heroic Throw"),
                    new Decorator(
                        ret => SpellManager.CanCast("Heroic Leap") && Me.CurrentTarget.Distance > 8,
                        new Action(
                            ret =>
                                {
                                    SpellManager.Cast("Heroic Leap");
                                    LegacySpellManager.ClickRemoteLocation(Me.CurrentTarget.Location);
                                }))
                    //,
                    //CreateSpellCast("Throw", 
                    //	ret => Me.Inventory.Equipped.Ranged != null &&
                    //        Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Thrown),
                    //CreateSpellCast("Shoot",
                    //	ret => Me.Inventory.Equipped.Ranged != null &&
                    //       (Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Bow ||
                    //        Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Crossbow ||
                    //		Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Gun)),
                    );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.FuryWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.CombatBuffs)]
        public Composite CreateWarriorFuryCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellCast("Lifeblood", ret => Me.HealthPercent < 70),
                    CreateSpellCast("Gift of the Naaru", ret => Me.HealthPercent < 50),
                    CreateSpellCast("Berserking"),
                    CreateSpellCast("Recklessness"),
                    CreateSpellBuffOnSelf("Berserker Rage",
                            ret => Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Fleeing ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Sapped ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Incapacitated ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Horrified)),
                    CreateSpellBuffOnSelf("Stoneform", ret => Me.HealthPercent < 60),
                    CreateSpellBuffOnSelf("Shadowmeld", ret => Me.HealthPercent < 20),
                    CreateSpellBuffOnSelf("Blood Fury"),
                    CreateSpellBuffOnSelf("Every Man for Himself",
                            ret => Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Asleep ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Stunned ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted)),
                    CreateSpellBuffOnSelf("Will of the Forsaken",
                            ret => Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Charmed ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Asleep ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Horrified ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Fleeing)),
                    CreateSpellBuffOnSelf("Escape Artist",
                            ret => Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Slowed ||
                                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted)),
                    CreateSpellBuffOnSelf("Death Wish",
                            ret => !Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Enraged)),
                    CreateSpellBuffOnSelf("Berserker Rage",
                            ret => !Me.Auras.Any(
                                aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Enraged)),
                    CreateSpellBuffOnSelf("Battle Shout", ret => !Me.HasAura("Horn of the Winter") &&
                                                                 !Me.HasAura("Roar of Courage") &&
                                                                 !Me.HasAura("Strength of Earth Totem"))
                );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.FuryWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        public Composite CreateWarriorFuryPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellBuffOnSelf("Berserker Stance"),
                    CreateSpellCast("Battle Shout")
                );
        }
    }
}