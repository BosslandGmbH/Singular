using System.Collections.Generic;
using System.Linq;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular.ClassSpecific.Rogue
{
    public enum LethalPoisonType
    {
        Deadly,
        Wound
    }


    public enum NonLethalPoisonType
    {
        Crippling,
        MindNumbing,
        Leeching,
        Paralytic
    }

    public static class Poisons
    {
        static Poisons()
        {
            //Lua.Events.AttachEvent("END_BOUND_TRADEABLE", HandleEndBoundTradeable);
        }

        private static void HandleEndBoundTradeable(object sender, LuaEventArgs args)
        {
            //Lua.DoString("EndBoundTradeable(" + args.Args[0] + ")");
        }

        //Lethal
        private const int DeadlyPoison = 2823;
        private const int WoundPoison = 8679;

        //Non-lethal
        private const int CripplingPoison = 3408;
        private const int MindNumbingPoison = 5761;
        private const int LeechingPoison = 108211;
        private const int ParalyticPoison = 108215;
        

            

        public static int NeedLethalPosion()
        {
            switch (SingularSettings.Instance.Rogue.LethalPoison)
            {
                case LethalPoisonType.Deadly:
                    if (SpellManager.HasSpell(DeadlyPoison) && !StyxWoW.Me.HasAura(DeadlyPoison))
                        return DeadlyPoison;
                    break;
                case LethalPoisonType.Wound:
                    if (SpellManager.HasSpell(WoundPoison) && !StyxWoW.Me.HasAura(WoundPoison))
                        return WoundPoison;
                    break;

                default:
                    return 0;


            }
            return 0;
        }


        public static int NeedNonLethalPosion()
        {
            switch (SingularSettings.Instance.Rogue.NonLethalPoison)
            {
                case NonLethalPoisonType.Crippling:
                    if (SpellManager.HasSpell(CripplingPoison) && !StyxWoW.Me.HasAura(CripplingPoison))
                        return CripplingPoison;
                    break;
                case NonLethalPoisonType.Leeching:
                    if (SpellManager.HasSpell(LeechingPoison) && !StyxWoW.Me.HasAura(LeechingPoison))
                        return LeechingPoison;
                    break;
                case NonLethalPoisonType.MindNumbing:
                    if (SpellManager.HasSpell(MindNumbingPoison) && !StyxWoW.Me.HasAura(MindNumbingPoison))
                        return MindNumbingPoison;
                    break;
                case NonLethalPoisonType.Paralytic:
                    if (SpellManager.HasSpell(ParalyticPoison) && !StyxWoW.Me.HasAura(ParalyticPoison))
                        return ParalyticPoison;
                    break;
                default:
                    return 0;


            }
            return 0;
        }


    }
}
