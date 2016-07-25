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

using System.Drawing;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.CommonBot.POI;
using Styx.WoWInternals;


namespace Singular.ClassSpecific.Rogue
{
    public class Outlaw
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static RogueSettings RogueSettings => SingularSettings.Instance.Rogue();
	    private static bool HasTalent(RogueTalents tal) { return TalentManager.IsSelected((int)tal); } 

        #region Normal Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueOutlaw, WoWContext.All)]
        public static Composite CreateRogueCombatPull()
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

                        CreateCombatDiagnosticOutputBehavior("Pull"),

                        Common.CreateRoguePullPickPocketButDontAttack(),

                        Common.CreateRogueOpenerBehavior(),
                        Common.CreatePullMobMovingAwayFromMe(),
                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        // ok, everything else failed so just hit him!!!!
                        Spell.Buff("Revealing Strike", req => true),
                        Spell.Cast("Sinister Strike")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueOutlaw, WoWContext.All)]
        public static Composite CreateRogueCombatNormalCombat()
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
                        new Action(ret =>
                        {
                            Me.CurrentTarget.TimeToDeath();
                            return RunStatus.Failure;
                        }),

                        CreateBladeFlurryBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),
                        Common.CreateDismantleBehavior(),

                        Common.CreateRogueOpenerBehavior(),

                        Spell.Cast("Vanish", when => Me.TimeToDeath() < 2 && Me.TimeToDeath() < Me.CurrentTarget.TimeToDeath()),

                        Spell.BuffSelf("Adrenaline Rush", ret => Me.CurrentEnergy < 20),
                        
                        Spell.Cast("Death from Above", req => !Me.HasAura("Adrenaline Rush") && Me.ComboPoints >= 6),

                        // If talented, should keep it up.
                        Spell.Buff("Slice and Dice", on => Me, ret => Me.ComboPoints > 0),

                        Spell.Buff("Roll the Bones"),

                        Spell.Cast("Run Through", req => Me.ComboPoints >= 6),

                        Spell.Cast("Pistol Shot", req => Me.HasAura("Opportunity")),

                        Spell.Cast("Saber Slash"))
                    )
                );
        }

        #endregion

        internal static Composite CreateBladeFlurryBehavior()
        {
            return new Sequence(
                new PrioritySelector(
                    Spell.BuffSelf("Blade Flurry", ret => Spell.UseAOE && Common.AoeCount > 1, gcd: HasGcd.No),
                    new Decorator(
                        ret => Me.HasAura("Blade Flurry") && (!Spell.UseAOE || Common.AoeCount <= 1),
                        new Sequence(
                            //new Action(ret => Logger.Write( LogColor.Cancel, "/cancel Blade Flurry")),
                            new Action(ret => Me.CancelAura("Blade Flurry")),
                            new Wait(TimeSpan.FromMilliseconds(500), ret => !Me.HasAura("Blade Flurry"), new ActionAlwaysSucceed())
                            )
                        )
                    ),
                new ActionAlwaysFail()  // since no GCD
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Rogue, WoWSpec.RogueOutlaw, priority: 99)]
        public static Composite CreateRogueHeal()
        {
            return CreateCombatDiagnosticOutputBehavior("Combat");
        }

        private static Composite CreateCombatDiagnosticOutputBehavior(string sState = "")
        {
            if (!SingularSettings.Debug)
                return new Action(ret => { return RunStatus.Failure; });

            return new ThrottlePasses(1,
                new Action(ret =>
                {           
                    string sMsg;
                    sMsg = string.Format(".... [{0}] h={1:F1}%, e={2:F1}%, mov={3}, stlth={4}, aoe={5}, recup={6}, slic={7}, rawc={8}, combo={9}, aoe={10}, combat={11}",
                        sState,
                        Me.HealthPercent,
                        Me.CurrentEnergy,
                        Me.IsMoving.ToYN(),
                        Common.AreStealthAbilitiesAvailable.ToYN(),
                        Common.AoeCount,
                        (int) Me.GetAuraTimeLeft("Recuperate", true).TotalSeconds,
                        (int) Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds,
                        Me.ComboPoints,
                        Me.ComboPoints,
                        Common.AoeCount,
                        Me.Combat.ToYN()
                        );

                    WoWAura aura = Me.GetAllAuras().FirstOrDefault(a => a.Name == "Shallow Insight" || a.Name == "Moderate Insight" || a.Name == "Deep Insight");
                    if (aura != null)
                        sMsg += string.Format(", {0}={1}", aura.Name, (int)aura.TimeLeft.TotalSeconds);

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        sMsg += string.Format(
                            ", {0}, {1:F1}%, dies {2}, {3:F1} yds, inmelee={4}, behind={5}, loss={6}, face={7}, rvlstrk={8}, rupture={9}",
                            target.SafeName(),
                            target.HealthPercent,
                            target.TimeToDeath(),
                            target.Distance,
                            target.IsWithinMeleeRange.ToYN(),
                            Me.IsSafelyBehind(target).ToYN(),
                            target.InLineOfSpellSight.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            (int) target.GetAuraTimeLeft("Revealing Strike", true).TotalSeconds,
                            (int) target.GetAuraTimeLeft("Rupture", true).TotalSeconds
                            );
                    }

                    Logger.WriteDebug(Color.LightYellow, sMsg);
                    return RunStatus.Failure;
                })
                );
        }
    }
}
