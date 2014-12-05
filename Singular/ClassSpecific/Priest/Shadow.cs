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
        const int SURGE_OF_DARKNESS = 87160;

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PriestSettings PriestSettings { get { return SingularSettings.Instance.Priest(); } }

        #region Orbs
        private static uint OrbCount        { get { return Me.GetCurrentPower(WoWPowerType.ShadowOrbs); } }
        private static uint MaxOrbs         { get; set; }
        private static bool NeedMoreOrbs    { get { return OrbCount < MaxOrbs; } }
        #endregion 

        [Behavior(BehaviorType.Initialize, WoWClass.Priest, WoWSpec.PriestShadow)]
        public static Composite CreateShadowInitialize()
        {
            MaxOrbs = Me.GetMaxPower(WoWPowerType.ShadowOrbs);

            if (SpellManager.HasSpell("Flash Heal"))
            {
                Logger.Write(LogColor.Init, "Flash Heal: if all enemies cc'd and health below {0}%", PriestSettings.ShadowFlashHeal);
            }

            if (SpellManager.HasSpell("Prayer of Mending"))
            {
                if (PriestSettings.PrayerOfMending == 0)
                    Logger.Write(LogColor.Init, "Prayer of Mending: disabled by user setting");
                else
                    Logger.Write(LogColor.Init, "Prayer of Mending: if all enemies cc'd and health below {0}%", PriestSettings.PrayerOfMending);
            }

            if (SpellManager.HasSpell("Psychic Scream"))
            {
                if (!PriestSettings.PsychicScreamAllow)
                    Logger.Write(LogColor.Init, "Psychic Scream: disabled by user setting");
                else
                { 
                    Logger.Write("Psychic Scream: cast when health falls below {0}%", PriestSettings.PsychicScreamHealth);
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
                Rest.CreateDefaultRestBehaviour("Flash Heal", "Resurrection"),
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
                Spell.Cast("Desperate Prayer", ret => Me, ret => Me.HealthPercent < PriestSettings.DesperatePrayerHealth),

                Spell.BuffSelf("Power Word: Shield", ret => (Me.HealthPercent < PriestSettings.PowerWordShield || (!Me.HasAura("Shadowform") && SpellManager.HasSpell("Shadowform"))) && !Me.HasAura("Weakened Soul")),

                // keep heal buffs on if (glyph no longer required)
                Spell.BuffSelf("Prayer of Mending", ret => Me.HealthPercent <= PriestSettings.PrayerOfMending),

                Common.CreatePsychicScreamBehavior(),

                Spell.Cast(
                    "Prayer of Mending",
                    ctx => Me,
                    ret => {
                        if (!Me.Combat)
                            return false;

                        if (Me.HealthPercent > PriestSettings.PrayerOfMending)
                            return false;

                        if (!Unit.UnitsInCombatWithUsOrOurStuff(40).All( u => u.IsCrowdControlled()))
                            return false;

                        return true;
                        }
                    ),

                Spell.Cast(
                    "Flash Heal",
                    ctx => Me,
                    ret => {
                        if (!Me.Combat)
                            return Me.PredictedHealthPercent(includeMyHeals: true) > 85;

                        if (Me.HealthPercent > PriestSettings.ShadowFlashHeal)
                            return false;

                        if (!Unit.UnitsInCombatWithUsOrOurStuff(40).All( u => u.IsCrowdControlled()))
                            return false;

                        return true;
                        }
                    )
                );
        }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Normal)]
        public static Composite CreatePriestShadowNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(FaceDuring.Yes),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.BuffSelf("Power Word: Shield", ret => PriestSettings.UseShieldPrePull && !Me.HasAura("Weakened Soul")),

                        Spell.BuffSelf("Shadowform"),

                        Spell.Buff("Devouring Plague", req => OrbCount >= 3),
                        Spell.Cast("Mind Blast", req => OrbCount < MaxOrbs),
                        Spell.Buff("Vampiric Touch", true),
                        Spell.Buff("Shadow Word: Pain", true)

                        // Spell.Cast("Holy Fire", ctx => Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow))
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Priest, WoWSpec.PriestShadow, WoWContext.Normal)]
        public static Composite CreatePriestShadowNormalCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),

                        Spell.BuffSelf("Shadowform"),

                        new Decorator(
                            req => !Unit.IsTrivial( Me.CurrentTarget),
                            new PrioritySelector(

                                Helpers.Common.CreateInterruptBehavior(),
                                Dispelling.CreatePurgeEnemyBehavior("Dispel Magic"),

                                // Mana Management stuff - send in the fiends
                                Common.CreateShadowfiendBehavior(),

                                // Defensive stuff
                                Spell.BuffSelf("Dispersion",
                                    ret => Me.ManaPercent < PriestSettings.DispersionMana
                                        || Me.HealthPercent < 40 
                                        || (Me.ManaPercent < SingularSettings.Instance.MinMana && Me.IsSwimming)
                                        || Unit.NearbyUnfriendlyUnits.Count(t => t.GotTarget && t.CurrentTarget.IsTargetingMyStuff()) >= 3),

                                Common.CreatePsychicScreamBehavior(),

                                Spell.Cast("Power Infusion", ret => Me.CurrentTarget.TimeToDeath() > 20 || AoeTargets.Count() > 2),
                
                                // don't attempt to heal unless below a certain percentage health
                                Spell.Cast(
                                    "Vampiric Embrace", 
                                    ret => Me, 
                                    ret => Me.HealthPercent < PriestSettings.VampiricEmbracePct 
                                        && ( Me.CurrentTarget.TimeToDeath() > 15 || AoeTargets.Count() > 1)
                                    )
                                )
                            ),

                        // Shadow immune npcs.
                        // Spell.Cast("Holy Fire", req => Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && AoeTargets.Count() > 1,
                            new PrioritySelector(
                                ctx => AoeTargets.FirstOrDefault(),

                                // cast on highest health mob (for greatest heal)
                                Spell.Buff(
                                    "Devouring Plague", 
                                    true,
                                    on => AoeTargets
                                        .Where( u => u != null && u.IsValid && u.IsAlive && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && !u.HasMyAura("Devouring Plague"))
                                        .OrderByDescending( u => (int) u.HealthPercent)
                                        .FirstOrDefault(),
                                    ret => OrbCount >= 3
                                    ),

                                Spell.Cast("Mind Blast", on => Me.CurrentTarget, req => OrbCount < MaxOrbs, cancel => false),
                                Spell.Cast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent <= 20 && OrbCount < MaxOrbs),

                                // halo only if nothing near we aren't already in combat with
                                Spell.Cast("Halo", 
                                    ret => AoeTargets.Count() >= 4 
                                        && Unit.NearbyUnfriendlyUnits.All(u => Me.SpellDistance(u) < 34 && !u.IsCrowdControlled() && u.Combat && (u.IsTargetingMeOrPet || u.IsTargetingMyRaidMember))
                                    ),
                                
                                new PrioritySelector(
                                    ctx => AoeTargets.FirstOrDefault(u => !u.HasAllMyAuras("Shadow Word: Pain", "Vampiric Touch")),
                                    Spell.Buff("Shadow Word: Pain", on => (WoWUnit) on),
                                    Spell.Buff("Vampiric Touch", on => (WoWUnit) on)
                                    ),

                                Spell.Cast( 
                                    sp => AoeTargets.Count() < 4 ? "Mind Flay" : "Mind Sear", 
                                    mov => true, 
                                    on => (WoWUnit)on, 
                                    cancel => Me.HealthPercent < PriestSettings.ShadowFlashHeal
                                    )
                                )
                            ),

                        // for NPCs immune to shadow damage.
                        // Spell.Cast("Holy Fire", ctx => Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),

                        // for targets we will fight longer than 10 seconds (it's a guess)
                        new Decorator(
                            ret => Me.GotTarget &&
                                ( Me.CurrentTarget.MaxHealth > (Me.MaxHealth * 2)
                                || Me.CurrentTarget.TimeToDeath() > 10
                                || (Me.CurrentTarget.Elite && Me.CurrentTarget.Level > (Me.Level - 10))),

                            new PrioritySelector(
                                // We don't want to dot targets below 40% hp to conserve mana. Mind Blast/Flay will kill them soon anyway
#if old
                                Spell.Cast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent <= 20 && OrbCount < MaxOrbs),
                                Spell.Cast("Mind Spike", ret => Me.HasAura(SURGE_OF_DARKNESS)),
                                Spell.Cast("Mind Blast", on => Me.CurrentTarget, req => OrbCount < MaxOrbs, cancel => false),
                                Spell.Buff("Devouring Plague", true, ret => OrbCount >= 3),
                                Spell.Buff("Vampiric Touch"),
                                Spell.Buff("Shadow Word: Pain", true, ret => Me.CurrentTarget.Elite || Me.CurrentTarget.HealthPercent > 40),
                                Spell.Cast("Mind Flay", mov => true, on => Me.CurrentTarget, req => Me.ManaPercent >= PriestSettings.MindFlayManaPct, cancel => false )
#else
                                Spell.Buff("Devouring Plague", true, ret => OrbCount >= 3),
                                Spell.Cast("Mind Blast", on => Me.CurrentTarget, req => OrbCount < MaxOrbs, cancel => false),
                                Spell.Cast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent <= 20 && OrbCount < MaxOrbs),
                                CastInsanity(),
                                Spell.Cast("Mind Spike", ret => Me.HasAura(SURGE_OF_DARKNESS)),
                                Spell.Buff("Vampiric Touch", true, ret => Me.CurrentTarget.Elite || Me.CurrentTarget.HealthPercent > 40 || Me.CurrentTarget.TimeToDeath() > 7),
                                Spell.Buff("Shadow Word: Pain", true, ret => Me.CurrentTarget.Elite || Me.CurrentTarget.HealthPercent > 40 || Me.CurrentTarget.TimeToDeath() > 7),
                                Spell.Cast("Mind Flay", mov => true, on => Me.CurrentTarget, req => Me.ManaPercent >= PriestSettings.MindFlayManaPct, cancel => false)
