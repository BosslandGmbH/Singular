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
            List<HealSpell> spells = new List<HealSpell>();

            AddHealSpell(spells, SingularSettings.Instance.Shaman.Heal.AncestralSwiftness-1, "Unleash Elements",
                Spell.Buff("Unleash Elements",
                    ret => (WoWUnit)ret,
                    ret => (Me.IsMoving || ((WoWUnit)ret).GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.AncestralSwiftness)
                        && Common.IsImbuedForHealing(Me.Inventory.Equipped.MainHand)
                        ));

            AddHealSpell(spells, SingularSettings.Instance.Shaman.Heal.AncestralSwiftness, "Ancestral Swiftness",
                new Sequence(
                    Spell.BuffSelf("Ancestral Swiftness", ret => ((WoWUnit)ret).GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.AncestralSwiftness),
                    Spell.Heal("Greater Healing Wave", ret => (WoWUnit)ret)
                    ));

            AddHealSpell(spells, SingularSettings.Instance.Shaman.Heal.GreaterHealingWave , "Greater Healing Wave",
                Spell.Heal("Greater Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.GreaterHealingWave));

            AddHealSpell(spells, SingularSettings.Instance.Shaman.Heal.HealingWave , "Healing Wave", 
                Spell.Heal("Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.HealingWave));

            AddHealSpell(spells, SingularSettings.Instance.Shaman.Heal.HealingSurge , "Healing Surge", 
                Spell.Heal("Healing Surge", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.HealingSurge));

            AddHealSpell(spells, SingularSettings.Instance.Shaman.Heal.ChainHeal , "Chain Heal", 
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

            AddHealSpell(spells, SingularSettings.Instance.Shaman.Heal.HealingRain , "Healing Rain",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        context => Clusters.GetBestUnitForCluster(Unit.NearbyFriendlyPlayers.Cast<WoWUnit>(), ClusterType.Radius, 10f),
                        Spell.CastOnGround(
                            "Healing Rain", ret => ((WoWPlayer)ret).Location,
                            ret =>
                            Clusters.GetClusterCount((WoWPlayer)ret, Unit.NearbyFriendlyPlayers.Cast<WoWUnit>(), ClusterType.Radius, 10f) >
                            // If we're in a raid, check for 4 players. If we're just in a party, check for 3.
                            (StyxWoW.Me.GroupInfo.IsInRaid ? 3 : 2))
                        )
                    )
                );

            AddHealSpell(spells, SingularSettings.Instance.Shaman.Heal.SpiritLinkTotem , "Spirit Link Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Spirit Link Totem", ret => (WoWPlayer)ret,
                        ret => Unit.NearbyFriendlyPlayers.Count(
                            p => p.GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.SpiritLinkTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.SpiritLink)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );

            AddHealSpell(spells, SingularSettings.Instance.Shaman.Heal.HealingTideTotemPercent , "Healing Tide Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Healing Tide Totem",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.HealingTideTotemPercent && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );

            AddHealSpell(spells, SingularSettings.Instance.Shaman.Heal.HealingStreamTotem , "Healing Stream Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Healing Stream Totem",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.HealingStreamTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                            && !Totems.Exist( WoWTotemType.Water)
                        )
                    )
                );

            AddHealSpell(spells, SingularSettings.Instance.Shaman.Heal.Ascendance , "Ascendance",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.BuffSelf(
                        "Ascendance",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.Ascendance) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );


            spells = spells.OrderBy(hs => hs.Pct).ToList();
            ListHealSpells(spells);


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

                            new PrioritySelector(
                                spells.Select(hs => hs.behavior).ToArray()),

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


        class HealSpell
        {
            public int Pct  { get; set; }
            public string SpellName  { get; set; }
            public Composite behavior  { get; set; }

            public HealSpell(int p, string s, Composite bt)
            {
                Pct = p;
                SpellName = s;
                behavior = bt;
            }
        }

        private static void AddHealSpell(List<HealSpell> spells, int p, string n, Composite bt)
        {
            if (p <= 0)
                Logger.WriteDebug("Skipping heal spell {0} configured for {1}%", n, p);
            else if (!SpellManager.HasSpell(n))
                Logger.WriteDebug("Skipping heal spell {0} not known by this character", n, p);
            else
                spells.Add(new HealSpell(p, n, bt));
        }

        private static DateTime NextListCall = DateTime.Now;

        private static void ListHealSpells(List<HealSpell> spells)
        {
            if (DateTime.Now > NextListCall)
            {
                foreach (HealSpell hs in spells)
                {
                    Logger.WriteDebug(Color.LightGreen, "   Heal @ {0}% using [{1}]", hs.Pct, hs.SpellName);
                }

                NextListCall = DateTime.Now + new TimeSpan(0, 0, 0, 0, 1000);
            }
        }
    }
}
