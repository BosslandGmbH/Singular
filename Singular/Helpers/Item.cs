using System.Linq;
using Styx;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Singular.Helpers
{
    public static class Item
    {
        public static Composite UseEquippedItem(uint slot)
        {
            return new PrioritySelector(
                ctx => StyxWoW.Me.Inventory.GetItemBySlot(slot),
                new Decorator(
                    ctx => ctx != null && CanUseItem((WoWItem)ctx),
                    new Action(ctx => UseItem((WoWItem)ctx))));

        }

        public static Composite UseItem(uint id)
        {
            return new PrioritySelector(
                ctx => StyxWoW.Me.Inventory.Items.FirstOrDefault(item => item.Entry == id),
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

    }
}
