﻿using System.Linq;
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
using Singular.Utilities;
using Styx.CommonBot.POI;

namespace Singular.ClassSpecific.Warrior
{
    public partial class Protection
    {

        #region Common

        static LocalPlayer Me => StyxWoW.Me;
        static WarriorSettings WarriorSettings => SingularSettings.Instance.Warrior();

        const int ULTIMATUM_PROC = 122510;
        const int FOCUSED_PROC = 202573;
        const int IGNORE_PROC = 202574;

        //[Behavior(BehaviorType.Initialize, WoWClass.Warrior, WoWSpec.WarriorProtection)]

        [Behavior(BehaviorType.Rest, WoWClass.Warrior, WoWSpec.WarriorProtection)]
        public static Composite CreateProtectionRest()
        {
            return new PrioritySelector(

                Common.CheckIfWeShouldCancelBladestorm(),

                Singular.Helpers.Rest.CreateDefaultRestBehaviour(),

                ClassSpecific.Warrior.Protection.CheckThatShieldIsEquippedIfNeeded()
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

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Spell.Cast("Storm Bolt", ret => WarriorSettings.ThrowPull == ThrowPull.StormBolt || WarriorSettings.ThrowPull == ThrowPull.Auto),
                        Spell.Cast("Heroic Throw", ret => WarriorSettings.ThrowPull == ThrowPull.HeroicThrow || WarriorSettings.ThrowPull == ThrowPull.Auto),
                        Common.CreateChargeBehavior(),

                        Spell.Cast("Shield Slam", req => HasShieldInOffHand),

                        // just in case user botting a Prot Warrior without a shield
                        Spell.Cast("Revenge"),
                        Spell.Cast("Thunder Clap", ret => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8) && !Me.CurrentTarget.ActiveAuras.ContainsKey("Weakened Blows")),

                        // filler to try and do something more than auto attack at this point
                        Spell.Cast("Devastate"),

                        CheckThatShieldIsEquippedIfNeeded()
                        )
                    )
                );
        }
        #endregion

        #region Normal

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorProtection)]
        public static Composite CreateProtectionCombatBuffs()
        {
            return new Decorator(
                req => !Unit.IsTrivial(Me.CurrentTarget)
                    && (SingularRoutine.CurrentWoWContext != WoWContext.Instances || Me.Shapeshift != (ShapeshiftForm)WarriorStance.GladiatorStance),
                new Throttle(    // throttle these because most are off the GCD
                    new PrioritySelector(

                        Spell.HandleOffGCD(
                            new PrioritySelector(
                                Spell.BuffSelf("Shield Wall", ret => Me.HealthPercent < WarriorSettings.WarriorShieldWallHealth, gcd: HasGcd.No),
                                Spell.BuffSelf("Shield Barrier", ret => Me.HealthPercent < WarriorSettings.WarriorShieldBarrierHealth, gcd: HasGcd.No),
                                Spell.BuffSelf("Shield Block", ret => Me.HealthPercent < WarriorSettings.WarriorShieldBlockHealth && !Me.HasAura("Shield Block"), gcd: HasGcd.No)
                                )
                            ),

                        Spell.HandleOffGCD(
                            new PrioritySelector(
                                Spell.HandleOffGCD(Spell.BuffSelf("Last Stand", ret => Me.HealthPercent < WarriorSettings.WarriorLastStandHealth, gcd: HasGcd.No))
                                )
                            ),

                        new Decorator(
                            req => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange,
                            new PrioritySelector(
                                new Decorator(
                                    ret => Me.CurrentTarget.IsBoss() || Me.CurrentTarget.IsPlayer || (!Me.IsInGroup() && Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),
                                    new PrioritySelector(
                                        Spell.HandleOffGCD(Spell.Cast("Demoralizing Shout", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(m => m.SpellDistance() < 10), req => true, gcd: HasGcd.No)),
                                        Spell.HandleOffGCD(Spell.BuffSelf("Battle Cry", req => true, 0, HasGcd.No)),
                                        Spell.HandleOffGCD(Spell.BuffSelf("Avatar", req => true, 0, HasGcd.No))
                                        )
                                    ),

                                Spell.BuffSelfAndWait("Berserker Rage", req => Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Turned), gcd: HasGcd.No)
								)
                            )
                        )
                    )
                );
        }

        static WoWUnit intTarget;

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorProtection)]
        public static Composite CreateProtectionCombat()
        {
            TankManager.NeedTankTargeting = SingularRoutine.CurrentWoWContext == WoWContext.Instances;
            EventHandlers.TrackDamage = true;

            return new PrioritySelector(
                // set context to current target
                ctx =>
                {
                    if (TankManager.NeedTankTargeting && TankManager.Instance.FirstUnit != null)
                        return TankManager.Instance.FirstUnit;

                    return Me.CurrentTarget;
                },

                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        new Decorator(
                            req => Me.GotTarget(),
                            CreateProtectionDefensiveCombat()
                            )
                        )
                    )
                );
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
				Common.CreateSpellReflectBehavior(),

                // special "in combat" pull logic for mobs not tagged and out of melee range
                Common.CreateWarriorCombatPullMore(),

                // Multi-target?  get the debuff on them
                new Decorator(
                    ret => Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() > 1,
                    new PrioritySelector(
                        Spell.BuffSelf("Avatar", ret => WarriorSettings.AvatarOnCooldownAOE),
                        Spell.Cast("Thunder Clap", on => Unit.UnfriendlyUnits(Common.DistanceWindAndThunder(8)).FirstOrDefault()),
                        Spell.Cast("Shockwave", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => Me.IsSafelyFacing(u)), ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 3))
                    ),

                Spell.BuffSelf("Avatar", ret => WarriorSettings.AvatarOnCooldownSingleTarget),

                Spell.HandleOffGCD(Spell.Cast("Focused Rage", ret => Me.HasAura(ULTIMATUM_PROC) || Me.RagePercent > 45 && Me.HasAura(FOCUSED_PROC) && !Me.HasAura("Focused Rage") || Me.RagePercent > 90)),
                Spell.HandleOffGCD(Spell.Cast("Ignore Pain",
                    ret =>
                        Me.RagePercent > 70 && Me.HasAura(IGNORE_PROC)
                        || (Me.RagePercent > 40 && Me.HasAura(IGNORE_PROC) && !Me.HasAura("Ignore Pain"))
                        || (Me.RagePercent > 70 && !Me.HasAura("Ignore Pain")))
                ),
                Spell.Cast("Shield Block", ret => Spell.GetCharges("Shield Block") > 1),

                // Generate Rage
                SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                Movement.WaitForFacing(),
                Movement.WaitForLineOfSpellSight(),

                Spell.Cast("Shield Slam", ret => Me.CurrentRage < RageBuild && HasShieldInOffHand),
                Spell.Cast("Revenge"),

                // Artifact Weapon
                new Decorator(
                    ret => WarriorSettings.UseArtifactOnlyInAoE && Clusters.GetConeClusterCount(90f, Unit.UnfriendlyUnits(12), 100f) > 1,
                    new PrioritySelector(
                        Spell.HandleOffGCD(Spell.BuffSelf("Battle Cry", req => true, 0, HasGcd.No)),
                        Spell.Cast("Neltharion's Fury", movement => false, target => Me.CurrentTarget, ret => WarriorSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None)
                    )
                ),
                Spell.HandleOffGCD(Spell.BuffSelf("Battle Cry", req => true, 0, HasGcd.No)),
                Spell.Cast("Neltharion's Fury",
                    movement => false,
                    target => Me.CurrentTarget,
                    ret => !WarriorSettings.UseArtifactOnlyInAoE && WarriorSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None && Clusters.GetConeClusterCount(90f, Unit.UnfriendlyUnits(12), 100f) >= 1
                ),

                // Filler
                Spell.Cast("Devastate"),

                //Charge
                Common.CreateChargeBehavior(),

                Common.CreateAttackFlyingOrUnreachableMobs(),

                new Action(ret =>
                {
                    if (Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && Me.IsSafelyFacing(Me.CurrentTarget))
                        Logger.WriteDebug("--- we did nothing!");
                    return RunStatus.Failure;
                })


            );

        }

        static Composite CreateProtectionTauntBehavior()
        {
            // limit all taunt attempts to 1 per second max since Mocking Banner and Taunt have no GCD
            // .. it will keep us from casting both for the same mob we lost aggro on
            return new Throttle(1, 1,
                new PrioritySelector(
                    ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(),

                    Spell.Cast("Taunt", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault()),

                    Spell.Cast("Storm Bolt", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(i => i.SpellDistance() < 30 && Me.IsSafelyFacing(i))),

                    Spell.Cast("Intercept",
                        ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(
                            mob => Group.Healers.Any(healer => mob.CurrentTargetGuid == healer.Guid && healer.Distance < 25)),
                        ret => MovementManager.IsClassMovementAllowed && Group.Healers.Count(h => h.IsAlive && h.Distance < 40) == 1
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
                return RageDump;
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
                const int ULTIMATUM_PROC = 122510;
                return Me.HasAura(ULTIMATUM_PROC);
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

        private static bool _hasShieldInOffHand;
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


        private static Composite CreateProtectionDiagnosticOutput()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(
                1, TimeSpan.FromMilliseconds(1500), RunStatus.Failure,
                new Action(ret =>
                {
                    string log =
                        $"... [prot] h={Me.HealthPercent:F1}%/r={Me.CurrentRage:F1}%, stnc={(WarriorStance)Me.Shapeshift}, Ultim={HasUltimatum}";

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

        #endregion

    }
}