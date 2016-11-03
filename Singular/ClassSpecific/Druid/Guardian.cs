using System;
using System.Drawing;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Druid
{
    class Guardian
    {
        private static DruidSettings Settings => SingularSettings.Instance.Druid();
        private static LocalPlayer Me => StyxWoW.Me;
        private static long RageDeficit => Me.MaxRage - Me.CurrentRage;

        #region Common

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite CreateGuardianPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                // Auto Attack

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        //Shoot flying targets
                        new Decorator(
                            ret => Me.CurrentTarget.IsFlying || !Movement.CanNavigateToMelee(Me.CurrentTarget),
                            new PrioritySelector(
                                Spell.Cast("Moonfire"),
                                Movement.CreateMoveToUnitBehavior(on => StyxWoW.Me.CurrentTarget, 27f, 22f)
                                )
                            ),

                        Common.CastForm(ShapeshiftForm.Bear, req => !Utilities.EventHandlers.IsShapeshiftSuppressed),
                        CreateGuardianWildChargeBehavior()
                        )
                    )
                );
        }

        #endregion

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian, priority: 99)]
        public static Composite CreateGuardianHeal()
        {
            return new PrioritySelector(
                CreateGuardianDiagnosticOutputBehavior("Combat"),

            // defensive
            Spell.HandleOffGCD(Spell.BuffSelf("Survival Instincts", ret => Me.HealthPercent <= Settings.TankSurvivalInstinctsHealth)),
            Spell.HandleOffGCD(Spell.BuffSelf("Barkskin", ret => Me.HealthPercent <= Settings.TankFeralBarkskin)),
            CreateGuardianIronfurBehavior(),
            Spell.HandleOffGCD(Spell.BuffSelf("Bristling Fur", req => Me.RagePercent <= Settings.TankFeralBristlingFurRage
                        && !Me.HasActiveAura("Survival Instincts") && !Me.HasActiveAura("Barkskin"))),
            Spell.HandleOffGCD(Spell.BuffSelf("Mark of Ursol", ret => (Me.HealthPercent < Settings.TankFeralMarkOfUrsolHealth) && Unit.NearbyUnitsInCombatWithMe.Any(u => u.IsCasting))),

                // self-heal
                Spell.HandleOffGCD(Spell.BuffSelf(
                    "Frenzied Regeneration",
                    req => (Me.HealthPercent < Settings.TankFrenziedRegenerationHealth && Me.CurrentRage >= 60)
                        || (Me.HealthPercent < 30 && Me.CurrentRage >= 15)
                    ))
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All, 999)]
        public static Composite CreateGuardianCombatBuffs()
        {
            return new PrioritySelector(

                Common.CastForm(ShapeshiftForm.Bear, req => !Utilities.EventHandlers.IsShapeshiftSuppressed)

                );
        }

        public static Composite CreateGuardianIronfurBehavior()
        {
            // Ironfur is one of the main defensive abilities now in Guardian, so it's a bit more complicated to find out if we need to use it.
            return new PrioritySelector(
                // ironfur by health % + mob count
                Spell.HandleOffGCD(Spell.BuffSelf("Ironfur", ret =>
                    Settings.TankFeralIronfurHealth > 0 && Settings.TankFeralIronfurNormalUnits > 0 &&
                    Me.HealthPercent <= Settings.TankFeralIronfurHealth && Unit.NearbyUnitsInCombatWithMe.Count() >= Settings.TankFeralIronfurNormalUnits)),

                // ironfur by health% only
                Spell.HandleOffGCD(Spell.BuffSelf("Ironfur", ret =>
                    Settings.TankFeralIronfurHealth > 0 && Me.HealthPercent < Settings.TankFeralIronfurHealth)),

                // ironfur by mob count only
                Spell.HandleOffGCD(Spell.BuffSelf("Ironfur", ret =>
                    Settings.TankFeralIronfurNormalUnits > 0 && Unit.NearbyUnitsInCombatWithMe.Count() >= Settings.TankFeralIronfurNormalUnits)),

                Spell.HandleOffGCD(Spell.BuffSelf("Ironfur", ret =>
                    Settings.TankFeralIronfurHealth > 0 && Unit.NearbyUnitsInCombatWithMe.Any(u => u.IsBoss || u.Elite))),

                Spell.HandleOffGCD(Spell.Cast("Ironfur", ret =>
                    Settings.TankFeralIronfurHealth > 0 && Me.CurrentRage > 95 && Unit.NearbyUnitsInCombatWithMe.Any(u => u.IsBoss || u.IsStressful())))
            );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite CreateGuardianCombat()
        {
            TankManager.NeedTankTargeting = (SingularRoutine.CurrentWoWContext == WoWContext.Instances);

            return new PrioritySelector(
                //Some profiles have problems pulling. Having this here reduces somewhat the delay to enter Bear Form this profiles cause.
                Common.CastForm(ShapeshiftForm.Bear, req => !Utilities.EventHandlers.IsShapeshiftSuppressed && Me.Shapeshift != ShapeshiftForm.Bear),
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                CreateGuardianWildChargeBehavior(),

                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        ctx => TankManager.Instance.TargetList.FirstOrDefault(u => u.IsWithinMeleeRange) ?? Me.CurrentTarget,
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        CreateGuardianTauntBehavior(),

                        Spell.HandleOffGCD(Spell.Cast("Rage of the Sleeper", ret => Settings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None
                                                                                && Me.HealthPercent <= Settings.ArtifactHealthPercent)),
                        Spell.BuffSelf("Incarnation: Guardian of Ursoc", ret => Me.CurrentRage <= 20),
                        Spell.Cast("Moonfire", on => (WoWUnit)on, ret => Me.HasActiveAura("Galactic Guardian")),
                        // thrash is slightly higher priority when mob count >= 3.
                        Spell.Cast("Thrash", on => (WoWUnit)on, req => Unit.UnitsInCombatWithMe(8).Count() >= 3),
                        Spell.Cast("Mangle", on => (WoWUnit)on),
                        Spell.Cast("Thrash", on => (WoWUnit)on, req => Unit.UnitsInCombatWithMe(8).Any()),
                        Spell.Cast("Moonfire", on => Unit.NearbyUnitsInCombatWithUsOrOurStuff.FirstOrDefault(u => u.GetAuraTimeLeft("Moonfire").TotalSeconds < 2.5)),
                        Spell.Cast("Swipe", on => (WoWUnit)on, req => Unit.UnitsInCombatWithMe(8).Any()),
                        Spell.HandleOffGCD(Spell.Cast("Maul", on => (WoWUnit)on, ret => (!Me.HasActiveAura("Ironfur") && !Me.HasActiveAura("Mark of Ursol") && RageDeficit < 10))),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        CreateGuardianWildChargeBehavior()
                        )
                    )
            );
        }


        private static Composite CreateGuardianTauntBehavior()
        {
            if (!SingularSettings.Instance.EnableTaunting)
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

        private static Throttle CreateGuardianWildChargeBehavior(UnitSelectionDelegate onUnit = null)
        {
            return new Throttle(7,
                new Sequence(
                    Spell.CastHack("Wild Charge", onUnit ?? (on => Me.CurrentTarget), ret => MovementManager.IsClassMovementAllowed && Me.Shapeshift == ShapeshiftForm.Bear && (Me.CurrentTarget.Distance + Me.CurrentTarget.CombatReach).Between(10, 25)),
                    new Action(ret => StopMoving.Clear()),
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