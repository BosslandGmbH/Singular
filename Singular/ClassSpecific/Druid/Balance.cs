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
using Styx.Pathing;
using Styx.Common.Helpers;
using Styx.CommonBot.Routines;

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

        private static bool glyphFlappingOwl { get; set; }
        private static bool glyphUntamedStars { get; set; }

        private static int CurrentEclipse { get { return BitConverter.ToInt32(BitConverter.GetBytes(StyxWoW.Me.CurrentEclipse), 0); } }

        private static DruidSettings DruidSettings
        {
            get { return SingularSettings.Instance.Druid(); }
        }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WoWUnit Target { get { return Me.CurrentTarget; } }

        private static CombatScenario scenario { get; set; }

        static int MushroomCount
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.Entry == 47649 && o.Distance <= 40).Count(o => o.CreatedByUnitGuid == StyxWoW.Me.Guid); }
        }

        static WoWUnit BestAoeTarget
        {
            get { return Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && !u.IsCrowdControlled()), ClusterType.Radius, 8f); }
        }

        #endregion

        [Behavior(BehaviorType.Initialize, WoWClass.Druid, WoWSpec.DruidBalance)]
        public static Composite CreateDruidBalanceInitialize()
        {
            scenario = new CombatScenario(44, 1.5f);

            glyphFlappingOwl = TalentManager.HasGlyph("Flapping Owl");
            if (glyphFlappingOwl)
                Logger.Write(LogColor.Init, "Glyph of Flapping Owl: will [Flap] when falling");

            glyphUntamedStars = TalentManager.HasGlyph("Untamed Stars");
            if (glyphUntamedStars)
                Logger.Write(LogColor.Init, "Glyph of Untamed Stars: will avoid pulling additional mobs");
            else
                Logger.Write(LogColor.Init, "Starfall: Untamed Stars not equipped, can safely cast without aggro");

            return null;
        }



        #region Heal


        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Normal, priority: 999)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Battlegrounds, priority: 999)]
        public static Composite CreateDruidBalanceHeal()
        {
            return new PrioritySelector(

                CreateBalanceDiagnosticOutputBehavior(),

                new Decorator(
                    req => Me.Combat 
                        && Me.HealthPercent < DruidSettings.SelfHealingTouchHealth && Me.GetPredictedHealthPercent(true) < DruidSettings.SelfHealingTouchHealth,
                    Common.CreateDruidCrowdControl()
                    )

                );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Normal)]
        public static Composite CreateBalancePullNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        // grinding or questing, if target meets these criteria cast an instant to tag quickly
                        // 1. mob is less than 12 yds, so no benefit from delay in Wrath/Starsurge missile arrival
                        // 2. area has another player possibly competing for mobs (we want to tag the mob quickly)
                        new Decorator(
                            ret => Me.CurrentTarget.Distance < 12
                                || ObjectManager.GetObjectsOfType<WoWPlayer>(false, false).Any(p => p.Location.DistanceSqr(Me.CurrentTarget.Location) <= 40 * 40),
                            new PrioritySelector(
                                Spell.Buff("Sunfire", ret => Me.CurrentEclipse > 0),
                                Spell.Buff("Moonfire")
                                )
                            ),

                        // otherwise, start with a bigger hitter with cast time so we can follow 
                        // with an instant to maximize damage at initial aggro
                        new Throttle(
                            3,
                            Spell.Cast("Starsurge", 
                                req => (Me.CurrentEclipse < -20 && !Me.HasAura("Lunar Empowerment"))
                                    || (Me.CurrentEclipse > 20 && !Me.HasAura("Solar Empowerment"))
                                    || Spell.GetCharges("Starsurge") >= 3
                                )
                            ),

                        Spell.Cast("Wrath", ret => Me.CurrentEclipse > 0),
                        Spell.Cast("Starfire", ret => Me.CurrentEclipse < 0),
                        Spell.Buff("Sunfire", ret => Me.CurrentEclipse > 0),
                        Spell.Buff("Moonfire", req => Me.CurrentEclipse <= 0)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Normal)]
        public static Composite CreateDruidBalanceNormalCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.Instance.HealBehavior,
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        new Action(r =>
                        {
                            scenario.Update(Target);
                            return RunStatus.Failure;
                        }),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        // Spell.Buff("Entangling Roots", ret => Me.CurrentTarget.Distance > 12),
                        CreateBalanceFaerieFireBehavior(),

                        // yes, only 8yds because we are knocking back only if close to melee range
                        Spell.Cast("Typhoon",
                            ret => Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8f) >= 1),

                        Spell.Cast("Mighty Bash", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        new Decorator(
                            ret => scenario.MobCount > 1,
                            new PrioritySelector(

                                // CreateMushroomSetAndDetonateBehavior(),

                                Spell.HandleOffGCD(Spell.Cast("Force of Nature", req => !Me.CurrentTarget.IsTrivial() && Me.CurrentTarget.TimeToDeath() > 8)),

                                // Starfall:  verify either not glyphed -or- at least 3 targets have DoT
                                Spell.Cast( "Starfall", req => IsStarfallNeeded),

                                new PrioritySelector(
                                    ctx => scenario.Mobs.Where(u => u.Combat && !u.IsCrowdControlled() && Me.IsSafelyFacing(u)).ToList(),
                                    Spell.Cast("Sunfire", on => ((List<WoWUnit>)on).FirstOrDefault(u => u.HasAuraExpired("Sunfire", 2)), req => Me.CurrentEclipse > 0 && Me.CurrentTarget.HasKnownAuraExpired("Sunfire", 3)),
                                    Spell.Cast("Moonfire", on => ((List<WoWUnit>)on).FirstOrDefault(u => u.HasAuraExpired("Moonfire", 2)), req => Me.CurrentEclipse > 0 && Me.CurrentTarget.HasKnownAuraExpired("Moonfire", 3)),
                                    Common.CastHurricaneBehavior(on => Me.CurrentTarget)
                                    )
                                )
                            ),

                        Helpers.Common.CreateInterruptBehavior(),

                        new Decorator(
                            req => true,
                            new PrioritySelector(
                                new Throttle(
                                    3,
                                    new PrioritySelector(
                                        //  1 Starsurge when Lunar Empowerment is down and Eclipse energy is > 20.
                                        Spell.Cast("Starsurge", req =>
                                        {
                                            if (Me.CurrentEclipse < -20 && !Me.HasAura("Lunar Empowerment"))
                                            {
                                                if (SingularSettings.Debug && Spell.CanCastHack("Starsurge", Me.CurrentTarget))
                                                    Logger.WriteDebug(LogColor.Hilite, "Starsurge: buffing Lunar Empowerment ");
                                                return true;
                                            }
                                            return false;
                                        }),

                                        //  2 Starsurge when Solar Empowerment is down and Eclipse energy is > 20.
                                        Spell.Cast("Starsurge", req =>
                                        {
                                            if (Me.CurrentEclipse > 20 && !Me.HasAura("Solar Empowerment"))
                                            {
                                                if (SingularSettings.Debug && Spell.CanCastHack("Starsurge", Me.CurrentTarget))
                                                    Logger.WriteDebug(LogColor.Hilite, "Starsurge: buffing Solar Empowerment ");
                                                return true;
                                            }
                                            return false;
                                        }),

                                        //  3 Starsurge with 3 charges. Watch for Shooting Stars procs.
                                        Spell.Cast("Starsurge", req =>
                                        {
                                            if (Spell.GetCharges("Starsurge") >= 3 && SpellManager.HasSpell("Shooting Stars"))
                                            {
                                                if (SingularSettings.Debug && Spell.CanCastHack("Starsurge", Me.CurrentTarget))
                                                    Logger.WriteDebug(LogColor.Hilite, "Starsurge: dumping excess Starsurge Charge");
                                                return true;
                                            }
                                            return false;
                                        })
                                        )
                                    ),

                                //  4 Sunfire to maintain DoT and consume Solar Peak buff.
                                Spell.Cast("Sunfire", req => Me.CurrentEclipse > 0 && (Me.CurrentTarget.HasKnownAuraExpired("Sunfire", 3) || Me.HasAura("Solar Peak"))),

                                //  5 Moonfire to maintain DoT and consume Lunar Peak buff.
                                Spell.Cast("Moonfire", req => Me.CurrentEclipse <= 0 && (Me.CurrentTarget.HasAuraExpired("Moonfire", 3) || Me.HasAura("Lunar Peak"))),

                                // multi-mob dotting
                                new PrioritySelector(
                                    ctx => Unit.UnitsInCombatWithUsOrOurStuff(40).Where(u => u.Combat && !u.IsCrowdControlled() && Me.IsSafelyFacing(u)).ToList(),
                                    Spell.Cast("Sunfire", on => ((List<WoWUnit>)on).FirstOrDefault(u => u.HasAuraExpired("Sunfire", 0)), req => Me.CurrentEclipse > 0 && Me.CurrentTarget.HasKnownAuraExpired("Sunfire", 0)),
                                    Spell.Cast("Moonfire", on => ((List<WoWUnit>)on).FirstOrDefault(u => u.HasAuraExpired("Moonfire", 0)), req => Me.CurrentEclipse > 0 && Me.CurrentTarget.HasKnownAuraExpired("Moonfire", 0))
                                    ),

                                //  6 Wrath as a filler when in a Solar Eclipse.
                                Spell.Cast("Wrath", req => Me.CurrentEclipse > 0),

                                //  7 Starfire as a filler when in a Lunar Eclipse.     
                                Spell.Cast("Starfire", req => Me.CurrentEclipse <= 0)
                                )
                            )
                        )
                    )
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
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                // Ensure we do /petattack if we have treants up.
                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        Common.CastForm( ShapeshiftForm.Moonkin, req => !Utilities.EventHandlers.IsShapeshiftSuppressed),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        CreateBalanceFaerieFireBehavior(),

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
                                    Spell.CastOnGround("Ursol's Vortex", on => (WoWUnit)on, req => Me.GotTarget(), false),
                                    Spell.Cast("Entangling Roots", on => (WoWUnit)on),
                                    new ActionAlwaysSucceed()
                                    )
                                )
                            ),

                        // Helpers.Common.CreateInterruptBehavior(),

                        Spell.Cast("Mighty Bash", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        Spell.Cast("Typhoon",
                            ret => Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8f) >= 1),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        // use every Shooting Stars proc
                        Spell.Cast("Starsurge", on => Me.CurrentTarget, req => Me.ActiveAuras.ContainsKey("Shooting Stars"), cancel => false),

                        // Spread MF/IS on Rouges / Feral Druids first
                        /*
                        Common.CreateFaerieFireBehavior(
                            on => (WoWUnit)Unit.NearbyUnfriendlyUnits.FirstOrDefault(p => (p.Class == WoWClass.Rogue || p.Shapeshift == ShapeshiftForm.Cat) && !p.HasAnyAura("Faerie Swarm") && p.Distance < 38 && Me.IsSafelyFacing(p) && p.InLineOfSpellSight), 
                            req => true
                            ),
                        */
                        // More DoTs!!  Dot EVERYTHING (including pets) to boost Shooting Stars proc chance
                        new Decorator(
                            req => SpellManager.HasSpell("Sunfire"),
                            new PrioritySelector(
                                ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => !u.HasMyAura("Sunfire") && !u.IsCrowdControlled() && Me.IsSafelyFacing(u) && u.InLineOfSpellSight),
                                Spell.Buff("Sunfire", on => (WoWUnit)on)
                                )
                            ),

                        new Decorator(
                            req => SpellManager.HasSpell("Moonfire"),
                            new PrioritySelector(
                                ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => !u.HasMyAura("Moonfire") && !u.IsCrowdControlled() && Me.IsSafelyFacing(u) && u.InLineOfSpellSight),
                                Spell.Buff("Moonfire", on => (WoWUnit)on)
                                )
                            ),

                        Spell.Cast("Starfall", req => IsStarfallNeeded),

                        new Decorator(
                            ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.CurrentTargetGuid == Me.Guid),
                            new PrioritySelector(
                                ctx => GetEclipseDirection() == EclipseType.Lunar,
                                Spell.Cast("Starfire", ret => !(bool) ret ),
                                Spell.Cast("Wrath", ret => (bool) ret)
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
                    )
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

                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Common.CastForm( ShapeshiftForm.Moonkin, req => !Utilities.EventHandlers.IsShapeshiftSuppressed),

                        // Spell.Cast("Mighty Bash", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        new PrioritySelector(
                            ctx => !Spell.UseAOE ? 1 : Unit.UnfriendlyUnitsNearTarget(10f).Count(),

                            new Decorator(
                                req => ((int)req) > 1,
                                new PrioritySelector(

                                    Spell.Cast("Starfall", req => IsStarfallNeeded),

                                    Spell.CastOnGround("Hurricane", on => Me.CurrentTarget, req => ((int)req) > 6, false),

                                    new PrioritySelector(
                                        ctx => Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && !u.IsCrowdControlled() && Me.IsSafelyFacing(u)).ToList(),
                                        Spell.Buff("Sunfire", on => (WoWUnit) ((List<WoWUnit>)on).FirstOrDefault(u => u.HasAuraExpired("Sunfire", 2))),
                                        Spell.Buff("Moonfire", on => (WoWUnit) ((List<WoWUnit>)on).FirstOrDefault(u => u.HasAuraExpired("Moonfire", 2))),
                                        Spell.CastOnGround("Hurricane", on => Me.CurrentTarget, req => Spell.UseAOE, false)
                                        )
                                    )
                                )
                            ),

                        Helpers.Common.CreateInterruptBehavior(),

                        new Decorator(
                            req => true,
                            new PrioritySelector(
                                //  1 Starsurge when Lunar Empowerment is down and Eclipse energy is > 20.
                                Spell.Cast("Starsurge", req => Me.CurrentEclipse < -20 && !Me.HasAura("Lunar Empowerment")),

                                //  2 Starsurge when Solar Empowerment is down and Eclipse energy is > 20.
                                Spell.Cast("Starsurge", req => Me.CurrentEclipse > 20 && !Me.HasAura("Solar Empowerment")),

                                //  3 Starsurge with 3 charges. Watch for Shooting Stars procs.
                                Spell.Cast("Starsurge", req => Spell.GetCharges("Starsurge") >= 3),

                                //  4 Sunfire to maintain DoT and consume Solar Peak buff.
                                Spell.Cast("Sunfire", req => Me.CurrentTarget.HasAuraExpired("Sunfire", 3) || Me.HasAura("Solar Peak")),

                                //  5 Moonfire to maintain DoT and consume Lunar Peak buff.
                                Spell.Cast("Moonfire", req => Me.CurrentTarget.HasAuraExpired("Moonfire", 3) || Me.HasAura("Lunar Peak")),

                                //  6 Wrath as a filler when in a Solar Eclipse.
                                Spell.Cast("Wrath", req => Me.HasAura("Eclipse Visual (Solar)")),

                                //  7 Starfire as a filler when in a Lunar Eclipse.     
                                Spell.Cast("Starfire", req => Me.HasAura("Eclipse Visual (Lunar)"))
                                )
                            )
                        )
                    )
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
                    // if (StyxWoW.Me.HasAura("Eclipse (Solar)"))
                    if (StyxWoW.Me.HasAura("Solar Peak"))
                        eclipseCurrent = EclipseType.Solar;
                    // else if (StyxWoW.Me.HasAura("Eclipse (Lunar)"))
                    else if (StyxWoW.Me.HasAura("Lunar Peak"))
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
                    new Action(r => Logger.WriteDebug("Refresh DoT: Moonfire")),
                    Spell.Buff("Moonfire", ret => newEclipseDotNeeded && eclipseLastCheck == EclipseType.Lunar),
                    new Action(ret => newEclipseDotNeeded = false )
                    ),

                new Sequence(
                    new Action(r => Logger.WriteDebug("Refresh DoT: Sunfire")),
                    Spell.Buff("Sunfire", ret => newEclipseDotNeeded && eclipseLastCheck == EclipseType.Solar),
                    new Action(ret => newEclipseDotNeeded = false )
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


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.All, 9)]
        public static Composite CreateBalancePreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Flap", req => glyphFlappingOwl && Me.Shapeshift == ShapeshiftForm.Moonkin && Me.IsFalling)
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Battlegrounds | WoWContext.Instances, 2)]
        public static Composite CreateBalancePreCombatBuffBattlegrounds()
        {
#if DO_WE_NEED_THIS
            return new PrioritySelector(
                new Decorator(
                    ret => SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds || !Unit.NearbyUnfriendlyUnits.Any(),
                    new PrioritySelector(
                        Spell.BuffSelf( "Astral Communion", ret => PVP.PrepTimeLeft < 20 && !Me.HasAnyAura("Eclipse (Lunar)","Eclipse (Solar)"))
                        )
                    )
                );
#else
            return new ActionAlwaysFail();
#endif
        }


        [Behavior(BehaviorType.PullBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.All, 2)]
        public static Composite CreateBalancePullBuff()
        {
            return new PrioritySelector(
                CreateBalanceDiagnosticOutputBehavior(),
                Common.CastForm( ShapeshiftForm.Moonkin, req => !Utilities.EventHandlers.IsShapeshiftSuppressed)
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Normal, 2)]
        public static Composite CreateBalanceCombatBuffNormal()
        {
            return new PrioritySelector(
                Common.CastForm( ShapeshiftForm.Moonkin, req => !Utilities.EventHandlers.IsShapeshiftSuppressed),
                Spell.BuffSelf("Flap", req => glyphFlappingOwl && Me.Shapeshift == ShapeshiftForm.Moonkin && Me.IsFalling)
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Battlegrounds | WoWContext.Instances, 2)]
        public static Composite CreateBalanceCombatBuffBattlegrounds()
        {
            return new PrioritySelector(
                Common.CastForm( ShapeshiftForm.Moonkin, req => !Utilities.EventHandlers.IsShapeshiftSuppressed),
                Spell.BuffSelf("Flap", req => glyphFlappingOwl && Me.Shapeshift == ShapeshiftForm.Moonkin && Me.IsFalling)
                );
        }


        private static bool IsStarfallNeeded
        {
            get
            {
                if (!glyphUntamedStars)
                {
                    if (3 <= scenario.Mobs.Count(u => u.HasAnyOfMyAuras("Sunfire", "Moonfire")))
                        return true;
                    return false;
                }

                return !scenario.AvoidAOE;
            }
        }
        private static Composite CreateBalanceFaerieFireBehavior()
        {
            if (!SpellManager.HasSpell("Faerie Swarm"))
                return new ActionAlwaysFail();

            return Common.CreateFaerieFireBehavior(
                on => Me.CurrentTarget,
                req => Me.GotTarget()
                    && Me.CurrentTarget.IsPlayer
                    && (Me.CurrentTarget.Class == WoWClass.Rogue || Me.CurrentTarget.Shapeshift == ShapeshiftForm.Cat)
                    && !Me.CurrentTarget.HasAnyAura("Faerie Fire", "Faerie Swarm")
                    && Me.CurrentTarget.SpellDistance() < 35
                    && Me.IsSafelyFacing(Me.CurrentTarget)
                    && Me.CurrentTarget.InLineOfSpellSight
                );
        }

        private static WaitTimer detonateTimer = new WaitTimer(TimeSpan.FromSeconds(4));
        private static int checkMushroomCount { get; set; }

        private static Composite CreateMushroomSetAndDetonateBehavior()
        {
            return new Decorator( 
                req => Spell.UseAOE, 
                new PrioritySelector(

                    new Action( r => { 
                        checkMushroomCount = MushroomCount; 
                        return RunStatus.Failure; 
                        }),

                    // detonate if we have 3 shrooms -or- or timer since last shroom cast has expired
                    // Spell.Cast("Wild Mushroom: Detonate", ret => checkMushroomCount >= 3 || (checkMushroomCount > 0 && detonateTimer.IsFinished)),
                    Spell.Cast("Wild Mushroom: Detonate", ret => checkMushroomCount > 0),

                    // Make sure we arenIf Detonate is coming off CD, make sure we drop some more shrooms. 3 seconds is probably a little late, but good enough.
                    // .. also, waitForSpell must be false since Wild Mushroom does not stop targeting after click like other click on ground spells
                    // .. will wait locally and fall through to cancel targeting regardless
                    new Sequence(
                        // Spell.CastOnGround("Wild Mushroom", on => BestAoeTarget, req => checkMushroomCount < 3 && Spell.GetSpellCooldown("Wild Mushroom: Detonate").TotalSeconds < 3f, false),
                        Spell.CastOnGround("Wild Mushroom", on => BestAoeTarget, req => checkMushroomCount < 1 && Spell.GetSpellCooldown("Wild Mushroom: Detonate").TotalSeconds < 1f, false),
                        new Action(ctx => detonateTimer.Reset()), 
                        new Action( ctx => Lua.DoString("SpellStopTargeting()"))                       
                        )
                    )
                );

        }

        #region Diagnostics

        private static Composite CreateBalanceDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    string log;
                    WoWAura eclips = Me.GetAllAuras().FirstOrDefault(a => a.Name == "Eclipse (Solar)" || a.Name == "Eclipse (Lunar)");
                    string eclipsString = eclips == null ? "None" : (eclips.Name == "Eclipse (Solar)" ? "Solar" : "Lunar");
                    WoWAura empower = Me.GetAllAuras().FirstOrDefault(a => a.Name == "Solar Empowerment" || a.Name == "Lunar Empowerment");
                    string empowerString = empower == null ? "None" : empower.Name.Remove(5);
                    WoWAura peak = Me.GetAllAuras().FirstOrDefault(a => a.Name == "Solar Peak" || a.Name == "Lunar Peak");
                    string peakString = peak == null ? "None" : peak.Name.Remove(5);
                    WoWAura visual = Me.GetAllAuras().FirstOrDefault(a => a.Name == "Eclipse Visual (Solar)" || a.Name == "Eclipse Visual (Solar)");
                    string visualString = visual == null ? "None" : visual.Name.Substring(16,5);

                    log = string.Format(".... h={0:F1}%/m={1:F1}%, form:{2}, empower={3}, peak={4}, visual={5}, eclps={6}, towards={7}, eclps#={8}, mushcnt={9}",
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.Shapeshift.ToString(),
                        empowerString,
                        peakString,
                        visualString,
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
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }

}
