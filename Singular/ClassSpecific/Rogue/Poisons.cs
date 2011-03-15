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
                        return StyxWoW.Me.CarriedItems.Where(i => InstantPoisons.Contains(i.Entry)).OrderByDescending(i => i.Entry).FirstOrDefault();
                    case PoisonType.Crippling:
                        return StyxWoW.Me.CarriedItems.Where(i => CripplingPoisons.Contains(i.Entry)).OrderByDescending(i => i.Entry).FirstOrDefault();
                    case PoisonType.MindNumbing:
                        return StyxWoW.Me.CarriedItems.Where(i => MindNumbingPoisons.Contains(i.Entry)).OrderByDescending(i => i.Entry).FirstOrDefault();
                    case PoisonType.Deadly:
                        return StyxWoW.Me.CarriedItems.Where(i => DeadlyPoisons.Contains(i.Entry)).OrderByDescending(i => i.Entry).FirstOrDefault();
                    case PoisonType.Wound:
                        return StyxWoW.Me.CarriedItems.Where(i => WoundPoisons.Contains(i.Entry)).OrderByDescending(i => i.Entry).FirstOrDefault();
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
                        return StyxWoW.Me.CarriedItems.Where(i => InstantPoisons.Contains(i.Entry)).OrderByDescending(i => i.Entry).FirstOrDefault();
                    case PoisonType.Crippling:
                        return StyxWoW.Me.CarriedItems.Where(i => CripplingPoisons.Contains(i.Entry)).OrderByDescending(i => i.Entry).FirstOrDefault();
                    case PoisonType.MindNumbing:
                        return StyxWoW.Me.CarriedItems.Where(i => MindNumbingPoisons.Contains(i.Entry)).OrderByDescending(i => i.Entry).FirstOrDefault();
                    case PoisonType.Deadly:
                        return StyxWoW.Me.CarriedItems.Where(i => DeadlyPoisons.Contains(i.Entry)).OrderByDescending(i => i.Entry).FirstOrDefault();
                    case PoisonType.Wound:
                        return StyxWoW.Me.CarriedItems.Where(i => WoundPoisons.Contains(i.Entry)).OrderByDescending(i => i.Entry).FirstOrDefault();
                    default:
                        return null;
                }
            }
        }

        private static HashSet<uint> InstantPoisons = new HashSet<uint>() { 6947, 43231 };

        private static HashSet<uint> CripplingPoisons = new HashSet<uint>() { 3775 };

        private static HashSet<uint> MindNumbingPoisons = new HashSet<uint>() { 5237 };

        private static HashSet<uint> DeadlyPoisons = new HashSet<uint>() { 2892, 43233 };

        private static HashSet<uint> WoundPoisons = new HashSet<uint>() { 10918, 43235 };
    }
}
