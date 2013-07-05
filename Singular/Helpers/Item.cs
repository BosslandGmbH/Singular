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

namespace Singular.Helpers
{
    internal static class Item
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } } 

        public static bool HasItem(uint itemId)
        {
            return StyxWoW.Me.CarriedItems.Any(i => i.Entry == itemId);
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
            return new PrioritySelector(
                ctx => StyxWoW.Me.Inventory.GetItemBySlot(slot),
                new Decorator(
                    ctx => ctx != null && CanUseEquippedItem((WoWItem)ctx),
                    new Action(ctx => UseItem((WoWItem)ctx))
                    )
                );

        }

        public static Composite UseEquippedTrinket(TrinketUsage usage)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => usage == SingularSettings.Instance.Trinket1Usage,
                    UseEquippedItem( (uint) WoWInventorySlot.Trinket1 )
                    ),
                new Decorator(
                    ret => usage == SingularSettings.Instance.Trinket2Usage,
                    UseEquippedItem( (uint) WoWInventorySlot.Trinket2 )
                    ),
                new Decorator(
                    ret => usage == SingularSettings.Instance.GloveUsage,
                    UseEquippedItem( (uint) WoWInventorySlot.Hands )
                    )
                );
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

        private static void UseItem(WoWItem item)
        {
            Logger.Write( Color.DodgerBlue, "Using item: " + item.Name);
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
                    let spells = i.ItemSpells
                    where i.ItemInfo != null && spells != null && spells.Count != 0 &&
                          i.Usable &&
                          i.Cooldown == 0 &&
                          i.ItemInfo.RequiredLevel <= StyxWoW.Me.Level &&
                          spells.Any(s => s.IsValid && s.ActualSpell != null && spellNameHashes.Contains(s.ActualSpell.Name))
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
                                new Action(ret => Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
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
                                new Action(ret => Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                Helpers.Common.CreateWaitForLagDuration()))))
                );
        }


        public static Composite CreateUseAlchemyBuffsBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret =>
                    SingularSettings.Instance.UseAlchemyFlasks && StyxWoW.Me.GetSkill(SkillLine.Alchemy).CurrentValue >= 400 &&
                    !StyxWoW.Me.Auras.Any(aura => aura.Key.StartsWith("Enhanced ") || aura.Key.StartsWith("Flask of ")), // don't try to use the flask if we already have or if we're using a better one
                    new PrioritySelector(
                        ctx => StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == 58149) ?? StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == 47499),
                // Flask of Enhancement / Flask of the North
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                Helpers.Common.CreateWaitForLagDuration(stopIf => StyxWoW.Me.Auras.Any(aura => aura.Key.StartsWith("Enhanced ") || aura.Key.StartsWith("Flask of ")))
                                )
                            )
                        )
                    )
                );
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
            if (!SingularSettings.Debug)
                return;

            uint totalItemLevel;
            SecondaryStats ss;          //create within frame (does series of LUA calls)

            using (StyxWoW.Memory.AcquireFrame())
            {
                totalItemLevel = CalcTotalGearScore();
                ss = new SecondaryStats();
            }

            Logger.WriteFile("");
            Logger.WriteFile("Equipped Total Item Level  : {0}", totalItemLevel);
            Logger.WriteFile("Equipped Average Item Level: {0}", totalItemLevel / 16);
            Logger.WriteFile("");
            Logger.WriteFile("Health:      {0}", Me.MaxHealth);
            Logger.WriteFile("Agility:     {0}", Me.Agility);
            Logger.WriteFile("Intellect:   {0}", Me.Intellect);
            Logger.WriteFile("Spirit:      {0}", Me.Spirit);
            Logger.WriteFile("");
            Logger.WriteFile("Hit(M/R):    {0}/{1}", ss.MeleeHit, ss.SpellHit);
            Logger.WriteFile("Expertise:   {0}", ss.Expertise);
            Logger.WriteFile("Mastery:     {0}", (int) ss.Mastery);
            Logger.WriteFile("Crit:        {0:F2}", ss.Crit);
            Logger.WriteFile("Haste(M/R):  {0}/{1}", ss.MeleeHaste, ss.SpellHaste);
            Logger.WriteFile("SpellPen:    {0}", ss.SpellPen);
            Logger.WriteFile("PvP Resil:   {0}", ss.Resilience);
            Logger.WriteFile("PvP Power:   {0}", ss.PvpPower);
            Logger.WriteFile("");

            Logger.WriteFile("Talents Selected: {0}", Singular.Managers.TalentManager.Talents.Count(t => t.Selected));
            foreach (var t in Singular.Managers.TalentManager.Talents)
            {
                if (!t.Selected)
                    continue;

                string talent = "";
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
        }

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
            public float PvpPower { get; set; }

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
                PvpPower = Lua.GetReturnVal<float>("return GetCombatRating(CR_PVP_POWER)", 0);
            }

        }

        private static WoWItem bandage = null;

        public static Composite CreateUseBandageBehavior()
        {
            return new Decorator( 

                ret => SingularSettings.Instance.UseBandages && Me.GetPredictedHealthPercent(true) < 95 && SpellManager.HasSpell( "First Aid") && !Me.HasAura( "Recently Bandaged") && !Me.ActiveAuras.Any( a => a.Value.IsHarmful ),

                new PrioritySelector(

                    new Action( ret => {
                        bandage = FindBestBandage();
                        return RunStatus.Failure;
                    }),

                    new Decorator(
                        ret => bandage != null && !Me.IsMoving,

                        new Sequence(
                            new Action(ret => UseItem(bandage)),
                            new WaitContinue( new TimeSpan(0,0,0,0,750), ret => Me.IsCasting || Me.IsChanneling, new ActionAlwaysSucceed()),
                            new WaitContinue(8, ret => (!Me.IsCasting && !Me.IsChanneling) || Me.HealthPercent > 99, new ActionAlwaysSucceed()),
                            new DecoratorContinue(
                                ret => Me.IsCasting || Me.IsChanneling,
                                new Sequence(
                                    new Action( r => Logger.Write( "/cancel First Aid @ {0:F0}%", Me.HealthPercent )),
                                    new Action( r => SpellManager.StopCasting() )
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
                    && b.ItemInfo.ContainerClass == WoWItemContainerClass.Bandage
                    && b.ItemInfo.RecipeClass == WoWItemRecipeClass.FirstAid
                    && Me.GetSkill(SkillLine.FirstAid).CurrentValue >= b.ItemInfo.RequiredSkillLevel
                    && CanUseItem(b))
                .OrderByDescending(b => b.ItemInfo.RequiredSkillLevel)
                .FirstOrDefault();
        }

    }
}
