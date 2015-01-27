using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.TreeSharp;
using System;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using System.Drawing;
using CommonBehaviors.Actions;

namespace Singular.ClassSpecific.Warrior
{
    public class Fury
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WoWUnit Target { get { return StyxWoW.Me.CurrentTarget; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }
        private static bool HasTalent(WarriorTalents tal) { return TalentManager.IsSelected((int)tal); }

        private static bool BloodbathUp { get { return StyxWoW.Me.GetAuraTimeLeft("Bloodbath").TotalSeconds > 0; } }
        private static bool RecklessnessUp { get { return StyxWoW.Me.GetAuraTimeLeft("Recklessness").TotalSeconds > 0; } }
        private static bool EnrageUp { get { return StyxWoW.Me.GetAuraTimeLeft("Enrage").TotalSeconds > 0; } }
        private static bool BloodsurgeUp { get { return StyxWoW.Me.GetAuraTimeLeft("Bloodsurge").TotalSeconds > 0; } }
        private static bool MeatCleaverUp { get { return StyxWoW.Me.GetAuraTimeLeft("Meat Cleaver").TotalSeconds > 0; } }
        private static bool RagingBlowUp { get { return StyxWoW.Me.GetAuraTimeLeft("Raging Blow").TotalSeconds > 0; } }
        private static uint Rage { get { return StyxWoW.Me.CurrentRage; } }
        private static uint RageMax { get { return StyxWoW.Me.MaxRage; } }
        private static double RagePercent { get { return StyxWoW.Me.RagePercent; } }


        private static double CooldownColossusSmash { get { return Spell.GetSpellCooldown("Colossus Smash").TotalSeconds; } }
        private static double DebuffColossusSmash { get { return Target.GetAuraTimeLeft("Colossus Smash").TotalSeconds; } }
        private static bool DebuffColossusSmashUp { get { return DebuffColossusSmash > 0; } }
        private static double DebuffRend { get { return Target.GetAuraTimeLeft("Rend").TotalSeconds; } }
        private static bool DebuffRendTicking { get { return DebuffRend > 0; } }
        private static CombatScenario scenario { get; set; }


        [Behavior(BehaviorType.Initialize, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CreateFuryInitialize()
        {
            scenario = new CombatScenario(8, 1.5f);
            return null;
        }

        [Behavior(BehaviorType.Rest, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CreateFuryRest()
        {
            return new PrioritySelector(

                Common.CheckIfWeShouldCancelBladestorm(),

                Singular.Helpers.Rest.CreateDefaultRestBehaviour(),

                CheckThatWeaponsAreEquipped()
                );
        }


        #region Normal
        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CreateFuryNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateDiagnosticOutputBehavior("Pull"),

                        //Buff up
                        Spell.BuffSelf(Common.SelectedShoutAsSpellName),

                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Common.CreateChargeBehavior(),

                        Spell.Cast("Bloodthirst")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CreateFuryNormalHeal()
        {
            return new Decorator(
                ret => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && !Unit.IsTrivial(Me.CurrentTarget),
                new PrioritySelector(
                    Common.CreateWarriorEnragedRegeneration(),

                    Common.CreateDieByTheSwordBehavior(),

                    Spell.BuffSelf("Rallying Cry", req => !Me.IsInGroup() && Me.HealthPercent < 50)
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorFury, WoWContext.Normal)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorFury, WoWContext.Battlegrounds)]
        public static Composite CreateFuryNormalCombatBuffs()
        {
            return new Decorator(
                ret => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && !Unit.IsTrivial(Me.CurrentTarget),

                new PrioritySelector(
                    new Decorator(
                        ret => SingularRoutine.CurrentWoWContext == WoWContext.Normal
                            && (Me.CurrentTarget.IsPlayer || 4 <= Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < (u.MeleeDistance() + 1)) || Me.CurrentTarget.TimeToDeath() > 40),
                        new PrioritySelector(
                            Spell.BuffSelfAndWait("Avatar", gcd: HasGcd.No),
                            Spell.BuffSelfAndWait("Bloodbath", gcd: HasGcd.No)
                            )
                        ),

                    Spell.BuffSelfAndWait("Recklessness", ret => (Spell.CanCastHack("Execute") || Common.Tier14FourPieceBonus) && (StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances), gcd: HasGcd.No),

                    new Decorator(
                        ret => Me.CurrentTarget.IsBoss(),
                        new PrioritySelector(
                            Spell.BuffSelfAndWait("Avatar", ret => Me.CurrentTarget.IsBoss(), gcd: HasGcd.No),
                            Spell.BuffSelfAndWait("Bloodbath", ret => Me.CurrentTarget.IsBoss(), gcd: HasGcd.No)
                            )
                        ),

                    Spell.Cast("Storm Bolt"),  // in normal rotation

                    Spell.Cast("Berserker Rage", ret => {
                        if (Me.CurrentTarget.HealthPercent <= 20)
                            return true;
                        if (!Me.ActiveAuras.ContainsKey("Enrage") && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds > 6)
                            return true;
                        return false;
                        }),

                    Spell.BuffSelf(Common.SelectedShoutAsSpellName)

                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorFury, WoWContext.Normal)]
        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorFury, WoWContext.Battlegrounds)]
        public static Composite CreateFuryCombatNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(FaceDuring.Yes),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && !StyxWoW.Me.HasAura("Bladestorm"),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        CreateDiagnosticOutputBehavior("Combat"),

                        // special "in combat" pull logic for mobs not tagged and out of melee range
                        Common.CreateWarriorCombatPullMore(),

                        // Dispel Bubbles
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.IsPlayer && (StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")),
                            new PrioritySelector(
                                Spell.WaitForCast(),
                                Movement.CreateEnsureMovementStoppedBehavior( 30, on => StyxWoW.Me.CurrentTarget, reason:"for shattering throw"),
                                Spell.Cast("Shattering Throw"),
                                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 30f, 25f)
                                )
                            ),

                        //Heroic Leap
                        Common.CreateHeroicLeapCloser(),

                        new Sequence(
                            new Decorator(
                                ret => Common.IsSlowNeeded(Me.CurrentTarget),
                                new PrioritySelector(
                                    Spell.Buff("Piercing Howl", ret => Me.CurrentTarget.SpellDistance().Between(8, 15)),
                                    Spell.Buff("Hamstring")
                                    )
                                ),
                            new Wait(TimeSpan.FromMilliseconds(500), until => !Common.IsSlowNeeded(Me.CurrentTarget), new ActionAlwaysSucceed())
                            ),

                        //Interupts
                        Helpers.Common.CreateInterruptBehavior(),

                        // Heal up in melee
                        Common.CreateVictoryRushBehavior(),

                        Common.CreateExecuteOnSuddenDeath(),

                        // AOE 
                        // -- check melee dist+3 rather than 8 so works for large hitboxes (8 is range of DR and WW)

                        new Decorator(  // Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3,
                            ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() < Common.DistanceWindAndThunder(8)) >= 3,                       
                            new PrioritySelector(
                                Spell.BuffSelf("Bladestorm"),
                                Spell.Cast("Shockwave"),
                                Spell.Cast("Dragon Roar"),

                                // do some AOE prior to learning BT
                                Spell.Cast("Thunder Clap", req => !SpellManager.HasSpell("Whirlwind")),

                                //Bloodthirst on cooldown when not Enraged. Procs Bloodsurge.
                                Spell.Cast("Bloodthirst", ret => !Common.IsEnraged),

                                //Whirlwind as a Rage dump and to build Raging Blow stacks.
                                Spell.Cast("Whirlwind", ret => Me.RagePercent > 80),

                                //Raging Blow with Raging Blow stacks.
                                Spell.Cast("Raging Blow", ret => Me.HasAura("Raging Blow", 1)),

                                //Wild Strike to consume Bloodsurge procs.
                                Spell.Cast("Wild Strike", ret => Common.IsEnraged || StyxWoW.Me.HasAura("Bloodsurge"))
                                )
                            ),

                        // Use the single target rotation!
                        SingleTarget(),

                            // Charge if we can
                        Common.CreateChargeBehavior()
                        )
                    ),

                //Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorFury, WoWContext.Instances)]
        public static Composite CreateFuryCombatInstances()
        {
            if (Me.Level < 100)
                return CreateFuryCombatNormal();

            Generic.SuppressGenericRacialBehavior = true;

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(FaceDuring.Yes),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        new Action(r =>
                        {
                            scenario.Update(Target);
                            return RunStatus.Failure;
                        }),

                        CreateDiagnosticOutputBehavior("Combat"),

                        Helpers.Common.CreateInterruptBehavior(),

                        new Decorator(
                            req => Me.GotTarget(),
                            new PrioritySelector(
                                Common.CreateVictoryRushBehavior(),

                                // # Executed every time the actor is available.
                                // 
                                // actions=charge
                                Common.CreateChargeCloser(),
                                // actions+=/auto_attack
                                //  ... handled by ensure
                                // # This is mostly to prevent cooldowns from being accidentally used during movement.
                                // actions+=/call_action_list,name=movement,if=movement.distance>5
                                // actions+=/berserker_rage,if=buff.enrage.down|(talent.unquenchable_thirst.enabled&buff.raging_blow.down)
                                // actions+=/heroic_leap,if=(raid_event.movement.distance>25&raid_event.movement.in>45)|!raid_event.movement.exists
                                // actions+=/use_item,name=bonemaws_big_toe,if=(talent.bladestorm.enabled&cooldown.bladestorm.remains=0)|buff.bloodbath.up|talent.avatar.enabled
                                // actions+=/use_item,name=turbulent_emblem,if=(talent.bladestorm.enabled&cooldown.bladestorm.remains=0)|buff.bloodbath.up|talent.avatar.enabled
                                // actions+=/potion,name=draenic_strength,if=(target.health.pct<20&buff.recklessness.up)|target.time_to_die<=25
                                // # Skip cooldown usage if we can line them up with bladestorm on a large set of adds, or if movement is coming soon.
                                // actions+=/call_action_list,name=single_target,if=(raid_event.adds.cooldown<60&raid_event.adds.count>2&active_enemies=1)|raid_event.movement.cooldown<5

                                new Decorator( 
                                    req => Me.IsMoving && !Target.IsWithinMeleeRange,
                                    new PrioritySelector(
                                        // actions.movement=heroic_leap
                                        Common.CreateHeroicLeapCloser(),
                                        // # May as well throw storm bolt if we can.
                                        // actions.movement+=/storm_bolt
                                        Spell.Cast("Storm Bolt"),
                                        // actions.movement+=/heroic_throw
                                        Spell.Cast("Heroic Throw"),
                                        new ActionAlwaysSucceed()
                                        )
                                    ),

                                // # This incredibly long line (Due to differing talent choices) says 'Use recklessness on cooldown, unless the boss will die before the ability is usable again, and then use it with execute.'
                                // actions+=/recklessness,if=((target.time_to_die>190|target.health.pct<20)&(buff.bloodbath.up|!talent.bloodbath.enabled))|target.time_to_die<=12|talent.anger_management.enabled
                                Spell.BuffSelfAndWait(
                                    "Recklessness", 
                                    req => ((Target.TimeToDeath() > 190 || Target.HealthPercent < 20) && (BloodbathUp || !Common.HasTalent(WarriorTalents.Bloodbath)))
                                        || Target.TimeToDeath() < 10
                                        || Common.HasTalent(WarriorTalents.AngerManagement),
                                        gcd: HasGcd.No
                                    ),
                                // actions+=/avatar,if=buff.recklessness.up|target.time_to_die<30
                                Spell.BuffSelfAndWait(
                                    "Avatar",
                                    req => Me.HasAura("Recklessness")
                                        || Target.TimeToDeath() < 25,
                                        gcd: HasGcd.No
                                    ),
                                // actions+=/blood_fury,if=buff.bloodbath.up|!talent.bloodbath.enabled|buff.recklessness.up
                                Spell.BuffSelfAndWait(
                                    "Blood Fury",
                                    req => BloodbathUp
                                        || !Common.HasTalent(WarriorTalents.Bloodbath)
                                        || RecklessnessUp,
                                        gcd: HasGcd.No
                                    ),
                                // actions+=/berserking,if=buff.bloodbath.up|!talent.bloodbath.enabled|buff.recklessness.up
                                Spell.BuffSelfAndWait(
                                    "Berserking",
                                    req => BloodbathUp
                                        || !Common.HasTalent(WarriorTalents.Bloodbath)
                                        || RecklessnessUp,
                                        gcd: HasGcd.No
                                    ),

                                // actions+=/arcane_torrent,if=rage<rage.max-40
                                Spell.BuffSelfAndWait(
                                    "Arcane Torrent",
                                    req => Me.CurrentRage < Me.MaxRage - 40,
                                    gcd: HasGcd.No
                                    ),

                                new Decorator(
                                    req => scenario.MobCount <= 1,
                                    new PrioritySelector(
                                        // 
                                        // actions.single_target=bloodbath
                                        Spell.BuffSelfAndWait("Bloodbath", gcd:HasGcd.No),
                                        // actions.single_target+=/recklessness,if=target.health.pct<20&raid_event.adds.exists
                                        Spell.BuffSelfAndWait("Recklessness", req => Target.HealthPercent < 20 && scenario.MobCount > 1, gcd:HasGcd.No),
                                        // actions.single_target+=/wild_strike,if=rage>110&target.health.pct>20
                                        Spell.Cast("Wild Strike", req => RagePercent > 91 && Target.HealthPercent > 20),
                                        // actions.single_target+=/bloodthirst,if=(!talent.unquenchable_thirst.enabled&rage<80)|buff.enrage.down
                                        Spell.Cast("Bloodthirst", req => Common.HasTalent(WarriorTalents.UnquenchableThirst) && Rage < 80 || !EnrageUp),
                                        // actions.single_target+=/ravager,if=buff.bloodbath.up|(!talent.bloodbath.enabled&(!raid_event.adds.exists|raid_event.adds.cooldown>60|target.time_to_die<40))
                                        Spell.CastOnGround(
                                            "Ravager", 
                                            on => Target, 
                                            req => Spell.UseAOE && (BloodbathUp || !Common.HasTalent(WarriorTalents.Bloodbath) )
                                            ),
                                        // actions.single_target+=/execute,if=buff.sudden_death.react
                                        Common.CreateExecuteOnSuddenDeath(),
                                        // actions.single_target+=/siegebreaker
                                        Spell.Cast("Siegebreaker"),
                                        // actions.single_target+=/storm_bolt
                                        Spell.Cast("Storm Bolt"),
                                        // actions.single_target+=/wild_strike,if=buff.bloodsurge.up
                                        Spell.Cast("Wild Strike", req => BloodsurgeUp),
                                        // actions.single_target+=/execute,if=buff.enrage.up|target.time_to_die<12
                                        Spell.Cast("Execute", req => EnrageUp || Target.TimeToDeath() < 12),
                                        // actions.single_target+=/dragon_roar,if=buff.bloodbath.up|!talent.bloodbath.enabled
                                        Spell.Cast("Dragon Roar", req => BloodbathUp || !Common.HasTalent(WarriorTalents.Bloodbath)),
                                        // actions.single_target+=/raging_blow
                                        Spell.Cast("Raging Blow"),
                                        // actions.single_target+=/wild_strike,if=buff.enrage.up&target.health.pct>20
                                        Spell.Cast("Wild Strike", req => EnrageUp && Target.HealthPercent > 20),
                                        // actions.single_target+=/bladestorm,if=!raid_event.adds.exists
                                        Spell.Cast("Bladestorm"),
                                        // actions.single_target+=/shockwave,if=!talent.unquenchable_thirst.enabled
                                        Spell.Cast("Shockwave", req => !Common.HasTalent(WarriorTalents.UnquenchableThirst)),
                                        // actions.single_target+=/impending_victory,if=!talent.unquenchable_thirst.enabled&target.health.pct>20
                                        Spell.Cast("Impending Victory", req => !Common.HasTalent(WarriorTalents.UnquenchableThirst) && Target.HealthPercent > 20),
                                        // actions.single_target+=/bloodthirst
                                        Spell.Cast("Bloodthirst")
                                        )
                                    ),

                                new Decorator(
                                    req => scenario.MobCount == 2,
                                    new PrioritySelector(
                                        // actions.two_targets=bloodbath
                                        Spell.BuffSelfAndWait("Bloodbath", gcd: HasGcd.No),
                                        // actions.two_targets+=/ravager,if=buff.bloodbath.up|!talent.bloodbath.enabled
                                            Spell.CastOnGround(
                                                "Ravager", 
                                                on => Target, 
                                                req => BloodbathUp || !Common.HasTalent(WarriorTalents.Bloodbath) 
                                                ),
                                        // actions.two_targets+=/dragon_roar,if=buff.bloodbath.up|!talent.bloodbath.enabled
                                        Spell.Cast(
                                            "Dragon Roar",
                                            on => Target, 
                                            req => BloodbathUp || !Common.HasTalent(WarriorTalents.Bloodbath) 
                                            ),
                                        // actions.two_targets+=/bladestorm,if=buff.enrage.up
                                        Spell.Cast("Bladestorm", req => EnrageUp),
                                        // actions.two_targets+=/bloodthirst,if=buff.enrage.down|rage<50|buff.raging_blow.down
                                        Spell.Cast("Bloodthirst", req => !EnrageUp || Rage < 50 || !RagingBlowUp),
                                        // actions.two_targets+=/execute,target=2
                                        // ... combined with next
                                        // actions.two_targets+=/execute,if=target.health.pct<20|buff.sudden_death.react
                                        Spell.Cast("Execute", on => scenario.Mobs.FirstOrDefault(u => u != Target && Spell.CanCastHack("Execute", u) && u.InLineOfSight && Me.IsSafelyFacing(u))),
                                        // actions.two_targets+=/raging_blow,if=buff.meat_cleaver.up
                                        Spell.Cast("Raging Blow", req => MeatCleaverUp ),
                                        // actions.two_targets+=/whirlwind,if=!buff.meat_cleaver.up
                                        Spell.Cast("Whirlwind", req => !MeatCleaverUp),
                                        // actions.two_targets+=/wild_strike,if=buff.bloodsurge.up&rage>75
                                        Spell.Cast("Wild Strike", req => BloodsurgeUp && Rage > 75),
                                        // actions.two_targets+=/bloodthirst
                                        Spell.Cast("Bloodthirst"),
                                        // actions.two_targets+=/whirlwind,if=rage>rage.max-20
                                        Spell.Cast("Whirlwind", req => Rage > RageMax - 20),
                                        // actions.two_targets+=/wild_strike,if=buff.bloodsurge.up
                                        Spell.Cast("Wild Strike", req => BloodsurgeUp)
                                        )
                                    ),

                                new Decorator(
                                    req => scenario.MobCount == 3,
                                    new PrioritySelector(
                                        // actions.three_targets=bloodbath
                                        Spell.BuffSelfAndWait("Bloodbath", gcd: HasGcd.No),
                                        // actions.three_targets+=/ravager,if=buff.bloodbath.up|!talent.bloodbath.enabled
                                        Spell.CastOnGround(
                                            "Ravager", 
                                            on => Target, 
                                            req => BloodbathUp || !Common.HasTalent(WarriorTalents.Bloodbath) 
                                            ),
                                        // actions.three_targets+=/bladestorm,if=buff.enrage.up
                                        Spell.Cast("Bladestorm", req => EnrageUp),
                                        // actions.three_targets+=/bloodthirst,if=buff.enrage.down|rage<50|buff.raging_blow.down
                                        Spell.Cast("Bloodthirst", req => !EnrageUp || Rage < 50 || !RagingBlowUp ),
                                        // actions.three_targets+=/raging_blow,if=buff.meat_cleaver.stack>=2
                                        Spell.Cast("Raging Blow", req => Me.GetAuraStacks("Meat Cleaver") >= 2),
                                        // actions.three_targets+=/execute,if=buff.sudden_death.react
                                        Common.CreateExecuteOnSuddenDeath(),
                                        // actions.three_targets+=/execute,target=2
                                        // ... combined with next
                                        // actions.three_targets+=/execute,target=3
                                        Spell.Cast("Execute", on => scenario.Mobs.FirstOrDefault(u => u != Target && Spell.CanCastHack("Execute", u) && u.InLineOfSight && Me.IsSafelyFacing(u))),
                                        // actions.three_targets+=/dragon_roar,if=buff.bloodbath.up|!talent.bloodbath.enabled
                                        Spell.Cast(
                                            "Dragon Roar",
                                            on => Target, 
                                            req => BloodbathUp || !Common.HasTalent(WarriorTalents.Bloodbath) 
                                            ),
                                        // actions.three_targets+=/whirlwind
                                        Spell.Cast("Whirlwind"),
                                        // actions.three_targets+=/bloodthirst
                                        Spell.Cast("Bloodthirst"),
                                        // actions.three_targets+=/wild_strike,if=buff.bloodsurge.up
                                        Spell.Cast("Wild Strike", req => BloodsurgeUp)
                                        )
                                    ),

                                new Decorator(
                                    req => scenario.MobCount > 3,
                                    new PrioritySelector(
                                        // actions.aoe=bloodbath
                                        Spell.BuffSelfAndWait("Bloodbath", gcd: HasGcd.No),
                                        // actions.aoe+=/ravager,if=buff.bloodbath.up|!talent.bloodbath.enabled
                                        Spell.CastOnGround(
                                            "Ravager", 
                                            on => Target, 
                                            req => BloodbathUp || !Common.HasTalent(WarriorTalents.Bloodbath) 
                                            ),
                                        // actions.aoe+=/raging_blow,if=buff.meat_cleaver.stack>=3&buff.enrage.up
                                        Spell.Cast("Raging Blow", req => Me.GetAuraStacks("Meat Cleaver") >= 3 && EnrageUp),
                                        // actions.aoe+=/bloodthirst,if=buff.enrage.down|rage<50|buff.raging_blow.down
                                        Spell.Cast("Bloodthirst", req => !EnrageUp || Rage < 50 || !RagingBlowUp),
                                        // actions.aoe+=/raging_blow,if=buff.meat_cleaver.stack>=3
                                        Spell.Cast("Raging Blow", req => Me.GetAuraStacks("Meat Cleaver") >= 3),
                                        // actions.aoe+=/recklessness,sync=bladestorm
                                        Spell.Cast("Recklessness"),
                                        // actions.aoe+=/bladestorm,if=buff.enrage.remains>6
                                        Spell.Cast("Bladestorm", req => Me.GetAuraTimeLeft("Enrage").TotalSeconds > 6),
                                        // actions.aoe+=/whirlwind
                                        Spell.Cast("Whirlwind"),
                                        // actions.aoe+=/execute,if=buff.sudden_death.react
                                        Common.CreateExecuteOnSuddenDeath(),
                                        // actions.aoe+=/dragon_roar,if=buff.bloodbath.up|!talent.bloodbath.enabled
                                        Spell.Cast("Dragon Roar", req => BloodbathUp || !Common.HasTalent(WarriorTalents.Bloodbath)),
                                        // actions.aoe+=/bloodthirst
                                        Spell.Cast("Bloodthirst"),
                                        // actions.aoe+=/wild_strike,if=buff.bloodsurge.up
                                        Spell.Cast("Wild Strike", req => BloodsurgeUp)
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        private static Composite SingleTarget()
        {
            return new PrioritySelector(

                new Decorator(
                    req => Me.CurrentTarget.HealthPercent > 20,
                    new PrioritySelector(

                        //Wild Strike to prevent capping your Rage.
                        Spell.Cast("Wild Strike", ret => Me.RagePercent > 80),

                        //Bloodthirst on cooldown when not Enraged. Procs Bloodsurge.
                        Spell.Cast("Bloodthirst", ret => !Common.IsEnraged),

                        //Raging Blow when available.
                        Spell.Cast("Raging Blow"),

                        //Wild Strike when Enraged or with Bloodsurge procs.
                        Spell.Cast("Wild Strike", ret => Common.IsEnraged || StyxWoW.Me.HasAura("Bloodsurge"))
                        )
                    ),

                new Decorator(
                    req => Me.CurrentTarget.HealthPercent <= 20,
                    new PrioritySelector(

                        //Execute to prevent capping your Rage.
                        Spell.Cast("Execute", req => Me.RagePercent > 80),

                        //Bloodthirst on cooldown when not Enraged. Procs Bloodsurge.
                        Spell.Cast("Bloodthirst", ret => !Common.IsEnraged),

                        //Execute while Enraged or with >= 60 Rage.
                        Spell.Cast("Execute", req => Me.RagePercent >= 60),

                        //Wild Strike when Enraged or with Bloodsurge procs.
                        Spell.Cast("Wild Strike", ret => Common.IsEnraged || StyxWoW.Me.HasAura("Bloodsurge"))
                        )
                    ),

                new Decorator(
                    req => !SpellManager.HasSpell("Whirlwind"),
                    new PrioritySelector(
                        Spell.Cast("Execute"),
                        Spell.Cast("Bloodthirst"),
                        Spell.Cast("Wild Strike"),
                        Spell.Cast("Thunder Clap", req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8))
                        )
                    ),

                // BR whenever we're not enraged, and can actually melee the target.
                // Use abilities that cost no rage, such as your tier 4 talents, etc
                new Decorator(
                    ret => Spell.UseAOE && Me.GotTarget() && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss()) && Me.CurrentTarget.Distance < 8,
                    new PrioritySelector(
                        Spell.BuffSelf("Bladestorm"),
                        Spell.Cast("Shockwave")
                        )
                    ),

                Spell.Cast("Dragon Roar", req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8),
                Spell.Cast("Storm Bolt"),

                Spell.BuffSelf("Berserker Rage", ret => !Common.IsEnraged && StyxWoW.Me.CurrentTarget.IsWithinMeleeRange)
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
//                    if (myRage >= 30 && StyxWoW.Me.HasAura("Deadly Calm"))
//                        return true;
                }
                return false;
            }
        }

        #endregion

        private static Composite _checkWeapons = null;

        public static Composite CheckThatWeaponsAreEquipped()
        {
            if (_checkWeapons == null)
            {
                _checkWeapons = new ThrottlePasses(60,
                    new Sequence(
                        new DecoratorContinue(
                            ret => !Me.Disarmed && !IsWeapon(Me.Inventory.Equipped.MainHand),
                            new Action(ret => Logger.Write(Color.HotPink, "User Error: a{0} requires a Main Hand Weapon equipped to be effective", SingularRoutine.SpecAndClassName()))
                            ),
                        new DecoratorContinue(
                            ret => !Me.Disarmed && !IsWeapon(Me.Inventory.Equipped.OffHand) && SpellManager.HasSpell("Wild Strike"),
                            new Action(ret => Logger.Write(Color.HotPink, "User Error: a{0} requires an Off Hand Weapon to cast Wild Strike", SingularRoutine.SpecAndClassName()))
                            ),
                        new ActionAlwaysFail()
                        )
                    );
            }
            return _checkWeapons;
        }

        public static bool IsWeapon(WoWItem hand)
        {
            return hand != null && hand.ItemInfo.ItemClass == WoWItemClass.Weapon;
        }

        #region Diagnostics 

        private static Composite CreateDiagnosticOutputBehavior(string context = null)
        {
            if (context == null)
                context = "...";
            else
                context = "<<" + context + ">>";

            return new Decorator(
                ret => SingularSettings.Debug,
                new ThrottlePasses(1,
                    new Action(ret =>
                    {
                        string log;
                        log = string.Format(context + " h={0:F1}%/r={1:F1}%, stance={2}, Enrage={3} Coloss={4} MortStrk={5}, RagingBlow={6}",
                            Me.HealthPercent,
                            Me.CurrentRage,
                            Me.Shapeshift,
                            Me.ActiveAuras.ContainsKey("Enrage"),
                            (int)Spell.GetSpellCooldown("Colossus Smash", -1).TotalMilliseconds,
                            (int)Spell.GetSpellCooldown("Mortal Strike", -1).TotalMilliseconds,
                            Me.GetAuraStacks("Raging Blow")
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            log += string.Format(", th={0:F1}%, dist={1:F1}, inmelee={2}, face={3}, loss={4}, dead={5} secs",
                                target.HealthPercent,
                                target.Distance,
                                target.IsWithinMeleeRange.ToYN(),
                                Me.IsSafelyFacing(target).ToYN(),
                                target.InLineOfSpellSight.ToYN(),
                                target.TimeToDeath()
                                );
                        }

                        Logger.WriteDebug(Color.AntiqueWhite, log);
                        return RunStatus.Failure;
                    })
                    )
                );
        }

#endregion
    }
}
