#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author$
// $Date$
// $HeadURL$
// $LastChangedBy$
// $LastChangedDate$
// $LastChangedRevision$
// $Revision$

#endregion

using System.Collections.Generic;
using System.Linq;

using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using CommonBehaviors.Actions;

namespace Singular.ClassSpecific.Shaman
{
    public enum Imbue
    {
        None = 0,

        Flametongue = 5,
        Windfury = 283,
        Earthliving = 3345,
        Frostbrand = 2,
        Rockbiter = 3021,
    }

    public static class Common
    {
        public static Composite CreateShamanRacialsCombat()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Blood Fury",
                    ret => SingularSettings.Instance.UseRacials &&
                        StyxWoW.Me.Race == WoWRace.Orc &&
                        !StyxWoW.Me.HasAnyAura("Elemental Mastery", "Lifeblood", "Bloodlust", "Heroism", "Time Warp", "Ancient Hysteria")),
                Spell.BuffSelf("Berserking",
                    ret => SingularSettings.Instance.UseRacials &&
                        StyxWoW.Me.Race == WoWRace.Troll &&
                        !StyxWoW.Me.HasAnyAura("Elemental Mastery", "Lifeblood", "Bloodlust", "Heroism", "Time Warp", "Ancient Hysteria")),
                Spell.BuffSelf("Lifeblood",
                    ret => SingularSettings.Instance.UseRacials &&
                        !StyxWoW.Me.HasAnyAura("Blood Fury", "Berserking", "Bloodlust", "Heroism", "Time Warp", "Ancient Hysteria")));
        }

        public static Composite CreateShamanImbueMainHandBehavior(params Imbue[] imbueList)
        {
            return new PrioritySelector(
                imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToString() + " Weapon")),

                new Decorator(
                ret => CanImbue(StyxWoW.Me.Inventory.Equipped.MainHand)
                    && StyxWoW.Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id != (int)ret
                    && SpellManager.HasSpell(ret.ToString() + " Weapon")
                    && SpellManager.CanCast(ret.ToString() + " Weapon", null, false, false),
                new Sequence(
                    new Action(ret => Lua.DoString("CancelItemTempEnchantment(1)")),
                    new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
                        new Sequence(
                            new Action(ret => Logger.Write("Imbuing main hand weapon with " + ((Imbue)ret).ToString())),
                            new Action(ret => SpellManager.Cast(((Imbue)ret).ToString() + " Weapon", null))
                            )
                        )
                    )
                )
            );
        }

        public static Composite CreateShamanImbueOffHandBehavior(params Imbue[] imbueList)
        {
            return new PrioritySelector(
                imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToString() + " Weapon")),

                new Decorator(
                ret => CanImbue(StyxWoW.Me.Inventory.Equipped.OffHand)
                    && StyxWoW.Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id != (int)ret
                    && SpellManager.HasSpell(ret.ToString() + " Weapon")
                    && SpellManager.CanCast(ret.ToString() + " Weapon", null, false, false),
                new Sequence(
                    new Action(ret => Lua.DoString("CancelItemTempEnchantment(2)")),
                    new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
                        new Sequence(
                            new Action(ret => Logger.Write("Imbuing Off hand weapon with " + ((Imbue)ret).ToString())),
                            new Action(ret => SpellManager.Cast(((Imbue)ret).ToString() + " Weapon", null))
                            )
                        )
                    )
                )
            );
        }

        public static bool CanImbue(WoWItem item)
        {
            if (item != null && item.ItemInfo.IsWeapon)
            {
                switch (item.ItemInfo.WeaponClass)
                {
                    case WoWItemWeaponClass.Axe:
                        return true;
                    case WoWItemWeaponClass.AxeTwoHand:
                        return true;
                    case WoWItemWeaponClass.Dagger:
                        return true;
                    case WoWItemWeaponClass.Fist:
                        return true;
                    case WoWItemWeaponClass.Mace:
                        return true;
                    case WoWItemWeaponClass.MaceTwoHand:
                        return true;
                    case WoWItemWeaponClass.Polearm:
                        return true;
                    case WoWItemWeaponClass.Staff:
                        return true;
                    case WoWItemWeaponClass.Sword:
                        return true;
                    case WoWItemWeaponClass.SwordTwoHand:
                        return true;
                }
            }

            return false;
        }

        public static Imbue GetImbue(WoWItem item)
        {
            if (item != null)
                return (Imbue) item.TemporaryEnchantment.Id;

            return Imbue.None;
        }

        public static bool IsImbuedForDPS(WoWItem item)
        {
            Imbue imb = GetImbue(item);
            return imb != Imbue.None && imb != Imbue.Earthliving;
        }
    }
}