using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

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
        [Context(WoWContext.All)]
        public static Composite CreateArmsCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom", "Infected Wounds" };
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
                Spell.Buff("Intimidating Shout", ret => StyxWoW.Me.CurrentTarget.Distance < 8 && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.IsCasting && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                // Dispel Bubbles
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield") && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false, 
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30f)
                        )),

                //Rocket belt! ----- Still Bugged
                // new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.Distance > 20, 
                //     Item.UseEquippedItem((uint)WoWInventorySlot.Waist)),
                
                //Charge
                Movement.CreateFaceTargetBehavior(),
                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                //Heroic Leap
                //Spell.CastOnGround("Heroic Leap",ret=>StyxWoW.Me.CurrentTarget.Location,ret=>StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun", 1)),
                new Decorator(
                    ret => SpellManager.CanCast("Heroic Leap") && StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun") && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false,
                    new Action(
                            ret =>
                            {
                                SpellManager.Cast("Heroic Leap");
                                LegacySpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                            })),

                //use it or lose it
                Spell.Cast("Colossus Smash", ret => StyxWoW.Me.HasAura("Sudden Death", 1)),

                // ranged slow
                Spell.Buff("Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                // Melee slow
                Spell.Cast("Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),

                //Mele Heal
                Spell.Cast("Victory Rush", ret => StyxWoW.Me.HealthPercent < 80),

                // AOE
                new Decorator(
                    ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3 && SingularSettings.Instance.Warrior.UseWarriorAOE,
                    new PrioritySelector(
                        // recklessness gets to be used in any stance soon
                        Spell.BuffSelf("Recklessness"),
                        Spell.BuffSelf("Sweeping Strikes"),
                        Spell.Cast("Bladestorm"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Mortal Strike"))),

                //Interupts
                new Decorator(
                    ret=>StyxWoW.Me.CurrentTarget.IsCasting && SingularSettings.Instance.Warrior.UseWarriorInterupts,
                    new PrioritySelector(
                        Spell.Cast("Pummel"),
                        // Only pop TD on elites/players
                        Spell.Cast("Throwdown", ret=>StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite))),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsBoss() && StyxWoW.Me.CurrentTarget.HealthPercent <= 25,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30))),

                //Rage Dump
                Spell.Cast("Heroic Strike", ret => StyxWoW.Me.HasAura("Incite", 1) || StyxWoW.Me.RagePercent > 60),

                // Use Engineering Gloves ------ Still Bugged
                // Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                //Execute under 20%
                Spell.Cast("Execute", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 20),

                //Default Rotatiom
                Spell.Buff("Rend"),
                Spell.Cast("Colossus Smash"),                
                Spell.Cast("Mortal Strike"),
                //Bladestorm after dots and MS if against player
                Spell.Cast("Bladestorm", ret => StyxWoW.Me.CurrentTarget.IsPlayer),
                Spell.Cast("Overpower"),
                Spell.Cast("Slam", ret => StyxWoW.Me.RagePercent > 40 && SingularSettings.Instance.Warrior.UseWarriorSlamTalent),
                //ensure were in melee
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.All)]
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

                //Dismount
                new Decorator(ret => StyxWoW.Me.Mounted,
                    new Action(o => Styx.Logic.Mount.Dismount())),

                //Shoot flying targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Spell.Cast("Heroic Throw"),
                        Spell.Cast("Shoot"),
                        Spell.Cast("Throw")
                    )),

                //Buff up
                Spell.BuffSelf("Battle Shout", ret => StyxWoW.Me.RagePercent < 20 && SingularSettings.Instance.Warrior.UseWarriorShouts),
                Spell.BuffSelf("Commanding Shout", ret => StyxWoW.Me.RagePercent < 20 && SingularSettings.Instance.Warrior.UseWarriorShouts == false),

                //Charge
                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance < 25 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                //Heroic Leap
                //Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun", 1) && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                //Heroic Throw if not already charging
                new Decorator(
                    ret => SpellManager.CanCast("Heroic Leap") && StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun") && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false,
                    new Action(
                            ret =>
                            {
                                SpellManager.Cast("Heroic Leap");
                                LegacySpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                            })),
                Spell.Cast("Heroic Throw", ret => !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Charge Stun") && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),

                // Move to Melee
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateArmsCombatBuffs()
        {
            return new PrioritySelector(                
                // get enraged to heal up
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HealthPercent < 70 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                //Heal
                Spell.Buff("Enraged Regeneration", ret => StyxWoW.Me.HealthPercent < 60 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),

                //Retaliation if fighting elite or targeting player that swings
                Spell.Buff("Retaliation", ret => (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) && StyxWoW.Me.CurrentTarget.PowerType != WoWPowerType.Mana && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false && SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns),
                // Recklessness if caster or elite
                Spell.Buff("Recklessness", ret => (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) && StyxWoW.Me.CurrentTarget.PowerType == WoWPowerType.Mana && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false && SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns),
                
                //Deadly calm if low on rage or during execute phase on bosses
                Spell.BuffSelf("Deadly Calm", ret => StyxWoW.Me.RagePercent < 10 || StyxWoW.Me.CurrentTarget.CurrentHealth > StyxWoW.Me.CurrentHealth && StyxWoW.Me.CurrentTarget.HealthPercent < 20 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false && SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns),
                //Inner Rage
                Spell.BuffSelf("Inner Rage", ret => StyxWoW.Me.RagePercent > 90 && SpellManager.Spells["Deadly Calm"].Cooldown && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false && SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns),

                // Fear Remover
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified)),
                
                // Buff up
                Spell.BuffSelf("Commanding Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts == false && SingularSettings.Instance.Warrior.UseWarriorT12),
                Spell.BuffSelf("Battle Shout", ret => !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout") || SingularSettings.Instance.Warrior.UseWarriorT12)
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateArmsPreCombatBuffs()
        {
            return new PrioritySelector(
                //Buff up
                Spell.BuffSelf("Battle Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts),
                Spell.BuffSelf("Commanding Shut", ret => SingularSettings.Instance.Warrior.UseWarriorShouts == false)
                );
        }
    }
}