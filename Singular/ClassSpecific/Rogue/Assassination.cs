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

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Normal | WoWContext.Battlegrounds | WoWContext.Instances )]
        public static Composite CreateAssaRoguePull()
        {
            return new PrioritySelector(
                Common.CreateRoguePullBuffs(),      // needed because some Bots not calling this behavior

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && Me.GotTarget && Me.IsSafelyFacing(Me.CurrentTarget),
                    new PrioritySelector(

                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),

                        CreateAssaDiagnosticOutputBehavior("Pull"),
                        Common.CreateRogueOpenerBehavior(),
                        Common.CreateAttackFlyingMobs(),

                        Spell.Cast("Mutilate")
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Normal | WoWContext.Battlegrounds )]
        public static Composite CreateAssaRogueNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),

                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),
                        CreateAssaDiagnosticOutputBehavior("Combat"),

                        Helpers.Common.CreateInterruptBehavior(),

                        Spell.Buff("Vendetta", ret => Me.CurrentTarget.IsPlayer || Me.CurrentTarget.Elite || Me.CurrentTarget.IsBoss() || Common.AoeCount > 1),

                        Spell.Cast("Garrote", ret => Common.IsStealthed && Me.CurrentTarget.MeIsBehind),

                        new Decorator(
                            ret => Common.AoeCount >= 3 && Spell.UseAOE,
                            new PrioritySelector(
                                Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                                Spell.Buff("Rupture", true, ret => (Me.CurrentTarget.GetAuraTimeLeft("Rupture", true).TotalSeconds < 3)),
                                Spell.Cast("Crimson Tempest", ret => Me.ComboPoints >= 5),
                                Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount ),
                                Spell.Cast("Mutilate", ret => !SpellManager.HasSpell("Fan of Knives")),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                        Spell.Buff("Rupture", true, ret => Me.ComboPoints == 5 && (Me.CurrentTarget.IsPlayer || Me.TimeToDeath() > 20) && Me.CurrentTarget.HasAuraExpired("Rupture", 3)),

                        new Decorator(
                            ret => Me.ComboPoints == 5 || !SpellManager.HasSpell("Slice and Dice") || (Me.HasAura("Slice and Dice") && Me.HasAuraExpired("Slice and Dice", 3) && Me.ComboPoints > 0),
                            new PrioritySelector(
                                Spell.Cast("Envenom", ret => true),
                                Spell.Cast("Eviscerate", ret => !SpellManager.HasSpell("Envenom"))
                                )
                            ),

                        Spell.Cast("Dispatch"), // daggers

                        Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount ),
                        Spell.Cast("Mutilate"),  // daggers

                        new ThrottlePasses(60,
                            new PrioritySelector(
                                new Decorator(
                                    ret => !Me.Disarmed && !Common.HasDaggerInMainHand && SpellManager.HasSpell("Dispatch"),
                                    new Action(ret => Logger.Write(Color.HotPink, "config error: cannot cast Dispatch without Dagger in Mainhand"))
                                    ),
                                new Decorator(
                                    ret => !Me.Disarmed && !Common.HasTwoDaggers && SpellManager.HasSpell("Mutilate"),
                                    new Action(ret => Logger.Write(Color.HotPink, "config error: cannot cast Mutilate without two Daggers"))
                                    )
                                )
                            )
                        )
                    ),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Instances)]
        public static Composite CreateAssaRogueInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),
                        CreateAssaDiagnosticOutputBehavior("Combat"),

                        Helpers.Common.CreateInterruptBehavior(),

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
                                Spell.Cast("Mutilate", ret => !SpellManager.HasSpell("Fan of Knives")),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        Movement.CreateMoveBehindTargetBehavior(),

                        Spell.Cast("Garrote", ret => Common.IsStealthed && Me.CurrentTarget.MeIsBehind),
                        Spell.Buff("Vendetta",  ret => Me.CurrentTarget.IsBoss() &&  (Me.CurrentTarget.HealthPercent < 35 || TalentManager.IsSelected(13))),

                        Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                        Spell.Buff("Rupture", true, ret => (Me.CurrentTarget.GetAuraTimeLeft("Rupture", true).TotalSeconds < 3)),
                        Spell.Buff("Envenom", true, ret => (Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds < 3 && Me.ComboPoints > 0) || Me.ComboPoints == 5),
                        Spell.Cast("Dispatch"),

                        Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount ),
                        Spell.Cast("Mutilate")
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion


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
                        Common.IsStealthed,
                        Common.AoeCount,
                        (int)Me.GetAuraTimeLeft("Recuperate", true).TotalSeconds,
                        (int)Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds,
                        Me.RawComboPoints,
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


