using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Lists;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.Common;
using System.Drawing;
using System.Collections.Generic;
using CommonBehaviors.Actions;

namespace Singular.ClassSpecific.Druid
{
    public class Balance
    {
        # region Properties & Fields

        private enum EclipseType
        {
            None,
            Solar,
            Lunar
        };
        
        private static EclipseType eclipseLastCheck = EclipseType.None;
        public static bool newEclipseDotNeeded;

        private static int StarfallRange { get { return TalentManager.HasGlyph("Focus") ? 20 : 40; } }

        private static int CurrentEclipse { get { return BitConverter.ToInt32(BitConverter.GetBytes(StyxWoW.Me.CurrentEclipse), 0); } }

        private static DruidSettings DruidSettings
        {
            get { return SingularSettings.Instance.Druid(); }
        }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        static int MushroomCount
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.Entry == 47649 && o.Distance <= 40).Count(o => o.CreatedByUnitGuid == StyxWoW.Me.Guid); }
        }

        static WoWUnit BestAoeTarget
        {
            get { return Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && !u.IsCrowdControlled()), ClusterType.Radius, 8f); }
        }

        #endregion

        #region Heal


        private static bool _ImaMoonBeast = false;
        private static WoWUnit _CycloneTarget;
        private static DateTime _CycloneExpires = DateTime.MinValue;

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidBalance)]
        public static Composite CreateDruidBalanceHeal()
        {
            _ImaMoonBeast = TalentManager.HasGlyph( "Moonbeast");

            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    Spell.Cast( "Rejuvenation", mov => false, on => Me, ret => _ImaMoonBeast && Me.HasAuraExpired("Rejuvenation", 1)), 

                    Spell.Cast("Renewal", ret => Me.HealthPercent < DruidSettings.RenewalHealth),
                    Spell.BuffSelf("Cenarion Ward", ret => Me.HealthPercent < 85 || Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro || (u.Combat && u.IsTargetingMeOrPet)) > 1),

                    Common.CreateNaturesSwiftnessHeal(ret => Me.HealthPercent < 60),

                    new Decorator(
                        ret => Me.HealthPercent < 40 || (DateTime.Now < _CycloneExpires && _CycloneTarget != null && _CycloneTarget.IsValid && _CycloneTarget.HasAura( "Cyclone")),
                        new PrioritySelector(

                            new Sequence(
                                new Action( 
                                    r => _CycloneTarget = Unit.NearbyUnfriendlyUnits
                                            .Where(u => u.CurrentTargetGuid == Me.Guid && u.Combat)
                                            .OrderByDescending( k => k.IsPlayer)
                                            .ThenBy( k => k.Guid == Me.CurrentTargetGuid )
                                            .ThenBy( k => k.Distance2DSqr)
                                            .FirstOrDefault() 
                                    ),

                                Spell.Buff("Cyclone", ctx => _CycloneTarget ),
                                new Action( r => _CycloneExpires = DateTime.Now + TimeSpan.FromMilliseconds(5500))
                                ),

                            new Decorator(
                                ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Aggro || (u.Combat && u.IsTargetingMeOrPet)),
                                new PrioritySelector(
                                    Spell.Cast("Disorienting Roar"),
                                    Spell.Cast("Might of Ursoc")
                                    )
                                ),

                            // heal out of form at this point (try to Barkskin at least to prevent spell pushback)
                            new Throttle(Spell.BuffSelf("Barkskin")),

                            new PrioritySelector(
                                Spell.Cast("Rejuvenation", on => Me, ret => Me.HasAuraExpired("Rejuvenation")),
                                Spell.Cast("Healing Touch", mov => true, on => Me, req => Me.GetPredictedHealthPercent(true) < 90, req => Me.HealthPercent > 95)
                                )
                            )
                        )
                    )
                );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Normal)]
        public static Composite CreateBalancePullNormal()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Movement.CreateEnsureMovementStoppedBehavior(35f),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // grinding or questing, if target meets these criteria cast an instant to tag quickly
                        // 1. mob is less than 12 yds, so no benefit from delay in Wrath/Starsurge missile arrival
                        // 2. area has another player possibly competing for mobs (we want to tag the mob quickly)
                        new Decorator(
                            ret => Me.CurrentTarget.Distance < 12
                                || ObjectManager.GetObjectsOfType<WoWPlayer>(false, false).Any(p => p.Location.DistanceSqr(Me.CurrentTarget.Location) <= 40 * 40),
                            new PrioritySelector(
                                Spell.Cast("Sunfire", ret => GetEclipseDirection() == EclipseType.Solar),
                                Spell.Cast("Moonfire")
                                )
                            ),

                        // otherwise, start with a bigger hitter with cast time so we can follow 
                        // with an instant to maximize damage at initial aggro
                        Spell.Cast("Starsurge"),
                        Spell.Cast("Wrath", ret => GetEclipseDirection() == EclipseType.Solar),

                        // we are moving so throw an instant of some type
                        Spell.Cast("Sunfire", ret => GetEclipseDirection() == EclipseType.Solar),
                        Spell.Cast("Moonfire")
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 38f)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Normal)]
        public static Composite CreateDruidBalanceNormalCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Helpers.Common.CreateAutoAttack(false),
                Movement.CreateEnsureMovementStoppedBehavior( 35f),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateBalanceDiagnosticOutputBehavior(),

                        // Spell.Buff("Entangling Roots", ret => Me.CurrentTarget.Distance > 12),
                        Spell.Buff("Faerie Swarm", ret => Me.CurrentTarget.IsMoving &&  Me.CurrentTarget.Distance > 20),

                        Spell.BuffSelf("Innervate",
                            ret => StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),

                        // yes, only 8yds because we are knocking back only if close to melee range
                        Spell.Cast( "Typhoon", 
                            ret => Clusters.GetClusterCount( Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8f) >= 1),

                        Spell.Cast("Mighty Bash", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        new Decorator(
                            ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(

                                Spell.Cast("Wild Mushroom: Detonate", ret => MushroomCount == 3),

                                // If Detonate is coming off CD, make sure we drop some more shrooms. 3 seconds is probably a little late, but good enough.
                                new Sequence(
                                    Spell.CastOnGround("Wild Mushroom",
                                        ret => BestAoeTarget.Location,
                                        ret => BestAoeTarget != null && Spell.GetSpellCooldown("Wild Mushroom: Detonate").TotalSeconds <= 5),
                                    new Action(ctx => Lua.DoString("SpellStopTargeting()"))
                                    ),

                                Spell.CastOnGround("Force of Nature",
                                    ret => StyxWoW.Me.CurrentTarget.Location,
                                    ret => true ),

                                Spell.Cast("Starfall",
                                    ret => StyxWoW.Me,
                                    ret => DruidSettings.UseStarfall),

                                Spell.Cast("Moonfire",
                                    ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u =>
                                                u.Combat && !u.IsCrowdControlled() && u.HasAuraExpired("Moonfire", 2))),

                                Spell.Cast("Sunfire",
                                    ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u =>
                                                u.Combat && !u.IsCrowdControlled() && !u.HasAuraExpired("Sunfire", 2)))
                                )
                            ),

                        Helpers.Common.CreateInterruptBehavior(),

                        // make sure we always have DoTs 
                        new Sequence(
                            Spell.Buff("Sunfire", true, on => Me.CurrentTarget, req => true, 2),
                            new Action(ret => Logger.WriteDebug("Adding DoT:  Sunfire"))
                            ),

                        new Sequence(
                            Spell.Buff("Moonfire", true, on => Me.CurrentTarget, req => true, 2),
                            new Action(ret => Logger.WriteDebug("Adding DoT:  Moonfire"))
                            ),

                        CreateDoTRefreshOnEclipse(),

                        Spell.Cast("Starsurge"),
                        Spell.Cast("Starfall", ret => DruidSettings.UseStarfall),

                        Spell.Cast("Wrath",
                            ret => GetEclipseDirection() == EclipseType.Lunar ),

                        Spell.Cast("Starfire",
                            ret => GetEclipseDirection() == EclipseType.Solar )

                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Battlegrounds)]
        public static Composite CreateDruidBalancePvPCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Movement.CreateEnsureMovementStoppedBehavior(30f),  // cause forced stop a little closer in PVP

                // Ensure we do /petattack if we have treants up.
                Helpers.Common.CreateAutoAttack(true),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        CreateBalanceDiagnosticOutputBehavior(),

                        Spell.BuffSelf("Moonkin Form"),

                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => u.IsCasting && u.Distance <30 && u.CurrentTargetGuid == Me.Guid ),
                            new Sequence(
                                // following sequence takes advantage of Cast() monitoring spells with a cancel delegate for their entire cast
                                Spell.Cast( "Solar Beam", on => (WoWUnit) on),
                                new PrioritySelector(
                                    Spell.WaitForGcdOrCastOrChannel(),
                                    new ActionAlwaysSucceed()
                                    ),
                                new PrioritySelector(
                                    Spell.Cast("Ursol's Vortex", mov => true, on => (WoWUnit)on, ret => Spell.GetSpellCooldown("Solar Beam").TotalSeconds == 0, cancel => false),
                                    Spell.Cast("Entangling Roots", on => (WoWUnit)on),
                                    new ActionAlwaysSucceed()
                                    )
                                )
                            ),

                        // Helpers.Common.CreateInterruptBehavior(),

                        Spell.Cast("Typhoon",
                            ret => Me.CurrentTarget.IsWithinMeleeRange 
                                && Me.CurrentTarget.Class != WoWClass.Priest
                                && (Me.CurrentTarget.Class != WoWClass.Warlock || Me.CurrentTarget.CastingSpellId == 1949 /*Hellfire*/ || Me.CurrentTarget.HasAura("Immolation Aura"))
                                && Me.CurrentTarget.Class != WoWClass.Hunter 
                                && Me.IsSafelyFacing( Me.CurrentTarget, 90f)),

                        Spell.Cast("Typhoon",
                            ret => Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8f) >= 1),

                        Spell.Cast("Mighty Bash", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        // use every Shooting Stars proc
                        Spell.Cast( "Starsurge", ret => Me.ActiveAuras.ContainsKey( "Shooting Stars")),

                        // Spread MF/IS on Rouges / Feral Druids first
                        Spell.Buff("Faerie Fire",
                            true,
                            on => (WoWUnit)Unit.NearbyUnfriendlyUnits.FirstOrDefault(p => (p.Class == WoWClass.Rogue || p.HasAura("Cat Form")) && !p.HasAnyAura("Faerie Fire", "Faerie Swarm") && p.Distance < 35 && Me.IsSafelyFacing(p) && p.InLineOfSpellSight),
                            req => true,
                            0
                            ),

                        // More DoTs!!  Dot EVERYTHING (including pets) to boost Shooting Stars proc chance
                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => !u.HasAllMyAuras( "Moonfire", "Sunfire") && Me.IsSafelyFacing(u) && u.InLineOfSpellSight ),
                            Spell.Buff( "Moonfire", on => (WoWUnit) on),
                            Spell.Buff( "Sunfire", on => (WoWUnit) on)
                            ),

                        Spell.Cast( "Starfall"),

                        new Decorator(
                            ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.CurrentTargetGuid == Me.Guid),
                            new PrioritySelector(
                                Spell.Cast("Wrath", ret => GetEclipseDirection() == EclipseType.Lunar),
                                Spell.Cast("Starfire", ret => GetEclipseDirection() == EclipseType.Solar)
                                )
                            ),
