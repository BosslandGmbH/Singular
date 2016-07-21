using System;
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
        private static float ChainHealHopDistance { get; set; }

        //private static WoWGuid guidLastHealTarget;
        private static WoWGuid guidLastEarthShield;

        #region INIT

        [Behavior(BehaviorType.Initialize, WoWClass.Shaman, WoWSpec.ShamanRestoration)]
        public static Composite CreateRestoShamanInitialize()
        {
            ChainHealCastTime = (int)Spell.GetSpellCastTime("Chain Heal").TotalMilliseconds;
            ChainHealHopDistance = TalentManager.Glyphs.Contains("Chaining") ? 25f : 12.5f;

            return null;
        }

        #endregion

        #region REST

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanRestoration)]
        public static Composite CreateRestoShamanRest()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !Helpers.Rest.IsEatingOrDrinking,
                    CreateRestoShamanCombatBuffs()    // call here so any Earth Shield done before we drink
                    ),
                Singular.Helpers.Rest.CreateDefaultRestBehaviour("Healing Surge", "Ancestral Spirit"),
                CreateRestoShamanHealingOnlyBehavior(selfOnly: false)
                );
        }

        #endregion

        #region BUFFS

        /// <summary>
        /// written as a single method to simplify reference by multi-context Rest behavior
        /// </summary>
        /// <returns></returns>
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Shaman, WoWSpec.ShamanRestoration)]
        public static Composite CreateRestoShamanCombatBuffs()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
            {
                return new PrioritySelector(
                    Spell.BuffSelf("Earth Shield", ret => Me.ManaPercent >= ShamanSettings.TwistDamageShield),
                    Spell.BuffSelf("Water Shield", ret => Me.ManaPercent <= ShamanSettings.TwistWaterShield)
                    );
            }

            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
            {
                return new PrioritySelector(
                    new Throttle(10,
                        new PrioritySelector(
                            Spell.Buff("Earth Shield", on => GetBestEarthShieldTargetInstance()),
                            Spell.BuffSelf("Water Shield", ret => !Me.HasAura("Earth Shield"))
                            )
                        )
                    );
            }

            // Normal
            return new Decorator(
                req => !((SingularRoutine.IsBgBotActive || SingularRoutine.IsDungeonBuddyActive) && !Me.Combat && Me.IsInsideSanctuary),
                new PrioritySelector(
                    Spell.BuffSelf("Earth Shield", ret => Me.ManaPercent >= ShamanSettings.TwistDamageShield),
                    Spell.BuffSelf("Water Shield", ret => Me.ManaPercent <= ShamanSettings.TwistWaterShield)
                    )
                );
        }

        /// <summary>
        /// selects best Earth Shield target
        /// </summary>
        /// <returns></returns>
        private static WoWUnit GetBestEarthShieldTargetInstance()
        {
            WoWUnit target = null;

            if (HealerManager.Instance.TargetList.Any(m => m.HasMyAura("Earth Shield")))
                return null;

            if (IsValidEarthShieldTarget(RaFHelper.Leader))
                target = RaFHelper.Leader;
            else
            {
                target = Group.Tanks.FirstOrDefault(t => IsValidEarthShieldTarget(t));
                if (Me.Combat && target == null)
                {
                    target = HealerManager.Instance.TargetList.Where(u => u.Combat && IsValidEarthShieldTarget(u))
                        .OrderByDescending(u => u.MaxHealth)
                        .FirstOrDefault();
                }
            }

            guidLastEarthShield = target != null ? target.Guid : WoWGuid.Empty;
            return target;
        }

        /// <summary>
        /// selects best Earth Shield target
        /// </summary>
        /// <returns></returns>
        private static WoWUnit GetBestEarthShieldTargetBattlegrounds()
        {
#if SUPPORT_MOST_IN_NEED_EARTHSHIELD_LOGIC
            var target = Unit.NearbyGroupMembers
                .Where(u => IsValidEarthShieldTarget(u.ToUnit()))
                .Select(u => new { Unit = u, Health = u.HealthPercent, EnemyCount = Unit.NearbyUnfriendlyUnits.Count(e => e.CurrentTargetGuid == u.Guid)})
                .OrderByDescending(v => v.EnemyCount)
                .ThenBy(v => v.Health )
                .FirstOrDefault();

            if (target == null)
            {
                guidLastEarthShield = Me.Guid;
            }
            else if (guidLastEarthShield != target.Unit.Guid)
            {
                guidLastEarthShield = target.Unit.Guid;
                Logger.WriteDebug("Best Earth Shield Target appears to be: {0} @ {1:F0}% with {2} attackers", target.Unit.SafeName(), target.Health, target.EnemyCount);
            }

            return target == null ? Me : target.Unit;
#else
            return Me;
#endif
        }

        private static bool IsValidEarthShieldTarget(WoWUnit unit)
        {
            if (unit == null || !unit.IsValid || !unit.IsAlive || !Unit.GroupMembers.Any(g => g.Guid == unit.Guid) || unit.Distance > 99)
                return false;

            return unit.HasMyAura("Earth Shield") || !unit.HasAnyAura("Earth Shield", "Water Shield", "Lightning Shield");
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

            /*
                        if (SpellManager.HasSpell("Earthliving Weapon"))
                        {
                            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.AncestralSwiftness),
                                "Unleash Elements",
                                "Unleash Elements",
                                Spell.Buff("Unleash Elements",
                                    ret => (WoWUnit)ret,
                                    ret => (Me.IsMoving || ((WoWUnit)ret).PredictedHealthPercent() < ShamanSettings.Heal.AncestralSwiftness)
                                        && Common.IsImbuedForHealing(Me.Inventory.Equipped.MainHand)
                                        ));
                        }
            */
            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                behavs.AddBehavior(9999, "Earth Shield", "Earth Shield", Spell.Buff("Earth Shield", on => GetBestEarthShieldTargetInstance()));

            int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
                behavs.AddBehavior(dispelPriority, "Purify Spirit", null, Dispelling.CreateDispelBehavior());

            #region Save the Group

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.SpiritLinkTotem) + 600, "Spirit Link Totem", "Spirit Link Totem",
                new Decorator(
                    ret => Me.Combat && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
                    Spell.Cast(
                        "Spirit Link Totem", ret => (WoWUnit)ret,
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
                        && (Common.talentTotemicPersistance || !Totems.Exist(WoWTotem.Cloudburst) || (Totems.GetTotem(WoWTotem.Cloudburst).Expires - DateTime.UtcNow).TotalMilliseconds < 1500),
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

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.RestoHealSettings.ChainHeal) + 100, "Chain Heal", "Chain Heal",
                new Decorator(
                    ret => Me.Combat && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
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

                                /*
                                                            Spell.Cast("Earth Shield",
                                                                ret => (WoWUnit)ret,
                                                                ret => ret is WoWUnit && Group.Tanks.Contains((WoWUnit)ret) && Group.Tanks.All(t => !t.HasMyAura("Earth Shield"))),
                                */

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
            WoWUnit ripTarget = null;
            ripTarget = Group.Tanks.Where(u => u.IsAlive && u.Combat && u.DistanceSqr < 40 * 40 && !u.HasMyAura("Riptide") && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();
            if (ripTarget != null)
                Logger.WriteDebug("GetBestRiptideTarget: found tank {0}, hasmyaura={1} with {2} ms left", ripTarget.SafeName(), ripTarget.HasMyAura("Riptide"), (int)ripTarget.GetAuraTimeLeft("Riptide").TotalMilliseconds);
            return ripTarget;
        }

        private static WoWUnit GetBestRiptideTarget()
        {
            WoWUnit ripTarget = Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, ChainHealHopDistance);
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

            // search players with Riptide first
            var targetInfo = ChainHealRiptidePlayers
                .Select(p => new { Unit = p, Count = Clusters.GetClusterCount(p, ChainHealPlayers, ClusterType.Chained, ChainHealHopDistance) })
                .OrderByDescending(v => v.Count)
                .ThenByDescending(v => Group.Tanks.Any(t => t.Guid == v.Unit.Guid))
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            WoWUnit target = targetInfo == null ? null : targetInfo.Unit;
            int count = targetInfo == null ? 0 : targetInfo.Count;

            // too few hops? then search any group member
            if (count < ShamanSettings.RestoHealSettings.MinChainHealCount)
            {
                target = Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, ChainHealHopDistance);
                if (target != null)
                {
                    count = Clusters.GetClusterCount(target, ChainHealPlayers, ClusterType.Chained, ChainHealHopDistance);
                    if (count < ShamanSettings.RestoHealSettings.MinChainHealCount)
                        target = null;
                }
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
                        .Where(pp => pp.Location.DistanceSqr(p.Location) < 12 * 12)
                        .Count(),
                    Covered = coveredTargets
                        .Where(pp => pp.Location.DistanceSqr(p.Location) < 12 * 12)
                        .Count()
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

                        string shield;

                        if (Me.HasAura("Earth Shield"))
                            shield = string.Format("EARTH[{0}]", Me.GetAuraStacks("Earth Shield"));
                        else if (Me.HasAura("Water Shield"))
                            shield = string.Format("WATER[{0}]", (long)Me.GetAuraTimeLeft("Water Shield").TotalMinutes);
                        else if (Me.HasAura("Lightning Shield"))
                            shield = string.Format("LHTNG[{0}]", Me.GetAuraStacks("Lightning Shield"));
                        else
                            shield = "-none-";

                        string line = string.Format("[{0}] h={1:F1}%/m={2:F1}%,combat={3},move={4},twaves={5},audtwaves={6},shield={7}",
                            Dynamics.CompositeBuilder.CurrentBehaviorType.ToString(),
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.Combat.ToYN(),
                            Me.IsMoving.ToYN(),
                            actvstks,
                            _tidalWaveStacksAudit,
                            shield
                            );

                        WoWUnit healTarg = onHealUnit(ret);
                        if (Me.IsInGroup() || (Me.FocusedUnitGuid.IsValid && healTarg == Me.FocusedUnit))
                        {
                            if (healTarg == null)
                                line += ", heal=(null)";
                            else if (!healTarg.IsValid)
                                line += ", heal=(invalid)";
                            else
                                line += string.Format(",heal={0} hh={1:F1}% @ {2:F1} yds,hcombat={3},hph={4:F1}%,htloss={5},eshield={6},riptide={7}",
                                    healTarg.SafeName(),
                                    healTarg.HealthPercent,
                                    healTarg.Distance,
                                    healTarg.Combat.ToYN(),
                                    healTarg.PredictedHealthPercent(),
                                    healTarg.InLineOfSpellSight,
                                    (healTarg.GetAuraStacks("Earth Shield") > 0).ToYN(),
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
