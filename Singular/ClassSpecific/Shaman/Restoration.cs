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


namespace Singular.ClassSpecific.Shaman
{
    class Restoration
    {
        private const int RESTO_T12_ITEM_SET_ID = 1014;
        private const int ELE_T12_ITEM_SET_ID = 1016;

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

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


        [Behavior(BehaviorType.CombatBuffs | BehaviorType.PreCombatBuffs, WoWClass.Shaman, WoWSpec.ShamanRestoration)]
        public static Composite CreateRestoShamanHealingBuffs()
        {
            return new PrioritySelector(
                // Keep WS up at all times. Period.
                Spell.BuffSelf("Water Shield"),

                Common.CreateShamanImbueMainHandBehavior( Imbue.Earthliving, Imbue.Flametongue)

                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanRestoration)]
        public static Composite CreateRestoShamanRest()
        {
            return new PrioritySelector(
                CreateRestoShamanHealingBuffs(),
                CreateRestoShamanHealingOnlyBehavior(true),
                Singular.Helpers.Rest.CreateDefaultRestBehaviour(),
                Spell.Resurrect("Ancestral Spirit"),
                CreateRestoShamanHealingOnlyBehavior(false,false)
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.All)]
        public static Composite CreateRestoShamanPullBehavior()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid),
                        Elemental.CreateShamanElementalNormalPull())
                    );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanRestoration, WoWContext.All)]
        public static Composite CreateRestoShamanCombatBehavior()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid),
                        Elemental.CreateShamanElementalNormalCombat())
                    );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanRestoration)]
        public static Composite CreateRestoShamanHealBehavior()
        {
            return
                new PrioritySelector(
                    CreateRestoShamanHealingOnlyBehavior());
        }

        public static Composite CreateRestoShamanHealingOnlyBehavior()
        {
            return CreateRestoShamanHealingOnlyBehavior(false, false);
        }

        public static Composite CreateRestoShamanHealingOnlyBehavior(bool selfOnly)
        {
            return CreateRestoShamanHealingOnlyBehavior(selfOnly, false);
        }

        public static Composite CreateRestoShamanHealingOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;

#if ORIGINAL
            return new PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                new Decorator(
                    ret => ret != null && (moveInRange || ((WoWUnit)ret).InLineOfSpellSight && ((WoWUnit)ret).DistanceSqr < 40 * 40),
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToLosBehavior(ret => (WoWUnit)ret)),

                        Totems.CreateTotemsBehavior(),

                        // Just pop RT on CD. Plain and simple. Calling GetBestRiptideTarget will see if we can spread RTs (T12 2pc)
                        Spell.Heal("Riptide", ret => GetBestRiptideTarget((WoWPlayer)ret)),
                        // And deal with some edge PVP cases.

                        Spell.Heal("Earth Shield", 
                            ret => (WoWUnit)ret, 
                            ret => ret is WoWPlayer && Group.Tanks.Contains((WoWPlayer)ret) && Group.Tanks.All(t => !t.HasMyAura("Earth Shield"))),

                        // Pop NS if someone is in some trouble.
                        Spell.BuffSelf("Ancestral Swiftness", ret => ((WoWUnit)ret).HealthPercent < 15),
                        Spell.Heal("Unleash Elements", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).HealthPercent < 40 && Common.IsImbuedForHealing(Me.Inventory.Equipped.MainHand)),
                        // GHW is highest priority. It should be fairly low health %. (High-end healers will have this set to 70ish
                        Spell.Heal("Greater Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).HealthPercent < 50),
                        // Most (if not all) will leave this at 90. Its lower prio, high HPM, low HPS
                        Spell.Heal("Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).HealthPercent < 60),


                        // CH/HR only in parties/raids
                        new Decorator(
                            ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                            new PrioritySelector(
                                // This seems a bit tricky, but its really not. This is just how we cache a somewhat expensive lookup.
                                // Set the context to the "best unit" for the cluster, so we don't have to do that check twice.
                                // Then just use the context when passing the unit to throw the heal on, and the target of the heal from the cluster count.
                                // Also ensure it will jump at least 2 times. (CH is pointless to cast if it won't heal 3 people)
                                new PrioritySelector(
                                    context => Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, 12f),
                                    Spell.Heal(
                                        "Chain Heal", ret => (WoWPlayer)ret,
                                        ret => Clusters.GetClusterCount((WoWPlayer)ret, ChainHealPlayers, ClusterType.Chained, 12f) >= 3)),

                                // Now we're going to do the same thing as above, but this time we're going to do it with healing rain.
                                new PrioritySelector(
                                    context => Clusters.GetBestUnitForCluster(Unit.NearbyFriendlyPlayers.Cast<WoWUnit>(), ClusterType.Radius, 10f),
                                    Spell.CastOnGround(
                                        "Healing Rain", ret => ((WoWPlayer)ret).Location,
                                        ret =>
                                        Clusters.GetClusterCount((WoWPlayer)ret, Unit.NearbyFriendlyPlayers.Cast<WoWUnit>(), ClusterType.Radius, 10f) >
                                        // If we're in a raid, check for 4 players. If we're just in a party, check for 3.
                                        (StyxWoW.Me.GroupInfo.IsInRaid ? 3 : 2))))),

                        new Decorator(
                            ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid),
                            new PrioritySelector(
                                Movement.CreateMoveToLosBehavior(),
                                Movement.CreateFaceTargetBehavior(),
                                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                                Spell.Cast("Earth Shock"),
                                Spell.Cast("Lightning Bolt"),
                                Movement.CreateMoveToTargetBehavior(true, 35f)
                                )),

                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToTargetBehavior(true, 38f, ret => (WoWUnit)ret))

                )));
