﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using Styx.CommonBot;
using System.Drawing;

using CommonBehaviors.Actions;
using Styx.Common;
using Action = Styx.TreeSharp.Action;


namespace Singular.ClassSpecific.Shaman
{
    class Restoration
    {
        private const int RESTO_T12_ITEM_SET_ID = 1014;
        private const int ELE_T12_ITEM_SET_ID = 1016;

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman(); } }

        private static int ChainHealCastTime { get; set; }
        private static float ChainHealHopDistance = 15;

        //private static WoWGuid guidLastHealTarget;

        #region INIT

        [Behavior(BehaviorType.Initialize, WoWClass.Shaman, WoWSpec.ShamanRestoration)]
        public static Composite CreateRestoShamanInitialize()
        {
            ChainHealCastTime = (int)Spell.GetSpellCastTime("Chain Heal").TotalMilliseconds;

            return null;
        }

        #endregion

        #region REST

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanRestoration)]
        public static Composite CreateRestoShamanRest()
        {
            return new PrioritySelector(
                Singular.Helpers.Rest.CreateDefaultRestBehaviour("Healing Surge", "Ancestral Spirit"),
                CreateRestoShamanHealingOnlyBehavior(selfOnly: false)
                );
        }

        #endregion

        #region HEAL

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Normal)]
        public static Composite CreateRestoShamanHealBehaviorNormal()
        {
            // for Solo only, use DPS Heal logic
            return Common.CreateShamanDpsHealBehavior();
        }

        #endregion

        #region NORMAL

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Normal)]
        public static Composite CreateRestoShamanCombatBehaviorSolo()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMediumRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateRestoDiagnosticOutputBehavior(on => null),

                        Helpers.Common.CreateInterruptBehavior(),
                        Totems.CreateTotemsBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Dispelling.CreatePurgeEnemyBehavior("Purge"),

                        Common.CastElementalBlast(),
                        Spell.Buff("Flame Shock", 3, on => Me.CurrentTarget, req => true),
                        Spell.Cast("Lava Burst"),
                        Spell.Cast("Frost Shock"),
                        Spell.Cast("Chain Lightning", ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && !Unit.UnfriendlyUnitsNearTarget(12f).Any(u => u.IsCrowdControlled())),
                        Spell.Cast("Lightning Bolt")
                        )
                    )
                );
        }

        #endregion

        #region BATTLEGROUNDS

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Battlegrounds)]
        public static Composite CreateRestoShamanCombatBehaviorBattlegrounds()
        {
            return new PrioritySelector(

                Spell.WaitForCastOrChannel(),

                CreateRestoDiagnosticOutputBehavior(on => HealerManager.Instance.FirstUnit),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && HealerManager.Instance.TargetList.Any(t => !t.IsMe && t.IsAlive),
                    new PrioritySelector(
                        HealerManager.CreateStayNearTankBehavior(),
                        CreateRestoShamanHealingOnlyBehavior(selfOnly: false)
                        )
                    ),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && HealerManager.AllowHealerDPS(),
                    new PrioritySelector(

                        Helpers.Common.EnsureReadyToAttackFromMediumRange(),

                        Spell.WaitForCastOrChannel(),

                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                Helpers.Common.CreateInterruptBehavior(),
                                Totems.CreateTotemsBehavior(),

                                Movement.WaitForFacing(),
                                Movement.WaitForLineOfSpellSight(),

                                Dispelling.CreatePurgeEnemyBehavior("Purge"),

                                Common.CastElementalBlast(),
                                Spell.Buff("Flame Shock", 3, on => Me.CurrentTarget, req => true),
                                Spell.Cast("Lava Burst"),
                                Spell.Cast("Frost Shock"),
                                Spell.Cast("Chain Lightning", ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && !Unit.UnfriendlyUnitsNearTarget(12f).Any(u => u.IsCrowdControlled())),
                                Spell.Cast("Lightning Bolt")
                                )
                            )
                        )
                    )
                );
        }

        #endregion

        #region INSTANCES

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Instances)]
        public static Composite CreateRestoShamanCombatBehaviorInstances()
        {
#if OLD_APPROACH
            return new PrioritySelector(

                ctx => HealerManager.Instance.TargetList.Any( t => !t.IsMe && t.IsAlive ),

                Safers.EnsureTarget(),
                Movement.CreateFaceTargetBehavior(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateRestoDiagnosticOutputBehavior(on => Me.CurrentTarget),

                        new Decorator(
                            ret => (bool) ret,
                            new PrioritySelector(
                                HealerManager.CreateStayNearTankBehavior(),
                                CreateRestoShamanHealingOnlyBehavior( selfOnly:false),
                                Helpers.Common.CreateInterruptBehavior(),
                                Dispelling.CreatePurgeEnemyBehavior("Purge"),
                                Totems.CreateTotemsBehavior(),
                                Spell.Cast("Lightning Bolt", ret => TalentManager.HasGlyph("Telluric Currents"))
                                )
                            ),

                        new Decorator(
                            ret => !((bool) ret),
                            new PrioritySelector(
                                CreateRestoDiagnosticOutputBehavior( on => HealerManager.Instance.FirstUnit),
                                Spell.Cast("Elemental Blast"),
                                Spell.Buff("Flame Shock", true, on => Me.CurrentTarget, req => true, 3),
                                Spell.Cast("Lava Burst"),
                                Spell.Cast("Frost Shock"),
                                Spell.Cast("Chain Lightning", ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && !Unit.UnfriendlyUnitsNearTarget(12f).Any(u => u.IsCrowdControlled())),
                                Spell.Cast("Lightning Bolt")
                                )
                            )

                        )
                    )
                );
