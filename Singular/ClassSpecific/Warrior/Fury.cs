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
        private static LocalPlayer Me => StyxWoW.Me;
        private static WarriorSettings WarriorSettings => SingularSettings.Instance.Warrior();
        private static CombatScenario Scenario { get; set; }

		const int MASSACRE_PROC = 206316;

        [Behavior(BehaviorType.Initialize, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CreateFuryInitialize()
        {
            Scenario = new CombatScenario(8, 1.5f);
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

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Spell.Cast("Storm Bolt", ret => WarriorSettings.ThrowPull == ThrowPull.StormBolt || WarriorSettings.ThrowPull == ThrowPull.Auto),
                        Spell.Cast("Heroic Throw", ret => WarriorSettings.ThrowPull == ThrowPull.HeroicThrow || WarriorSettings.ThrowPull == ThrowPull.Auto),
                        Common.CreateChargeBehavior(),

                        Spell.Cast("Rampage")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CreateFuryNormalHeal()
        {
            return new Decorator(
                ret => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && !Me.CurrentTarget.IsTrivial(),
                new PrioritySelector(
                    Common.CreateWarriorEnragedRegeneration(),

                    Common.CreateDieByTheSwordBehavior()
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
                            Spell.BuffSelfAndWait("Avatar", gcd: HasGcd.No)
                            )
                        ),

                    Spell.BuffSelfAndWait("Battle Cry", ret => (Spell.CanCastHack("Execute") || Common.Tier14FourPieceBonus) && (StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances), gcd: HasGcd.No),

                    new Decorator(
                        ret => Me.CurrentTarget.IsBoss(),
                        new PrioritySelector(
                            Spell.BuffSelfAndWait("Avatar", ret => Me.CurrentTarget.IsBoss(), gcd: HasGcd.No),
                            Spell.BuffSelfAndWait("Bloodbath", ret => Me.CurrentTarget.IsBoss(), gcd: HasGcd.No)
                            )
                        ),

                    Spell.Cast("Storm Bolt"),  // in normal rotation

                    Spell.BuffSelf(Common.SelectedShoutAsSpellName)

                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CreateFuryCombatNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && !StyxWoW.Me.HasAura("Bladestorm"),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        CreateDiagnosticOutputBehavior("Combat"),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

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

                        new Throttle(
                            TimeSpan.FromSeconds( 0.5),
                            new Sequence(
                                new Decorator(
                                    ret => Common.IsSlowNeeded(Me.CurrentTarget),
                                    new PrioritySelector(
                                        Spell.Buff("Piercing Howl", ret => Me.CurrentTarget.SpellDistance().Between(8, 15)),
                                        Spell.Buff("Hamstring")
                                        )
                                    ),
                                new Wait(TimeSpan.FromMilliseconds(500), until => !Common.IsSlowNeeded(Me.CurrentTarget), new ActionAlwaysSucceed())
                                )
                            ),

                        //Interupts
                        Helpers.Common.CreateInterruptBehavior(),
                        Common.CreateSpellReflectBehavior(),

                        // Heal up in melee
                        Common.CreateVictoryRushBehavior(),

                        Common.CreateExecuteOnSuddenDeath(),

                        // AOE
                        // -- check melee dist+3 rather than 8 so works for large hitboxes (8 is range of DR and WW)

                        // Artifact Weapon
                        new Decorator(  // Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3,
                            ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() < Common.DistanceWindAndThunder(8)) >= 3,
                            new PrioritySelector(
                                new Decorator(
                                    ret => WarriorSettings.UseArtifactOnlyInAoE,
                                        new PrioritySelector(
                                            Spell.Cast("Odyn's Fury", ret => WarriorSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None && Me.CurrentTarget.IsWithinMeleeRange)
                                        )
                                ),
                                Spell.BuffSelf("Avatar", ret => WarriorSettings.AvatarOnCooldownAOE),
                                Spell.BuffSelf("Bladestorm"),
                                Spell.Cast("Shockwave"),

                                Spell.Cast("Whirlwind", ret => !Me.HasAura("Meat Cleaver") && Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8)),
                                Spell.Cast("Rampage", ret => !Me.HasAura("Enrage")),
                                Spell.Cast("Bloodthirst"),
                                Spell.Cast("Raging Blow", ret => Scenario.MobCount < 3 && Me.HasAura("Raging Blow", 1)),
                                Spell.Cast("Bloodthirst"),
                                Spell.Cast("Whirlwind", ret => Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8)),

                                // do some AOE prior to learning BT
                                Spell.Cast("Thunder Clap", req => !SpellManager.HasSpell("Whirlwind"))
                                )
                            ),

                        // Use the single target rotation!
                        SingleTarget(),

                            // Charge if we can
                        Common.CreateChargeBehavior(),

                        Common.CreateAttackFlyingOrUnreachableMobs()

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
                        Spell.Cast("Odyn's Fury",
                            ret =>
                                !WarriorSettings.UseArtifactOnlyInAoE &&
                                WarriorSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None &&
                                Me.CurrentTarget.IsWithinMeleeRange),
                        Spell.BuffSelf("Avatar", ret => WarriorSettings.AvatarOnCooldownSingleTarget),
                        Spell.BuffSelf("Berserker Rage",
                            ret =>
                                !Common.IsEnraged && Common.HasTalent(WarriorTalents.Outburst) &&
                                StyxWoW.Me.CurrentTarget.IsWithinMeleeRange),
                        new Decorator(
                            req =>
                                (!Common.IsEnraged || Me.RagePercent >= 85 ||
                                 Common.HasTalent(WarriorTalents.Carnage) && Me.RagePercent >= 70) &&
                                SpellManager.CanCast("Rampage"),
                            new PrioritySelector(
                                Spell.Cast("Dragon Roar"),
                                Spell.Cast("Battle Cry"),
                                Spell.Cast("Rampage")
                            )),
                        Spell.Cast("Bloodthirst", ret => !Common.IsEnraged),
                        Spell.Cast("Raging Blow", ret => Common.HasTalent(WarriorTalents.InnerRage)), // Could || IsEnraged check here, but DPS is higher if we prioritize Whirlwind
                        Spell.Cast("Whirlwind", ret => Spell.UseAOE && Me.HasActiveAura("Wrecking Ball") && Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8)),
                        Spell.Cast("Raging Blow"),
                        Spell.Cast("Bloodthirst"),
                        Spell.Cast("Furious Slash")
                        )
                    ),

                new Decorator(
                    req => Me.CurrentTarget.HealthPercent <= 20,
                    new PrioritySelector(

                        //Execute to prevent capping your Rage.
                        Spell.Cast("Execute", req => Me.RagePercent >= 75),

                        //Bloodthirst on cooldown when not Enraged. Procs Bloodsurge.
						Spell.Cast("Rampage", ret => !Common.IsEnraged && Me.HasAura (MASSACRE_PROC)),
                        Spell.Cast("Bloodthirst", ret => !Common.IsEnraged || Me.RagePercent < 25),
						Spell.Cast("Raging Blow", ret => Me.RagePercent < 25),

                        //Execute while Enraged or with >= 25 Rage.
                        Spell.Cast("Execute", req => Me.RagePercent >= 25)
                        )
                    ),

                new Decorator(
                    req => !SpellManager.HasSpell("Whirlwind"),
                    new PrioritySelector(
                        Spell.Cast("Execute"),
                        Spell.Cast("Bloodthirst"),
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

                Spell.Cast("Storm Bolt")
                );
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
