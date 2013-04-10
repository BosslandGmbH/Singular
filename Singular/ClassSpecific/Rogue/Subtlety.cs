using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using CommonBehaviors.Actions;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Drawing;

namespace Singular.ClassSpecific.Rogue
{
    public class Subtlety
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static RogueSettings RogueSettings { get { return SingularSettings.Instance.Rogue(); } }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueSubtlety, WoWContext.Normal | WoWContext.Battlegrounds | WoWContext.Instances)]
        public static Composite CreateRogueSubtletyNormalPull()
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

                        CreateSubteltyDiagnosticOutputBehavior("Pull"),

                        Common.CreateRogueOpenerBehavior(),
                        Common.CreateAttackFlyingMobs(),
                        Spell.Buff("Premeditation", req => Common.IsStealthed && Me.ComboPoints < 5),
                        Spell.Cast("Hemorrhage")
                        )
                    ),

                Movement.CreateMoveBehindTargetBehavior(),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueSubtlety, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateRogueSubtletyNormalCombat()
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
                        CreateSubteltyDiagnosticOutputBehavior("Combat"),

                        new Throttle(Helpers.Common.CreateInterruptBehavior()),

                        Movement.CreateMoveBehindTargetBehavior(),

                        Common.CreateRogueOpenerBehavior(),

                        Spell.Buff("Premeditation", req => Common.IsStealthed && Me.ComboPoints <= 3),

                        new Decorator(
                            ret => Common.AoeCount > 1,
                            new PrioritySelector(
                                Spell.BuffSelf("Shadow Dance", ret => Common.AoeCount >= 3),
                                Spell.Cast("Eviscerate", ret => Me.ComboPoints >= 5 && Common.AoeCount < 7 && !Me.CurrentTarget.HasAuraExpired("Crimson Tempest", 7)),
                                Spell.Cast("Crimson Tempest", ret => Me.ComboPoints >= 5),
                                Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount )
                                )
                            ),
                        new Decorator(
                            ret => Common.AoeCount >= 3,
                            new PrioritySelector(
                                Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                                Spell.Cast("Crimson Tempest", ret => Me.ComboPoints >= 5),
                                Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount ),
                                Spell.Cast("Hemorrhage", ret => !SpellManager.HasSpell("Fan of Knives")),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        // Vanish to boost DPS if behind target, not stealthed, have slice/dice, and 0/1 combo pts
                        Spell.BuffSelf("Shadow Dance",
                            ret => Me.GotTarget
                                && !Common.IsStealthed
                                && !Me.HasAuraExpired("Slice and Dice", 3)
                                && Me.ComboPoints < 2),

                        Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                        Spell.Buff("Rupture", true, ret => Me.ComboPoints >= 5),
                        Spell.Cast("Eviscerate", ret => Me.ComboPoints >= 5),

                        Spell.Cast("Ambush", ret => Me.IsSafelyBehind(Me.CurrentTarget) && Common.IsStealthed),
                        Spell.Buff("Hemorrhage"),
                        Spell.Cast("Backstab", ret => Me.IsSafelyBehind(Me.CurrentTarget)),
                        Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount ),

                // following cast is as a Combo Point builder if we can't cast Backstab
                        Spell.Cast("Hemorrhage", ret => Me.CurrentEnergy >= 35 || !SpellManager.HasSpell("Backstab") || !Me.IsSafelyBehind(Me.CurrentTarget)),

                        new ThrottlePasses(60,
                            new Decorator(
                                ret => !Me.Disarmed && !Common.HasDaggerInMainHand && SpellManager.HasSpell("Backstab"),
                                new Action(ret => Logger.Write(Color.HotPink, "config error: cannot cast Backstab without Dagger in Mainhand"))
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotation

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueSubtlety, WoWContext.Instances)]
        public static Composite CreateRogueSubtletyInstanceCombat()
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
                        CreateSubteltyDiagnosticOutputBehavior("Combat"),
                        Helpers.Common.CreateInterruptBehavior(),

                        Spell.Buff("Premeditation", req => Common.IsStealthed && Me.ComboPoints <= 3),

                        new Decorator(
                            ret => Common.AoeCount >= 3 && Spell.UseAOE,
                            new PrioritySelector(
                                Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                                Spell.Cast("Crimson Tempest", ret => Me.ComboPoints >= 5),
                                Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount)
                                )
                            ),

                        Movement.CreateMoveBehindTargetBehavior(),
                        Spell.BuffSelf("Shadow Dance", ret => Me.CurrentTarget.MeIsBehind && !Me.HasAura("Stealth")),
                        Spell.BuffSelf("Vanish", ret => Me.CurrentTarget.IsBoss() && Me.CurrentTarget.MeIsBehind),

                        Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints >= (Me.CurrentTarget.IsBoss() ? 5 : 1) && Me.HasAuraExpired("Slice and Dice", 2)),
                        Spell.Buff("Rupture", true, ret => Me.ComboPoints == 5),
                        Spell.Cast("Eviscerate", ret => Me.ComboPoints == 5),

                        Spell.Cast("Ambush", ret => Me.CurrentTarget.MeIsBehind && (Me.HasAura("Shadow Dance") || Me.HasAura("Stealth"))),
                        Spell.Buff("Hemorrhage"),
                        Spell.Cast("Backstab", ret => Me.CurrentTarget.MeIsBehind && HasDaggersEquipped),
                        Spell.Cast("Hemorrhage", ret => !Me.CurrentTarget.MeIsBehind || !HasDaggersEquipped))
                    ),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        static bool HasDaggersEquipped
        {
            get
            {
                var mainhand = Me.Inventory.Equipped.MainHand;
                var offhand = Me.Inventory.Equipped.OffHand;
                return mainhand != null && mainhand.ItemInfo != null && mainhand.ItemInfo.WeaponClass == WoWItemWeaponClass.Dagger && offhand != null &&
                       offhand.ItemInfo != null && offhand.ItemInfo.WeaponClass == WoWItemWeaponClass.Dagger;
            }
        }

        #endregion

        private static Composite CreateSubteltyDiagnosticOutputBehavior(string sState = "")
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
                            ", {0}, {1:F1}%, {2} secs, {3:F1} yds, behind={4}, loss={5}, rupture={6}",
                            target.SafeName(),
                            target.HealthPercent,
                            target.TimeToDeath(),
                            target.Distance,
                            Me.IsSafelyBehind(target),
                            target.InLineOfSpellSight,
                            (int)target.GetAuraTimeLeft("Rupture", true).TotalSeconds
                            );
                    }

                    Logger.WriteDebug(Color.LightYellow, sMsg);
                    return RunStatus.Failure;
                })
                );
        }
    }
}
