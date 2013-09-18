using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

using Singular.Settings;
using Styx.WoWInternals;
using Styx.Common.Helpers;

namespace Singular.ClassSpecific.Monk
{
    public class Windwalker
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MonkSettings MonkSettings { get { return SingularSettings.Instance.Monk(); } }
        public static bool HasTalent(MonkTalents tal) { return TalentManager.IsSelected((int)tal); }

        // delay casting instant ranged abilities if we just cast Roll/FSK
        private readonly static WaitTimer RollTimer = new WaitTimer(TimeSpan.FromMilliseconds(1500));

        #region NORMAL
        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Normal )]
        public static Composite CreateWindwalkerMonkPullNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(true),

                Spell.WaitForCast(FaceDuring.Yes),

                // close distance if at range
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

#if NOT_NOW
                        //only use Spinning Fire Blossom on flying targets presently
                        new Decorator(
                            ret => Me.CurrentTarget.IsAerialTarget(),
                            new PrioritySelector(
                                new Action( ret => {
                                    Logger.WriteDebug( "{0} is an aerial target", Me.CurrentTarget.SafeName());
                                    return RunStatus.Failure;
                                    }),
                                Movement.CreateFaceTargetBehavior(2f),
                                new Throttle(1, 5, Spell.Cast("Spinning Fire Blossom", ret => Me.CurrentTarget.Distance.Between(10,40) && Me.IsSafelyFacing(Me.CurrentTarget, 1f)))
                                )
                            ),
