using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx;
using Styx.WoWInternals.WoWObjects;
using Singular.Settings;

namespace Singular
{
    public enum PoisonType
    {
        Instant,
        Crippling,
        MindNumbing,
        Deadly,
        Wound
    }

    class Poisons
    {
        public static bool MainHandNeedsPoison
        {
            get
            {
                return StyxWoW.Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id == 0;
            }
        }

        public static bool OffHandNeedsPoison
        {
            get
            {
                return StyxWoW.Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id == 0;
            }
        }

        public static WoWItem MainHandPoison
        {
            get
            {
                switch (SingularSettings.Instance.Rogue.MHPoison)
                {
                    case PoisonType.Instant:
                        return StyxWoW.Me.CarriedItems.FirstOrDefault(i => InstantPoisons.Contains(i.Entry));
                    case PoisonType.Crippling:
                        return StyxWoW.Me.CarriedItems.FirstOrDefault(i => CripplingPoisons.Contains(i.Entry));
                    case PoisonType.MindNumbing:
                        return StyxWoW.Me.CarriedItems.FirstOrDefault(i => MindNumbingPoisons.Contains(i.Entry));
                    case PoisonType.Deadly:
                        return StyxWoW.Me.CarriedItems.FirstOrDefault(i => DeadlyPoisons.Contains(i.Entry));
                    case PoisonType.Wound:
                        return StyxWoW.Me.CarriedItems.FirstOrDefault(i => WoundPoisons.Contains(i.Entry));
                    default:
                        return null;
                }
            }
        }

        public static WoWItem OffHandPoison
        {
            get
            {
                switch (SingularSettings.Instance.Rogue.OHPoison)
                {
                    case PoisonType.Instant:
                        return StyxWoW.Me.CarriedItems.FirstOrDefault(i => InstantPoisons.Contains(i.Entry));
                    case PoisonType.Crippling:
                        return StyxWoW.Me.CarriedItems.FirstOrDefault(i => CripplingPoisons.Contains(i.Entry));
                    case PoisonType.MindNumbing:
                        return StyxWoW.Me.CarriedItems.FirstOrDefault(i => MindNumbingPoisons.Contains(i.Entry));
                    case PoisonType.Deadly:
                        return StyxWoW.Me.CarriedItems.FirstOrDefault(i => DeadlyPoisons.Contains(i.Entry));
                    case PoisonType.Wound:
                        return StyxWoW.Me.CarriedItems.FirstOrDefault(i => WoundPoisons.Contains(i.Entry));
                    default:
                        return null;
                }
            }
        }

        private static HashSet<uint> InstantPoisons = new HashSet<uint>() { 6947, 6949, 6950, 8926, 8927, 8928, 21927, 43230, 43231 };

        private static HashSet<uint> CripplingPoisons = new HashSet<uint>() { 3775 };

        private static HashSet<uint> MindNumbingPoisons = new HashSet<uint>() { 5237 };

        private static HashSet<uint> DeadlyPoisons = new HashSet<uint>() { 2892, 2893, 8984, 8985, 20844, 22053, 22054, 43232, 43233 };

        private static HashSet<uint> WoundPoisons = new HashSet<uint>() { 10918, 10920, 10921, 10922, 22055, 43234, 43235 };
    }
}
