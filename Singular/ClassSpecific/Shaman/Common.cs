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

using System;
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

using CommonBehaviors.Actions;

using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using System.Drawing;

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
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        public static Composite CreateShamanRacialsCombat()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Blood Fury",
                    ret => SingularSettings.Instance.UseRacials &&
                        Me.Race == WoWRace.Orc &&
                        !Me.HasAnyAura("Elemental Mastery", "Lifeblood", "Bloodlust", "Heroism", "Time Warp", "Ancient Hysteria")),
                Spell.BuffSelf("Berserking",
                    ret => SingularSettings.Instance.UseRacials &&
                        Me.Race == WoWRace.Troll &&
                        !Me.HasAnyAura("Elemental Mastery", "Lifeblood", "Bloodlust", "Heroism", "Time Warp", "Ancient Hysteria")),
                Spell.BuffSelf("Lifeblood",
                    ret => SingularSettings.Instance.UseRacials &&
                        !Me.HasAnyAura("Blood Fury", "Berserking", "Bloodlust", "Heroism", "Time Warp", "Ancient Hysteria")));
        }

        public static Composite CreateShamanImbueMainHandBehavior(params Imbue[] imbueList)
        {
            return new Decorator( ret => CanImbue(Me.Inventory.Equipped.MainHand),
                new PrioritySelector(
                    imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToString() + " Weapon")),

                    new Decorator(
                        ret => Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id != (int)ret
                            && SpellManager.HasSpell(ret.ToString() + " Weapon")
                            && SpellManager.CanCast(ret.ToString() + " Weapon", null, false, false),
                        new Sequence(
                            new Action(ret => Logger.WriteDebug(Color.Pink, "Main hand currently imbued: " + ((Imbue)Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id).ToString())),
                            new Action(ret => Lua.DoString("CancelItemTempEnchantment(1)")),
                            new WaitContinue( 1,
                                ret => Me.Inventory.Equipped.MainHand != null && (Imbue)Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id == Imbue.None,
                                new ActionAlwaysSucceed()),
                            new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
                                new Sequence(
                                    new Action(ret => Logger.Write( Color.Pink, "Imbuing main hand weapon with " + ((Imbue)ret).ToString())),
                                    new Action(ret => SpellManager.Cast(((Imbue)ret).ToString() + " Weapon", null)),
                                    new Action(ret => SetNextAllowedImbueTime())
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static Composite CreateShamanImbueOffHandBehavior(params Imbue[] imbueList)
        {
            return new Decorator( ret => CanImbue(Me.Inventory.Equipped.OffHand),
                new PrioritySelector(
                    imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToString() + " Weapon")),

                    new Decorator(
                        ret => Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id != (int)ret
                            && SpellManager.HasSpell(ret.ToString() + " Weapon")
                            && SpellManager.CanCast(ret.ToString() + " Weapon", null, false, false),
                        new Sequence(
                            new Action(ret => Logger.WriteDebug(Color.Pink, "Off hand currently imbued: " + ((Imbue)Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id).ToString())),
                            new Action(ret => Lua.DoString("CancelItemTempEnchantment(2)")),
                            new WaitContinue( 1,
                                ret => Me.Inventory.Equipped.OffHand != null && (Imbue)Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id == Imbue.None,
                                new ActionAlwaysSucceed()),
                            new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
                                new Sequence( 
                                    new Action(ret => Logger.Write(System.Drawing.Color.Pink, "Imbuing Off hand weapon with " + ((Imbue)ret).ToString())),
                                    new Action(ret => SpellManager.Cast(((Imbue)ret).ToString() + " Weapon", null)),
                                    new Action(ret => SetNextAllowedImbueTime())
                                    )
                                )
                            )
                        )
                )
            );
        }

        // imbues are sometimes slow to appear on client... need to allow time
        // .. for buff to appear, otherwise will get in an imbue spam loop

        private static DateTime nextImbueAllowed = DateTime.Now;

        public static bool CanImbue(WoWItem item)
        {
            if (item != null && item.ItemInfo.IsWeapon && nextImbueAllowed < DateTime.Now)
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

        public static void SetNextAllowedImbueTime()
        {
            // 2 seconds to allow for GCD + 0.5 seconds plus latency for buff
            nextImbueAllowed = DateTime.Now + new TimeSpan(0, 0, 0, 0, 500); // 1500 + (int) StyxWoW.WoWClient.Latency << 1);
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

        public static bool IsImbuedForHealing(WoWItem item)
        {
            return GetImbue(item) == Imbue.Earthliving;
        }
    }
}