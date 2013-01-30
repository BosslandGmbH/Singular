using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Collections.Generic;
using System.Drawing;

namespace Singular.ClassSpecific.Priest
{
    public class Shadow
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PriestSettings PriestSettings { get { return SingularSettings.Instance.Priest(); } }


        [Behavior(BehaviorType.Rest, WoWClass.Priest, WoWSpec.PriestShadow)]
        public static Composite CreateShadowPriestRestBehavior()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Rest.CreateDefaultRestBehaviour("Flash Heal", "Resurrection"),
                        Common.CreatePriestMovementBuff("Rest")
                        )
                    )
                );

        }

        [Behavior(BehaviorType.Heal, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Normal | WoWContext.Battlegrounds )]
        public static Composite CreateShadowHeal()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(
                        Spell.Cast("Flash Heal",
                            ctx => StyxWoW.Me,
                            ret => StyxWoW.Me.HealthPercent <= 20 ),

                        Spell.Cast("Flash Heal",
                            ctx => StyxWoW.Me,
                            ret => !Me.Combat && StyxWoW.Me.GetPredictedHealthPercent(true) <= 85),

                        Spell.BuffSelf("Renew",
                            ret => StyxWoW.Me.GetPredictedHealthPercent(true) <= 75),

                        Spell.Cast("Flash Heal",
                            ctx => StyxWoW.Me,
                            ret => StyxWoW.Me.GetPredictedHealthPercent(true) <= 50)
                        )
                    )
                );
        }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Normal)]
        public static Composite CreatePriestShadowNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Spell.WaitForCast(true),

                Spell.BuffSelf("Power Word: Shield", 
                    ret => SingularSettings.Instance.Priest().UseShieldPrePull && !StyxWoW.Me.HasAura("Weakened Soul") && !SpellManager.HasSpell("Mind Spike")),
                Spell.Cast("Holy Fire", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),
                //Spell.Cast("Smite", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),
                //Spell.Buff("Devouring Plague", true, ret => SingularSettings.Instance.Priest().DevouringPlagueFirst), // We have to have 3 orbs - why would we ever pull with this?
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

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateShadowDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                        Spell.BuffSelf("Shadow Form"),

                        // updated time to death tracking values before we need them
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),

                        TimeToDeathExtension.CreateWriteDebugTimeToDeath(),

                        // Defensive stuff
                        Spell.BuffSelf("Power Word: Shield", 
                            ret => !StyxWoW.Me.HasAura("Weakened Soul") &&
                                   (!SpellManager.HasSpell("Mind Spike") || StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Priest().ShieldHealthPercent)),
                        Spell.BuffSelf("Dispersion",
                            ret => StyxWoW.Me.ManaPercent < SingularSettings.Instance.Priest().DispersionMana
                                || StyxWoW.Me.HealthPercent < 40 
                                || Unit.NearbyUnfriendlyUnits.Count(t => t.GotTarget && t.CurrentTarget.IsTargetingUs()) >= 3),
                        Spell.BuffSelf("Psychic Scream", 
                            ret => SingularSettings.Instance.Priest().UsePsychicScream &&
                                   Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10) >= SingularSettings.Instance.Priest().PsychicScreamAddCount),
                
                        Spell.Cast("Flash Heal", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Priest().ShadowFlashHealHealth),
                        // don't attempt to heal unless below a certain percentage health
                        new Decorator(ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.Priest().DontHealPercent,
                            new PrioritySelector(
                                Spell.Cast("Desperate Prayer", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 30),
                                Spell.Cast("Flash Heal", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 40),
                                Spell.Cast("Vampiric Embrace", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 40)
                                )),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && AoeTargets.Count() > 1,
                            new PrioritySelector(
                                ctx => AoeTargets.FirstOrDefault(),
                                Spell.Cast("Mind Sear", ctx => (WoWUnit) ctx, ret => AoeTargets.Count() >= 5),
                                Spell.Buff("Shadow Word: Pain", true, ret => AoeTargets.FirstOrDefault(u => !u.HasMyAura("Shadow Word: Pain"))),
                                Spell.Buff("Vampiric Touch", true, ret => AoeTargets.FirstOrDefault(u => !u.HasMyAura("Vampiric Touch"))),
                                Spell.Cast("Mind Sear", ctx => (WoWUnit) ctx)
                                )
                            ),

                        // for NPCs immune to shadow damage.
                        Spell.Cast("Holy Fire", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),
                        //Spell.Cast("Smite", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)), // Shadow no longer has smite

                        // for targets we will fight longer than 10 seconds (it's a guess)
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.MaxHealth > (StyxWoW.Me.MaxHealth * 2)
                                || Me.CurrentTarget.TimeToDeath() > 10
                                || (Me.CurrentTarget.Elite && Me.CurrentTarget.Level > (Me.Level - 10)),

                            new PrioritySelector(
                                // We don't want to dot targets below 40% hp to conserve mana. Mind Blast/Flay will kill them soon anyway
                                Spell.Cast("Mind Blast", ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.ShadowOrbs) < 3),
                                Spell.Buff("Devouring Plague", true, ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.ShadowOrbs) >= 3),
                                Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                                Spell.Cast("Halo"),
                                Spell.Cast("Mind Spike", ret => Me.HasAura("Surge of Darkness")),
                                Spell.Buff("Vampiric Touch"),
                                Spell.Buff("Shadow Word: Pain", true, ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.HealthPercent > 40),
                                Spell.Cast("Mindbender", ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.HealthPercent > 50),
                                Spell.Cast("Power Infusion"),
                                Spell.Cast("Shadowfiend", ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Priest().ShadowfiendMana && StyxWoW.Me.CurrentTarget.HealthPercent >= 60), // Mana check is for mana management. Don't mess with it
                                Spell.Cast("Mind Flay", ret => StyxWoW.Me.ManaPercent >= SingularSettings.Instance.Priest().MindFlayMana),
                                Spell.Cast("Mind Blast"),
                                // Helpers.Common.CreateUseWand(ret => SingularSettings.Instance.Priest().UseWand), // we no longer have wands or shoot
                                Movement.CreateMoveToTargetBehavior(true, 35f)
                                )),

                        // for targets that die quickly
                        new PrioritySelector(
                            Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                            Spell.Buff("Devouring Plague", true, ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.ShadowOrbs) >= 3),
                            Spell.Cast("Mind Blast"),

                            new Decorator(
                                ret => !TalentManager.HasGlyph( "Mind Spike"),
                                new PrioritySelector(
                                    Spell.Buff("Shadow Word: Pain", true, ret => Me.CurrentTarget.TimeToDeath() > 13),
                                    Spell.Buff("Vampiric Touch", true, ret => Me.CurrentTarget.TimeToDeath() > 10)
                                    )
                                ),

                            Spell.Cast("Mind Flay", ret => Me.HasAura( "Glyph of Mind Spike", 2)),

                            Spell.Cast("Mind Spike", ret => !Me.CurrentTarget.HasMyAura("Devouring Plague") && !Me.CurrentTarget.HasMyAura("Shadow Word: Pain") && !Me.CurrentTarget.HasMyAura("Vampiric Touch")),
                                
                            Spell.Cast("Mind Flay"),

                            Movement.CreateMoveToTargetBehavior(true, 35f)
                            )
                        )
                    )
                );
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
                Helpers.Common.CreateDismount("Pulling"),
                Spell.WaitForCast(true),
                Spell.BuffSelf("Shadow Form"),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive stuff
                Spell.BuffSelf("Power Word: Shield", ret => !StyxWoW.Me.HasAura("Weakened Soul")),
                Spell.BuffSelf("Dispersion", ret => StyxWoW.Me.HealthPercent < 40 || Unit.NearbyUnfriendlyUnits.Count( t => t.GotTarget && t.CurrentTarget.IsTargetingUs()) >= 3),
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
                Spell.Cast("Shadowfiend", ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Priest().ShadowfiendMana && StyxWoW.Me.CurrentTarget.HealthPercent >= 60), // Mana check is for mana management. Don't mess with it
                Spell.Cast("Mind Flay", ret => StyxWoW.Me.ManaPercent >= SingularSettings.Instance.Priest().MindFlayMana),
                // Helpers.Common.CreateUseWand(ret => SingularSettings.Instance.Priest().UseWand), // we no longer have wands or shoot
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Rest, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Instances)]
        public static Composite CreatePriestShadowRest()
        {
            return Rest.CreateDefaultRestBehaviour(null, "Resurrection");
        }

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Instances)]
        public static Composite CreatePriestShadowInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                        Spell.BuffSelf("Shadow Form"),

                        // use fade to drop aggro.
                        Spell.Cast("Fade", ret => (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid) && Targeting.GetAggroOnMeWithin(StyxWoW.Me.Location, 30) > 0),

                        // Shadow immune npcs.
                        Spell.Cast("Holy Fire", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && AoeTargets.Count() > 1,
                            new PrioritySelector(
                                ctx => AoeTargets.FirstOrDefault(),
                                Spell.Cast( "Mind Sear", ctx => BestMindSearTarget, ret => AoeTargets.Count() >= 5),
                                Spell.Buff( "Shadow Word: Pain", true, ret => (WoWUnit) ret, ret => !((WoWUnit) ret).HasMyAura("Shadow Word: Pain")),
                                Spell.Buff( "Vampiric Touch", true, ret => (WoWUnit) ret, ret => !((WoWUnit) ret).HasMyAura("Vampiric Touch")),
                                Spell.Cast( "Mind Sear", ret => BestMindSearTarget )
                                )
                            ),

                        // Single target rotation
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.IsBoss(),
                            new PrioritySelector(

                                Spell.Buff("Devouring Plague", true, ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.ShadowOrbs) >= 3),

                                Spell.Cast("Mind Blast", ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.ShadowOrbs) < 3 || StyxWoW.Me.HasAura("Divine Insight")),

                                Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                                Spell.BuffSelf("Halo", ret => Unit.NearbyUnfriendlyUnits.Any( u => u.Distance < 30) || Unit.NearbyGroupMembers.Any( m => m.Distance < 30 && m.HealthPercent < 85)),
                                Spell.Cast("Mind Spike", ret => StyxWoW.Me.HasAura("Surge of Darkness")),
                                Spell.Buff("Shadow Word: Pain", true),
                                Spell.Buff("Vampiric Touch", true),
                                Spell.Cast("Mindbender"),
                                Spell.Cast("Power Infusion"),
                                Spell.Cast("Shadowfiend", ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Priest().ShadowfiendMana && StyxWoW.Me.CurrentTarget.HealthPercent >= 60), // Mana check is for mana management. Don't mess with it
                                Spell.Cast("Mind Flay", ret => StyxWoW.Me.ManaPercent >= SingularSettings.Instance.Priest().MindFlayMana),

                                Movement.CreateMoveToTargetBehavior(true, 35f)
                                )),

                        // Single target trash rotation
                        Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                        Spell.Cast("Mind Blast"),
                        Spell.Cast("Mind Spike"),
                        Spell.Cast("Mind Flay")
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        public static IEnumerable<WoWUnit> AoeTargets
        {
            get
            {
                return Unit.UnfriendlyUnitsNearTarget(10);
            }
        }


        static WoWUnit BestMindSearTarget
        {
            get 
            { 
                return Group.AnyTankNearby
                    ? Group.Tanks.Where( t => t.IsAlive && t.Distance < 40).OrderByDescending(t => AoeTargets.Count(a => t.Location.Distance(a.Location) < 10f)).FirstOrDefault()
                    : Clusters.GetBestUnitForCluster( AoeTargets, ClusterType.Radius, 10f); 
            }
        }


        #endregion

        #region Diagnostics

        private static Composite CreateShadowDiagnosticOutputBehavior()
        {
            return new Throttle(1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        uint orbs = Me.GetCurrentPower(WoWPowerType.ShadowOrbs);

                        string line = string.Format(".... h={0:F1}%/m={1:F1}%, moving={2}, orbs={3}",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.IsMoving,
                            orbs
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target == null)
                            line += ", target=(null)";
                        else
                            line += string.Format(", target={0} @ {1:F1} yds, th={2:F1}%, tface={3}, tlos={4}, tloss={5}",
                                target.SafeName(),
                                target.Distance,
                                target.HealthPercent,
                                Me.IsSafelyFacing(target,70f),
                                target.InLineOfSight,
                                target.InLineOfSpellSight
                                );

                        Logger.WriteDebug(Color.Wheat, line);
                        return RunStatus.Success;
                    }))
                );
        }

        #endregion
    }
}
