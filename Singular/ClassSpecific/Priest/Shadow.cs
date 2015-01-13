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

                CreateFightAssessment(),

                Spell.Cast("Desperate Prayer", ret => Me, ret => Me.HealthPercent < PriestSettings.DesperatePrayerHealth),

                Spell.BuffSelf("Power Word: Shield", ret => (Me.HealthPercent < PriestSettings.PowerWordShield || (!Me.HasAura("Shadowform") && SpellManager.HasSpell("Shadowform"))) && !Me.HasAura("Weakened Soul")),

                Common.CreatePsychicScreamBehavior(),

                // skip direct heals if VE is up (unless no mobs in range)
                new Decorator(
                    req => !Me.HasAura("Vampiric Embrace") || !Unit.UnitsInCombatWithMeOrMyStuff(40).Any(),
                    new PrioritySelector(
                        Spell.Cast(
                            "Prayer of Mending",
                            ctx => Me,
                            ret => {
                                if (!Me.Combat)
                                    return false;

                                if (Me.HealthPercent > PriestSettings.PrayerOfMending)
                                    return false;

                                if (!Unit.UnitsInCombatWithMeOrMyStuff(40).All(u => u.IsCrowdControlled()))
                                    return false;

                                return true;
                                }
                            ),

                        Spell.Cast(
                            "Flash Heal",
                            ctx => Me,
                            ret => {
                                if (Me.HealthPercent > PriestSettings.ShadowFlashHeal)
                                    return false;

                                if (Unit.UnitsInCombatWithMeOrMyStuff(40).Any(u => !u.IsCrowdControlled()))
                                    return false;

                                return true;
                                }
                            )
                        )
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
                        CastMindBlast(),
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

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Spell.BuffSelf("Shadowform"),

                        new Decorator(
                            req => !Unit.IsTrivial(Me.CurrentTarget),
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
                                        || Unit.NearbyUnfriendlyUnits.Count(t => t.GotTarget() && t.CurrentTarget.IsTargetingMyStuff()) >= 3),

                                Common.CreatePriestShackleUndeadAddBehavior(),
                                Common.CreatePsychicScreamBehavior(),

                                Spell.OffGCD(
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
                                ctx => AoeTargets.Where( u=> Me.IsSafelyFacing(u) && u.InLineOfSpellSight).FirstOrDefault(),

                                Spell.Buff(
                                    "Void Entropy", 
                                    true, 
                                    on => AoeTargets.FirstOrDefault( u => (u != Me.CurrentTarget && u.HealthPercent > 90) || (u == Me.CurrentTarget && Me.CurrentTarget.TimeToDeath() > 30)), 
                                    ret => OrbCount >= 3 && Me.CurrentTarget.TimeToDeath() > 30
                                    ),

                                new Decorator(
                                    req => Common.HasTalent(PriestTalents.AuspiciousSpirits),
                                    Spell.Buff("Shadow Word: Pain", true, on => AoeTargets.FirstOrDefault(u => !u.HasMyAura("Shadow Word: Pain") && u.InLineOfSpellSight), req => true)
                                    ),

                                // cast on highest health mob (for greatest heal)
                                Spell.Buff(
                                    "Devouring Plague",
                                    true,
                                    on => AoeTargets
                                        .Where(u => u != null && u.IsValid && u.IsAlive && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && !u.HasMyAura("Devouring Plague"))
                                        .OrderByDescending(u => (int)u.HealthPercent)
                                        .FirstOrDefault(),
                                    ret => OrbCount >= 3
                                    ),

                                // cast on the lowest health mob (to reduce numbers with kill if possible)
                                new Decorator(
                                    req => OrbCount < MaxOrbs,
                                    new PrioritySelector(
                                        ctx => AoeTargets
                                            .Where(u => u.IsAlive && Me.IsSafelyFacing(u) && u.InLineOfSpellSight)
                                            .OrderBy(u => (int)u.HealthPercent)
                                            .FirstOrDefault(),
                                        CastMindBlast(on => (WoWUnit)on),
                                        Spell.Cast("Shadow Word: Death", on => (WoWUnit)on, req => ((WoWUnit)req).HealthPercent < 20)
                                        )
                                    ),

                                Spell.Buff("Shadow Word: Pain", true, on => AoeTargets.FirstOrDefault(u => !u.HasMyAura("Shadow Word: Pain") && u.InLineOfSpellSight), req => true),
                                Spell.Buff("Vampiric Touch", true, on => AoeTargets.FirstOrDefault(u => !u.HasMyAura("Vampiric Touch") && Me.IsSafelyFacing(u) && u.InLineOfSpellSight), req => true),

                                new Decorator(
                                    req => cntAoeTargets >= PriestSettings.TalentTier6Count,
                                    new PrioritySelector(
                                        // halo only if nothing near we aren't already in combat with
                                        Spell.Cast("Halo", req => UseHalo()),
                                        Spell.Cast("Cascade", on => GetBestCascadeTarget()),
                                        Spell.Cast("Divine Star", req => UseDivineStar())
                                        )
                                    ),

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
                                    cancel => Me.HealthPercent < PriestSettings.ShadowFlashHeal
                                    ),
                                CastMindFlay(on => (WoWUnit)on, req => cntAoeTargets < 4)
                                )
                            ),

                        Spell.Buff("Void Entropy", true, ret => OrbCount >= 3 && Me.CurrentTarget.TimeToDeath() > 30),
                        Spell.Buff("Devouring Plague", true, ret => OrbCount >= 3),

                        Spell.Buff("Shadow Word: Pain", true),
                        Spell.Buff("Vampiric Touch", true),
                        CastMindBlast(),
                        Spell.Cast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent <= 20 && OrbCount < MaxOrbs),
                        CastInsanity(),
                        Spell.Cast("Mind Spike", ret => Me.HasAura(SURGE_OF_DARKNESS)),
                        Spell.Cast("Halo",
                            ret => Unit.NearbyUnfriendlyUnits
                                .All(u => Me.SpellDistance(u) < 35
                                    && !u.IsCrowdControlled()
                                    && u.Combat
                                    && (u.Aggro || u.IsTargetingMeOrPet || u.IsTargetingMyRaidMember)
                                    )
                            ),

                        new Decorator(
                            req => Common.HasTalent(PriestTalents.VoidEntropy),
                            Spell.Buff("Devouring Plague", true, ret => OrbCount >= 3)
                            ),

                        CastMindFlay()
                        )
                    )
                );
        }

        private static Composite CastMindBlast(UnitSelectionDelegate onUnit = null, SimpleBooleanDelegate requirements = null)
        {
            UnitSelectionDelegate on = onUnit ?? (o => Me.CurrentTarget); 
            SimpleBooleanDelegate req;

            if (requirements == null)
                req = r => OrbCount < MaxOrbs;
            else
                req = r => OrbCount < MaxOrbs && requirements(r);

            return new Throttle( 
                TimeSpan.FromMilliseconds(1500),
                Spell.Cast("Mind Blast", on, req, cancel => false)
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
                        Spell.BuffSelf("Dispersion", ret => Me.HealthPercent < 40 || Unit.NearbyUnfriendlyUnits.Count(t => t.GotTarget() && t.CurrentTarget.IsTargetingMyStuff()) >= 3),

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

                        CastMindBlast(),
                      
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
                            ret => Spell.UseAOE && cntAoeTargets > 1,
                            new PrioritySelector(
                                ctx => Me.CurrentTarget,

                                // halo only if nothing near we aren't already in combat with
                                Spell.Cast("Halo", 
                                    ret => Unit.NearbyUnfriendlyUnits
                                        .All(u => Me.SpellDistance(u) < 34 && !u.IsCrowdControlled() && u.Combat && (u.IsTargetingMeOrPet || u.IsTargetingMyRaidMember))),
                                Spell.Cast("Divine Star"),
                                Spell.Cast("Cascade"),

                                new Decorator(
                                    req => cntAoeTargets <= 4,
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
                        CastMindBlast(),

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
            const int INSANITY_PROC = 132573;
            return Spell.Cast(
                "Mind Flay",
                mov => true,
                on => Me.CurrentTarget,
                req => {
                    if (!Me.HasAura(INSANITY_PROC))
                        return false;
                    if (!Spell.CanCastHack("Mind Flay", Me.CurrentTarget))
                        return false;
                    Logger.Write(LogColor.Hilite, "^Insanity: casting a serious Mind Flay");
                    return true;
                    },
                cancel => false
                );
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

            int count = hitByDS.Count();
            if (count >= PriestSettings.TalentTier6Count)
            {
                Logger.WriteDiagnostic(
                    "UseDivineStar: will hit {0} mobs without aggroing adds / cc break",
                    count
                    );
                return true;
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

            int count = hitByHalo.Count();
            if (count >= PriestSettings.TalentTier6Count)
            {
                Logger.WriteDiagnostic(
                    "UseHalo: will hit {0} mobs without unwanted aggro / cc break",
                    count
                    );
                return true;
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

                    string line = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, moving={3}, form={4}, orbs={5}, SoD={6}, divins={7}, aoe={8}",
                        CompositeBuilder.CurrentBehaviorType.ToString(),
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving,
                        Me.Shapeshift,
                        orbs,
                        (long)Me.GetAuraTimeLeft(SURGE_OF_DARKNESS, true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Divine Insight", true).TotalMilliseconds,
                        cntAoeTargets 
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
