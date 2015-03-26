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
using Styx.Pathing;

namespace Singular.ClassSpecific.Warrior
{
    /// <summary>
    /// plaguerized from Apoc's simple Arms Warrior CC 
    /// see http://www.thebuddyforum.com/honorbuddy-forum/combat-routines/warrior/79699-arms-armed-quick-dirty-simple-fast.html#post815973
    /// </summary>
    public class Arms
    {

        #region Common

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WoWUnit Target { get { return StyxWoW.Me.CurrentTarget; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }
        private static bool HasTalent(WarriorTalents tal) { return TalentManager.IsSelected((int)tal); }
        private static double CooldownMortalStrike { get { return Spell.GetSpellCooldown("Mortal Strike").TotalSeconds; } }
        private static double CooldownColossusSmash { get { return Spell.GetSpellCooldown("Colossus Smash").TotalSeconds; } }
        private static double DebuffColossusSmash { get { return Target.GetAuraTimeLeft("Colossus Smash").TotalSeconds; } }
        private static bool DebuffColossusSmashUp { get { return DebuffColossusSmash > 0; } }
        private static double DebuffRend { get { return Target.GetAuraTimeLeft("Rend").TotalSeconds; } }
        private static bool DebuffRendTicking { get { return DebuffRend > 0;  } }
        private static CombatScenario scenario { get; set; }

        [Behavior(BehaviorType.Initialize, WoWClass.Warrior, WoWSpec.WarriorArms)]
        public static Composite CreateArmsInitialize()
        {
            scenario = new CombatScenario(8, 1.5f);
            Logger.WriteDiagnostic("CreateArmsInitialize: Arms init complete");
            return null;
        }

        [Behavior(BehaviorType.Rest, WoWClass.Warrior, WoWSpec.WarriorArms)]
        public static Composite CreateArmsRest()
        {
            return new PrioritySelector(

                Common.CheckIfWeShouldCancelBladestorm(),

                Singular.Helpers.Rest.CreateDefaultRestBehaviour(),

                CheckThatWeaponIsEquipped()
                );
        }


        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.All)]
        public static Composite CreateArmsNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateDiagnosticOutputBehavior("Pull"),

                        new Throttle( 2, Spell.BuffSelf(Common.SelectedShoutAsSpellName)),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Common.CreateChargeBehavior(),

