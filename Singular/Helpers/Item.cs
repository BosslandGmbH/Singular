using System;
using System.Linq;
using Styx;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using System.Collections.Generic;
using Action = TreeSharp.Action;

namespace Singular.Helpers
{
    internal static class Item
    {
        public static bool HasWeapoinImbue(WoWInventorySlot slot, string imbueName)
        {
            //Logger.Write("Checking Weapon Imbue on " + slot + " for " + imbueName);
            var item = StyxWoW.Me.Inventory.Equipped.GetEquippedItem(slot);
            if (item == null)
            {
                //Logger.Write("We have no " + slot + " equipped!");
                return false;
            }

            var enchant = item.TemporaryEnchantment;
            //if (enchant != null)
            //    Logger.Write("Enchant: " + enchant.Name + " - " + (enchant.Name == imbueName));
            return enchant != null && enchant.Name == imbueName;
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
                    ctx => ctx != null && CanUseItem((WoWItem)ctx),
                    new Action(ctx => UseItem((WoWItem)ctx))));

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

        private static void UseItem(WoWItem item)
        {
            Logger.Write("Using item: " + item.Name);
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
                        ctx => FindFirstUsableItemBySpell("Healthstone", "Healing Potion"),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                new Action(ret => StyxWoW.SleepForLagDuration())))
                        )),
                new Decorator(
                    ret => StyxWoW.Me.ManaPercent < manaPercent,
                    new PrioritySelector(
                        ctx => FindFirstUsableItemBySpell("Restore Mana"),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                new Action(ret => StyxWoW.SleepForLagDuration())))))
                );
        }
    }
}
