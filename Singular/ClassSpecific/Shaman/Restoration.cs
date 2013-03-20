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

        #region BUFFS

        [Behavior(BehaviorType.CombatBuffs | BehaviorType.PreCombatBuffs, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Normal )]
        public static Composite CreateRestoShamanHealingBuffsNormal()
        {
            return new PrioritySelector(

                Spell.BuffSelf("Water Shield", ret => Me.ManaPercent < 20),
                Spell.BuffSelf("Earth Shield", ret => Me.ManaPercent > 35),

                Common.CreateShamanImbueMainHandBehavior(Imbue.Earthliving, Imbue.Flametongue)
                );
        }

        [Behavior(BehaviorType.CombatBuffs | BehaviorType.PreCombatBuffs, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Battlegrounds)]
        public static Composite CreateRestoHealingBuffsBattlegrounds()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Earth Shield", ret => Me.ManaPercent > 25),
                Spell.BuffSelf("Water Shield", ret => Me.ManaPercent < 15),

                Common.CreateShamanImbueMainHandBehavior(Imbue.Earthliving, Imbue.Flametongue)
                );
        }

        private static ulong guidLastEarthShield = 0;

        [Behavior(BehaviorType.CombatBuffs | BehaviorType.PreCombatBuffs, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Instances )]
        public static Composite CreateRestoHealingBuffsInstance()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    new Throttle( 2,
                        new PrioritySelector(
                            Spell.Buff("Earth Shield", on => GetBestEarthShieldTargetInstance()),
                            Spell.BuffSelf("Water Shield", ret => !Me.HasAura("Earth Shield"))
                            )
                        ),

                    Common.CreateShamanImbueMainHandBehavior(Imbue.Earthliving, Imbue.Flametongue)
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

            if (IsValidEarthShieldTarget(RaFHelper.Leader))
                target = RaFHelper.Leader;
            else
            {
                target = Group.Tanks.FirstOrDefault(t => IsValidEarthShieldTarget(t));
                if (Me.Combat && target == null && !Unit.NearbyGroupMembers.Any(m => m.HasMyAura("Earth Shield")))
                {
                    target = HealerManager.Instance.HealList.Where(u => IsValidEarthShieldTarget(u.ToUnit())).OrderByDescending(u => HealerManager.Instance.HealList.Count(e => e.CurrentTargetGuid == u.Guid)).FirstOrDefault();
                }
            }

            guidLastEarthShield = target != null ? target.Guid : 0;
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
            if ( unit == null || !unit.IsValid || !unit.IsAlive || !Unit.GroupMembers.Any( g => g.Guid == unit.Guid ) || unit.Distance > 99 )
                return false;

            return unit.HasMyAura("Earth Shield") || !unit.HasAnyAura( "Earth Shield", "Water Shield", "Lightning Shield");
        }

        #endregion

        #region NORMAL 

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Normal)]
        public static Composite CreateRestoShamanRest()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Decorator(
                            ret => !StyxWoW.Me.HasAura("Drink") && !StyxWoW.Me.HasAura("Food"),
                            CreateRestoShamanHealingOnlyBehavior(true, false)
                            ),
                        Singular.Helpers.Rest.CreateDefaultRestBehaviour(null, "Ancestral Spirit"),
                        CreateRestoShamanHealingOnlyBehavior(false, true)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Shaman, WoWSpec.ShamanRestoration)]
        public static Composite CreateRestoShamanPullBehavior()
        {
            return
                new PrioritySelector(
                    Helpers.Common.CreateDismount("Pulling"),
                    Spell.WaitForCastOrChannel(),
                    CreateRestoShamanHealingOnlyBehavior(false, true),
                    new Decorator(
                        ret => !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid),

                        new PrioritySelector(
                            Safers.EnsureTarget(),
                            Movement.CreateMoveToLosBehavior(),
                            Movement.CreateFaceTargetBehavior(),
                            Helpers.Common.CreateDismount("Pulling"),

                            new Decorator(
                                ret => Me.GotTarget && Me.CurrentTarget.Distance < 35,
                                Movement.CreateEnsureMovementStoppedBehavior()
                                ),

                            Spell.WaitForCastOrChannel(),

                            new Decorator(
                                ret => !Spell.IsGlobalCooldown(),
                                new PrioritySelector(

                                    Totems.CreateTotemsBehavior(),

                            // grinding or questing, if target meets these cast Flame Shock if possible
                            // 1. mob is less than 12 yds, so no benefit from delay in Lightning Bolt missile arrival
                            // 2. area has another player competing for mobs (we want to tag the mob quickly)
                                    new Decorator(
                                        ret => StyxWoW.Me.CurrentTarget.Distance < 12
                                            || ObjectManager.GetObjectsOfType<WoWPlayer>(false, false).Any(p => p.Location.DistanceSqr(StyxWoW.Me.CurrentTarget.Location) <= 40 * 40),
                                        new PrioritySelector(
                                            Spell.Buff("Flame Shock", true),
                                            Spell.Cast("Earth Shock", ret => !SpellManager.HasSpell("Flame Shock"))
                                            )
                                        ),

                                    Spell.Cast("Lightning Bolt", mov => false, on => Me.CurrentTarget, ret => !StyxWoW.Me.IsMoving || Spell.HaveAllowMovingWhileCastingAura() || TalentManager.HasGlyph("Unleashed Lightning")),
                                    Spell.Cast("Flame Shock"),
                                    Spell.Cast("Unleash Weapon", ret => Common.IsImbuedForDPS(StyxWoW.Me.Inventory.Equipped.MainHand))
                                    )
                                ),

                            Movement.CreateMoveToTargetBehavior(true, 35)
                            )
                        )
                    );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Normal)]
        public static Composite CreateRestoShamanHealBehaviorNormal()
        {
            return
                new PrioritySelector(
                    Spell.WaitForCastOrChannel(),
                    CreateRestoShamanHealingOnlyBehavior(selfOnly: false, moveInRange: true));
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Normal)]
        public static Composite CreateRestoShamanCombatBehaviorNormal()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                CreateRestoShamanHealingOnlyBehavior(selfOnly: false, moveInRange: true),

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                new Decorator(
                    ret => Me.GotTarget && Me.CurrentTarget.Distance < 35,
                    Movement.CreateEnsureMovementStoppedBehavior()
                    ),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),

                        Totems.CreateTotemsBehavior(),

                        Spell.Cast("Elemental Blast"),
                        Spell.Buff("Flame Shock", true),

                        Spell.Cast("Lava Burst"),
                        Spell.Cast("Earth Shock",
                            ret => StyxWoW.Me.HasAura("Lightning Shield", 5) &&
                                    StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 3),

                        Spell.Cast("Chain Lightning", ret => Spell.UseAOE && Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && !Unit.UnfriendlyUnitsNearTarget(10f).Any(u => u.IsCrowdControlled())),
                        Spell.Cast("Lightning Bolt")
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 35)
                // Movement.CreateMoveToRangeAndStopBehavior(ret => Me.CurrentTarget, ret => NormalPullDistance)
                );
        }

        #endregion

        #region INSTANCES

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Instances)]
        public static Composite CreateRestoShamanRestInstances()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateRestoHealingBuffsInstance(),
                        new Decorator(
                            ret => !StyxWoW.Me.HasAura("Drink") && !StyxWoW.Me.HasAura("Food"),
                            CreateRestoShamanHealingOnlyBehavior(true, false)
                            ),
                        Singular.Helpers.Rest.CreateDefaultRestBehaviour(null, "Ancestral Spirit"),
                        CreateRestoShamanHealingOnlyBehavior(false, true)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Instances)]
        public static Composite CreateRestoShamanHealBehaviorInstance()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                CreateRestoShamanHealingOnlyBehavior( selfOnly: false, moveInRange: true)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Instances )]
        public static Composite CreateRestoShamanCombatBehaviorInstances()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),

                CreateRestoShamanHealingOnlyBehavior(selfOnly: false, moveInRange: true),

                // no healing needed, then move within heal range of tank
                Movement.CreateMoveToRangeAndStopBehavior(ret => BestTankToMoveNear, ret => 38f),

                new Decorator(
                    ret => !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid) || TalentManager.HasGlyph("Telluric Currents"),
                    new PrioritySelector(
                        Safers.EnsureTarget(),
                        Movement.CreateMoveToLosBehavior(),
                        Movement.CreateFaceTargetBehavior(),
                        Spell.WaitForCast(),

                        new Decorator(
                            ret => !Spell.GcdActive,
                            new PrioritySelector(
                                Helpers.Common.CreateInterruptBehavior(),
                                Spell.Cast("Lightning Bolt"),

                                Totems.CreateTotemsBehavior(),

                                Spell.Cast("Elemental Blast"),
                                Spell.Buff("Flame Shock", true),

                                Spell.Cast("Lava Burst"),
                                Spell.Cast("Earth Shock",
                                    ret => StyxWoW.Me.HasAura("Lightning Shield", 5) &&
                                            StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 3),

                                Spell.Cast("Chain Lightning", ret => Spell.UseAOE && Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && !Unit.UnfriendlyUnitsNearTarget(10f).Any(u => u.IsCrowdControlled())),
                                Spell.Cast("Lightning Bolt")
                                )
                            )
                        )
                    )
                );

        }

        private static WoWUnit BestTankToMoveNear
        {
            get
            {
                if (!SingularSettings.Instance.StayNearTank)
                    return null;

                if (RaFHelper.Leader != null && RaFHelper.Leader.IsValid && RaFHelper.Leader.IsAlive && RaFHelper.Leader.Distance < 100)
                    return RaFHelper.Leader;

                return Group.Tanks.Where(t => t.IsAlive && t.Distance < SingularSettings.Instance.MaxHealTargetRange).OrderBy(t => t.Distance).FirstOrDefault();
            }
        }

        #endregion

        #region BATTLEGROUNDS

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Battlegrounds )]
        public static Composite CreateRestoShamanRestPvp()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Decorator(
                            ret => !StyxWoW.Me.HasAura("Drink") && !StyxWoW.Me.HasAura("Food"),
                            CreateRestoShamanHealingOnlyBehavior(true, false)
                            ),
                        Singular.Helpers.Rest.CreateDefaultRestBehaviour(null, "Ancestral Spirit"),
                        CreateRestoShamanHealingOnlyBehavior(false, true)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Battlegrounds)]
        public static Composite CreateRestoShamanCombatBehaviorPvp()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                CreateRestoShamanHealingOnlyBehavior(false, true)
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Battlegrounds)]
        public static Composite CreateRestoShamanHealBehaviorPvp()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                CreateRestoShamanHealingOnlyBehavior(false, true)
                );
        }

        #endregion


        // private static ulong guidLastHealTarget = 0;

        public static Composite CreateRestoShamanHealingOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();
            int cancelHeal = (int)Math.Max( SingularSettings.Instance.IgnoreHealTargetsAboveHealth, Math.Max(ShamanSettings.Heal.HealingWave, Math.Max(ShamanSettings.Heal.GreaterHealingWave, ShamanSettings.Heal.HealingSurge)));

            Logger.WriteFile( "Shaman Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);

/*
            if (SpellManager.HasSpell("Earthliving Weapon"))
            {
                behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.AncestralSwiftness),
                    "Unleash Elements",
                    "Unleash Elements",
                    Spell.Buff("Unleash Elements",
                        ret => (WoWUnit)ret,
                        ret => (Me.IsMoving || ((WoWUnit)ret).GetPredictedHealthPercent() < ShamanSettings.Heal.AncestralSwiftness)
                            && Common.IsImbuedForHealing(Me.Inventory.Equipped.MainHand)
                            ));
            }
*/
            if ( SingularRoutine.CurrentWoWContext == WoWContext.Instances )
                behavs.AddBehavior( 9999, "Earth Shield", "Earth Shield", Spell.Buff("Earth Shield", on => GetBestEarthShieldTargetInstance()));

            int dispelPriority = (SingularSettings.Instance.DispelDebuffs == DispelStyle.HighPriority) ? 999 : -999;
            if ( SingularSettings.Instance.DispelDebuffs != DispelStyle.None)
                behavs.AddBehavior( dispelPriority, "Purify Spirit", null, Dispelling.CreateDispelBehavior());

            #region Save the Group

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.SpiritLinkTotem) + 600, "Spirit Link Totem", "Spirit Link Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Spirit Link Totem", ret => (WoWUnit)ret,
                        ret => HealerManager.Instance.HealList.Count(
                            p => p.GetPredictedHealthPercent() < ShamanSettings.Heal.SpiritLinkTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.SpiritLink)) >= ShamanSettings.Heal.MinSpiritLinkCount
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.AncestralSwiftness) + 500,
                String.Format("Oh Shoot Heal @ {0}%", ShamanSettings.Heal.AncestralSwiftness),
                "Ancestral Swiftness",
                new Decorator(
                    ret => ((WoWUnit)ret).GetPredictedHealthPercent() < ShamanSettings.Heal.AncestralSwiftness,
                    new Sequence(
                        Spell.BuffSelf("Ancestral Swiftness"),
                        new PrioritySelector(
                            Spell.Cast("Greater Healing Wave", on => (WoWUnit)on),
                            Spell.Cast("Healing Surge", on => (WoWUnit)on, ret => !SpellManager.HasSpell("Greater Healing Wave"))
                            )
                        )
                    )
                );

            #endregion

            #region AoE Heals

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.HealingTideTotem) + 400, "Healing Tide Totem", "Healing Tide Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Healing Tide Totem",
                        ret => Me.Combat && HealerManager.Instance.HealList.Count(p => p.GetPredictedHealthPercent() < ShamanSettings.Heal.HealingTideTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.HealingStreamTotem) + 300, "Healing Stream Totem", "Healing Stream Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Healing Stream Totem",
                        ret => Me.Combat 
                            && !Totems.Exist(WoWTotemType.Water)
                            && HealerManager.Instance.HealList.Any(p => p.GetPredictedHealthPercent() < ShamanSettings.Heal.HealingStreamTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide))
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.HealingRain) + 200, "Healing Rain", "Healing Rain",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        context => GetBestHealingRainTarget(),
                        new Decorator(
                            ret => ret != null,
                            new PrioritySelector(
                                new Sequence(
                                    new Action(r => Logger.WriteDebug("UE+HR - just before UE check")),
                                    BuffUnleashLife(on => HealerManager.Instance.HealList.FirstOrDefault()),
                                    new Action(r => Logger.WriteDebug("UE+HR - past UE")),
                                    Helpers.Common.CreateWaitForLagDuration(ret => Spell.IsGlobalCooldown()),
                                    new Action(r => Logger.WriteDebug("UE+HR - past GCD start")),
                                    new WaitContinue(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                                    new Action(r => Logger.WriteDebug("UE+HR - past GCD stop")),
                                    Spell.CastOnGround("Healing Rain", on => (WoWUnit)on, req => true, false)
                                    ),
                                new Action( ret => {
                                    if (ret != null)
                                    {
                                        if (!((WoWUnit)ret).IsValid)
                                            Logger.WriteDebug("UE+HR - FAILED - Healing Target object became invalid");
                                        else if (((WoWUnit)ret).Distance > 40)
                                            Logger.WriteDebug("UE+HR - FAILED - Healing Target moved out of range");
                                        else if (!SpellManager.CanCast("Healing Rain"))
                                            Logger.WriteDebug("UE+HR - FAILED - SpellManager.CanCast() said NO to Healing Target");
                                        else if (Styx.WoWInternals.World.GameWorld.IsInLineOfSpellSight(StyxWoW.Me.GetTraceLinePos(), ((WoWUnit) ret).Location))
                                            Logger.WriteDebug("UE+HR - FAILED - SpellManager.CanCast() unit location not in Line of Sight");
                                        else
                                            Logger.WriteDebug("UE+HR - Something FAILED with Unleash Life + Healing Rain cast sequence");
                                    }
                                    return RunStatus.Failure;
                                    })
                                )
                            )
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.ChainHeal) + 100, "Chain Heal", "Chain Heal",
                new PrioritySelector(
                    ctx => GetBestChainHealTarget(),
                    new Decorator(
                        ret => ret != null,
                        new PrioritySelector(
                            new Sequence(
                                Spell.Buff("Riptide", on => (WoWUnit)on),
                                new Action(r => Logger.WriteDebug("ChainHeal:  prepped target with Riptide first")),
                                new Wait(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown(), new ActionAlwaysFail())
                                ),
                            new Sequence(
                                Spell.Cast("Chain Heal", on => (WoWUnit)on),
                                new Action(r => TidalWaveRefresh())
                                )
                            )
                        )
                    )
                );

            #endregion

            #region Single Target Heals

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.GreaterHealingWave), "Greater Healing Wave", "Greater Healing Wave",
                new Decorator( ret => ((WoWUnit)ret).GetPredictedHealthPercent() < ShamanSettings.Heal.GreaterHealingWave,
                    new Sequence(
                        BuffUnleashLife(on => (WoWUnit) on),
                        new WaitContinue(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                        Spell.Cast("Greater Healing Wave",
                            mov => true, 
                            on => (WoWUnit)on, 
                            req => ((WoWUnit)req).GetPredictedHealthPercent() < ShamanSettings.Heal.GreaterHealingWave, 
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal),
                        new Action( r => TidalWaveConsume() )
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.HealingWave), "Healing Wave", "Healing Wave",
                new Sequence(
                        Spell.Cast("Healing Wave",
                            mov => true,
                            on => (WoWUnit)on,
                            req => ((WoWUnit)req).GetPredictedHealthPercent() < ShamanSettings.Heal.HealingWave,
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal),
                    new Action(r => TidalWaveConsume())
                    )
                );

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.HealingSurge), "Healing Surge", "Healing Surge", 
                new Sequence(
                        Spell.Cast("Healing Surge",
                            mov => true,
                            on => (WoWUnit)on,
                            req => ((WoWUnit)req).GetPredictedHealthPercent() < ShamanSettings.Heal.HealingSurge,
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal),
                    new Action(r => TidalWaveConsume())
                    )
                );

            #endregion

            #region Healing Cooldowns

            behavs.AddBehavior(HealthToPriority( ShamanSettings.Heal.Ascendance) + 100, "Ascendance", "Ascendance",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.BuffSelf(
                        "Ascendance",
                        ret => HealerManager.Instance.HealList.Count(p => p.GetPredictedHealthPercent() < ShamanSettings.Heal.Ascendance) >= ShamanSettings.Heal.MinAscendanceCount 
                        )
                    )
                );

            #endregion

            behavs.OrderBehaviors();
            behavs.ListBehaviors();


            return new PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,

                CreateRestoDiagnosticOutputBehavior(ret => (WoWUnit)ret),

                new Decorator(
                    ret => ret != null && (Me.Combat || ((WoWUnit)ret).Combat || ((WoWUnit)ret).GetPredictedHealthPercent() <= 99),

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
                                        if (unit != null && Spell.CanCastHack("Riptide", unit))
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
                                    ret => {
                                        int rollCount = HealerManager.Instance.HealList.Count(u => u.IsAlive && u.HasMyAura("Riptide"));
                                        Logger.WriteDebug("GetBestRiptideTarget:  currently {0} group members have my Riptide", rollCount);
                                        return rollCount < ShamanSettings.Heal.RollRiptideCount;
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
                                                Logger.WriteDebug(Color.White, "ROLLING RIPTIDE on: {0}", unit.SafeName());

                                            return unit;
                                        }),
                                        new Action(r => TidalWaveRefresh())
                                        )
                                    )



