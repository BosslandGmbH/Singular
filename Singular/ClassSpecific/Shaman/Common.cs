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
using System.Drawing;

using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using CommonBehaviors.Actions;

using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Shaman
{
    /// <summary>
    /// Temporary Enchant Id associated with Shaman Imbue
    /// Note: each enum value and Imbue.GetSpellName() must be maintained in a way to allow tranlating an enum into a corresponding spell name
    /// </summary>
    public enum Imbue
    {
        None = 0,

        Flametongue = 5,
        Windfury = 283,
        Earthliving = 3345,
        Frostbrand = 2,
        Rockbiter = 3021
    }

    public static class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        public static string BloodlustName
        {
            get
            {
                return Me.IsHorde ? "Bloodlust" : "Heroism";
            }
        }

        /// <summary>
        /// builds behaviors to cast Racial or Profession buffs during Combat 
        /// so they are used one at a time (don't cast Blood Fury if Bloodlust active.) 
        /// This is most useful for grinding/questing
        /// </summary>
        /// <returns></returns>
        public static Composite CreateShamanInCombatBuffs( bool mutuallyExclude )
        {
            PrioritySelector ps = new PrioritySelector();
            List<string> aurasToAvoid = new List<string>();
           
            if ( Me.Race == WoWRace.Orc )
                ps.AddChild( Spell.BuffSelf("Blood Fury", ret => SingularSettings.Instance.UseRacials && (!Me.IsInInstance || Me.HasAura(BloodlustName))));

            if (Me.Race == WoWRace.Troll )
                ps.AddChild( Spell.BuffSelf("Berserking", ret => SingularSettings.Instance.UseRacials));

            if (SpellManager.HasSpell("Lifeblood"))
            {
                aurasToAvoid.Add("Lifeblood");
                ps.AddChild(Spell.BuffSelf("Lifeblood", ret => SingularSettings.Instance.UseRacials));
            }

            if (Me.Specialization == WoWSpec.ShamanElemental)
                aurasToAvoid.Add("Elemental Mastery");

            aurasToAvoid.Add(BloodlustName);
            aurasToAvoid.Add("Time Warp");
            aurasToAvoid.Add("Ancient Hysteria");

            if ( mutuallyExclude )
            {
                return new Decorator( 
                    ret => !Me.HasAnyAura( aurasToAvoid.ToArray()),
                    ps );
            }

            return ps;
        }

        #region IMBUE SUPPORT
        public static Decorator CreateShamanImbueMainHandBehavior(params Imbue[] imbueList)
        {
            return new Decorator( ret => CanImbue(Me.Inventory.Equipped.MainHand),
                new PrioritySelector(
                    imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToSpellName())),

                    new Decorator(
                        ret => Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id != (int)ret
                            && SpellManager.HasSpell(((Imbue)ret).ToSpellName())
                            && SpellManager.CanCast(((Imbue)ret).ToSpellName(), null, false, false),
                        new Sequence(
                            new Action(ret => Logger.WriteDebug(Color.Pink, "Main hand currently imbued: " + ((Imbue)Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id).ToString())),
                            new Action(ret => Lua.DoString("CancelItemTempEnchantment(1)")),
                            new WaitContinue( 1,
                                ret => Me.Inventory.Equipped.MainHand != null && (Imbue)Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id == Imbue.None,
                                new ActionAlwaysSucceed()),
                            new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
                                new Sequence(
                                    new Action(ret => Logger.Write( Color.Pink, "Imbuing main hand weapon with " + ((Imbue)ret).ToString())),
                                    new Action(ret => SpellManager.Cast(((Imbue)ret).ToSpellName(), null)),
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
                    imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToSpellName())),

                    new Decorator(
                        ret => Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id != (int)ret
                            && SpellManager.HasSpell(((Imbue)ret).ToSpellName())
                            && SpellManager.CanCast(((Imbue)ret).ToSpellName(), null, false, false),
                        new Sequence(
                            new Action(ret => Logger.WriteDebug(Color.Pink, "Off hand currently imbued: " + ((Imbue)Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id).ToString())),
                            new Action(ret => Lua.DoString("CancelItemTempEnchantment(2)")),
                            new WaitContinue( 1,
                                ret => Me.Inventory.Equipped.OffHand != null && (Imbue)Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id == Imbue.None,
                                new ActionAlwaysSucceed()),
                            new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
                                new Sequence( 
                                    new Action(ret => Logger.Write(System.Drawing.Color.Pink, "Imbuing Off hand weapon with " + ((Imbue)ret).ToString())),
                                    new Action(ret => SpellManager.Cast(((Imbue)ret).ToSpellName(), null)),
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
            if (item != null && item.ItemInfo.IsWeapon )
            {
                // during combat, only mess with imbues if they are missing
                if (Me.Combat && item.TemporaryEnchantment.Id != 0)
                    return false;

                // check if enough time has passed since last imbue
                // .. guards against detecting is missing immediately after a cast but before buff appears
                // .. (which results in imbue cast spam)
                if (nextImbueAllowed > DateTime.Now)
                    return false;

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
            // 2 seconds to allow for 0.5 seconds plus latency for buff to appear
            nextImbueAllowed = DateTime.Now + new TimeSpan(0, 0, 0, 0, 500); // 1500 + (int) StyxWoW.WoWClient.Latency << 1);
        }

        public static string ToSpellName(this Imbue i)
        {
            return i.ToString() + " Weapon";
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
            return imb == Imbue.Flametongue || imb == Imbue.Windfury;
        }

        public static bool IsImbuedForHealing(WoWItem item)
        {
            return GetImbue(item) == Imbue.Earthliving;
        }

        #endregion


        public static bool InGCD
        {
            get
            {
                return SpellManager.GlobalCooldown;
            }
        }


        public static bool Players(this CastOn c)
        {
            return c == CastOn.All || c == CastOn.Players;
        }

        public static bool Bosses(this CastOn c)
        {
            return c == CastOn.All || c == CastOn.Bosses;
        }

        #region NON-RESTO HEALING

        public static Composite CreateShamanNonHealBehavior()
        {
            return
                new PrioritySelector(

                    new Decorator(
                        ret => !StyxWoW.Me.Combat,
                            Spell.Heal(
                                "Healing Surge",
                                ret => StyxWoW.Me,
                                ret => StyxWoW.Me.HealthPercent <= 70)
                        ),
                    new Decorator(
                        ret => StyxWoW.Me.Combat,
                        new PrioritySelector(
                            Spell.BuffSelf(
                                "Healing Tide Totem",
                                ret => Unit.NearbyFriendlyPlayers.Any(
                                        p => p.HealthPercent < SingularSettings.Instance.Shaman.HealingTideTotemPercent
                                            && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide))),
                            Spell.BuffSelf(
                                "Healing Stream Totem",
                                ret => !Totems.Exist(WoWTotemType.Water)
                                    && Unit.NearbyFriendlyPlayers.Any(
                                        p => p.HealthPercent < SingularSettings.Instance.Shaman.HealHealingStreamTotem
                                            && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide))),
                            Spell.Heal(
                                "Healing Surge",
                                ret => StyxWoW.Me,
                                ret => StyxWoW.Me.HealthPercent <= 30)
                            )
                        )
                    );
        }

        #endregion

    }
}