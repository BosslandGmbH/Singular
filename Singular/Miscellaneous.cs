using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Singular.Settings;

using Styx;
using Styx.WoWInternals.WoWObjects;

namespace Singular
{
    public enum TrinketUsage
    {
        Never,
        OnCooldown,
        OnCooldownInCombat,
        LowPower,
        LowHealth
    }
    class Miscellaneous
    {
        public static bool UseTrinket(bool firstSlot)
        {
            var usage = firstSlot ? SingularSettings.Instance.FirstTrinketUsage : SingularSettings.Instance.SecondTrinketUsage;

            // If we're not going to use it, don't bother going any further. Save some performance here.
            if (usage == TrinketUsage.Never)
                return false;

            WoWItem item = firstSlot ? StyxWoW.Me.Inventory.Equipped.Trinket1 : StyxWoW.Me.Inventory.Equipped.Trinket2;
            var percent = firstSlot ? SingularSettings.Instance.FirstTrinketUseAtPercent : SingularSettings.Instance.SecondTrinketUseAtPercent;

            if (item == null)
                return false;

            // Its on cooldown, just ignore it.
            if (item.Cooldown > 0)
                return false;

            bool useIt = false;
            switch (usage)
            {
                case TrinketUsage.OnCooldown:
                    // We know its off cooldown... so just use it :P
                    useIt = true;
                    break;
                case TrinketUsage.OnCooldownInCombat:
                    if (StyxWoW.Me.Combat)
                        useIt = true;
                    break;
                case TrinketUsage.LowPower:
                    // We use the PowerPercent here, since it applies to ALL types of power. (Runic, Mana, Rage, Energy, Focus)
                    if (StyxWoW.Me.PowerPercent < percent)
                        useIt = true;
                    break;
                case TrinketUsage.LowHealth:
                    if (StyxWoW.Me.HealthPercent < percent)
                        useIt = true;
                    break;
            }

            if (useIt)
            {
                Logger.Write("Popping trinket " + item.Name);
                item.Use();
                return true;
            }
            return false;
        }

		public static bool UseEquippedItem(uint slotId)
		{
			var item = StyxWoW.Me.Inventory.GetItemBySlot(slotId);

			if (item == null)
				return false;

			if (item.Cooldown != 0)
				return false;

			if (!item.Usable)
				return false;

			return item.Use();
		}

        public static WoWItem FindFirstUsableItemBySpell(params string[] spellNames)
        {
            var carried = StyxWoW.Me.CarriedItems;
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
    }
}
