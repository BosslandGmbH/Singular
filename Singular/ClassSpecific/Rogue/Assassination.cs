using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CommonBehaviors.Actions;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;

using Styx.TreeSharp;

using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;
using System.Drawing;

namespace Singular.ClassSpecific.Rogue
{
    class Assassination
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static RogueSettings RogueSettings { get { return SingularSettings.Instance.Rogue(); } }
        private static bool HasTalent(RogueTalents tal) { return TalentManager.IsSelected((int)tal); }

        const int BLINDSIDE = 121153;

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Normal | WoWContext.Battlegrounds | WoWContext.Instances)]
        public static Composite CreateAssaRoguePull()
        {
            return new PrioritySelector(
                Common.CreateRogueDismount("Pulling"),
                Common.CreateRoguePullBuffs(),      // needed because some Bots not calling this behavior
                Safers.EnsureTarget(),
                Common.CreateRoguePullSkipNonPickPocketableMob(),
                Common.CreateRogueControlNearbyEnemyBehavior(),
                Common.CreateRogueMoveBehindTarget(),
                Common.RogueEnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && Me.GotTarget() && Me.IsSafelyFacing(Me.CurrentTarget),
                    new PrioritySelector(

                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),
                        CreateAssaDiagnosticOutputBehavior("Pull"),

                        Common.CreateRoguePullPickPocketButDontAttack(),

                        Common.CreateRogueOpenerBehavior(),
                        Common.CreatePullMobMovingAwayFromMe(),
                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Spell.Cast("Mutilate", req => Common.HasTwoDaggers),

                        AssaCastSinisterStrike()
                        )
                    )
                );
        }

        private static Composite AssaCastSinisterStrike()
        {
            return new Decorator(
                req => !Common.HasTwoDaggers,
                new Sequence(
                    new Action(r => Logger.Write( LogColor.Hilite, "^User Error: not Dual Wielding Daggers - using Sinister Strike")),
                    Spell.Cast("Sinister Strike")
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Normal | WoWContext.Battlegrounds )]
        public static Composite CreateAssaRogueNormalCombat()
        {
            return new PrioritySelector(
                Common.RogueEnsureWeKillSappedMobs(),
                Common.CreateRogueMoveBehindTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        // updated time to death tracking values before we need them
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),

                        Helpers.Common.CreateInterruptBehavior(),
                        Common.CreateDismantleBehavior(),

                        Spell.Buff("Vendetta", ret => Me.CurrentTarget.IsPlayer || Me.CurrentTarget.Elite || Me.CurrentTarget.IsBoss() || Common.AoeCount > 1),

                        Spell.Cast("Garrote", ret => Common.AreStealthAbilitiesAvailable && Me.CurrentTarget.MeIsBehind),

                        new Decorator(
                            ret => Spell.UseAOE && Common.AoeCount > 1,
                            new PrioritySelector(
                                ctx => Common.AoeCount >= RogueSettings.AoeSpellPriorityCount,

                                Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 6)),
                                Spell.Cast("Crimson Tempest", req => (bool)req  && Me.ComboPoints >= 5),
                                Spell.Buff(
                                    "Rupture", 
                                    3, 
                                    on => Unit.UnfriendlyUnits(5)
                                        .FirstOrDefault(u => !u.HasMyAura("Rupture") && u.IsWithinMeleeRange && Me.IsSafelyFacing(u) && u.InLineOfSpellSight),
                                    req => !(bool) req
                                    ),
                                Spell.BuffSelf(
                                    "Fan of Knives", 
                                    req => !Me.CurrentTarget.IsPlayer && Common.AoeCount >= RogueSettings.FanOfKnivesCount 
                                    ),
                                Spell.Cast("Mutilate", ret => !SpellManager.HasSpell("Fan of Knives") && Common.HasTwoDaggers),
                                AssaCastSinisterStrike(),

                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        Spell.Cast("Envenom", on => Me.CurrentTarget, req => Me.ComboPoints > 0 && Me.GetAuraTimeLeft("Slice and Dice").TotalMilliseconds.Between(50, 6000)),
                        Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && !Me.HasAuraExpired("Slice and Dice", 1)),
                        Spell.Buff("Rupture", true, ret => Me.ComboPoints >= 5 && (Me.CurrentTarget.IsPlayer || Me.TimeToDeath() > 20) && Me.CurrentTarget.HasAuraExpired("Rupture", 7)),

                        // catch all to make sure we finish at 5 pts
                        new Decorator(
                            ret => Me.ComboPoints >= 5 || !SpellManager.HasSpell("Slice and Dice"),
                            new PrioritySelector(
                                Spell.Cast("Envenom", ret => true),
                                Spell.Cast("Eviscerate", ret => !SpellManager.HasSpell("Envenom"))
                                )
                            ),

                        Spell.Cast("Dispatch", req => Common.HasDaggerInMainHand && (Me.CurrentTarget.HealthPercent < 35 || Me.HasAura(BLINDSIDE))), // daggers

                        Spell.BuffSelf("Fan of Knives", ret => Spell.UseAOE && !Me.CurrentTarget.IsPlayer && Common.AoeCount >= RogueSettings.FanOfKnivesCount),
                        Spell.Cast("Mutilate", req => Common.HasTwoDaggers && Me.CurrentTarget.HealthPercent >= 35),  // daggers

                        Common.CheckThatDaggersAreEquippedIfNeeded(),

                        AssaCastSinisterStrike()
                        )
                    )
                );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Instances)]
        public static Composite CreateAssaRogueInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateRogueMoveBehindTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && Me.GotTarget(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        // updated time to death tracking values before we need them
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),

                        Helpers.Common.CreateInterruptBehavior(),
                        Common.CreateDismantleBehavior(),

                        // Agro management
                        Spell.Cast(
                            "Tricks of the Trade", 
                            ret => Common.BestTricksTarget,
                            ret => RogueSettings.UseTricksOfTheTrade),

                        // Common.CreateRogueFeintBehavior(),

                        new Decorator(
                            ret => Common.AoeCount >= 3 && Spell.UseAOE,
                            new PrioritySelector(
                                Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                                Spell.Buff("Rupture", true, ret => (Me.CurrentTarget.GetAuraTimeLeft("Rupture", true).TotalSeconds < 3)),
                                Spell.Cast("Crimson Tempest", ret => Me.ComboPoints >= 5),
                                Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount ),
                                Spell.Cast("Mutilate", ret => !SpellManager.HasSpell("Fan of Knives") && Common.HasTwoDaggers ),
                                AssaCastSinisterStrike(),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        Spell.Cast("Garrote", ret => Common.AreStealthAbilitiesAvailable && Me.CurrentTarget.MeIsBehind),
                        Spell.Buff("Vendetta",  ret => Me.CurrentTarget.IsBoss() &&  (Me.CurrentTarget.HealthPercent < 35 || TalentManager.IsSelected(13))),

                        // Spend Combo Points
                        Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 3)),
                        Spell.Cast("Rupture", req => Me.CurrentTarget.HasAuraExpired("Rupture", 7)),
                        Spell.Buff("Envenom", true, ret => Me.ComboPoints == 5),

                        // Build Combo Points
                        Spell.Cast("Dispatch", req => Common.HasDaggerInMainHand && (Me.CurrentTarget.HealthPercent < 35 || Me.GetAuraTimeLeft("Blindside").TotalSeconds > 0)),
                        Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount ),
                        Spell.Cast("Mutilate", req => Common.HasTwoDaggers),

                        Common.CheckThatDaggersAreEquippedIfNeeded(),
                        AssaCastSinisterStrike()
                        )
                    )
                );
        }

        #endregion


        [Behavior(BehaviorType.Heal, WoWClass.Rogue, WoWSpec.RogueAssassination, priority:99)]
        public static Composite CreateRogueHeal()
        {
            return CreateAssaDiagnosticOutputBehavior("Combat");
        }

        private static Composite CreateAssaDiagnosticOutputBehavior(string sState = "")
        {
            if (!SingularSettings.Debug)
                return new Action(ret => { return RunStatus.Failure; });

            return new ThrottlePasses(1,
                new Action(ret =>
                {
                    string sMsg;
                    sMsg = string.Format(".... [{0}] h={1:F1}%, e={2:F1}%, moving={3}, stealth={4}, aoe={5}, recup={6}, slic={7}, rawc={8}, combo={9}, aoe={10}",
                        sState,
                        Me.HealthPercent,
                        Me.CurrentEnergy,
                        Me.IsMoving,
                        Common.AreStealthAbilitiesAvailable,
                        Common.AoeCount,
                        (int)Me.GetAuraTimeLeft("Recuperate", true).TotalSeconds,
                        (int)Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds,
                        Me.ComboPoints,
                        Me.ComboPoints,
                        Common.AoeCount 
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        sMsg += string.Format(
                            ", {0}, {1:F1}%, {2} secs, {3:F1} yds, behind={4}, loss={5}, rupt={6}, enven={7}",
                            target.SafeName(),
                            target.HealthPercent,
                            target.TimeToDeath(),
                            target.Distance,
                            Me.IsSafelyBehind(target),
                            target.InLineOfSpellSight,
                            (int)target.GetAuraTimeLeft("Rupture", true).TotalSeconds,
                            (int)target.GetAuraTimeLeft("Envenom", true).TotalSeconds
                            );
                    }

                    Logger.WriteDebug(Color.LightYellow, sMsg);
                    return RunStatus.Failure;
                })
                );
        }
    }
}


