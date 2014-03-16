
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

    public static class Common
    {
        #region Local Helpers

        private const int StressMobCount = 3;
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman(); } }

        #endregion

        #region Status and Config Helpers

        public static string BloodlustName { get { return Me.IsHorde ? "Bloodlust" : "Heroism"; } }
        public static string SatedName { get { return Me.IsHorde ? "Sated" : "Exhaustion"; } }

        public static bool HasTalent(ShamanTalents tal)
        {
            return TalentManager.IsSelected((int)tal);
        }

        public static bool StressfulSituation
        {
            get
            {
                return SingularRoutine.CurrentWoWContext == WoWContext.Normal
                    && (Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= StressMobCount
                    || Unit.NearbyUnfriendlyUnits.Any(u => u.Combat && u.IsTargetingMeOrPet && (u.IsPlayer || (u.Elite && u.Level + 8 > Me.Level))));
            }
        }

        /// <summary>
        /// checks if in a relatively balanced fight where atleast 3 of your
        /// teammates will benefti from Bloodlust.  fight must be atleast 3 v 3
        /// and size difference between factions nearby in fight cannot be greater
        /// than size / 3 + 1.  For example:
        /// 
        /// Yes:  3 v 3, 3 v 4, 3 v 5, 6 v 9, 9 v 13
        /// No :  2 v 3, 3 v 6, 4 v 7, 6 v 10, 9 v 14
        /// </summary>
        public static bool IsPvpFightWorthLusting
        {
            get
            {
                int friends = Unit.NearbyFriendlyPlayers.Count(f => f.IsAlive);
                int enemies = Unit.NearbyUnfriendlyUnits.Count();

                if (friends < 3 || enemies < 3)
                    return false;

                int readyfriends = Unit.NearbyFriendlyPlayers.Count(f => f.IsAlive && !f.HasAnyAura(SatedName, "Temporal Displacement"));
                if (readyfriends < 3)
                    return false;

                int diff = Math.Abs(friends - enemies);
                return diff <= ((friends / 3) + 1);
            }
        }

        #endregion

        [Behavior(BehaviorType.LossOfControl, WoWClass.Shaman)]
        public static Composite CreateShamanLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.Cast(WoWTotem.Tremor.ToSpellId(), on => Me, ret => Me.Fleeing),
                    new Decorator(
                        ret => Me.Fleeing && Spell.CanCastHack("Tremor Totem", Me),
                        Spell.CastHack("Tremor Totem", on => Me, req => { Logger.WriteDebug( Color.Pink, "Hack Casting Tremor"); return true; })
                        ),
                    Spell.Cast("Thunderstorm", on => Me, ret => Me.Stunned && Unit.NearbyUnfriendlyUnits.Any( u => u.IsWithinMeleeRange )),
                    Spell.BuffSelf("Shamanistic Rage", ret => Me.Stunned && Unit.NearbyUnfriendlyUnits.Any(u => u.IsWithinMeleeRange))
                    )
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Shaman)]
        public static Composite CreateShamanPreCombatBuffs()
        {
            return new PrioritySelector(
                CreateShamanMovementBuff()
                );
        }


        [Behavior(BehaviorType.CombatBuffs, WoWClass.Shaman, (WoWSpec)int.MaxValue, WoWContext.Normal | WoWContext.Instances, 1)]
        public static Composite CreateShamanCombatBuffs()
        {
            return new Decorator(
                req => !Unit.IsTrivial( Me.CurrentTarget),
                new PrioritySelector(

                    Totems.CreateTotemsBehavior(),

                    Spell.BuffSelf("Astral Shift", ret => Me.HealthPercent < ShamanSettings.AstralShiftPercent || Common.StressfulSituation),
                    Spell.BuffSelf(WoWTotem.StoneBulwark.ToSpellId(), ret => !Me.IsMoving && (Common.StressfulSituation || Me.HealthPercent < ShamanSettings.StoneBulwarkTotemPercent && !Totems.Exist(WoWTotem.EarthElemental))),
                    Spell.BuffSelf("Shamanistic Rage", ret => Me.HealthPercent < 70 || Me.ManaPercent < 70 || Common.StressfulSituation),

                    // hex someone if they are not current target, attacking us, and 12 yds or more away
                    new Decorator(
                        req => Me.GotTarget && (Me.Specialization != WoWSpec.ShamanEnhancement || !ShamanSettings.AvoidMaelstromDamage),
                        new PrioritySelector(
                            new PrioritySelector(
                                ctx => Unit.NearbyUnfriendlyUnits
                                    .Where(u => (u.CreatureType == WoWCreatureType.Beast || u.CreatureType == WoWCreatureType.Humanoid)
                                            && Me.CurrentTargetGuid != u.Guid
                                            && (u.Aggro || u.PetAggro || (u.Combat && u.IsTargetingMeOrPet))
                                            && !u.IsCrowdControlled()
                                            && u.Distance.Between(10, 30) && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && u.Location.Distance(Me.CurrentTarget.Location) > 10)
                                    .OrderByDescending(u => u.Distance)
                                    .FirstOrDefault(),
                                Spell.Cast("Hex", onUnit => (WoWUnit)onUnit)
                                ),

                            // bind someone if we can
                            new PrioritySelector(
                                ctx => Unit.NearbyUnfriendlyUnits
                                    .Where(u => u.CreatureType == WoWCreatureType.Elemental
                                            && Me.CurrentTargetGuid != u.Guid
                                            && (u.Aggro || u.PetAggro || (u.Combat && u.IsTargetingMeOrPet))
                                            && !u.IsCrowdControlled()
                                            && u.Distance.Between(10, 30) && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && u.Location.Distance(Me.CurrentTarget.Location) > 10)
                                    .OrderByDescending(u => u.Distance)
                                    .FirstOrDefault(),
                                Spell.Cast("Bind Elemental", onUnit => (WoWUnit)onUnit)
                                )
                            )
                        ),

                    new Decorator(
                        ret => ShamanSettings.UseBloodlust
                            && MovementManager.IsClassMovementAllowed,

                        new PrioritySelector(
                            Spell.BuffSelf(Common.BloodlustName,
                                ret => SingularRoutine.CurrentWoWContext == WoWContext.Normal
                                    && !Unit.GroupMembers.Any(m => m.IsAlive && m.Distance < 100)
                                    && Common.StressfulSituation),

                            Spell.BuffSelf(Common.BloodlustName,
                                ret => SingularRoutine.CurrentWoWContext == WoWContext.Instances
                                    && !Me.GroupInfo.IsInRaid
                                    && Me.CurrentTarget.IsBoss())
                            )
                        ),

                    Spell.BuffSelf("Ascendance", ret => SingularRoutine.CurrentWoWContext == WoWContext.Normal && Common.StressfulSituation),

                    Spell.BuffSelf("Elemental Mastery", ret => !PartyBuff.WeHaveBloodlust)

                    // , Spell.BuffSelf("Spiritwalker's Grace", ret => Me.IsMoving && Me.Combat)
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Shaman, (WoWSpec)int.MaxValue, WoWContext.Battlegrounds, 1)]
        public static Composite CreateShamanCombatBuffsPVP()
        {
            return new PrioritySelector(

                Totems.CreateTotemsBehavior(),

                Spell.BuffSelf("Astral Shift", ret => Me.HealthPercent < ShamanSettings.AstralShiftPercent || Common.StressfulSituation ),
                Spell.BuffSelf(WoWTotem.StoneBulwark.ToSpellId(), ret => !Me.IsMoving && (Common.StressfulSituation || Me.HealthPercent < ShamanSettings.StoneBulwarkTotemPercent && !Totems.Exist(WoWTotem.EarthElemental))),
                Spell.BuffSelf("Shamanistic Rage", ret => Me.HealthPercent < 70 || Me.ManaPercent < 70 || Common.StressfulSituation),

                // hex someone if they are not current target, attacking us, and 12 yds or more away
                new PrioritySelector(
                    ctx => Unit.NearbyUnfriendlyUnits
                        .Where(u => (u.CreatureType == WoWCreatureType.Beast || u.CreatureType == WoWCreatureType.Humanoid)
                                && (u.Aggro || u.PetAggro || (u.Combat && u.IsTargetingMeOrPet))
                                && u.Distance.Between(10, 30) && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && Me.GotTarget && u.Location.Distance(Me.CurrentTarget.Location) > 10)
                        .OrderByDescending(u => u.Distance)
                        .FirstOrDefault(),
                    Spell.Cast("Hex", onUnit => (WoWUnit)onUnit)
                    ),

                Spell.BuffSelf(Common.BloodlustName,
                    ret => ShamanSettings.UseBloodlust
                        && MovementManager.IsClassMovementAllowed
                        && IsPvpFightWorthLusting),

                Spell.BuffSelf("Ascendance",
                    ret => ((Me.GotTarget && Me.CurrentTarget.HealthPercent > 70) || Unit.NearbyUnfriendlyUnits.Count() > 1)),

                Spell.BuffSelf("Elemental Mastery", ret => !PartyBuff.WeHaveBloodlust)

                // , Spell.BuffSelf("Spiritwalker's Grace", ret => Me.IsMoving && Me.Combat)

                );
        }

        #region IMBUE SUPPORT
        public static Decorator CreateShamanImbueMainHandBehavior(params Imbue[] imbueList)
        {
            return new Decorator(ret => CanImbue(Me.Inventory.Equipped.MainHand),
                new PrioritySelector(
                    imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToSpellName())),

                    new Decorator(
                        ret => Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id != (int)ret
                            && SpellManager.HasSpell(((Imbue)ret).ToSpellName())
                            && Spell.CanCastHack(((Imbue)ret).ToSpellName(), null),
                        new Sequence(
                            new Action(ret => Logger.WriteDebug(Color.Pink, "Main hand [" 
                                + (Me.Inventory.Equipped.MainHand == null ? "-null-" : Me.Inventory.Equipped.MainHand.Name )
                                + " #" + (Me.Inventory.Equipped.MainHand == null ? 0 : Me.Inventory.Equipped.MainHand.Entry ) 
                                + "] currently imbued: " + ((Imbue)Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id).ToString())),
                            new Action(ret => Lua.DoString("CancelItemTempEnchantment(1)")),
                            new WaitContinue(1,
                                ret => Me.Inventory.Equipped.MainHand != null && (Imbue)Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id == Imbue.None,
                                new ActionAlwaysSucceed()),
                            new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
                                new Sequence(
                                    new Action(ret => Logger.Write(Color.Pink, 
                                        "Imbuing main hand ["
                                        + " #" + (Me.Inventory.Equipped.MainHand == null ? 0 : Me.Inventory.Equipped.MainHand.Entry)
                                        + "] weapon with " + ((Imbue)ret).ToString())
                                        ),
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
            return new Decorator(ret => CanImbue(Me.Inventory.Equipped.OffHand),
                new PrioritySelector(
                    imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToSpellName())),

                    new Decorator(
                        ret => Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id != (int)ret
                            && SpellManager.HasSpell(((Imbue)ret).ToSpellName())
                            && Spell.CanCastHack(((Imbue)ret).ToSpellName(), null),
                        new Sequence(
                            new Action(ret => Logger.WriteDebug(Color.Pink, "Off hand ["
                                + (Me.Inventory.Equipped.OffHand == null ? "-null-" : Me.Inventory.Equipped.OffHand.Name)
                                + " #" + (Me.Inventory.Equipped.OffHand == null ? 0 : Me.Inventory.Equipped.OffHand.Entry) 
                                + "] currently imbued: " + ((Imbue)Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id).ToString())),
                            new Action(ret => Lua.DoString("CancelItemTempEnchantment(2)")),
                            new WaitContinue(1,
                                ret => Me.Inventory.Equipped.OffHand != null && (Imbue)Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id == Imbue.None,
                                new ActionAlwaysSucceed()),
                            new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
                                new Sequence(
                                    new Action(ret => Logger.Write(System.Drawing.Color.Pink, "Imbuing Off hand ["
                                        + (Me.Inventory.Equipped.OffHand == null ? "-null-" : Me.Inventory.Equipped.OffHand.Name)
                                        + " #" + (Me.Inventory.Equipped.OffHand == null ? 0 : Me.Inventory.Equipped.OffHand.Entry)
                                        + "] weapon with " + ((Imbue)ret).ToString())),
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
            if (ShamanSettings.UseWeaponImbues && item != null && item.ItemInfo.IsWeapon)
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
            nextImbueAllowed = DateTime.Now + new TimeSpan(0, 0, 0, 0, 750); // 1500 + (int) StyxWoW.WoWClient.Latency << 1);
        }

        public static string ToSpellName(this Imbue i)
        {
            return i.ToString() + " Weapon";
        }

        public static Imbue GetImbue(WoWItem item)
        {
            if (item != null)
                return (Imbue)item.TemporaryEnchantment.Id;

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
            return new Throttle( 8,
                new PrioritySelector(
                    ctx => HealerManager.ActingAsOffHealer,
                    Spell.BuffSelf("Water Shield", ret => (bool)ret || Me.ManaPercent <= ShamanSettings.TwistWaterShield ),
                    Spell.BuffSelf("Lightning Shield", ret => !(bool)ret && Me.ManaPercent >= ShamanSettings.TwistDamageShield )
                    )
                );
        }

        public static Composite CreateShamanDpsHealBehavior()
        {
            Composite offheal;
            if (!SingularSettings.Instance.DpsOffHealAllowed)
                offheal = new ActionAlwaysFail();
            else
            {
                offheal = new Decorator(
                    ret => HealerManager.ActingAsOffHealer,
                    CreateDpsShamanOffHealBehavior()
                    );
            }

            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
            // use predicted health for non-combat healing to reduce drinking downtime and help
            // .. avoid unnecessary heal casts
                    new Decorator(
                        ret => !Me.Combat,  // non-combat = top off health
                        Spell.Cast("Healing Surge", ret => Me, ret => Me.GetPredictedHealthPercent(true) < 85)
                        ),

                    new Decorator(
                        ret => Me.Combat,

                        new PrioritySelector(

                            Spell.OffGCD( 
                                Spell.BuffSelf("Ancestral Guidance", 
                                    ret => Me.HealthPercent < ShamanSettings.SelfAncestralGuidance 
                                        && Me.GotTarget
                                        && Me.CurrentTarget.TimeToDeath() > 8 
                                    )
                                ),

                            // save myself if possible
                            new Decorator(
                                ret => (!Me.IsInGroup() || Battlegrounds.IsInsideBattleground)
                                    && Me.HealthPercent < ShamanSettings.SelfAncestralSwiftnessHeal,
                                new Sequence(
                                    Spell.BuffSelf("Ancestral Swiftness"),
                                    Spell.Cast("Healing Surge", ret => Me)
                                    )
                                )
                            )
                        ),

                    offheal
                    )
                );
        }


        #region DPS Off Heal
        private static WoWUnit _moveToHealUnit = null;

        public static Composite CreateDpsShamanOffHealBehavior()
        {
            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();
            int cancelHeal = (int)Math.Max(SingularSettings.Instance.IgnoreHealTargetsAboveHealth, ShamanSettings.OffHealSettings.HealingSurge);

            bool moveInRange = (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds);

            Logger.WriteDebugInBehaviorCreate("Shaman Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);
/*
            int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
                behavs.AddBehavior(dispelPriority, "Cleanse Spirit", null, Dispelling.CreateDispelBehavior());
*/
            #region Save the Group

            behavs.AddBehavior(Restoration.HealthToPriority(ShamanSettings.OffHealSettings.AncestralSwiftness) + 500,
                String.Format("Oh Shoot Heal @ {0}%", ShamanSettings.OffHealSettings.AncestralSwiftness),
                null,
                new Decorator(
                    ret => (Me.Combat || ((WoWUnit)ret).Combat) && ((WoWUnit)ret).GetPredictedHealthPercent() < ShamanSettings.OffHealSettings.AncestralSwiftness,
                    new PrioritySelector(
                        Spell.OffGCD(Spell.BuffSelf("Ancestral Swiftness")),
                        Spell.Cast("Healing Surge", on => (WoWUnit)on, ret => !SpellManager.HasSpell("Greater Healing Wave"))
                        )
                    )
                );

            #endregion

            #region AoE Heals

            behavs.AddBehavior(Restoration.HealthToPriority(ShamanSettings.OffHealSettings.HealingTideTotem) + 400,
                string.Format("Healing Tide Totem @ {0}% Count={1}", ShamanSettings.OffHealSettings.HealingTideTotem, ShamanSettings.OffHealSettings.MinHealingTideCount),
                "Healing Tide Totem",
                new Decorator(
                    ret => (Me.Combat || ((WoWUnit)ret).Combat) && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
                    Spell.Cast(
                        "Healing Tide Totem",
                        on => Me,
                        req => Me.Combat && HealerManager.Instance.TargetList.Count(p => p.GetPredictedHealthPercent() < ShamanSettings.OffHealSettings.HealingTideTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= ShamanSettings.OffHealSettings.MinHealingTideCount
                        )
                    )
                );

            behavs.AddBehavior(Restoration.HealthToPriority(ShamanSettings.OffHealSettings.HealingStreamTotem) + 300,
                string.Format("Healing Stream Totem @ {0}%", ShamanSettings.OffHealSettings.HealingStreamTotem),
                "Healing Stream Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Healing Stream Totem",
                        on => (!Me.Combat || Totems.Exist(WoWTotemType.Water)) ? null : HealerManager.Instance.TargetList.FirstOrDefault(p => p.GetPredictedHealthPercent() < ShamanSettings.OffHealSettings.HealingStreamTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingStream))
                        )
                    )
                );

            behavs.AddBehavior(Restoration.HealthToPriority(ShamanSettings.OffHealSettings.HealingRain) + 200,
                string.Format("Healing Rain @ {0}% Count={1}", ShamanSettings.OffHealSettings.HealingRain, ShamanSettings.OffHealSettings.MinHealingRainCount),
                "Healing Rain",
                Spell.CastOnGround("Healing Rain", on => Restoration.GetBestHealingRainTarget(), req => HealerManager.Instance.TargetList.Count() > 1, false)
                );

            behavs.AddBehavior(Restoration.HealthToPriority(ShamanSettings.OffHealSettings.ChainHeal) + 100,
                string.Format("Chain Heal @ {0}% Count={1}", ShamanSettings.OffHealSettings.ChainHeal, ShamanSettings.OffHealSettings.MinChainHealCount),
                "Chain Heal",
                Spell.Cast("Chain Heal", on => Restoration.GetBestChainHealTarget())
                );

            #endregion

            #region Single Target Heals

            behavs.AddBehavior(Restoration.HealthToPriority(ShamanSettings.OffHealSettings.HealingSurge),
                string.Format("Healing Surge @ {0}%", ShamanSettings.OffHealSettings.HealingSurge),
                "Healing Surge",
                Spell.Cast("Healing Surge",
                    mov => true,
                    on => (WoWUnit)on,
                    req => ((WoWUnit)req).GetPredictedHealthPercent() < ShamanSettings.OffHealSettings.HealingSurge,
                    cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                    )
                );

            #endregion

            behavs.OrderBehaviors();

            if (Singular.Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Heal )
                behavs.ListBehaviors();

            return new PrioritySelector(
                ctx => HealerManager.FindLowestHealthTarget(), // HealerManager.Instance.FirstUnit,

                new Decorator(
                    ret => ret != null && (Me.Combat || ((WoWUnit)ret).Combat || ((WoWUnit)ret).GetPredictedHealthPercent() <= 99),

                    new PrioritySelector(
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                Totems.CreateTotemsBehavior(),

    /*
                                Spell.Cast("Earth Shield",
                                    ret => (WoWUnit)ret,
                                    ret => ret is WoWUnit && Group.Tanks.Contains((WoWUnit)ret) && Group.Tanks.All(t => !t.HasMyAura("Earth Shield"))),
    */

                                behavs.GenerateBehaviorTree(),

                                new Decorator(
                                    ret => moveInRange,
                                    new Sequence(
                                        new Action(r => _moveToHealUnit = (WoWUnit)r),
                                        new PrioritySelector(
                                            Movement.CreateMoveToLosBehavior(on => _moveToHealUnit),
                                            Movement.CreateMoveToUnitBehavior(on => _moveToHealUnit, 30f, 25f)
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }
        #endregion

        public static DateTime GhostWolfRequest;

        public static Decorator CreateShamanMovementBuff()
        {
            return new Decorator(
                ret => ShamanSettings.UseGhostWolf
                    && !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown()
                    && MovementManager.IsClassMovementAllowed
                    && SingularRoutine.CurrentWoWContext != WoWContext.Instances
                    && Me.IsMoving // (DateTime.Now - GhostWolfRequest).TotalMilliseconds < 1000
                    && Me.IsAlive
                    && !Me.OnTaxi && !Me.InVehicle && !Me.Mounted && !Me.IsOnTransport && !Me.IsSwimming 
                    && !Me.HasAura("Ghost Wolf")
                    && SpellManager.HasSpell("Ghost Wolf")
                    && !Utilities.EventHandlers.IsShapeshiftSuppressed
                    && BotPoi.Current != null
                    && BotPoi.Current.Type != PoiType.None
                    && BotPoi.Current.Type != PoiType.Hotspot
                    && BotPoi.Current.Location.Distance(Me.Location) > 10
                    && (BotPoi.Current.Location.Distance(Me.Location) < Styx.Helpers.CharacterSettings.Instance.MountDistance || (Me.IsIndoors && !Mount.CanMount()) || (Me.GetSkill(SkillLine.Riding).CurrentValue == 0))
                    && !Me.IsAboveTheGround(),

                new Sequence(
                    new Action(r => Logger.WriteDebug("ShamanMoveBuff: poitype={0} poidist={1:F1} indoors={2} canmount{3} riding={4}", 
                        BotPoi.Current.Type, 
                        BotPoi.Current.Location.Distance(Me.Location),
                        Me.IsIndoors.ToYN(),
                        Mount.CanMount().ToYN(),
                        Me.GetSkill(SkillLine.Riding).CurrentValue
                        )),
                    Spell.BuffSelf("Ghost Wolf"),
                    Helpers.Common.CreateWaitForLagDuration()
                    )
                );
        }

        #endregion


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
        RushingStreams,
        AncestralGuidance,
        Conductivity,
        UnleashedFury,
        PrimalElementalist,
        ElementalBlast
    }

}