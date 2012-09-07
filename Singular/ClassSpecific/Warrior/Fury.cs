

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.TreeSharp;

using Styx.Helpers;
using System;
using Styx.WoWInternals;
using Action = Styx.TreeSharp.Action;



namespace Singular.ClassSpecific.Warrior
{
    public class Fury
    {
        private static string[] _slows;

        #region Normal
        [Class(WoWClass.Warrior)]
        [Spec(WoWSpec.WarriorFury)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.Normal)]
        public static Composite CreateFuryNormalPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Berserker Stance"),
                Spell.BuffSelf("Battle Shout"));
        }

        [Spec(WoWSpec.WarriorFury)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal)]
        public static Composite CreateFuryNormalPull()
        {
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                // LOS check
                Movement.CreateMoveToLosBehavior(),
                //face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                //Shoot flying targets
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsFlying && SpellManager.HasSpell("Heroic Throw"),
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Spell.Cast("Heroic Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 27f)
                        )),

                //low level support
                new Decorator(
                    ret => StyxWoW.Me.Level < 50,
                    new PrioritySelector(
                        Spell.BuffSelf("Berserker Stance"),
                        Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance.Between(12,25)),
                        Spell.Cast("Heroic Throw", ret => !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun")),
                        Movement.CreateMoveToTargetBehavior(true, 5f))),

                // Get closer to target
                Spell.Cast("Charge", ret => PreventDoubleIntercept && StyxWoW.Me.CurrentTarget.Distance.Between(8f, TalentManager.HasGlyph("Long Charge") ? 30f : 25f)),
                Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.CurrentTarget.Distance.Between(10,40) && PreventDoubleIntercept),

                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Spec(WoWSpec.WarriorFury)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal)]
        public static Composite CreateFuryNormalCombatBuffs()
        {
            return new PrioritySelector(
                //Heal
                Spell.Buff("Enraged Regeneration", ret => StyxWoW.Me.HealthPercent < 60),
                // Only ever pop reck if we're going to be executing. It's not quite strong enough anymore to use "on CD". Fights are also too short.
                Spell.BuffSelf("Recklessness", ret => SpellManager.CanCast("Execute")),
                // Heroic Fury
                Spell.BuffSelf("Heroic Fury", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Rooted)),
                // Fear Remover, or to get ourselves enraged again.
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified) || !StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Enraged)),
                //Battleshout Check
                Spell.BuffSelf("Battle Shout", ret => !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout") || StyxWoW.Me.RagePercent < 10)
                );
        }

        [Spec(WoWSpec.WarriorFury)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal)]
        public static Composite CreateFuryNormalCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom", "Infected Wounds" };
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                // LOS check
                Movement.CreateMoveToLosBehavior(),
                // Face Target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                // Low level support
                new Decorator(ret => StyxWoW.Me.Level < 30,
                    new PrioritySelector(
                        Movement.CreateMoveBehindTargetBehavior(),
                        Spell.Cast("Victory Rush"),
                        Spell.Cast("Execute"),
                        Spell.Cast("Bloodthirst"),
                        Spell.Cast("Wild Strike"),
                //rage dump
                        Spell.Cast("Thunder Clap", ret => StyxWoW.Me.RagePercent > 50 && Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) > 3),
                        Spell.Cast("Heroic Strike", ret => StyxWoW.Me.RagePercent > 60),
                        Movement.CreateMoveToMeleeBehavior(true))),
                //30-50 support
                Spell.BuffSelf("Berserker Stance", ret => StyxWoW.Me.Level > 30 && SingularSettings.Instance.Warrior.UseWarriorKeepStance),

                // Dispel Bubbles
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && (StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")) && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30f)
                        )),

                //Heroic Leap
                Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.CurrentTarget.Distance > 9 && PreventDoubleIntercept),

                // ranged slow
                Spell.Buff("Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                // melee slow
                Spell.Buff("Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),

                //Interupts
                new Decorator(
                    ret => SingularSettings.Instance.Warrior.UseWarriorInterupts,
                    Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget)),

                Movement.CreateMoveBehindTargetBehavior(),
                //Heal up in melee
                Spell.Cast("Victory Rush",ret=>StyxWoW.Me.HealthPercent < 90),
                Spell.Cast("Heroic Throw", ret => StyxWoW.Me.CurrentTarget.Distance.Between(15,30)),

                // engineering gloves
                Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                // AOE
                new Decorator(ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3 && SingularSettings.Instance.Warrior.UseWarriorAOE,
                    new PrioritySelector(
                        Spell.Cast("Whirlwind"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Raging Blow"),
                        Spell.Cast("Blood Thirst"))),       

                // These 2 need to be used on cooldown. No excuses.
                Spell.Cast("Bloodthirst"),
                Spell.Cast("Colossus Smash"),

                // These are off the GCD. So rage dump if we can.
                Spell.Cast("Cleave", ret =>
                    // Only even think about Cleave for more than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) >= 2 &&
                                    // If we have Incite, Deadly Calm, or enough rage (pooling for CS if viable) we're good.
                                (StyxWoW.Me.HasAura("Incite", 1) || CanUseRageDump())),
                Spell.Cast("Heroic Strike", ret =>
                    // Only even think about HS for less than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) < 2 &&
                                    // If we have Incite, or enough rage (pooling for CS if viable) we're good.
                                (StyxWoW.Me.HasAura("Incite", 1) || CanUseRageDump())),

                // Execute if it's available. Ignoring any other abilities!
                Spell.Cast("Execute"),

                Spell.Cast("Raging Blow"),
                Spell.Cast("Wild Strike"),

                //Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        } 
        #endregion

        
        #region Utils
        private static readonly WaitTimer InterceptTimer = new WaitTimer(TimeSpan.FromMilliseconds(2000));

        private static bool PreventDoubleIntercept
        {
            get
            {
                var tmp = InterceptTimer.IsFinished;
                if (tmp)
                    InterceptTimer.Reset();
                return tmp;
            }
        }

        private static bool TargetSmashed { get { return StyxWoW.Me.CurrentTarget.HasAura("Colossus Smash", 1); } }

        static bool CanUseRageDump()
        {
            // Pooling rage for upcoming CS. If its > 8s, make sure we have 60 rage. < 8s, only pop it at 85 rage.
            if (SpellManager.HasSpell("Colossus Smash"))
                return SpellManager.Spells["Colossus Smash"].CooldownTimeLeft.TotalSeconds > 8 ? StyxWoW.Me.RagePercent > 60 : StyxWoW.Me.RagePercent > 85;

            // We don't know CS. So just check if we have 60 rage to use cleave.
            return StyxWoW.Me.RagePercent > 60;
        }

        static bool HasSpellIntercept()
        {
            if (SpellManager.HasSpell("Intercept"))
                return true;
            return false;
        } 
        #endregion
    }
}
