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
using Singular.Utilities;
using Styx.CommonBot.Profiles;

namespace Singular.ClassSpecific.Priest
{
    public class Shadow
    {
        const int SURGE_OF_DARKNESS = 87160;

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PriestSettings PriestSettings { get { return SingularSettings.Instance.Priest(); } }

        private static bool InVoidform => VoidformStacks > 0;

	    private static uint VoidformStacks => Me.GetAllAuras().Where(a => a.Name == "Voidform").Select(a => a.StackCount).DefaultIfEmpty(0u).Max();

		[Behavior(BehaviorType.Initialize, WoWClass.Priest, WoWSpec.PriestShadow)]
        public static Composite CreateShadowInitialize()
        {
            if (SpellManager.HasSpell("Shadow Mend"))
            {
                Logger.Write(LogColor.Init, "Shadow Mend: if all enemies cc'd and health below {0}%", PriestSettings.ShadowHeal);
            }

            if (SpellManager.HasSpell("Psychic Scream"))
            {
                if (!PriestSettings.PsychicScreamAllow)
                    Logger.Write(LogColor.Init, "Psychic Scream: disabled by user setting");
                else
                {
                    Logger.Write(LogColor.Init, "Psychic Scream: cast when health falls below {0}%", PriestSettings.PsychicScreamHealth);
                    if (TalentManager.HasGlyph("Psychic Scream"))
                        Logger.Write(LogColor.Init, "Psychic Scream: cast when 2 or more mobs attacking (glyphed)");
                    else
                        Logger.Write(LogColor.Init, "Psychic Scream: cast when {0} or more mobs attacking", PriestSettings.PsychicScreamAddCount);
                }
            }

            return null;
        }

