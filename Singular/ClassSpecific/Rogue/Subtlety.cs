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
        private static bool HasTalent(RogueTalents tal) { return TalentManager.IsSelected((int)tal); }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueSubtlety)]
        public static Composite CreateRogueSubtletyNormalPull()
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

                        CreateSubteltyDiagnosticOutputBehavior("Pull"),

                        Common.CreateRoguePullPickPocketButDontAttack(),

                        Common.CreateRogueOpenerBehavior(),
                        Common.CreatePullMobMovingAwayFromMe(),
                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        // ok, everything else failed so just hit him!!!!
                        Spell.HandleOffGCD(Spell.Buff("Premeditation", req => Common.AreStealthAbilitiesAvailable && Me.ComboPoints < 4 && Me.CurrentTarget.IsWithinMeleeRange))
                        )
                    )
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueSubtlety)]
        public static Composite CreateRogueSubtletyNormalCombat()
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

                        Common.CreateRogueOpenerBehavior(),

						Spell.HandleOffGCD(Spell.Cast("Symbols of Death")),
                        Spell.Cast("Shadow Blades"),
						Spell.Cast("Shadowstrike"),
						Spell.Buff("Nightblade", req => Me.ComboPoints >= 5),

                        Spell.Cast("Shadow Dance",
                            ret => !Common.AreStealthAbilitiesAvailable
                                && Spell.GetCharges("Shadow Dance") >= 2),
                        Spell.Cast("Goremaw's Bite", ret => RogueSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None),
                        Spell.Cast("Eviscerate", req => Me.ComboPoints >= 5),
						Spell.Cast("Shuriken Storm", req => Common.AoeCount >= 2),
                        Spell.Cast("Backstab"),

                        // lets try a big hit if stealthed and behind before anything
                        Spell.Cast(sp => "Ambush", chkMov => false, on => Me.CurrentTarget, req => Common.IsAmbushNeeded(), canCast: Common.RogueCanCastOpener),

                        Common.CheckThatDaggersAreEquippedIfNeeded()
                        )
                    )
                );
        }

        #endregion


        [Behavior(BehaviorType.Heal, WoWClass.Rogue, WoWSpec.RogueSubtlety, priority: 99)]
        public static Composite CreateRogueHeal()
        {
            return CreateSubteltyDiagnosticOutputBehavior("Combat");
        }

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
                            ", {0}, {1:F1}%, {2} secs, {3:F1} yds, behind={4}, behindorside={5}, loss={6}, rupture={7}",
                            target.SafeName(),
                            target.HealthPercent,
                            target.TimeToDeath(),
                            target.Distance,
                            Me.IsSafelyBehind(target),
                            Me.IsBehindOrSide(target),
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