#endif
                                )
                            ),

                        // for targets that die quickly
                        new PrioritySelector(
                            Spell.Cast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent <= 20 && OrbCount < MaxOrbs),
                            Spell.Cast("Mind Spike", ret => Me.HasAura(SURGE_OF_DARKNESS)),
                            Spell.Cast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent <= 20),
                            Spell.Cast("Mind Blast", on => Me.CurrentTarget, req => OrbCount < MaxOrbs, cancel => false),

                            CastInsanity(),

                            Spell.Buff("Devouring Plague", true, ret => !Unit.IsTrivial(Me.CurrentTarget) && OrbCount >= 3),

                            new Decorator(
                                ret => Me.GotTarget && Me.CurrentTarget.TimeToDeath() > 8 && !Unit.IsTrivial(Me.CurrentTarget),
                                new PrioritySelector(
                                    Spell.Buff("Shadow Word: Pain", true),
                                    Spell.Buff("Vampiric Touch", true)
                                    )
                                ),

                            Spell.Cast("Halo", 
                                ret => Unit.NearbyUnfriendlyUnits.All(u => Me.SpellDistance(u) < 34 && !u.IsCrowdControlled() && u.Combat && (u.IsTargetingMeOrPet || u.IsTargetingMyRaidMember))),                           

                            CastMindFlay()
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
            Kite.CreateKitingBehavior(Common.CreateSlowMeleeBehavior(), Common.CreatePriestMovementBuff(), null);

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Spell.BuffSelf("Shadowform"),

                        // blow-up target (or snipe a kill) when possible 
                        Spell.Cast("Shadow Word: Death", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && Me.IsSafelyFacing(u) && u.InLineOfSpellSight)),

                        // don't attempt to heal unless below a certain percentage health
                        Spell.Cast(
                            "Vampiric Embrace",
                            ret => Me,
                            ret => Me.CurrentTarget.TimeToDeath(-1) > 12
                                && Unit.NearbyGroupMembers.Any(u => u.HealthPercent < PriestSettings.VampiricEmbracePct)
                            ),

                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Dispel Magic"),

                        // Defensive stuff
                        Common.CreateFadeBehavior(),
                        Spell.BuffSelf("Dispersion", ret => Me.HealthPercent < 40 || Unit.NearbyUnfriendlyUnits.Count(t => t.GotTarget && t.CurrentTarget.IsTargetingMyStuff()) >= 3),

                        Common.CreatePsychicScreamBehavior(),

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
                                    req => OrbCount >= 1
                                        && ((WoWUnit)req).Distance < 30 
                                        && (((WoWUnit)req).Class == WoWClass.Hunter || ((WoWUnit)req).Class == WoWClass.Rogue || ((WoWUnit)req).Class == WoWClass.Warrior || ((WoWUnit)req).Class == WoWClass.DeathKnight || ((WoWUnit)req).HasAnyAura("Maelstrom Weapon", "Savage Roar"))
                                    )
                                )
                            ),

                        // Offensive

                        // save Halo for combined damage and healing with no crowd control at risk
                        Spell.Cast("Halo", 
                            ret => !Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < 32 && u.IsCrowdControlled())
                                && Unit.NearbyUnfriendlyUnits.Count( u => Me.SpellDistance(u) < 32) > 1
                            ),

                        // snipe kills where possible
                        Spell.Cast("Shadow Word: Death", 
                            on => Unit.NearbyUnfriendlyUnits.Where(u => u.HealthPercent < 20 && Me.IsSafelyFacing(u) && u.InLineOfSpellSight ).OrderBy( k => k.HealthPercent).FirstOrDefault(),
                            ret => Me.CurrentTarget.HealthPercent <= 20),

                        // at 3 orbs, time for some burst
                        new Decorator(
                            ret => OrbCount >= 3,
                            new PrioritySelector(
                                Spell.Cast("Mindbender"),
                                Spell.Cast("Power Infusion"),
                                Spell.Cast("Shadowfiend"),
                                Spell.Buff("Devouring Plague")
                                )
                            ),

                        // only cast Mind Spike if its instant
                        Spell.Cast("Mind Spike", ret => Me.HasAura(SURGE_OF_DARKNESS)),

                        CastInsanity(),

                        Spell.Cast("Mind Blast", on => Me.CurrentTarget, req => OrbCount < 3, cancel => false),
                      
                        Spell.Buff("Vampiric Touch", 3, on => Me.CurrentTarget, req => Me.CurrentTarget.TimeToDeath() > 6),
                        Spell.Buff("Shadow Word: Pain", 1, on => Me.CurrentTarget, req => Me.CurrentTarget.TimeToDeath() > 6),

                        // multi-dot to supply procs and mana
                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits
                                .FirstOrDefault(u => u.Guid != Me.CurrentTargetGuid && (u.HasAuraExpired("Vampiric Touch", 3) || u.HasAuraExpired("Shadow Word: Pain", 1)) && u.InLineOfSpellSight),
                            Spell.Buff("Vampiric Touch", 3, on => (WoWUnit) on, req => true),
                            Spell.Buff("Shadow Word: Pain", 1, on => (WoWUnit) on, req => true)
                            ),

                        CastMindFlay()
                        )
                    )
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
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Dispel Magic"),

                        Spell.BuffSelf("Shadowform"),

                        // don't attempt to heal unless below a certain percentage health
                        Spell.Cast(
                            "Vampiric Embrace", 
                            ret => Me, 
                            ret => Me.CurrentTarget.IsBoss()
                                && Unit.NearbyGroupMembers.Count( u => u.HealthPercent < PriestSettings.VampiricEmbracePct ) >= PriestSettings.VampiricEmbraceCount
                            ),

                        // use fade to drop aggro.
                        Common.CreateFadeBehavior(),

                        // Shadow immune npcs.
                        // Spell.Cast("Holy Fire", ctx => Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && AoeTargets.Count() > 1,
                            new PrioritySelector(
                                ctx => Me.CurrentTarget,

                                // halo only if nothing near we aren't already in combat with
                                Spell.Cast("Halo", 
                                    ret => Unit.NearbyUnfriendlyUnits.All(u => Me.SpellDistance(u) < 34 && !u.IsCrowdControlled() && u.Combat && (u.IsTargetingMeOrPet || u.IsTargetingMyRaidMember))),
                                Spell.Cast("Divine Star"),
                                Spell.Cast("Cascade"),

                                new Decorator(
                                    req => AoeTargets.Count() <= 4,
                                    new PrioritySelector(
                                        ctx => AoeTargets.FirstOrDefault(u => !u.HasAllMyAuras("Shadow Word: Pain", "Vampiric Touch")),
                                        Spell.Buff("Vampiric Touch", on => (WoWUnit) on),
                                        Spell.Buff("Shadow Word: Pain", on => (WoWUnit) on)
                                        )
                                    ),

                                Spell.Cast("Mind Sear", 
                                    mov => true, 
                                    ctx => BestMindSearTarget, 
                                    ret => true, 
                                    cancel => Me.HealthPercent < PriestSettings.ShadowFlashHeal ),

                                new PrioritySelector(
                                    ctx => AoeTargets.FirstOrDefault(u => !u.HasAllMyAuras("Shadow Word: Pain", "Vampiric Touch")),
                                    Spell.Buff("Vampiric Touch", on => (WoWUnit) on),
                                    Spell.Buff("Shadow Word: Pain", on => (WoWUnit) on)
                                    )
                                )
                            ),

                        // Single target rotation
                        Spell.BuffSelf( "Dispersion", req => Me.ManaPercent < PriestSettings.DispersionMana),

                        Spell.BuffSelf("Power Infusion", req => !Me.IsMoving && Spell.CanCastHack("Shadow Word: Pain", Me.CurrentTarget, skipWowCheck: true) && !Me.CurrentTarget.IsMoving),
                        
                        Spell.Cast("Devouring Plague", req => OrbCount >= 3),
                        Spell.Cast("Mind Blast", on => Me.CurrentTarget, req => OrbCount < MaxOrbs, cancel => false),

                        Spell.Cast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent <= 20 && OrbCount < MaxOrbs),
                        Spell.Cast("Mind Spike", ret => Me.HasAura(SURGE_OF_DARKNESS)),
                        CastInsanity(),
                        Spell.Buff("Shadow Word: Pain", 5, on => Me.CurrentTarget, req => true),
                        Spell.Buff("Vampiric Touch", 4, on => Me.CurrentTarget, req => true),
                        CastMindFlay()
                        )
                    )
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

        private static WoWUnit GetBestMindSearTarget()
        {
            const int MindSearCount = 3;

            if (!Me.IsInGroup() || !Me.Combat)
                return Clusters.GetBestUnitForCluster(AoeTargets, ClusterType.Radius, 10f);

            if (!Spell.CanCastHack("Mind Sear", Me, skipWowCheck: true))
            {
                // Logger.WriteDebug("GetBestMindSearTarget: CanCastHack says NO to Healing Rain");
                return null;
            }

            List<WoWUnit> targetsCovered = Unit.UnfriendlyUnits(50).ToList();
            if (targetsCovered.Count() < MindSearCount)
                return null;
              
            List<WoWUnit> targets = targetsCovered
                .Union( Group.Tanks)
                .ToList();

            // search all targets to find best one in best location to use as anchor for cast on ground
            var t = targets
                .Where( u => u.SpellDistance() < 40 && Me.IsSafelyFacing(u) && u.InLineOfSpellSight )
                .Select(p => new
                {
                    Unit = p,
                    Count = targetsCovered
                        .Where(pp => pp.Location.DistanceSqr(p.Location) < 10 * 10)
                        .Count()
                })
                .OrderByDescending(v => v.Count)
                .ThenByDescending(v => v.Unit.IsPlayer)
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (t != null && t.Count >= MindSearCount )
            {
                Logger.WriteDebug("GetBestMindSearTarget:  found {0} with {1} enemies within 10 yds", t.Unit.SafeName(), t.Count);
                return t.Unit;
            }

            return null;
        }

        static Composite CastInsanity()
        {
            const int INSANITY = 129197;
            /*
            return Spell.Cast(
                INSANITY,
                mov => true,
                on => Me.CurrentTarget,
                req => Me.HasAura("Shadow Word: Insanity"),
                cancel => false
                );
            */
            return null;
        }

        static Composite CastMindFlay()
        {
            return Spell.Cast("Mind Flay", 
                mov => true, 
                on => Me.CurrentTarget, 
                req => Me.ManaPercent > PriestSettings.MindFlayManaPct,
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

        #endregion

        #region Diagnostics

        private static Composite CreateShadowDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    uint orbs = OrbCount;

                    string line = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, moving={3}, form={4}, orbs={5}, SoD={6}, divins={7}",
                        CompositeBuilder.CurrentBehaviorType.ToString(),
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving,
                        Me.Shapeshift,
                        orbs,
                        (long)Me.GetAuraTimeLeft(SURGE_OF_DARKNESS, true).TotalMilliseconds,
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
