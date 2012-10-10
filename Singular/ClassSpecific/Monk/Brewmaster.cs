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

namespace Singular.ClassSpecific.Monk
{
    public class Brewmaster
    {
        private static readonly WaitTimer _clashTimer = new WaitTimer(TimeSpan.FromSeconds(3));

        private static bool UseChiBrew
        {
            get
            {
                return TalentManager.IsSelected((int)Common.Talents.ChiBrew) && StyxWoW.Me.CurrentChi == 0 && StyxWoW.Me.CurrentTargetGuid > 0 &&
                       (SingularRoutine.CurrentWoWContext == WoWContext.Instances && StyxWoW.Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances);
            }
        }

        // fix for when clash target is out of range due to long server generatord paths.
        private static Composite TryCastClashBehavior()
        {
            return new Decorator(
                ctx => _clashTimer.IsFinished && SpellManager.CanCast("Clash", StyxWoW.Me.CurrentTarget, true, false),
                new Sequence(new Action(ctx => SpellManager.Cast("Clash")), new Action(ctx => _clashTimer.Reset())));
        }

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        public static Composite CreateBrewmasterMonkPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.CastOnGround("Dizzying Haze", ctx => StyxWoW.Me.CurrentTarget.Location, ctx => Unit.UnfriendlyUnitsNearTarget(8).Count() > 1, false),
                TryCastClashBehavior(),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll", ret => StyxWoW.Me.CurrentTarget.Distance.Between(10, 40)),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkBrewmaster,priority: 1)]
        public static Composite CreateBrewmasterMonkCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.Cast(
                        "Avert Harm",
                        ctx =>
                        SingularSettings.Instance.Monk.UseAvertHarm && StyxWoW.Me.GroupInfo.IsInParty &&
                        StyxWoW.Me.RaidMembers.Where(r => !r.IsMe).Average(u => u.HealthPercent) <= SingularSettings.Instance.Monk.AvertHarmGroupHealthPercent),

                    Spell.Cast("Fortifying Brew", ctx => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Monk.FortifyingBrewPercent),
                    Spell.Cast("Guard", ctx => StyxWoW.Me.HasAura("Power Guard") && StyxWoW.Me.Auras["Power Guard"].StackCount == 3),
                    Spell.Cast("Elusive Brew", ctx => StyxWoW.Me.HasAura("Elusive Brew") && StyxWoW.Me.Auras["Elusive Brew"].StackCount == 3),
                    Spell.Cast("Chi Brew", ctx => UseChiBrew));
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
                    (StyxWoW.Me.HealthPercent < SingularSettings.Instance.Monk.ChiWavePercent ||
                     StyxWoW.Me.RaidMembers.Count(m => m.DistanceSqr <= 20 * 20 && m.HealthPercent <= SingularSettings.Instance.Monk.ChiWavePercent) >= 3))

                //Spell.Cast("Zen Sphere", ctx => TalentManager.IsSelected((int)Common.Talents.ZenSphere) && StyxWoW.Me.RaidMembers.Count(m => m.DistanceSqr <= 20 * 20 && m.HealthPercent <= 70) >= 3)
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
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // make sure I have aggro.
                Spell.Cast("Provoke", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(), ret => SingularSettings.Instance.EnableTaunting),
                Spell.Cast("Keg Smash", ctx => StyxWoW.Me.CurrentChi < 4 && Unit.NearbyUnitsInCombatWithMe.Any(u => u.DistanceSqr <= 8 * 8)),
                Spell.CastOnGround("Dizzying Haze", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault().Location, ctx => TankManager.Instance.NeedToTaunt.Any(), false),
                Spell.Cast("Tiger Palm", ret => !SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1),
                //So when we get our kick spell we dont waste chi on stacking a buff more then we have too.
                //The OR part is so if we dont have the buff we can cast Tiger Palm and apply it, then the Or will make sure once we have it, it wont go over 3 stacks since that will be a waste of chi.
                Spell.Cast(
                    "Tiger Palm",
                    ret =>
                    SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1 &&
                    (!StyxWoW.Me.HasAura("Tiger Power") || StyxWoW.Me.HasAura("Tiger Power") && StyxWoW.Me.Auras["Tiger Power"].StackCount < 3)),
                Spell.Cast("Blackout Kick", ret => StyxWoW.Me.CurrentChi >= 2),
                Spell.Cast("Jab"),
                TryCastClashBehavior(),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll", ret => StyxWoW.Me.CurrentTarget.Distance.Between(10, 40)),
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
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.Cast("Tiger Palm", ret => !SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1),
                //So when we get our kick spell we dont waste chi on stacking a buff more then we have too.
                //The OR part is so if we dont have the buff we can cast Tiger Palm and apply it, then the Or will make sure once we have it, it wont go over 3 stacks since that will be a waste of chi.
                Spell.Cast(
                    "Tiger Palm",
                    ret =>
                    SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1 &&
                    (!StyxWoW.Me.HasAura("Tiger Power") || StyxWoW.Me.HasAura("Tiger Power") && StyxWoW.Me.Auras["Tiger Power"].StackCount < 3)),
                Spell.Cast("Blackout Kick", ret => StyxWoW.Me.CurrentChi >= 2),
                Spell.Cast("Jab"),
                TryCastClashBehavior(),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll", ret => StyxWoW.Me.CurrentTarget.Distance.Between(10, 40)),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        #region Instance

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Instances)]
        public static Composite CreateBrewmasterMonkInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // make sure I have aggro.
                Spell.Cast("Provoke", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(), ret => SingularSettings.Instance.EnableTaunting),
                Spell.Cast("Keg Smash", ctx => StyxWoW.Me.CurrentChi < 3 && Unit.NearbyUnitsInCombatWithMe.Any(u => u.DistanceSqr <= 8 * 8)),
                Spell.CastOnGround("Dizzying Haze", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault().Location, ctx => TankManager.Instance.NeedToTaunt.Any(), false),
                // AOE
                new Decorator(ctx => Unit.NearbyUnitsInCombatWithMe.Count(u => u.DistanceSqr <= 8 * 8) >= 3, new PrioritySelector(
                    Spell.Cast("Breath of Fire", ctx => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8) >= 3),
                    
                    // aoe stuns
                    Spell.Cast("Charging Ox Wave", ctx => TalentManager.IsSelected((int)Common.Talents.ChargingOxWave) && Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 30) >= 3),                    
                    Spell.Cast("Leg Sweep", ctx => TalentManager.IsSelected((int)Common.Talents.LegSweep) )
                    
                    )),


                Spell.Cast("Tiger Palm", ret => !SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1),
                //So when we get our kick spell we dont waste chi on stacking a buff more then we have too.
                //The OR part is so if we dont have the buff we can cast Tiger Palm and apply it, then the Or will make sure once we have it, it wont go over 3 stacks since that will be a waste of chi.
                Spell.Cast(
                    "Tiger Palm",
                    ret =>
                    SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1 &&
                    (!StyxWoW.Me.HasAura("Tiger Power") || StyxWoW.Me.HasAura("Tiger Power") && StyxWoW.Me.Auras["Tiger Power"].StackCount < 3)),
                Spell.Cast("Blackout Kick", ret => StyxWoW.Me.CurrentChi >= 2),
                //Spell.Cast("Jab"),

                TryCastClashBehavior(),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll", ret => StyxWoW.Me.CurrentTarget.Distance.Between(10, 40)),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        #endregion
    }
}