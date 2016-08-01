#region

using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using Styx.WoWInternals;
using System.Drawing;
using Styx.CommonBot.POI;
using Styx.Helpers;

#endregion

namespace Singular.ClassSpecific.Druid
{
    public class Feral
    {
        private static LocalPlayer Me => StyxWoW.Me;
        private static DruidSettings DruidSettings => SingularSettings.Instance.Druid();
        private static long EnergyDecifit => Me.MaxEnergy - Me.CurrentEnergy;
        private static double RakeAndThrashRefresh => Common.HasTalent(DruidTalents.JaggedWounds) ? 3 : 4.2;
        private static double RipRefresh => Common.HasTalent(DruidTalents.JaggedWounds) ? 4.8 : 7.2;

        #region Common

        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidFeral, priority: 1)]
        public static Composite CreateFeralDruidRest()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => !Rest.IsEatingOrDrinking
                        && Me.HasActiveAura("Predatory Swiftness")
                        && (Me.PredictedHealthPercent(includeMyHeals: true) < 95),
                    new PrioritySelector(
                        new Action(r => { Logger.WriteDebug("Druid Rest Swift Heal @ {0:F1}% and moving:{1} in form:{2}", Me.HealthPercent, Me.IsMoving, Me.Shapeshift); return RunStatus.Failure; }),
                        Spell.Cast("Healing Touch",
                            mov => true,
                            on => Me,
                            req => true,
                            cancel => Me.HealthPercent > 95 )
                        )
                    ),

                Common.CreateProwlBehavior(ret => Rest.IsEatingOrDrinking)

                // remainder of rest behavior in common.cs CreateNonRestoDruidRest()
                );
        }


        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidFeral)]
        public static Composite CreateFeralNormalPull()
        {
            return new PrioritySelector(
                CreateFeralDiagnosticOutputBehavior( "Pull" ),
                Helpers.Common.EnsureReadyToAttackFromMelee(req => !Me.HasAura("Prowl")),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Spell.Buff("Moonfire",
                            req => Me.GotTarget() && Me.CurrentTarget.IsTrivial() &&
                                    (Me.Shapeshift != ShapeshiftForm.Cat || (Common.HasTalent(DruidTalents.LunarInspiration) && !Me.HasAura("Prowl")))),

                        Common.CreateProwlBehavior(
                            req =>
                            {
                                if (!Me.GotTarget() || Me.CurrentTarget.IsTrivial())
                                    return false;

                                float dist = Me.CurrentTarget.SpellDistance();
                                if (dist < 29 && !Spell.IsSpellOnCooldown("Wild Charge"))
                                    return true;
                                if (dist < 9)
                                    return true;
                                if (dist < Math.Max( 18, 4 + Me.GetAggroRange(Me.CurrentTarget)) && Me.GetReactionTowards(Me.CurrentTarget) < WoWUnitReaction.Neutral)
                                    return true;
                                return false;
                            }
                            ),

                        Common.CastForm(ShapeshiftForm.Cat, req => Me.Shapeshift != ShapeshiftForm.Cat && !Utilities.EventHandlers.IsShapeshiftSuppressed),

                        CreateFeralWildChargeBehavior(),

                        // only Dash if we dont have WC or WC was cast more than 2 seconds ago

                        Spell.BuffSelf("Dash",
                            ret => MovementManager.IsClassMovementAllowed
                                && Me.IsMoving
                                && Me.HasAura("Prowl")
                                && Me.CurrentTarget.Distance > 17
                                && Spell.GetSpellCooldown("Wild Charge", 999).TotalSeconds > 1
                            ),

                        Spell.Buff("Rake", req => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && Me.CurrentTarget.Distance < (Me.CurrentTarget.CombatReach + 2))
                        )
                    ),

                // Move to Melee, going behind target if prowling
                Common.CreateMoveBehindTargetWhileProwling(),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Composite CreateFeralFaerieFireBehavior()
        {
            return Common.CreateFaerieFireBehavior(
                                                on => Me.CurrentTarget,
                                                req => Me.GotTarget()
                                                    && Me.CurrentTarget.IsPlayer
                                                    && (Me.CurrentTarget.Class == WoWClass.Rogue || Me.CurrentTarget.Shapeshift == ShapeshiftForm.Cat)
                                                    && !Me.CurrentTarget.HasAnyAura("Faerie Fire", "Faerie Swarm")
                                                    && Me.CurrentTarget.SpellDistance() < 35
                                                    && Me.IsSafelyFacing(Me.CurrentTarget)
                                                    && Me.CurrentTarget.InLineOfSpellSight
                                                );
        }

        private static Throttle CreateFeralWildChargeBehavior()
        {
            // save WC for later if Dash is active. also throttle to deal with possible pathing issues
            return new Throttle(7,
                new Sequence(
                    Spell.CastHack("Wild Charge", ret => MovementManager.IsClassMovementAllowed && !Me.HasActiveAura("Dash") && (Me.CurrentTarget.Distance + Me.CurrentTarget.CombatReach).Between(10, 25)),
                    new Wait(1, until => !Me.GotTarget() || Me.CurrentTarget.IsWithinMeleeRange, new ActionAlwaysSucceed())
                    )
                );
        }

        #endregion

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral)]
        public static Composite CreateFeralNormalPreCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown(),
                new PrioritySelector(

                    // prowl will cast form if used, so check for it first
                    Common.CreateProwlBehavior(
                        ret => DruidSettings.Prowl == ProwlMode.Always
                            && (Me.Shapeshift == ShapeshiftForm.Cat || !Utilities.EventHandlers.IsShapeshiftSuppressed)
                            && !Me.HasAnyShapeshift(ShapeshiftForm.Aqua, ShapeshiftForm.FlightForm, ShapeshiftForm.EpicFlightForm)
                            && BotPoi.Current.Type != PoiType.Loot
                            && BotPoi.Current.Type != PoiType.Skin
                            && !ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.IsDead && ((CharacterSettings.Instance.LootMobs && u.CanLoot && u.Lootable) || (CharacterSettings.Instance.SkinMobs && u.Skinnable && u.CanSkin)) && u.Distance < CharacterSettings.Instance.LootRadius)
                        ),

                    // cast cat form
                    // since this check comes while not in combat (so will be doing other things like Questing) need to add some checks:
                    // - only if Moving
                    // - only if Not Swimming
                    // - only if Not Flying
                    // - only if Not in one of the various forms for travel
                    // - only if No Recent Shapefhift Error (since form may have resulted from error in picking up Quest, completing Quest objectives, or turning in Quest)
                    new Throttle(
                        10,
                        Common.CastForm(
                            ShapeshiftForm.Cat,
                            req => !Utilities.EventHandlers.IsShapeshiftSuppressed
                                && Me.IsMoving
                                && !Me.IsFlying && !Me.IsSwimming
                                && !Me.HasAnyShapeshift( ShapeshiftForm.Cat, ShapeshiftForm.Travel, ShapeshiftForm.Aqua, ShapeshiftForm.FlightForm, ShapeshiftForm.EpicFlightForm)
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All, 999)]
        public static Composite CreateFeralHeal()
        {
            return new PrioritySelector(
                CreateFeralDiagnosticOutputBehavior("Combat"),

                Spell.BuffSelf("Survival Instincts", req => Me.HealthPercent < DruidSettings.SurvivalInstinctsHealth),

                Spell.Cast(
                    "Healing Touch",
                    on =>
                    {
                        int pstime = (int) Me.GetAuraTimeLeft("Predatory Swiftness").TotalMilliseconds;
                        if (pstime < 150)
                            return null;

                        WoWUnit unit = null;
                        if (Common.HasTalent(DruidTalents.Bloodtalons) && (Me.ComboPoints >= 4 || pstime < 1650))
                        {
                            unit = Unit.GroupMembers
                                .Where( u => u.IsAlive && Spell.CanCastHack("Healing Touch", u))
                                .OrderBy( u => (int) u.HealthPercent )
                                .FirstOrDefault();

                            if (unit == null && Spell.CanCastHack("Healing Touch", Me))
                                unit = Me;

                            if (unit != null)
                                Logger.Write(LogColor.Hilite, "^Bloodtalons: buffing bleeds");

                            return unit;
                        }

                        // raids: don't waste a GCD on healing without a buff
                        if (SingularRoutine.CurrentHealContext == HealingContext.Raids)
                            return null;

                        // else: find lowest health unit
                        unit = HealerManager.NeedHealTargeting
                            ? HealerManager.FindHighestPriorityTarget()
                            : Unit.GroupMembers
                                .Where( u => u.IsAlive && Spell.CanCastHack("Healing Touch", u))
                                .OrderBy( u => (int) u.HealthPercent )
                                .FirstOrDefault();

                        if (unit != null)
                        {
                            if (unit.HealthPercent < DruidSettings.PredSwiftnessHealingTouchHealth)
                                return unit;
                            if (unit.HealthPercent < 90 && pstime < 1650)
                                return unit;
                            unit = null;
                        }

                        return unit;
                    }
                    )
                );
        }


        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All, 1)]
        public static Composite CreateFeralCombatBuffs()
        {
            return new PrioritySelector(

                Common.CastForm(ShapeshiftForm.Cat, req => Me.Shapeshift != ShapeshiftForm.Cat && !Utilities.EventHandlers.IsShapeshiftSuppressed)

                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral)]
        public static Composite CreateFeralNormalCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(req => !Me.HasAura("Prowl")),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        // updated time to death tracking values before we need them
                        new Action( ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; } ),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),
                        //Single target
                        CreateFeralFaerieFireBehavior(),

                        Spell.Cast("Elune's Guidance", ret => Me.ComboPoints <= 0 && Me.CurrentEnergy >= 50),
                        Spell.BuffSelf("Healing Touch", ret => Me.HasActiveAura("Predatory Swiftness") && Me.ComboPoints >= 4),
                        new Decorator(ret => Me.ComboPoints >= 5,
                            new PrioritySelector(
                                Spell.Cast("Ferocious Bite",
                                    on => Unit.UnfriendlyUnits(8).FirstOrDefault(
                                                u => (u.HealthPercent < 25 || Common.HasTalent(DruidTalents.Sabertooth)) && u.HasMyAura("Rip") ||
                                                        u.TimeToDeath(int.MaxValue) < 18 || u.GetAuraTimeLeft("Rip").TotalSeconds >= RipRefresh),
                                    ret => (!Common.HasTalent(DruidTalents.SavageRoar) || Me.GetAuraTimeLeft("Savage Roar").TotalSeconds > 12) && Me.CurrentEnergy >= 50),
                                Spell.Cast("Rip",
                                    on => Unit.UnfriendlyUnits(8).FirstOrDefault(
                                                u => u.HealthPercent >= 25 && u.TimeToDeath(int.MaxValue) > 18 && u.GetAuraTimeLeft("Rip").TotalSeconds < RipRefresh)),
                                Spell.Cast("Savage Roar"))),

                        Spell.Cast("Moonfire",
                            on => Unit.NearbyUnitsInCombatWithUsOrOurStuff.FirstOrDefault(u => u.GetAuraTimeLeft("Moonfire").TotalSeconds < 4.2),
                            ret => Common.HasTalent(DruidTalents.LunarInspiration)),
                        Spell.Cast("Tiger's Fury", ret => EnergyDecifit > 65),
                        Spell.Cast("Berserk", ret => Me.HasActiveAura("Tiger's Fury") && Me.CurrentTarget.IsStressful()),

                        Spell.Cast("Rake",
                            on => Unit.UnfriendlyUnits(8).OrderBy(u => u.GetAuraTimeLeft("Rake")).
                                        FirstOrDefault(
                                            u => Me.HasActiveAura("Bloodtalons") || u.GetAuraTimeLeft("Rake").TotalSeconds < RakeAndThrashRefresh)),
                        Spell.Cast("Thrash", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => u.GetAuraTimeLeft("Thrash").TotalSeconds < RakeAndThrashRefresh)),
                        Spell.Cast("Brutal Slash"),
                        Spell.Cast("Shred", ret => Unit.UnfriendlyUnitsNearTarget(8).Count() <= 1),
                        Spell.Cast("Swipe"),

                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed && Me.IsMoving && Me.CurrentTarget.Distance > (Me.CurrentTarget.IsPlayer ? 10 : 15),
                            new PrioritySelector(
                                CreateFeralWildChargeBehavior(),
                                Spell.BuffSelf("Dash", ret => MovementManager.IsClassMovementAllowed && Spell.GetSpellCooldown("Wild Charge", 0).TotalSeconds < 13 )
                                )
                            )

                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #region Diagnostics

        private static Composite CreateFeralDiagnosticOutputBehavior(string context = null)
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
                    log = string.Format(context + " h={0:F1}%/e={1:F1}%/m={2:F1}%, shp={3}, prwl={4}, mov={5}, savg={6}, tfury={7}, brsrk={8}, predswf={9}, omen={10}, pts={11}",
                        Me.HealthPercent,
                        Me.EnergyPercent,
                        Me.ManaPercent,
                        Me.Shapeshift.ToString().Length < 4 ? Me.Shapeshift.ToString() : Me.Shapeshift.ToString().Substring(0, 4),
                        Me.HasAura("Prowl").ToYN(),
                        Me.IsMoving.ToYN(),
                        (long)Me.GetAuraTimeLeft("Savage Roar", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Tiger's Fury", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Berserk", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Clearcasting", true).TotalMilliseconds,
                        Me.ComboPoints
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        log += string.Format(", th={0:F1}%, dist={1:F1}, inmelee={2}, face={3}, loss={4}, dead={5} secs, rake={6}, rip={7}, thrash={8}",
                            target.HealthPercent,
                            target.Distance,
                            target.IsWithinMeleeRange.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            target.InLineOfSpellSight.ToYN(),
                            target.TimeToDeath(),
                            (long)target.GetAuraTimeLeft("Rake", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Rip", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Thrash", true).TotalMilliseconds
                            );

                        if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                        {
                            log += string.Format(", refsvg={0}, refrip={1}",
                                Me.HasAuraExpired("Savage Roar", TalentManager.HasGlyph("Savagery") ? 1 : 6).ToYN(),
                                target.HasAuraExpired("Rip", 6).ToYN()
                                );
                        }
                    }

                    Logger.WriteDebug(Color.AntiqueWhite, log);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }
}