#if SPAM_INSTANT_TO_AVOID_SPELLLOCK
                        new Decorator(
                            ret => Unit.NearbyUnfriendlyUnits.Any(u => u.CurrentTargetGuid == Me.Guid),
                            new PrioritySelector(
                                Spell.Buff("Moonfire", req => !Me.HasAura("Eclipse (Solar)")),
                                Spell.Buff("Sunfire")
                                )
                            ),
#endif
                        // Now on any group target missing Weakened Armor
                        Spell.Buff("Fairie Fire",
                        on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.Distance < 35 && !u.HasAura("Weakened Armor")
                                                                    && Unit.GroupMembers.Any(m => m.CurrentTargetGuid == u.Guid)
                                                                    && Me.IsSafelyFacing(u) && u.InLineOfSpellSight)),

                        // Now any enemy missing Weakened Armor
                        Spell.Buff("Fairie Fire", 
                            on => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => u.Distance < 35 && !u.HasAura( "Weakened Armor") && Me.IsSafelyFacing(u) && u.InLineOfSpellSight ))

                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion


        #region Instance Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Instances)]
        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Instances)]
        public static Composite CreateDruidBalanceInstanceCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Helpers.Common.CreateAutoAttack(false),
                Movement.CreateEnsureMovementStoppedBehavior(35f),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateBalanceDiagnosticOutputBehavior(),

                        Spell.Buff("Innervate",
                            ret => (from healer in Group.Healers 
                                    where healer != null && healer.IsAlive && healer.Distance < 30 && healer.ManaPercent <= 15
                                    select healer).FirstOrDefault()),

                        Spell.BuffSelf("Innervate",
                            ret => StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),

                        Spell.BuffSelf("Moonkin Form"),

                        Spell.Cast("Mighty Bash", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        new Decorator(
                            ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(

                                Spell.Cast("Wild Mushroom: Detonate", ret => MushroomCount == 3),

                                // If Detonate is coming off CD, make sure we drop some more shrooms. 3 seconds is probably a little late, but good enough.
                                new Sequence(
                                    Spell.CastOnGround("Wild Mushroom",
                                        ret => BestAoeTarget.Location,
                                        ret => Spell.GetSpellCooldown("Wild Mushroom: Detonate").TotalSeconds <= 2 && BestAoeTarget != null && MushroomCount < 3 ),
                                    new Action(ctx => Lua.DoString("SpellStopTargeting()"))
                                    ),

                                Spell.CastOnGround("Force of Nature",
                                    ret => StyxWoW.Me.CurrentTarget.Location,
                                    ret => true ),

                                Spell.Cast("Starfall",
                                    ret => StyxWoW.Me,
                                    ret => DruidSettings.UseStarfall),

                                Spell.Cast("Moonfire",
                                    ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u =>
                                                u.Combat && !u.IsCrowdControlled() && !u.HasMyAura("Moonfire"))),

                                Spell.Cast("Sunfire",
                                    ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u =>
                                                u.Combat && !u.IsCrowdControlled() && !u.HasMyAura("Sunfire")))
                                )
                            ),

                        Helpers.Common.CreateInterruptBehavior(),

                        // make sure we always have DoTs 
                        new Sequence(
                            Spell.Cast("Sunfire", ret => !Me.CurrentTarget.HasMyAura("Sunfire")),
                            new Action(ret => Logger.WriteDebug("Adding DoT:  Sunfire"))
                            ),

                        new Sequence(
                            Spell.Cast("Moonfire", ret => !Me.CurrentTarget.HasMyAura("Moonfire")),
                            new Action(ret => Logger.WriteDebug("Adding DoT:  Moonfire"))
                            ),

                        new Decorator( 
                            ret => Me.HasAura("Celestial Alignment"),
                            new PrioritySelector(
                                // to do: make last two casts DoTs if possible... 
                                Spell.Cast("Starsurge", ret => SpellManager.HasSpell("Starsurge")),
                                Spell.Cast("Starfire", ret => SpellManager.HasSpell("Starfire"))
                                )
                            ),

                        CreateDoTRefreshOnEclipse(),

                        Spell.Cast("Starsurge"),
                        Spell.Cast("Starfall", ret => DruidSettings.UseStarfall),

                        Spell.Cast("Wrath",
                            ret => GetEclipseDirection() == EclipseType.Lunar ),

                        Spell.Cast("Starfire",
                            ret => GetEclipseDirection() == EclipseType.Solar )

                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        /// <summary>
        /// creates behavior to recast DoTs when an Eclipse occurs.  this creates
        /// a more powerful DoT then when cast out of associate Eclipse so no
        /// check on overwriting existing DoT is made.  in the event that
        /// a more powerful version already exists, only one failed attempt
        /// (red message) should occur
        /// </summary>
        /// <returns></returns>
        private static Composite CreateDoTRefreshOnEclipse()
        {
            return new PrioritySelector(

                new Action(ret =>
                {
                    EclipseType eclipseCurrent;
                    if (StyxWoW.Me.HasAura("Eclipse (Solar)"))
                        eclipseCurrent = EclipseType.Solar;
                    else if (StyxWoW.Me.HasAura("Eclipse (Lunar)"))
                        eclipseCurrent = EclipseType.Lunar;
                    else
                        eclipseCurrent = EclipseType.None;

                    if (eclipseLastCheck != eclipseCurrent)
                    {
                        eclipseLastCheck = eclipseCurrent;
                        newEclipseDotNeeded = eclipseLastCheck != EclipseType.None;
                    }

                    return RunStatus.Failure;
                }),

                new Sequence(
                    Spell.Cast("Moonfire", ret => newEclipseDotNeeded && eclipseLastCheck == EclipseType.Lunar),
                    new Action(ret =>
                    {
                        newEclipseDotNeeded = false;
                        Logger.WriteDebug("Refresh DoT: Moonfire");
                    })
                    ),

                new Sequence(
                    Spell.Cast("Sunfire", ret => newEclipseDotNeeded && eclipseLastCheck == EclipseType.Solar),
                    new Action(ret =>
                    {
                        newEclipseDotNeeded = false;
                        Logger.WriteDebug("Refresh DoT: Sunfire");
                    })
                    )

                );
        }

        private static EclipseType GetEclipseDirection()
        {
#if USE_LUA_FOR_ECLIPSE
            return Lua.GetReturnVal<string>("return GetEclipseDirection();", 0);
#else
            WoWAura eclipse = Me.GetAllAuras().FirstOrDefault(a => a.SpellId == 67483 || a.SpellId == 67484);
            if (eclipse == null)
                return EclipseType.None;

            if (eclipse.SpellId == 67483)
                return EclipseType.Solar;

            return EclipseType.Lunar; // 67484
#endif
        }

        #endregion


        #region Diagnostics

        private static Composite CreateBalanceDiagnosticOutputBehavior()
        {
            return new Decorator(
                ret => SingularSettings.Debug,
                new Throttle(1,
                    new Action(ret =>
                    {
                        string log;
                        WoWAura eclips = Me.GetAllAuras().FirstOrDefault(a => a.Name == "Eclipse (Solar)" || a.Name == "Eclipse (Lunar)");
                        string eclipsString = eclips == null ? "None" : (eclips.Name == "Eclipse (Solar)" ? "Solar" : "Lunar");

                        log = string.Format(".... h={0:F1}%/m={1:F1}%, form:{2}, eclps={3}, towards={4}, eclps#={5}, mushcnt={6}",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.Shapeshift.ToString(),
                            eclipsString,
                            GetEclipseDirection().ToString(),
                            Me.CurrentEclipse,
                            MushroomCount
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            log += string.Format(", th={0:F1}%/tm={1:F1}%, dist={2:F1}, face={3}, loss={4}, sfire={5}, mfire={6}",
                                target.HealthPercent,
                                target.ManaPercent,
                                target.Distance,
                                Me.IsSafelyFacing(target),
                                target.InLineOfSpellSight,
                                (long)target.GetAuraTimeLeft("Sunfire", true).TotalMilliseconds,
                                (long)target.GetAuraTimeLeft("Moonfire", true).TotalMilliseconds
                                );
                        }

                        Logger.WriteDebug(Color.AntiqueWhite, log);
                    })
                    )
                );
        }

        #endregion

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Battlegrounds | WoWContext.Instances, 2)]
        public static Composite CreateBalancePreCombatBuffBattlegrounds()
        {
            return new PrioritySelector(
                Common.CreateDruidCastSymbiosis(on => GetBalanceBestSymbiosisTarget()),
                new Decorator(
                    ret => SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds || !Unit.NearbyUnfriendlyUnits.Any(),
                    new PrioritySelector(
                        Spell.BuffSelf( "Astral Communion", ret => PVP.PrepTimeLeft < 20 && !Me.HasAnyAura("Eclipse (Lunar)","Eclipse (Solar)"))
                        )
                    )
                );
        }


        [Behavior(BehaviorType.PullBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.All, 2)]
        public static Composite CreateBalancePullBuff()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Moonkin Form")
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Normal, 2)]
        public static Composite CreateBalanceCombatBuffNormal()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Moonkin Form")
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Battlegrounds | WoWContext.Instances, 2)]
        public static Composite CreateBalanceCombatBuffBattlegrounds()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Moonkin Form"),

                // Symbiosis