#else

            return new PrioritySelector(

                Spell.WaitForCastOrChannel(),
                CreateRestoDiagnosticOutputBehavior(on => HealerManager.FindLowestHealthTarget()),

                HealerManager.CreateStayNearTankBehavior(),

                new Decorator(
                    ret => Me.Combat && HealerManager.AllowHealerDPS(),
                    new PrioritySelector(

                        Helpers.Common.EnsureReadyToAttackFromMediumRange(),
                        Movement.CreateFaceTargetBehavior(),
                        Spell.WaitForCastOrChannel(),

                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(
                                Helpers.Common.CreateInterruptBehavior(),

                                Totems.CreateTotemsBehavior(),

                                Movement.WaitForFacing(),
                                Movement.WaitForLineOfSpellSight(),

                                Common.CastElementalBlast(cancel: c => HealerManager.CancelHealerDPS()),
                                Spell.Buff("Flame Shock", 3, on => Me.CurrentTarget, req => true),
                                Spell.Cast("Lava Burst", on => Me.CurrentTarget, req => true, cancel => HealerManager.CancelHealerDPS()),
                                Spell.Cast("Frost Shock"),
                                Spell.Cast("Chain Lightning", on => Me.CurrentTarget, req => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && !Unit.UnfriendlyUnitsNearTarget(12f).Any(u => u.IsCrowdControlled()), cancel => HealerManager.CancelHealerDPS()),
                                Spell.Cast("Lightning Bolt", on => Me.CurrentTarget, req => true, cancel => HealerManager.CancelHealerDPS())
                                )
                            )
                        )
                    ),

                new Decorator(
                    ret => Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                    new PrioritySelector(
                        CreateRestoShamanHealingOnlyBehavior(selfOnly: false),
                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Purge"),
                        Totems.CreateTotemsBehavior(),
                        new Decorator(
                            req => TalentManager.HasGlyph("Telluric Currents"),
                            new PrioritySelector(
                                Safers.EnsureTarget(),
                                Movement.CreateFaceTargetBehavior(),
                                Spell.Cast("Lightning Bolt",
                                    mov => true,
                                    on => Unit.NearbyUnitsInCombatWithUsOrOurStuff
                                        .Where(u => u.IsAlive && u.SpellDistance() < 40 && Me.IsSafelyFacing(u))
                                        .OrderByDescending(u => u.HealthPercent)
                                        .FirstOrDefault(),
                                    req => !HealerManager.Instance.TargetList.Any(h => h.IsAlive && h.SpellDistance() < 40 && h.HealthPercent < ShamanSettings.RestoHealSettings.TelluricHealthCast),
                                    cancel => HealerManager.Instance.TargetList.Any(h => h.IsAlive && h.SpellDistance() < 40 && h.HealthPercent < ShamanSettings.RestoHealSettings.TelluricHealthCancel)
                                    )
                                )
                            )
                        )
                    )
                );

