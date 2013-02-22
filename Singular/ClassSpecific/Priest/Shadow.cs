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
                Spell.BuffSelf("Dispersion", ret => Me.ManaPercent < SingularSettings.Instance.MinMana && Me.IsSwimming ),
                Rest.CreateDefaultRestBehaviour("Flash Heal", "Resurrection"),
                Common.CreatePriestMovementBuff("Rest")
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
                        Spell.Cast("Desperate Prayer", ret => Me, ret => Me.HealthPercent < PriestSettings.DesperatePrayerHealth),

                        Spell.BuffSelf("Power Word: Shield", ret => (Me.HealthPercent < PriestSettings.ShieldHealthPercent || !Me.HasAura("Shadowform")) && !Me.HasAura("Weakened Soul")),

                        // keep heal buffs on if glyphed
                        new Decorator(
                            ret => TalentManager.HasGlyph("Dark Binding") || !Me.HasAura("Shadowform"),
                            new PrioritySelector(
                                Spell.BuffSelf("Prayer of Mending", ret => Me.HealthPercent <= 90),
                                Spell.BuffSelf("Renew", ret => Me.HealthPercent <= 90)
                                )
                            ),

                        Spell.Cast("Psychic Scream", ret => PriestSettings.UsePsychicScream
                            && Me.HealthPercent <= PriestSettings.ShadowFlashHealHealth
                            && (Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10) >= PriestSettings.PsychicScreamAddCount || (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds && Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < 8 )))),

                        Spell.Cast("Flash Heal",
                            ctx => Me,
                            ret => Me.HealthPercent <= PriestSettings.ShadowFlashHealHealth),

                        Spell.Cast("Flash Heal",
                            ctx => Me,
                            ret => !Me.Combat && Me.GetPredictedHealthPercent(true) <= 85)
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
                Movement.CreateEnsureMovementStoppedBehavior(35f),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.BuffSelf("Shadowform"),

                        Spell.BuffSelf("Power Word: Shield", ret => PriestSettings.UseShieldPrePull && !Me.HasAura("Weakened Soul")),

                        Spell.Buff("Devouring Plague", true),
                        Spell.Buff("Vampiric Touch", true),
                        Spell.Buff("Shadow Word: Pain", true),

                        Spell.Cast("Holy Fire", ctx => Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow))
                        )
                    ),
                Movement.CreateMoveToTargetBehavior(true, 38f)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Normal)]
        public static Composite CreatePriestShadowNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateEnsureMovementStoppedBehavior(35f),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),

                        CreateShadowDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),
                        Spell.BuffSelf("Shadowform"),

                        // Mana Management stuff - send in the fiends
                        new Decorator(
                            ret => Me.CurrentTarget.TimeToDeath() >= 15 || AoeTargets.Count() > 1,
                            new PrioritySelector(
                                Spell.Cast("Mindbender", ret => Me.ManaPercent <= PriestSettings.MindbenderMana ),
                                Spell.Cast("Shadowfiend", ret => Me.ManaPercent <= PriestSettings.ShadowfiendMana  ) 
                                )
                            ),

                        // Defensive stuff
                        Spell.BuffSelf("Dispersion",
                            ret => Me.ManaPercent < PriestSettings.DispersionMana
                                || Me.HealthPercent < 40 
                                || (Me.ManaPercent < SingularSettings.Instance.MinMana && Me.IsSwimming)
                                || Unit.NearbyUnfriendlyUnits.Count(t => t.GotTarget && t.CurrentTarget.IsTargetingUs()) >= 3),

                        Spell.Cast("Psychic Scream",  ret => PriestSettings.UsePsychicScream && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10) >= PriestSettings.PsychicScreamAddCount),

                        Spell.Cast("Power Infusion", ret => Me.CurrentTarget.TimeToDeath() > 20 || AoeTargets.Count() > 2),
                
                        // don't attempt to heal unless below a certain percentage health
                        Spell.Cast("Vampiric Embrace", ret => Me, ret => Me.HealthPercent < 65 && Me.TimeToDeath() > 10),

                        // Shadow immune npcs.
                        Spell.Cast("Holy Fire", req => Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && AoeTargets.Count() > 1,
                            new PrioritySelector(
                                ctx => AoeTargets.FirstOrDefault(),

                                // halo only if nothing near we aren't already in combat with
                                Spell.Cast("Halo", ret => !Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < 32 && !u.IsTargetingMeOrPet && !u.Fleeing)),
                                Spell.Cast("Mind Sear", mov => true, ctx => (WoWUnit)ctx, ret => AoeTargets.Count() > 5, cancel => Me.HealthPercent < PriestSettings.ShadowFlashHealHealth ),

                                new PrioritySelector(
                                    ctx => AoeTargets.FirstOrDefault(u => !u.HasAllMyAuras("Shadow Word: Pain", "Vampiric Touch")),
                                    Spell.Buff("Vampiric Touch", on => (WoWUnit) on),
                                    Spell.Buff("Shadow Word: Pain", on => (WoWUnit) on)
                                    ),

                                Spell.Cast("Mind Sear", ctx => (WoWUnit) ctx)
                                )
                            ),

                        // for NPCs immune to shadow damage.
                        Spell.Cast("Holy Fire", ctx => Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),

                        // for targets we will fight longer than 10 seconds (it's a guess)
                        new Decorator(
                            ret => Me.CurrentTarget.MaxHealth > (Me.MaxHealth * 2)
                                || Me.CurrentTarget.TimeToDeath() > 10
                                || (Me.CurrentTarget.Elite && Me.CurrentTarget.Level > (Me.Level - 10)),

                            new PrioritySelector(
                                // We don't want to dot targets below 40% hp to conserve mana. Mind Blast/Flay will kill them soon anyway
                                Spell.Cast("Mind Blast", ret => Me.GetCurrentPower(WoWPowerType.ShadowOrbs) < 3),
                                Spell.Buff("Devouring Plague", true, ret => Me.GetCurrentPower(WoWPowerType.ShadowOrbs) >= PriestSettings.NormalContextOrbs ),
                                Spell.Cast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent <= 20),
                                Spell.Cast("Mind Spike", ret => Me.HasAura("Surge of Darkness")),
                                Spell.Buff("Vampiric Touch"),
                                Spell.Buff("Shadow Word: Pain", true, ret => Me.CurrentTarget.Elite || Me.CurrentTarget.HealthPercent > 40),
                                Spell.Cast("Mind Flay", ret => Me.ManaPercent >= PriestSettings.MindFlayMana),
                                Spell.Cast("Mind Blast"),

                                Movement.CreateMoveToTargetBehavior(true, 35f)
                                )
                            ),

                        // for targets that die quickly
                        new PrioritySelector(
                            Spell.Cast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent <= 20),
                            Spell.Buff("Devouring Plague", true, ret => Me.GetCurrentPower(WoWPowerType.ShadowOrbs) >= PriestSettings.NormalContextOrbs ),
                            Spell.Cast("Mind Blast", ret => Me.GetCurrentPower(WoWPowerType.ShadowOrbs) < 3),

                            new Decorator(
                                ret => Me.CurrentTarget.TimeToDeath() > 8,
                                new PrioritySelector(
                                    Spell.Buff("Shadow Word: Pain", true),
                                    Spell.Buff("Vampiric Touch", true)
                                    )
                                ),

                            Spell.Cast("Halo", ret => !Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < 32 && !u.IsTargetingMeOrPet && !u.Fleeing)),

                            Spell.Cast("Mind Spike", ret => Me.HasAura("Surge of Darkness")),
                            Spell.Cast("Mind Flay")
                            )
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 38f)
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
                Movement.CreateEnsureMovementStoppedBehavior(35f),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateShadowDiagnosticOutputBehavior(),

                        Spell.BuffSelf("Shadowform"),

                        // blow-up target (or snipe a kill) when possible 
                        Spell.Cast("Shadow Word: Insanity", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.GetAuraTimeLeft("Shadow Word: Pain", true).TotalMilliseconds.Between(350, 5000) && u.InLineOfSpellSight)),
                        Spell.Cast("Shadow Word: Death", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && u.InLineOfSpellSight)),

                        Helpers.Common.CreateInterruptBehavior(),

                        // Defensive stuff
                        Spell.BuffSelf("Fade", ret => Common.HasTalent(PriestTalent.Phantasm) && (Me.HasAuraWithMechanic(WoWSpellMechanic.Snared) || Unit.NearbyUnfriendlyUnits.Any(u => u.CurrentTargetGuid == Me.Guid && (u.Class == WoWClass.Hunter || u.Class == WoWClass.Mage || u.Class == WoWClass.Warlock || u.Class == WoWClass.Priest || u.Class == WoWClass.Shaman || u.Class == WoWClass.Druid)))),
                        Spell.BuffSelf("Fade", ret => ObjectManager.GetObjectsOfType<WoWUnit>(false,false).Any( p => p.Distance < 10 && p.IsPet )),
                        Spell.BuffSelf("Dispersion", ret => Me.HealthPercent < 40 || Unit.NearbyUnfriendlyUnits.Count(t => t.GotTarget && t.CurrentTarget.IsTargetingUs()) >= 3),
                        Spell.BuffSelf("Psychic Scream", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10*10) >= 1),

                        new Decorator(
                            ret => !Spell.IsSpellOnCooldown( "Psyfiend"),
                            new PrioritySelector(
                                ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => u.CurrentTargetGuid == Me.Guid && !(u.Fleeing || u.Stunned)),
                                Spell.CastOnGround("Psyfiend",
                                    loc => ((WoWUnit)loc).Distance <= 20 ? ((WoWUnit)loc).Location : WoWMovement.CalculatePointFrom( ((WoWUnit)loc).Location, (float) ((WoWUnit)loc).Distance - 20),
                                    req => ((WoWUnit)req) != null,
                                    false
                                    ),
                                Spell.Cast("Psychic Horror", 
                                    on => (WoWUnit)on, 
                                    req => Me.GetCurrentPower(WoWPowerType.ShadowOrbs) == 1
                                        && ((WoWUnit)req).Distance < 30 
                                        && (((WoWUnit)req).Class == WoWClass.Hunter || ((WoWUnit)req).Class == WoWClass.Rogue || ((WoWUnit)req).Class == WoWClass.Warrior || ((WoWUnit)req).Class == WoWClass.DeathKnight || ((WoWUnit)req).HasAnyAura("Inquisition", "Maelstrom Weapon", "Savage Roar"))
                                    )
                                )
                            ),

                        // Offensive

                        // save Halo for combined damage and healing with no crowd control at risk
                        Spell.Cast("Halo", 
                            ret => !Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < 32 && u.IsCrowdControlled())
                                && Unit.NearbyUnfriendlyUnits.Count( u => Me.SpellDistance(u) < 32) > 1
                                && Unit.NearbyFriendlyPlayers.Any( f => f.HealthPercent < 70)),

                        // snipe kills where possible
                        Spell.Cast("Shadow Word: Death", 
                            on => Unit.NearbyUnfriendlyUnits.Where(u => u.HealthPercent < 20 && Me.IsSafelyFacing(u) && u.InLineOfSpellSight ).OrderBy( k => k.HealthPercent).FirstOrDefault(),
                            ret => Me.CurrentTarget.HealthPercent <= 20),

                        // at 3 orbs, time for some burst
                        new Decorator(
                            ret => Me.GetCurrentPower(WoWPowerType.ShadowOrbs) >= 3,
                            new PrioritySelector(
                                Spell.Cast("Mindbender"),
                                Spell.Cast("Power Infusion"),
                                Spell.Cast("Shadowfiend"),
                                Spell.Buff("Devouring Plague")
                                )
                            ),

                        // only cast Mind Spike if its instant
                        Spell.Cast("Mind Spike", ret => Me.HasAura("Surge of Darkness")),

                        Spell.Cast("Mind Blast", ret => Me.GetCurrentPower(WoWPowerType.ShadowOrbs) < 3),

                        // multi-dot to supply procs and mana
                        Spell.Buff("Shadow Word: Pain", true, on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HasAuraExpired("Shadow Word: Pain", 1) && u.InLineOfSpellSight), req => true, 1),
                        Spell.Buff("Vampiric Touch", true, on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HasAuraExpired("Vampiric Touch", 3) && u.InLineOfSpellSight), req => true, 3),

                        Spell.Cast("Mind Flay", mov => true, on => Me.CurrentTarget, req => true, 
                            cancel => {
                                if (Spell.IsGlobalCooldown())
                                    return false;

                                if ( !Spell.IsSpellOnCooldown("Mind Blast") && Spell.GetSpellCastTime("Mind Blast") == TimeSpan.Zero)
                                    Logger.Write(Color.White, "/cancel Mind Flay for instant Mind Blast proc");
                                else if (Me.HasAura("Surge of Darkness"))
                                    Logger.Write(Color.White, "/cancel Mind Flay for instant Mind Spike proc");
                                else if (SpellManager.HasSpell("Shadow Word: Insanity") && Unit.NearbyUnfriendlyUnits.Any(u => u.GetAuraTimeLeft("Shadow Word: Pain", true).TotalMilliseconds.Between(1000, 5000) && u.InLineOfSpellSight ))
                                    Logger.Write(Color.White, "/cancel Mind Flay for Shadow Word: Insanity proc");
                                else if (!Spell.IsSpellOnCooldown("Shadow Word: Death") && Unit.NearbyUnfriendlyUnits.Any(u => u.HealthPercent < 20 && u.InLineOfSpellSight))
                                    Logger.Write(Color.White, "/cancel Mind Flay for Shadow Word: Death proc");
                                else
                                    return false;

                                return true;
                            })
                        )
                    ),
                Movement.CreateMoveToTargetBehavior(true, 38f)
                );
        }

        #endregion

        #region PreCombatBuffs
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Priest, WoWSpec.PriestShadow)]
        public static Composite CreatePriestShadowRest()
        {
            return Spell.BuffSelf("Shadowform");
        }

        #endregion 

        #region Instance Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Instances)]
        public static Composite CreatePriestShadowInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateEnsureMovementStoppedBehavior(35f),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Combat"),
                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateShadowDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Spell.BuffSelf("Shadowform"),

                        // use fade to drop aggro.
                        Spell.Cast("Fade", ret => (Me.GroupInfo.IsInParty || Me.GroupInfo.IsInRaid) && Targeting.GetAggroOnMeWithin(Me.Location, 30) > 0),

                        // Shadow immune npcs.
                        Spell.Cast("Holy Fire", ctx => Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && AoeTargets.Count() > 1,
                            new PrioritySelector(
                                ctx => Me.CurrentTarget,

                                // halo only if nothing near we aren't already in combat with
                                Spell.Cast("Halo", ret => !Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < 32 && (u.IsCrowdControlled() || !u.IsTargetingMeOrPet && !u.IsTargetingMyRaidMember))),
                                Spell.Cast("Mind Sear", mov => true, ctx => (WoWUnit)ctx, ret => AoeTargets.Count() >= 5, cancel => Me.HealthPercent < PriestSettings.ShadowFlashHealHealth ),

                                new PrioritySelector(
                                    ctx => AoeTargets.FirstOrDefault(u => !u.HasAllMyAuras("Shadow Word: Pain", "Vampiric Touch")),
                                    Spell.Buff("Vampiric Touch", on => (WoWUnit) on),
                                    Spell.Buff("Shadow Word: Pain", on => (WoWUnit) on)
                                    ),

                                Spell.Cast("Mind Sear", ctx => (WoWUnit) ctx)
                                )
                            ),

                        // Single target rotation
                        Spell.Buff("Devouring Plague", true, ret => Me.GetCurrentPower(WoWPowerType.ShadowOrbs) >= (Me.CurrentTarget.IsBoss() ? 3 : PriestSettings.NormalContextOrbs )),

                        Spell.Cast("Mind Blast", ret => Me.GetCurrentPower(WoWPowerType.ShadowOrbs) < 3 || Me.HasAura("Divine Insight")),

                        Spell.Cast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent <= 20),

                        Spell.Cast("Halo", ret => !Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < 32 && (u.IsCrowdControlled() || !u.IsTargetingMeOrPet && !u.IsTargetingMyRaidMember))),

                        Spell.Cast("Mind Spike", ret => Me.HasAura("Surge of Darkness")),
                        Spell.Cast("Mindbender"),
                        Spell.Cast("Power Infusion"),
                        Spell.Buff("Vampiric Touch", true, on => Me.CurrentTarget, req => true, 3),
                        Spell.Buff("Shadow Word: Pain", true, on => Me.CurrentTarget, req => true, 3),
                        Spell.Cast("Shadowfiend", ret => Me.ManaPercent <= PriestSettings.ShadowfiendMana && Me.CurrentTarget.TimeToDeath() > 30), // Mana check is for mana management. Don't mess with it
                        Spell.Cast("Mind Flay", ret => Me.ManaPercent >= PriestSettings.MindFlayMana)
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 38f)
                );
        }

        public static int NumAoeTargets { get; set; }

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

                        string line = string.Format(".... h={0:F1}%/m={1:F1}%, moving={2}, form={3}, orbs={4}, surgdark={5}, divinsight={6}",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.IsMoving,
                            Me.Shapeshift,
                            orbs,
                            (long)Me.GetAuraTimeLeft("Surge of Darkness", true).TotalMilliseconds,
                            (long)Me.GetAuraTimeLeft("Divine Insight", true).TotalMilliseconds
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target == null)
                            line += ", target=(null)";
                        else
                            line += string.Format(", target={0} @ {1:F1} yds, th={2:F1}%, tface={3}, tloss={4}, sw:p={5}, vamptch={6}, devplague={7}",
                                target.SafeName(),
                                target.Distance,
                                target.HealthPercent,
                                Me.IsSafelyFacing(target,70f),
                                target.InLineOfSpellSight,
                                (long)target.GetAuraTimeLeft("Shadow Word: Pain", true).TotalMilliseconds,
                                (long)target.GetAuraTimeLeft("Vampiric Touch", true).TotalMilliseconds,
                                (long)target.GetAuraTimeLeft("Devouring Plague", true).TotalMilliseconds
                                );

                        Logger.WriteDebug(Color.Wheat, line);
                        return RunStatus.Success;
                    }))
                );
        }

        #endregion
    }
}