#else
            List<HealSpell> spells = new List<HealSpell>();

            AddHealSpell( spells, SingularSettings.Instance.Shaman.HealAncestralSwiftness, "Ancestral Swiftness",
                new Sequence(
                    Spell.BuffSelf("Ancestral Swiftness", ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Shaman.HealAncestralSwiftness),
                    Spell.Heal("Greater Healing Wave", ret => (WoWUnit)ret)
                    ));

            AddHealSpell(spells, SingularSettings.Instance.Shaman.HealUnleashElements , "Unleash Elements", 
                Spell.Heal("Unleash Elements", 
                    ret => (WoWUnit)ret, 
                    ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Shaman.HealUnleashElements
                        && Common.IsImbuedForHealing(Me.Inventory.Equipped.MainHand) 
                        && !Me.HasAura("Ancestral Swiftness")));

            AddHealSpell(spells, SingularSettings.Instance.Shaman.HealGreaterHealingWave , "Greater Healing Wave",
                Spell.Heal("Greater Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Shaman.HealGreaterHealingWave));

            AddHealSpell(spells, SingularSettings.Instance.Shaman.HealHealingWave , "Healing Wave", 
                Spell.Heal("Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Shaman.HealHealingWave));

            AddHealSpell(spells, SingularSettings.Instance.Shaman.HealHealingSurge , "Healing Surge", 
                Spell.Heal("Healing Surge", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.Shaman.HealHealingSurge));

            AddHealSpell(spells, SingularSettings.Instance.Shaman.HealChainHeal , "Chain Heal", 
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

            AddHealSpell(spells, SingularSettings.Instance.Shaman.HealHealingRain , "Healing Rain",
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

            AddHealSpell(spells, SingularSettings.Instance.Shaman.HealSpiritLinkTotem , "Spirit Link Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Heal(
                        "Spirit Link Totem", ret => (WoWPlayer)ret,
                        ret => Unit.NearbyFriendlyPlayers.Count(
                            p => p.HealthPercent < SingularSettings.Instance.Shaman.HealSpiritLinkTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.SpiritLink)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );

            AddHealSpell(spells, SingularSettings.Instance.Shaman.HealHealingTideTotem , "Healing Tide Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.BuffSelf(
                        "Healing Tide Totem",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.HealthPercent < SingularSettings.Instance.Shaman.HealHealingTideTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );

            AddHealSpell(spells, SingularSettings.Instance.Shaman.HealHealingStreamTotem , "Healing Stream Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.BuffSelf(
                        "Healing Stream Totem",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.HealthPercent < SingularSettings.Instance.Shaman.HealHealingStreamTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                            && !Totems.Exist( WoWTotemType.Water)
                        )
                    )
                );

            AddHealSpell(spells, SingularSettings.Instance.Shaman.HealAscendance , "Ascendance",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.BuffSelf(
                        "Healing Stream Totem",
                        ret => Unit.NearbyFriendlyPlayers.Count(p => p.HealthPercent < SingularSettings.Instance.Shaman.HealAscendance) >= (Me.GroupInfo.IsInRaid ? 3 : 2)
                        )
                    )
                );


            spells = spells.OrderBy(hs => hs.Pct).ToList();
            ListHealSpells(spells);

            return new PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                new Decorator(
                    ret => ret != null
                        && ((WoWUnit)ret).HealthPercent < SingularSettings.Instance.IgnoreHealTargetsAboveHealth
                        && !(Me.Mounted && SingularSettings.Instance.DisableAllMovement )
                        && (moveInRange || ((WoWUnit)ret).InLineOfSpellSight && ((WoWUnit)ret).DistanceSqr < 40 * 40),
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        new Decorator( 
                            ret => !Common.InGCD,
                            new PrioritySelector(

                                new Decorator(
                                    ret => moveInRange,
                                    Movement.CreateMoveToLosBehavior(ret => (WoWUnit)ret)),

                                Totems.CreateTotemsBehavior(),

                                Spell.Heal("Earth Shield",
                                    ret => (WoWUnit)ret,
                                    ret => ret is WoWPlayer && Group.Tanks.Contains((WoWPlayer)ret) && Group.Tanks.All(t => !t.HasMyAura("Earth Shield"))),

                                // Just pop RT on CD. Plain and simple. Calling GetBestRiptideTarget will see if we can spread RTs (T12 2pc)
                                Spell.Heal("Riptide",
                                    ret => GetBestRiptideTarget((WoWPlayer)ret),
                                    ret => !Me.HasAnyAura("Tidal Waves", "Ancestral Swiftness")
                                        && (((WoWPlayer)ret).HealthPercent > 15 || Spell.GetSpellCooldown("Ancestral Swiftness").TotalMinutes > 0f) // use Ancestral Swiftness value to check
                                    ),

                                new PrioritySelector(spells.Select(hs => hs.behavior).ToArray()),

                                Spell.Heal("Riptide",
                                    ret => GetBestRiptideTarget((WoWPlayer)ret),
                                    ret => !Me.HasAura("Ancestral Swiftness")),

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
                                )
                            ),

                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToTargetBehavior(true, 38f, ret => (WoWUnit)ret))

                        )
                    )
                );

#endif 
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
                return Unit.NearbyFriendlyPlayers.Where(u => u.HealthPercent < SingularSettings.Instance.Shaman.HealChainHeal).Select(u => (WoWUnit)u);
            }
        }

        private static WoWPlayer GetBestRiptideTarget(WoWPlayer originalTarget)
        {
            if (!originalTarget.HasMyAura("Riptide") && originalTarget.InLineOfSpellSight)
                return originalTarget;

            // Target already has RT. So lets find someone else to throw it on. Lowest health first preferably.
            return Unit.NearbyFriendlyPlayers.Where(u => !u.HasMyAura("Riptide") && u.InLineOfSpellSight ).OrderBy(u => u.HealthPercent).FirstOrDefault();
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

        private static DateTime LastListCall = DateTime.Now;

        private static void ListHealSpells(List<HealSpell> spells)
        {
            if ((DateTime.Now - LastListCall).TotalMilliseconds > 1000)
            {
                foreach (HealSpell hs in spells)
                {
                    Logger.WriteDebug(Color.LightGreen, "   Heal @ {0}% using [{1}]", hs.Pct, hs.SpellName);
                }

                LastListCall = DateTime.Now;
            }
        }
    }
}
