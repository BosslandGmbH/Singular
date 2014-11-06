#region

using System;
using System.Collections.Generic;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.CommonBot;
using Singular.Managers;
using CommonBehaviors.Actions;
using System.Drawing;
using Styx.CommonBot.POI;

#endregion

namespace Singular.ClassSpecific.Druid
{
    public class Common
    {
        public static ShapeshiftForm WantedDruidForm { get; set; }
        private static DruidSettings DruidSettings { get { return SingularSettings.Instance.Druid(); } }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        public static bool HasTalent(DruidTalents tal) { return TalentManager.IsSelected((int)tal); }

        public const WoWSpec DruidAllSpecs = (WoWSpec)int.MaxValue;

        [Behavior(BehaviorType.Initialize, WoWClass.Druid)]
        public static Composite CreateDruidInitialize()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal || SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
            {
                if ( TalentManager.CurrentSpec == WoWSpec.DruidBalance || TalentManager.CurrentSpec == WoWSpec.DruidRestoration)
                {
                    Kite.CreateKitingBehavior(null, null, null);
                }
            }

            return null;
        }


        #region PreCombat Buffs

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid)]
        public static Composite CreateDruidPreCombatBuff()
        {
            // Cast motw if player doesn't have it or if in instance/bg, out of combat and 'Buff raid with Motw' is true or if in instance and in combat and both CatRaidRebuff and 'Buff raid with Motw' are true
            return new PrioritySelector(

                PartyBuff.BuffGroup( "Mark of the Wild", ret => !Me.HasAura("Prowl") && !Me.Combat ),
                Spell.BuffSelf( "Mark of the Wild", ret => !Me.HasAura("Prowl") && !Me.IsInGroup() )

                /*   This logic needs work. 
                new Decorator(
                    ret =>
                    !Me.HasAura("Bear Form") &&
                    DruidSettings.PvPStealth && (Battlegrounds.IsInsideBattleground 
                    || Me.CurrentMap.IsArena) &&
                    !Me.Mounted && !Me.HasAura("Travel Form"),
                    Spell.BuffSelf("Cat Form")
                    ),
                new Decorator(
                    ret =>
                    Me.HasAura("Cat Form") &&
                    (DruidSettings.PvPStealth && (Battlegrounds.IsInsideBattleground 
                    || Me.CurrentMap.IsArena)),
                    Spell.BuffSelf("Prowl")
                    )*/
                );
        }

        #endregion

        [Behavior(BehaviorType.LossOfControl, WoWClass.Druid)]
        public static Composite CreateDruidLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    new Sequence(
                        Spell.BuffSelf("Barkskin"),
                        new Action( r => Logger.Write( Color.LightCoral, "Loss of Control - BARKSKIN!!!!"))
                        )
                    )
                );
        }
        

        #region Combat Buffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, DruidAllSpecs, WoWContext.Normal)]
        public static Composite CreateDruidCombatBuffsNormal()
        {
            return new Decorator(
                req => !Unit.IsTrivial(Me.CurrentTarget),
                new PrioritySelector(
                    Spell.Cast("Barkskin", ctx => Me, ret => Me.HealthPercent < DruidSettings.Barkskin || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),
                    Spell.Cast("Disorenting Roar", ctx => Me, ret => Me.HealthPercent < 40 || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),

                    // will hibernate only if can cast in form, or already left form for some other reason
                    Spell.Buff("Hibernate",
                        ctx => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(
                            u => (u.IsBeast || u.IsDragon)
                                && (Me.HasAura("Predatory Swiftness") || (!u.IsMoving && Me.Shapeshift == ShapeshiftForm.Normal))
                                && (!Me.GotTarget || Me.CurrentTarget.Location.Distance(u.Location) > 10)
                                && Me.CurrentTargetGuid != u.Guid
                                && !u.HasAnyAura("Hibernate", "Cyclone", "Entangling Roots")
                                )
                            ),

                    // will root only if can cast in form, or already left form for some other reason
                    Spell.Buff("Entangling Roots",
                        ctx => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(
                                u => (Me.HasAura("Predatory Swiftness") || Me.Shapeshift == ShapeshiftForm.Normal || Me.Shapeshift == ShapeshiftForm.Moonkin)
                                    && Me.CurrentTargetGuid != u.Guid
                                    && u.SpellDistance() > 15
                                    && !u.HasAnyAura("Hibernate", "Cyclone", "Entangling Roots", "Sunfire", "Moonfire")
                                ),
                        req => !Me.HasAura("Starfall")
                        ),

                    // combat buffs - make sure we have target and in range and other checks
                    // ... to avoid wastine cooldowns
                    new Decorator(
                        ret => Me.GotTarget
                            && (Me.CurrentTarget.IsPlayer || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3)
                            && Me.SpellDistance(Me.CurrentTarget) < ((TalentManager.CurrentSpec == WoWSpec.DruidFeral || TalentManager.CurrentSpec == WoWSpec.DruidGuardian) ? 8 : 40)
                            && Me.CurrentTarget.InLineOfSight
                            && Me.IsSafelyFacing(Me.CurrentTarget),
                        new PrioritySelector(
                            Spell.BuffSelf("Celestial Alignment", ret => Spell.GetSpellCooldown("Celestial Alignment") == TimeSpan.Zero && PartyBuff.WeHaveBloodlust),

                            Spell.OffGCD(Spell.Cast("Force of Nature", req => TalentManager.CurrentSpec != WoWSpec.DruidRestoration && Me.CurrentTarget.TimeToDeath() > 8)),

                    // to do:  time ICoE at start of eclipse
                            Spell.BuffSelf("Incarnation: Chosen of Elune"),
                            Spell.BuffSelf("Nature's Vigil")
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Instances | WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.Instances | WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidRestoration, WoWContext.Instances | WoWContext.Battlegrounds)]
        public static Composite CreateDruidCombatBuffsInstance()
        {
            return new PrioritySelector(

                CreateRebirthBehavior(ctx => Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead) ?? Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead)),

                Spell.Cast("Barkskin", ctx => Me, ret => Me.HealthPercent < DruidSettings.Barkskin || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),
                Spell.Cast("Disorenting Roar", ctx => Me, ret => Me.HealthPercent < 40 || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),

                // combat buffs - make sure we have target and in range and other checks
                // ... to avoid wastine cooldowns
                new Decorator(
                    ret => Me.GotTarget 
                        && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss())
                        && Me.SpellDistance( Me.CurrentTarget) < ((TalentManager.CurrentSpec == WoWSpec.DruidFeral || TalentManager.CurrentSpec == WoWSpec.DruidGuardian) ? 8 : 40)
                        && Me.CurrentTarget.InLineOfSight 
                        && Me.IsSafelyFacing(Me.CurrentTarget),
                    new PrioritySelector(
                        Spell.BuffSelf("Celestial Alignment", ret => Spell.GetSpellCooldown("Celestial Alignment") == TimeSpan.Zero && PartyBuff.WeHaveBloodlust),
                        new Sequence(
                            Spell.OffGCD(Spell.Cast("Force of Nature", req => TalentManager.CurrentSpec != WoWSpec.DruidRestoration && Me.CurrentTarget.TimeToDeath() > 8)),
                            new ActionAlwaysFail()
                            ),
                        // to do:  time ICoE at start of eclipse
                        Spell.BuffSelf("Incarnation: Chosen of Elune"),
                        Spell.BuffSelf("Nature's Vigil")
                        )
                    )
                );
        }

