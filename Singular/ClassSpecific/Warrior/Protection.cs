using System.Linq;
using System.Runtime.Remoting.Contexts;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;

using Styx.Helpers;
using System;
using Styx.WoWInternals;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;

using Styx.Common;
using System.Drawing;
using CommonBehaviors.Actions;

namespace Singular.ClassSpecific.Warrior
{
    public partial class Protection
    {

        #region Common

        static LocalPlayer Me { get { return StyxWoW.Me; } }
        static WoWUnit Target { get { return StyxWoW.Me.CurrentTarget; } }
        static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }

        public static bool talentGladiator { get; set; }
        public static bool glyphCleave { get; set; }
        private static CombatScenario scenario { get; set; }


        [Behavior(BehaviorType.Initialize, WoWClass.Warrior, WoWSpec.WarriorProtection)]
        public static Composite CreateProtectionInit()
        {
            talentGladiator = Common.HasTalent(WarriorTalents.GladiatorsResolve);
            glyphCleave = TalentManager.HasGlyph("Cleave");
            scenario = new CombatScenario(8, 1.5f);
            return null;
        }

        [Behavior(BehaviorType.Rest, WoWClass.Warrior, WoWSpec.WarriorProtection)]
        public static Composite CreateProtectionRest()
        {
            return new PrioritySelector(

                Common.CheckIfWeShouldCancelBladestorm(),

                Singular.Helpers.Rest.CreateDefaultRestBehaviour(),

                ClassSpecific.Warrior.Protection.CheckThatShieldIsEquippedIfNeeded()
                );
        }


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalPreCombatBuffs()
        {
            return new PrioritySelector(

                    // no shield means no shield slam, so use Battle Stance for more Rage generation for 
                // ... those Prot warriors the owner didnt see fit to give a shield
                // Spell.BuffSelf( stance => HasShieldInOffHand ? "Defensive Stance" : "Battle Stance", req => true),

                    // PartyBuff.BuffGroup(Common.SelectedShoutAsSpellName)
                // PartyBuff.BuffGroup( "Battle Shout", ret => WarriorSettings.Shout == WarriorShout.BattleShout ),
                // PartyBuff.BuffGroup( "Commanding Shout", ret => WarriorSettings.Shout == WarriorShout.CommandingShout )
                    
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Common.CreateChargeBehavior(),

                        //Buff up 
                        new Throttle( TimeSpan.FromSeconds(2), 
                            new PrioritySelector(
                                PartyBuff.BuffGroup(Common.SelectedShoutAsSpellName)
                                // Spell.Cast("Battle Shout", ret => !Me.HasAura("Battle Shout") && !Me.HasMyAura("Commanding Shout") && !Me.HasPartyBuff(PartyBuffType.AttackPower)),
                                // Spell.Cast("Commanding Shout", ret => !Me.HasAura("Battle Shout") && !Me.HasMyAura("Battle Shout") && !Me.HasPartyBuff(PartyBuffType.Stamina))
                                )
                            ),

                        Spell.Cast( "Shield Slam", req => HasShieldInOffHand ),

                        // just in case user botting a Prot Warrior without a shield
                        Spell.Cast("Revenge"),
                        Spell.Cast("Devastate", ret => !Me.CurrentTarget.HasAura("Weakened Armor", 3)),
                        Spell.Cast("Thunder Clap", ret => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8) && !Me.CurrentTarget.ActiveAuras.ContainsKey("Weakened Blows")),

                        // filler to try and do something more than auto attack at this point
                        Spell.Cast("Devastate"),
                        Spell.Cast("Heroic Strike"),

                        CheckThatShieldIsEquippedIfNeeded()
                        )
                    )
                );
        }
        #endregion

        #region Normal

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionCombatBuffs()
        {
            return new Decorator(
                req => !Unit.IsTrivial(Me.CurrentTarget)
                    && (SingularRoutine.CurrentWoWContext != WoWContext.Instances || Me.Shapeshift != (ShapeshiftForm)WarriorStance.GladiatorStance),
                new Throttle(    // throttle these because most are off the GCD
                    new PrioritySelector(
                        Spell.HandleOffGCD(Spell.Cast("Demoralizing Shout", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(m => m.SpellDistance() < 10), req => true, gcd: HasGcd.No)),

                        Spell.HandleOffGCD( 
                            new PrioritySelector(
                                Spell.BuffSelf("Shield Wall", ret => Me.HealthPercent < WarriorSettings.WarriorShieldWallHealth, gcd: HasGcd.No ),
                                Spell.BuffSelf("Shield Barrier", ret => Me.HealthPercent < WarriorSettings.WarriorShieldBarrierHealth, gcd: HasGcd.No),
                                Spell.BuffSelf("Shield Block", ret => Me.HealthPercent < WarriorSettings.WarriorShieldBlockHealth, gcd: HasGcd.No)
                                )
                            ),

                        Spell.HandleOffGCD(
                            new PrioritySelector(
                                Spell.HandleOffGCD(Spell.BuffSelf("Last Stand", ret => Me.HealthPercent < WarriorSettings.WarriorLastStandHealth, gcd: HasGcd.No)),
                                Spell.HandleOffGCD(Common.CreateWarriorEnragedRegeneration())
                                )
                            ),

                        new Decorator(
                            req => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange,
                            new PrioritySelector(
                                new Decorator(
                                    ret => Me.CurrentTarget.IsBoss() || Me.CurrentTarget.IsPlayer || (!Me.IsInGroup() && scenario.MobCount >= 3),
                                    new PrioritySelector(
                                        Spell.HandleOffGCD( Spell.BuffSelf("Recklessness", req => true, 0, HasGcd.No)),
                                        Spell.HandleOffGCD( Spell.BuffSelf("Avatar", req => true, 0, HasGcd.No))
                                        )
                                    ),

                                Spell.BuffSelfAndWait("Bloodbath", gcd: HasGcd.No),
                                Spell.BuffSelfAndWait("Berserker Rage", gcd: HasGcd.No)
                                )
                            )

                // new Action(ret => { UseTrinkets(); return RunStatus.Failure; }),
                // Spell.Cast("Deadly Calm", ret => TalentManager.HasGlyph("Incite") || Me.CurrentRage >= RageDump)
                        )
                    )
                );
        }

        static WoWUnit intTarget;

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionCombat()
        {
            UpdateWhetherWarriorNeedsTankTargeting();

            return new PrioritySelector(
                // set context to current target
                ctx => 
                {
                    if (TankManager.NeedTankTargeting && TankManager.Instance.FirstUnit != null)
                        return TankManager.Instance.FirstUnit;

                    return Me.CurrentTarget;
                },

                // establish here whether tank targeting is needed
                new Action( r => { UpdateWhetherWarriorNeedsTankTargeting(); return RunStatus.Failure; } ),

                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(FaceDuring.Yes),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        new Action(r =>
                        {
                            scenario.Update(Target);
                            return RunStatus.Failure;
                        }),

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        new Decorator(
                            req => Me.GotTarget() && Me.Shapeshift != (ShapeshiftForm) WarriorStance.GladiatorStance,
                            CreateProtectionDefensiveCombat()
                            ),

                        new Decorator(
                            req => Me.GotTarget() && Me.Shapeshift == (ShapeshiftForm)WarriorStance.GladiatorStance,
                            CreateProtectionGladiatorCombat()                            
                            )
                        )
                    )
                );
        }

        private static void UpdateWhetherWarriorNeedsTankTargeting()
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                TankManager.NeedTankTargeting = false;
            else if (StyxWoW.Me.Shapeshift == (ShapeshiftForm)WarriorStance.GladiatorStance)
                TankManager.NeedTankTargeting = false;
            else
                TankManager.NeedTankTargeting = true;
        }

        private static Composite CreateProtectionDefensiveCombat()
        {
            return new PrioritySelector(

                CreateProtectionDiagnosticOutput(),

                Common.CreateVictoryRushBehavior(),

                new Decorator(
                    ret => SingularSettings.Instance.EnableTaunting && SingularRoutine.CurrentWoWContext == WoWContext.Instances,
                    CreateProtectionTauntBehavior()
                    ),

                new Sequence(
                    new Decorator(
                        ret => Common.IsSlowNeeded(Me.CurrentTarget),
                        new PrioritySelector(
                            Spell.Buff("Hamstring")
                            )
                        ),
                    new Wait(TimeSpan.FromMilliseconds(500), until => !Common.IsSlowNeeded(Me.CurrentTarget), new ActionAlwaysSucceed())
                    ),

                CreateProtectionInterrupt(),

                // special "in combat" pull logic for mobs not tagged and out of melee range
                Common.CreateWarriorCombatPullMore(),

                // Multi-target?  get the debuff on them
                new Decorator(
                    ret => scenario.MobCount > 1,
                    new PrioritySelector(
                        Spell.Cast("Thunder Clap", on => Unit.UnfriendlyUnits(Common.DistanceWindAndThunder(8)).FirstOrDefault()),
                        Spell.Cast("Bladestorm", on => Unit.UnfriendlyUnits(8).FirstOrDefault(), ret => scenario.MobCount >= 4),
                        Spell.Cast("Shockwave", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => Me.IsSafelyFacing(u)), ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 3),
                        Spell.Cast("Dragon Roar", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => Me.IsSafelyFacing(u)), ret => Me.CurrentTarget.SpellDistance() <= 8 || Me.CurrentTarget.IsWithinMeleeRange)
                        )
                    ),

                Common.CreateExecuteOnSuddenDeath(),

                // Generate Rage
                Spell.Cast("Shield Slam", ret => Me.CurrentRage < RageBuild && HasShieldInOffHand),
                Spell.Cast("Revenge"),
                Spell.Cast("Execute", ret => Me.CurrentRage > RageDump && Me.CurrentTarget.HealthPercent <= 20),
                Spell.Cast("Thunder Clap", ret => Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8) && !Me.CurrentTarget.ActiveAuras.ContainsKey("Weakened Blows")),

                // Filler
                Spell.Cast("Devastate"),

                // Dump Rage
                new Throttle(
                    Spell.Cast(
                        "Heroic Strike", 
                        on => Me.CurrentTarget,
                        req => (Spell.UseAOE || !scenario.AvoidAOE || !glyphCleave) 
                            && (HasUltimatum || Me.CurrentRage > RageDump || !SpellManager.HasSpell("Devastate") || !HasShieldInOffHand),
                        gcd: HasGcd.No
                        )
                    ),

                //Charge
                Common.CreateChargeBehavior(),

                new Action(ret =>
                {
                    if (Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && Me.IsSafelyFacing(Me.CurrentTarget))
                        Logger.WriteDebug("--- we did nothing!");
                    return RunStatus.Failure;
                })
            );

        }


        public static Composite CreateProtectionGladiatorCombat()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                return CreateProtectionGladiatorCombatInstances();

            return new PrioritySelector(

                CreateGladiatorDiagnosticOutput(),

                Common.CreateVictoryRushBehavior(),

                new Decorator(
                    ret => SingularSettings.Instance.EnableTaunting && SingularRoutine.CurrentWoWContext == WoWContext.Instances,
                    CreateProtectionTauntBehavior()
                    ),

                new Sequence(
                    new Decorator(
                        ret => SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || (Me.GotTarget() && Me.CurrentTarget.IsPlayer)
                            && Common.IsSlowNeeded(Me.CurrentTarget),
                        new Sequence(
                            new PrioritySelector(
                                Spell.Buff("Hamstring")
                                ),
                            new Wait(TimeSpan.FromMilliseconds(500), until => !Common.IsSlowNeeded(Me.CurrentTarget), new ActionAlwaysSucceed())
                            )
                        )
                    ),

                CreateProtectionInterrupt(),

                // special "in combat" pull logic for mobs not tagged and out of melee range
                Common.CreateWarriorCombatPullMore(),

                // Multi-target?  get the debuff on them
                new Decorator(
                    ret => scenario.MobCount > 1,
                    new PrioritySelector(
                        Spell.Cast("Thunder Clap", on => Unit.UnfriendlyUnits(Common.DistanceWindAndThunder(8)).FirstOrDefault()),
                        Spell.Cast("Bladestorm", on => Unit.UnfriendlyUnits(8).FirstOrDefault(), ret => scenario.MobCount >= 4),
                        Spell.Cast("Shockwave", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => Me.IsSafelyFacing(u)), ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 3),
                        Spell.Cast("Dragon Roar", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => Me.IsSafelyFacing(u)), ret => Me.CurrentTarget.SpellDistance() <= 8 || Me.CurrentTarget.IsWithinMeleeRange)
                        )
                    ),

                Common.CreateExecuteOnSuddenDeath(),

                CreateShieldCharge( null, req => Spell.GetCharges("Shield Charge") >= 2 || !Me.HasAura("Shield Charge")),
                Spell.HandleOffGCD(new Throttle(Spell.Cast("Heroic Strike", on => Me.CurrentTarget, req => Me.HasAura("Shield Charge") || HasUltimatum || Me.CurrentRage > RageDump, gcd: HasGcd.No))),
                Spell.Cast("Shield Slam", ret => Me.CurrentRage < RageBuild && HasShieldInOffHand),
                Spell.Cast("Revenge"),
                Spell.Cast("Execute", ret => Me.CurrentRage > RageDump && Me.CurrentTarget.HealthPercent <= 20),
                Spell.Cast(
                    "Thunder Clap", 
                    ret => Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8) 
                        && !Me.CurrentTarget.ActiveAuras.ContainsKey("Weakened Blows")
                    ),
                Spell.Cast("Devastate"),
                Spell.Cast("Heroic Strike", req => !HasShieldInOffHand),

                //Charge
                Common.CreateChargeBehavior(),

                new Action(ret =>
                {
                    if (Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && Me.IsSafelyFacing(Me.CurrentTarget))
                        Logger.WriteDebug("--- we did nothing!");
                    return RunStatus.Failure;
                })
                );
        }


        public static Composite CreateProtectionGladiatorCombatInstances()
        {
            Generic.SuppressGenericRacialBehavior = true;

            return new PrioritySelector(

                new Action(r =>
                {
                    scenario.Update(Target);
                    return RunStatus.Failure;
                }),

                CreateGladiatorDiagnosticOutput(),

                CreateProtectionInterrupt(),

                // # Executed every time the actor is available.
                // 
                // actions=charge
                Common.CreateChargeCloser(),

                // actions+=/auto_attack
                // .. already handled by Singular

                // # This is mostly to prevent cooldowns from being accidentally used during movement.
                // actions+=/call_action_list,name=movement,if=movement.distance>5
                new Decorator( 
                    req => Target.SpellDistance() > 5,
                    new PrioritySelector(
                        // actions.movement=heroic_leap
                        Common.CreateHeroicLeapCloser(),
                        // actions.movement+=/shield_charge
                        Common.CreateShieldChargeCloser(),
                        // # May as well throw storm bolt if we can.
                        // actions.movement+=/storm_bolt
                        Spell.Cast("Storm Bolt", req => Me.IsMoving && !Target.IsWithinMeleeRange),
                        // actions.movement+=/heroic_throw
                        Spell.Cast("Heroic Throw", req => Me.IsMoving && !Target.IsWithinMeleeRange),

                        // don't allow to proceed when moving    
                        new ActionAlwaysSucceed()
                        )
                    ),

                // actions+=/avatar
                Spell.BuffSelfAndWait("Avatar", gcd: HasGcd.No),
                // actions+=/bloodbath
                Spell.BuffSelfAndWait("Bloodbath", gcd: HasGcd.No),
                // actions+=/blood_fury,if=buff.bloodbath.up|buff.avatar.up|buff.shield_charge.up|target.time_to_die<10
                Spell.BuffSelfAndWait("Blood Fury", req => Me.HasAnyAura("Bloodbath", "Avatar", "Shield Charge") || Target.TimeToDeath() < 10, gcd: HasGcd.No),
                // actions+=/berserking,if=buff.bloodbath.up|buff.avatar.up|buff.shield_charge.up|target.time_to_die<10
                Spell.BuffSelfAndWait("Berserking", req => Me.HasAnyAura("Bloodbath", "Avatar", "Shield Charge") || Target.TimeToDeath() < 10, gcd: HasGcd.No),
                // actions+=/arcane_torrent,if=rage<rage.max-40
                Spell.BuffSelfAndWait("Arcane Torrent", req => Me.CurrentRage < Me.MaxRage - 40, gcd: HasGcd.No),
                // actions+=/potion,name=draenic_armor,if=buff.bloodbath.up|buff.avatar.up|buff.shield_charge.up
                // ... ignore potions
                // actions+=/shield_charge,if=(!buff.shield_charge.up&!cooldown.shield_slam.remains)|charges=2
                Spell.Cast("Shield Charge", ret => HasShieldInOffHand && (!Me.HasAura("Shield Charge") || Spell.GetCharges("Shield Charge") >= 2)),
                // actions+=/berserker_rage,if=buff.enrage.down
                Spell.BuffSelfAndWait("Berserker Rage", req => !Me.HasAura("Enrage")), // wowhead doesnt show this effect, but ok
                // actions+=/heroic_leap,if=(raid_event.movement.distance>25&raid_event.movement.in>45)|!raid_event.movement.exists
                Common.CreateHeroicLeapCloser(),

                Spell.HandleOffGCD(
                    new Throttle(
                        // actions+=/heroic_strike,if=(buff.shield_charge.up|(buff.unyielding_strikes.up&rage>=50-buff.unyielding_strikes.stack*5))&target.health.pct>20
                        Spell.Cast(
                            "Heroic Strike", 
                            on => Target,
                            req => Me.HasAura("Shield Charge") 
                                || Me.HasAura("Unyielding Strikes") && Me.CurrentRage >= (50 - 5 * Me.GetAuraStacks("Unyielding Strikes")) && Target.HealthPercent > 20,
                            gcd: HasGcd.No
                            ),
                        // actions+=/heroic_strike,if=buff.ultimatum.up|rage>=rage.max-20|buff.unyielding_strikes.stack>4|target.time_to_die<10
                        Spell.Cast(
                            "Heroic Strike",
                            on => Target,
                            req => Me.HasAura("Ultimatum")
                                || Me.CurrentRage > Me.MaxRage - 20
                                || Me.GetAuraStacks("Unyielding Strikes") > 4
                                || Target.TimeToDeath() < 10,
                            gcd: HasGcd.No
                            )
                        )
                    ),

                // actions+=/call_action_list,name=single,if=active_enemies=1               
                // actions+=/call_action_list,name=aoe,if=active_enemies>=2
               
                new Decorator(
                    ret => scenario.MobCount <= 1,
                    new PrioritySelector(
                        // actions.single=devastate,if=buff.unyielding_strikes.stack>0&buff.unyielding_strikes.stack<6&buff.unyielding_strikes.remains<1.5
                        Spell.Cast("Devastate", req => Me.GetAuraStacks("Unyielding Strikes").Between(1u, 5u) && Me.GetAuraTimeLeft("Unyielding Strikes") < TimeSpan.FromSeconds(1.5)),
                        // actions.single+=/shield_slam
                        Spell.Cast("Shield Slam"),
                        // actions.single+=/revenge
                        Spell.Cast("Revenge"),
                        // actions.single+=/execute,if=buff.sudden_death.react
                        Common.CreateExecuteOnSuddenDeath(),
                        // actions.single+=/storm_bolt
                        Spell.Cast("Storm Bolt"),
                        // actions.single+=/dragon_roar
                        Spell.Cast("Dragon Roar"),
                        // actions.single+=/execute,if=rage>60&target.health.pct<20
                        Spell.Cast("Execute", req => Me.CurrentRage > 60 && Target.HealthPercent < 20),
                        // actions.single+=/devastate
                        Spell.Cast("Devastate")
                        )
                    ),

                new Decorator(
                    ret => scenario.MobCount > 1,
                    new PrioritySelector(
                        // 
                        // actions.aoe=revenge
                        Spell.Cast("Revenge"),
                        // actions.aoe+=/shield_slam
                        Spell.Cast("Shield Slam"),
                        // actions.aoe+=/dragon_roar,if=(buff.bloodbath.up|cooldown.bloodbath.remains>10)|!talent.bloodbath.enabled
                        Spell.Cast(
                            "Dragon Roar",
                            req => Me.HasAura("Bloodbath") || Spell.GetSpellCooldown("Bloodbath").TotalSeconds > 10
                            ),
                        // actions.aoe+=/storm_bolt,if=(buff.bloodbath.up|cooldown.bloodbath.remains>7)|!talent.bloodbath.enabled
                        Spell.Cast(
                            "Storm Bolt",
                            req => Me.HasAura("Bloodbath") || Spell.GetSpellCooldown("Bloodbath").TotalSeconds > 7
                            ),
                        // actions.aoe+=/thunder_clap,cycle_targets=1,if=dot.deep_wounds.remains<3&active_enemies>4
                        Spell.Cast(
                            "Thunder Clap", 
                            req => Target.GetAuraTimeLeft("Deep Wounds").TotalSeconds < 3 && scenario.MobCount > 4
                            ),
                        // actions.aoe+=/bladestorm,if=buff.shield_charge.down
                        Spell.Cast(
                            "Bladestorm", 
                            on => Unit.UnfriendlyUnits(8).FirstOrDefault(), 
                            req => scenario.MobCount >= 4 && !Me.HasAura("Shield Charge")
                            ),
                        // actions.aoe+=/execute,if=buff.sudden_death.react
                        Common.CreateExecuteOnSuddenDeath(),
                        // actions.aoe+=/thunder_clap,if=active_enemies>6
                        Spell.Cast(
                            "Thunder Clap",
                            req => scenario.MobCount > 6
                            ),
                        // actions.aoe+=/devastate,cycle_targets=1,if=dot.deep_wounds.remains<5&cooldown.shield_slam.remains>execute_time*0.4
                        // .. redundant since next action would make same cast with a subset of criteria
                        // actions.aoe+=/devastate,if=cooldown.shield_slam.remains>execute_time*0.4
                        Spell.Cast(                    
                            "Devastate",
                            req => Spell.GetSpellCooldown("Shield Slam").TotalSeconds > (1.5 * 0.4 * Me.SpellHasteModifier)
                            )
                        )
                    ),

                new Action(ret =>
                {
                    if (Me.GotTarget() && Target.IsWithinMeleeRange && Me.IsSafelyFacing(Target))
                        Logger.WriteDebug("--- we did nothing!");
                    return RunStatus.Failure;
                })
                );
        }

        static Composite CreateProtectionTauntBehavior()
        {
            // limit all taunt attempts to 1 per second max since Mocking Banner and Taunt have no GCD
            // .. it will keep us from casting both for the same mob we lost aggro on
            return new Throttle( 1, 1,
                new PrioritySelector(
                    ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(),

                    Spell.CastOnGround("Mocking Banner",
                        on => (WoWUnit) on,
                        ret => TankManager.Instance.NeedToTaunt.Any() && Clusters.GetCluster(TankManager.Instance.NeedToTaunt.FirstOrDefault(), TankManager.Instance.NeedToTaunt, ClusterType.Radius, 15f).Count() >= 2),

                    Spell.Cast("Taunt", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault()),

                    Spell.Cast("Storm Bolt", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(i => i.SpellDistance() < 30 && Me.IsSafelyFacing(i))),

                    Spell.Cast("Intervene", 
                        ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(
                            mob => Group.Healers.Any(healer => mob.CurrentTargetGuid == healer.Guid && healer.Distance < 25)),
                        ret => MovementManager.IsClassMovementAllowed && Group.Healers.Count( h => h.IsAlive && h.Distance < 40) == 1
                        )
                    )
                );
        }

        static Composite CreateProtectionInterrupt()
        {
            return new Throttle(
                new PrioritySelector(
                    new Action(ret =>
                    {
                        intTarget = Unit.NearbyUnfriendlyUnits.FirstOrDefault(i => i.IsCasting && i.CanInterruptCurrentSpellCast && i.IsWithinMeleeRange && Me.IsSafelyFacing(i));
                        return RunStatus.Failure;
                    }),

                    Spell.Cast("Pummel", ctx => intTarget),

                    new Action(ret =>
                    {
                        intTarget = Unit.NearbyUnfriendlyUnits.FirstOrDefault(i => i.IsCasting && i.CanInterruptCurrentSpellCast && i.Distance < 10);
                        return RunStatus.Failure;
                    }),

                    Spell.Cast("Disrupting Shout", ctx => intTarget),

                    new Action(ret =>
                    {
                        intTarget = Unit.NearbyUnfriendlyUnits.FirstOrDefault(i => i.IsCasting && i.CanInterruptCurrentSpellCast && i.Distance < 30 && Me.IsSafelyFacing(i));
                        return RunStatus.Failure;
                    }),

                    Spell.Cast("Storm Bolt", ctx => intTarget)
                    )
                );
        }

        static int RageBuild
        {
            get
            {
                return (int)Me.MaxRage - 5;
            }
        }

        static int RageDump
        {
            get
            {
                return (int)Me.MaxRage - 20;
            }
        }

        static bool HasUltimatum
        {
            get
            {
                return Me.ActiveAuras.ContainsKey("Ultimatum");
            }
        }

        private static Composite _checkShield = null;

        public static Composite CheckThatShieldIsEquippedIfNeeded()
        {
            if (_checkShield == null)
            {
                _checkShield = new ThrottlePasses(60,
                    new Sequence(
                        new DecoratorContinue(
                            ret => !Me.Disarmed && !HasShieldInOffHand && SpellManager.HasSpell("Shield Slam"),
                            new Action(ret => Logger.Write(Color.HotPink, "User Error: a{0} requires a Shield in offhand to cast Shield Slam", SingularRoutine.SpecAndClassName()))
                            ),
                        new ActionAlwaysFail()
                        )
                    );
            }
            return _checkShield;
        }

        private static bool _hasShieldInOffHand { get; set; }
        public static bool HasShieldInOffHand
        {
            get
            {
                bool hasShield = IsShield(Me.Inventory.Equipped.OffHand);
                if (hasShield != _hasShieldInOffHand)
                {
                    _hasShieldInOffHand = hasShield;
                    Logger.WriteDiagnostic("HasShieldCheck: shieldEquipped={0}", hasShield.ToYN());
                }
                return _hasShieldInOffHand;
            }
        }

        public static bool IsShield(WoWItem hand)
        {
            return hand != null && hand.ItemInfo.ItemClass == WoWItemClass.Armor && hand.ItemInfo.InventoryType == InventoryType.Shield;
        }

        private static Composite CreateShieldCharge(UnitSelectionDelegate onUnit = null, SimpleBooleanDelegate requirements = null)
        {

            if (onUnit == null)
                onUnit = on => Me.CurrentTarget;

            if (requirements == null)
                requirements = req => true;

            return new Sequence(
                new Decorator(
                    req => Spell.DoubleCastContains(Me, "Shield Charge") || !HasShieldInOffHand,
                    new ActionAlwaysFail()
                    ),
                Spell.Cast("Shield Charge", onUnit, req => requirements(req), gcd: HasGcd.No),
                new Action(ret => Spell.UpdateDoubleCast("Shield Charge", Me))
                );
        }


        private static Composite CreateProtectionDiagnosticOutput()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses( 
                1, TimeSpan.FromMilliseconds(1500), RunStatus.Failure,
                new Action(ret =>
                    {
                    string log = string.Format("... [prot] h={0:F1}%/r={1:F1}%, stnc={2}, Ultim={3}, aoe={4}, cc={5}",
                        Me.HealthPercent,
                        Me.CurrentRage,
                        (WarriorStance) Me.Shapeshift,
                        HasUltimatum,
                        scenario.MobCount,
                        scenario.CcCount
                        );

                    WarriorTalents tier3 = Common.GetTierTalent(3);
                    string tier3spell = "";
                    if (tier3 == WarriorTalents.SuddenDeath)
                        tier3spell = "Sudden Death";
                    else if (tier3 == WarriorTalents.UnyieldingStrikes)
                        tier3spell = "Unyielding Strikes";

                    if (tier3 != WarriorTalents.None)
                    {
                        TimeSpan tsbs = Me.GetAuraTimeLeft(tier3spell);
                        log += string.Format(", {0}={1:F0} ms", tier3spell, tsbs.TotalMilliseconds);
                    }

                    WarriorTalents tier6 = Common.GetTierTalent(6);
                    string tier6spell = "";
                    if (tier6 == WarriorTalents.Avatar)
                        tier6spell = "Avatar";
                    else if (tier6 == WarriorTalents.Bloodbath)
                        tier6spell = "Bloodbath";
                    else if (tier6 == WarriorTalents.Bladestorm)
                        tier6spell = "Bladestorm";

                    if (tier6 != WarriorTalents.None)
                    {
                        TimeSpan tsbs = Me.GetAuraTimeLeft(tier6spell);
                        log += string.Format(", {0}={1:F0} ms", tier6spell, tsbs.TotalMilliseconds);
                    }

                    if (!Me.GotTarget())
                        log += ", Targ=(null)";
                    else
                        log += string.Format(", Targ={0} {1:F1}% @ {2:F1} yds, Melee={3}, Facing={4}, LoSS={5}, DeepWounds={6}",
                            Me.CurrentTarget.SafeName(),
                            Me.CurrentTarget.HealthPercent,
                            Me.CurrentTarget.Distance,
                            Me.CurrentTarget.IsWithinMeleeRange,
                            Me.IsSafelyFacing(Me.CurrentTarget),
                            Me.CurrentTarget.InLineOfSpellSight,
                            (long)Me.CurrentTarget.GetAuraTimeLeft("Deep Wounds").TotalMilliseconds
                            );
                    Logger.WriteDebug(Color.AntiqueWhite, log);
                    return RunStatus.Failure;
                    })
                );
        }


        private static Composite CreateGladiatorDiagnosticOutput()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(
                1, TimeSpan.FromMilliseconds(1500), RunStatus.Failure, 
                new Action(ret =>
                {
                    string log = string.Format("... [glad] h={0:F1}%/r={1:F1}%, gstnc={2}, Ultim={3}, SChgStk={4}, SChgBuf={5}, aoe={6}, cc={7}",
                        Me.HealthPercent,
                        Me.CurrentRage,
                        (WarriorStance)Me.Shapeshift,
                        HasUltimatum,
                        Spell.GetCharges("Shield Charge"),
                        (long)Me.GetAuraTimeLeft("Shield Charge").TotalMilliseconds,
                        scenario.MobCount,
                        scenario.CcCount
                        );

                    WarriorTalents tier3 = Common.GetTierTalent(3);
                    string tier3spell = "";
                    if (tier3 == WarriorTalents.SuddenDeath)
                        tier3spell = "Sudden Death";
                    else if (tier3 == WarriorTalents.UnyieldingStrikes)
                        tier3spell = "Unyielding Strikes";

                    if (tier3 != WarriorTalents.None)
                    {
                        TimeSpan tsbs = Me.GetAuraTimeLeft(tier3spell);
                        log += string.Format(", {0}={1:F0} ms", tier3spell, tsbs.TotalMilliseconds);
                    }

                    WarriorTalents tier6 = Common.GetTierTalent(6);
                    string tier6spell = "";
                    if (tier6 == WarriorTalents.Avatar)
                        tier6spell = "Avatar";
                    else if (tier6 == WarriorTalents.Bloodbath)
                        tier6spell = "Bloodbath";
                    else if (tier6 == WarriorTalents.Bladestorm)
                        tier6spell = "Bladestorm";

                    if (tier6 != WarriorTalents.None)
                    {
                        TimeSpan tsbs = Me.GetAuraTimeLeft(tier6spell);
                        log += string.Format(", {0}={1:F0} ms", tier6spell, tsbs.TotalMilliseconds);
                    }

                    if (!Me.GotTarget())
                        log += ", Targ=(null)";
                    else
                        log += string.Format(", Targ={0} {1:F1}% @ {2:F1} yds, Melee={3}, Facing={4}, LoSS={5}, DeepWounds={6}",
                            Me.CurrentTarget.SafeName(),
                            Me.CurrentTarget.HealthPercent,
                            Me.CurrentTarget.Distance,
                            Me.CurrentTarget.IsWithinMeleeRange,
                            Me.IsSafelyFacing(Me.CurrentTarget),
                            Me.CurrentTarget.InLineOfSpellSight,
                            (long) Me.CurrentTarget.GetAuraTimeLeft("Deep Wounds").TotalMilliseconds
                            );
                    Logger.WriteDebug(Color.AntiqueWhite, log);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion

    }
}