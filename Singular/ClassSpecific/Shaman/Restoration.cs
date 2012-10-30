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

        #region BUFFS

        [Behavior(BehaviorType.CombatBuffs | BehaviorType.PreCombatBuffs, WoWClass.Shaman, WoWSpec.ShamanRestoration)]
        public static Composite CreateRestoShamanHealingBuffs()
        {
            return new PrioritySelector(
                // Keep WS up at all times. Period.
                Spell.BuffSelf("Water Shield"),

                Common.CreateShamanImbueMainHandBehavior( Imbue.Earthliving, Imbue.Flametongue)

                );
        }

        #endregion

        #region NORMAL 

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Normal)]
        public static Composite CreateRestoShamanRest()
        {
            return new PrioritySelector(
                CreateRestoShamanHealingBuffs(),
                CreateRestoShamanHealingOnlyBehavior(true, false),
                Singular.Helpers.Rest.CreateDefaultRestBehaviour(),
                Spell.Resurrect("Ancestral Spirit"),
                CreateRestoShamanHealingOnlyBehavior(false, true)
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Shaman, WoWSpec.ShamanRestoration)]
        public static Composite CreateRestoShamanPullBehavior()
        {
            return
                new PrioritySelector(
                    CreateRestoShamanHealingOnlyBehavior( false, true),
                    new Decorator(
                        ret => !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid),
                        Elemental.CreateShamanElementalNormalPull()
                        )
                    );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Normal)]
        public static Composite CreateRestoShamanHealBehaviorNormal()
        {
            return
                new PrioritySelector(
                    CreateRestoShamanHealingOnlyBehavior());
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Normal)]
        public static Composite CreateRestoShamanCombatBehaviorNormal()
        {
            return
                new PrioritySelector(
                    CreateRestoShamanHealingOnlyBehavior(),
                    new Decorator(
                        ret => !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid),
                        Elemental.CreateShamanElementalNormalCombat())
                    );
        }

        #endregion

        #region INSTANCES

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Instances)]
        public static Composite CreateRestoShamanRestInstances()
        {
            return new PrioritySelector(
                CreateRestoShamanHealingBuffs(),
                CreateRestoShamanHealingOnlyBehavior(true, false),
                Singular.Helpers.Rest.CreateDefaultRestBehaviour(),
                Spell.Resurrect("Ancestral Spirit"),
                CreateRestoShamanHealingOnlyBehavior(false, true)
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Instances)]
        public static Composite CreateRestoShamanHealBehaviorInstance()
        {
            return
                new PrioritySelector(
                    CreateRestoShamanHealingOnlyBehavior());
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Instances )]
        public static Composite CreateRestoShamanCombatBehaviorInstances()
        {
            return new PrioritySelector(
                CreateRestoShamanHealingOnlyBehavior(),
                // already waited on cast to complete in heal behavior
                new Decorator(
                    ret => !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid) || TalentManager.HasGlyph("Telluric Currents"),
                    new PrioritySelector(
                        Safers.EnsureTarget(),
                        Movement.CreateMoveToRangeAndStopBehavior( ret => (WoWUnit)ret, ret => 38f),
                        Movement.CreateFaceTargetBehavior(),
                        new Decorator(
                            ret => !SpellManager.GlobalCooldown,
                            new PrioritySelector(
                                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                                Spell.Cast("Lightning Bolt")
                                )
                            )
                        )
                    )
                );

        }

        #endregion

        #region BATTLEGROUNDS

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Battlegrounds )]
        public static Composite CreateRestoShamanRestPvp()
        {
            return new PrioritySelector(
                CreateRestoShamanHealingBuffs(),
                CreateRestoShamanHealingOnlyBehavior(true,false),
                Singular.Helpers.Rest.CreateDefaultRestBehaviour(),
                Spell.Resurrect("Ancestral Spirit"),
                CreateRestoShamanHealingOnlyBehavior(false, true)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Battlegrounds)]
        public static Composite CreateRestoShamanCombatBehaviorPvp()
        {
            return
                new PrioritySelector(
                    CreateRestoShamanHealingOnlyBehavior(false, true)
                    );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.Battlegrounds)]
        public static Composite CreateRestoShamanHealBehaviorPvp()
        {
            return
                new PrioritySelector(
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

        private static ulong guidLastHealTarget = 0;

        public static Composite CreateRestoShamanHealingOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();

            behavs.AddBehavior( HealthToPriority( SingularSettings.Instance.Shaman.Heal.AncestralSwiftness), 
                "Unleash Elements", 
                "Unleash Elements",
                Spell.Buff("Unleash Elements",
                    ret => (WoWUnit)ret,
                    ret => (Me.IsMoving || ((WoWUnit)ret).GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.AncestralSwiftness)
                        && Common.IsImbuedForHealing(Me.Inventory.Equipped.MainHand)
                        ));

            behavs.AddBehavior(HealthToPriority( SingularSettings.Instance.Shaman.Heal.AncestralSwiftness),
                String.Format("Ancestral Swiftness @ {0}%", SingularSettings.Instance.Shaman.Heal.AncestralSwiftness), 
                "Ancestral Swiftness",
                new Sequence(
                    Spell.BuffSelf("Ancestral Swiftness", ret => ((WoWUnit)ret).GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.AncestralSwiftness),
                    Spell.Heal("Greater Healing Wave", ret => (WoWUnit)ret)
                    ));

            behavs.AddBehavior(HealthToPriority( SingularSettings.Instance.Shaman.Heal.GreaterHealingWave), "Greater Healing Wave", "Greater Healing Wave",
                Spell.Heal("Greater Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.GreaterHealingWave));

            behavs.AddBehavior(HealthToPriority( SingularSettings.Instance.Shaman.Heal.HealingWave), "Healing Wave", "Healing Wave", 
                Spell.Heal("Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.HealingWave));

            behavs.AddBehavior(HealthToPriority( SingularSettings.Instance.Shaman.Heal.HealingSurge), "Healing Surge", "Healing Surge", 
                Spell.Heal("Healing Surge", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.HealingSurge));

            behavs.AddBehavior(HealthToPriority( SingularSettings.Instance.Shaman.Heal.ChainHeal), "Chain Heal", "Chain Heal", 
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        new PrioritySelector(
                            context => Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, ChainHealHopRange ),
                            Spell.Heal(
                                "Chain Heal", ret => (WoWPlayer)ret,
                                ret => Clusters.GetClusterCount((WoWPlayer)ret, ChainHealPlayers, ClusterType.Chained, ChainHealHopRange) >= 3)
                            )
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority( SingularSettings.Instance.Shaman.Heal.HealingRain), "Healing Rain", "Healing Rain",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        context => Clusters.GetBestUnitForCluster(Unit.NearbyFriendlyPlayers.Cast<WoWUnit>(), ClusterType.Radius, 10f),
                        Spell.CastOnGround(
                            "Healing Rain", 
                            ret => ((WoWPlayer)ret).Location,
                            ret => (StyxWoW.Me.GroupInfo.IsInRaid ? 3 : 2) < Clusters.GetClusterCount((WoWPlayer)ret, Unit.NearbyFriendlyPlayers.Cast<WoWUnit>(), ClusterType.Radius, 10f))
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority( SingularSettings.Instance.Shaman.Heal.SpiritLinkTotem), "Spirit Link Totem", "Spirit Link Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Spirit Link Totem", ret => (WoWPlayer)ret,
                        ret => Unit.NearbyFriendlyPlayers.Count(
                            p => p.GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.SpiritLinkTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.SpiritLink)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority( SingularSettings.Instance.Shaman.Heal.HealingTideTotemPercent), "Healing Tide Totem", "Healing Tide Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Healing Tide Totem",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.HealingTideTotemPercent && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority( SingularSettings.Instance.Shaman.Heal.HealingStreamTotem), "Healing Stream Totem", "Healing Stream Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Healing Stream Totem",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.HealingStreamTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                            && !Totems.Exist( WoWTotemType.Water)
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority( SingularSettings.Instance.Shaman.Heal.Ascendance), "Ascendance", "Ascendance",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.BuffSelf(
                        "Ascendance",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.Ascendance) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );


            behavs.OrderBehaviors();
            behavs.ListBehaviors();


            return new PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
//                ctx => selfOnly ? StyxWoW.Me : GetHealTarget(),

                Spell.WaitForCast(),

                new Decorator(
                    ret => Me.IsCasting,
                    new ActionAlwaysSucceed()),

                new Decorator(
                    ret => ret != null && ((WoWUnit)ret).GetPredictedHealthPercent() <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth,

                    new PrioritySelector(

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

/*
                        new Sequence(
                            new Action(ret => Logger.WriteDebug(Color.LightGreen, "-- past spellcast")),
                            new Action(ret => { return RunStatus.Failure; })
                            ),
*/
                        new Decorator(
                            ret => !SpellManager.GlobalCooldown,

                            new PrioritySelector(
    /*
                                new Sequence(
                                    new Action(ret => Logger.WriteDebug(Color.LightGreen, "-- past gcd")),
                                    new Action(ret => { return RunStatus.Failure; })
                                    ),
    */
                            Totems.CreateTotemsBehavior(),

                            Spell.Heal("Earth Shield",
                                ret => (WoWUnit)ret,
                                ret => ret is WoWPlayer && Group.Tanks.Contains((WoWPlayer)ret) && Group.Tanks.All(t => !t.HasMyAura("Earth Shield"))),

                            // Just pop RT on CD. Plain and simple. Calling GetBestRiptideTarget will see if we can spread RTs (T12 2pc)
                            Spell.Heal("Riptide",
                                ret => GetBestRiptideTarget((WoWPlayer)ret),
                                ret => !Me.HasAnyAura("Tidal Waves", "Ancestral Swiftness")
                                    && (((WoWPlayer)ret).GetPredictedHealthPercent() > 15 || Spell.GetSpellCooldown("Ancestral Swiftness").TotalMinutes > 0f) // use Ancestral Swiftness value to check
                                ),

                            behavs.GenerateBehaviorTree(),

                            Spell.Heal("Riptide",
                                ret => GetBestRiptideTarget((WoWPlayer)ret),
                                ret => !Me.HasAura("Ancestral Swiftness"))

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
                                    Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

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

        private static WoWPlayer GetHealTarget()
        {
            List<WoWPlayer> targets = 
                (from p in Unit.GroupMembers
                where !(p.IsDead || p.IsGhost)
                    && p.IsHorde == p.IsHorde
                    && !p.IsHostile
                    && p.GetPredictedHealthPercent() <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth
                    && ((p.Distance < 40 && p.InLineOfSpellSight) || (!SingularSettings.Instance.DisableAllMovement && p.Distance < 80))
                orderby p.GetPredictedHealthPercent()
                select p)
                .ToList();

            Logger.WriteDebug(" ");
            foreach (WoWPlayer p in targets)
            {
                Logger.WriteDebug(Color.LightGreen, "  HTrg @ {0:F1} yds - {1}-{2} {3:F1}%", p.Distance, p.Class.ToString(), p.SafeName(), p.GetPredictedHealthPercent());
            }

            return targets.FirstOrDefault();
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
                return Unit.NearbyFriendlyPlayers.Where(u => u.GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.ChainHeal).Select(u => (WoWUnit)u);
            }
        }

        private static WoWPlayer GetBestRiptideTarget(WoWPlayer originalTarget)
        {
            if (!originalTarget.HasMyAura("Riptide") && originalTarget.InLineOfSpellSight)
                return originalTarget;

            // Target already has RT. So lets find someone else to throw it on. Lowest health first preferably.
            WoWPlayer ripTarget = Unit.GroupMembers.Where(u => u.DistanceSqr < 40 * 40 && !u.HasMyAura("Riptide") && u.InLineOfSpellSight).OrderBy(u => u.GetPredictedHealthPercent()).FirstOrDefault();
            if (ripTarget != null && ripTarget.GetPredictedHealthPercent() > SingularSettings.Instance.IgnoreHealTargetsAboveHealth)
                ripTarget = null;

            return ripTarget;
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
                blist = blist.OrderBy(b => -b.Priority).ToList();
            }

            public Composite GenerateBehaviorTree()
            {
                blist = blist.OrderBy(b => b.Priority).ToList();
                return new PrioritySelector(blist.Select(b => b.behavior).ToArray());
            }

            public void ListBehaviors()
            {
                foreach (PrioritizedBehavior hs in blist)
                {
                    Logger.WriteDebug(Color.LightGreen, "   Priority {0} for Behavior [{1}]", hs.Priority, hs.Name);
                }
            }
        }
        
    }
}