/*
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Instances, 1)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral , WoWContext.Instances, 1)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian , WoWContext.Instances, 1)]
        public static Composite CreateNonRestoDruidInstanceCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    new Decorator(
                        ret => HasTalent(DruidTalents.DreamOfCenarius) && !Me.HasAura("Dream of Cenarius"),
                        new PrioritySelector(
                            Spell.Heal("Healing Touch", ret => Me.ActiveAuras.ContainsKey("Predatory Swiftness")),
                            CreateNaturesSwiftnessHeal(on => GetBestHealTarget())
                            )
                        )
                    )
                );
        }
*/
        #endregion

        #region Heal

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Normal)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.Normal)]
        public static Composite CreateDruidNonRestoHealNormal()
        {
            return new PrioritySelector(

                // defensive check first
                Spell.BuffSelf("Survival Instincts", ret => TalentManager.CurrentSpec == WoWSpec.DruidFeral && Me.HealthPercent < DruidSettings.SurvivalInstinctsHealth),

                // keep rejuv up 
                Spell.Cast("Rejuvenation", on => Me,
                    ret =>
                    {
                        if (!Me.HasAuraExpired("Rejuvenation", 1))
                            return false;
                        if (TalentManager.CurrentSpec == WoWSpec.DruidGuardian && Me.HasAura("Heart of the Wild") && Me.HealthPercent < 95)
                            return true;
                        return !Group.MeIsTank && Me.PredictedHealthPercent(includeMyHeals: true) < DruidSettings.SelfRejuvenationHealth;
                    }),

                Spell.Cast( "Healing Touch", on => 
                    {
                        WoWUnit target = null;
                        if (Me.HasAura("Predatory Swiftness"))
                        {
                            // heal self if needed
                            if (Me.HealthPercent < DruidSettings.PredSwiftnessHealingTouchHealth)
                                target = Me;
                            // already checked self, so skip group searches
                            else if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                                target = null;  
                            // heal others if needed
                            else if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || (Me.GotTarget && Me.CurrentTarget.IsPlayer))
                                target = Unit.GroupMembers.Where(p => p.IsAlive && p.PredictedHealthPercent() < DruidSettings.PredSwiftnessPvpHeal && p.DistanceSqr < 40 * 40).FirstOrDefault();
                            // heal anyone if buff about to expire
                            else if (Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalMilliseconds.Between(500, 2000))
                                target = Unit.GroupMembers.Where(p => p.IsAlive && p.DistanceSqr < 40 * 40 && p.HealthPercent < 30).OrderBy(k => k.PredictedHealthPercent()).FirstOrDefault();

                            if (target != null)
                            {
                                Logger.WriteDebug("PredSwift Heal @ actual:{0:F1}% predict:{1:F1}% and moving:{2} in form:{3}", target.HealthPercent, target.PredictedHealthPercent(includeMyHeals: true), target.IsMoving, target.Shapeshift);
                            }
                        }
                        return target;
                    }) ,

                Spell.Cast("Renewal", on => Me, ret => Me.HealthPercent < DruidSettings.SelfRenewalHealth),
                Spell.BuffSelf("Cenarion Ward", ret => Me.HealthPercent < DruidSettings.SelfCenarionWardHealth),

                Spell.Cast("Disorienting Roar", ret => Me.HealthPercent <= DruidSettings.DisorientingRoarHealth && DruidSettings.DisorientingRoarCount <= Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro || (u.Combat && u.IsTargetingMeOrPet))),

                // heal out of form at this point (try to Barkskin at least)
                new Throttle(Spell.BuffSelf("Barkskin", ret => Me.HealthPercent < DruidSettings.Barkskin)),

                // for a lowbie Feral or a Bear not serving as Tank in a group
                new Decorator(
                    ret => Me.HealthPercent < DruidSettings.SelfHealingTouchHealth && !SpellManager.HasSpell("Predatory Swiftness") && !Group.MeIsTank && SingularRoutine.CurrentWoWContext != WoWContext.Instances,
                    new PrioritySelector(
                        Spell.Cast("Rejuvenation", on => Me, ret => Me.HasAuraExpired("Rejuvenation", 1)),
                        Spell.Cast("Healing Touch", on => Me)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Instances )]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.Instances)]
        public static Composite CreateDruidNonRestoHealInstances()
        {
            return new PrioritySelector(

                // defensive check first
                Spell.BuffSelf("Survival Instincts", ret => TalentManager.CurrentSpec == WoWSpec.DruidFeral && Me.HealthPercent < DruidSettings.SurvivalInstinctsHealth),

                Spell.Cast("Renewal", on => Me, ret => Me.HealthPercent < DruidSettings.SelfRenewalHealth),
                Spell.BuffSelf("Cenarion Ward", ret => Me.HealthPercent < DruidSettings.SelfCenarionWardHealth),

                // heal out of form at this point (try to Barkskin at least)
                new Throttle(Spell.BuffSelf("Barkskin", ret => Me.HealthPercent < DruidSettings.Barkskin)),

                new Decorator(
                    req => !Group.AnyHealerNearby,
                    CreateDruidNonRestoHealNormal()
                    )
                );
        }

        public static Composite CreateNaturesSwiftnessHeal(SimpleBooleanDelegate requirements = null)
        {
            return CreateNaturesSwiftnessHeal(on => Me, requirements);
        }

        public static Composite CreateNaturesSwiftnessHeal(UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements = null)
        {
            return new Decorator(
                ret => onUnit != null && onUnit(ret) != null && requirements != null && requirements(ret),
                new Sequence(
                    Spell.BuffSelf("Nature's Swiftness"),
                    new Wait(TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Nature's Swiftness"), new ActionAlwaysSucceed()),
                    Spell.Cast("Healing Touch", ret => false, onUnit, req => true)
                    )
                );
        }

        public static WoWUnit GetBestHealTarget()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal || Me.HealthPercent < 40)
                return Me;

            return Unit.NearbyFriendlyPlayers.Where(p=>p.IsAlive).OrderBy(k=>k.PredictedHealthPercent()).FirstOrDefault();
        }

        #endregion

        #region Rest

        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidBalance)]
        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidFeral)]
        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite CreateNonRestoDruidRest()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => !Me.HasAura("Drink") && !Me.HasAura("Food")
                        && Me.PredictedHealthPercent(includeMyHeals: true) < (Me.Shapeshift == ShapeshiftForm.Normal ? 85 : SingularSettings.Instance.MinHealth)
                        && ((Me.HasAuraExpired("Rejuvenation", 1) && Spell.CanCastHack("Rejuvenation", Me)) || Spell.CanCastHack("Healing Touch", Me)),
                    new PrioritySelector(
                        Movement.CreateEnsureMovementStoppedBehavior( reason:"to heal"),
                        new Action(r => { Logger.WriteDebug("Rest Heal @ actual:{0:F1}% predict:{1:F1}% and moving:{2} in form:{3}", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving, Me.Shapeshift ); return RunStatus.Failure; }),
                        Spell.BuffSelf("Rejuvenation", req => !SpellManager.HasSpell("Healing Touch")),
                        Spell.Cast("Healing Touch",
                            mov => true,
                            on => Me,
                            req => true,
                            cancel => Me.HealthPercent > 92)
                        )
                    ),

                Rest.CreateDefaultRestBehaviour(null, "Revive"),
                CreateDruidMovementBuff()
                );
        }

        #endregion

        public static Composite CastHurricaneBehavior(UnitSelectionDelegate onUnit, SimpleBooleanDelegate cancel = null)
        {
            if (cancel == null)
            {
                cancel = u =>
                    {
                        if (Me.HealthPercent < 30)
                        {
                            Logger.Write(LogColor.Cancel, "/cancel Hurricane since my health at {0:F1}%", Me.HealthPercent);
                            return true;
                        }
                        return false;
                    };
            }

            return new Sequence(
                ctx => onUnit(ctx),

                Spell.CastOnGround("Hurricane", on => (WoWUnit)on, req => Me.HealthPercent > 40 && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3, false),

                new Wait(
                    TimeSpan.FromMilliseconds(1000),
                    until => Spell.IsCastingOrChannelling() && Unit.NearbyUnfriendlyUnits.Any(u => u.HasMyAura("Hurricane")),
                    new ActionAlwaysSucceed()
                    ),
                new Wait(
                    TimeSpan.FromSeconds(10),
                    until =>
                    {
                        if (!Spell.IsCastingOrChannelling())
                        {
                            Logger.WriteDiagnostic("Hurricane: cast complete or interrupted");
                            return true;
                        }
                        int cnt = Unit.NearbyUnfriendlyUnits.Count(u => u.HasMyAura("Hurricane"));
                        if (cnt < 3)
                        {
                            Logger.Write( LogColor.Cancel, "/cancel Hurricane since only {0} targets effected", cnt);
                            return true;
                        }
                        if (cancel(until))
                        {
                            // message should be output by cancel delegate
                            SpellManager.StopCasting();
                            return true;
                        }

                        return false;
                    },
                    new ActionAlwaysSucceed()
                    ),
                new DecoratorContinue(
                    req => Spell.IsChannelling(),
                    new Action(r => SpellManager.StopCasting())
                    ),
                new WaitContinue(
                    TimeSpan.FromMilliseconds(500),
                    until => !Spell.IsChannelling(),
                    new ActionAlwaysSucceed()
                    )
                )
            ;
        }


        internal static Composite CreateProwlBehavior(SimpleBooleanDelegate req = null)
        {
            return new Sequence(
                Spell.BuffSelf("Prowl", ret => Me.Shapeshift == ShapeshiftForm.Cat && (req == null || req(ret))),
                new Wait(TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Prowl"), new ActionAlwaysSucceed())
                );
        }

        public static Composite CreateRebirthBehavior(UnitSelectionDelegate onUnit)
        {
            if (TalentManager.CurrentSpec == WoWSpec.DruidGuardian)
                return Helpers.Common.CreateCombatRezBehavior("Rebirth", on => ((WoWUnit)on).SpellDistance() < 40 && ((WoWUnit)on).InLineOfSpellSight, requirements => true);

            return Helpers.Common.CreateCombatRezBehavior("Rebirth", filter => true, reqd => !Me.HasAnyAura("Nature's Swiftness", "Predatory Swiftness"));
        }

        public static Composite CreateFaerieFireBehavior(UnitSelectionDelegate onUnit = null, SimpleBooleanDelegate Required = null)
        {
            if (onUnit == null)
                onUnit = on => Me.CurrentTarget;

            if (Required == null)
                Required = req => true;

            // Fairie Fire has a 1.5 sec GCD, Faerie Swarm 0.0.  Handle both here
            return new ThrottlePasses( 1, TimeSpan.FromMilliseconds(500),
                new Sequence(
                    new PrioritySelector(
                        Spell.Buff("Faerie Swarm", on => onUnit(on), ret => Required(ret)),
                        Spell.Buff("Faerie Fire", on => onUnit(on), ret => Required(ret))
                        ),
                    // fail if used Faerie Swarm since off GCD
                    new DecoratorContinue( req => HasTalent(DruidTalents.FaerieSwarm), new ActionAlwaysFail())
                    )
                );
        }

        private static bool IsBotPoiWithinMovementBuffRange()
        {
            int minDistKillPoi = 10;
            int minDistOtherPoi = 10;
            int maxDist = Styx.Helpers.CharacterSettings.Instance.MountDistance;

            if (Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull || Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.PullBuffs)
                maxDist = Math.Max( 100, Styx.Helpers.CharacterSettings.Instance.MountDistance);

            if (!Me.IsMelee())
                minDistKillPoi += 40;

            if (BotPoi.Current == null || BotPoi.Current.Type == PoiType.None)
                return false;

            double dist = -1;
            if (BotPoi.Current.Type != PoiType.Kill || BotPoi.Current.AsObject.ToUnit() == null)
            {
                dist = Me.Location.Distance(BotPoi.Current.Location);
                if ( dist < minDistOtherPoi)
                    return false;
            }
            else 
            {
                WoWUnit unit = BotPoi.Current.AsObject.ToUnit();
                if (unit.SpellDistance() < minDistKillPoi)
                    return false;
            }

            // always speedbuff if indoors and cannot mount
            if (Me.IsIndoors && !Mount.CanMount())
                return true;

            // always speedbuff if riding not trained yet
            if (Me.GetSkill(SkillLine.Riding).CurrentValue == 0)
                return true;

            // calc distance if we havent already
            if (dist == -1)
                dist = Me.Location.Distance(BotPoi.Current.Location);

            // speedbuff if dist within maxdist
            if (dist <= maxDist)
                return true;

            // otherwise no speedbuff wanted
            return false;
        }

        public static Composite CreateDruidMovementBuff()
        {

            return new Throttle( 5,
                new Decorator(
                    req =>  !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown()
                        && MovementManager.IsClassMovementAllowed
                        && SingularRoutine.CurrentWoWContext != WoWContext.Instances
                        && Me.IsMoving 
                        && Me.IsAlive
                        && !Me.OnTaxi 
                        && !Me.InVehicle 
                        && !Me.IsOnTransport
                        && !Utilities.EventHandlers.IsShapeshiftSuppressed
                        && BotPoi.Current != null
                        && BotPoi.Current.Type != PoiType.None
                        && BotPoi.Current.Type != PoiType.Hotspot
                        && !Me.IsAboveTheGround()
                        ,
                    new Sequence(
                        new PrioritySelector(
                            new Decorator(
                                ret => DruidSettings.UseTravelForm
                                    && !Me.Mounted
                                    && !Me.IsSwimming
                                    && !Me.HasAnyShapeshift(ShapeshiftForm.Travel, ShapeshiftForm.FlightForm, ShapeshiftForm.EpicFlightForm)
                                    && SpellManager.HasSpell("Cat Form")
                                    && IsBotPoiWithinMovementBuffRange(),
                                new Sequence(
                                    new Action(r => Logger.WriteDebug("DruidMoveBuff: poitype={0} poidist={1:F1} indoors={2} canmount={3} riding={4} form={5}",
                                        BotPoi.Current.Type,
                                        BotPoi.Current.Location.Distance(Me.Location),
                                        Me.IsIndoors.ToYN(),
                                        Mount.CanMount().ToYN(),
                                        Me.GetSkill(SkillLine.Riding).CurrentValue,
                                        Me.Shapeshift.ToString()
                                        )),
                                    new PrioritySelector(
                                        Common.CastForm("Travel Form", 
                                            req => {
                                                if (!Me.IsOutdoors || BotPoi.Current.Type == PoiType.Kill)
                                                    return false;
                                                WoWUnit possibleAggro = Unit.UnfriendlyUnits(40).FirstOrDefault(u => u.IsHostile && (!u.Combat || u.CurrentTargetGuid != Me.Guid));
                                                if (possibleAggro != null && !Me.IsInsideSanctuary)
                                                {
                                                    Logger.WriteDiagnostic("DruidMoveBuff: suppressing Travel Form since hostile {0} is {1:F1} yds away", possibleAggro.SafeName(), possibleAggro.SpellDistance());
                                                    return false;
                                                }
                                                return true;
                                            }),
                                        Common.CastForm("Cat Form")
                                        )
                                    )
                                ),
                            new Decorator( 
                                req => AllowAquaticForm 
                                    && BotPoi.Current.Location.Distance(Me.Location) >= 10
                                    && Me.Shapeshift != ShapeshiftForm.Aqua
                                    && Spell.CanCastHack("Aquatic Form", Me, false), 
                                Common.CastForm( "Aquatic Form")
                                )
                            ),

                            Helpers.Common.CreateWaitForLagDuration()
                        )
                    )
                );
        }

        public static bool AllowAquaticForm
        {
            get
            {
                const int ABYSSAL_SEAHORSE = 75207;
                const int SUBDUED_SEAHORSE = 98718;
                const int SEA_TURTLE = 64731;

                if (!DruidSettings.UseAquaticForm)
                    return false;

                if (!Me.IsSwimming)
                    return false;

                if (Me.Shapeshift != ShapeshiftForm.Aqua)
                {
                    if (Me.Combat)
                    return false;

                    if (!SpellManager.HasSpell("Aquatic Form"))
                        return false;

                    MirrorTimerInfo breath = StyxWoW.Me.GetMirrorTimerInfo(MirrorTimerType.Breath);
                    if (!breath.IsVisible)
                    {
                        if (Me.Mounted && (Me.MountDisplayId == ABYSSAL_SEAHORSE || Me.MountDisplayId == SUBDUED_SEAHORSE || Me.MountDisplayId == SEA_TURTLE))
                            return false;
                    }

                    Logger.WriteDebug( "DruidSwimBuff: breath={0} canmount={1} mounted={2} mountdispid={3}",
                        breath.IsVisible.ToYN(),
                        Mount.CanMount().ToYN(),
                        Me.Mounted.ToYN(),
                        Me.MountDisplayId
                        );
                }

                return true;
            }
        }

        public static Composite CreateMoveBehindTargetWhileProwling()
        {
            return new Decorator(
                req => DruidSettings.MoveBehindTargets && Me.HasAura("Prowl"),
                Movement.CreateMoveBehindTargetBehavior()
                );
        }
        
        public static Composite CastForm( string spellName, SimpleBooleanDelegate requirements = null)
        {
            return new Decorator(
                req => !Me.HasAura(spellName) && (requirements == null || requirements(req)),
                new Sequence(
                    new Action( r => {
                        WoWAura aura = Me.GetAllAuras().FirstOrDefault( a => a.Spell.Name.Substring(a.Name.Length - 5).Equals(" Form"));
                        Logger.WriteDiagnostic( "CastForm: changing to form='{0}',  current='{1}',  hb-says='{2}'", 
                            spellName, aura == null ? "-none-" : aura.Name, Me.Shapeshift.ToString()
                            );
                        }),
                    Spell.BuffSelf(spellName),
                    new PrioritySelector(
                        new Wait(TimeSpan.FromMilliseconds(500), until => Me.HasAura(spellName), new Action( r => Logger.WriteDiagnostic("CastForm: the form '{0}' is now active!", spellName))),
                        new Action( r => Logger.WriteDiagnostic("CastForm: error - did not yet enter form '{0}'", spellName))
                        )
                    )
                );
        }

#if NOT_IN_USE
        public static Composite CreateEscapeFromCc()
        {
            return
                new PrioritySelector(
                    Spell.Cast("Dash",
                               ret =>
                               DruidSettings.PvPRooted &&
                               Me.HasAuraWithMechanic(WoWSpellMechanic.Rooted) &&
                               Me.Shapeshift == ShapeshiftForm.Cat),
                    new Decorator(
                        ret =>
                        (DruidSettings.PvPRooted &&
                         Me.HasAuraWithMechanic(WoWSpellMechanic.Rooted) &&
                         Me.Shapeshift == ShapeshiftForm.Cat && SpellManager.HasSpell("Dash") &&
                         SpellManager.Spells["Dash"].Cooldown),
                        new Sequence(
                            new Action(ret => Spell.CastPrimative(WoWSpell.FromId(77764))
                                )
                            )),
                    new Decorator(
                        ret =>
                        (DruidSettings.PvPSnared &&
                         Me.HasAuraWithMechanic(WoWSpellMechanic.Snared) &&
                         !Me.ActiveAuras.ContainsKey("Crippling Poison") &&
                         Me.Shapeshift == ShapeshiftForm.Cat),
                        new Sequence(
                            new Action(ret => Lua.DoString("RunMacroText(\"/Cast !Cat Form\")")
                                )
                            )
                        ),
                    new Decorator(
                        ret =>
                        (DruidSettings.PvPSnared &&
                         Me.HasAuraWithMechanic(WoWSpellMechanic.Snared) &&
                         !Me.ActiveAuras.ContainsKey("Crippling Poison") &&
                         Me.Shapeshift == ShapeshiftForm.Bear),
                        new Sequence(
                            new Action(ret => Lua.DoString("RunMacroText(\"/Cast !Bear Form\")")
                                )
                            )
                        )
                    );
        }

        public static Composite CreateCycloneAdd()
        {
            return
                new PrioritySelector(
                    ctx =>
                    Unit.NearbyUnfriendlyUnits.OrderByDescending(u => u.CurrentHealth).FirstOrDefault(IsViableForCyclone),
                    new Decorator(
                        ret =>
                        ret != null && DruidSettings.PvPccAdd &&
                        Me.ActiveAuras.ContainsKey("Predatory Swiftness") &&
                        Unit.NearbyUnfriendlyUnits.All(u => !u.HasMyAura("Polymorph")),
                        new PrioritySelector(
                            Spell.Buff("Cyclone", ret => (WoWUnit) ret))));
        }

        private static bool IsViableForCyclone(WoWUnit unit)
        {
            if (unit.IsCrowdControlled())
                return false;

            if (unit.CreatureType != WoWCreatureType.Beast && unit.CreatureType != WoWCreatureType.Humanoid)
                return false;

            if (Me.CurrentTarget != null && Me.CurrentTarget == unit)
                return false;

            if (!unit.Combat)
                return false;

            if (!unit.IsTargetingMeOrPet && !unit.IsTargetingMyPartyMember)
                return false;

            if (Me.GroupInfo.IsInParty &&
                Me.PartyMembers.Any(p => p.CurrentTarget != null && p.CurrentTarget == unit))
                return false;

            return true;
        }
#endif

    }


    public enum DruidTalents
    {
#if PRE_WOD
        FelineSwiftness = 1,
        DispacerBeast,
        WildCharge,
        YserasGift,
        Renewal,
        CenarionWard,
        FaerieSwarm,
        MassEntanglement,
        Typhoon,
        SoulOfTheForest,
        Incarnation,
        ForceOfNature,
        DisorientingRoar,
        UrsolsVortex,
        MightyBash,
        HeartOfTheWild,
        DreamOfCenarius,
        NaturesVigil
#else

        FelineSwiftness = 1,
        DisplacerBeast,
        WildCharge,

        YserasGift,
        Renewal,
        CenarionWard,

        FaerieSwarm,
        MassEntanglement,
        Typhoon,

        SoulOfTheForest,
        Incarnation,
        ForceOfNature,

        IncapacitatingRoar,
        UrsolsVortex,
        MightyBash,

        HeartOfTheWild,
        DreamOfCenarius,
        NaturesVigil,

        Euphoria,
        LunarInspiration = Euphoria,
        GuardianOfElune = Euphoria,
        MomentOfClarity = Euphoria,

        StellarFlare,
        Bloodtalons = StellarFlare,
        Pulverize = StellarFlare,
        Germination = StellarFlare,

        BalanceOfPower,
        ClawsOfShirvallah = BalanceOfPower,
        BristlingFur = BalanceOfPower,
        RampantGrowth = BalanceOfPower

#endif
    }
}