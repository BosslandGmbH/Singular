using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using System;
using Styx.WoWInternals;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;
using System.Drawing;
using CommonBehaviors.Actions;

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
        private static bool DebuffRendTicking { get { return DebuffRend > 0; } }
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


        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorArms)]
        public static Composite CreateArmsNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateDiagnosticOutputBehavior("Pull"),

                        new Throttle(2, Spell.BuffSelf(Common.SelectedShoutAsSpellName)),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Spell.Cast("Storm Bolt", ret => WarriorSettings.ThrowPull == ThrowPull.StormBolt || WarriorSettings.ThrowPull == ThrowPull.Auto),
                        Spell.Cast("Heroic Throw", ret => WarriorSettings.ThrowPull == ThrowPull.HeroicThrow || WarriorSettings.ThrowPull == ThrowPull.Auto),
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
                    ret => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && !Me.CurrentTarget.IsTrivial(),

                    new PrioritySelector(
                        Common.CreateWarriorEnragedRegeneration(),

                        Common.CreateDieByTheSwordBehavior(),

                        Spell.HandleOffGCD(Spell.BuffSelf("Rallying Cry", req => !Me.IsInGroup() && Me.HealthPercent < 50, 0, HasGcd.No))
                        )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms)]
        public static Composite CreateArmsCombatBuffsNormal()
        {
            return new Throttle(
                new Decorator(
                    ret => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && !Me.CurrentTarget.IsTrivial(),

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
                                Spell.HandleOffGCD(Spell.BuffSelf("Avatar", req => true, 0, HasGcd.No))
                                )
                            ),

                        Spell.Cast("Battle Cry"),
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


        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms)]
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

                                Spell.BuffSelf("Avatar", ret => WarriorSettings.AvatarOnCooldownSingleTarget),

                                new Decorator(
                                    ret => Spell.UseAOE && Me.GotTarget() && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss()) && Me.CurrentTarget.SpellDistance() < 8,
                                    new PrioritySelector(
                                        Spell.Cast("Storm Bolt"),
                                        Spell.BuffSelf("Bladestorm"),
                                        Spell.Cast("Shockwave")
                                        )
                                    ),

                                new Decorator(
                                    new PrioritySelector(
                                        Spell.Cast("Colossus Smash"),
                                        Spell.Cast("Execute", ret => Me.CurrentTarget.HasAura("Colossus Smash") && Me.CurrentTarget.HealthPercent < 20),
                                        Spell.Cast("Overpower"),
                                        Spell.Cast("Mortal Strike", ret => Spell.GetSpellCooldown("Colossus Smash") > TimeSpan.FromSeconds(2)),
                                        Spell.Cast("Slam"),
                                        new ActionAlwaysFail()
                                        )),

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

                        Common.CreateChargeBehavior(),

                        Common.CreateAttackFlyingOrUnreachableMobs()

                        )
                    )
                );
        }
		
        private static Composite CreateArmsAoeCombat(SimpleIntDelegate aoeCount)
        {
            return new PrioritySelector(
                new Decorator(ret => Spell.UseAOE && aoeCount(ret) >= 3,
                    new PrioritySelector(
                        Spell.BuffSelf("Avatar", ret => WarriorSettings.AvatarOnCooldownAOE),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Thunder Clap"),

                        Spell.Cast("Bladestorm", ret => aoeCount(ret) >= 4),
                        Spell.Cast("Shockwave", ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 3),

                        Spell.Cast("Whirlwind"),
                        Spell.Cast("Mortal Strike"),
                        Spell.Cast("Colossus Smash", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Colossus Smash")),
                        Spell.Cast("Overpower")
                        )
                    )
                );
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
                        (int)Spell.GetSpellCooldown("Colossus Smash", -1).TotalMilliseconds,
                        (int)Spell.GetSpellCooldown("Mortal Strike", -1).TotalMilliseconds
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
                            new Action(ret => Logger.Write(Color.HotPink, "User Error: a {0} requires a Two Handed Weapon equipped to be effective", SingularRoutine.SpecAndClassName()))
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