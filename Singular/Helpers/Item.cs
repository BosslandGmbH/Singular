using System;
using System.Linq;

using CommonBehaviors.Actions;

using Singular.Settings;

using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System.Collections.Generic;
using Action = Styx.TreeSharp.Action;
using Styx.Helpers;
using Styx.Common;
using Styx.CommonBot;
using System.Drawing;
using System.Text.RegularExpressions;
using Singular.Managers;

namespace Singular.Helpers
{
    internal static class Item
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } } 

        public static bool HasItem(uint itemId)
        {
            WoWItem item = FindItem(itemId);
            return item != null;
        }

        public static WoWItem FindItem(uint itemId)
        {
            return StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == itemId);
        }

        public static bool HasWeaponImbue(WoWInventorySlot slot, string imbueName, int imbueId)
        {
            Logger.Write("Checking Weapon Imbue on " + slot + " for " + imbueName);
            var item = StyxWoW.Me.Inventory.Equipped.GetEquippedItem(slot);
            if (item == null)
            {
                Logger.Write("We have no " + slot + " equipped!");
                return true;
            }

            var enchant = item.TemporaryEnchantment;

            return enchant != null && (enchant.Name == imbueName || imbueId == enchant.Id);
        }


        /// <summary>
        ///  Creates a behavior to use an equipped item.
        /// </summary>
        /// <param name="slot"> The slot number of the equipped item. </param>
        /// <returns></returns>
        public static Composite UseEquippedItem(uint slot)
        {
            return new Throttle( TimeSpan.FromMilliseconds(250),
                new PrioritySelector(
                    ctx => StyxWoW.Me.Inventory.GetItemBySlot(slot),
                    new Decorator(
                        ctx => ctx != null && CanUseEquippedItem((WoWItem)ctx),
                        new Action(ctx => UseItem((WoWItem)ctx))
                        )
                    )
                );

        }

        public static Composite UseEquippedTrinket(TrinketUsage usage)
        {
            PrioritySelector ps = new PrioritySelector();

            if (SingularSettings.Instance.Trinket1Usage == usage)
            {
                ps.AddChild( UseEquippedItem( (uint) WoWInventorySlot.Trinket1 ));
            }

            if (SingularSettings.Instance.Trinket2Usage == usage)
            {
                ps.AddChild(UseEquippedItem((uint)WoWInventorySlot.Trinket2));
            }

            if (!ps.Children.Any())
                return new ActionAlwaysFail();

            return ps;
        }

        /// <summary>
        ///  Creates a behavior to use an item, in your bags or paperdoll.
        /// </summary>
        /// <param name="id"> The entry of the item to be used. </param>
        /// <returns></returns>
        public static Composite UseItem(uint id)
        {
            return new PrioritySelector(
                ctx => ObjectManager.GetObjectsOfType<WoWItem>().FirstOrDefault(item => item.Entry == id),
                new Decorator(
                    ctx => ctx != null && CanUseItem((WoWItem)ctx),
                    new Action(ctx => UseItem((WoWItem)ctx))));
        }

        private static bool CanUseItem(WoWItem item)
        {
            return item.Usable && item.Cooldown <= 0;
        }

        private static bool CanUseEquippedItem(WoWItem item)
        {
            // Check for engineering tinkers!
            string itemSpell = Lua.GetReturnVal<string>("return GetItemSpell(" + item.Entry + ")",0);
            if (string.IsNullOrEmpty(itemSpell))
                return false;

            return item.Usable && item.Cooldown <= 0;
        }

        public static WoWSpell GetItemSpell(WoWItem item)
        {
            string spellName = Lua.GetReturnVal<string>("return GetItemSpell(" + item.Entry + ")", 0);
            if (string.IsNullOrEmpty(spellName))
            {
                return null;
            }

            int spellId = Lua.GetReturnVal<int>("return GetSpellBookItemInfo('" + spellName + "')", 1);
            return WoWSpell.FromId(spellId);
        }

        private static void UseItem(WoWItem item)
        {
            Logger.Write( LogColor.Hilite, "/use {0}", item.Name);
            item.Use();
        }

        /// <summary>
        ///  Checks for items in the bag, and returns the first item that has an usable spell from the specified string array.
        /// </summary>
        /// <param name="spellNames"> Array of spell names to be check.</param>
        /// <returns></returns>
        public static WoWItem FindFirstUsableItemBySpell(params string[] spellNames)
        {
            List<WoWItem> carried = StyxWoW.Me.CarriedItems;
            // Yes, this is a bit of a hack. But the cost of creating an object each call, is negated by the speed of the Contains from a hash set.
            // So take your optimization bitching elsewhere.
            var spellNameHashes = new HashSet<string>(spellNames);

            return (from i in carried
                    let spells = i.Effects
                    where i.ItemInfo != null && spells != null && spells.Count != 0 &&
                          i.Usable &&
                          i.Cooldown == 0 &&
                          i.ItemInfo.RequiredLevel <= StyxWoW.Me.Level &&
                          spells.Any(s => s.Spell != null && spellNameHashes.Contains(s.Spell.Name))
                    orderby i.ItemInfo.Level descending
                    select i).FirstOrDefault();
        }

        /// <summary>
        ///  Returns true if you have a wand equipped, false otherwise.
        /// </summary>
        public static bool HasWand
        {
            get
            {
                return StyxWoW.Me.Inventory.Equipped.Ranged != null &&
                       StyxWoW.Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Wand;
            }
        }

        /// <summary>
        ///   Creates a composite to use potions and healthstone.
        /// </summary>
        /// <param name = "healthPercent">Healthpercent to use health potions and healthstone</param>
        /// <param name = "manaPercent">Manapercent to use mana potions</param>
        /// <returns></returns>
        public static Composite CreateUsePotionAndHealthstone(double healthPercent, double manaPercent)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => StyxWoW.Me.HealthPercent < healthPercent,
                    new PrioritySelector(
                        ctx => FindFirstUsableItemBySpell("Healthstone", "Healing Potion", "Life Spirit"),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logger.Write(LogColor.SpellHeal, "/use {0} @ {1:F1}% Health", ((WoWItem)ret).Name, StyxWoW.Me.HealthPercent )),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                Helpers.Common.CreateWaitForLagDuration()))
                        )),
                new Decorator(
                    ret => Me.PowerType == WoWPowerType.Mana && StyxWoW.Me.ManaPercent < manaPercent,
                    new PrioritySelector(
                        ctx => FindFirstUsableItemBySpell("Restore Mana", "Water Spirit"),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logger.Write(LogColor.Hilite, "/use {0} @ {1:F1}% Mana", ((WoWItem)ret).Name, StyxWoW.Me.ManaPercent )),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                Helpers.Common.CreateWaitForLagDuration()))))
                );
        }

        /// <summary>
        /// use Alchemist's Flask if no flask buff active. do over optimize since this is a precombatbuff behavior
        /// </summary>
        /// <returns></returns>

        public static Composite CreateUseFlasksBehavior()
        {
            if (!SingularSettings.Instance.UseAlchemyFlasks || Me.Level < 85)
                return new ActionAlwaysFail();

            return new ThrottlePasses(
                1,
                TimeSpan.FromSeconds(5),
                RunStatus.Failure,
                new Decorator(
                    req => !StyxWoW.Me.Auras
                        .Any(aura => 
                            !aura.Key.StartsWith("Flask of ")
                            && !aura.Key.StartsWith("Enhanced ")
                            && !flaskAura.Contains( aura.Value.SpellId )
                            ), 
                    new PrioritySelector(
                        // save highest priority WoWItem to use
                        ctx => flaskItem
                            .Select( f => StyxWoW.Me.CarriedItems
                                .FirstOrDefault( 
                                    i => f == i.Entry
                                        && (i.ItemInfo.RequiredSkillId == 0 || Me.GetSkill(i.ItemInfo.RequiredSkillId).CurrentValue >= i.ItemInfo.RequiredSkillLevel)
                                    )
                                )
                            .FirstOrDefault(),
                        new Decorator(
                            req => req != null && CanUseItem((WoWItem)req),
                            new Sequence(
                                new Action(r => Logger.Write(LogColor.Hilite, "/use flask: {0}", ((WoWItem)r).Name)),
                                new Action(r => ((WoWItem)r).UseContainerItem()),
                                new PrioritySelector(
                                    new DynaWait( 
                                        ts => TimeSpan.FromMilliseconds(Math.Max(500, SingularRoutine.Latency * 2)),
                                        until => StyxWoW.Me.Auras
                                            .Any(aura => 
                                                aura.Key.StartsWith("Flask of ")
                                                || aura.Key.StartsWith("Enhanced ")
                                                || flaskAura.Contains( aura.Value.SpellId )
                                                ),
                                        new ActionAlwaysSucceed()
                                        ),
                                    new Action( r => {
                                        Logger.WriteDiagnostic("UseFlasks: do not see an aura from item [{0}]");
                                        return RunStatus.Failure;
                                        })
                                    )
                                )
                            )
                        )
                    )
                );
        }

        /// <summary>
        /// list of items which provide Flask like buffs.  must appear in 
        /// order of priority to use
        /// </summary>
        private static List<uint> flaskItem = new List<uint>()
        {
            118922,     // Oralius' Whispering Crystal (item)
            86569,      // Crystal of Insanity (item)
            75525,      // Alchemist's Flask (item)
        };

        /// <summary>
        /// do use a flaskItem if one of these auras is present
        /// note: do not list flasks here
        /// </summary>
        private static HashSet<int> flaskAura = new HashSet<int>()
        {
            176151,     // Whispers of Insanity (aura)
            127230,     // Visions of Insanity (aura)
            105617,     // Alchemist's Flask (aura)
        };

        private static DateTime suppressScrollsUntil = DateTime.MinValue;

        public static Composite CreateUseScrollsBehavior()
        {
            if (!SingularSettings.Instance.UseScrolls)
                return new PrioritySelector();

            return new Decorator(
                req => IsScrollNeeded(),
                CreateUseBestScroll()
                );
        }

        private static bool IsScrollNeeded()
        {
            if (suppressScrollsUntil > DateTime.Now)
                return false;

            if (Me.Auras.Any(a => a.Value.ApplyAuraType == WoWApplyAuraType.ModStat))
                return false;

            return true;
        }

        public struct ScrollContext
        {
            public WoWItem scroll;
            public DateTime usedAt;
        }

        private static Composite CreateUseBestScroll()
        {
            return new Sequence(
                ctx =>
                {
                    ScrollContext sc = new ScrollContext();
                    sc.scroll = FindBestScroll();
                    return sc;
                },

                new Decorator(
                    req => req != null && ((ScrollContext)req).scroll != null,
                    new Action(r => Logger.WriteDebug("UseBestScroll: will attempt to use {0} #{1}", ((ScrollContext)r).scroll.Name, ((ScrollContext)r).scroll.Entry))
                    ),

                new Action(r =>
                    {
                        ScrollContext sc = (ScrollContext)r;
                        sc.usedAt = DateTime.Now;
                        UseItem(sc.scroll);
                    }),

                new WaitContinue(
                    TimeSpan.FromMilliseconds(250),
                    until => Utilities.EventHandlers.LastRedErrorMessage > ((ScrollContext) until).usedAt,
                    new Action(r => {
                        int suppressFor = 5;
                        suppressScrollsUntil = DateTime.Now.AddMinutes(suppressFor);
                        Logger.WriteDebug("UseBestScroll: suppressing Scroll Use for {0} minutes due to WoWRedError encountered", suppressFor);
                        return RunStatus.Failure;
                        })
                    )
                );
        }

        private static WoWItem FindBestScroll()
        {
            Styx.StatType primary = Me.GetPrimaryStat();
            WoWItem scroll = FindFirstUsableItemBySpell(primary.ToString());
            if (scroll == null)
                scroll = FindFirstUsableItemBySpell("Stamina");
            if (scroll == null && primary == StatType.Intellect)
                scroll = FindFirstUsableItemBySpell("Spirit");
            return scroll;
        }

        // 
        // see Generic.cs for trinket support
        //public static Composite CreateUseTrinketsBehavior()
        //{
        //    return new PrioritySelector(
        //        new Decorator(
        //            ret => SingularSettings.Instance.Trinket1,
        //            new Decorator(
        //                ret => UseTrinket(true),
        //                new ActionAlwaysSucceed())),
        //        new Decorator(
        //            ret => SingularSettings.Instance.Trinket2,
        //            new Decorator(
        //                ret => UseTrinket(false),
        //                new ActionAlwaysSucceed()))
        //        );
        //}

        public static bool RangedIsType(WoWItemWeaponClass wepType)
        {
            var ranged = StyxWoW.Me.Inventory.Equipped.Ranged;
            if (ranged != null && ranged.IsValid)
            {
                return ranged.ItemInfo != null && ranged.ItemInfo.WeaponClass == wepType;
            }
            return false;
        }

        public static void WriteCharacterGearAndSetupInfo()
        {
            Logger.WriteFile("");
            if (SingularSettings.Debug)
            {
                uint totalItemLevel;
                SecondaryStats ss;          //create within frame (does series of LUA calls)

                using (StyxWoW.Memory.AcquireFrame())
                {
                    totalItemLevel = CalcTotalGearScore();
                    ss = new SecondaryStats();
                }

                Logger.WriteFile("Equipped Total Item Level  : {0}", totalItemLevel);
                Logger.WriteFile("Equipped Average Item Level: {0}", totalItemLevel / 16);
                Logger.WriteFile("");
                Logger.WriteFile("Health:      {0}", Me.MaxHealth);
                Logger.WriteFile("Strength:    {0}", Me.Strength);
                Logger.WriteFile("Agility:     {0}", Me.Agility);
                Logger.WriteFile("Intellect:   {0}", Me.Intellect);
                Logger.WriteFile("Spirit:      {0}", Me.Spirit);
                Logger.WriteFile("");
                Logger.WriteFile("Hit(M/R):    {0}/{1}", ss.MeleeHit, ss.SpellHit);
                Logger.WriteFile("Expertise:   {0}", ss.Expertise);
                Logger.WriteFile("Mastery:     {0}", (int)ss.Mastery);
                Logger.WriteFile("Crit:        {0:F2}", ss.Crit);
                Logger.WriteFile("Haste(M/R):  {0}/{1}", ss.MeleeHaste, ss.SpellHaste);
                Logger.WriteFile("SpellPen:    {0}", ss.SpellPen);
                Logger.WriteFile("PvP Resil:   {0}", ss.Resilience);
                Logger.WriteFile("");
                Logger.WriteFile("PrimaryStat: {0}", Me.GetPrimaryStat() );
                Logger.WriteFile("");
            }

            Logger.WriteFile("Talents Selected: {0}", Singular.Managers.TalentManager.Talents.Count(t => t.Selected));
            foreach (var t in Singular.Managers.TalentManager.Talents)
            {
                if (!t.Selected)
                    continue;

                string talent = "unknown";
                switch (Me.Class)
                {
                    case WoWClass.DeathKnight:
                        talent = ((ClassSpecific.DeathKnight.DeathKnightTalents)t.Index).ToString();
                        break;
                    case WoWClass.Druid:
                        talent = ((ClassSpecific.Druid.DruidTalents)t.Index).ToString();
                        break;
                    case WoWClass.Hunter:
                        talent = ((ClassSpecific.Hunter.HunterTalents)t.Index).ToString();
                        break;
                    case WoWClass.Mage:
                        talent = ((ClassSpecific.Mage.MageTalents)t.Index).ToString();
                        break;
                    case WoWClass.Monk:
                        talent = ((ClassSpecific.Monk.MonkTalents)t.Index).ToString();
                        break;
                    case WoWClass.Paladin:
                        talent = ((ClassSpecific.Paladin.PaladinTalents)t.Index).ToString();
                        break;
                    case WoWClass.Priest:
                        talent = ((ClassSpecific.Priest.PriestTalents)t.Index).ToString();
                        break;
                    case WoWClass.Rogue:
                        talent = ((ClassSpecific.Rogue.RogueTalents)t.Index).ToString();
                        break;
                    case WoWClass.Shaman:
                        talent = ((ClassSpecific.Shaman.ShamanTalents)t.Index).ToString();
                        break;
                    case WoWClass.Warlock:
                        talent = ((ClassSpecific.Warlock.WarlockTalents)t.Index).ToString();
                        break;
                    case WoWClass.Warrior:
                        talent = ((ClassSpecific.Warrior.WarriorTalents)t.Index).ToString();
                        break;
                }

                Logger.WriteFile("--- #{0} -{1}", t.Index, talent.CamelToSpaced());
            }

            Logger.WriteFile(" ");
            Logger.WriteFile("Glyphs Equipped: {0}", Singular.Managers.TalentManager.Glyphs.Count());
            foreach (string glyphName in Singular.Managers.TalentManager.Glyphs.OrderBy(g => g).Select(g => g).ToList())
            {
                Logger.WriteFile("--- {0}", glyphName );
            }

            Logger.WriteFile("");

            Regex pat = new Regex( "Item \\-" + Me.Class.ToString().CamelToSpaced() + " .*P Bonus");
            if ( Me.GetAllAuras().Any( a => pat.IsMatch( a.Name )))
            {
                foreach( var a in Me.GetAllAuras())
                {
                    if ( pat.IsMatch( a.Name ))
                    {
                        Logger.WriteFile( "  Tier Bonus Aura:  {0}", a.Name );
                    }
                }

                Logger.WriteFile("");
            }

            if (Me.Inventory.Equipped.Trinket1 != null)
                Logger.WriteFile("Trinket1: {0} #{1}", Me.Inventory.Equipped.Trinket1.Name, Me.Inventory.Equipped.Trinket1.Entry);

            if (Me.Inventory.Equipped.Trinket2 != null)
                Logger.WriteFile("Trinket2: {0} #{1}", Me.Inventory.Equipped.Trinket2.Name, Me.Inventory.Equipped.Trinket2.Entry);

            if (Me.Inventory.Equipped.Hands != null)
            {
                WoWItem item = Me.Inventory.Equipped.Hands;
                if (!item.Usable)
                    Logger.WriteDiagnostic("Hands: {0} #{1} - are not usable and will be ignored", item.Name, item.Entry);
                else 
                {
                    string itemSpell = Lua.GetReturnVal<string>("return GetItemSpell(" + item.Entry + ")",0);       
                    if (string.IsNullOrEmpty(itemSpell))
                        Logger.WriteDiagnostic("Hands: {0} #{1} - does not appear to have a usable enchant present and will be ignored", item.Name, item.Entry);
                    else
                        Logger.WriteFile("Hands: {0} #{1} - found [{2}] and will use as per user settings", item.Name, item.Entry, itemSpell);
                }

                /*
                // debug logic:  try another method to check for Engineering Tinkers
                foreach (var enchName in GloveEnchants)
                {
                    WoWItem.WoWItemEnchantment ench = item.GetEnchantment(enchName);
                    if (ench != null)
                        Logger.WriteFile("Hands (double check): {0} #{1} - found enchant [{2}] #{3} (debug info only)", item.Name, item.Entry, ench.Name, ench.Id);
                }
                */

                item = Me.Inventory.Equipped.Waist;
                if (item != null)
                {
                    foreach (var enchName in BeltEnchants)
                    {
                        WoWItem.WoWItemEnchantment ench = item.GetEnchantment(enchName);
                        if (ench != null)
                            Logger.WriteFile("Belt (double check): {0} #{1} - found enchant [{2}] #{3} (debug info only)", item.Name, item.Entry, ench.Name, ench.Id);
                    }
                }
            }
        }

        /*
        // should be an api to inspect gloves, but instead yal (yet another list)
        internal static List<string> GloveEnchants = new List<string>() 
        {
            "Hyperspeed Accelerators",
            "Hand-Mounted Pyro Rocket",
            "Tazik Shocker",
            "Synapse Springs",
            "Phase Fingers",
            "Incendiary Fireworks Launcher"
        };
        */

        internal static List<string> BeltEnchants = new List<string>() 
        {
            "Nitro Boosts",
            "Frag Belt"
        };

        public static uint CalcTotalGearScore()
        {
            uint totalItemLevel = 0;
            for (uint slot = 0; slot < Me.Inventory.Equipped.Slots; slot++)
            {
                WoWItem item = Me.Inventory.Equipped.GetItemBySlot(slot);
                if (item != null && IsItemImportantToGearScore(item))
                {
                    uint itemLvl = GetGearScore(item);
                    totalItemLevel += itemLvl;
                    // Logger.WriteFile("  good:  item[{0}]: {1}  [{2}]", slot, itemLvl, item.Name);
                }
            }

            // double main hand score if have a 2H equipped
            if (GetInventoryType(Me.Inventory.Equipped.MainHand) == InventoryType.TwoHandWeapon)
                totalItemLevel += GetGearScore(Me.Inventory.Equipped.MainHand);

            return totalItemLevel;
        }

        private static uint GetGearScore(WoWItem item)
        {
            uint iLvl = 0;
            try
            {
                if (item != null)
                    iLvl = (uint)item.ItemInfo.Level;
            }
            catch
            {
                Logger.WriteDebug("GearScore: ItemInfo not available for [0] #{1}", item.Name, item.Entry );
            }

            return iLvl;
        }

        private static InventoryType GetInventoryType(WoWItem item)
        {
            InventoryType typ = Styx.InventoryType.None;
            try
            {
                if (item != null)
                    typ = item.ItemInfo.InventoryType;
            }
            catch
            {
                Logger.WriteDebug("InventoryType: ItemInfo not available for [0] #{1}", item.Name, item.Entry);
            }

            return typ;
        }

        private static bool IsItemImportantToGearScore(WoWItem item)
        {
            if (item != null && item.ItemInfo != null)
            {
                switch (item.ItemInfo.InventoryType)
                {
                    case InventoryType.Head:
                    case InventoryType.Neck:
                    case InventoryType.Shoulder:
                    case InventoryType.Cloak:
                    case InventoryType.Body:
                    case InventoryType.Chest:
                    case InventoryType.Robe:
                    case InventoryType.Wrist:
                    case InventoryType.Hand:
                    case InventoryType.Waist:
                    case InventoryType.Legs:
                    case InventoryType.Feet:
                    case InventoryType.Finger:
                    case InventoryType.Trinket:
                    case InventoryType.Relic:
                    case InventoryType.Ranged:
                    case InventoryType.Thrown:

                    case InventoryType.Holdable:
                    case InventoryType.Shield:
                    case InventoryType.TwoHandWeapon:
                    case InventoryType.Weapon:
                    case InventoryType.WeaponMainHand:
                    case InventoryType.WeaponOffHand:
                        return true;
                }
            }

            return false;
        }

        // public static bool Tier14TwoPieceBonus { get { return NumItemSetPieces(1144) >= 2; } }
        // public static bool Tier14FourPieceBonus { get { return NumItemSetPieces(1144) >= 4; } }

        private static int NumItemSetPieces( int setId)
        {
            // return StyxWoW.Me.CarriedItems.Count(i => i.ItemInfo.ItemSetId == setId);
            return Me.Inventory.Equipped.Items.Count(i => i != null && i.ItemInfo.ItemSetId == setId);
        }


        class SecondaryStats
        {
            public float MeleeHit { get; set; }
            public float SpellHit { get; set; }
            public float Expertise { get; set; }
            public float MeleeHaste { get; set; }
            public float SpellHaste { get; set; }
            public float SpellPen { get; set; }
            public float Mastery { get; set; }
            public float Crit { get; set; }
            public float Resilience { get; set; }

            public SecondaryStats()
            {
                Refresh();
            }

            public void Refresh()
            {
                MeleeHit = Lua.GetReturnVal<float>("return GetCombatRating(CR_HIT_MELEE)", 0);
                SpellHit = Lua.GetReturnVal<float>("return GetCombatRating(CR_HIT_SPELL)", 0);
                Expertise = Lua.GetReturnVal<float>("return GetCombatRating(CR_EXPERTISE)", 0);
                MeleeHaste = Lua.GetReturnVal<float>("return GetCombatRating(CR_HASTE_MELEE)", 0);
                SpellHaste = Lua.GetReturnVal<float>("return GetCombatRating(CR_HASTE_SPELL)", 0);
                SpellPen = Lua.GetReturnVal<float>("return GetSpellPenetration()", 0);
                Mastery = Lua.GetReturnVal<float>("return GetCombatRating(CR_MASTERY)", 0);
                Crit = Lua.GetReturnVal<float>("return GetCritChance()", 0);               
                Resilience = Lua.GetReturnVal<float>("return GetCombatRating(COMBAT_RATING_RESILIENCE_CRIT_TAKEN)", 0);
            }

        }

        private static WoWItem bandage = null;

        public static Composite CreateUseBandageBehavior()
        {
            return new Decorator(

                ret => SingularSettings.Instance.UseBandages && Me.PredictedHealthPercent(includeMyHeals: true) < 95 && SpellManager.HasSpell("First Aid") && !Me.HasAura("Recently Bandaged") && !Me.ActiveAuras.Any(a => a.Value.IsHarmful),

                new PrioritySelector(

                    new Action(ret =>
                    {
                        bandage = FindBestBandage();
                        return RunStatus.Failure;
                    }),

                    new Decorator(
                        ret => bandage != null && !Me.IsMoving,

                        new Sequence(
                            new Action(ret =>
                            {
                                Logger.Write(LogColor.Hilite, "/use {0} @ {1:F1}%", bandage.Name, Me.HealthPercent);
                                bandage.Use();
                            }),
                            new WaitContinue(new TimeSpan(0, 0, 0, 0, 750), ret => Me.IsCasting || Me.IsChanneling, new ActionAlwaysSucceed()),
                            new WaitContinue(8, ret => (!Me.IsCasting && !Me.IsChanneling) || Me.HealthPercent > 99, new ActionAlwaysSucceed()),
                            new DecoratorContinue(
                                ret => Me.IsCasting || Me.IsChanneling,
                                new Sequence(
                                    new Action(r => Logger.Write(LogColor.Cancel, "/cancel First Aid @ {0:F0}%", Me.HealthPercent)),
                                    new Action(r => SpellManager.StopCasting())
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static bool HasBandage()
        {
            return null != FindBestBandage();
        }

        public static WoWItem FindBestBandage()
        {
            return Me.CarriedItems
                .Where(b => b.ItemInfo.ItemClass == WoWItemClass.Consumable
                    && b.ItemInfo.ConsumableClass == WoWItemConsumableClass.Bandage
                    && (b.ItemInfo.RequiredSkillId == 0 || Me.GetSkill(b.ItemInfo.RequiredSkillId).CurrentValue >= b.ItemInfo.RequiredSkillLevel)
                    && b.ItemInfo.RequiredLevel <= Me.Level
                    && CanUseItem(b))
                .OrderBy(b => b.ItemInfo.Level)
                .ThenByDescending(b => b.ItemInfo.RequiredSkillLevel)
                .FirstOrDefault();
        }

        public static Composite CreateThunderLordGrappleBehavior()
        {
            const int FROSTFIRE_RIDGE = 6720;
            const int THUNDERLORD_GRAPPLE = 101677;

            if (!SingularSettings.Instance.ToysAllowUse)
                return new ActionAlwaysFail();

            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return new ActionAlwaysFail();

            if (!Me.IsMelee())
                return new ActionAlwaysFail();

            return new Throttle(
                15,
                new Decorator(
                    req => Me.ZoneId == FROSTFIRE_RIDGE,
                    new Decorator(
                        req => MovementManager.IsClassMovementAllowed   // checks Movement and GapCloser capability flags
                            && CanUseCarriedItem(THUNDERLORD_GRAPPLE)
                            && Me.GotTarget()
                            && Me.CurrentTarget.Distance.Between(25, 40)
                            && Me.CurrentTarget.InLineOfSight
                            && Me.IsSafelyFacing(Me.CurrentTarget)
                            && (DateTime.Now - Utilities.EventHandlers.LastNoPathFailure) > TimeSpan.FromSeconds(15),
                        new Sequence(
                            new Action( r => StopMoving.Now()),
                            new Wait(
                                TimeSpan.FromMilliseconds(500),
                                until => !Me.IsMoving,
                                new ActionAlwaysSucceed()
                                ),
                            new Action(r => 
                            {
                                Logger.Write(LogColor.Hilite, "^Thunderlord Grapple on {0} @ {1:F1} yds", Me.CurrentTarget.SafeName(), Me.CurrentTarget.SpellDistance());
                                WoWItem item = FindItem(THUNDERLORD_GRAPPLE);
                                item.Use();
                            }),
                            new Wait(
                                1,
                                until => Spell.IsCastingOrChannelling(),
                                new ActionAlwaysSucceed()
                                ),
                            new Action( r => Logger.WriteDebug("ThunderlordGrapple: start @ {0:F1} yds", Me.CurrentTarget.Distance)),
                            new Wait(
                                3,
                                until => !Spell.IsCastingOrChannelling(),
                                new ActionAlwaysSucceed()
                                ),
                            new Wait(
                                1,
                                until => !Me.IsMoving || Me.CurrentTarget.IsWithinMeleeRange,
                                new ActionAlwaysSucceed()
                                ),
                            new Action( r => Logger.WriteDebug("ThunderlordGrapple: ended @ {0:F1} yds", Me.CurrentTarget.Distance))
                            )
                        )
                    )
                );
        }

        private static bool CanUseCarriedItem(int itemId)
        {
            WoWItem item = Me.CarriedItems
                .Where(b => b.Entry == itemId)
                .FirstOrDefault();           
            return item == null ? false : CanUseItem(item);
        }
    }
}
