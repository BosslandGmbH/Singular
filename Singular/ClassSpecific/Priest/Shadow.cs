using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.Priest
{
    public class Shadow
    {
        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Normal)]
        public static Composite CreatePriestShadowNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),

                Spell.BuffSelf("Power Word: Shield", 
                    ret => SingularSettings.Instance.Priest.UseShieldPrePull && !StyxWoW.Me.HasAura("Weakened Soul") && !SpellManager.HasSpell("Mind Spike")),
                Spell.Cast("Holy Fire", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),
                //Spell.Cast("Smite", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),
                //Spell.Buff("Devouring Plague", true, ret => SingularSettings.Instance.Priest.DevouringPlagueFirst), // We have to have 3 orbs - why would we ever pull with this?
                Spell.Cast("Mind Blast"),
                Spell.Buff("Vampiric Touch", true, ret => !SpellManager.HasSpell("Mind Spike") || StyxWoW.Me.CurrentTarget.Elite),
                Spell.Buff("Shadow Word: Pain"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Normal)]
        public static Composite CreatePriestShadowNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.BuffSelf("Shadow Form"),

                // Defensive stuff
                Spell.BuffSelf("Power Word: Shield", 
                    ret => !StyxWoW.Me.HasAura("Weakened Soul") &&
                           (!SpellManager.HasSpell("Mind Spike") || StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Priest.ShieldHealthPercent)),
                Spell.BuffSelf("Dispersion", ret => StyxWoW.Me.ManaPercent < SingularSettings.Instance.Priest.DispersionMana),
                Spell.BuffSelf("Psychic Scream", 
                    ret => SingularSettings.Instance.Priest.UsePsychicScream &&
                           Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10) >= SingularSettings.Instance.Priest.PsychicScreamAddCount),
                
                Spell.Heal("Flash Heal", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Priest.ShadowFlashHealHealth),
                // don't attempt to heal unless below a certain percentage health
                new Decorator(ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.Priest.DontHealPercent,
                    new PrioritySelector(
                        Spell.Heal("Desperate Prayer", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 30),
                        Spell.Heal("Flash Heal", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 40),
                        Spell.Heal("Vampiric Embrace", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 40)
                        )),
                // for NPCs immune to shadow damage.
                Spell.Cast("Holy Fire", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),
                //Spell.Cast("Smite", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)), // Shadow no longer has smite

                // Before Mind Spike
                new Decorator(
                    ret => (StyxWoW.Me.CurrentTarget.MaxHealth > (StyxWoW.Me.MaxHealth / 2)) || StyxWoW.Me.CurrentTarget.Elite,
                    new PrioritySelector(
                        Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                        // We don't want to dot targets below 40% hp to conserve mana. Mind Blast/Flay will kill them soon anyway
                        Spell.Cast("Mind Blast", ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.ShadowOrbs) < 3),
                        Spell.Buff("Devouring Plague", true, ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.ShadowOrbs) >= 3),
                        Spell.Buff("Shadow Word: Pain", true, ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.HealthPercent > 40),
                        Spell.Buff("Vampiric Touch", true, ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.HealthPercent > 40),
                        Spell.Cast("Mindbender", ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.HealthPercent > 50),
                        Spell.Cast("Power Infusion"),
                        Spell.Cast("Mind Blast"),
                        Spell.Cast("Shadowfiend", ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Priest.ShadowfiendMana && StyxWoW.Me.CurrentTarget.HealthPercent >= 60), // Mana check is for mana management. Don't mess with it
                        Spell.Cast("Mind Flay", ret => StyxWoW.Me.ManaPercent >= SingularSettings.Instance.Priest.MindFlayMana),
                        // Helpers.Common.CreateUseWand(ret => SingularSettings.Instance.Priest.UseWand), // we no longer have wands or shoot
                        Movement.CreateMoveToTargetBehavior(true, 35f)
                        )),

                // After we have Mind Spike
                new Decorator(
                    ret => (StyxWoW.Me.CurrentTarget.MaxHealth < (StyxWoW.Me.MaxHealth / 2)),
                    new PrioritySelector(
                Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                Spell.Cast("Mind Blast"),
                Spell.Cast("Mind Spike"),
                //Helpers.Common.CreateUseWand(ret => SingularSettings.Instance.Priest.UseWand), // we no longer have wands or shoot
                Movement.CreateMoveToTargetBehavior(true, 35f)
                )));
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Battlegrounds)]
        public static Composite CreatePriestShadowPvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Spell.BuffSelf("Shadow Form"),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive stuff
                Spell.BuffSelf("Power Word: Shield", ret => !StyxWoW.Me.HasAura("Weakened Soul")),
                Spell.BuffSelf("Dispersion", ret => StyxWoW.Me.HealthPercent < 40),
                Spell.BuffSelf("Psychic Scream", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10*10) >= 1),

                // Offensive
                Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                // We don't want to dot targets below 40% hp to conserve mana. Mind Blast/Flay will kill them soon anyway
                Spell.Cast("Mind Blast", ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.ShadowOrbs) < 3),
                Spell.Buff("Devouring Plague", true, ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.ShadowOrbs) >= 3),
                Spell.Buff("Shadow Word: Pain", true, ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.HealthPercent > 30),
                Spell.Buff("Vampiric Touch", true, ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.HealthPercent > 30),
                Spell.Cast("Mindbender"),
                Spell.Cast("Power Infusion"),
                Spell.Cast("Mind Blast"),
                Spell.Cast("Shadowfiend", ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Priest.ShadowfiendMana && StyxWoW.Me.CurrentTarget.HealthPercent >= 60), // Mana check is for mana management. Don't mess with it
                Spell.Cast("Mind Flay", ret => StyxWoW.Me.ManaPercent >= SingularSettings.Instance.Priest.MindFlayMana),
                // Helpers.Common.CreateUseWand(ret => SingularSettings.Instance.Priest.UseWand), // we no longer have wands or shoot
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Rest, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Instances)]
        public static Composite CreatePriestShadowRest()
        {
            return new PrioritySelector(
                Spell.Resurrect("Resurrection"),
                Rest.CreateDefaultRestBehaviour()
                );
        }

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Instances)]
        public static Composite CreatePriestShadowInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.BuffSelf("Shadow Form"),

                // use fade to drop aggro.
                Spell.Cast("Fade", ret => (StyxWoW.Me.IsInParty || StyxWoW.Me.IsInRaid) && Targeting.GetAggroOnMeWithin(StyxWoW.Me.Location, 30) > 0),

                // Shadow immune npcs.
                Spell.Cast("Holy Fire", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),
                //Spell.Cast("Smite", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),

                // AoE Rotation
                new PrioritySelector(
                    ret => Group.Tanks.FirstOrDefault(t => 
                                Clusters.GetClusterCount(t, Unit.NearbyUnfriendlyUnits,ClusterType.Radius, 10f) >= 3),
                    new Decorator(
                        ret => ret != null,
                        Spell.Cast("Mind Sear", ret => (WoWUnit)ret))),
                        
                // In case of a guild raid
                new Decorator(
                    ret => !Group.Tanks.Any() && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    Spell.Cast("Mind Sear")),



                // Single target boss rotation
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsBoss(),
                    new PrioritySelector(
                        Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                        Spell.Cast("Mind Blast", ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.ShadowOrbs) < 3),
                        Spell.Buff("Devouring Plague", true, ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.ShadowOrbs) >= 3),
                        Spell.Buff("Shadow Word: Pain", true, ret => StyxWoW.Me.CurrentTarget.Elite),
                        Spell.Buff("Vampiric Touch", true, ret => StyxWoW.Me.CurrentTarget.Elite),
                        Spell.Cast("Mindbender"),
                        Spell.Cast("Power Infusion"),
                        Spell.Cast("Mind Blast"),
                        Spell.Cast("Shadowfiend", ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Priest.ShadowfiendMana && StyxWoW.Me.CurrentTarget.HealthPercent >= 60), // Mana check is for mana management. Don't mess with it
                        Spell.Cast("Mind Flay", ret => StyxWoW.Me.ManaPercent >= SingularSettings.Instance.Priest.MindFlayMana),
                        // Helpers.Common.CreateUseWand(ret => SingularSettings.Instance.Priest.UseWand), // we no longer have wands or shoot
                        Movement.CreateMoveToTargetBehavior(true, 35f)
                        )),

                // Single target trash rotation
                Spell.Cast("Mind Blast"),
                Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                Spell.Cast("Mind Spike"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion
    }
}
