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

namespace Singular.ClassSpecific.Monk
{
    public class Brewmaster
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MonkSettings MonkSettings { get { return SingularSettings.Instance.Monk(); } }

        private static readonly WaitTimer _clashTimer = new WaitTimer(TimeSpan.FromSeconds(3));
        // ToDo  Summon Black Ox Statue, Chi Burst and 

        private static bool UseChiBrew
        {
            get
            {
                return TalentManager.IsSelected((int)Common.Talents.ChiBrew) && Me.CurrentChi == 0 && Me.CurrentTargetGuid > 0 &&
                       (SingularRoutine.CurrentWoWContext == WoWContext.Instances && Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances);
            }
        }

        // fix for when clash target is out of range due to long server generatord paths.
        private static Composite TryCastClashBehavior()
        {
            return new Decorator(
                ctx => _clashTimer.IsFinished && SpellManager.CanCast("Clash", Me.CurrentTarget, true, false),
                new Sequence(new Action(ctx => SpellManager.Cast("Clash")), new Action(ctx => _clashTimer.Reset())));
        }

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        public static Composite CreateBrewmasterMonkPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => Me.CurrentTarget),
                Spell.CastOnGround("Dizzying Haze", ctx => Me.CurrentTarget.Location, ctx => Unit.UnfriendlyUnitsNearTarget(8).Count() > 1, true),
                TryCastClashBehavior(),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && Me.CurrentTarget.Distance.Between(10, 40)),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkBrewmaster, priority: 1)]
        public static Composite CreateBrewmasterMonkCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Stance of the Sturdy Ox"),
                    Spell.BuffSelf(
                        "Avert Harm",
                        ctx =>
                        {
                            if (!MonkSettings.UseAvertHarm || !Me.GroupInfo.IsInParty)
                                return false;
                            var nearbyGroupMembers = Me.RaidMembers.Where(r => !r.IsMe && r.Distance <= 10).ToList();
                            return nearbyGroupMembers.Any() && nearbyGroupMembers.Average(u => u.HealthPercent) <= MonkSettings.AvertHarmGroupHealthPercent;
                        }),
                    Spell.BuffSelf("Zen Meditation", ctx => Targeting.Instance.FirstUnit != null && Targeting.Instance.FirstUnit.IsCasting),

                    Spell.BuffSelf("Fortifying Brew", ctx => Me.HealthPercent <= MonkSettings.FortifyingBrewPercent),
                    Spell.BuffSelf("Guard", ctx => Me.HasAura("Power Guard")),
                    Spell.BuffSelf("Elusive Brew", ctx => MonkSettings.UseElusiveBrew && Me.HasAura("Elusive Brew") && Me.Auras["Elusive Brew"].StackCount >= MonkSettings.ElusiveBrewMinumumStackCount),
                    Spell.Cast("Chi Brew", ctx => UseChiBrew),
                    Spell.BuffSelf("Zen Sphere", ctx => TalentManager.IsSelected((int)Common.Talents.ZenSphere) && Me.HealthPercent < 90 && Me.CurrentChi >= 4));
        }

        [Behavior(BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkBrewmaster, priority: 1)]
        public static Composite CreateBrewmasterMonkHeal()
        {
            return new PrioritySelector(
                // use chi wave as a heal.
                Spell.Cast(
                    "Chi Wave",
                    ctx =>
                    TalentManager.IsSelected((int)Common.Talents.ChiWave) &&
                    (Me.HealthPercent < MonkSettings.ChiWavePercent ||
                     Me.RaidMembers.Count(m => m.DistanceSqr <= 20 * 20 && m.HealthPercent <= MonkSettings.ChiWavePercent) >= 3))

                //Spell.Cast("Zen Sphere", ctx => TalentManager.IsSelected((int)Common.Talents.ZenSphere) && Me.RaidMembers.Count(m => m.DistanceSqr <= 20 * 20 && m.HealthPercent <= 70) >= 3)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Normal)]
        public static Composite CreateBrewmasterMonkNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => Me.CurrentTarget),
                // make sure I have aggro.
                Spell.Cast("Provoke", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(), ret => SingularSettings.Instance.EnableTaunting),
                Spell.Cast("Keg Smash", ctx => Me.CurrentChi < 4 && Unit.NearbyUnitsInCombatWithMe.Any(u => u.DistanceSqr <= 8 * 8)),
                Spell.CastOnGround("Dizzying Haze", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault().Location, ctx => TankManager.Instance.NeedToTaunt.Any(), true),
                Spell.Cast("Tiger Palm", ret => Me.CurrentChi >= 1 && Me.HasKnownAuraExpired("Tiger Power")),
                Spell.Cast("Blackout Kick", ret => Me.CurrentChi >= 2),
                Spell.Cast("Jab"),
                TryCastClashBehavior(),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll",
                    ret => MovementManager.IsClassMovementAllowed
                        && Me.CurrentTarget.Distance.Between(10, 40)),
                Movement.CreateMoveToMeleeBehavior(true));
        }


        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Battlegrounds)]
        public static Composite CreateBrewmasterMonkPvpCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => Me.CurrentTarget),
                Spell.Cast("Tiger Palm", ret => Me.CurrentChi >= 1 && Me.HasKnownAuraExpired("Tiger Power")),
                Spell.Cast("Blackout Kick", ret => Me.CurrentChi >= 2),
                Spell.Cast("Jab"),
                TryCastClashBehavior(),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll",
                    ret => MovementManager.IsClassMovementAllowed
                        && Me.CurrentTarget.Distance.Between(10, 40)),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        #region Instance

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Instances)]
        public static Composite CreateBrewmasterMonkInstanceCombat()
        {

            var powerStrikeTimer = new WaitTimer(TimeSpan.FromSeconds(20));

            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => Me.CurrentTarget),

                // make sure I have aggro.
                Spell.Cast("Provoke", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(), ret => SingularSettings.Instance.EnableTaunting),
                // apply the Weakened Blows debuff. Keg Smash also generates allot of threat 
                Spell.Cast("Keg Smash", ctx => Me.MaxChi - Me.CurrentChi >= 2 &&
                    Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8).Any(u => !u.HasAura("Weakened Blows"))),
                Spell.CastOnGround("Dizzying Haze", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault().Location, ctx => TankManager.Instance.NeedToTaunt.Any(), true),

                // AOE
                new Decorator(ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= 3,
                    new PrioritySelector(
                // cast breath of fire to apply the dot.
                        Spell.Cast("Breath of Fire", ctx => Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8).Count(u => u.HasAura("Dizzying Haze") && !u.HasAura("Breath of Fire")) >= 3),
                        Spell.Cast("Zen Sphere", ctx => TalentManager.IsSelected((int)Common.Talents.ZenSphere) && Me.HealthPercent < 90 && Me.HasAura("Zen Sphere") && Me.CurrentChi >= 4),
                // aoe stuns
                        Spell.Cast("Charging Ox Wave", ctx => TalentManager.IsSelected((int)Common.Talents.ChargingOxWave) && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 30) >= 3),
                        Spell.Cast("Leg Sweep", ctx => TalentManager.IsSelected((int)Common.Talents.LegSweep))
                        )),

                // ***** Spend Chi *****
                Spell.Cast("Rushing Jade Wind", ctx => TalentManager.IsSelected((int)Common.Talents.RushingJadeWind) && (!Me.HasAura("Shuffle") || Me.Auras["Shuffle"].TimeLeft <= TimeSpan.FromSeconds(1))),
                Spell.Cast("Blackout Kick", ctx => !Me.HasAura("Shuffle") || Me.Auras["Shuffle"].TimeLeft <= TimeSpan.FromSeconds(1)),
                Spell.Cast("Tiger Palm", ret => Me.CurrentChi >= 2 && SpellManager.HasSpell("Guard") && !Me.HasAura("Power Guard")),
                //Spell.Cast("Tiger Palm", ret => Me.CurrentChi >= 2 && SpellManager.HasSpell("Blackout Kick") && (!Me.HasAura("Tiger Power") || Me.Auras["Tiger Power"].StackCount < 3)),

                Spell.BuffSelf("Purifying Brew", ctx => Me.HasAura("Stagger") && Me.CurrentChi >= 3),

                // ***** Generate Chi *****
                Spell.Cast("Keg Smash", ctx => Me.MaxChi - Me.CurrentChi >= 2 && Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8)),
                Spell.Cast("Spinning Crane Kick", ctx => Me.MaxChi - Me.CurrentChi >= 1 && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= 3),
                // jab with power strike talent is > expel Harm if off CD.
                new Decorator(ctx => TalentManager.IsSelected((int)Common.Talents.PowerStrikes) && Me.MaxChi - Me.CurrentChi >= 2 && SpellManager.CanCast("Jab") && powerStrikeTimer.IsFinished,
                    new Sequence(
                        new Action(ctx => powerStrikeTimer.Reset()),
                        Spell.Cast("Jab")
                )),

                Spell.Cast("Expel Harm", ctx => Me.HealthPercent < 90 && Me.MaxChi - Me.CurrentChi >= 1 && Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 10 * 10)),
                Spell.Cast("Jab", ctx => Me.MaxChi - Me.CurrentChi >= 1),

                // filler
                Spell.Cast("Tiger Palm", ret => !SpellManager.HasSpell("Blackout Kick") || SpellManager.HasSpell("Brewmaster Training")),

                TryCastClashBehavior(),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll",
                    ret => MovementManager.IsClassMovementAllowed
                        && Me.CurrentTarget.Distance.Between(10, 40)),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        #endregion
    }
}