
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
using Styx.CommonBot.POI;
using Singular.Dynamics;

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

    public enum ShamanTalents
    {
        NaturesGuardian = 1,
        StoneBulwarkTotem,
        AstralShift,
        FrozenPower,
        EarthgrabTotem,
        WindwalkTotem,
        CallOfTheElements,
        TotemicRestoration,
        TotemicProjection,
        ElementalMastery,
        AncestralSwiftness,
        EchoOfTheElements,
        HealingTideTotem,
        AncestralGuidance,
        Conductivity,
        UnleashedFury,
        PrimalElementalist,
        ElementalBlast
    }

    public static class Common
    {
        #region Local Helpers

        private const int StressMobCount = 3;
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman; } }

        #endregion

        #region Status and Config Helpers

        public static string BloodlustName { get { return Me.IsHorde ? "Bloodlust" : "Heroism"; } }
        public static string SatedName { get { return Me.IsHorde ? "Sated" : "Exhaustion"; } }
        public static bool AnyHealersNearby { get { return Group.Healers.Any(h => h.IsAlive && h.Distance < 60); } }

        public static bool HasTalent(ShamanTalents tal)
        {
            return TalentManager.IsSelected((int)tal);
        }

        public static bool StressfulSituation
        {
            get 
            {
                return Unit.NearbyUnitsInCombatWithMe.Count() >= StressMobCount
                    || Unit.NearbyUnfriendlyUnits.Any(u => u.Combat && u.IsTargetingMeOrPet && (u.IsPlayer || (u.Elite && u.Level + 8 > Me.Level)));
            }
        }

        public static bool ActingAsHealer
        {
            get
            {
                return ShamanSettings.AllowOffHealHeal
                    && Me.IsInGroup() && !Me.GroupInfo.IsInRaid
                    && !Common.AnyHealersNearby;
            }
        }

        public static bool NeedToOffHealSomeone
        {
            get
            {
                return ShamanSettings.AllowOffHealHeal
                    && Me.IsInGroup() && !Me.GroupInfo.IsInRaid
                    && (!Common.AnyHealersNearby || Unit.NearbyGroupMembers.Any(m => m.IsAlive && m.HealthPercent < 30));
            }
        }

        /// <summary>
        /// checks if pvp fight is worth popping lust by comparing # of combatants
        /// from each faction.  must be atleast 3 on each side with the difference
        /// being approx 33% at most.  additionally requires atleast 3 or more
        /// friendlies to not be sated
        /// </summary>
        public static bool IsPvpFightWorthLusting
        {
            get
            {
                int friends = Unit.NearbyFriendlyPlayers.Count(f => f.IsAlive);
                int enemies = Unit.NearbyUnfriendlyUnits.Count();

                if (friends < 3 || enemies < 3)
                    return false;

                int readyfriends = Unit.NearbyFriendlyPlayers.Count(f => f.IsAlive && !f.HasAura(SatedName));
                if (readyfriends < 3)
                    return false;

                int diff = Math.Abs(friends - enemies);
                return diff <= ((friends / 3) + 1);
            }
        }

        #endregion

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Shaman, (WoWSpec)int.MaxValue, WoWContext.All, 1)]
        public static Composite CreateShamanCombatBuffs()
        {
            return new PrioritySelector(

                Totems.CreateTotemsBehavior(),

                Spell.BuffSelf( "Astral Shift", ret => Me.HealthPercent < 50),

                new Decorator( 
                    ret => ShamanSettings.UseBloodlust 
                        && !SingularSettings.Instance.DisableAllMovement 
                        && Common.StressfulSituation,

                    new PrioritySelector(
                        Spell.BuffSelf( Common.BloodlustName, 
                            ret => SingularRoutine.CurrentWoWContext == WoWContext.Normal ),

                        Spell.BuffSelf(Common.BloodlustName,
                            ret => SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds
                                && IsPvpFightWorthLusting),

                        Spell.BuffSelf( Common.BloodlustName, 
                            ret => SingularRoutine.CurrentWoWContext == WoWContext.Instances  
                                && !Me.GroupInfo.IsInRaid  
                                && Me.CurrentTarget.IsBoss )
                        )
                    ),

                Spell.BuffSelf("Elemental Mastery", ret => !PartyBuff.WeHaveBloodlust),

                Spell.BuffSelf("Spiritwalker's Grace", ret => Me.IsMoving && Me.Combat)

                );
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

        public static Composite CreateShamanDpsShieldBehavior()
        {
            return new PrioritySelector(
                ctx => ActingAsHealer,
                Spell.BuffSelf("Water Shield", ret => (bool)ret),
                Spell.BuffSelf("Lightning Shield", ret => !(bool)ret)
                );
        }

        public static Composite CreateShamanDpsHealBehavior()
        {
            return new PrioritySelector(

                // use predicted health for non-combat healing to reduce drinking downtime and help
                // .. avoid unnecessary heal casts
                new Decorator(
                    ret => !StyxWoW.Me.Combat,
                    Spell.Heal("Healing Surge", ret => StyxWoW.Me, ret => StyxWoW.Me.GetPredictedHealth(true) <= 85)
                    ),

                new Decorator(
                    ret => Me.Combat,

                    new PrioritySelector(

                        // save myself if possible
                        new Decorator(
                            ret => (!Me.IsInGroup() || Battlegrounds.IsInsideBattleground)
                                && Me.HealthPercent < 20,
                            new Sequence( 
                                Spell.BuffSelf("Ancestral Swiftness", ret => ((WoWUnit)ret).GetPredictedHealthPercent() < SingularSettings.Instance.Shaman.Heal.AncestralSwiftness),
                                Spell.Heal("Greater Healing Wave", ret => (WoWUnit)ret)
                                )
                            ),

                        // use non-predicted health as we only off-heal when its already an emergency
                        new Decorator(
                            ret => NeedToOffHealSomeone,
                            Restoration.CreateRestoShamanHealingOnlyBehavior()
                            ),

                        // use non-predicted health as a trigger for totems
                        new Decorator(
                            ret => !Common.AnyHealersNearby,
                            new PrioritySelector(
                                Spell.BuffSelf(
                                    "Healing Tide Totem",
                                    ret => !Me.IsMoving
                                        && Unit.GroupMembers.Any(
                                            p => p.HealthPercent < SingularSettings.Instance.Shaman.HealingTideTotemPercent
                                                && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide))),
                                Spell.BuffSelf(
                                    "Healing Stream Totem",
                                    ret => !Me.IsMoving 
                                        && !Totems.Exist(WoWTotemType.Water)
                                        && Unit.GroupMembers.Any(
                                            p => p.HealthPercent < SingularSettings.Instance.Shaman.HealHealingStreamTotem
                                                && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide))),

                                // use actual health for following, not predicted as its a low health value
                                // .. and its okay for multiple heals at that point
                                Spell.Heal(
                                    "Healing Surge",
                                    ret => StyxWoW.Me,
                                    ret => StyxWoW.Me.HealthPercent <= 30)
                                )
                            )
                        )
                    )
                );
        }

        public static DateTime GhostWolfRequest;

        public static Decorator CreateShamanMovementBuff()
        {
            return new Decorator(
                ret => ShamanSettings.UseGhostWolf
                    && !SingularSettings.Instance.DisableAllMovement
                    && SingularRoutine.CurrentWoWContext != WoWContext.Instances 
                    && Me.IsMoving // (DateTime.Now - GhostWolfRequest).TotalMilliseconds < 1000
                    && Me.IsAlive
                    && !Me.OnTaxi && !Me.InVehicle && !Me.Mounted && !Me.IsOnTransport 
                    && SpellManager.HasSpell("Ghost Wolf")
                    && (BotPoi.Current == null || BotPoi.Current.Type == PoiType.None || BotPoi.Current.Location.Distance(Me.Location) > 10)
                    && !Me.IsAboveTheGround(),
                new PrioritySelector(
                    Spell.WaitForCast(),
                    new Decorator(
                        ret => Me.IsChanneling || Spell.IsGlobalCooldown(),
                        new ActionAlwaysSucceed()
                        ),
                    Spell.BuffSelf("Ghost Wolf"),
                    Helpers.Common.CreateWaitForLagDuration()
                    )
                );
        }

        #endregion

    }
}