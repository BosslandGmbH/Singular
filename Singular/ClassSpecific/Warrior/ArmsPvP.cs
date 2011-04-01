#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author: jon310 $
// $Date: 2011-03-31 11:39:53 -0700 (Thu, 31 Mar 2011) $
// $HeadURL: http://svn.apocdev.com/singular/trunk/Singular/ClassSpecific/Warrior/Arms.cs $
// $LastChangedBy: jon310 $
// $LastChangedDate: 2011-03-31 11:39:53 -0700 (Thu, 31 Mar 2011) $
// $LastChangedRevision: 250 $
// $Revision: 250 $

#endregion

using System;
using System.Linq;

using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;

using TreeSharp;

using Action = TreeSharp.Action;
using Styx.Logic;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.Combat)]
        public Composite CreateArmsWarriorBGCombat()
        {
            return new PrioritySelector(
                CreateCheckPlayerPvPState(),
                CreateCheckTargetPvPState(),
                // Make sure we have target
                CreateEnsurePVPTargetMelee(),
                // Face target
                CreateFaceUnit(),
                new Decorator(
                        ret => MeUnderStunLikeControl || MeUnderSheepLikeControl || MeUnderFearLikeControl,
                        new PrioritySelector(
                            CreateUseAntiPvPControl(),
                            // return true, so as we are under control, we cannot do anything anyway
                            new Action(ret => { return RunStatus.Success; }))),
                // always move to target
                new Decorator(ret => Me.CurrentTarget == null || !Me.CurrentTarget.IsAlive,
                    new Action(ret => { return RunStatus.Success; })),
                CreateMoveToAndFacePvP(),
                // Ranged interupt on players
                CreateSpellCast(
                    "Intimidating Shout", ret => Me.CurrentTarget.Distance < 8 &&
                                                 Me.CurrentTarget.IsCasting),
                // Dispel Bubbles
                new Decorator(
                    ret => Me.CurrentTarget.HasAura("Ice Block") || 
                           Me.CurrentTarget.HasAura("Hand of Protection") ||
                           Me.CurrentTarget.HasAura("Devine Shield"), 
                           new PrioritySelector(
                                CreateWaitForCast(),
                                CreateSpellCast("Shattering Throw"))),
                // close gap
                new Decorator(
                    ret => Me.CurrentTarget.Distance > 10,
                    new PrioritySelector(
                        CreateSpellCastOnLocation("Heroic Leap", ret => Me.CurrentTarget.Location))),
                CreateSpellCast("Charge", ret => Me.CurrentTarget.Distance > 10),
                // ranged slow
                CreateSpellCast(
                    "Piercing Howl", ret => Me.CurrentTarget.Distance < 10 &&
                                            !TargetState.Slowed && 
                                            !TargetState.Invulnerable),
                //Make sure were attacking
                CreateAutoAttack(true),
                //use it or lose it
                CreateSpellCast("Colossus Smash", ret => Me.HasAura("Sudden Death") &&
                                                         (!TargetState.Invulnerable ||
                                                         !TargetState.Incapacitated)),
                // Mele slow
                CreateSpellCast(
                    "Hamstring", ret => !TargetState.Slowed &&
                                        !TargetState.Invulnerable),
                //Mele Heal
                CreateSpellCast("Victory Rush", ret => Me.HealthPercent < 80),
                //Interupts
                CreateSpellCast("Pummel", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
                CreateSpellCast("War Stomp", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
                CreateSpellCast("Arcane Torrent", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
                //knockdown player
                CreateSpellCast("Throwdown", ret => !TargetState.Incapacitated ||
                                                    !TargetState.Invulnerable),
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
                CreateSpellCast("Bladestorm"),
                //after dodge
                CreateSpellCast("Overpower"),
                //on rend proc
                CreateSpellCast("Overpower", ret => HasAuraStacks("Taste for Blood", 1)),
                CreateSpellCast("Slam", ret => Me.RagePercent > 30)
                );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateArmsWarriorBGPull()
        {
            return
                new PrioritySelector(
                    // reset initial timer
                    new Action(ret =>
                    {
                        targetAwayFromMeleeTimer.Reset();
                        return RunStatus.Failure;
                    }),
                    //Ensure target
                    new Decorator(ret => Battlegrounds.IsInsideBattleground,
                        CreateEnsurePVPTargetMelee()),
                    new Decorator(ret => !Battlegrounds.IsInsideBattleground,
                        CreateEnsureTarget()),
                    //checkstate
                    CreateCheckTargetPvPState(),
                    // move to target
                    CreateMoveToAndFacePvP(),
                    //Dismount
                    new Decorator(ret => IsMounted,
                        new Action(o => Styx.Logic.Mount.Dismount())),
                    //close gap
                    new Decorator(
                        ret => Me.CurrentTarget.Distance > 10,
                        new PrioritySelector(
                            CreateSpellCastOnLocation("Heroic Leap", ret => Me.CurrentTarget.Location))),
                    CreateSpellCast("Charge", ret => Me.CurrentTarget.Distance > 10),
                    // start auto attack
                    CreateAutoAttack(true)
                    );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.CombatBuffs)]
        public Composite CreateArmsWarriorBGCombatBuffs()
        {
            return
                new PrioritySelector(
                    //Heal up
                    CreateSpellCast("Lifeblood", ret => Me.HealthPercent < 70),
                    //Draenai Heal
                    CreateSpellCast("Gift of the Naaru", ret => Me.HealthPercent < 50),
                    //Get enraged so we can use enraged regen
                    CreateSpellCast("Berserker Rage", ret => Me.HealthPercent < 65),
                    //Heal
                    CreateSpellCast("Enraged Regeneration", ret => Me.HealthPercent < 60),
                    //Troll Racial
                    CreateSpellCast("Berserking"),
                    //Retaliation 
                    CreateSpellCast("Retaliation"),
                    //Deadly calm
                    CreateSpellCast("Deadly Calm"),
                    //Remove cc
                    CreateSpellCast(
                        "Every Man for Himself", ret => MeUnderFearLikeControl ||
                                                        MeUnderStunLikeControl ||
                                                        MeUnderSheepLikeControl),
                    CreateSpellCast(
                        "Will of the Forsaken", ret => MeUnderFearLikeControl ||
                                                       MeUnderSheepLikeControl),
                    CreateSpellCast(
                        "Escape Artist", ret => MeRooted || MeSlowed),
                    CreateSpellCast(
                        "Berserker Rage", ret => MeUnderFearLikeControl ||
                                                 MeUnderStunLikeControl),
                    //Dwarf Racial
                    CreateSpellBuffOnSelf("Stoneform", ret => Me.HealthPercent < 60),
                    //Night elf racial
                    CreateSpellBuffOnSelf("Shadowmeld", ret => Me.HealthPercent < 20),
                    //Orc Racial
                    CreateSpellBuffOnSelf("Blood Fury"),       
                    // Buff up
                    CreateSpellBuffOnSelf("Battle Shout", ret => Me.CurrentRage < 10)
                    );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        public Composite CreateArmsWarriorBGPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    //keep in proper stance
                    CreateSpellBuffOnSelf("Battle Stance")
                    );
        }
    }
}