#endif
        }

        #endregion

        private static WoWUnit _moveToHealUnit = null;

        public static Composite CreateRestoShamanHealingOnlyBehavior(bool selfOnly = false)
        {
            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();
            int cancelHeal = (int)Math.Max(SingularSettings.Instance.IgnoreHealTargetsAboveHealth, Math.Max(ShamanSettings.RestoHealSettings.HealingWave, ShamanSettings.RestoHealSettings.HealingSurge));

            bool moveInRange = false;
            if (!selfOnly)
                moveInRange = (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds);

            Logger.WriteDebugInBehaviorCreate("Shaman Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);

            int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
                behavs.AddBehavior(dispelPriority, "Purify Spirit", null, Dispelling.CreateDispelBehavior());

            #region Save the Group

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.SpiritLinkTotem) + 600, "Spirit Link Totem", "Spirit Link Totem",
                new Decorator(
                    ret => Me.Combat && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
                    Spell.CastOnGround("Spirit Link Totem",
                            on => (WoWUnit)on,
                            ret => HealerManager.Instance.TargetList.Count(
                                p => p.PredictedHealthPercent() < ShamanSettings.RestoHealSettings.SpiritLinkTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.SpiritLink)) >= ShamanSettings.RestoHealSettings.MinSpiritLinkCount
                        )
                    )
                );

            #endregion

            #region AoE Heals

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.HealingTideTotem) + 400, "Healing Tide Totem", "Healing Tide Totem",
                new Decorator(
                    ret => (Me.Combat || ((WoWUnit)ret).Combat)
                        && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid)
                        && (!Totems.Exist(WoWTotem.Cloudburst) || (Totems.GetTotem(WoWTotem.Cloudburst).Expires - DateTime.UtcNow).TotalMilliseconds < 1500),
                    Spell.Cast(
                        "Healing Tide Totem",
                        on => Me,
                        req => Me.Combat && HealerManager.Instance.TargetList.Count(p => p.PredictedHealthPercent() < ShamanSettings.RestoHealSettings.HealingTideTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= ShamanSettings.RestoHealSettings.MinHealingTideCount
                        )
                    )
                );

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.HealingStreamTotem) + 300, "Healing Stream Totem", "Healing Stream Totem",
                new Decorator(
                    ret => Me.Combat && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
                    Spell.Cast(
                        "Healing Stream Totem",
                        on =>
                        {
                            if (Totems.Exist(WoWTotemType.Water))
                                return null;

                            if (Spell.IsSpellOnCooldown(Totems.ToSpellId(WoWTotem.HealingStream)))
                                return null;

                            // if tank in group, make sure we are near the tank
                            WoWUnit tank = HealerManager.TankToStayNear;
                            if (tank != null)
                            {
                                if (!HealerManager.IsTankSettledIntoFight(tank))
                                    return null;
                                if (tank.Distance > Totems.GetTotemRange(WoWTotem.HealingStream))
                                    return null;
                            }

                            WoWUnit unit = HealerManager.Instance.TargetList
                                .FirstOrDefault(
                                    p => p.PredictedHealthPercent() < ShamanSettings.RestoHealSettings.HealingStreamTotem
                                        && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingStream)
                                    );

                            return unit;
                        }
                        )
                    )
                );

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.GiftoftheQueen) + 300, "Gift of the Queen", "Gift of the Queen",
                new Decorator(
                    ret => Me.Combat && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
                    new PrioritySelector(
                        context => GetBestGiftoftheQueenTarget(),
                        new Decorator(
                            ret => ret != null,
                            Spell.CastOnGround("Gift of the Queen", on => (WoWUnit)on, req => true, false)
                            )
                        )
                    )
                );

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.CloudburstTotem) + 300, "Cloudburst Totem", "Cloudburst Totem",
                new Decorator(
                    ret => Me.Combat && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
                    Spell.Cast(
                        "Cloudburst Totem",
                        on =>
                        {
                            if (Totems.Exist(WoWTotemType.Water))
                                return null;

                            if (Spell.IsSpellOnCooldown(Totems.ToSpellId(WoWTotem.Cloudburst)))
                                return null;

                            if (Unit.ValidUnit(Me.CurrentTarget) && (Me.CurrentTarget.TimeToDeath() < 20 || Unit.UnitsInCombatWithUsOrOurStuff().Count() < 3))
                                return null;

                            // if tank in group, make sure we are near the tank
                            WoWUnit tank = HealerManager.TankToStayNear;
                            if (tank != null)
                            {
                                if (!HealerManager.IsTankSettledIntoFight(tank))
                                    return null;
                                if (tank.Distance > Totems.GetTotemRange(WoWTotem.Cloudburst))
                                    return null;
                            }

                            WoWUnit unit = HealerManager.Instance.TargetList
                                .Where(
                                    p => p.HealthPercent < ShamanSettings.RestoHealSettings.CloudburstTotem
                                        && p.Distance <= Totems.GetTotemRange(WoWTotem.Cloudburst)
                                    )
                                .OrderBy(p => (int)p.HealthPercent)
                                .FirstOrDefault();

                            return unit;
                        }
                        )
                    )
                );

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.HealingRain) + 200, "Healing Rain", "Healing Rain",
                new Decorator(
                    ret => Me.Combat && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
                    new PrioritySelector(
                        context => GetBestHealingRainTarget(),
                        new Decorator(
                            ret => ret != null,
                            Spell.CastOnGround("Healing Rain", on => (WoWUnit)on, req => true, false)
                            )
                        )
                    )
                );

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.ChainHeal) + 150, "Chain Heal", "Chain Heal",
                new Decorator(
                    ret => Me.Combat  && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
                    new PrioritySelector(
                        ctx => GetBestChainHealTarget(),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new DecoratorContinue(
                                    req => ((WoWUnit)req).HasAuraExpired("Riptide", TimeSpan.FromMilliseconds(ChainHealCastTime), true),
                                    new Sequence(
                                        Spell.Cast("Riptide", on => (WoWUnit)on, req => true, cancel => false),
                                        new Wait(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown(LagTolerance.No), new ActionAlwaysSucceed()),
                                        new Action(r => TidalWaveRefresh())
                                        )
                                    ),
                                new WaitContinue(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown(LagTolerance.No), new ActionAlwaysSucceed()),
                                Spell.Cast("Chain Heal", on => (WoWUnit)on),
                                new Action(r => TidalWaveRefresh())
                                )
                            )
                        )
                    )
                );

            #endregion

            #region Single Target Heals

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.HealingWave), "Healing Wave", "Healing Wave",
                new Decorator(ret => ((WoWUnit)ret).PredictedHealthPercent() < ShamanSettings.RestoHealSettings.HealingWave,
                    new Sequence(
                        new WaitContinue(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                        new WaitContinue(2, until => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                        Spell.Cast("Healing Wave",
                            mov => true,
                            on => (WoWUnit)on,
                            req => true,
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal),
                        new Action(r => TidalWaveConsume())
                        )
                    )
                );

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.HealingSurge), "Healing Surge", "Healing Surge",
                new Decorator(ret => ((WoWUnit)ret).PredictedHealthPercent() < ShamanSettings.RestoHealSettings.HealingSurge,
                    new Sequence(
                        new WaitContinue(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                        new WaitContinue(2, until => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                        Spell.Cast("Healing Surge",
                            mov => true,
                            on => (WoWUnit)on,
                            req => true,
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal),
                        new Action(r => TidalWaveConsume())
                        )
                    )

                );

            #endregion

            #region Healing Cooldowns

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.Ascendance) + 100, "Ascendance", "Ascendance",
                new Decorator(
                    ret => ShamanSettings.UseAscendance && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
                    Spell.BuffSelf(
                        "Ascendance",
                        ret => HealerManager.Instance.TargetList.Count(p => p.PredictedHealthPercent() < ShamanSettings.RestoHealSettings.Ascendance) >= ShamanSettings.RestoHealSettings.MinAscendanceCount
                        )
                    )
                );

            #endregion

            behavs.OrderBehaviors();

            if (selfOnly == false && CompositeBuilder.CurrentBehaviorType == BehaviorType.Combat)
                behavs.ListBehaviors();


            return new PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.FindLowestHealthTarget(),
                // HealerManager.Instance.FirstUnit,

                CreateRestoDiagnosticOutputBehavior(ret => (WoWUnit) ret),

                new Decorator(
                    ret =>
                        ret != null &&
                        (Me.Combat || ((WoWUnit) ret).Combat || ((WoWUnit) ret).PredictedHealthPercent() <= 99),

                    new PrioritySelector(
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                Totems.CreateTotemsBehavior(),

                                // roll Riptide on Tanks otherwise
                                new Sequence(
                                    Spell.Cast("Riptide", on =>
                                    {
                                        WoWUnit unit = GetBestRiptideTankTarget();
                                        if (unit != null && Spell.CanCastHack("Riptide", unit, skipWowCheck: true))
                                        {
                                            Logger.WriteDebug("Buffing RIPTIDE ON TANK: {0}", unit.SafeName());
                                            return unit;
                                        }
                                        return null;
                                    }),
                                    new Action(r => TidalWaveRefresh())
                                    ),

                                // cast Riptide if we are a low level
                                CreateRestoShamanBuffRiptideLowLevel(),

                                // cast Riptide if we need Tidal Waves -- skip if Ancestral Swiftness is
                                CreateRestoShamanBuffTidalWaves(),

                                behavs.GenerateBehaviorTree(),

                                // cast Riptide if we need Tidal Waves -- skip if Ancestral Swiftness is
                                new Decorator(
                                    ret =>
                                    {
                                        int rollCount =
                                            HealerManager.Instance.TargetList.Count(
                                                u => u.IsAlive && u.HasMyAura("Riptide"));
                                        // Logger.WriteDebug("GetBestRiptideTarget:  currently {0} group members have my Riptide", rollCount);
                                        return rollCount < ShamanSettings.RestoHealSettings.RollRiptideCount;
                                        },
                                    new Sequence(
                                        Spell.Cast("Riptide", on =>
                                        {
                                            // if tank needs Riptide, bail out on Rolling as they have priority
                                            if (GetBestRiptideTankTarget() != null)
                                                return null;

                                            // get the best target from all wowunits in our group
                                            WoWUnit unit = GetBestRiptideTarget();
                                            if (unit != null)
                                                Logger.WriteDebug(Color.White, "ROLLING RIPTIDE on: {0}",
                                                    unit.SafeName());

                                            return unit;
                                        }),
                                        new Action(r => TidalWaveRefresh())
                                        )
                                    ),

                            new Decorator(
                                    ret => moveInRange,
                                    new Sequence(
                                        new Action(r => _moveToHealUnit = (WoWUnit) r),
                                        new PrioritySelector(
                                            Movement.CreateMoveToLosBehavior(on => _moveToHealUnit),
                                            Movement.CreateMoveToUnitBehavior(on => _moveToHealUnit, 40f, 34f)
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        private static Composite CreateRestoShamanBuffTidalWaves()
        {
            return new Decorator(
                ret => IsTidalWavesNeeded,
                new Sequence(
                    Spell.Cast("Riptide", on =>
                    {
                        WoWUnit unit = GetBestRiptideTarget();
                        if (unit != null)
                            Logger.WriteDebug(Color.White, "BUFFING TIDAL WAVES with Riptide on: {0}", unit.SafeName());
                        return unit;
                        }),
                    new Action(r => TidalWaveRefresh())
                    )
                );
        }

        private static Composite CreateRestoShamanBuffRiptideLowLevel()
        {
            return new Decorator(
                ret => Me.Level < 50,
                new Sequence(
                    Spell.Cast("Riptide", on =>
                    {
                        WoWUnit unit = GetBestRiptideTarget();
                        if (unit != null)
                            Logger.WriteDebug(Color.White, "BUFFING Riptide on: {0}", unit.SafeName());
                        return unit;
                    })
                    )
                );
        }

        private static IEnumerable<WoWUnit> ChainHealPlayers
        {
            get
            {
                // TODO: Decide if we want to do this differently to ensure we take into account the T12 4pc bonus. (Not removing RT when using CH)
                return HealerManager.Instance.TargetList
                    .Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && u.PredictedHealthPercent() < ShamanSettings.RestoHealSettings.ChainHeal)
                    .Select(u => (WoWUnit)u);
            }
        }

        private static IEnumerable<WoWUnit> ChainHealRiptidePlayers
        {
            get
            {
                // refresh each call to account for changed in haste
                ChainHealCastTime = (int)Spell.GetSpellCastTime("Chain Heal").TotalMilliseconds;

                // TODO: Decide if we want to do this differently to ensure we take into account the T12 4pc bonus. (Not removing RT when using CH)
                return HealerManager.Instance.TargetList
                    .Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && u.PredictedHealthPercent() < ShamanSettings.RestoHealSettings.ChainHeal && u.GetAuraTimeLeft("Riptide").TotalMilliseconds > ChainHealCastTime)
                    .Select(u => (WoWUnit)u);
            }
        }

        #region Tidal Waves Bookkeeping

        private static int _tidalWaveStacksAudit = 0;

        private static void TidalWaveRefresh()
        {
            _tidalWaveStacksAudit = 2;
        }

        private static void TidalWaveConsume()
        {
            if (_tidalWaveStacksAudit > 0)
                _tidalWaveStacksAudit--;
        }

        private static int TidalWaveAuditCount()
        {
            return _tidalWaveStacksAudit;
        }

        private static bool IsTidalWavesNeeded
        {
            get
            {
                const int HW = 331;
                const int GHW = 77472;
                const int HS = 8004;

                if (Me.Level < 50 || TalentManager.CurrentSpec != WoWSpec.ShamanRestoration)
                    return false;

                // WoWAura tw = Me.GetAuraByName("Tidal Waves");
                uint stacks = Me.GetAuraStacks("Tidal Waves");

                // 2 stacks means we don't have an issue
                if (stacks >= 2)
                {
                    Logger.WriteDebug("Tidal Waves={0}", stacks);
                    return false;
                }

                // 1 stack? special case and a spell that will consume it is in progress or our audit count shows its gone
                int castId = Me.CastingSpellId;
                string castname = Me.CastingSpell == null ? "(none)" : Me.CastingSpell.Name;
                if (stacks == 1 && TidalWaveAuditCount() > 0 && castId != HW && castId != GHW && castId != HS)
                {
                    Logger.WriteDebug("Tidal Waves={0}", stacks);
                    return false;
                }

                Logger.WriteDebug("Tidal Waves={0} and Audit={1} while casting={2}, gcd={3}", stacks, TidalWaveAuditCount(), castId, Spell.IsGlobalCooldown().ToYN());
                return true;
            }
        }

        #endregion

        private static WoWUnit GetBestRiptideTankTarget()
        {
            WoWUnit ripTarget = Group.Tanks.Where(u => u.IsAlive && u.Combat && u.DistanceSqr < 40 * 40 && u.GetAuraTimeLeft("Riptide").TotalSeconds < 6 && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();
            if (ripTarget != null)
                Logger.WriteDebug("GetBestRiptideTarget: found tank {0}, hasmyaura={1} with {2} ms left", ripTarget.SafeName(), ripTarget.HasMyAura("Riptide"), (int)ripTarget.GetAuraTimeLeft("Riptide").TotalMilliseconds);
            return ripTarget;
        }

        private static WoWUnit GetBestRiptideTarget()
        {
            // Chain heal no longer benefits from Riptide.
            // WoWUnit ripTarget = Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, ChainHealHopDistance);
            WoWUnit ripTarget = Group.Dps.Where(u => u.IsAlive && u.Combat && u.DistanceSqr < 40 * 40 && u.GetAuraTimeLeft("Riptide").TotalSeconds < 3 && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();
            if (ripTarget != null)
                Logger.WriteDebug("GetBestRiptideTarget: found optimal target {0}, hasmyaura={1} with {2} ms left", ripTarget.SafeName(), ripTarget.HasMyAura("Riptide"), (int)ripTarget.GetAuraTimeLeft("Riptide").TotalMilliseconds);

            return ripTarget;
        }

        public static WoWUnit GetBestChainHealTarget()
        {
            if (!Me.IsInGroup())
                return null;

            if (!Spell.CanCastHack("Chain Heal", Me, skipWowCheck: true))
            {
                Logger.WriteDebug("GetBestChainHealTarget: CanCastHack says NO to Chain Heal");
                return null;
            }

            WoWUnit target = Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, ChainHealHopDistance);
            int count = 0;
            if (target != null)
            {
                count = Clusters.GetClusterCount(target, ChainHealPlayers, ClusterType.Chained, ChainHealHopDistance);
                if (count < ShamanSettings.RestoHealSettings.MinChainHealCount)
                    target = null;
            }

            if (target != null)
                Logger.WriteDebug("Chain Heal Target:  found {0} with {1} nearby under {2}%", target.SafeName(), count, ShamanSettings.RestoHealSettings.ChainHeal);

            return target;
        }

        public static WoWUnit GetBestHealingRainTarget()
        {
#if ORIGINAL
            return Clusters.GetBestUnitForCluster(Unit.NearbyFriendlyPlayers.Cast<WoWUnit>(), ClusterType.Radius, 10f);
#else
            if (!Me.IsInGroup() || !Me.Combat)
                return null;

            if (!Spell.CanCastHack("Healing Rain", Me, skipWowCheck: true))
            {
                // Logger.WriteDebug("GetBestHealingRainTarget: CanCastHack says NO to Healing Rain");
                return null;
            }

            // note: expensive, but worth it to optimize placement of Healing Rain by
            // finding location with most heals, but if tied one with most living targets also
            // build temp list of targets that could use heal and are in range + radius
            List<WoWUnit> coveredTargets = HealerManager.Instance.TargetList
                .Where(u => u.IsAlive && u.DistanceSqr < 50 * 50)
                .ToList();
            List<WoWUnit> coveredRainTargets = coveredTargets
                .Where(u => u.HealthPercent < ShamanSettings.RestoHealSettings.HealingRain)
                .ToList();

            // search all targets to find best one in best location to use as anchor for cast on ground
            var t = coveredTargets
                .Where(u => u.DistanceSqr < 40 * 40)
                .Select(p => new
                {
                    Player = p,
                    Count = coveredRainTargets
                        .Count(pp => pp.Location.DistanceSquared(p.Location) < 12 * 12),
                    Covered = coveredTargets
                        .Count(pp => pp.Location.DistanceSquared(p.Location) < 12 * 12)
                    })
                .OrderByDescending(v => v.Count)
                .ThenByDescending(v => v.Covered)
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (t != null && t.Count >= ShamanSettings.RestoHealSettings.MinHealingRainCount)
            {
                Logger.WriteDebug("Healing Rain Target:  found {0} with {1} nearby under {2}%", t.Player.SafeName(), t.Count, ShamanSettings.RestoHealSettings.HealingRain);
                return t.Player;
            }

            return null;
#endif
        }

        public static WoWUnit GetBestGiftoftheQueenTarget()
        {
            if (!Me.IsInGroup() || !Me.Combat)
                return null;

            if (!Spell.CanCastHack("Gift of the Queen", Me, skipWowCheck: true))
            {
                // Logger.WriteDebug("GetBestGiftoftheQueenTarget: CanCastHack says NO to Gift of the Queen");
                return null;
            }

            // note: expensive, but worth it to optimize placement of Gift of theQueen by
            // finding location with most heals, but if tied one with most living targets also
            // build temp list of targets that could use heal and are in range + radius
            List<WoWUnit> coveredTargets = HealerManager.Instance.TargetList
                .Where(u => u.IsAlive && u.DistanceSqr < 50 * 50)
                .ToList();
            List<WoWUnit> coveredRainTargets = coveredTargets
                .Where(u => u.HealthPercent < ShamanSettings.RestoHealSettings.GiftoftheQueen)
                .ToList();

            // search all targets to find best one in best location to use as anchor for cast on ground
            var t = coveredTargets
                .Where(u => u.DistanceSqr < 40 * 40)
                .Select(p => new
                {
                    Player = p,
                    Count = coveredRainTargets
                        .Count(pp => pp.Location.DistanceSquared(p.Location) < 13 * 13),
                    Covered = coveredTargets
                        .Count(pp => pp.Location.DistanceSquared(p.Location) < 13 * 13)
                    })
                .OrderByDescending(v => v.Count)
                .ThenByDescending(v => v.Covered)
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (t != null && t.Count >= ShamanSettings.RestoHealSettings.MinGiftoftheQueenCount)
            {
                Logger.WriteDebug("Gift of the Queen Target:  found {0} with {1} nearby under {2}%", t.Player.SafeName(), t.Count, ShamanSettings.RestoHealSettings.GiftoftheQueen);
                return t.Player;
            }

            return null;
        }

        private static int NumTier12Pieces
        {
            get
            {
                int count = StyxWoW.Me.Inventory.Equipped.Hands.ItemInfo.ItemSetId == RESTO_T12_ITEM_SET_ID ? 1 : 0;
                count += StyxWoW.Me.Inventory.Equipped.Legs.ItemInfo.ItemSetId == RESTO_T12_ITEM_SET_ID ? 1 : 0;
                count += StyxWoW.Me.Inventory.Equipped.Chest.ItemInfo.ItemSetId == RESTO_T12_ITEM_SET_ID ? 1 : 0;
                count += StyxWoW.Me.Inventory.Equipped.Shoulder.ItemInfo.ItemSetId == RESTO_T12_ITEM_SET_ID ? 1 : 0;
                count += StyxWoW.Me.Inventory.Equipped.Head.ItemInfo.ItemSetId == RESTO_T12_ITEM_SET_ID ? 1 : 0;
                return count;
            }
        }

        #region Diagnostics

        private static Composite CreateRestoDiagnosticOutputBehavior(UnitSelectionDelegate onHealUnit)
        {
            return new ThrottlePasses(1, 1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        WoWUnit target = Me.CurrentTarget;
                        uint actvstks = 0;

                        WoWAura aura = Me.GetAllAuras()
                            .Where(a => a.Name == "Tidal Waves" && a.TimeLeft != TimeSpan.Zero)
                            .FirstOrDefault();

                        if (aura != null)
                        {
                            actvstks = aura.StackCount;
                            if (actvstks == 0)
                                actvstks = 1;
                        }

                        string line = string.Format("[{0}] h={1:F1}%/m={2:F1}%,combat={3},move={4},twaves={5},audtwaves={6}",
                            Dynamics.CompositeBuilder.CurrentBehaviorType.ToString(),
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.Combat.ToYN(),
                            Me.IsMoving.ToYN(),
                            actvstks,
                            _tidalWaveStacksAudit
                            );

                        WoWUnit healTarg = onHealUnit(ret);
                        if (Me.IsInGroup() || (Me.FocusedUnitGuid.IsValid && healTarg == Me.FocusedUnit))
                        {
                            if (healTarg == null)
                                line += ", heal=(null)";
                            else if (!healTarg.IsValid)
                                line += ", heal=(invalid)";
                            else
                                line += string.Format(",heal={0} hh={1:F1}% @ {2:F1} yds,hcombat={3},hph={4:F1}%,htloss={5},riptide={6}",
                                    healTarg.SafeName(),
                                    healTarg.HealthPercent,
                                    healTarg.Distance,
                                    healTarg.Combat.ToYN(),
                                    healTarg.PredictedHealthPercent(),
                                    healTarg.InLineOfSpellSight,
                                    (long)healTarg.GetAuraTimeLeft("Riptide").TotalMilliseconds
                                    );

                            if (SingularSettings.Instance.StayNearTank)
                            {
                                WoWUnit tank = HealerManager.TankToStayNear;
                                if (tank == null)
                                    line += ",tank=(null)";
                                else if (!tank.IsAlive)
                                    line += ",tank=(dead)";
                                else
                                {
                                    float hh = (float)tank.HealthPercent;
                                    float hph = tank.PredictedHealthPercent();
                                    line += string.Format(",tank={0} {1:F1}% @ {2:F1} yds,tph={3:F1}%,tcombat={4},tmove={5},tloss={6}",
                                        tank.SafeName(),
                                        hh,
                                        tank.SpellDistance(),
                                        hph,
                                        tank.Combat.ToYN(),
                                        tank.IsMoving.ToYN(),
                                        tank.InLineOfSpellSight.ToYN()
                                        );
                                }
                            }
                        }

                        if (target == null)
                            line += ", target=(null)";
                        else if (!target.IsValid)
                            line += ", target=(invalid)";
                        else
                            line += string.Format(",target={0} th={1:F1}%,{2:F1} yds,face={3},tloss={4},fs={5}",
                                target.SafeName(),
                                target.HealthPercent,
                                target.Distance,
                                Me.IsSafelyFacing(target),
                                target.InLineOfSpellSight,
                                (long)target.GetAuraTimeLeft("Flame Shock").TotalMilliseconds
                                );

                        Logger.WriteDebug(Color.Yellow, line);
                        return RunStatus.Failure;
                    }))
                );
        }

        #endregion

    }
}
