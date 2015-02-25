using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Common.Helpers;
using Styx.TreeSharp;
using System.Collections.Generic;
using Styx.CommonBot;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using CommonBehaviors.Actions;

namespace Singular.ClassSpecific.Monk
{
    public class Brewmaster
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MonkSettings MonkSettings { get { return SingularSettings.Instance.Monk(); } }
        public static bool HasTalent(MonkTalents tal) { return TalentManager.IsSelected((int)tal); }

        // ToDo  Summon Black Ox Statue, Chi Burst and 

        private static bool UseChiBrew
        {
            get
            {
                return TalentManager.IsSelected((int)MonkTalents.ChiBrew) && Me.CurrentChi == 0 && Me.CurrentTargetGuid.IsValid &&
                       (SingularRoutine.CurrentWoWContext == WoWContext.Instances && Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances);
            }
        }


        private static Composite CastDizzyingHaze( UnitSelectionDelegate onUnit = null)
        {
            const string DIZZYING_HAZE = "Dizzying Haze";

            if (onUnit == null)
            {
                // cast on those in combat with us that are moving away and don't have Dizzying Haze
                onUnit = uu => Unit.NearbyUnfriendlyUnits.FirstOrDefault(
                    u => (u.Aggro || u.Combat && u.IsTargetingUs()) 
                        && !Me.HasMyAura(DIZZYING_HAZE) 
                        && !Spell.DoubleCastContains(u, DIZZYING_HAZE) 
                        && (u.SpellDistance() > 10 || u.IsMovingAway()));
            }

            return new Throttle(
                TimeSpan.FromMilliseconds(2500),
                new PrioritySelector(
                    ctx => {
                        WoWUnit unit = onUnit(ctx);
                        if (unit.HasMyAura(DIZZYING_HAZE))
                            unit = null;
                        return unit;
                        },
                    new Sequence(
                        Spell.CastOnGround(DIZZYING_HAZE, on => on as WoWUnit, req => true, false),
                        new Action( r => Spell.UpdateDoubleCast(DIZZYING_HAZE, r as WoWUnit))
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        public static Composite CreateBrewmasterMonkPullNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),
                        CreateCloseDistanceBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        CastDizzyingHaze(),
                        Spell.Cast("Tiger Palm"),
                        Spell.Cast("Expel Harm", on => Common.BestExpelHarmTarget(), ret => Me.HealthPercent < 80 && Me.CurrentTarget.Distance < 10),
                        Spell.Cast("Jab")
                //Only roll to get to the mob quicker. 
                        )
                    )
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.All)]
        public static Composite CreateMonkPreCombatBuffs()
        {
            return new PrioritySelector(
                PartyBuff.BuffGroup("Legacy of the White Tiger")
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkBrewmaster, priority: 1)]
        public static Composite CreateBrewmasterMonkCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelfAndWait(sp => "Stance of the Sturdy Ox", req => !Me.GetAllAuras().Any(a => a.ApplyAuraType == WoWApplyAuraType.ModShapeshift && a.IsPassive && a.Name == "Stance of the Sturdy Ox")),

                new Decorator(
                    req => !Unit.IsTrivial( Me.CurrentTarget),
                    new PrioritySelector(
                        Spell.BuffSelf("Zen Meditation", req => Targeting.Instance.TargetList.Any( u => u.IsCasting && u.CurrentTargetGuid != Me.Guid && Me.GroupInfo.IsInCurrentRaid(u.CurrentTargetGuid) && u.SpellDistance(u.CurrentTarget) < 20)),
                        Spell.BuffSelf("Fortifying Brew", ctx => Me.HealthPercent <= MonkSettings.FortifyingBrewPct),
                        Spell.BuffSelf("Guard", req => Me.HealthPercent < MonkSettings.GuardHealthPct || Unit.UnfriendlyUnits(40).Count( u => u.CurrentTargetGuid == Me.Guid) >= MonkSettings.GuardMobCount ),
                        Spell.BuffSelf("Elusive Brew", ctx => MonkSettings.UseElusiveBrew && Me.HasAura("Elusive Brew", MonkSettings.ElusiveBrewMinumumCount)),
                        Spell.Cast("Chi Brew", ctx => UseChiBrew),
                        Spell.BuffSelf("Zen Sphere", ctx => HasTalent(MonkTalents.ZenSphere) && Me.HealthPercent < 90)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Instances, priority: 1)]
        public static Composite CreateBrewmasterMonkHeal()
        {
            return new PrioritySelector(
                // use chi wave as a heal.
                Spell.Cast(
                    "Chi Wave",
                    ctx =>
                    TalentManager.IsSelected((int)MonkTalents.ChiWave) &&
                    (Me.HealthPercent < MonkSettings.ChiWavePct ||
                     Me.RaidMembers.Count(m => m.DistanceSqr <= 20 * 20 && m.HealthPercent <= MonkSettings.ChiWavePct) >= 3))

                //Spell.Cast("Zen Sphere", ctx => TalentManager.IsSelected((int)MonkTalents.ZenSphere) && Me.RaidMembers.Count(m => m.DistanceSqr <= 20 * 20 && m.HealthPercent <= 70) >= 3)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateBrewmasterMonkNormalCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        // keep these auras up!
                        Spell.Cast("Tiger Palm", ret => Me.HasKnownAuraExpired("Tiger Power", 1)),
                        Spell.Cast("Blackout Kick", ctx => SpellManager.HasSpell("Brewmaster Training") && Me.HasKnownAuraExpired("Shuffle", 1)),

                        // AOE
                        new Decorator(
                            req => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() <= 8) >= 3,
                            new PrioritySelector(
                                ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => Me.IsSafelyFacing(u,150f)),

                                // cast heal in anticipation of damage
                                Spell.BuffSelf("Zen Sphere", ctx => HasTalent(MonkTalents.ZenSphere) && Me.HealthPercent < 90),

                                new Decorator( 
                                    req => req != null,
                                    new PrioritySelector(
                                        // throw debuff on them to hit themselves and slow
                                        CastDizzyingHaze(),

                                        // throw DoT on them
                                        Spell.Cast("Breath of Fire", 
                                            ctx => Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8)
                                                .Any(u => u.HasAura("Dizzying Haze") && !u.HasAura("Breath of Fire"))),

                                        // aoe stuns (one per 3 seconds at most)
                                        new Throttle(3,
                                            new PrioritySelector(
                                                Spell.Cast("Charging Ox Wave", ctx => HasTalent(MonkTalents.ChargingOxWave) && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits.Where(u => !u.IsStunned()), ClusterType.Cone, 30) >= (Me.HealthPercent > 50 ? 3 : 1)),
                                                Spell.Cast("Leg Sweep", req => HasTalent(MonkTalents.LegSweep) && Unit.UnitsInCombatWithUsOrOurStuff(5).Count(u => !u.IsStunned()) >= (Me.HealthPercent > 50 ? 3 : 1))
                                                )
                                            ),
                                        Spell.Cast(sp => HasTalent(MonkTalents.RushingJadeWind) ? "Rushing Jade Wind" : "Spinning Crane Kick", req => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() <= 8) >= MonkSettings.SpinningCraneKickCnt)
                                        )
                                    )
                                )
                            ),

                        // execute if we can
                        Common.CastTouchOfDeath(),

                        // stun if configured to do so
                        Spell.Cast("Leg Sweep", ret => Spell.UseAOE && MonkSettings.StunMobsWhileSolo && SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.CurrentTarget.IsWithinMeleeRange),

                        // taunt if needed
                        Spell.Cast("Provoke", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(), ret => SingularSettings.Instance.EnableTaunting),

                        Spell.Cast("Jab", req => Me.CurrentChi < Me.MaxChi && Me.CurrentEnergy > 85),

                        new Decorator(
                            req => Me.CurrentChi <= (Me.MaxChi - 2),
                            new PrioritySelector(
                                Spell.Cast("Keg Smash"),
                                Spell.Cast("Expel Harm"),
                                Spell.Cast("Jab")
                                )
                            ),

                        // use dizzying only if target not in range of keg smash or running away and doesnt have debuff
                        CastDizzyingHaze(),

                        new Decorator(
                            req => Me.CurrentChi > 2,
                            new PrioritySelector(
                                Spell.BuffSelf("Elusive Brew", ctx => MonkSettings.UseElusiveBrew && Me.HasAura("Elusive Brew", MonkSettings.ElusiveBrewMinumumCount)),
                                Spell.BuffSelf("Purifying Brew", ctx => Me.HasAnyAura("Moderate Stagger", "Heavy Stagger") && Me.CurrentChi >= 3),
                                Spell.Cast("Blackout Kick", ctx => SpellManager.HasSpell("Brewmaster Training") && Me.HasKnownAuraExpired("Shuffle", 1)),
                                Spell.Cast("Guard"),
                                Spell.Cast("Blackout Kick", req => Me.CurrentChi >= 4),
                                Spell.Cast("Tiger Palm", ret => Me.HasKnownAuraExpired("Tiger Power", 2))
                                )
                            ),

                        Spell.Cast("Jab"),

                        CreateCloseDistanceBehavior()
                        )
                    )
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Instances)]
        public static Composite CreateBrewmasterMonkInstanceCombat()
        {
            TankManager.NeedTankTargeting = (SingularRoutine.CurrentWoWContext == WoWContext.Instances);

            var powerStrikeTimer = new WaitTimer(TimeSpan.FromSeconds(20));

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                CreateCloseDistanceBehavior(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        // Execute if we can
                        Common.CastTouchOfDeath(),

                        // make sure I have aggro.
                        new Decorator(
                            req => SingularSettings.Instance.EnableTaunting,
                            new Decorator(
                                req => TankManager.Instance.NeedToTaunt.FirstOrDefault() != null,
                                new PrioritySelector(
                                    new Decorator(
                                        req => TankManager.Instance.NeedToTaunt.Count() > 1,
                                        new PrioritySelector(
                                            new Sequence(
                                                CreateSummonBlackOxStatueBehavior( on => TankManager.Instance.NeedToTaunt.FirstOrDefault()),
                                                new Wait(1, until => FindStatue() != null, new ActionAlwaysSucceed()),
                                                Spell.Cast("Provoke", ret => FindStatue())
                                                ),
                                            Spell.Cast("Provoke", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault()),
                                            CastDizzyingHaze()
                                            )
                                        )
                                    )
                                )
                            ),

                        // highest priority -- Keg Smash for threat and debuff
                        Spell.Cast("Keg Smash", req => Me.MaxChi - Me.CurrentChi >= 2 && Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8).Any(u => !u.HasAura("Weakened Blows"))),

                        // taunt if needed
                        new Throttle( TimeSpan.FromMilliseconds(1500),                           
                            Spell.CastOnGround("Dizzying Haze", on => TankManager.Instance.NeedToTaunt.FirstOrDefault(), req => TankManager.Instance.NeedToTaunt.Any(), false)
                            ),

                        // AOE
                        new Decorator(
                            req => Spell.UseAOE && Unit.UnfriendlyUnits(8).Count() >= 3,
                            new PrioritySelector(
                        // cast breath of fire to apply the dot.
                                Spell.Cast("Breath of Fire", ctx => Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8).Count(u => u.HasAura("Dizzying Haze") && !u.HasAura("Breath of Fire")) >= 3),
                                Spell.BuffSelf("Zen Sphere", ctx => HasTalent( MonkTalents.ZenSphere) && Me.HealthPercent < 90),
                        // aoe stuns
                                Spell.Cast("Charging Ox Wave", ctx => TalentManager.IsSelected((int)MonkTalents.ChargingOxWave) && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 30) >= 3),
                                Spell.Cast("Leg Sweep", ctx => TalentManager.IsSelected((int)MonkTalents.LegSweep)),
                                Spell.Cast("Spinning Crane Kick", req => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= MonkSettings.SpinningCraneKickCnt),
                                Spell.Cast("Rushing Jade Wind", req => HasTalent(MonkTalents.RushingJadeWind) && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= 3)
                                )
                            ),

                        Spell.BuffSelf("Purifying Brew", ctx => Me.HasAura("Stagger") && Me.CurrentChi >= 3),


                        new Decorator(
                            req => Me.CurrentChi <= (Me.MaxChi - 2),
                            new PrioritySelector(
                                Spell.Cast("Keg Smash"),
                                Spell.Cast("Expel Harm"),
                                Spell.Cast("Jab")
                                )
                            ),

                        // use dizzying only if target not in range of keg smash or running away and doesnt have debuff
                        CastDizzyingHaze(),

                        new Decorator(
                            req => Me.CurrentChi > 2,
                            new PrioritySelector(
                                Spell.BuffSelf("Elusive Brew", ctx => MonkSettings.UseElusiveBrew && Me.HasAura("Elusive Brew", MonkSettings.ElusiveBrewMinumumCount)),
                                Spell.BuffSelf("Purifying Brew", ctx => Me.HasAnyAura("Moderate Stagger", "Heavy Stagger") && Me.CurrentChi >= 3),
                                Spell.Cast("Blackout Kick", ctx => SpellManager.HasSpell("Brewmaster Training") && Me.HasKnownAuraExpired("Shuffle", 1)),
                                Spell.Cast("Guard"),
                                Spell.Cast("Blackout Kick", req => Me.CurrentChi >= 4),
                                Spell.Cast("Tiger Palm", ret => Me.HasKnownAuraExpired("Tiger Power", 2))
                                )
                            ),

                        // filler:
                        // cast on cooldown when Level < 26 (Guard) or Level >= 34 (Brewmaster Training), otherwise try to save some Chi for Guard if available
                        Spell.Cast("Tiger Palm", ret => SpellManager.HasSpell("Brewmaster Training") || Me.CurrentChi > 2 || !SpellManager.HasSpell("Guard") || Spell.GetSpellCooldown("Guard").TotalSeconds > 4)

                        )
                    )
                );
        }

        public static Composite CreateCloseDistanceBehavior()
        {
            return new Throttle(TimeSpan.FromMilliseconds(1500),
                new Decorator(
                    ret => MovementManager.IsClassMovementAllowed && Me.GotTarget(),
                    new PrioritySelector(
                        ctx => Me.CurrentTarget,
                        new Decorator( 
                            req => !((WoWUnit)req).IsAboveTheGround() && ((WoWUnit)req).SpellDistance() > 10 && Me.IsSafelyFacing(((WoWUnit)req), 10f),
                            new Sequence(
                                new PrioritySelector(
                                    Spell.Cast("Roll", on => (WoWUnit) on, req => !MonkSettings.DisableRoll && MovementManager.IsClassMovementAllowed)
                                    )
                                )
                            )
                        )
                    )
                );

        }

        private static WoWUnit _statue;

        private static Composite CreateSummonBlackOxStatueBehavior( UnitSelectionDelegate on )
        {
            if (!SpellManager.HasSpell("Summon Black Ox Statue"))
                return new ActionAlwaysFail();

            return new Throttle(
                8,
                new Decorator(
                    req => !Spell.IsSpellOnCooldown("Summon Black Ox Statue"),

                    new PrioritySelector(
                        ctx => _statue = FindStatue(),

                        new Decorator(

                            req => _statue == null || (Me.GotTarget() && _statue.SpellDistance(on(req)) > 30),

                            new Throttle(
                                10,
                                new PrioritySelector(

                                    ctx => on(ctx),

                                    Spell.CastOnGround(
                                        "Summon Black Ox Statue",
                                        loc =>
                                        {
                                            WoWUnit unit = on(loc);
                                            WoWPoint locStatue = WoWMovement.CalculatePointFrom(unit.Location, -5);
                                            if (locStatue.Distance(Me.Location) > 30)
                                            {
                                                float needFacing = Styx.Helpers.WoWMathHelper.CalculateNeededFacing(Me.Location, locStatue );
                                                locStatue = locStatue.RayCast(needFacing, 30f);
                                            }
                                            return locStatue;
                                        },
                                        req => req != null,
                                        false,
                                        desc => string.Format("{0:F1} yds from {1}", 5, (desc as WoWUnit).SafeName())
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static WoWUnit FindStatue()
        {
            const uint BLACK_OX_STATUE = 61146;
            WoWGuid guidMe = Me.Guid;
            return ObjectManager.GetObjectsOfType<WoWUnit>()
                .FirstOrDefault(u => u.Entry == BLACK_OX_STATUE && u.CreatedByUnitGuid == guidMe);
        }

    }
}