/*
                Common.SymbCast("Mirror Image", on => Me.CurrentTarget, ret => Me.GotTarget && Me.Shapeshift == ShapeshiftForm.Moonkin),
                Common.SymbCast("Hammer of Justice", on => Me.CurrentTarget, ret => Me.GotTarget && !Me.CurrentTarget.IsBoss() && (Me.CurrentTarget.IsCasting || Me.CurrentTarget.IsPlayer)),

                Common.SymbBuff("Unending Resolve", on => Me, ret => Me.HealthPercent < DruidSettings.Barkskin),
                Common.SymbBuff("Anti-Magic Shell", on => Me, ret => Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == Me.Guid)),
                // add mass dispel ...
                Common.SymbBuff("Cloak of Shadows", on => Me, ret => Me.ActiveAuras.Any(a => a.Value.IsHarmful && a.Value.IsActive && a.Value.Spell.DispelType != WoWDispelType.None))
*/
                Common.SymbCast( Symbiosis.MirrorImage, on => Me.CurrentTarget, ret => Me.GotTarget && Me.Shapeshift == ShapeshiftForm.Moonkin),
                Common.SymbCast( Symbiosis.HammerOfJustice, on => Me.CurrentTarget, ret => Me.GotTarget && !Me.CurrentTarget.IsBoss() && (Me.CurrentTarget.IsCasting || Me.CurrentTarget.IsPlayer)),

                Common.SymbBuff( Symbiosis.UnendingResolve, on => Me, ret => Me.HealthPercent < DruidSettings.Barkskin),
                Common.SymbBuff( Symbiosis.AntiMagicShell, on => Me, ret => Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == Me.Guid)),
                // add mass dispel ...
                Common.SymbBuff( Symbiosis.CloakOfShadows, on => Me, ret => Me.ActiveAuras.Any(a => a.Value.IsHarmful && a.Value.IsActive && a.Value.Spell.DispelType != WoWDispelType.None))
                );
        }

        private static WoWUnit GetBalanceBestSymbiosisTarget()
        {
            WoWUnit target = null;
            if (target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Mage);
            if (target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Warlock);
            if (target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.DeathKnight);
            if (target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Paladin);
            if (target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Rogue);
            // if (target == null)
            //    target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Priest);

            return target;
        }
    }

}
