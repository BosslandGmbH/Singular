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
                    target = Unit.NearbyGroupMembers.Where(u => IsValidEarthShieldTarget(u.ToUnit())).OrderByDescending(u => Unit.NearbyUnfriendlyUnits.Count(e => e.CurrentTargetGuid == u.Guid)).FirstOrDefault();
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

                        CreateRestoShamanHealingBuffsNormal(),
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
                    CreateRestoShamanHealingOnlyBehavior());
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Normal)]
        public static Composite CreateRestoShamanCombatBehaviorNormal()
        {
            return
                new PrioritySelector(
                    Spell.WaitForCastOrChannel(),
                    CreateRestoShamanHealingOnlyBehavior(),
                    new Decorator(
                        ret => !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid),
                        new PrioritySelector(
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
                            )
                        
                        )
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
                CreateRestoShamanHealingOnlyBehavior()
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Instances )]
        public static Composite CreateRestoShamanCombatBehaviorInstances()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),

                CreateRestoShamanHealingOnlyBehavior(),

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


        public static Composite CreateRestoShamanHealingOnlyBehavior()
        {
            return CreateRestoShamanHealingOnlyBehavior(false, true);
        }

        public static Composite CreateRestoShamanHealingOnlyBehavior(bool selfOnly)
        {
            return CreateRestoShamanHealingOnlyBehavior(selfOnly, true);
        }

        // private static ulong guidLastHealTarget = 0;

        public static Composite CreateRestoShamanHealingOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();

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
            behavs.AddBehavior(HealthToPriority( ShamanSettings.Heal.AncestralSwiftness),
                String.Format("Ancestral Swiftness @ {0}%", ShamanSettings.Heal.AncestralSwiftness), 
                "Ancestral Swiftness",
                new Decorator(
                    ret => ((WoWUnit)ret).GetPredictedHealthPercent() < ShamanSettings.Heal.AncestralSwiftness,
                    new Sequence(
                        Spell.BuffSelf("Ancestral Swiftness"),
                        Spell.Cast("Greater Healing Wave", ret => (WoWUnit)ret)
                        )
                    )
                );

            if ( SingularRoutine.CurrentWoWContext == WoWContext.Instances )
                behavs.AddBehavior( 9999, "Earth Shield", "Earth Shield", Spell.Buff("Earth Shield", on => GetBestEarthShieldTargetInstance()));

            int dispelPriority = (SingularSettings.Instance.DispelDebuffs == DispelStyle.HighPriority) ? 9999 : -9999;
            behavs.AddBehavior( dispelPriority, "Purify Spirit", null, Dispelling.CreateDispelBehavior());

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.GreaterHealingWave), "Greater Healing Wave", "Greater Healing Wave",
                new Decorator( ret => ((WoWUnit)ret).GetPredictedHealthPercent() < ShamanSettings.Heal.GreaterHealingWave,
                    new Sequence(
                        BuffUnleashLife(on => (WoWUnit) on),
                        new WaitContinue(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                        Spell.Cast("Greater Healing Wave", on => (WoWUnit)on),
                        new Action( r => TidalWaveConsume() )
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.HealingWave), "Healing Wave", "Healing Wave",
                new Sequence(
                    Spell.Cast("Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).GetPredictedHealthPercent() < ShamanSettings.Heal.HealingWave),
                    new Action( r => TidalWaveConsume() )
                    )
                );

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.HealingSurge), "Healing Surge", "Healing Surge", 
                new Sequence(
                    Spell.Cast("Healing Surge", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).GetPredictedHealthPercent() < ShamanSettings.Heal.HealingSurge),
                    new Action(r => TidalWaveConsume())
                    )
                );

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.ChainHeal), "Chain Heal", "Chain Heal",
                new Sequence(
                    Spell.Cast( "Chain Heal", on => GetBestChainHealTarget() ),
                    new Action( r => TidalWaveRefresh() )
                    )
                );

            behavs.AddBehavior(HealthToPriority(ShamanSettings.Heal.HealingRain), "Healing Rain", "Healing Rain",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        context => GetBestHealingRainTarget(),
                        new Sequence(
                            BuffUnleashLife(on => (WoWUnit) on),
                            new WaitContinue(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                            Spell.CastOnGround(
                                "Healing Rain", 
                                on => ((WoWUnit)on).Location,
                                ret => true)
                            )
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority( ShamanSettings.Heal.SpiritLinkTotem), "Spirit Link Totem", "Spirit Link Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Spirit Link Totem", ret => (WoWPlayer)ret,
                        ret => Unit.NearbyFriendlyPlayers.Count(
                            p => p.GetPredictedHealthPercent() < ShamanSettings.Heal.SpiritLinkTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.SpiritLink)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority( ShamanSettings.Heal.HealingTideTotemPercent), "Healing Tide Totem", "Healing Tide Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Healing Tide Totem",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() < ShamanSettings.Heal.HealingTideTotemPercent && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority( ShamanSettings.Heal.HealingStreamTotem), "Healing Stream Totem", "Healing Stream Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Healing Stream Totem",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() < ShamanSettings.Heal.HealingStreamTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                            && !Totems.Exist( WoWTotemType.Water)
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority( ShamanSettings.Heal.Ascendance), "Ascendance", "Ascendance",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.BuffSelf(
                        "Ascendance",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() < ShamanSettings.Heal.Ascendance) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );


            behavs.OrderBehaviors();
            behavs.ListBehaviors();


            return new PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,

                CreateRestoDiagnosticOutputBehavior(ret => (WoWUnit)ret),

                new Decorator(
                    ret => ret != null && (Me.Combat || ((WoWUnit)ret).Combat || ((WoWUnit)ret).GetPredictedHealthPercent() <= 99),

                    new PrioritySelector(
/*
                        new Sequence(
                            new Decorator(
                                ret => guidLastHealTarget != ((WoWUnit)ret).Guid,
                                new Action(ret =>
                                {
                                    guidLastHealTarget = ((WoWUnit)ret).Guid;
                                    Logger.WriteDebug(Color.LightGreen, "Heal Target - {0} {1:F1}% @ {2:F1} yds", ((WoWUnit)ret).SafeName(), ((WoWUnit)ret).GetPredictedHealthPercent(), ((WoWUnit)ret).Distance);
                                })),
                            new Action(ret => { return RunStatus.Failure; })
                            ),
*/
/*
                        new Sequence(
                            new Action(ret => Logger.WriteDebug(Color.LightGreen, "-- past spellcast")),
                            new Action(ret => { return RunStatus.Failure; })
                            ),
*/
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                new ThrottlePasses( 1, 1,
                                    new Action(ret => {
                                        Logger.WriteDebug(Color.LightGreen, "Heal Target - {0} {1:F1}% @ {2:F1} yds", ((WoWUnit)ret).SafeName(), ((WoWUnit)ret).GetPredictedHealthPercent(), ((WoWUnit)ret).Distance);
                                        return RunStatus.Failure;
                                        })
                                    ),

                                Totems.CreateTotemsBehavior(),

    /*
                                Spell.Cast("Earth Shield",
                                    ret => (WoWUnit)ret,
                                    ret => ret is WoWPlayer && Group.Tanks.Contains((WoWPlayer)ret) && Group.Tanks.All(t => !t.HasMyAura("Earth Shield"))),
    */
                                // cast Riptide if we need Tidal Waves -- skip if Ancestral Swiftness is 
                                new Decorator(
                                    ret => IsTidalWavesNeeded,
                                    new Sequence(
                                        Spell.Cast("Riptide", on => {
                                            WoWUnit unit = GetBestRiptideTarget((WoWPlayer)on, true);
                                            if (unit != null)
                                                Logger.WriteDebug("Buffing Tidal Waves with Riptide: {0}", unit.SafeName());
                                            return unit;
                                            }),
                                        new Action( r => TidalWaveRefresh() )
                                        )
                                    ),

                                // roll Riptide on Tanks if we are glyphed (prep for most chain heals)
                                new Decorator(
                                    ret => TalentManager.HasGlyph("Riptide"),
                                    new Sequence(
                                        Spell.Cast("Riptide", on => {
                                            WoWUnit unit = Group.Tanks.FirstOrDefault(t => t.IsAlive && !t.HasMyAura("Riptide"));
                                            if (unit != null && (Me.Combat || unit.Combat))
                                            {
                                                Logger.WriteDebug("Rolling Riptide on Best Target: {0}", unit.SafeName());
                                                return unit;
                                            }
                                            return null;
                                            }),
                                        new Action( r => TidalWaveRefresh() )
                                        )
                                    ),

                                behavs.GenerateBehaviorTree(),

                                // roll Riptide on others because we can
                                new Sequence(
                                    Spell.Cast("Riptide", on => {
                                        if (Unit.GroupMembers.Count(m => m.HasMyAura("Riptide")) >= ShamanSettings.Heal.RollRiptideCount)
                                            return null;

                                        WoWUnit unit = GetBestRiptideTarget((WoWPlayer)on);
                                        if (unit != null && (Me.Combat || unit.Combat))
                                        {
                                            Logger.WriteDebug("Rolling Riptide on Best Target: {0}", unit.SafeName());
                                            return unit;
                                        }
                                        return null;
                                        }),

                                    new Action(r => TidalWaveRefresh())
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
                                    ))
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
                return Unit.NearbyGroupMembersAndPets.Where(u => u.IsAlive && u.GetPredictedHealthPercent() < ShamanSettings.Heal.ChainHeal).Select(u => (WoWUnit)u);
            }
        }

        private static IEnumerable<WoWUnit> ChainHealRiptidePlayers
        {
            get
            {
                // TODO: Decide if we want to do this differently to ensure we take into account the T12 4pc bonus. (Not removing RT when using CH)
                return Unit.NearbyGroupMembers.Where(u => u.IsAlive && u.GetPredictedHealthPercent() < ShamanSettings.Heal.ChainHeal && u.HasMyAura("Riptide")).Select(u => (WoWUnit)u);
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

                // WoWAura tw = Me.ActiveAuras.Where(a => a.Key == "Tidal Waves").Select(a => a.Value).FirstOrDefault();
                // WoWAura tw = Me.GetAuraByName("Tidal Waves");
                uint stacks = Me.GetAuraStacks("Tidal Waves");

                // 2 stacks means we don't have an issue
                if ( stacks >= 2 )
                    return false;

                // 1 stack? special case and a spell that will consume it is in progress or our audit count shows its gone
                uint castId = Me.CurrentCastId;
                string castname = castId == 0 ? "(none)" : WoWSpell.FromId((int)castId).Name;
                if (stacks == 1 && TidalWaveAuditCount() > 0 && castId != HW && castId != GHW && castId != HS)
                    return false;

                return true;
            }
        }

        #endregion

        private static WoWPlayer GetBestRiptideTarget(WoWPlayer originalTarget, bool force = false)
        {
            if (originalTarget != null && originalTarget.IsValid && originalTarget.IsAlive && !originalTarget.HasMyAura("Riptide") && originalTarget.InLineOfSpellSight && originalTarget.GetPredictedHealthPercent(true) < 95)
                return originalTarget;

            // cant RT target, so lets check tanks
            WoWPlayer ripTarget = null;

//            if (!SpellManager.HasSpell("Earth Shield"))
            {
                ripTarget = Group.Tanks.Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && !u.HasMyAura("Riptide") && u.InLineOfSpellSight).OrderBy(u => u.GetPredictedHealthPercent()).FirstOrDefault();
                if (ripTarget != null && ripTarget.Combat) // (ripTarget.Combat || ripTarget.GetPredictedHealthPercent() <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth))
                    return ripTarget;
            }

            // cant RT target or tanks, so lets find someone else to throw it on. Lowest health first preferably for now.
            ripTarget = Unit.GroupMembers.Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && !u.HasMyAura("Riptide") && u.InLineOfSpellSight).OrderBy(u => u.GetPredictedHealthPercent()).FirstOrDefault();
            if (ripTarget != null && ripTarget.IsAlive ) // && ripTarget.GetPredictedHealthPercent() <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth)
                return ripTarget;

            if (force)
            {
                ripTarget = Unit.GroupMembers
                    .Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && u.InLineOfSpellSight)
                    .OrderBy(u => u.GetAuraTimeLeft("Riptide").TotalSeconds)
                    .FirstOrDefault();
                return ripTarget;
            }

            return null;
        }

        private static WoWPlayer GetBestTargetRiptide(WoWPlayer originalTarget)
        {
            if (originalTarget != null && originalTarget.IsValid && originalTarget.IsAlive && !originalTarget.HasMyAura("Riptide") && originalTarget.InLineOfSpellSight)
                return originalTarget;

            // cant RT target, so lets check tanks
            WoWPlayer ripTarget = null;

            //            if (!SpellManager.HasSpell("Earth Shield"))
            {
                ripTarget = Group.Tanks.Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && !u.HasMyAura("Riptide") && u.InLineOfSpellSight).OrderBy(u => u.GetPredictedHealthPercent()).FirstOrDefault();
                if (ripTarget != null && ripTarget.Combat) // (ripTarget.Combat || ripTarget.GetPredictedHealthPercent() <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth))
                    return ripTarget;
            }

            // cant RT target, so lets find someone else to throw it on. Lowest health first preferably for now.
            ripTarget = Unit.GroupMembers.Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && !u.HasMyAura("Riptide") && u.InLineOfSpellSight).OrderBy(u => u.GetPredictedHealthPercent()).FirstOrDefault();
            if (ripTarget != null && ripTarget.IsAlive) // && ripTarget.GetPredictedHealthPercent() <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth)
                return ripTarget;

            return null;
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


            var targetInfo = ChainHealRiptidePlayers
                .Select( p => new { Unit = p, Count = Clusters.GetClusterCount(p, ChainHealPlayers, ClusterType.Chained, ChainHealHopRange) })
                .OrderByDescending( v => v.Count )
                .ThenByDescending( v => Group.Tanks.Any( t => t.Guid == v.Unit.Guid))
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            WoWUnit target = targetInfo == null ? null : targetInfo.Unit ;
            int count = targetInfo == null ? 0 : targetInfo.Count ;

            if ( target == null || count < 3 )
            {
                target = Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, ChainHealHopRange);
                if (target != null)
                {
                    count = Clusters.GetClusterCount(target, ChainHealPlayers, ClusterType.Chained, ChainHealHopRange);
                    if (count < 3)
                    {
                        target = null;
                    }
                }
            }

            if (target != null)
                Logger.WriteDebug("Chain Heal Target:  found {0} with {1} nearby", target.SafeName(), count);

            return target;
        }

        private static WoWUnit GetBestHealingRainTarget()
        {
#if ORIGINAL
            return Clusters.GetBestUnitForCluster(Unit.NearbyFriendlyPlayers.Cast<WoWUnit>(), ClusterType.Radius, 10f);
#else
            if (!Me.IsInGroup())
                return null;

            if (!Spell.CanCastHack("Healing Rain", Me))
            {
                Logger.WriteDebug("GetBestHealingRainTarget: CanCastHack says NO to Healing Rain");
                return null;
            }

            var t = Unit.NearbyGroupMembersAndPets
                .Where(p => p.IsAlive && (p.Combat || p.HealthPercent < 100))
                .Select(p => new {
                    Player = p,
                    Count = Unit.NearbyGroupMembersAndPets
                        .Where(pp => pp.IsAlive && pp.Location.DistanceSqr(p.Location) < 100)
                        .Count()
                    })
                .OrderByDescending(v => v.Count)
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (t != null && t.Count >= 4)
            {
                Logger.WriteDebug("Healing Rain Target:  found {0} with {1} nearby", t.Player.SafeName(), t.Count);
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
            return nHealth == 0 ? 0 : 1000 - nHealth;
        }

        class PrioritizedBehaviorList
        {
            class PrioritizedBehavior
            {
                public int Priority { get; set; }
                public string Name { get; set; }
                public Composite behavior { get; set; }

                public PrioritizedBehavior(int p, string s, Composite bt)
                {
                    Priority = p;
                    Name = s;
                    behavior = bt;
                }
            }

            List<PrioritizedBehavior> blist = new List<PrioritizedBehavior>();

            public void AddBehavior(int pri, string behavName, string spellName, Composite bt)
            {
                if (pri <= 0)
                    Logger.WriteDebug("Skipping Behavior [{0}] configured for Priority {1}", behavName, pri);
                else if (!String.IsNullOrEmpty(spellName) && !SpellManager.HasSpell(spellName))
                    Logger.WriteDebug("Skipping Behavior [{0}] since spell '{1}' is not known by this character", behavName, spellName);
                else
                    blist.Add(new PrioritizedBehavior(pri, behavName, bt));
            }

            public void OrderBehaviors()
            {
                blist = blist.OrderByDescending(b => b.Priority).ToList();
            }

            public Composite GenerateBehaviorTree()
            {
                return new PrioritySelector(blist.Select(b => b.behavior).ToArray());
            }

            public void ListBehaviors()
            {
                foreach (PrioritizedBehavior hs in blist)
                {
                    Logger.WriteDebug(Color.GreenYellow, "   Priority {0} for Behavior [{1}]", hs.Priority, hs.Name);
                }
            }
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
                        uint tstks = 0;
                        WoWAura aura = Me.ActiveAuras.Where(kvp => kvp.Key == "Tidal Waves").Select(kvp => kvp.Value).FirstOrDefault();
                        if (aura != null)
                            tstks = aura.StackCount;

                        if (tstks != Me.GetAuraStacks("Tidal Waves"))
                            Logger.WriteDebug(Color.MediumVioletRed, "Inconsistancy Error:  have {0} stacks != Me.GetAuraStacks('Tidal Waves')", tstks);

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
                            tstks,
                            shield
                            );                      

                        if (healtarget == null)
                            line += ", target=(null)";
                        else
                            line += string.Format(", target={0}:th={1:F1}% @ {2:F1} yds, combat={3}, tph={4:F1}%, tloss={5}, eshield={6}, riptide={7}",
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
