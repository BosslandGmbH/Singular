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
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.Common;
using System.Drawing;

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

        private static string _oldDps = "Wrath";

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

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidBalance)]
        public static Composite CreateDruidBalanceHeal()
        {
            _ImaMoonBeast = TalentManager.HasGlyph( "Moonbeast");

            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    Spell.Cast( "Rejuvenation", mov => false, on => Me, ret => Me.Combat && _ImaMoonBeast && Me.HasAuraExpired("Rejuvenation")), 

                    Spell.Cast("Renewal", ret => Me.HealthPercent < DruidSettings.RenewalHealth),
                    Spell.BuffSelf("Cenarion Ward", ret => Me.HealthPercent < 75 || Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro || (u.Combat && u.IsTargetingMeOrPet)) > 1),

                    Common.CreateNaturesSwiftnessHeal(ret => Me.HealthPercent < 60),

                    new Decorator(
                        ret => Me.HealthPercent < 40,
                        new PrioritySelector(

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
                                Spell.Cast("Healing Touch", on => Me)
                                )
                            )
                        )
                    )
                );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Normal)]
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

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateBalanceDiagnosticOutputBehavior(),

                        Spell.BuffSelf("Innervate",
                            ret => StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),

                        Spell.BuffSelf("Moonkin Form"),

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
                                                u.Combat && !u.IsCrowdControlled() && !u.HasMyAura("Moonfire"))),

                                Spell.Cast("Sunfire",
                                    ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u =>
                                                u.Combat && !u.IsCrowdControlled() && !u.HasMyAura("Sunfire")))
                                )
                            ),

                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                        // make sure we always have DoTs 
                        new Sequence(
                            Spell.Cast("Sunfire", ret => !Me.CurrentTarget.HasMyAura("Sunfire")),
                            new Action(ret => Logger.WriteDebug("Adding DoT:  Sunfire"))
                            ),

                        new Sequence(
                            Spell.Cast("Moonfire", ret => !Me.CurrentTarget.HasMyAura("Moonfire")),
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

                // Ensure we do /petattack if we have treants up.
                Helpers.Common.CreateAutoAttack(true),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        CreateBalanceDiagnosticOutputBehavior(),

                        //Inervate

                        Spell.BuffSelf("Moonkin Form"),
                        Spell.BuffSelf("Barkskin", 
                            ret => StyxWoW.Me.IsCrowdControlled() || StyxWoW.Me.HealthPercent < 40),

                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                        // Spread MF/IS
                        Spell.CastOnGround("Force of Nature", ret => StyxWoW.Me.CurrentTarget.Location),

                        Spell.Buff("Faerie Fire", 
                            ret => StyxWoW.Me.CurrentTarget.Class == WoWClass.Rogue ||
                                   StyxWoW.Me.CurrentTarget.Class == WoWClass.Druid),

                        Spell.Cast("Typhoon",
                            ret => Me.CurrentTarget.IsWithinMeleeRange 
                                && Me.CurrentTarget.Class != WoWClass.Priest
                                && (Me.CurrentTarget.Class != WoWClass.Warlock || Me.CurrentTarget.CastingSpellId == 1949 /*Hellfire*/ || Me.CurrentTarget.HasAura("Immolation Aura"))
                                && Me.CurrentTarget.Class != WoWClass.Hunter 
                                && Me.IsSafelyFacing( Me.CurrentTarget, 90f)),

                        Spell.Cast("Mighty Bash", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        // make sure we always have DoTs 
                        new Sequence(
                            Spell.Cast("Sunfire", ret => Me.CurrentTarget.HasAuraExpired("Sunfire", 2)),
                            new Action(ret => Logger.WriteDebug("Adding DoT:  Sunfire"))
                            ),

                        new Sequence(
                            Spell.Cast("Moonfire", ret => Me.CurrentTarget.HasAuraExpired("Moonfire", 2)),
                            new Action(ret => Logger.WriteDebug("Adding DoT:  Moonfire"))
                            ),

                        CreateDoTRefreshOnEclipse(),

                        Spell.Cast("Starsurge"),
                        Spell.Cast("Starfall", ret => DruidSettings.UseStarfall),

                        Spell.Cast("Wrath",
                            ret => GetEclipseDirection() == EclipseType.Lunar),

                        Spell.Cast("Starfire",
                            ret => GetEclipseDirection() == EclipseType.Solar)
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

                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

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
                        newEclipseDotNeeded = true;
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

        private static EclipseType lastEclipse;

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

                        log = string.Format(".... h={0:F1}%/m={1:F1}%, eclps={2}, towards={3}, eclps#={4}, mushcnt={5}",
                            Me.HealthPercent,
                            Me.ManaPercent,
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
    }
}