                        Spell.Cast("Mortal Strike")
                        )
                    )
                );
        }

        #endregion

        #region Normal

        [Behavior(BehaviorType.Heal, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Normal)]
        [Behavior(BehaviorType.Heal, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Instances)]
        public static Composite CreateArmsCombatHeal()
        {
            return new Throttle(
                new Decorator(
                    ret => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && !Unit.IsTrivial(Me.CurrentTarget),

                    new PrioritySelector(
                        Common.CreateWarriorEnragedRegeneration(),

                        Common.CreateDieByTheSwordBehavior(),

                        Spell.HandleOffGCD( Spell.BuffSelf("Rallying Cry", req => !Me.IsInGroup() && Me.HealthPercent < 50, 0, HasGcd.No))
                        )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Normal)]
        public static Composite CreateArmsCombatBuffsNormal()
        {
            return new Throttle(
                new Decorator(
                    ret => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && !Unit.IsTrivial(Me.CurrentTarget),

                    new PrioritySelector(

                        new Decorator(
                            ret =>
                            {
                                if (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss())
                                    return true;

                                if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                                    return false;

                                if (Me.CurrentTarget.TimeToDeath() > 40)
                                    return true;

                                return Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= 4;
                            },
                            new PrioritySelector(
                                Spell.HandleOffGCD(Spell.BuffSelf("Avatar", req => true, 0, HasGcd.No)),
                                Spell.HandleOffGCD(Spell.BuffSelf("Bloodbath", req => true, 0, HasGcd.No))
                                )
                            ),

                        Spell.HandleOffGCD(Spell.BuffSelf("Recklessness", ret => (Spell.CanCastHack("Execute") || Common.Tier14FourPieceBonus || PartyBuff.WeHaveBloodlust) && (StyxWoW.Me.CurrentTarget.TimeToDeath() > 40 || StyxWoW.Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances), 0, HasGcd.No)),

                        Spell.Cast("Storm Bolt"),  // in normal rotation

                        // Execute is up, so don't care just cast
                        Spell.HandleOffGCD(
                            Spell.BuffSelf(
                                "Berserker Rage", 
                                req => 
                                {
                                    if (Me.CurrentTarget.HealthPercent <= 20)
                                        return true;
                                    if (!Common.IsEnraged && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds > 6)
                                        return true;
                                    return false;
                                },
                                0,
                                HasGcd.No
                                )
                            ),


                        Spell.BuffSelf(Common.SelectedShoutAsSpellName)

                        )
                    )
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Normal)]
        public static Composite CreateArmsCombatNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        CreateDiagnosticOutputBehavior("Combat"),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateVictoryRushBehavior(),

                        // special "in combat" pull logic for mobs not tagged and out of melee range
                        Common.CreateWarriorCombatPullMore(),

                        Common.CreateExecuteOnSuddenDeath(),

                        new Throttle(
                            new Decorator(
                                ret => Me.HasAura("Glyph of Cleave"),
                                Spell.Cast("Heroic Strike")
                                )
                            ),

                        new Sequence(
                            new Decorator(
                                req => Common.IsSlowNeeded(Me.CurrentTarget),
                                new PrioritySelector(
                                    Spell.Buff("Hamstring")
                                    )
                                ),
                            new Wait(TimeSpan.FromMilliseconds(500), until => !Common.IsSlowNeeded(Me.CurrentTarget), new ActionAlwaysSucceed())
                            ),

                        CreateArmsAoeCombat(ret => Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() < (Common.DistanceWindAndThunder(8)))),

                        // Noxxic
                //----------------
                        new Decorator(
                            ret => Me.GotTarget(), // WarriorSettings.ArmsSpellPriority == WarriorSettings.SpellPriority.Noxxic,
                            new PrioritySelector(

                                new Decorator(
                                    ret => Spell.UseAOE && Me.GotTarget() && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss()) && Me.CurrentTarget.SpellDistance() < 8,
                                    new PrioritySelector(
                                        Spell.Cast("Storm Bolt"),
                                        Spell.BuffSelf("Bladestorm"),
                                        Spell.Cast("Shockwave"),
                                        Spell.Cast("Dragon Roar")
                                        )
                                    ),

                                new Decorator(
                                    req => !Me.CurrentTarget.HasAura("Colossus Smash"),
                                    new PrioritySelector(
                // 1 Rend maintained at all times. Refresh with < 5 sec remaining.
                                        Spell.Buff("Rend", req => Me.CurrentTarget.HasAuraExpired("Rend", 4)),

                                        // 2 Execute with >= 60 Rage and target is below 20% health.
                                        Spell.Cast("Execute", req => Me.CurrentRage > 60 && Me.CurrentTarget.HealthPercent <= 20),

                                        // 3 Mortal Strike on cooldown when target is above 20% health.
                                        Spell.Cast("Mortal Strike", req => Me.CurrentTarget.HealthPercent > 20),

                                        // 4 Colossus Smash as often as possible.
                                        Spell.Cast("Colossus Smash"),

                                        // 5 Whirlwind as a filler ability when target is above 20% health.
                                        Spell.Cast("Whirlwind", req => Me.CurrentTarget.HealthPercent > 20 && Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8)),

                                        // Done here
                                        new ActionAlwaysFail()
                                        )
                                    ),

                                new Decorator(
                                    req => Me.CurrentTarget.HasAura("Colossus Smash"),
                                    new PrioritySelector(
                // 1 Execute on cooldown when target is below 20% health.
                                        Spell.Cast("Execute", req => Me.CurrentTarget.HealthPercent <= 20),

                                        // 2 Mortal Strike on cooldown when target is above 20% health.
                                        Spell.Cast("Mortal Strike", req => Me.HealthPercent > 20),

                                        // 3 Whirlwind as a filler ability when target is above 20% health.
                                        Spell.Cast("Whirlwind", req => Me.CurrentTarget.HealthPercent > 20 && Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8)),

                                        // Done here
                                        new ActionAlwaysFail()
                                        )
                                    ),

                                Spell.Cast("Dragon Roar", req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8),
                                Spell.Cast("Storm Bolt"),

                                // if we are low-level with low rage regen, do any damage we can
                                new Decorator(
                                    req => !SpellManager.HasSpell("Whirlwind"),
                                    new PrioritySelector(
                                        Spell.Cast("Rend"),
                                        Spell.Cast("Thunder Clap", req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8)
                                        )
                                    )
                                )
                            ),

                        Common.CreateChargeBehavior()
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Instances )]
        public static Composite CreateArmsCombatInstances()
        {
            if (Me.Level < 100)
                return CreateArmsCombatNormal();

            Generic.SuppressGenericRacialBehavior = true;

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        new Action( r => 
                        { 
                            scenario.Update(Target);
                            return RunStatus.Failure;
                        }),

                        CreateDiagnosticOutputBehavior("Combat"),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Helpers.Common.CreateInterruptBehavior(),

                        new Decorator(
                            req => Me.GotTarget(),
                            new PrioritySelector(
                                Common.CreateVictoryRushBehavior(),

                                // # Executed every time the actor is available.
                                // 
                                // actions=charge
                                // actions+=/auto_attack
                                // # This is mostly to prevent cooldowns from being accidentally used during movement.
                                // actions+=/call_action_list,name=movement,if=movement.distance>5
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

                                // actions+=/use_item,name=bonemaws_big_toe,if=(buff.bloodbath.up|(!talent.bloodbath.enabled&debuff.colossus_smash.up))
                                // actions+=/use_item,name=turbulent_emblem,if=(buff.bloodbath.up|(!talent.bloodbath.enabled&debuff.colossus_smash.up))
                                // actions+=/potion,name=draenic_strength,if=(target.health.pct<20&buff.recklessness.up)|target.time_to_die<25
                                // ... potion and trinket usage not implemented

                                // # This incredibly long line (Due to differing talent choices) says 
                                //  'Use recklessness on cooldown with colossus smash, unless the boss will die before the ability is usable again, and then use it with execute.'
                                // actions+=/recklessness,if=(dot.rend.ticking&(target.time_to_die>190|target.health.pct<20)&(!talent.bloodbath.enabled&(cooldown.colossus_smash.remains<2|debuff.colossus_smash.remains>=5)|buff.bloodbath.up))|target.time_to_die<10
                                Spell.BuffSelfAndWait(
                                    "Recklessness", 
                                    req => (DebuffRendTicking
                                        && (Target.TimeToDeath() > 190 || Target.HealthPercent < 20) 
                                        && (!Common.HasTalent(WarriorTalents.Bloodbath) && (CooldownColossusSmash < 2 || DebuffColossusSmash >=5)) || Me.HasAura("Bloodbath"))
                                        || Target.TimeToDeath() < 10,
                                    gcd: HasGcd.No
                                    ),
                                // actions+=/bloodbath,if=(dot.rend.ticking&cooldown.colossus_smash.remains<5)|target.time_to_die<20
                                Spell.BuffSelfAndWait(
                                    "Bloodbath", 
                                    req => DebuffRendTicking && CooldownColossusSmash < 5
                                        || Target.TimeToDeath() < 20,
                                    gcd: HasGcd.No
                                    ),
                                // actions+=/avatar,if=buff.recklessness.up|target.time_to_die<25
                                Spell.BuffSelfAndWait(
                                    "Avatar",
                                    req => Me.HasAura("Recklessness") 
                                        || Target.TimeToDeath() < 25
                                    ),
                                // actions+=/blood_fury,if=buff.bloodbath.up|(!talent.bloodbath.enabled&debuff.colossus_smash.up)|buff.recklessness.up
                                Spell.BuffSelfAndWait(
                                    "Blood Fury",
                                    req => Me.HasAura("Bloodbath") 
                                        || !Common.HasTalent(WarriorTalents.Bloodbath) && DebuffColossusSmashUp
                                        || Me.HasAura("Recklessness"),
                                    gcd: HasGcd.No
                                    ),
                                // actions+=/berserking,if=buff.bloodbath.up|(!talent.bloodbath.enabled&debuff.colossus_smash.up)|buff.recklessness.up
                                Spell.BuffSelfAndWait(
                                    "Berserking",
                                    req => Me.HasAura("Bloodbath") 
                                        || !Common.HasTalent(WarriorTalents.Bloodbath) && DebuffColossusSmashUp
                                        || Me.HasAura("Recklessness"),
                                    gcd: HasGcd.No
                                    ),
                                // actions+=/arcane_torrent,if=rage<rage.max-40
                                Spell.BuffSelfAndWait(
                                    "Arcane Torrent",
                                    req => Me.CurrentRage < Me.MaxRage - 40,
                                    gcd: HasGcd.No
                                    ),
                                // actions+=/heroic_leap,if=(raid_event.movement.distance>25&raid_event.movement.in>45)|!raid_event.movement.exists
                                Common.CreateHeroicLeapCloser(),

                                // actions+=/call_action_list,name=single,if=active_enemies=1
                                // actions+=/call_action_list,name=aoe,if=active_enemies>1
                        
                                new Decorator(
                                    req => scenario.MobCount <= 1, /* single fight */
                                    new PrioritySelector(
                                        // actions.single=rend,if=!ticking&target.time_to_die>4
                                        Spell.Cast("Rend", req => !DebuffRendTicking && Target.TimeToDeath() > 4 ),
                                        // actions.single+=/ravager,if=cooldown.colossus_smash.remains<4
                                        Spell.CastOnGround("Ravager", on => Target, req => Spell.UseAOE && Target.Distance < 40 && CooldownColossusSmash < 4),
                                        // actions.single+=/colossus_smash
                                        Spell.Cast("Colossus Smash"),
                                        // actions.single+=/bladestorm,if=!raid_event.adds.exists&debuff.colossus_smash.up&rage<70
                                        Spell.Cast("Bladestorm", req => Spell.UseAOE && DebuffColossusSmashUp && Me.CurrentRage < 70),
                                        // actions.single+=/mortal_strike,if=target.health.pct>20&cooldown.colossus_smash.remains>1
                                        Spell.Cast("Mortal Strike", req => Target.HealthPercent > 20 && CooldownColossusSmash > 1),
                                        // actions.single+=/storm_bolt,if=(cooldown.colossus_smash.remains>4|debuff.colossus_smash.up)&rage<90
                                        Spell.Cast("Storm Bolt", req => (CooldownColossusSmash > 4 || DebuffColossusSmashUp) && Me.CurrentRage < 90),
                                        // actions.single+=/siegebreaker
                                        Spell.Cast("Siegebreaker"),
                                        // actions.single+=/dragon_roar,if=!debuff.colossus_smash.up
                                        Spell.Cast("Dragon Roar", req => Spell.UseAOE && !DebuffColossusSmashUp),
                                        // actions.single+=/rend,if=!debuff.colossus_smash.up&target.time_to_die>4&remains<5.4
                                        Spell.Cast("Rend", req => !DebuffColossusSmashUp && Target.TimeToDeath() > 4 && DebuffRend < 5.4),
                                        // actions.single+=/execute,if=(rage>=60&cooldown.colossus_smash.remains>execute_time)|debuff.colossus_smash.up|buff.sudden_death.react|target.time_to_die<5
                                        Spell.Cast(
                                            "Execute", 
                                            req => (Me.CurrentRage >= 60 && CooldownColossusSmash > scenario.GcdTime)
                                                || DebuffColossusSmashUp
                                                || Me.HasAura(Common.SUDDEN_DEATH_PROC)
                                                || Target.TimeToDeath() < 5
                                            ),
                                        // actions.single+=/impending_victory,if=rage<40&target.health.pct>20&cooldown.colossus_smash.remains>1&cooldown.mortal_strike.remains>1
                                        Spell.Cast(
                                            "Impending Victory",
                                            req => Me.CurrentRage < 40 
                                                && Target.HealthPercent > 20
                                                && CooldownColossusSmash > 1
                                                && CooldownMortalStrike > 1
                                            ),
                                        // actions.single+=/slam,if=(rage>20|cooldown.colossus_smash.remains>execute_time)&target.health.pct>20&cooldown.colossus_smash.remains>1&cooldown.mortal_strike.remains>1
                                        Spell.Cast(
                                            "Slam",
                                            req => (Me.CurrentRage > 20 || CooldownColossusSmash > scenario.GcdTime)
                                                && Target.HealthPercent > 20
                                                && CooldownColossusSmash > 1
                                                && CooldownMortalStrike > 1
                                            ),
                                        // actions.single+=/whirlwind,if=!talent.slam.enabled&target.health.pct>20&(rage>=40|set_bonus.tier17_4pc|debuff.colossus_smash.up)&cooldown.colossus_smash.remains>1&cooldown.mortal_strike.remains>1
                                        Spell.Cast(
                                            "Whirlwind",
                                            req => Spell.UseAOE 
                                                && !Common.HasTalent(WarriorTalents.Slam)
                                                && Target.HealthPercent > 20 
                                                && (Me.CurrentRage >= 40 || false /*4pcbonus*/ || DebuffColossusSmashUp)
                                                && CooldownColossusSmash > 1
                                                && CooldownMortalStrike > 1
                                            ),
                                        // actions.single+=/shockwave
                                        Spell.Cast("Shockwave", req => Spell.UseAOE )
                                        )
                                    ),

                                new Decorator(
                                    req => scenario.MobCount > 1, /* aoe fight */
                                    new PrioritySelector(
                                        // actions.aoe=sweeping_strikes
                                        Spell.BuffSelfAndWait("Sweeping Strikes", gcd: HasGcd.No),
                                        // actions.aoe+=/rend,if=ticks_remain<2&target.time_to_die>4
                                        Spell.Buff("Rend", req => DebuffRend < 6 && Target.TimeToDeath() > 4),
                                        // actions.aoe+=/ravager,if=buff.bloodbath.up|!talent.bloodbath.enabled
                                        Spell.CastOnGround("Ravager", on => Target, req => Spell.UseAOE && (Me.HasAura("Bloodbath") || !Common.HasTalent(WarriorTalents.Bloodbath))),
                                        // actions.aoe+=/bladestorm
                                        Spell.Cast("Bladestorm"),
                                        // actions.aoe+=/colossus_smash,if=dot.rend.ticking
                                        Spell.Buff("Colossus Smash", req => DebuffRendTicking),
                                        // actions.aoe+=/mortal_strike,if=cooldown.colossus_smash.remains>1.5&target.health.pct>20&active_enemies=2
                                        Spell.Buff("Mortal Strike", req => CooldownColossusSmash > 1.5 && Target.HealthPercent > 20 && scenario.MobCount == 2),
                                        // actions.aoe+=/execute,target=2,if=active_enemies=2
                                        Spell.Cast("Execute", req => scenario.MobCount == 2),
                                        // actions.aoe+=/execute,if=((rage>60|active_enemies=2)&cooldown.colossus_smash.remains>execute_time)|debuff.colossus_smash.up|target.time_to_die<5
                                        Spell.Cast("Execute", 
                                            req => ((Me.CurrentRage > 60 || scenario.MobCount == 2) && CooldownColossusSmash > scenario.GcdTime)
                                                || DebuffColossusSmashUp
                                                || Target.TimeToDeath() < 5
                                            ),
                                        // actions.aoe+=/dragon_roar,if=cooldown.colossus_smash.remains>1.5&!debuff.colossus_smash.up
                                        Spell.Cast( "Dragon Roar", req => CooldownColossusSmash > 1.5 && !DebuffColossusSmashUp ),
                                        // actions.aoe+=/whirlwind,if=cooldown.colossus_smash.remains>1.5&(target.health.pct>20|active_enemies>3)
                                        Spell.Cast(
                                            "Whirlwind",
                                            req => CooldownColossusSmash > 1.5 
                                                && (Target.HealthPercent > 20 || scenario.MobCount > 3)
                                            ),
                                        // actions.aoe+=/rend,cycle_targets=1,if=!ticking&target.time_to_die>8
                                        Spell.Buff("Rend",on => Unit.UnfriendlyUnits().FirstOrDefault(u => !u.HasMyAura("Rend") /* timetodie only for current target */)),
                                        // actions.aoe+=/siegebreaker
                                        Spell.Cast("Siegebreaker"),
                                        // actions.aoe+=/storm_bolt,if=cooldown.colossus_smash.remains>4|debuff.colossus_smash.up
                                        Spell.Cast("Storm Bolt", req => CooldownColossusSmash > 4 || DebuffColossusSmash > 0),
                                        // actions.aoe+=/shockwave
                                        Spell.Cast("Shockwave"),
                                        // actions.aoe+=/execute,if=buff.sudden_death.react
                                        Common.CreateExecuteOnSuddenDeath()
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        private static Composite CreateArmsAoeCombat(SimpleIntDelegate aoeCount)
        {
            return new PrioritySelector(
                Spell.HandleOffGCD( Spell.BuffSelf("Sweeping Strikes", ret => aoeCount(ret) >= 2, 0, HasGcd.No) ),
                new Decorator(ret => Spell.UseAOE && aoeCount(ret) >= 3,
                    new PrioritySelector(
                        Spell.Cast( "Thunder Clap" ),

                        Spell.Cast("Bladestorm", ret => aoeCount(ret) >= 4),
                        Spell.Cast("Shockwave", ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 3),
                        Spell.Cast("Dragon Roar"),

                        Spell.Cast("Whirlwind"),
                        Spell.Cast("Mortal Strike"),
                        Spell.Cast("Colossus Smash", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Colossus Smash")),
                        Spell.Cast("Overpower")
                        )
                    )
                );
        }


        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Battlegrounds)]
        public static Composite CreateArmsCombatBuffsBattlegrounds()
        {
            return new Throttle(
                new Decorator(
                    ret => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange,

                    new PrioritySelector(
                        Spell.BuffSelf(Common.SelectedShoutAsSpellName),

                        Common.CreateDieByTheSwordBehavior(),

                        Spell.BuffSelf("Rallying Cry", req => Me.HealthPercent < 60, gcd: HasGcd.No),

                        new Decorator(
                            ret => Me.CurrentTarget.IsWithinMeleeRange && Me.CurrentTarget.IsCrowdControlled(),
                            new PrioritySelector(
                                Spell.HandleOffGCD( Spell.BuffSelf("Avatar", req => true, 0, HasGcd.No)),
                                Spell.HandleOffGCD(Spell.BuffSelf("Bloodbath", req => true, 0, HasGcd.No)),
                                Spell.HandleOffGCD(Spell.BuffSelf("Recklessness", req => true, 0, HasGcd.No))
                                )
                            ),

                        // Execute is up, so don't care just cast
                        // try to avoid overlapping Enrages
                        Spell.HandleOffGCD(
                            Spell.BuffSelf("Berserker Rage", 
                                req => !Common.IsEnraged
                                    && Spell.GetSpellCooldown("Mortal Strike").TotalSeconds > 4
                                    && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds > 6,
                                0,
                                HasGcd.No
                                )
                            ),

                        Spell.Cast( "Colossus Smash", req => !Me.CurrentTarget.HasAura("Colossus Smash")),
                        Spell.Cast( "Rend", req => Me.CurrentTarget.HasAuraExpired("Rend", 4)),
                        Spell.Cast( "Execute", req => Me.CurrentRage > 60 && Me.CurrentTarget.HealthPercent <= 20),
                        Spell.Cast( "Mortal Strike"),
                        Spell.Cast( "Slam"),
                        Spell.Cast("Whirlwind", req => Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8)),

                        new Decorator(
                            req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8,
                            new PrioritySelector(
                                Spell.Cast("Dragon Roar", req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8),
                                Spell.Cast("Storm Bolt"),
                                Spell.BuffSelf("Bladestorm"),
                                Spell.Cast("Shockwave")
                                )
                            ),

                        // if we are low-level with low rage regen, do any damage we can
                        new Decorator(
                            req => !SpellManager.HasSpell("Whirlwind"),
                            new PrioritySelector(
                                Spell.Cast("Rend"),
                                Spell.Cast("Thunder Clap", req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8)
                                )
                            )
                        )
                    )
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Battlegrounds)]
        public static Composite CreateArmsCombatBattlegrounds()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && !StyxWoW.Me.HasAura("Bladestorm"),

                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        CreateDiagnosticOutputBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Spell.Cast("Shattering Throw",
                            ret => Me.CurrentTarget.IsPlayer
                                && Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection")),

                        Common.CreateVictoryRushBehavior(),
                      
            #region Stun

                // charge them now
                        Common.CreateChargeBehavior(),

                        // another stun on them if possible
                        new Decorator(
                            ret => !Me.CurrentTarget.Stunned && !Me.HasAura("Charge"),
                            new PrioritySelector(
                                Spell.Cast("Shockwave", req => Me.CurrentTarget.SpellDistance() < 10 && Me.IsSafelyFacing(Me.CurrentTarget, 90f)),
                                Spell.Cast("Storm Bolt", req => Spell.IsSpellOnCooldown("Shockwave") || Me.CurrentTarget.SpellDistance() > 10)
                                )
                            ),

            #endregion

            #region Slow

                // slow them down
                        new Sequence(
                            new Decorator(
                                ret => Common.IsSlowNeeded(Me.CurrentTarget),
                                new PrioritySelector(
                                    Spell.Buff("Hamstring")
                                    )
                                ),  
                            new Wait( TimeSpan.FromMilliseconds(500), until => !Common.IsSlowNeeded(Me.CurrentTarget), new ActionAlwaysSucceed())
                            ),

            #endregion

            #region Damage

                         Common.CreateExecuteOnSuddenDeath(),

                // see if we can get debuff on them
                        Spell.Cast("Colossus Smash", ret => Me.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModDecreaseSpeed) && Me.CurrentTarget.GetAuraTimeLeft("Colossus Smash").TotalMilliseconds < 1500),

                        Spell.Cast("Heroic Strike", req => Me.RagePercent > 85),
                        Spell.Cast("Mortal Strike"),
                        Spell.Cast("Overpower"),
                        Spell.Cast("Slam", req => Me.RagePercent > 65 && Me.CurrentTarget.HasAura("Colossus Smash")),

                        Spell.Cast("Thunder Clap",
                            req => {
                                if (Me.CurrentTarget.SpellDistance() <= Common.DistanceWindAndThunder(8))
                                {
                                    // cast only if out of melee or behind us
                                    if (!Me.CurrentTarget.IsWithinMeleeRange || !Me.IsSafelyFacing(Me.CurrentTarget))
                                        return true;
                                }

                                return false;
                            })

            #endregion

                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }


        private static void UseTrinkets()
        {
            var firstTrinket = StyxWoW.Me.Inventory.Equipped.Trinket1;
            var secondTrinket = StyxWoW.Me.Inventory.Equipped.Trinket2;
            var hands = StyxWoW.Me.Inventory.Equipped.Hands;

            if (firstTrinket != null && CanUseEquippedItem(firstTrinket))
                firstTrinket.Use();


            if (secondTrinket != null && CanUseEquippedItem(secondTrinket))
                secondTrinket.Use();

            if (hands != null && CanUseEquippedItem(hands))
                hands.Use();

        }
        private static bool CanUseEquippedItem(WoWItem item)
        {
            string itemSpell = Lua.GetReturnVal<string>("return GetItemSpell(" + item.Entry + ")", 0);
            if (string.IsNullOrEmpty(itemSpell))
                return false;

            return item.Usable && item.Cooldown <= 0;
        }

/*
        static bool NeedTasteForBloodDump
        {
            get
            {
                var tfb = Me.GetAllAuras().FirstOrDefault(a => a.Name == "Taste for Blood" && a.TimeLeft > TimeSpan.Zero && a.StackCount > 0);
                if (tfb != null)
                {
                    // If we have more than 3 stacks, pop HS
                    if (tfb.StackCount >= 3)
                    {
                        Logger.WriteDebug(Color.White, "^Taste for Blood");
                        return true;
                    }

                    // If it's about to drop, and we have at least 2 stacks, then pop HS.
                    // If we have 1 stack, then a slam is better used here.
                    if (tfb.TimeLeft.TotalSeconds < 1 && tfb.StackCount >= 2)
                    {
                        Logger.WriteDebug(Color.White, "^Taste for Blood (falling off)");
                        return true;
                    }
                }
                return false;
            }
        }
*/
        static bool NeedHeroicStrikeDumpIcyVeins
        {
            get
            {
                if (Me.GotTarget() && Me.RagePercent >= 70 && Spell.CanCastHack("Heroic Strike", Me.CurrentTarget, skipWowCheck: true))
                {
                    if (Me.RagePercent >= (Me.MaxRage - 15) && (Me.CurrentTarget.HealthPercent > 20 || !SpellManager.HasSpell("Colossus Smash")))
                    {
                        Logger.Write( LogColor.Hilite, "^Heroic Strike - Rage Dump @ {0}%", (int)Me.RagePercent);
                        return true;
                    }

                    if (Me.CurrentTarget.HasAura("Colossus Smash"))
                    {
                        Logger.Write( LogColor.Hilite, "^Heroic Strike - Rage Dump @ {0}% with Colossus Smash active", (int)Me.RagePercent);
                        return true;
                    }
                }

                return false;
            }
        }

        static bool NeedHeroicStrikeDumpNoxxic
        {
            get
            {
                if (Me.GotTarget() && Me.RagePercent >= 70 && Spell.CanCastHack("Heroic Strike", Me.CurrentTarget, skipWowCheck: true))
                {
                    if (Me.CurrentTarget.HasAura("Colossus Smash") || !SpellManager.HasSpell("Colossus Smash") || Me.CurrentTarget.TimeToDeath() < 8)
                    {
                        Logger.Write( LogColor.Hilite, "^Heroic Strike - Rage Dump @ {0}%", (int)Me.RagePercent);
                        return true;
                    }
                }
                return false;
            }
        }

        private static Composite CreateDiagnosticOutputBehavior(string context = null)
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            if (context == null)
                context = Dynamics.CompositeBuilder.CurrentBehaviorType.ToString();
            
            context = "<<" + context + ">>";

            return new ThrottlePasses(
                1, TimeSpan.FromSeconds(1.5), RunStatus.Failure,
                new Action(ret =>
                {
                    string log;
                    log = string.Format(context + " h={0:F1}%/r={1:F1}%, stance={2}, Enrage={3} Coloss={4} MortStrk={5}",
                        Me.HealthPercent,
                        Me.CurrentRage,
                        Me.Shapeshift,
                        Me.ActiveAuras.ContainsKey("Enrage"),
                        (int) Spell.GetSpellCooldown("Colossus Smash", -1).TotalMilliseconds,
                        (int) Spell.GetSpellCooldown("Mortal Strike", -1).TotalMilliseconds
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        log += string.Format(", th={0:F1}%, dist={1:F1}, inmelee={2}, face={3}, loss={4}, dead={5} secs, flying={6}",
                            target.HealthPercent,
                            target.Distance,
                            target.IsWithinMeleeRange.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            target.InLineOfSpellSight.ToYN(),
                            target.TimeToDeath(),
                            target.IsFlying.ToYN()
                            );
                    }

                    int mobc;
                    bool avoidaoe;
                    int mobcc;

                    if (scenario == null)
                    {
                        mobc = 0;
                        avoidaoe = false;
                        mobcc = 0;
                    }
                    else
                    {
                        mobc = scenario.MobCount;
                        avoidaoe = scenario.AvoidAOE;
                        mobcc = scenario.Mobs == null ? 0 : scenario.Mobs.Count();
                    }

                    log += string.Format(
                        "cdcs={0:F2}, cdms={1:F2}, mobs={2}, avoidaoe={3}, enemies={4}",
                        CooldownColossusSmash,
                        CooldownMortalStrike,
                        mobc,
                        avoidaoe.ToYN(),
                        mobcc
                        );

                    Logger.WriteDebug(Color.AntiqueWhite, log);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion

        private static Composite _checkWeapons = null;
        public static Composite CheckThatWeaponIsEquipped()
        {
            if (_checkWeapons == null)
            {
                _checkWeapons = new ThrottlePasses(60,
                    new Sequence(
                        new DecoratorContinue(
                            ret => !Me.Disarmed && !IsWeapon2H(Me.Inventory.Equipped.MainHand),
                            new Action(ret => Logger.Write(Color.HotPink, "User Error: a{0} requires a Two Handed Weapon equipped to be effective", SingularRoutine.SpecAndClassName()))
                            ),
                        new ActionAlwaysFail()
                        )
                    );
            }
            return _checkWeapons;
        }
        public static bool IsWeapon2H(WoWItem hand)
        {
            return hand != null 
                && hand.ItemInfo.ItemClass == WoWItemClass.Weapon
                && hand.ItemInfo.InventoryType == InventoryType.TwoHandWeapon;
        }
    }
}