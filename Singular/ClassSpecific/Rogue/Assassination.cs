using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
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

        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueAssassination)]
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

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueAssassination)]
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

                        Spell.Buff("Rupture", req => Me.ComboPoints >= 5),
                        Spell.Buff("Vendetta", ret => Me.CurrentTarget.IsPlayer || Me.CurrentTarget.Elite || Me.CurrentTarget.IsBoss() || Common.AoeCount > 1),
                        Spell.Buff("Hemorrhage"),
                        Spell.Buff("Garrote"),
                        Spell.Cast("Exsanguinate"),
                        Spell.Cast("Kingsbane", ret => !RogueSettings.UseArtifactOnlyInAoE && RogueSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None),
                        Spell.Cast("Envenom", req => Me.ComboPoints >= 5),
                        Spell.Cast("Mutilate"),

                        new Decorator(
                            ret => Spell.UseAOE && Common.AoeCount > 1,
                            new PrioritySelector(
                                ctx => Common.AoeCount >= RogueSettings.AoeSpellPriorityCount,
                                Spell.Cast("Kingsbane", ret => RogueSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None),
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


