using System.Runtime.Remoting.Contexts;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.CommonBot;
using Styx.TreeSharp;

using Styx.Helpers;
using System;
using Styx.WoWInternals;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Warrior
{
    public class Arms
    {
        private static string[] _slows;

        #region Common
        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Normal)]
        public static Composite CreateArmsNormalPull()
        {
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                //face target
                Movement.CreateFaceTargetBehavior(),
                // LOS check
                Movement.CreateMoveToLosBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                //Dismount
                new Decorator(ret => StyxWoW.Me.Mounted,
                    Helpers.Common.CreateDismount("Pulling")),
                //Shoot flying targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Spell.Cast("Heroic Throw"),
                        Spell.Cast("Throw", ret => StyxWoW.Me.CurrentTarget.IsFlying && Item.RangedIsType(WoWItemWeaponClass.Thrown)),
                        Spell.Cast("Shoot", ret => StyxWoW.Me.CurrentTarget.IsFlying &&
                            (Item.RangedIsType(WoWItemWeaponClass.Bow) || Item.RangedIsType(WoWItemWeaponClass.Gun))),
                        Movement.CreateMoveToTargetBehavior(true, 27f)
                        )),

                //Buff up
                Spell.BuffSelf("Battle Shout", ret => (SingularSettings.Instance.Warrior.UseWarriorShouts) && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout")),
                Spell.BuffSelf("Commanding Shout", ret => StyxWoW.Me.RagePercent < 20 && SingularSettings.Instance.Warrior.UseWarriorShouts == false),

                //Charge
                Spell.Cast(
                    "Charge",
                    ret =>
                    StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance < 25 &&
                     SingularSettings.Instance.Warrior.UseWarriorCloser &&
                    Common.PreventDoubleCharge),
                //Heroic Leap
                Spell.CastOnGround(
                    "Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location,
                    ret =>
                    StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun", 1) &&
                    SingularSettings.Instance.Warrior.UseWarriorCloser &&
                    Common.PreventDoubleCharge),
                Spell.Cast(
                    "Heroic Throw",
                    ret =>
                    !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Charge Stun")),

                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        #endregion

        #region Normal
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior,WoWSpec.WarriorArms,WoWContext.Normal)]
        public static Composite CreateArmsNormalPreCombatBuffs()
        {
            return new PrioritySelector(
                //Buff up
                Spell.BuffSelf("Battle Shout", ret => (SingularSettings.Instance.Warrior.UseWarriorShouts) && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout")),
                Spell.BuffSelf("Commanding Shut", ret => SingularSettings.Instance.Warrior.UseWarriorShouts == false)
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Normal)]
        public static Composite CreateArmsNormalCombatBuffs()
        {
            return new PrioritySelector(
                // get enraged to heal up
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HealthPercent < 70 ),
                //Heal
                Spell.Buff("Enraged Regeneration", ret => StyxWoW.Me.HealthPercent < 60) ,

                //Retaliation if fighting elite or targeting player that swings
                Spell.Buff("Retaliation", ret => StyxWoW.Me.HealthPercent < 66 && StyxWoW.Me.CurrentTarget.DistanceSqr < 36 &&
                    (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) &&
                    StyxWoW.Me.CurrentTarget.PowerType != WoWPowerType.Mana),
                // Recklessness if caster or elite
                Spell.Buff("Recklessness", ret => (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite)),

                //Deadly calm if low on rage or during execute phase on bosses
                Spell.BuffSelf("Deadly Calm", ret => StyxWoW.Me.RagePercent < 10 ||
                    (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.CurrentHealth > StyxWoW.Me.CurrentHealth && StyxWoW.Me.CurrentTarget.HealthPercent < 20) ),
                //Inner Rage
                new Decorator(ret => HasSpellDeadlyCalm(),
                    new PrioritySelector(
                        Spell.BuffSelf("Inner Rage", ret => StyxWoW.Me.RagePercent > 90 && SpellManager.Spells["Deadly Calm"].Cooldown))),

                // Fear Remover
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified)),

                // Buff up
                Spell.BuffSelf("Commanding Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts == false),
                Spell.BuffSelf("Battle Shout", ret => (SingularSettings.Instance.Warrior.UseWarriorShouts) && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout"))
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Normal)]
        public static Composite CreateArmsNormalCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom", "Infected Wounds" };
            return new PrioritySelector(
                //Ensure Target
                Safers.EnsureTarget(),
                //LOS check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                // Dispel Bubbles
                new Decorator(
                    ret =>
                    StyxWoW.Me.CurrentTarget.IsPlayer &&
                    (StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") ||
                     StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") ||
                     StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")),
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30f)
                        )),

                //Rocket belt!
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.Distance > 20,
                              Item.UseEquippedItem((uint)WoWInventorySlot.Waist)),

                // Hands
                //Item.UseEquippedItem((uint) WoWInventorySlot.Hands),

                //Stance Dancing
                //Pop over to Zerker
                Spell.BuffSelf("Berserker Stance",
                               ret =>
                               StyxWoW.Me.CurrentTarget.HasMyAura("Rend") &&
                               !StyxWoW.Me.ActiveAuras.ContainsKey("Taste for Blood") && StyxWoW.Me.RagePercent < 75 &&
                               StyxWoW.Me.CurrentTarget.IsBoss() ),
                //Keep in Battle Stance
                Spell.BuffSelf("Battle Stance",
                               ret =>
                               !StyxWoW.Me.CurrentTarget.HasMyAura("Rend") ||
                               ((StyxWoW.Me.ActiveAuras.ContainsKey("Overpower") ||
                                 StyxWoW.Me.ActiveAuras.ContainsKey("Taste for Blood")) &&
                                SpellManager.Spells["Mortal Strike"].Cooldown) && StyxWoW.Me.RagePercent <= 75 ),

                Spell.Cast("Charge",
                           ret =>
                           StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25 &&
                           SingularSettings.Instance.Warrior.UseWarriorCloser && Common.PreventDoubleCharge),
                //Heroic Leap
                Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location,
                                   ret =>
                                   StyxWoW.Me.CurrentTarget.Distance > 9 &&
                                   !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun", 1) &&
                                   SingularSettings.Instance.Warrior.UseWarriorCloser && Common.PreventDoubleCharge),

                Movement.CreateMoveBehindTargetBehavior(),
                //use it or lose it
                Spell.Cast("Colossus Smash", ret => StyxWoW.Me.HasAura("Sudden Death", 1)),

                // ranged slow
                Spell.Buff("Piercing Howl",
                           ret =>
                           StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer &&
                           !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) &&
                           SingularSettings.Instance.Warrior.UseWarriorSlows),
                // Melee slow
                Spell.Cast("Hamstring",
                           ret =>
                           StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) &&
                           SingularSettings.Instance.Warrior.UseWarriorSlows),

                //freebie dps - use it if it's available
                Spell.Cast("Victory Rush"),

                // AOE
                new Decorator(
                    ret =>
                    Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3 ,
                    new PrioritySelector(
                // recklessness gets to be used in any stance soon
                        Spell.BuffSelf("Recklessness"),
                        Spell.BuffSelf("Sweeping Strikes"),
                        Spell.Cast("Bladestorm",ret =>StyxWoW.Me.CurrentTarget.DistanceSqr < 36),
                // cast rend if target doesn't have it and player has 'Blood and Thunder' talent
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Mortal Strike"))),

                //Interupts
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsCasting && SingularSettings.Instance.Warrior.UseWarriorInterrupts,
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // Only pop TD on elites/players
                        Spell.Cast("Throwdown", ret => (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite)),
                        Spell.Buff("Intimidating Shout", ret => StyxWoW.Me.CurrentTarget.Distance < 8 && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.IsCasting ))),

                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsBoss() && StyxWoW.Me.CurrentTarget.HealthPercent <= 25,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30))),

                // Use Engineering Gloves
                //Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                //Execute under 20%
                Spell.Cast("Execute", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 20),

                //Default Rotatiom
                Spell.Cast("Colossus Smash"),
                Spell.Cast("Mortal Strike"),
                //Bladestorm after dots and MS if against player
                Spell.Cast("Bladestorm", ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.DistanceSqr < 36),
                Spell.Cast("Overpower"),
                Spell.Cast("Slam", ret => StyxWoW.Me.RagePercent > 40),

                Spell.Cast("Cleave", ret =>
                    // Only even think about Cleave for more than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) >= 2 &&
                                    // If we have Incite, Deadly Calm, or enough rage (pooling for CS if viable) we're good.
                                (StyxWoW.Me.HasAura("Incite", 1) || CanUseRageDump() || StyxWoW.Me.ActiveAuras.ContainsKey("Deadly Calm"))),
                Spell.Cast("Heroic Strike", ret =>
                    // Only even think about HS for less than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) < 2 &&
                                    // If we have Incite, Deadly Calm, or enough rage (pooling for CS if viable) we're good.
                                (StyxWoW.Me.HasAura("Incite", 1) || CanUseRageDump() || StyxWoW.Me.ActiveAuras.ContainsKey("Deadly Calm"))),

                //ensure were in melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        #endregion

        #region Pvp
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Battlegrounds)]
        public static Composite CreateArmsPvpPreCombatBuffs()
        {
            return new PrioritySelector(
                //Buff up
                Spell.BuffSelf("Battle Shout", ret => (SingularSettings.Instance.Warrior.UseWarriorShouts) && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout")),
                Spell.BuffSelf("Commanding Shut", ret => SingularSettings.Instance.Warrior.UseWarriorShouts == false)
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Battlegrounds)]
        public static Composite CreateArmsPvpCombatBuffs()
        {
            return new PrioritySelector(
                // get enraged to heal up
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HealthPercent < 70) ,
                //Heal
                Spell.Buff("Enraged Regeneration", ret => StyxWoW.Me.HealthPercent < 60) ,

                //Retaliation if fighting elite or targeting player that swings
                Spell.Buff("Retaliation", ret => StyxWoW.Me.HealthPercent < 66 && StyxWoW.Me.CurrentTarget.DistanceSqr < 36 &&
                    (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) &&
                    StyxWoW.Me.CurrentTarget.PowerType != WoWPowerType.Mana),
                // Recklessness if caster or elite
                Spell.Buff("Recklessness", ret => (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) ),

                //Deadly calm if low on rage or during execute phase on bosses
                Spell.BuffSelf("Deadly Calm", ret => StyxWoW.Me.RagePercent < 10 ||
                    (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.CurrentHealth > StyxWoW.Me.CurrentHealth && StyxWoW.Me.CurrentTarget.HealthPercent < 20)),
                //Inner Rage
                new Decorator(ret => HasSpellDeadlyCalm(),
                    new PrioritySelector(
                        Spell.BuffSelf("Inner Rage", ret => StyxWoW.Me.RagePercent > 90 && SpellManager.Spells["Deadly Calm"].Cooldown))),

                // Fear Remover
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified)),

                // Buff up
                Spell.BuffSelf("Commanding Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts == false ),
                Spell.BuffSelf("Battle Shout", ret => (SingularSettings.Instance.Warrior.UseWarriorShouts) && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout"))
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Battlegrounds)]
        public static Composite CreateArmsPvpCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom", "Infected Wounds" };
            return new PrioritySelector(
                //Ensure Target
                Safers.EnsureTarget(),
                //LOS check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                Spell.BuffSelf("Battle Shout", ret => !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout")),
                Spell.BuffSelf("Commanding Shout", ret => StyxWoW.Me.RagePercent < 20),

                //Rocket belt!
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.Distance > 20,
                Item.UseEquippedItem((uint)WoWInventorySlot.Waist)),

                // Hands
                //Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                //Keep in Battle Stance
                Spell.BuffSelf("Battle Stance", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Rend") || ((StyxWoW.Me.ActiveAuras.ContainsKey("Overpower") || StyxWoW.Me.ActiveAuras.ContainsKey("Taste for Blood")) && SpellManager.Spells["Mortal Strike"].Cooldown) && StyxWoW.Me.RagePercent <= 75),

                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25  && SingularSettings.Instance.Warrior.UseWarriorCloser && Common.PreventDoubleCharge),

                Spell.Cast("Intercept", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25  && SingularSettings.Instance.Warrior.UseWarriorCloser && Common.PreventDoubleCharge),
                //Heroic Leap
                Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun", 1)  && SingularSettings.Instance.Warrior.UseWarriorCloser && Common.PreventDoubleCharge),

                Movement.CreateMoveBehindTargetBehavior(),
                //use it or lose it
                Spell.Cast("Colossus Smash", ret => StyxWoW.Me.HasAura("Sudden Death", 1)),

                // ranged slow
                Spell.Buff("Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows ),
                // Melee slow
                Spell.Cast("Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows ),

                //Melee Heal
                Spell.Cast("Victory Rush", ret => StyxWoW.Me.HealthPercent < 80),

                // AOE
                new Decorator(ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3 ,
                    new PrioritySelector(
                // recklessness gets to be used in any stance soon
                        Spell.BuffSelf("Recklessness") ,
                        Spell.BuffSelf("Sweeping Strikes"),
                        Spell.Cast("Bladestorm", ret => StyxWoW.Me.CurrentTarget.DistanceSqr < 36),
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Mortal Strike"))),

                //Interupts
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsCasting && SingularSettings.Instance.Warrior.UseWarriorInterrupts,
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // Only pop TD on elites/players
                        Spell.Buff("Intimidating Shout", ret => StyxWoW.Me.CurrentTarget.Distance < 8 && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.IsCasting ))),

                // Use Engineering Gloves
                //Item.UseEquippedItem((uint)WoWInventorySlot.Hands),                

                // Dispel Bubbles
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && (StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")) ,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30f)
                        )),
                //Execute under 20%
                Spell.Cast("Execute", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 20),

                //Default Rotatiom
                Spell.Buff("Overpower"),
                Spell.Cast("Throwdown", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 30 || StyxWoW.Me.HealthPercent < 60),
                Spell.Cast("Colossus Smash", ret => StyxWoW.Me.HasAura("Sudden Death", 1)),
                Spell.Cast("Mortal Strike"),
                //Spell.Cast("Slam", ret => StyxWoW.Me.RagePercent > 40 && SingularSettings.Instance.Warrior.UseWarriorSlamTalent),

                Spell.Cast("Cleave", ret =>
                    // Only even think about Cleave for more than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) >= 2 &&
                                    // If we have Incite, Deadly Calm, or enough rage (pooling for CS if viable) we're good.
                                (StyxWoW.Me.HasAura("Incite", 1) || CanUseRageDump() || StyxWoW.Me.ActiveAuras.ContainsKey("Deadly Calm"))),
                Spell.Cast("Heroic Strike", ret =>
                    // Only even think about HS for less than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) < 2 &&
                                    // If we have Incite, Deadly Calm, or enough rage (pooling for CS if viable) we're good.
                                (StyxWoW.Me.HasAura("Incite", 1) || CanUseRageDump() || StyxWoW.Me.ActiveAuras.ContainsKey("Deadly Calm"))),

                //ensure were in melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        #endregion

        #region Instances
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Instances)]
        public static Composite CreateArmsInstancePreCombatBuffs()
        {
            return new PrioritySelector(
                //Buff up
                Spell.BuffSelf("Battle Shout", ret => (SingularSettings.Instance.Warrior.UseWarriorShouts) && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout")),
                Spell.BuffSelf("Commanding Shut", ret => SingularSettings.Instance.Warrior.UseWarriorShouts == false)
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Instances)]
        public static Composite CreateArmsInstanceCombatBuffs()
        {
            return new PrioritySelector(
                // get enraged to heal up
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HealthPercent < 70 ),
                //Heal
                Spell.Buff("Enraged Regeneration", ret => StyxWoW.Me.HealthPercent < 60) ,

                //Retaliation if fighting elite or targeting player that swings
                Spell.Buff("Retaliation", ret => StyxWoW.Me.HealthPercent < 66 && StyxWoW.Me.CurrentTarget.DistanceSqr < 36 &&
                    (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) &&
                    StyxWoW.Me.CurrentTarget.PowerType != WoWPowerType.Mana),
                // Recklessness if caster or elite
                Spell.Buff("Recklessness", ret => (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite)),

                //Deadly calm if low on rage or during execute phase on bosses
                Spell.BuffSelf("Deadly Calm", ret => StyxWoW.Me.RagePercent < 10 ||
                    (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.CurrentHealth > StyxWoW.Me.CurrentHealth && StyxWoW.Me.CurrentTarget.HealthPercent < 20)),
                //Inner Rage
                new Decorator(ret => HasSpellDeadlyCalm(),
                    new PrioritySelector(
                        Spell.BuffSelf("Inner Rage", ret => StyxWoW.Me.RagePercent > 90 && SpellManager.Spells["Deadly Calm"].Cooldown))),

                // Fear Remover
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified)),

                // Buff up
                Spell.BuffSelf("Commanding Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts == false),
                Spell.BuffSelf("Battle Shout", ret => (SingularSettings.Instance.Warrior.UseWarriorShouts) && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout"))
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Instances)]
        public static Composite CreateArmsInstanceCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom", "Infected Wounds" };
            return new PrioritySelector(
                //Ensure Target
                Safers.EnsureTarget(),
                //LOS check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                // Dispel Bubbles
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && (StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")) ,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30f)
                        )),

                //Rocket belt!
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.Distance > 20,
                Item.UseEquippedItem((uint)WoWInventorySlot.Waist)),

                // Hands
                //Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                //Stance Dancing
                //Pop over to Zerker
                Spell.BuffSelf("Berserker Stance", ret => StyxWoW.Me.CurrentTarget.HasMyAura("Rend") && !StyxWoW.Me.ActiveAuras.ContainsKey("Taste for Blood") && StyxWoW.Me.RagePercent < 75 && StyxWoW.Me.CurrentTarget.IsBoss()),
                //Keep in Battle Stance
                Spell.BuffSelf("Battle Stance", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Rend") || ((StyxWoW.Me.ActiveAuras.ContainsKey("Overpower") || StyxWoW.Me.ActiveAuras.ContainsKey("Taste for Blood")) && SpellManager.Spells["Mortal Strike"].Cooldown) && StyxWoW.Me.RagePercent <= 75),

                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25  && SingularSettings.Instance.Warrior.UseWarriorCloser && Common.PreventDoubleCharge),
                //Heroic Leap
                Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun", 1)  && SingularSettings.Instance.Warrior.UseWarriorCloser && Common.PreventDoubleCharge),

                Movement.CreateMoveBehindTargetBehavior(),
                //use it or lose it
                Spell.Cast("Colossus Smash", ret => StyxWoW.Me.HasAura("Sudden Death", 1)),

                // ranged slow
                Spell.Buff("Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows ),
                // Melee slow
                Spell.Cast("Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows) ,

             //freebie dps - use it if it's available
                Spell.Cast("Victory Rush"),

                // AOE
                new Decorator(ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3 ,
                    new PrioritySelector(
                // recklessness gets to be used in any stance soon
                        Spell.BuffSelf("Recklessness"),
                        Spell.BuffSelf("Sweeping Strikes"),
                        Spell.Cast("Bladestorm", ret =>  StyxWoW.Me.CurrentTarget.DistanceSqr < 36),
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Mortal Strike"))),

                //Interupts
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsCasting && SingularSettings.Instance.Warrior.UseWarriorInterrupts,
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // Only pop TD on elites/players
                        Spell.Cast("Throwdown", ret => (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) ),
                        Spell.Buff("Intimidating Shout", ret => StyxWoW.Me.CurrentTarget.Distance < 8 && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.IsCasting ))),

                        new Decorator(ret => StyxWoW.Me.CurrentTarget.IsBoss() && StyxWoW.Me.CurrentTarget.HealthPercent <= 25,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30))),

                // Use Engineering Gloves
                //Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                //Default Rotatiom
                Spell.Cast("Colossus Smash"),
                Spell.Cast("Mortal Strike", ret => (!StyxWoW.Me.HasAura("Slaughter", 3) || StyxWoW.Me.GetAuraTimeLeft("Slaughter", true).TotalSeconds <= 6)
                                                    && StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                //Execute when high rage
                Spell.Cast("Execute", ret => StyxWoW.Me.RagePercent > 30),

                //Default Rotation
                Spell.Cast("Mortal Strike"),
                Spell.Cast("Execute", ret => StyxWoW.Me.RagePercent < 30),

                //Bladestorm after dots and MS if against player
                Spell.Cast("Bladestorm", ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.DistanceSqr < 36 ),
                Spell.Cast("Overpower"),
                Spell.Cast("Slam", ret => StyxWoW.Me.RagePercent > 40),

                Spell.Cast("Cleave", ret =>
                    // Only even think about Cleave for more than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) >= 2 &&
                                    // If we have Incite, Deadly Calm, or enough rage (pooling for CS if viable) we're good.
                                (StyxWoW.Me.HasAura("Incite", 1) || CanUseRageDump() || StyxWoW.Me.ActiveAuras.ContainsKey("Deadly Calm"))),
                Spell.Cast("Heroic Strike", ret =>
                    // Only even think about HS for less than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) < 2 &&
                                    // If we have Incite, Deadly Calm, or enough rage (pooling for CS if viable) we're good.
                                (StyxWoW.Me.HasAura("Incite", 1) || CanUseRageDump() || StyxWoW.Me.ActiveAuras.ContainsKey("Deadly Calm"))),

                //ensure were in melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        #endregion

        #region Utils

        static bool HasSpellDeadlyCalm()
        {
            if (SpellManager.HasSpell("Deadly Calm"))
                return true;
            return false;
        }

        static bool CanUseRageDump()
        {
            // Pooling rage for upcoming CS. If its > 8s, make sure we have 60 rage. < 8s, only pop it at 85 rage.
            if (SpellManager.HasSpell("Colossus Smash"))
                return SpellManager.Spells["Colossus Smash"].CooldownTimeLeft.TotalSeconds > 8 ? StyxWoW.Me.RagePercent > 60 : StyxWoW.Me.RagePercent > 85;

            // We don't know CS. So just check if we have 60 rage to use cleave.
            return StyxWoW.Me.RagePercent > 60;
        }
        #endregion
    }
}