#endif
                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed && !Me.CurrentTarget.IsAboveTheGround() && Me.CurrentTarget.Distance > 10,
                            new Sequence(
                                new PrioritySelector(
                                    Spell.Cast("Flying Serpent Kick", ret => TalentManager.HasGlyph("Flying Serpent Kick")),
                                    Spell.Cast("Roll", ret => Me.CurrentTarget.Distance > 12 && !Me.HasAura("Flying Serpent Kick"))
                                    ),
                                new Action( r => RollTimer.Reset() )
                                )
                            ),

                        Common.CreateGrappleWeaponBehavior(),

                        // only cast these in Pull if we didn't just roll/fsk.  allow us to reach
                        // .. targets we cannot fully navigate to, but don't want to cast 
                        // .. while while closing to melee range with roll/fsk
                        new Decorator(
                            ret => RollTimer.IsFinished,
                            new PrioritySelector(
                                Spell.Cast("Provoke", ret => !Me.CurrentTarget.IsPlayer && !Me.CurrentTarget.Combat && Me.CurrentTarget.Distance.Between( 10, 40)),
                                Spell.Cast("Crackling Jade Lightning", ret => !Me.IsMoving && Me.CurrentTarget.Distance < 40)
                                )
                            ),

                        Spell.Cast("Blackout Kick", ret => Me.CurrentChi == Me.MaxChi || Me.HasAura("Combo Breaker: Blackout Kick")),
                        Spell.Cast("Tiger Palm", ret => (Me.CurrentChi > 0 && Me.HasKnownAuraExpired( "Tiger Power")) || Me.HasAura("Combo Breaker: Tiger Palm")),
                        Spell.Cast( "Expel Harm", ret => Me.CurrentChi < (Me.MaxChi-2) && Me.HealthPercent < 80 && Me.CurrentTarget.Distance < 10 ),
                        Spell.Cast("Jab", ret => Me.CurrentChi < Me.MaxChi)
                        )
                    ),

                new Decorator( 
                    ret => Me.CurrentTarget.IsAboveTheGround(), 
                    new Sequence(
                        new Action(r => Logger.WriteDebug("Target appears airborne: flying={0} aboveground={1} dist={2:F1} zdiff={2:F1}", Me.CurrentTarget.IsFlying.ToYN(), Me.CurrentTarget.IsAboveTheGround().ToYN())),
                        Movement.CreateMoveToUnitBehavior(on => StyxWoW.Me.CurrentTarget, 35f, 30f)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Normal | WoWContext.Battlegrounds )]
        public static Composite CreateWindwalkerMonkCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Stance of the Fierce Tiger"),

                Spell.BuffSelf("Legacy of the White Tiger"),
                Spell.BuffSelf("Legacy of the Emperor"),

                new Decorator(
                    req => !Unit.IsTrivial( Me.CurrentTarget),
                    new PrioritySelector(
                        Spell.Buff("Touch of Karma",
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(
                                u => u.IsTargetingMeOrPet
                                    && (u.IsPlayer || SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds)
                                    && (u.IsWithinMeleeRange || (u.Distance < 20 && TalentManager.HasGlyph("Touch of Karma")))),
                            ret => Me.HealthPercent < 70),

                        Spell.Cast("Tigereye Brew", ctx => Me, ret => Me.HasAura("Tigereye Brew", 10)),
                        Spell.Cast("Energizing Brew", ctx => Me, ret => Me.CurrentEnergy < 40),
                        Spell.Cast("Chi Brew", ctx => Me, ret => Me.CurrentChi == 0),
                        Spell.Cast("Fortifying Brew", ctx => Me, ret => Me.HealthPercent <= SingularSettings.Instance.Monk().FortifyingBrewPercent),
                        Spell.BuffSelf("Zen Sphere", ctx => Me.HealthPercent < 90 && Me.CurrentChi >= 4),
                        Spell.Cast("Invoke Xuen, the White Tiger", ret => !Me.IsMoving && Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 10) >= 2)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Normal)]
        public static Composite CreateWindwalkerMonkCombatNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(true),

                new Decorator( 
                    ret => StyxWoW.Me.HasAura( "Fists of Fury")
                        && !Unit.NearbyUnfriendlyUnits.Any( u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)),
                    new Action( ret => {
                        Logger.WriteDebug( "cancelling Fists of Fury - no targets within range");
                        SpellManager.StopCasting();
                        return RunStatus.Success;
                        })
                    ),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateWindwalkerDiagnosticBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateGrappleWeaponBehavior(),

                        Spell.Cast("Touch of Death", ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),

                        // Symbiosis
                        Spell.Cast("Bear Hug", ret => !Unit.NearbyUnfriendlyUnits.Any( u => u.Guid != Me.CurrentTargetGuid && u.CurrentTargetGuid == Me.Guid)),

                        // AoE behavior
                        Spell.Cast("Paralysis", 
                            onu => Unit.NearbyUnfriendlyUnits
                                .FirstOrDefault( u => u.IsCasting && u.Distance.Between( 9, 20) && Me.IsSafelyFacing(u) )),

                        Spell.Cast("Rising Sun Kick"),

                        Spell.Cast("Fists of Fury", 
                            ret => Unit.NearbyUnfriendlyUnits.Count( u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)) >= 2),

                        Spell.Cast("Rushing Jade Wind", ctx => HasTalent(MonkTalents.RushingJadeWind) && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= 4),
                        Spell.Cast("Spinning Crane Kick", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4),

                        Spell.Cast("Tiger Palm", ret => Me.CurrentChi > 0 && Me.HasKnownAuraExpired( "Tiger Power")),

                        // chi dump
                        Spell.Cast("Blackout Kick", ret => Me.CurrentChi == Me.MaxChi),

                        // free Tiger Palm or Blackout Kick... do before Jab
                        Spell.Cast("Blackout Kick", ret => Me.HasAura("Combo Breaker: Blackout Kick")),
                        Spell.Cast("Tiger Palm", ret => Me.HasAura("Combo Breaker: Tiger Palm")),

                        Spell.Cast( "Expel Harm", ret => Me.CurrentChi < (Me.MaxChi-2) && Me.HealthPercent < 80),

                        Spell.Cast("Jab", ret => Me.CurrentChi < Me.MaxChi)
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
        #region BATTLEGROUNDS

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Battlegrounds  )]
        public static Composite CreateWindwalkerMonkPullBattlegrounds()
        {
            // replace with battleground specific logic 
            return CreateWindwalkerMonkPullNormal();
        }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Battlegrounds)]
        public static Composite CreateWindwalkerMonkCombatBattlegrounds()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(true),

                new Decorator(
                    ret => StyxWoW.Me.HasAura("Fists of Fury")
                        && !Unit.NearbyUnfriendlyUnits.Any(u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)),
                    new Action(ret =>
                    {
                        Logger.WriteDebug("cancelling Fists of Fury - no targets within range");
                        SpellManager.StopCasting();
                        return RunStatus.Success;
                    })
                    ),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateWindwalkerDiagnosticBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateGrappleWeaponBehavior(),

                        Spell.Cast("Leg Sweep", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.IsWithinMeleeRange && !u.IsCrowdControlled())),

                        Spell.Cast("Touch of Death", ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),

                        Spell.Buff("Paralysis",
                            onu => Unit.NearbyUnfriendlyUnits
                                .FirstOrDefault(u => u.IsCasting && u.Distance.Between(9, 20) && Me.IsSafelyFacing(u))),

                        Spell.Buff("Spear Hand Strike",
                            onu => Unit.NearbyUnfriendlyUnits
                                .FirstOrDefault(u => u.IsCasting && u.IsWithinMeleeRange && Me.IsSafelyFacing(u))),

                        Spell.Cast("Rising Sun Kick"),

                        Spell.Cast("Fists of Fury",
                            ret => Unit.NearbyUnfriendlyUnits.Any(u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u))),

                        Spell.Cast("Rushing Jade Wind", ctx => HasTalent(MonkTalents.RushingJadeWind) && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= 4),
                        Spell.Cast("Spinning Crane Kick", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4),

                        Spell.Cast("Tiger Palm", ret => Me.CurrentChi > 0 && Me.HasKnownAuraExpired("Tiger Power")),
                                    
                        // chi dump
                        Spell.Cast("Blackout Kick", ret => Me.CurrentChi == Me.MaxChi),

                        // free Tiger Palm or Blackout Kick... do before Jab
                        Spell.Cast("Blackout Kick", ret => Me.HasAura("Combo Breaker: Blackout Kick")),
                        Spell.Cast("Tiger Palm", ret => Me.HasAura("Combo Breaker: Tiger Palm")),

                        Spell.Cast("Jab", ret => Me.CurrentChi < Me.MaxChi),

                        // close distance if at range
                        new Decorator(
                            ret => !Me.IsSafelyFacing( Me.CurrentTarget, 10f),
                            new Action( ret => {
                                // Logger.WriteDebug("WindWalkerMonk: Facing because turned more than 10 degrees");
                                StyxWoW.Me.CurrentTarget.Face();
                                return RunStatus.Failure;
                                }) 
                            ),

                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed && Me.IsSafelyFacing(Me.CurrentTarget, 10f) && Me.CurrentTarget.Distance > 10,
                            new PrioritySelector(
                                Spell.Cast("Flying Serpent Kick",  ret => TalentManager.HasGlyph("Flying Serpent Kick")),
                                Spell.Cast("Roll", ret => Me.CurrentTarget.Distance > 12 && !Me.HasAura("Flying Serpent Kick"))
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region INSTANCES

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Instances )]
        public static Composite CreateWindwalkerMonkPullInstances()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(true),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && Me.CurrentTarget.Distance > 15)
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Instances )]
        public static Composite CreateWindwalkerMonkInstanceCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Stance of the Fierce Tiger"),
                Spell.Cast("Tigereye Brew", ctx => Me, ret => Me.HasAura("Tigereye Brew", 10)),
                Spell.Cast("Energizing Brew", ctx => Me, ret => Me.CurrentEnergy < 40),
                Spell.Cast("Chi Brew", ctx => Me, ret => Me.CurrentChi == 0),
                Spell.Cast("Fortifying Brew", ctx => Me, ret => Me.HealthPercent <= SingularSettings.Instance.Monk().FortifyingBrewPercent),
                Spell.BuffSelf("Zen Sphere", ctx => TalentManager.IsSelected((int)MonkTalents.ZenSphere) && Me.HealthPercent < 90 && Me.CurrentChi >= 4),
                Spell.BuffSelf("Invoke Xuen, the White Tiger", ret => !Me.IsMoving && Me.CurrentTarget.IsBoss() && Me.CurrentTarget.IsWithinMeleeRange)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Instances)]
        public static Composite CreateWindwalkerMonkCombatInstances()
        {
            return CreateWindwalkerMonkCombatNormal();
        }

        #endregion



        [Behavior(BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateWindwalkerMonkHeal()
        {
            return new PrioritySelector(

                // not likely, but if one close don't waste it
                new Decorator(
                    ret => Me.HealthPercent < 80 && Common.AnySpheres(SphereType.Life, MonkSettings.SphereDistanceInCombat ),
                    Common.CreateMoveToSphereBehavior(SphereType.Life, MonkSettings.SphereDistanceInCombat)
                    ),

                Common.CreateHealingSphereBehavior(65),

                Spell.Cast("Expel Harm", on =>
                {
                    WoWUnit target = null;
                        
                    if (Me.HealthPercent < MonkSettings.ExpelHarmHealth)
                        target = Me;
                    else if (MonkSettings.AllowOffHeal && TalentManager.HasGlyph("Targeted Explusion"))
                        target = Unit.GroupMembers.Where(p => p.IsAlive && p.GetPredictedHealthPercent() < MonkSettings.ExpelHarmHealth && p.DistanceSqr < 40 * 40).FirstOrDefault();

                    if (target != null)
                        Logger.WriteDebug("Expel Harm Heal @ actual:{0:F1}% predict:{1:F1}% and moving:{2}", target.HealthPercent, target.GetPredictedHealthPercent(true), target.IsMoving);

                    return target;
                }),

                Spell.Cast( "Chi Wave", ctx => Me, ret => TalentManager.IsSelected((int)MonkTalents.ChiWave) && Me.HealthPercent < SingularSettings.Instance.Monk().ChiWavePercent)
                );
        }

        /// <summary>
        /// selects best target, favoring healing multiple group members followed by damaging multiple targets
        /// </summary>
        /// <returns></returns>
        private static WoWUnit BestChiBurstTarget()
        {
            WoWUnit target = null;

            if (Me.IsInGroup())
                target = Clusters.GetBestUnitForCluster( 
                    Unit.NearbyGroupMembers.Where(m => m.IsAlive && m.HealthPercent < 80), 
                    ClusterType.PathToUnit, 
                    40f);

            if ( target == null || target.IsMe)
                target = Clusters.GetBestUnitForCluster(
                    Unit.NearbyUnitsInCombatWithMeOrMyStuff,
                    ClusterType.PathToUnit,
                    40f);

            if (target == null)
                target = Me;

            return target;
        }

        public static Composite CreateCloseDistanceBehavior()
        {
            return new Throttle(TimeSpan.FromMilliseconds(1500),
                new Decorator(
                    ret => MovementManager.IsClassMovementAllowed && !Me.CurrentTarget.IsAboveTheGround() && Me.CurrentTarget.SpellDistance() > 10 && Me.IsSafelyFacing(Me.CurrentTarget, 10f),
                    new Sequence(
                        new PrioritySelector(
                            Spell.Cast("Flying Serpent Kick", ret => TalentManager.HasGlyph("Flying Serpent Kick")),
                            Spell.Cast("Roll", ret => Me.CurrentTarget.Distance > 12 && !Me.HasAnyAura("Flying Serpent Kick"))
                            )
                        )
                    )
                );

        }

        private static Composite CreateWindwalkerDiagnosticBehavior()
        {
            return new ThrottlePasses( 1, 1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action( ret => {
                        Logger.WriteDebug(".... health={0:F1}%, energy={1}%, chi={2}, tpower={3}, tptime={4}, tgt={5:F1} @ {6:F1}, ",
                            Me.HealthPercent,
                            Me.CurrentEnergy,
                            Me.CurrentChi,
                            Me.HasAura("Tiger Power"),
                            Me.GetAuraTimeLeft("Tiger Power", true).TotalMilliseconds,
                            Me.CurrentTarget == null ? 0f : Me.CurrentTarget.HealthPercent ,
                            (Me.CurrentTarget ?? Me).Distance
                            );
                        return RunStatus.Failure;
                        })
                    )
                );

        }
    }
}