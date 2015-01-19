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
        static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }

        public static bool talentGladiator { get; set; }
        public static bool glyphCleave { get; set; }

        [Behavior(BehaviorType.Initialize, WoWClass.Warrior, WoWSpec.WarriorProtection)]
        public static Composite CreateProtectionInit()
        {
            talentGladiator = Common.HasTalent(WarriorTalents.GladiatorsResolve);
            glyphCleave = TalentManager.HasGlyph("Cleave");
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

                Helpers.Common.CreateDismount("Pulling"),

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
                req => !Unit.IsTrivial(Me.CurrentTarget),
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
                                    ret => Me.CurrentTarget.IsBoss() || Me.CurrentTarget.IsPlayer || (!Me.IsInGroup() && AoeCount >= 3),
                                    new PrioritySelector(
                                        Spell.HandleOffGCD( Spell.BuffSelf("Recklessness", req => true, 0, HasGcd.No)),
                                        Spell.HandleOffGCD( Spell.BuffSelf("Avatar", req => true, 0, HasGcd.No))
                                        )
                                    ),

                                Spell.Cast("Bloodbath"),
                                Spell.Cast("Berserker Rage")
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

                CreateFightAssessment(),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        new Decorator(
                            req => Me.Shapeshift != (ShapeshiftForm) WarriorStance.GladiatorStance,
                            CreateProtectionDefensiveCombat()
                            ),

                        new Decorator(
                            req => Me.Shapeshift == (ShapeshiftForm) WarriorStance.GladiatorStance,
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
                    ret => UseAOE,
                    new PrioritySelector(
                        Spell.Cast("Thunder Clap", on => Unit.UnfriendlyUnits(Common.DistanceWindAndThunder(8)).FirstOrDefault()),
                        Spell.Cast("Bladestorm", on => Unit.UnfriendlyUnits(8).FirstOrDefault(), ret => AoeCount >= 4),
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
                    new PrioritySelector(
                        Spell.Cast("Cleave", ret => Spell.UseAOE && UseAOE && Me.CurrentRage > RageDump && Me.IsInGroup() && UseAOE),
                        Spell.Cast("Heroic Strike", req => HasUltimatum || Me.CurrentRage > RageDump)
                        )
                    ),

                Spell.Cast("Heroic Strike", req => !SpellManager.HasSpell("Devastate") || !HasShieldInOffHand),

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
                    ret => UseAOE,
                    new PrioritySelector(
                        Spell.Cast("Thunder Clap", on => Unit.UnfriendlyUnits(Common.DistanceWindAndThunder(8)).FirstOrDefault()),
                        Spell.Cast("Bladestorm", on => Unit.UnfriendlyUnits(8).FirstOrDefault(), ret => AoeCount >= 4),
                        Spell.Cast("Shockwave", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => Me.IsSafelyFacing(u)), ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 3),
                        Spell.Cast("Dragon Roar", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => Me.IsSafelyFacing(u)), ret => Me.CurrentTarget.SpellDistance() <= 8 || Me.CurrentTarget.IsWithinMeleeRange)
                        )
                    ),

                Common.CreateExecuteOnSuddenDeath(),

                Spell.Cast("Shield Charge", ret => HasShieldInOffHand && (Spell.GetCharges("Shield Charge") >= 2 || !Me.HasAura("Shield Charge"))),
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

        static bool UseAOE
        {            
            get
            {
                if (Me.GotTarget() && Me.CurrentTarget.IsPlayer)
                    return false;

                return AoeCount >= 2 && Spell.UseAOE;
            }
        }

        static int cntAoeTargets { get; set; }
        static int cntCC { get; set; }

        static Composite CreateFightAssessment()
        {
            return new Action( r => {
                cntAoeTargets = 0;
                cntCC = 0;

                foreach (var u in Unit.UnfriendlyUnits())
                {
                    if (u.SpellDistance() < 8)
                    {
                        if (u.IsCrowdControlled())
                            cntCC++;
                        else if (u.Combat && (u.Aggro || u.PetAggro || u.IsTargetingUs()))
                            cntAoeTargets++;
                        if (u.IsPlayer)
                        {
                            cntAoeTargets = 1;
                            cntCC = 0;
                            break;
                        }
                    }
                }

                return RunStatus.Failure;
            });
        }
        static int AoeCount
        {
            get
            {
                return cntAoeTargets;
            }
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


        private static Composite CreateProtectionDiagnosticOutput()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses( 1,
                new Action(ret =>
                    {
                    string log = string.Format("... [prot] h={0:F1}%/r={1:F1}%, stnc={2}, Ultim={3}, aoe={4}, cc={5}",
                        Me.HealthPercent,
                        Me.CurrentRage,
                        (WarriorStance) Me.Shapeshift,
                        HasUltimatum,
                        cntAoeTargets,
                        cntCC
                        );
                            
                    TimeSpan tsbs = Me.GetAuraTimeLeft("Bladestorm");
                    if (tsbs > TimeSpan.Zero)
                        log += string.Format(", Bladestorm={0:F0} ms", tsbs.TotalMilliseconds);

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
                    return RunStatus.Failure;
                    })
                );
        }


        private static Composite CreateGladiatorDiagnosticOutput()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1,
                new Action(ret =>
                {
                    string log = string.Format("... [glad] h={0:F1}%/r={1:F1}%, gstnc={2}, Ultim={3}, ShlChgStk={4}, ShlChgBuff={5}, aoe={6}, cc={7}",
                        Me.HealthPercent,
                        Me.CurrentRage,
                        (WarriorStance)Me.Shapeshift,
                        HasUltimatum,
                        Spell.GetCharges("Shield Charge"),
                        (long)Me.GetAuraTimeLeft("Shield Charge").TotalMilliseconds,
                        cntAoeTargets,
                        cntCC
                        );

                    TimeSpan tsbs = Me.GetAuraTimeLeft("Bladestorm");
                    if (tsbs > TimeSpan.Zero)
                        log += string.Format(", Bladestorm={0:F0} ms", tsbs.TotalMilliseconds);

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
                    return RunStatus.Failure;
                })
                );
        }

        #endregion

    }
}