        [Behavior(BehaviorType.Rest, WoWClass.Priest, WoWSpec.PriestShadow)]
        public static Composite CreateShadowPriestRestBehavior()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Dispersion", ret => Me.ManaPercent < SingularSettings.Instance.MinMana && Me.IsSwimming ),
                Rest.CreateDefaultRestBehaviour("Shadow Mend", "Resurrection"),
                Common.CreatePriestMovementBuffOnTank("Rest")
                );

        }

        /// <summary>
        /// perform diagnostic output logging at highest priority behavior that occurs while in Combat
        /// </summary>
        /// <returns></returns>
        [Behavior(BehaviorType.Heal | BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.All, 999)]
        public static Composite CreateShadowLogDiagnostic()
        {
            return CreateShadowDiagnosticOutputBehavior();
        }


        [Behavior(BehaviorType.Heal, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Normal | WoWContext.Battlegrounds )]
        public static Composite CreateShadowHeal()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Power Word: Shield", ret => Me.HealthPercent < PriestSettings.PowerWordShield && !Me.HasAura("Weakened Soul")),

                Common.CreatePsychicScreamBehavior(),
                Spell.Cast(
                    "Shadow Mend",
                    ctx => Me,
                    ret => {
                        if (Me.HealthPercent > PriestSettings.ShadowHeal)
                            return false;

                        if (Unit.UnitsInCombatWithMeOrMyStuff(40).Any(u => !u.IsCrowdControlled()))
                            return false;

                        return true;
                    }
                )
            );
        }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestShadow)]
        public static Composite CreatePriestShadowNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                // grinding or questing, if target meets either of these cast instant if possible
                // 1. mob is less than 12 yds, so no benefit from delay in Lightning Bolt missile arrival
                // 2. area has another player competing for mobs (we want to tag the mob quickly)
                        new Decorator(
                            ret =>
                            {
                                if (StyxWoW.Me.CurrentTarget.IsHostile && StyxWoW.Me.CurrentTarget.Distance < 12)
                                {
                                    Logger.Write(LogColor.Hilite, "Pull with Instant since hostile target is {0:F1} yds away", StyxWoW.Me.CurrentTarget.Distance);
                                    return true;
                                }
                                WoWPlayer nearby = ObjectManager.GetObjectsOfType<WoWPlayer>(true, false).FirstOrDefault(p => !p.IsMe && p.SpellDistance(Me.CurrentTarget) <= 40);
                                if (nearby != null)
                                {
                                    Logger.Write(LogColor.Hilite, "Pull with Instant since player {0} nearby @ {1:F1} yds", nearby.SafeName(), nearby.Distance);
                                    return true;
                                }
                                Logger.WriteDiagnostic("Pull with normal rotation since no urgency");
                                return false;
                            },

                            new PrioritySelector(
                                Spell.Buff("Shadow Word: Pain", true)
                                )
                            ),

                        Spell.BuffSelf("Power Word: Shield", ret => PriestSettings.UseShieldPrePull && !Me.HasAura("Weakened Soul")),
                        
                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),
                        
                        Spell.Buff("Vampiric Touch", true),
                        Spell.Buff("Shadow Word: Pain", true)

                        // Spell.Cast("Holy Fire", ctx => Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow))
                        )
                    )
                );
        }

        /**
         *  The single target rotation relies on the following priority system of abilities, depending on which phase you are in.

            Build Phase (not in Voidform Icon Voidform)
            Cast Void Eruption Icon Void Eruption (to enter Voidform).
            Cast Mind Blast Icon Mind Blast.
            Apply and maintain Shadow Word: Pain Icon Shadow Word: Pain and Vampiric Touch Icon Vampiric Touch.
            Cast Mind Flay Icon Mind Flay as your filler spell.
            Spend/Maintain Phase (in Voidform Icon Voidform)
            Cast Void Bolt Icon Void Bolt.
            Cast Shadowfiend Icon Shadowfiend if available at low Voidform stacks.
            Cast Shadow Word: Death Icon Shadow Word: Death
            Always cast Shadow Word: Death once when you have 2 stacks.
            Do not cast Shadow Word: Death if you have 1 stack and are not in danger of falling out of Voidform AND if Mind Blast Icon Mind Blast is off cooldown.
            Cast Mind Blast Icon Mind Blast.
            Cast Shadowfiend Icon Shadowfiend if available at higher Voidform stacks.
            Re-apply Shadow Word: Pain Icon Shadow Word: Pain and Vampiric Touch Icon Vampiric Touch if they fall off of your target.
            Cast Mind Flay Icon Mind Flay as your filler spell.
         * 
         **/

        [Behavior(BehaviorType.Combat, WoWClass.Priest, WoWSpec.PriestShadow)]
        public static Composite CreatePriestShadowNormalCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(),
				CreateFightAssessment(),
				new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),
                        
                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        new Decorator(
                            req => !Me.CurrentTarget.IsTrivial(),
                            new PrioritySelector(

                                Dispelling.CreatePurgeEnemyBehavior("Dispel Magic"),

                                // Mana Management stuff - send in the fiends
                                Common.CreateShadowfiendBehavior(),

                                // Defensive stuff
                                Spell.BuffSelf("Dispersion",
                                    ret => Me.ManaPercent < PriestSettings.DispersionMana
                                        || Me.HealthPercent < 40
                                        || (Me.ManaPercent < SingularSettings.Instance.MinMana && Me.IsSwimming)
                                        || Unit.NearbyUnfriendlyUnits.Count(t => t.GotTarget() && t.CurrentTarget.IsTargetingMyStuff()) >= 3),

                                Common.CreatePriestShackleUndeadAddBehavior(),
                                Common.CreatePsychicScreamBehavior(),

                                Spell.HandleOffGCD(
                                    new PrioritySelector(
                                        ctx => Me.CurrentTarget.TimeToDeath() > 15 || cntAoeTargets > 1,
                                        new Sequence(
                                            Spell.BuffSelfAndWait(sp => "Vampiric Embrace", req => ((bool)req) && Me.HealthPercent < PriestSettings.VampiricEmbracePct),
                                            Spell.BuffSelf("Power Infusion")
                                            ),
                                        Spell.Cast("Power Infusion", req => ((bool)req) || Me.HasAura("Vampiric Embrace"))
                                        )
                                    )
                                )
                            ),

                        // Shadow immune npcs.
                // Spell.Cast("Holy Fire", req => Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && cntAoeTargets > 1,
                            new PrioritySelector(
                                ctx => AoeTargets.FirstOrDefault(u => Me.IsSafelyFacing(u) && u.InLineOfSpellSight),

                                Spell.Cast("Void Eruption", when => !InVoidform),

                                Spell.Cast(
                                    "Mind Sear",
                                    mov => true,
                                    on =>
                                    {
                                        IEnumerable<WoWUnit> aoeTargetsFacing = AoeTargets.Where(u => Me.IsSafelyFacing(u) && u.InLineOfSpellSight && u.IsTrivial());
                                        WoWUnit unit = Clusters.GetBestUnitForCluster(aoeTargetsFacing, ClusterType.Radius, 10);
                                        if (unit == null)
                                            return null;
                                        if (3 > Clusters.GetClusterCount(unit, AoeTargets, ClusterType.Radius, 10))
                                            return null;
                                        if (Unit.UnfriendlyUnits(50).Any(u => !u.IsTrivial() && unit.SpellDistance(u) < 12))
                                            return null;

                                        Logger.Write(LogColor.Hilite, "^Trivial Farming: all trivial mobs within 12 yds of {0}", unit.SafeName());
                                        return unit;
                                    },
                                    cancel => Me.HealthPercent < PriestSettings.ShadowHeal
                                    ),
                                
                                Spell.Buff("Shadow Word: Pain", true),      // no multi message for current target
                                Spell.Buff(                                 // multi-dot others w/ message
                                    "Shadow Word: Pain", 
                                    true, 
                                    on => 
                                    {
                                        WoWUnit dotTarget = AoeTargets.FirstOrDefault(u => u != Me.CurrentTarget && !u.HasMyAura("Shadow Word: Pain") && u.InLineOfSpellSight && !Spell.DoubleCastContains(u, "Shadow Word: Pain"));
                                        if (dotTarget != null && Spell.CanCastHack("Shadow Word: Pain", dotTarget))
                                        {
                                            Logger.Write(LogColor.Hilite, "^Multi-DoT: cast Shadow Word: Pain on {0}", dotTarget.SafeName());
                                            return dotTarget;
                                        }
                                        return null;
                                    }, 
                                    req => true
                                    ),
                                
                                // When we enter void form, even if AOE, we use our single target rotation after maintaining debuffs.
                                new Decorator(ret => InVoidform,
                                    CreateMaintainVoidformBehaviour()),

                                // filler spell
                                Spell.Cast(
                                    "Mind Sear",
                                    mov => true,
                                    on => {
                                        IEnumerable<WoWUnit> AoeTargetsFacing = AoeTargets.Where(u => Me.IsSafelyFacing(u) && u.InLineOfSpellSight);
                                        WoWUnit unit = Clusters.GetBestUnitForCluster( AoeTargetsFacing, ClusterType.Radius, 10);
                                        if (unit == null)
                                            return null;
                                        if ( 4 > Clusters.GetClusterCount(unit, AoeTargets, ClusterType.Radius, 10))
                                            return null;
                                        return unit;
                                        },
                                    cancel => Me.HealthPercent < PriestSettings.ShadowHeal
                                    ),
                                CastMindFlay(on => (WoWUnit)on, req => cntAoeTargets < 4)
                                )
                            ),
                        

                        new Decorator(ret => InVoidform,
                            CreateMaintainVoidformBehaviour()),


                        CreateBuildVoidformBehaviour()
                        )
                    )
                );
        }

        private static Composite CreateBuildVoidformBehaviour()
        {
            return new PrioritySelector(
                Spell.Cast("Void Eruption"),
                Spell.Cast("Mind Blast"),
                Spell.Buff("Shadow Word: Pain", on => Me.CurrentTarget),
                Spell.Buff("Vampiric Touch", on => Me.CurrentTarget),
                Spell.Cast("Mind Flay", when => AoeTargets.Count <= 2)
                );
        }

        private static Composite CreateMaintainVoidformBehaviour()
        {
            return new PrioritySelector(
                // Artifact Weapon
                new Decorator(
                    ret => PriestSettings.UseArtifactOnlyInAoE && Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() > 1,
                        new PrioritySelector(
                            Spell.Cast("Void Torrent", ret =>
                                    PriestSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown
                                    || ( PriestSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.AtHighestDPSOpportunity
                                        && Me.CurrentTarget.GetAuraTimeLeft("Shadow Word: Pain") >= TimeSpan.FromSeconds(6)
                                        && Me.CurrentTarget.GetAuraTimeLeft("Vampiric Touch") >= TimeSpan.FromSeconds(6) )
                            )
                        )
                ),
                Spell.Cast("Void Torrent", ret =>
                    !PriestSettings.UseArtifactOnlyInAoE &&
                    ( PriestSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown
                    || ( PriestSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.AtHighestDPSOpportunity
                        && Me.CurrentTarget.GetAuraTimeLeft("Shadow Word: Pain") >= TimeSpan.FromSeconds(6)
                        && Me.CurrentTarget.GetAuraTimeLeft("Vampiric Touch") >= TimeSpan.FromSeconds(6) ) )
                ),

                Spell.Cast("Void Eruption", when => InVoidform), // This is for casting Void Bolt, but something is causing Singular to fail casting it.
                Spell.Cast("Shadowfiend", when => VoidformStacks < 20),
                Spell.Cast("Shadow Word: Death", when => Me.GetAuraStacks("Shadow Word: Death") == 2 || (VoidformStacks < 10 && !Spell.CanCastHack("Mind Blast"))),
                Spell.Cast("Mind Blast"),
                Spell.Cast("Shadowfiend"),
                Spell.Cast("Mind Flay", when => AoeTargets.Count <= 2)
                );
        }
        
        #endregion

        static int cntAoeTargets { get; set; }
        static int cntCC { get; set; }
        static int cntAvoid { get; set; }

        static List<WoWUnit> AoeTargets { get; set; }

        /// <summary>
        /// creates a behavior which will populate list AoeTargets that we 
        /// can safely attack.  will also populate cntAoeTargets, cntCC,
        /// and cntAvoid appropriately.  if avoid mob or cc detected, then
        /// AoeTargets will contain list of mobs we can attack, but cntAoeTargtets
        /// will be 1 indicating no AOE spells should be used
        /// </summary>
        /// <returns>behavior</returns>
        static Composite CreateFightAssessment()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                return new ActionAlwaysFail();

            return new Action( r => {

                if (Me.GotTarget())
                    Me.CurrentTarget.TimeToDeath();

                cntAoeTargets = 0;
                cntCC = 0;

                AoeTargets = new List<WoWUnit>();
                foreach (var u in Unit.UnfriendlyUnits(40))
                {
                    if (u.IsCrowdControlled())
                        cntCC++;
                    else if (u.IsAvoidMob())
                        cntAvoid++;
                    else
                    {
                        // abort AOE if enemy player involved
                        if (u.IsPlayer)
                        {
                            cntAoeTargets = 1;
                            cntCC = 0;
                            AoeTargets = new List<WoWUnit>();
                            AoeTargets.Add(u);
                            break;
                        }

                        // count AOE targets
                        if (u.Combat && (u.Aggro || u.PetAggro || u.TaggedByMe || u.IsTargetingUs()))
                        {
                            cntAoeTargets++;
                            AoeTargets.Add(u);
                        }
                    }
                }

                if (cntCC > 0 || cntAvoid > 0)
                    cntAoeTargets = 1;

                return RunStatus.Failure;
            });
        }
		
        static Composite CastMindFlay( UnitSelectionDelegate onUnit = null, SimpleBooleanDelegate requirements = null)
        {
            UnitSelectionDelegate o = onUnit ?? (on => Me.CurrentTarget);
            SimpleBooleanDelegate r;

            if (requirements == null)
                r = req => Me.ManaPercent > PriestSettings.MindFlayManaPct;
            else
                r = req => Me.ManaPercent > PriestSettings.MindFlayManaPct && requirements(req);

            return Spell.Cast("Mind Flay", 
                mov => true, 
                o, 
                r,
                cancel =>
                {
                    if (Spell.IsGlobalCooldown())
                        return false;

                    if (!Spell.IsSpellOnCooldown("Mind Blast") && Spell.GetSpellCastTime("Mind Blast") == TimeSpan.Zero)
                        Logger.Write(LogColor.Cancel, "/cancel Mind Flay for instant Mind Blast proc");
                    else if (Me.HasAura(SURGE_OF_DARKNESS))
                        Logger.Write(LogColor.Cancel, "/cancel Mind Flay for instant Mind Spike proc");
                    else if (SpellManager.HasSpell("Shadow Word: Insanity") && Unit.NearbyUnfriendlyUnits.Any(u => u.GetAuraTimeLeft("Shadow Word: Pain", true).TotalMilliseconds.Between(1000, 5000) && u.InLineOfSpellSight))
                        Logger.Write(LogColor.Cancel, "/cancel Mind Flay for Shadow Word: Insanity proc");
                    else if (!Spell.IsSpellOnCooldown("Shadow Word: Death") && Unit.NearbyUnfriendlyUnits.Any(u => u.HealthPercent < 20 && u.InLineOfSpellSight))
                        Logger.Write(LogColor.Cancel, "/cancel Mind Flay for Shadow Word: Death proc");
                    else
                        return false;

                    return true;
                });

        }

        /// <summary>
        /// checks for chained cascade targets.  this is expensive as the theoretical linked distance is 200 yds
        /// if all 4 hops occur and mobs are in straight line.  regardless, this ability can easily aggro the 
        /// entire countryside while questing, so we will be conservative in our assessment of whether it can
        /// be used or not while still maximizing the number of mobs hit based upon the initial targets proximity
        /// to linked mobs
        /// </summary>
        /// <returns></returns>
        public static WoWUnit GetBestCascadeTarget()
        {
            if (!Spell.CanCastHack("Cascade", Me, skipWowCheck: true))
            {
                Logger.WriteDebug("GetBestCascadeTarget: CanCastHack says NO to Cascade");
                return null;
            }

            // search players we are facing in range for the number of hops they represent
            var targetInfo = Unit.UnfriendlyUnits()
                .Where(u => u.SpellDistance() < 40 && Me.IsSafelyFacing(u) && u.InLineOfSpellSight)
                .Select(p => new { Unit = p, Count = Clusters.GetChainedClusterCount(p, Unit.UnfriendlyUnits(), 40f, AvoidAttackingTarget) })
                .OrderByDescending(v => v.Count)                    // maximize count
                .OrderByDescending(v => (int)v.Unit.DistanceSqr)   // maximize initial distance
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (targetInfo == null)
            {
                Logger.WriteDiagnostic("GetBestCascadeTarget:  0 mobs without unwanted aggro / cc break");
                return null;
            }

            Logger.WriteDiagnostic(
                "GetBestCascadeTarget: {0} will hit {1} mobs without unwanted aggro / cc break",
                targetInfo.Unit.SafeName(),
                targetInfo.Count
                );
            return targetInfo.Unit;
        }

        /// <summary>
        /// checks for chained cascade targets.  this is expensive as the theoretical linked distance is 200 yds
        /// if all 4 hops occur and mobs are in straight line.  regardless, this ability can easily aggro the 
        /// entire countryside while questing, so we will be conservative in our assessment of whether it can
        /// be used or not while still maximizing the number of mobs hit based upon the initial targets proximity
        /// to linked mobs
        /// </summary>
        /// <returns></returns>
        public static bool UseDivineStar()
        {
            if (!Spell.CanCastHack("Divine Star", Me.CurrentTarget, skipWowCheck: true))
            {
                return false;
            }

            WoWPoint endPoint = WoWPoint.RayCast( Me.Location, Me.RenderFacing, 26);
            List<WoWUnit> hitByDS = Clusters.GetPathToPointCluster(endPoint, Unit.UnfriendlyUnits(26), 4).ToList();

            if (hitByDS == null || !hitByDS.Any())
            {
                Logger.WriteDiagnostic("UseDivineStar:  0 mobs would be hit");
                return false;
            }

            WoWUnit avoid = hitByDS.FirstOrDefault(u => u.IsAvoidMob());
            if (avoid != null)
            {
                Logger.WriteDiagnostic(
                    "UseDivineStar: skipping to avoid hitting {0} - aggr:{1} cc:{2} avdmob:{3}", 
                    avoid.SafeName(),
                    (avoid.Aggro || avoid.PetAggro).ToYN(),
                    avoid.IsCrowdControlled().ToYN(),
                    avoid.IsAvoidMob().ToYN()
                    );
                return false;
            }

            return false;
        }

        public static bool UseHalo()
        {
            if (!Spell.CanCastHack("Halo", Me.CurrentTarget, skipWowCheck: true))
            {
                return false;
            }

            List<WoWUnit> hitByHalo = Unit.NearbyUnfriendlyUnits.Where(u => Me.SpellDistance(u) < 34).ToList();

            if (hitByHalo == null || !hitByHalo.Any()) 
            {
                Logger.WriteDiagnostic("UseHalo:  0 mobs hit");
                return false;
            }

            WoWUnit avoid = hitByHalo.FirstOrDefault(u => u.IsAvoidMob());
            if (avoid != null)
            {
                Logger.WriteDiagnostic(
                    "UseHalo: skipping to avoid hitting {0} - aggr:{1} cc:{2} avdmob:{3}", 
                    avoid.SafeName(),
                    (avoid.Aggro || avoid.PetAggro).ToYN(),
                    avoid.IsCrowdControlled().ToYN(),
                    avoid.IsAvoidMob().ToYN()
                    );
                return false;
            }

            return false;
        }

        /// <summary>
        /// checks if a WoWUnit (passed as object for use as delegate) represents a mob
        /// we want to avoid hitting when already in combat
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        private static bool AvoidAttackingTarget( object ctx)
        {
            WoWUnit unit = (WoWUnit)ctx;
            if (unit.IsTrivial())
                return false;

            if (unit.Combat && (unit.TaggedByMe || unit.Aggro || unit.PetAggro || unit.IsTargetingUs()))
            {
                if (unit.IsAvoidMob())
                    return true;
                if (unit.IsCrowdControlled())
                    return true;
                return false;
            }
                
            return true;
        }

        #region Diagnostics

        private static Composite CreateShadowDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    string line = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, moving={3}, form={4}, SoD={5}, divins={6}, aoe={7}",
                        CompositeBuilder.CurrentBehaviorType.ToString(),
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving,
                        Me.Shapeshift,
                        (long)Me.GetAuraTimeLeft(SURGE_OF_DARKNESS, true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Divine Insight", true).TotalMilliseconds,
                        cntAoeTargets 
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target == null)
                        line += ", target=(null)";
                    else
                        line += string.Format(", target={0} @ {1:F1} yds, th={2:F1}%, ttd={3}, tface={4}, tloss={5}, sw:p={6}, vamptch={7}, devplague={8}",
                            target.SafeName(),
                            target.Distance,
                            target.HealthPercent,
                            target.TimeToDeath(),
                            Me.IsSafelyFacing(target),
                            target.InLineOfSpellSight,
                            (long)target.GetAuraTimeLeft("Shadow Word: Pain", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Vampiric Touch", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Devouring Plague", true).TotalMilliseconds
                            );

                    Logger.WriteDebug(Color.Wheat, line);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }
}
