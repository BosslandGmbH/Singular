using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using System.Drawing;

using Action = Styx.TreeSharp.Action;
using Singular.Managers;

namespace Singular.ClassSpecific.Druid
{
    class Guardian
    {
        private static DruidSettings Settings
        {
            get { return SingularSettings.Instance.Druid(); }
        }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        #region Common

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalPreCombatBuffs()
        {
            return new PrioritySelector();
        }

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                // Auto Attack

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        //Shoot flying targets
                        new Decorator(
                            ret => Me.CurrentTarget.IsFlying || !Styx.Pathing.Navigator.CanNavigateFully(Me.Location, Me.CurrentTarget.Location),
                            new PrioritySelector(
                                Spell.Cast("Moonfire"),
                                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 27f, 22f)
                                )
                            ),

                        Common.CastForm(ShapeshiftForm.Bear, req => !Utilities.EventHandlers.IsShapeshiftSuppressed),
                        CreateGuardianWildChargeBehavior(),
                        Common.CreateFaerieFireBehavior( on => Me.CurrentTarget, req => true)
                        )
                    )
                );
        }

        #endregion

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian, priority: 99)]
        public static Composite CreateDruidNonRestoHeal()
        {
            return new PrioritySelector(
                CreateGuardianDiagnosticOutputBehavior( "Combat")
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All, 1)]
        public static Composite CreateGuardianNormalCombatBuffs()
        {
            return new PrioritySelector(
                Common.CastForm( ShapeshiftForm.Bear, req => !Utilities.EventHandlers.IsShapeshiftSuppressed),

                Spell.BuffSelf("Frenzied Regeneration", ret => Me.HealthPercent < Settings.TankFrenziedRegenerationHealth && Me.CurrentRage >=60),
                Spell.BuffSelf("Frenzied Regeneration", ret => Me.HealthPercent < 30 && Me.CurrentRage >= 15),
                Spell.BuffSelf("Savage Defense", ret => Me.HealthPercent <= Settings.TankSavageDefense),
                Spell.BuffSelf("Survival Instincts", ret => Me.HealthPercent <= Settings.TankSurvivalInstinctsHealth),
                Spell.BuffSelf("Barkskin", ret => Me.HealthPercent <= Settings.TankFeralBarkskin),
                Spell.Cast("Renewal", on => Me, ret => Me.HealthPercent <= Settings.SelfRenewalHealth)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalCombat()
        {
            TankManager.NeedTankTargeting = (SingularRoutine.CurrentWoWContext == WoWContext.Instances);

           // Logger.Write("guardian loop.");
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                CreateGuardianWildChargeBehavior(),

                Spell.WaitForCast(FaceDuring.Yes),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        CreateGuardianTauntBehavior(),

                        new Decorator(
                            req => true,    // Noxxic
                            new PrioritySelector(

                                new Decorator(
                                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 8) >= 2,
                                    new PrioritySelector(
                                        Spell.Cast("Berserk"),
                                        Spell.Cast("Thrash"),
                                        Spell.Cast("Maul", ret => StyxWoW.Me.HasAura("Tooth and Claw"))
                                        )
                                    ),

                                Common.CreateFaerieFireBehavior(on => Me.CurrentTarget, req => true),

                                Spell.Cast("Mangle"),
                                Spell.Cast("Thrash", req => Me.CurrentTarget.HasAuraExpired("Thrash", 1)),
                                Spell.Cast("Maul", ret => StyxWoW.Me.HasAura("Tooth and Claw")),

                                Spell.Cast("Lacerate", req => Me.CurrentTarget.HasAuraExpired("Lacerate", "Lacerate", 3, TimeSpan.FromSeconds(3), true)),

                                Spell.Cast("Maul", ret => Me.CurrentTarget.CurrentTargetGuid != Me.Guid || SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                                )
                            ),

                        new Decorator(
                            req => false,
                            new PrioritySelector(
                                Spell.Cast("Maul", ret => Me.CurrentRage >= 90 && StyxWoW.Me.HasAura("Tooth and Claw")),

                                Spell.Cast("Mangle"),
                                Spell.Cast("Thrash", req => Me.CurrentTarget.HasAuraExpired("Thrash", 1) || Me.CurrentTarget.HasAuraExpired("Weakened Blows", 1)),

                                Spell.Cast("Bear Hug",
                                    ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances
                                        && !Me.HasAura("Berserk")
                                        && !Unit.NearbyUnfriendlyUnits.Any(u => u.Guid != Me.CurrentTargetGuid && u.CurrentTargetGuid == Me.Guid)),

                                new Decorator(
                                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 8) >= 2,
                                    new PrioritySelector(
                                        Spell.Cast("Berserk"),
                                        Spell.Cast("Thrash")
                                        )
                                    ),
                                Spell.Cast("Lacerate"),
                                Common.CreateFaerieFireBehavior(on => Me.CurrentTarget, req => true),

                                Spell.Cast("Maul", ret => Me.CurrentTarget.CurrentTargetGuid != Me.Guid || SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                                )
                            ),

                        CreateGuardianWildChargeBehavior()
                        )
                    )
            );
        }

        private static Composite CreateGuardianTauntBehavior()
        {
            if ( !SingularSettings.Instance.EnableTaunting )
                return new ActionAlwaysFail();

            return new Decorator(
                ret => TankManager.Instance.NeedToTaunt.Any()
                    && TankManager.Instance.NeedToTaunt.FirstOrDefault().InLineOfSpellSight,
                new Throttle(TimeSpan.FromMilliseconds(1500),
                    new PrioritySelector(
                // Direct Taunt
                        Spell.Cast("Growl",
                            ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                            ret => true),

                        new Decorator(
                            ret => TankManager.Instance.NeedToTaunt.Any()   /*recheck just before referencing member*/
                                && Me.SpellDistance(TankManager.Instance.NeedToTaunt.FirstOrDefault()) > 10,

                            new PrioritySelector(
                                CreateGuardianWildChargeBehavior(on => TankManager.Instance.NeedToTaunt.FirstOrDefault())
                                )
                            )
                        )
                    )
                );

        }

        private static Throttle CreateGuardianWildChargeBehavior( UnitSelectionDelegate onUnit = null)
        {
            return new Throttle(7,
                new Sequence(
                    Spell.CastHack("Wild Charge", onUnit ?? (on => Me.CurrentTarget), ret => MovementManager.IsClassMovementAllowed && (Me.CurrentTarget.Distance + Me.CurrentTarget.CombatReach).Between( 10, 25)),
                    new Action( ret => StopMoving.Clear() ),
                    new Wait(1, until => !Me.GotTarget() || Me.CurrentTarget.IsWithinMeleeRange, new ActionAlwaysSucceed())
                    )
                );
        }



        #region Diagnostics

        private static Composite CreateGuardianDiagnosticOutputBehavior(string context = null)
        {
            if (context == null)
                context = "...";
            else
                context = "<<" + context + ">>";

            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1,
                new Action(ret =>
                {
                    string log;
                    log = string.Format(context + " h={0:F1}%/rage={1:F1}%/mana={2:F1}%, shape={3}, savage={4}, tooclaw={5}, brsrk={6}",
                        Me.HealthPercent,
                        Me.RagePercent,
                        Me.ManaPercent,
                        Me.Shapeshift.ToString().Length < 4 ? Me.Shapeshift.ToString() : Me.Shapeshift.ToString().Substring(0, 4),
                        (long)Me.GetAuraTimeLeft("Savage Defense", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Tooth and Claw", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Berserk", true).TotalMilliseconds
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        log += string.Format(", th={0:F1}%, dist={1:F1}, inmelee={2}, face={3}, loss={4}, dead={5} secs, rake={6}, lacerat={7}@{8}, thrash={9}, weakarmor={10}",
                            target.HealthPercent,
                            target.Distance,
                            target.IsWithinMeleeRange.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            target.InLineOfSpellSight.ToYN(),
                            target.TimeToDeath(),
                            (long)target.GetAuraTimeLeft("Rake", true).TotalMilliseconds,
                            target.GetAuraStacks("Lacerate", true),
                            (long)target.GetAuraTimeLeft("Lacerate", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Thrash", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Weakened Armor", true).TotalMilliseconds
                            );
                    }

                    Logger.WriteDebug(Color.AntiqueWhite, log);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }
}