

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

        public static ShapeshiftForm SelectedStance
        {
            get
            {
                var stance = SingularSettings.Instance.Warrior.WarriorDpsStance;
                if (stance == WarriorStance.BattleStance)
                    return ShapeshiftForm.BattleStance;
                return ShapeshiftForm.BerserkerStance;
            }
        }

        public static string SelectedShout
        {
            get { return SingularSettings.Instance.Warrior.UseShout.ToString().CamelToSpaced(); }
        }

        #region Normal
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CreateFuryNormalPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf(SingularSettings.Instance.Warrior.WarriorDpsStance.ToString().CamelToSpaced(),
                        ret => StyxWoW.Me.Shapeshift != SelectedStance), Spell.BuffSelf(SelectedShout));
        }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorFury)]
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

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorFury)]
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
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified))
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorFury)]
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
                
                // Dispel Bubbles
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && (StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")),
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30f)
                        )),

                //Heroic Leap
                Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.CurrentTarget.Distance > 9 && PreventDoubleIntercept),

                // ranged slow
                Spell.Buff("Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows),
                // melee slow
                Spell.Buff("Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows),

                //Interupts
                new Decorator(ret => SingularSettings.Instance.Warrior.UseWarriorInterrupts, Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget)),

                Movement.CreateMoveBehindTargetBehavior(),
                //Heal up in melee
                Spell.Cast("Victory Rush",ret=>StyxWoW.Me.HealthPercent < 90),

                // engineering gloves
                Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                // AOE
                new Decorator(ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3,
                    new PrioritySelector(
                        Spell.Cast("Whirlwind"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Dragon Roar"),
                        // Only pop RB when we have a few stacks of meat cleaver. Increased DPS by quite a bit.
                        Spell.Cast("Raging Blow", ret=>StyxWoW.Me.HasAura("Meat Cleaver", 3)))),       

                // Use the single target rotation!
                SingleTarget(),

                //Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        } 


        private static Composite SingleTarget()
        {
            return new PrioritySelector( // Prio #1 -> BR whenever we're not enraged, and can actually melee the target.
                Spell.BuffSelf("Berserker Rage", ret => !IsEnraged && StyxWoW.Me.CurrentTarget.IsWithinMeleeRange),
                // DC if we have rage, target has CS, and we're not within execute range.
                Spell.BuffSelf("Deadly Calm", ret => StyxWoW.Me.RagePercent >= 40 && TargetSmashed && !WithinExecuteRange),
                // Cast CS when the requirements are met. There's a few, so this is extracted into its own property.
                Spell.Cast("Heroic Strike", ret=> NeedHeroicStrike),
                // BT basically on cooldown, unless we're in execute range, then save it for rage building. Execute is worth more DPR here.
                Spell.Cast("Bloodthirst", ret => !WithinExecuteRange || (StyxWoW.Me.CurrentTarget.HealthPercent <= 20 && StyxWoW.Me.RagePercent <= 30)),
                // Wild strike proc. (Bloodsurge)
                Spell.Cast("Wild Strike", ret=> !WithinExecuteRange && StyxWoW.Me.HasAura("Bloodsurge")),
                // CS on cooldown
                Spell.Cast("Colossus Smash"),
                // Execute on CD
                Spell.Cast("Execute", ret => WithinExecuteRange),
                // RB only when we're not going to have BT come off CD during a GCD
                Spell.Cast("Raging Blow", ret=> BTCD.TotalSeconds >= 1),
                // Dump rage on WS
                Spell.Cast("Wild Strike", ret=>!WithinExecuteRange && TargetSmashed && BTCD.TotalSeconds >= 1),
                // Dragon Roar basically on CD. It ignores armor, so no need to check if the target has CS
                Spell.Cast("Dragon Roar", ret=>StyxWoW.Me.CurrentTarget.Distance < 8),
                // HT on CD. Why not? No GCD extra damage. :)
                Spell.Cast("Heroic Throw"),
                // Shout when we need to pool some rage.
                Spell.Cast(SelectedShout, ret=>!TargetSmashed && StyxWoW.Me.CurrentRage < 70),
                // Fill with WS when BT/CS aren't about to come off CD, and we have some rage to spend.
                Spell.Cast("Wild Strike", ret=> !WithinExecuteRange && BTCD.TotalSeconds >= 1 && CSCD.TotalSeconds >= 1.6 && StyxWoW.Me.CurrentRage >= 60),
                // Costs nothing, and does some damage. So cast it please!
                Spell.Cast("Impending Victory", ret => !WithinExecuteRange),

                // Very last in the prio, just pop BS to waste a GCD and get some rage. Nothing else to do here.
                Spell.Cast("Battle Shout", ret=> StyxWoW.Me.CurrentRage < 70)
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


        #endregion

        #region Calculations - These are for the super-high DPS rotations for raiding as SMF. (TG isn't quite as good as SMF anymore!)

        static TimeSpan BTCD { get { return Spell.GetSpellCooldown("Bloodthirst"); } }
        static TimeSpan CSCD { get { return Spell.GetSpellCooldown("Colossus Smash"); } }

        static bool WithinExecuteRange { get { return StyxWoW.Me.CurrentTarget.HealthPercent <= 20; } }
        static bool IsEnraged { get { return StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Enraged); } }
        private static bool TargetSmashed { get { return StyxWoW.Me.CurrentTarget.HasAura("Colossus Smash"); } }


        static bool NeedHeroicStrike
        {
            get
            {
                if (StyxWoW.Me.CurrentTarget.HealthPercent >= 20)
                {
                    // Go based off % since we have the new glyph to add 20% rage.
                    var myRage = StyxWoW.Me.RagePercent;

                    // Basically, here's how this works.
                    // If the target is CS'd, and we have > 40 rage, then pop HS.
                    // If we *ever* have more than 90% rage, then pop HS
                    // If we popped DC and have more than 30 rage, pop HS (it's more DPR than basically anything else at 15 rage cost)
                    if (myRage >= 40 && TargetSmashed)
                        return true;
                    if (myRage >= 90)
                        return true;
                    if (myRage >= 30 && StyxWoW.Me.HasAura("Deadly Calm"))
                        return true;
                }
                return false;
            }
        }

        #endregion
    }
}