#if false                  
                            ,
                            new Sequence(
                                new Action(ret => Logger.WriteDebug(Color.LightGreen, "No Action - stunned:{0} silenced:{1}"
                                    , Me.Stunned || Me.IsStunned() 
                                    , Me.Silenced 
                                    )),
                                new Action(ret => { return RunStatus.Failure; })
                                )
                            ,

                            new Decorator(
                                ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid),
                                new PrioritySelector(
                                    Movement.CreateMoveToLosBehavior(),
                                    Movement.CreateFaceTargetBehavior(),
                                    Helpers.Common.CreateInterruptBehavior(),

                                    Spell.Cast("Earth Shock"),
                                    Spell.Cast("Lightning Bolt"),
                                    Movement.CreateMoveToTargetBehavior(true, 35f)
                                    )
                                )
#endif
)
                        ),

                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToRangeAndStopBehavior(ret => (WoWUnit)ret, ret => 38f))
                        )
                    )
                );
        }

        private static Composite CreateRestoShamanBuffTidalWaves()
        {
            return new Decorator(
                ret => IsTidalWavesNeeded,
                new Sequence(
                    Spell.Cast("Riptide", on =>{
                        WoWUnit unit = GetBestRiptideTarget();
                        if (unit != null)
                            Logger.WriteDebug(Color.White, "BUFFING TIDAL WAVES with Riptide on: {0}", unit.SafeName());
                        return unit;
                        }),
                    new Action(r => TidalWaveRefresh())
                    )
                );
        }

        private static Composite BuffUnleashLife( UnitSelectionDelegate onUnit)
        {
            return new PrioritySelector(
                Spell.Cast("Unleash Elements",
                    onUnit,
                    ret => Common.IsImbuedForHealing(Me.Inventory.Equipped.MainHand) && (Me.Combat || onUnit(ret).Combat)),
                new ActionAlwaysSucceed()
                );
        }

        private static float ChainHealHopRange
        {
            get
            {
                return TalentManager.Glyphs.Contains("Chaining") ? 25f : 12.5f;
            }
        }

        private static IEnumerable<WoWUnit> ChainHealPlayers
        {
            get
            {
                // TODO: Decide if we want to do this differently to ensure we take into account the T12 4pc bonus. (Not removing RT when using CH)
                return HealerManager.Instance.HealList
                    .Where(u => u.IsAlive && u.DistanceSqr < 40*40 && u.GetPredictedHealthPercent() < ShamanSettings.Heal.ChainHeal)
                    .Select(u => (WoWUnit)u);
            }
        }

        private static IEnumerable<WoWUnit> ChainHealRiptidePlayers
        {
            get
            {
                // TODO: Decide if we want to do this differently to ensure we take into account the T12 4pc bonus. (Not removing RT when using CH)
                return HealerManager.Instance.HealList
                    .Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && u.GetPredictedHealthPercent() < ShamanSettings.Heal.ChainHeal && u.HasMyAura("Riptide"))
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
            if ( _tidalWaveStacksAudit > 0)
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

                if ( Me.Level < 50 || Me.Specialization != WoWSpec.ShamanRestoration)
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

                Logger.WriteDebug("Tidal Waves={0} and Audit={1} while casting {2}", stacks, TidalWaveAuditCount(), castId);
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
            WoWUnit ripTarget = Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, ChainHealHopRange);
            if (ripTarget != null)
                Logger.WriteDebug("GetBestRiptideTarget: found optimal target {0}, hasmyaura={1} with {2} ms left", ripTarget.SafeName(), ripTarget.HasMyAura("Riptide"), (int)ripTarget.GetAuraTimeLeft("Riptide").TotalMilliseconds);

            return ripTarget;
        }

        private static WoWUnit GetBestChainHealTarget()
        {
            if (!Me.IsInGroup())
                return null;

            if (!Spell.CanCastHack("Chain Heal", Me))
            {
                Logger.WriteDebug("GetBestChainHealTarget: CanCastHack says NO to Chain Heal");
                return null;
            }

            // search players with Riptide first
            var targetInfo = ChainHealRiptidePlayers
                .Select( p => new { Unit = p, Count = Clusters.GetClusterCount(p, ChainHealPlayers, ClusterType.Chained, ChainHealHopRange) })
                .OrderByDescending( v => v.Count )
                .ThenByDescending( v => Group.Tanks.Any( t => t.Guid == v.Unit.Guid))
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            WoWUnit target = targetInfo == null ? null : targetInfo.Unit ;
            int count = targetInfo == null ? 0 : targetInfo.Count ;

            // too few hops? then search any group member
            if (count < ShamanSettings.Heal.MinChainHealCount)
            {
                target = Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, ChainHealHopRange);
                if (target != null)
                {
                    count = Clusters.GetClusterCount(target, ChainHealPlayers, ClusterType.Chained, ChainHealHopRange);
                    if (count < ShamanSettings.Heal.MinChainHealCount)
                        target = null;
                }
            }

            if (target != null)
                Logger.WriteDebug("Chain Heal Target:  found {0} with {1} nearby under {2}%", target.SafeName(), count, ShamanSettings.Heal.ChainHeal);

            return target;
        }

        private static WoWUnit GetBestHealingRainTarget()
        {
#if ORIGINAL
            return Clusters.GetBestUnitForCluster(Unit.NearbyFriendlyPlayers.Cast<WoWUnit>(), ClusterType.Radius, 10f);
#else
            if (!Me.IsInGroup() || !Me.Combat)
                return null;

            if (!Spell.CanCastHack("Healing Rain", Me))
            {
                Logger.WriteDebug("GetBestHealingRainTarget: CanCastHack says NO to Healing Rain");
                return null;
            }

            // build temp list of targets that could use heal and are in range + radius
            List<WoWUnit> coveredTargets = HealerManager.Instance.HealList
                .Where(u => u.IsAlive && u.DistanceSqr < 50 * 50 && u.HealthPercent < ShamanSettings.Heal.HealingRain )
                .ToList();

            // search all targets to find best one in best location to use as anchor for cast on ground
            var t = Unit.NearbyGroupMembersAndPets
                .Select(p => new {
                    Player = p,
                    Count = coveredTargets
                        .Where(pp => pp.IsAlive && pp.Location.DistanceSqr(p.Location) < 10 * 10)
                        .Count()
                    })
                .OrderByDescending(v => v.Count)
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (t != null && t.Count >= ShamanSettings.Heal.MinHealingRainCount )
            {
                Logger.WriteDebug("Healing Rain Target:  found {0} with {1} nearby under {2}%", t.Player.SafeName(), t.Count, ShamanSettings.Heal.HealingRain);
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

        private static int HealthToPriority(int nHealth)
        {
            return nHealth == 0 ? 0 : 200 - nHealth;
        }

        #region Diagnostics

        private static Composite CreateRestoDiagnosticOutputBehavior( UnitSelectionDelegate onUnit )
        {
            return new ThrottlePasses(1,1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        WoWUnit healtarget = onUnit(ret);
                        uint getaurastks = Me.GetAuraStacks("Tidal Waves");
                        uint actvstks = 0;

                        WoWAura aura = Me.Auras
                            .Where(kvp => kvp.Key == "Tidal Waves" && kvp.Value.TimeLeft != TimeSpan.Zero)
                            .Select(kvp => kvp.Value).FirstOrDefault();

                        if (aura != null)
                        {
                            actvstks = aura.StackCount;
                            if (actvstks == 0)
                                actvstks = 1;
                        }

                        if (actvstks != getaurastks)
                            Logger.WriteDebug(Color.MediumVioletRed, "Inconsistancy Error:  ActiveAuras[{0}] stacks != Me.GetAuraStacks('Tidal Waves') at [{1}] stacks", actvstks, getaurastks);

                        string shield;
                        uint shstacks;

                        shstacks = Me.GetAuraStacks("Earth Shield");
                        if (shstacks > 0)
                            shield = string.Format( "EARTH[{0}]", shstacks);
                        else if ( Me.HasAura("Water Shield"))
                            shield = string.Format( "WATER[{0}]", (long) Me.GetAuraTimeLeft("Water Shield").TotalMinutes);
                        else if ( Me.HasAura("Lightning Shield"))
                            shield = string.Format( "LHTNG[{0}]", (long) Me.GetAuraTimeLeft("Water Shield").TotalMinutes );
                        else 
                            shield = "NONE";

                        string line = string.Format(".... h={0:F1}%/m={1:F1}%, combat={2}, twaves={3}, shield={4}",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.Combat.ToYN(),
                            actvstks,
                            shield
                            );                      

                        if (healtarget == null)
                            line += ", target=(null)";
                        else
                            line += string.Format(", target={0} th={1:F1}% @ {2:F1} yds, combat={3}, tph={4:F1}%, tloss={5}, eshield={6}, riptide={7}",
                                healtarget.SafeName(),
                                healtarget.HealthPercent,
                                healtarget.Distance,
                                healtarget.Combat.ToYN(),
                                healtarget.GetPredictedHealthPercent(true),
                                healtarget.InLineOfSpellSight,
                                (healtarget.GetAuraStacks("Earth Shield") > 0).ToYN(),
                                (long) healtarget.GetAuraTimeLeft("Riptide").TotalMilliseconds
                                );

                        Logger.WriteDebug(Color.Yellow, line);
                        return RunStatus.Failure;
                    }))
                );
        }

        #endregion

    }
}
