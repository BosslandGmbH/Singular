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
using Singular.Settings;
using Styx.WoWInternals;

namespace Singular.ClassSpecific.Monk
{
    public class Windwalker
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        #region NORMAL
        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Normal )]
        public static Composite CreateWindwalkerMonkPullNormal()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                       // close distance if at range
                        new Decorator(
                            ret => !Me.IsSafelyFacing( Me.CurrentTarget, 10f),
                            new Action( ret => {
                                StyxWoW.Me.CurrentTarget.Face();
                                return RunStatus.Failure;
                                }) 
                            ),

                        new Decorator(
                            ret => Me.IsSafelyFacing( Me.CurrentTarget, 10f) && Me.CurrentTarget.Distance > 10,
                            new PrioritySelector(
                                Spell.Cast("Flying Serpent Kick",  ret => TalentManager.HasGlyph("Flying Serpent Kick")),
                                Spell.Cast("Roll", ret => Me.CurrentTarget.Distance > 12 && !Me.HasAura("Flying Serpent Kick"))
                                )
                            ),

                        Spell.Cast("Grapple Weapon", ret => Me.CurrentTarget.Distance < 40 && !Me.CurrentTarget.Disarmed ),
                        Spell.Cast("Provoke", ret => !Me.CurrentTarget.Combat && Me.CurrentTarget.Distance < 40),
                        Spell.Cast("Crackling Jade Lightning", ret => !Me.IsMoving && Me.CurrentTarget.Distance < 40),
                        Spell.Cast("Chi Burst", ret => !Me.IsMoving && Me.CurrentTarget.Distance < 40),
                        Spell.Cast("Roll", ret => Me.CurrentTarget.Distance > 14)
                        )),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Normal)]
        public static Composite CreateWindwalkerMonkCombatNormal()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
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

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        new Decorator(
                            ret => SingularSettings.Instance.EnableDebugLogging,
                            new Action( ret => {
                                Logger.WriteDebug(".... health={0:F1}%, energy={1}%, chi={2}, tp3stk={3}, tptime={4} ms",
                                    Me.HealthPercent,
                                    Me.CurrentEnergy,
                                    Me.CurrentChi,
                                    Me.HasAura("Tiger Power", 3),
                                    Me.GetAuraTimeLeft("Tiger Power", true).TotalMilliseconds);
                                return RunStatus.Failure;
                                })
                            ),

                        Spell.Cast("Touch of Death", ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),

                        // AoE behavior
                        Spell.Cast("Rising Sun Kick"),

                        Spell.Cast("Paralysis", 
                            onu => Unit.NearbyUnfriendlyUnits
                                .FirstOrDefault( u => u.IsCasting && u.Distance.Between( 9, 20) && Me.IsSafelyFacing(u) )),

                        Spell.Cast("Rising Sun Kick"),

                        Spell.Cast("Fists of Fury", 
                            ret => Unit.NearbyUnfriendlyUnits.Any( u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u))),

                        Spell.Cast("Spinning Crane Kick", ret => Unit.NearbyUnfriendlyUnits.Count( u => u.Distance <= 8 ) >= 4),

                        Spell.Cast("Tiger Palm",
                            ret => Me.CurrentChi > 0
                                && (!Me.HasAura("Tiger Power", 3) || Me.GetAuraTimeLeft("Tiger Power", true).TotalSeconds < 4)),

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
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
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

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        new Decorator(
                            ret => SingularSettings.Instance.EnableDebugLogging,
                            new Action(ret =>
                            {
                                Logger.WriteDebug(".... health={0:F1}%, energy={1}%, chi={2}, tp3stk={3}, tptime={4} ms",
                                    Me.HealthPercent,
                                    Me.CurrentEnergy,
                                    Me.CurrentChi,
                                    Me.HasAura("Tiger Power", 3),
                                    Me.GetAuraTimeLeft("Tiger Power", true).TotalMilliseconds);
                                return RunStatus.Failure;
                            })
                            ),

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
                        
                        Spell.Cast("Spinning Crane Kick", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4),

                        Spell.Cast("Tiger Palm",
                            ret => Me.CurrentChi > 0
                                && (!Me.HasAura("Tiger Power", 3) || Me.GetAuraTimeLeft("Tiger Power", true).TotalSeconds < 4)),
                                    
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
                                StyxWoW.Me.CurrentTarget.Face();
                                return RunStatus.Failure;
                                }) 
                            ),

                        new Decorator(
                            ret => Me.IsSafelyFacing( Me.CurrentTarget, 10f) && Me.CurrentTarget.Distance > 10,
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
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.Cast("Roll", ret => Me.CurrentTarget.Distance > 15 )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Instances)]
        public static Composite CreateWindwalkerMonkCombatInstances()
        {
            return CreateWindwalkerMonkCombatNormal();
        }

        #endregion



        [Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkWindwalker)]
        public static Composite CreateWindwalkerMonkCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.BuffSelf("Stance of the Fierce Tiger"),

                        Spell.Buff("Touch of Karma", 
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault( 
                                u => u.IsTargetingMeOrPet 
                                    && (u.IsPlayer || SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds) 
                                    && (u.IsWithinMeleeRange || (u.Distance < 20 && TalentManager.HasGlyph("Touch of Karma")))),
                            ret => Me.CurrentChi >= 2 && Me.HealthPercent < 70),

                        Spell.Cast("Tigereye Brew", ctx => Me, ret => Me.HasAura("Tigereye Brew", 10)),
                        Spell.Cast("Energizing Brew", ctx => Me, ret => Me.CurrentEnergy < 40),
                        Spell.Cast("Fortifying Brew", ctx => Me, ret => Me.HealthPercent <= SingularSettings.Instance.Monk.FortifyingBrewPercent),
                        Spell.BuffSelf("Zen Sphere", ctx => TalentManager.IsSelected((int)Common.Talents.ZenSphere) && Me.HealthPercent < 90 && Me.CurrentChi >= 4)

                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateWindwalkerMonkHeal()
        {
            return 
                new PrioritySelector(
                    Spell.WaitForCast(true),

                    new Decorator(
                        ret => !Spell.IsGlobalCooldown() && !Me.Stunned,
                        new PrioritySelector(

                            // not likely, but if one close don't waste it
                            new Decorator(
                                ret => Me.HealthPercent < 80 && Common.AnySpheres(SphereType.Life, 10),
                                Common.CreateMoveToSphereBehavior(SphereType.Life)
                                ),

                            // healing sphere keeps spell on cursor for up to 3 casts... need to stop targeting after 1
                            new Sequence(
                                Spell.CastOnGround("Healing Sphere", 
                                    ctx => Me.Location, 
                                    ret => Me.HealthPercent < 65 && Me.EnergyPercent > 60 && !Common.AnySpheres( SphereType.Healing, 1f), 
                                    false),
                                new DecoratorContinue(
                                    ret => Me.CurrentPendingCursorSpell != null,
                                    new Action(ret => Lua.DoString("SpellStopTargeting()"))
                                    )
                                ),

                            Spell.Heal( "Expel Harm", ctx => Me, ret => Me.HealthPercent < 65 ),

                            Spell.Heal( "Chi Wave", ctx => Me, ret => TalentManager.IsSelected((int)Common.Talents.ChiWave) && Me.HealthPercent < SingularSettings.Instance.Monk.ChiWavePercent)
#if USE_CHI_BURST                            
                            ,

                            // check if spell exists in requirement since we dont want to evaluate onUnit if talent not taken
                            Spell.Cast("Chi Burst", 
                                ctx => BestChiBurstTarget(), 
                                ret => SpellManager.HasSpell("Chi Burst") 
                                    && Me.CurrentChi >= 2 
                                    && Me.HealthPercent < SingularSettings.Instance.Monk.ChiWavePercent)
#endif
                            )
                        )
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
                    ClusterType.Path, 
                    40f);

            if ( target == null || target.IsMe)
                target = Clusters.GetBestUnitForCluster(
                    Unit.NearbyUnitsInCombatWithMe,
                    ClusterType.Path,
                    40f);

            if (target == null)
                target = Me;

            return target;
        }
    }
}