using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Inventory;
using TreeSharp;

namespace Singular.ClassSpecific
{
    public static class Generic
    {
        [Spec(TalentSpec.Any)]
        [Behavior(BehaviorType.All)]
        [Class(WoWClass.None)]
        [Priority(999)]
        [Context(WoWContext.All)]
        [IgnoreBehaviorCount(BehaviorType.Combat)]
        public static Composite CreateFlasksBehaviour()
        {
            return new Decorator(
                ret => SingularSettings.Instance.UseFlasks,
                new PrioritySelector(
                    Item.UseItem(58149),
                    Item.UseItem(47499)));
        }

        [Spec(TalentSpec.Any)]
        [Behavior(BehaviorType.All)]
        [Class(WoWClass.None)]
        [Priority(999)]
        [Context(WoWContext.All)]
        [IgnoreBehaviorCount(BehaviorType.Combat)]
        public static Composite CreateTrinketBehaviour()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => SingularSettings.Instance.Trinket1,
                    Item.UseEquippedItem((uint)InventorySlot.Trinket0Slot)),
                new Decorator(
                    ret => SingularSettings.Instance.Trinket2,
                    Item.UseEquippedItem((uint)InventorySlot.Trinket1Slot)));
        }
    }
}
