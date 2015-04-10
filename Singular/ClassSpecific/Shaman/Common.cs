
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
using Singular.Utilities;

namespace Singular.ClassSpecific.Shaman
{
    /// <summary>
    /// Temporary Enchant Id associated with Shaman Imbue
    /// Note: each enum value and Imbue.GetSpellName() must be maintained in a way to allow tranlating an enum into a corresponding spell name
    /// </summary>
    //public enum Imbue
    //{
    //    None = 0,

    //    Flametongue = 5,
    //    Windfury = 283,
    //    Earthliving = 3345,
    //    Frostbrand = 2,
    //    Rockbiter = 3021
    //}

    public static class Common
    {
        #region Local Helpers

        private const int StressMobCount = 3;
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman(); } }

        #endregion

        #region Status and Config Helpers

        public static bool talentTotemicPersistance { get; set; }

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

                int readyfriends = Unit.NearbyFriendlyPlayers.Count(f => f.IsAlive && !f.HasAnyAura(PartyBuff.SatedDebuffName, "Temporal Displacement"));
                if (readyfriends < 3)
                    return false;

                int diff = Math.Abs(friends - enemies);
                return diff <= ((friends / 3) + 1);
            }
        }

        #endregion

        #region INIT

        [Behavior(BehaviorType.Initialize, WoWClass.Shaman)]
        public static Composite CreateShamanInitialize()
        {
            PetManager.NeedsPetSupport = HasTalent(ShamanTalents.PrimalElementalist);
            talentTotemicPersistance = HasTalent(ShamanTalents.TotemicPersistence);
            return null;
        }

        #endregion


        /// <summary>
        /// invoke on CurrentTarget if not tagged. use ranged instant casts first
        /// and if none known or off cooldown use Lightning Bolt.  this  is a blend
        /// of abilities across all specializations
        /// </summary>
        /// <returns></returns>
        public static Composite CreateShamanInCombatPullMore()
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return new ActionAlwaysFail();

            return new Throttle(
                2,
                new Decorator(
                    req => Me.GotTarget()
                        && !Me.CurrentTarget.IsPlayer
                        && !Me.CurrentTarget.IsTagged
                        && !Me.CurrentTarget.IsWithinMeleeRange,
                    new PrioritySelector(
                        Spell.Cast("Earth Shock", ret => StyxWoW.Me.HasAura("Lightning Shield", 5)),
                        Spell.Buff("Flame Shock", true, req => SpellManager.HasSpell("Lava Burst")),
                        Spell.Cast("Unleash Elements"),
                        Spell.Cast("Earth Shock", ret => !SpellManager.HasSpell("Flame Shock")),
                        Spell.Cast("Lightning Bolt")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Shaman)]
        public static Composite CreateShamanLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    new Decorator(
                        ret => Me.Fleeing && !Spell.IsSpellOnCooldown(WoWTotem.Tremor.ToSpellId()),
                        new PrioritySelector(
                            new Action( r => {
                                Logger.Write( LogColor.Hilite, "/use Tremor Totem (I am fleeing...)");
                                return RunStatus.Failure;
                            }),
                            Spell.Cast(WoWTotem.Tremor.ToSpellId(), on => Me),
                            Spell.CastHack("Tremor Totem", on => Me, 
                                req => {
                                    if (!Spell.CanCastHack("Tremor Totem", Me))
                                        return false;
                                    Logger.WriteDebug( Color.Pink, "Hack Casting Tremor"); 
                                    return true; 
                                })
                            )
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

                    // hex someone if they are not current target, attacking us, and 12 yds or more away
                    new Decorator(
                        req => Me.GotTarget() && (TalentManager.CurrentSpec != WoWSpec.ShamanEnhancement || !ShamanSettings.AvoidMaelstromDamage),
                        new PrioritySelector(
                            new PrioritySelector(
                                ctx => Unit.NearbyUnfriendlyUnits
                                    .Where(u => (u.CreatureType == WoWCreatureType.Beast || u.CreatureType == WoWCreatureType.Humanoid)
                                            && Me.CurrentTargetGuid != u.Guid
                                            && (u.Aggro || u.PetAggro || (u.Combat && u.IsTargetingMeOrPet))
                                            && !u.IsCrowdControlled()
                                            && u.SpellDistance().Between(10, 30) && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && u.Location.Distance(Me.CurrentTarget.Location) > 10)
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
                        ret => ShamanSettings.UseBloodlust && MovementManager.IsClassMovementAllowed,
                        Spell.BuffSelf(
                            PartyBuff.BloodlustSpellName,
                            req => {
                                // when Solo, use it if in a stressful fight
                                if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                                {
                                    if (Unit.GroupMembers.Any(m => m.IsAlive && m.Distance < 100))
                                        return false;

                                    return Common.StressfulSituation;
                                }

                                // should be manually cast when LazyRaiding, or by DungeonBuddy
                                if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                                {
                                    return !Me.GroupInfo.IsInRaid && Me.CurrentTarget.IsBoss();
                                }

                                return false;
                                }
                            )
                        ),

                    Spell.BuffSelf("Ascendance", ret => ShamanSettings.UseAscendance && SingularRoutine.CurrentWoWContext == WoWContext.Normal && Common.StressfulSituation),

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
                                && u.Distance.Between(10, 30) && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && Me.GotTarget() && u.Location.Distance(Me.CurrentTarget.Location) > 10)
                        .OrderByDescending(u => u.Distance)
                        .FirstOrDefault(),
                    Spell.Cast("Hex", onUnit => (WoWUnit)onUnit)
                    ),

                new Decorator(
                    req => {
                        if (!Unit.ValidUnit( Me.CurrentTarget))
                            return false;
                        if (Me.Specialization == WoWSpec.ShamanEnhancement && !Me.CurrentTarget.IsWithinMeleeRange)
                            return false;

                        if (Me.CurrentTarget.TimeToDeath() > 5 || Me.CurrentTarget.HealthPercent > 20 || Me.HealthPercent < Me.CurrentTarget.HealthPercent)
                            return true;
                        if (Unit.UnfriendlyUnits(40).Count(u => u.IsTargetingMyStuff()) >= 2)
                            return true;

                        return false;
                    },
                    new PrioritySelector(
                        Spell.BuffSelfAndWait(PartyBuff.BloodlustSpellName,
                            req => ShamanSettings.UseBloodlust
                                && MovementManager.IsClassMovementAllowed
                                && IsPvpFightWorthLusting,
                            gcd: HasGcd.No
                            ),

                        Spell.BuffSelf(
                            "Ascendance",
                            req => ShamanSettings.UseAscendance 
                                && ((Me.GotTarget() && Me.CurrentTarget.HealthPercent > 70) 
                                || Unit.NearbyUnfriendlyUnits.Count() > 1),
                            gcd: HasGcd.No
                            ),

                        Spell.BuffSelf(
                            "Elemental Mastery", 
                            req => !PartyBuff.WeHaveBloodlust, 
                            gcd: HasGcd.No
                            )
                        )
                    )
                // , Spell.BuffSelf("Spiritwalker's Grace", ret => Me.IsMoving && Me.Combat)

                );
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
            return new Throttle( 8, Spell.BuffSelf("Lightning Shield") );
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

                    new Decorator(
                        ret => !Me.Combat
                            && (!Me.IsMoving || Me.HasAura("Maelstrom Weapon", 5))
                            && Me.HealthPercent <= 85  // not redundant... this eliminates unnecessary GetPredicted... checks
                            && SpellManager.HasSpell("Healing Surge")
                            && Me.PredictedHealthPercent(includeMyHeals: true) < 85,
                        new PrioritySelector(
                            new Sequence(
                                ctx => (float)Me.HealthPercent,
                                new Action(r => Logger.WriteDebug("Healing Surge: {0:F1}% Predict:{1:F1}% and moving:{2}, cancast:{3}", (float) r, Me.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving, Spell.CanCastHack("Healing Surge", Me, skipWowCheck: false))),
                                Spell.Cast(
                                    "Healing Surge",
                                    mov => true,
                                    on => Me,
                                    req => true,
                                    cancel => Me.HealthPercent > 85
                                    ),
                                new WaitContinue(TimeSpan.FromMilliseconds(500), until => !Me.IsCasting && Me.HealthPercent > (1.1 * ((float)until)), new ActionAlwaysSucceed()),
                                new Action( r => Logger.WriteDebug("Healing Surge: After Heal Attempted: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true)))
                                ),
                            new Action( r => Logger.WriteDebug("Healing Surge: After Heal Skipped: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true)))
                            )
                        ),

                    new Decorator(
                        ret => Me.Combat,

                        new PrioritySelector(

                            Spell.BuffSelf("Astral Shift", ret => Me.HealthPercent < ShamanSettings.AstralShiftPercent || Common.StressfulSituation),
                            Spell.BuffSelf(WoWTotem.StoneBulwark.ToSpellId(), ret => !Me.IsMoving && (Common.StressfulSituation || Me.HealthPercent < ShamanSettings.StoneBulwarkTotemPercent && !Totems.Exist(WoWTotem.EarthElemental))),
                            Spell.BuffSelf("Shamanistic Rage", ret => Me.HealthPercent < ShamanSettings.ShamanisticRagePercent || Common.StressfulSituation),

                            Spell.HandleOffGCD( 
                                Spell.BuffSelf("Ancestral Guidance", 
                                    ret => Me.HealthPercent < ShamanSettings.SelfAncestralGuidance 
                                        && Me.GotTarget()
                                        && Me.CurrentTarget.TimeToDeath() > 8 
                                    )
                                ),

                            new Decorator(
                                req => !Me.IsMoving && ShamanSettings.SelfHealingStreamTotem > 0 && !Totems.Exist(WoWTotemType.Water),
                                Spell.BuffSelf(
                                    WoWTotem.HealingStream.ToSpellId(), 
                                    req => 
                                    {
                                        int count = Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count(u => !u.IsTrivial());
                                        if (count > 1)
                                            return true;
                                        if (count > 0 && Me.HealthPercent < ShamanSettings.SelfHealingStreamTotem)
                                        {
                                            if (!Me.GotTarget() || Me.CurrentTarget.IsPlayer || Me.CurrentTarget.TimeToDeath() > 5)
                                            {
                                                return true;
                                            }
                                            if (Me.HealthPercent < (ShamanSettings.SelfHealingStreamTotem / 2))
                                            {
                                                return true;
                                            }
                                        }
                                        return false;
                                    }
                                    )
                                ),

                            // save myself if possible
                            new Decorator(
                                ret => (!Me.IsInGroup() || Battlegrounds.IsInsideBattleground)
                                    && (!Me.IsMoving || Me.HasAura("Maelstrom Weapon", 5) || !Spell.IsSpellOnCooldown("Ancestral Swiftness"))
                                    && Me.HealthPercent < ShamanSettings.SelfAncestralSwiftnessHeal
                                    && Me.PredictedHealthPercent(includeMyHeals: true) < ShamanSettings.SelfAncestralSwiftnessHeal,
                                new PrioritySelector(
                                    Spell.HandleOffGCD( Spell.BuffSelf("Ancestral Swiftness") ),
                                    new PrioritySelector(
                                        new Sequence(
                                            ctx => (float)Me.HealthPercent,
                                            new Action(r => Logger.WriteDebug("Healing Surge: {0:F1}% Predict:{1:F1}% and moving:{2}, cancast:{3}", (float)r, Me.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving, Spell.CanCastHack("Healing Surge", Me, skipWowCheck: false))),
                                            Spell.Cast(
                                                "Healing Surge",
                                                mov => true,
                                                on => Me,
                                                req => true,
                                                cancel => Me.HealthPercent > 85
                                                ),
                                            new WaitContinue(TimeSpan.FromMilliseconds(500), until => !Me.IsCasting && Me.HealthPercent > (1.1 * ((float)until)), new ActionAlwaysSucceed()),
                                            new Action(r => Logger.WriteDebug("Healing Surge: After Heal Attempted: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true)))
                                            ),
                                        new Action(r => Logger.WriteDebug("Healing Surge: After Heal Skipped: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true)))
                                        )
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

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.OffHealSettings.AncestralSwiftness) + 500,
                String.Format("Oh Shoot Heal @ {0}%", ShamanSettings.OffHealSettings.AncestralSwiftness),
                null,
                new Decorator(
                    ret => (Me.Combat || ((WoWUnit)ret).Combat) && ((WoWUnit)ret).PredictedHealthPercent() < ShamanSettings.OffHealSettings.AncestralSwiftness,
                    new PrioritySelector(
                        Spell.HandleOffGCD(Spell.BuffSelf("Ancestral Swiftness")),
                        Spell.Cast("Healing Surge", on => (WoWUnit)on)
                        )
                    )
                );

            #endregion

            #region AoE Heals

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.OffHealSettings.HealingStreamTotem) + 300,
                string.Format("Healing Stream Totem @ {0}%", ShamanSettings.OffHealSettings.HealingStreamTotem),
                "Healing Stream Totem",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    Spell.Cast(
                        "Healing Stream Totem",
                        on => (!Me.Combat || Totems.Exist(WoWTotemType.Water)) ? null : HealerManager.Instance.TargetList.FirstOrDefault(p => p.PredictedHealthPercent() < ShamanSettings.OffHealSettings.HealingStreamTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingStream))
                        )
                    )
                );

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.OffHealSettings.HealingRain) + 200,
                string.Format("Healing Rain @ {0}% Count={1}", ShamanSettings.OffHealSettings.HealingRain, ShamanSettings.OffHealSettings.MinHealingRainCount),
                "Healing Rain",
                Spell.CastOnGround("Healing Rain", on => Restoration.GetBestHealingRainTarget(), req => HealerManager.Instance.TargetList.Count() > 1, false)
                );

            #endregion

            #region Single Target Heals

            behavs.AddBehavior(HealerManager.HealthToPriority(ShamanSettings.OffHealSettings.HealingSurge),
                string.Format("Healing Surge @ {0}%", ShamanSettings.OffHealSettings.HealingSurge),
                "Healing Surge",
                Spell.Cast("Healing Surge",
                    mov => true,
                    on => (WoWUnit)on,
                    req => ((WoWUnit)req).PredictedHealthPercent(includeMyHeals: true) < ShamanSettings.OffHealSettings.HealingSurge,
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
                    ret => ret != null && (Me.Combat || ((WoWUnit)ret).Combat || ((WoWUnit)ret).PredictedHealthPercent() <= 99),

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


        public static Composite CastElementalBlast( UnitSelectionDelegate onUnit = null, SimpleBooleanDelegate requirements = null, SimpleBooleanDelegate cancel = null)
        {
            const string ELEMENTAL_BLAST = "Elemental Blast";
            UnitSelectionDelegate ondel = onUnit ?? (o => Me.CurrentTarget);
            SimpleBooleanDelegate reqdel = requirements ?? (r => true);
            SimpleBooleanDelegate candel = cancel ?? (c => false);

            // we do a doublecast check with Me as the target
            // .. since the buff appears on us -- a different model requiring custom handling below
            return new Decorator(
                req => {
                    if ( Spell.DoubleCastContains(Me, ELEMENTAL_BLAST))
                        return false;
                    if ( !Me.HasAuraExpired(ELEMENTAL_BLAST, 0, myAura:true))
                        return false;
                    return true;
                    },
                new Sequence(
                    Spell.Cast(ELEMENTAL_BLAST, ondel, reqdel, candel),
                    new Action( r => Spell.UpdateDoubleCast(ELEMENTAL_BLAST, Me, 750))
                    )
                );
        }
    }

    public enum ShamanTalents
    {
#if PRE_WOD
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
#else

        NaturesGuardian = 1,
        StoneBulwarkTotem,
        AstralShift,

        FrozenPower,
        EarthgrabTotem,
        WindwalkTotem,

        CallOfTheElements,
        TotemicPersistence,
        TotemicProjection,

        ElementalMastery,
        AncestralSwiftness,
        EchoOfTheElements,

        RushingStreams,
        AncestralGuidance,
        Conductivity,

        UnleashedFury,
        ElementalBlast,
        PrimalElementalist,

        ElementalFusion,
        CloudburstTotem = ElementalFusion,
        StormElementalTotem,
        LiquidMagma,
        HighTide = LiquidMagma

#endif
    }

}