#define FIND_LOWEST_AT_THE_MOMENT

using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Styx.WoWInternals.World;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.WoWInternals;
using CommonBehaviors.Actions;
using System.Collections.Generic;
using System.Drawing;
using Styx.Common;

namespace Singular.ClassSpecific.Priest
{
    public class Disc
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PriestSettings PriestSettings { get { return SingularSettings.Instance.Priest(); } }
        public static bool HasTalent(PriestTalents tal) { return TalentManager.IsSelected((int)tal); }

        const int PriSurgeOfLight = 800;
        const int PriBorrowedTime = 700;
        const int PriEmergencyBase = 600;
        const int PriHighBase = 500;
        const int PriSaveEssential = 400;
        const int PriHighAtone = 300;
        const int PriAoeBase = 200;
        const int PriSingleBase = 100;
        const int PriLowBase = 0;

        const int SURGE_OF_LIGHT = 114255;
        const int SPIRIT_SHELL_SPELL = 109964;
        const int SPIRIT_SHELL_ABSORB = 114908;

        #region Spirit Shell Support

        private static bool IsSpiritShellEnabled() { return Me.HasAura(SPIRIT_SHELL_SPELL); }
        private static bool HasSpiritShellAbsorb(WoWUnit u) { return u.HasAura(SPIRIT_SHELL_ABSORB); }

        private static bool HasReflectiveShield = false;


        private static bool SkipForSpiritShell(WoWUnit u)
        {
            if (IsSpiritShellEnabled())
                return HasSpiritShellAbsorb(u);
            return false;
        }

        private static bool CanWePwsUnit(WoWUnit unit)
        {
            return unit != null && (!unit.HasAura("Weakened Soul") || Me.HasAura("Divine Insight"));
        }

        #endregion

        [Behavior(BehaviorType.Initialize, WoWClass.Priest, WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscInitialize()
        {
            HasReflectiveShield = TalentManager.HasGlyph("Reflective Shield");
            if (HasReflectiveShield)
                Logger.Write(LogColor.Init, "[Glyph of Reflective Shield] will prioritize Power Word: Shield on self if we are being attacked");
            return null;
        }

        [Behavior(BehaviorType.Rest, WoWClass.Priest, WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscRest()
        {
            return new PrioritySelector(
                new Decorator(
                    req => SingularRoutine.CurrentWoWContext != WoWContext.Normal,
                    CreateDiscDiagnosticOutputBehavior("REST")
                    ),

                CreateSpiritShellCancel(),

                // Heal self before resting. There is no need to eat while we have 100% mana
                CreateDiscHealOnlyBehavior(true, false),
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour(SingularRoutine.CurrentWoWContext == WoWContext.Normal ? "Plea" : null, "Resurrection"),
                // Make sure we're healing OOC too!
                CreateDiscHealOnlyBehavior(false, false),
                // now buff our movement if possible
                Common.CreatePriestMovementBuffOnTank("Rest")
                );
        }

        //private static WoWUnit _moveToHealTarget = null;
        //private static WoWUnit _lastMoveToTarget = null;

        // temporary lol name ... will revise after testing
        public static Composite CreateDiscHealOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                return new ActionAlwaysFail();

            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();
            int cancelHeal = (int)Math.Max(SingularSettings.Instance.IgnoreHealTargetsAboveHealth, PriestSettings.DiscHeal.Heal);
            cancelHeal = (int)Math.Max(cancelHeal, PriestSettings.DiscHeal.FlashHeal);

            Logger.WriteDebugInBehaviorCreate("Priest Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);

            int flashHealHealth = PriestSettings.DiscHeal.FlashHeal;
            if (!SpellManager.HasSpell("Heal"))
            {
                flashHealHealth = Math.Max(flashHealHealth, PriestSettings.DiscHeal.Heal);
            }


            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
            {
                int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
                behavs.AddBehavior(dispelPriority, "Dispel", null, Common.CreatePriestDispelBehavior());
            }

            #region Surge of Light

            behavs.AddBehavior(99 + PriSurgeOfLight, "Surge of Light: Dont Waste", "Flash of Light",
                new Decorator(
                    req => Me.GetAuraStacks(SURGE_OF_LIGHT) >= 2 || Me.GetAuraTimeLeft(SURGE_OF_LIGHT) < TimeSpan.FromSeconds(2),
                    Spell.Cast("Flash of Light", on => (WoWUnit)on)
                    )
                );

            #endregion

            #region Keep Up Borrowed Time

            behavs.AddBehavior(99 + PriBorrowedTime, "Power Word: Shield for Borrowed Time", "Power Word: Shield",
                new Decorator(
                    req => Me.Combat && Me.GetAuraTimeLeft("Borrowed Time") == TimeSpan.Zero,
                    new PrioritySelector(
                        Spell.Buff(
                            "Power Word: Shield",
                            on => Group.Tanks
                                .Where(u => u.IsAlive && u.SpellDistance() < 40 && CanWePwsUnit(u) && Spell.CanCastHack("Power Word: Shield", u))
                                .FirstOrDefault(),
                            req =>
                            {
                                Logger.Write(LogColor.Hilite, "^Borrowed Time: shield Tank for haste buff");
                                return true;
                            }),
                        Spell.Buff(
                            "Power Word: Shield",
                            on => HealerManager.Instance.TargetList
                                .Where(u => u.IsAlive && u.SpellDistance() < 40 && CanWePwsUnit(u) && Spell.CanCastHack("Power Word: Shield", u))
                                .OrderBy(u => u.CurrentHealth)
                                .FirstOrDefault(),
                            req =>
                            {
                                Logger.Write(LogColor.Hilite, "^Borrowed Time: shield for haste buff");
                                return true;
                            })
                        )
                    )
                );

            #endregion

            #region Save the Group

            behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.PainSuppression) + PriEmergencyBase, "Pain Suppression @ " + PriestSettings.DiscHeal.PainSuppression + "%", "Pain Suppression",
                new Decorator(
                    req => Me.Combat,
                    Spell.Cast("Pain Suppression",
                        mov => false,
                        on => (WoWUnit) on,
                        req => ((WoWUnit)req).IsPlayer
                            && ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.PainSuppression
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.PowerWordBarrier) + PriEmergencyBase, "Power Word: Barrier @ " + PriestSettings.DiscHeal.PowerWordBarrier + "%", "Power Word: Barrier",
                new Decorator(
                    ret => Me.Combat && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
                    new PrioritySelector(
                        context => GetBestPowerWordBarrierTarget(),
                        new Decorator(
                            ret => ret != null,
                            new PrioritySelector(
                                new Sequence(
                                    new Action(r => Logger.WriteDebug("PW:B - attempting cast")),
                                    Spell.CastOnGround("Power Word: Barrier", on => (WoWUnit)on, req => true, false)
                                    ),
                                new Action( ret => {
                                    if (ret != null)
                                    {
                                        if (!((WoWUnit)ret).IsValid)
                                            Logger.WriteDebug("PW:B - FAILED - Healing Target object became invalid");
                                        else if (((WoWUnit)ret).Distance > 40)
                                            Logger.WriteDebug("PW:B - FAILED - Healing Target moved out of range");
                                        else if (!Spell.CanCastHack("Power Word: Barrier"))
                                            Logger.WriteDebug("PW:B - FAILED - Spell.CanCastHack() said NO to Power Word: Barrier");
                                        else if (GameWorld.IsInLineOfSpellSight(StyxWoW.Me.GetTraceLinePos(), ((WoWUnit)ret).Location))
                                            Logger.WriteDebug("PW:B - FAILED - Spell.CanCastHack() unit location not in Line of Sight");
                                        else if (Spell.IsSpellOnCooldown("Power Word: Barrier"))
                                            Logger.WriteDebug("PW:B - FAILED - Power Word: Barrier is on cooldown");
                                        else
                                            Logger.WriteDebug("PW:B - Something FAILED with Power Word: Barrier cast sequence (target={0}, h={1:F1}%, d={2:F1} yds, spellmax={3:F1} yds, cooldown={4})",
                                                ((WoWUnit)ret).SafeName(),
                                                ((WoWUnit)ret).HealthPercent,
                                                ((WoWUnit)ret).Distance,
                                                Spell.ActualMaxRange("Power Word: Barrier", (WoWUnit)ret),
                                                Spell.IsSpellOnCooldown("Power Word: Barrier")
                                                );
                                    }
                                    return RunStatus.Failure;
                                    })
                                )
                            )
                        )
                    )
                );


            #endregion

            #region Tank Buffing

            if (PriestSettings.DiscHeal.PrayerOfHealing != 0)
                behavs.AddBehavior(HealthToPriority(99) + PriHighBase, "Spirit Shell - Group MinCount: " + PriestSettings.DiscHeal.CountPrayerOfHealing, "Prayer of Healing",
                    new Decorator(
                        ret => IsSpiritShellEnabled(),
                        Spell.Cast("Prayer of Healing", on => {
                            WoWUnit unit = HealerManager.GetBestCoverageTarget("Prayer of Healing", 101, 40, 30, PriestSettings.DiscHeal.CountPrayerOfHealing, req => !HasSpiritShellAbsorb((WoWUnit)req));
                            if (unit != null && Spell.CanCastHack("Prayer of Healing", unit, skipWowCheck: true))
                            {
                                Logger.WriteDebug("Buffing Spirit Shell with Prayer of Healing on Group: {0}", unit.SafeName());
                                return unit;
                            }
                            return null;
                            })
                        )
                    );

            behavs.AddBehavior(HealthToPriority(98) + PriHighBase, "Spirit Shell - Tank", "Spirit Shell",
                new Decorator(
                    req => IsSpiritShellEnabled(),
                    Spell.Cast("Heal", on =>
                    {
                        WoWUnit unit = Group.Tanks.Where(t => t.IsAlive && t.Combat && !HasSpiritShellAbsorb(t) && t.SpellDistance() < 40).OrderBy( a => a.Distance).FirstOrDefault();
                        if (unit != null && Spell.CanCastHack("Heal", unit, skipWowCheck: true))
                        {
                            Logger.WriteDebug("Buffing Spirit Shell with Heal on TANK: {0}", unit.SafeName());
                            return unit;
                        }
                        return null;
                    })
                    )
                );

            behavs.AddBehavior(HealthToPriority(97) + PriHighBase, "Power Word: Shield - Tank", "Power Word: Shield",
                new Decorator(
                    req => Me.Combat,
                    Spell.Buff(
                        "Power Word: Shield",
                        on => Group.Tanks
                            .Where(u => u.IsAlive && CanWePwsUnit(u) && Spell.CanCastHack("Power Word: Shield", u))
                            .FirstOrDefault()
                        )
                    )
                );


            #endregion

            #region Save the Essential Party Members

            const int SaveEssential = 30;
            behavs.AddBehavior(HealthToPriority(SaveEssential) + PriSaveEssential, "Save Essential Target below " + SaveEssential + "%", "Flash Heal",
                new Decorator(
                    req => Me.Combat,
                    new PrioritySelector(
                        ctx => HealerManager.FindLowestHealthEssentialTarget(),

                        Spell.HandleOffGCD( Spell.Buff( "Power Infusion", on => Me, req => req != null, gcd: HasGcd.No) ),

                        Spell.Buff(
                            "Power Word: Shield",
                            on => (WoWUnit)on,
                            req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.PowerWordShield
                                && CanWePwsUnit((WoWUnit)req)
                            ),
                        Spell.Buff(
                            "Saving Grace",
                            on => (WoWUnit)on,
                            req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.SavingGrace
                                && CanWePwsUnit((WoWUnit)req)
                            )
                        )
                    )
                );
            #endregion

            #region Atonement Only

            // only Atonement healing if above Health %
            if (AddAtonementBehavior() && PriestSettings.DiscHeal.AtonementAbovePercent > 0)
            {
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.AtonementAbovePercent) + PriHighAtone, "Atonement Above " + PriestSettings.DiscHeal.AtonementAbovePercent + "%", "Atonement",
                    new Decorator(
                        req => (Me.Combat || SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                            && !HealerManager.Instance.TargetList.Any(h => h.HealthPercent < PriestSettings.DiscHeal.AtonementCancelBelowHealthPercent && h.SpellDistance() < 50)
                            && HealerManager.Instance.TargetList.Count(h => h.HealthPercent < PriestSettings.DiscHeal.AtonementAbovePercent) < PriestSettings.DiscHeal.AtonementAboveCount,
                        new PrioritySelector(
                            HealerManager.CreateAttackEnsureTarget(),

                            CreateDiscAtonementMovement(),

                            new Decorator(
                                req => Unit.ValidUnit(Me.CurrentTarget),
                                new PrioritySelector(
                                    Movement.CreateFaceTargetBehavior(),
                                    new Decorator(
                                        req => Me.IsSafelyFacing( Me.CurrentTarget, 150) && Me.ManaPercent >= PriestSettings.DiscHeal.AtonementCancelBelowManaPercent,
                                        new PrioritySelector(
                                            Spell.Cast("Penance", mov => true, on => Me.CurrentTarget, req => true, cancel => false),
                                            Spell.Cast("Smite", mov => true, on => Me.CurrentTarget, req => PriestSettings.DiscHeal.AtonementUseSmite, cancel => CancelAtonementDPS())
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    );
            }

            #endregion

            #region AoE Heals

            int maxDirectHeal = Math.Max(PriestSettings.DiscHeal.FlashHeal, PriestSettings.DiscHeal.Heal);

            if (PriestSettings.DiscHeal.DiscLevel90Talent != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.DiscLevel90Talent) + PriAoeBase, "Halo @ " + PriestSettings.DiscHeal.DiscLevel90Talent + "% MinCount: " + PriestSettings.DiscHeal.CountLevel90Talent, "Halo",
                    new Decorator(
                        ret => SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || SingularRoutine.CurrentWoWContext == WoWContext.Instances,
                        new Decorator(
                            ret => ret != null && HealerManager.Instance.TargetList.Count(u => u.IsAlive && u.HealthPercent < PriestSettings.DiscHeal.DiscLevel90Talent && u.Distance < 30) >= PriestSettings.DiscHeal.CountLevel90Talent
                                && Spell.CanCastHack("Halo", (WoWUnit)ret),
                            new PrioritySelector(
                                Spell.HandleOffGCD(Spell.BuffSelf("Archangel", req => ((WoWUnit)req) != null && Me.HasAura("Evangelism", 5))),
                                Spell.CastOnGround("Halo", on => (WoWUnit)on, req => true)
                                )
                            )
                        )
                    );

            if (PriestSettings.DiscHeal.DiscLevel90Talent != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.DiscLevel90Talent) + PriAoeBase, "Cascade @ " + PriestSettings.DiscHeal.DiscLevel90Talent + "% MinCount: " + PriestSettings.DiscHeal.CountLevel90Talent, "Cascade",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        new PrioritySelector(
                            context => HealerManager.GetBestCoverageTarget("Cascade", PriestSettings.DiscHeal.DiscLevel90Talent, 40, 30, PriestSettings.DiscHeal.CountLevel90Talent),
                            new Decorator(
                                ret => ret != null && Spell.CanCastHack("Cascade", (WoWUnit) ret),
                                new PrioritySelector(
                                    Spell.HandleOffGCD(Spell.BuffSelf("Archangel", req => Me.HasAura("Evangelism", 5))),
                                    Spell.Cast("Cascade", mov => true, on => (WoWUnit)on, req => true)
                                    )
                                )
                            )
                        )
                    );

            if (PriestSettings.DiscHeal.HolyNova != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.HolyNova) + PriAoeBase, "Prayer of Healing @ " + PriestSettings.DiscHeal.PrayerOfHealing + "% MinCount: " + PriestSettings.DiscHeal.CountHolyNova, "Holy Nova",
                    new Decorator(
                        req => Me.Combat && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid) && Me.IsMoving,
                        Spell.Cast(
                            "Holy Nova",
                            on => (WoWUnit)on,
                            req => PriestSettings.DiscHeal.CountHolyNova <= HealerManager.Instance.TargetList.Count( u => u.HealthPercent.Between(1, PriestSettings.DiscHeal.HolyNova) && u.SpellDistance() < 12)
                            )
                        )
                    );

            if (PriestSettings.DiscHeal.PrayerOfHealing != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.PrayerOfHealing) + PriAoeBase, "Prayer of Healing @ " + PriestSettings.DiscHeal.PrayerOfHealing + "% MinCount: " + PriestSettings.DiscHeal.CountPrayerOfHealing, "Prayer of Healing",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        new PrioritySelector(
                            context => HealerManager.GetBestCoverageTarget("Prayer of Healing", PriestSettings.DiscHeal.PrayerOfHealing, 40, 30, PriestSettings.DiscHeal.CountPrayerOfHealing),
                            CastBuffsBehavior("Prayer of Healing"),
                            Spell.Cast("Prayer of Healing", on => (WoWUnit)on)
                            )
                        )
                    );

            #endregion

            #region Direct Heals

            if (HasReflectiveShield)
            {
                behavs.AddBehavior(100 + PriSingleBase, "Power Word: Shield Self (Glyph of Reflective Shield)", "Power Word: Shield",
                    Spell.BuffSelf("Power Word: Shield", req => CanWePwsUnit(Me) && Unit.NearbyUnitsInCombatWithUsOrOurStuff.Any())
                    );
            }

            if (PriestSettings.DiscHeal.PowerWordShield != 0)
            {
                behavs.AddBehavior(99 + PriSingleBase, "Power Word: Shield @ " + PriestSettings.DiscHeal.PowerWordShield + "%", "Power Word: Shield",
                    Spell.Buff("Power Word: Shield", on => (WoWUnit) on, req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.PowerWordShield && CanWePwsUnit((WoWUnit) req))
                    );
            }

            if (PriestSettings.DiscHeal.Penance != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.Penance) + PriSingleBase, "Penance @ " + PriestSettings.DiscHeal.Penance + "%", "Penance",
                new Decorator(
                    req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.Penance,
                    new PrioritySelector(
                        CastBuffsBehavior("Penance"),
                        Spell.Cast("Penance",
                            mov => true,
                            on => (WoWUnit)on,
                            req => HasTalent(PriestTalents.ThePenitent),
                            cancel => false
                            )
                        )
                    )
                );

            if (PriestSettings.DiscHeal.Plea != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.Plea) + PriSingleBase, "Plea @ " + PriestSettings.DiscHeal.Plea + "%", "Plea",
                new Decorator(
                    req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.Plea,
                    new PrioritySelector(
                        CastBuffsBehavior("Plea"),
                        Spell.Cast("Plea",
                            mov => true,
                            on => (WoWUnit)on,
                            req => true,
                            cancel => false
                            )
                        )
                    )
                );

            #endregion

            behavs.OrderBehaviors();

            if (selfOnly == false && CompositeBuilder.CurrentBehaviorType == BehaviorType.Combat)
                behavs.ListBehaviors();

            return new PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.FindHighestPriorityTarget(), // HealerManager.Instance.FirstUnit,

                // use gcd/cast time to choose dps target and face if needed
                new Decorator(
                    req => Me.Combat && (Spell.IsGlobalCooldown() || Spell.IsCastingOrChannelling()),
                    new PrioritySelector(
                        HealerManager.CreateAttackEnsureTarget(),
                        Movement.CreateFaceTargetBehavior(waitForFacing: false)
                        )
                    ),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && ret != null,
                    behavs.GenerateBehaviorTree()
                    )

#if OLD_TANK_FOLLOW_CODE
                ,
                new Decorator(
                    ret => moveInRange,
                    Movement.CreateMoveToUnitBehavior(
                        ret => Battlegrounds.IsInsideBattleground ? (WoWUnit)ret : Group.Tanks.Where(a => a.IsAlive).OrderBy(a => a.Distance).FirstOrDefault(),
                        35f
                        )
                    )
#endif
                );
        }

        private static bool AddAtonementBehavior()
        {
            return CompositeBuilder.CurrentBehaviorType == BehaviorType.Heal
                || CompositeBuilder.CurrentBehaviorType == BehaviorType.PullBuffs
                || CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull
                || CompositeBuilder.CurrentBehaviorType == BehaviorType.CombatBuffs
                || CompositeBuilder.CurrentBehaviorType == BehaviorType.Combat;
        }

        public static bool CancelAtonementDPS()
        {
            // always let DPS casts while solo complete
            WoWContext ctx = SingularRoutine.CurrentWoWContext;
            if (ctx == WoWContext.Normal)
                return false;

            bool castInProgress = Spell.IsCastingOrChannelling();
            if (!castInProgress)
            {
                return false;
            }

            // allow casts that are close to finishing to finish regardless
            if (castInProgress && Me.CurrentCastTimeLeft.TotalMilliseconds < 333 && Me.CurrentChannelTimeLeft.TotalMilliseconds < 333)
            {
                Logger.WriteDebug("CancelAtonementDPS: suppressing /cancel since less than 333 ms remaining");
                return false;
            }

            // use a window less than actual to avoid cast/cancel/cast/cancel due to mana hovering at setting level
            if (Me.ManaPercent < (PriestSettings.DiscHeal.AtonementCancelBelowManaPercent - 3))
            {
                Logger.Write(LogColor.Hilite, "^Atonement: cancel since mana={0:F1}% below min={1}%", Me.ManaPercent, PriestSettings.DiscHeal.AtonementCancelBelowManaPercent);
                return true;
            }

            // check if group health has dropped below setting
            WoWUnit low = HealerManager.FindLowestHealthTarget();
            if (low != null && low.HealthPercent < PriestSettings.DiscHeal.AtonementCancelBelowHealthPercent)
            {
                Logger.Write(LogColor.Hilite, "^Atonemet: cancel since {0} @ {1:F1}% fell below minimum {2}%", low.SafeName(), low.HealthPercent, SingularSettings.Instance.HealerCombatMinHealth);
                return true;
            }

            return false;
        }

        private static int HealthToPriority(int nHealth)
        {
            return nHealth == 0 ? 0 : 200 - nHealth;
        }


        [Behavior(BehaviorType.Heal, WoWClass.Priest, WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscHeal()
        {
            return new Decorator(
                ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                new PrioritySelector(
                    Spell.BuffSelf("Power Word: Shield", ret => Me.Combat && Me.HealthPercent < PriestSettings.PowerWordShield && CanWePwsUnit( Me)),

                    Common.CreatePsychicScreamBehavior(),

                    Spell.Cast("Plea",
                        ctx => Me,
                        ret => Me.HealthPercent <= PriestSettings.ShadowHeal && !SkipForSpiritShell(Me)),

                    Spell.Cast("Plea",
                        ctx => Me,
                        ret => !Me.Combat && Me.PredictedHealthPercent(includeMyHeals: true) <= 85 && !SkipForSpiritShell(Me))
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Priest, WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(
                    req => !Unit.IsTrivial(Me.CurrentTarget),
                    new PrioritySelector(
                        Common.CreateFadeBehavior(),

                        Spell.BuffSelf("Power Word: Shield",
                            req => HasReflectiveShield
                                && SingularRoutine.CurrentWoWContext == WoWContext.Normal
                            ),

                        Common.CreateShadowfiendBehavior(),

                        Common.CreateLeapOfFaithBehavior(),

                        // Spell.Cast("Power Word: Solace", req => Me.GotTarget() && Unit.ValidUnit(Me.CurrentTarget) && Me.IsSafelyFacing( Me.CurrentTarget) && Me.CurrentTarget.InLineOfSpellSight )
                // Spell.Cast(129250, req => Me.GotTarget() && Unit.ValidUnit(Me.CurrentTarget) && Me.IsSafelyFacing(Me.CurrentTarget) && Me.CurrentTarget.InLineOfSpellSight),
                        Spell.CastHack("Schism", req => Me.GotTarget() && Unit.ValidUnit(Me.CurrentTarget) && Me.IsSafelyFacing(Me.CurrentTarget) && Me.CurrentTarget.InLineOfSpellSight)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscCombatNormalPull()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMediumRange(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateDiscDiagnosticOutputBehavior(CompositeBuilder.CurrentBehaviorType.ToString()),

                        Spell.BuffSelf("Power Word: Shield", ret => PriestSettings.UseShieldPrePull && CanWePwsUnit(Me)),
                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Dispelling.CreatePurgeEnemyBehavior("Dispel Magic"),

                        // slow pull if no aggro and not competing for mobs
                        Spell.Cast(
                            "Smite",
                            mov => true,
                            on => Me.CurrentTarget,
                            req => !ObjectManager.GetObjectsOfType<WoWUnit>(true,false).Any( u => u.Aggro || (u.IsPlayer && !u.IsMe && u.DistanceSqr < 60 * 60))
                            ),

                        Spell.Buff("Shadow Word: Pain", req => Me.CurrentTarget.HasAuraExpired("Shadow Word: Pain", 1) && Me.CurrentTarget.TimeToDeath(99) >= 8),
                        Spell.Buff("Shadow Word: Pain", true, on =>
                        {
                            WoWUnit unit = Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.Guid != Me.CurrentTargetGuid && u.IsTargetingMeOrPet && !u.HasMyAura("Shadow Word: Pain") && !u.IsCrowdControlled());
                            return unit;
                        }),
                        Spell.Cast("Penance", mov => true, on => Me.CurrentTarget, req => true, cancel => false),
                        Common.CreateHolyFireBehavior(),
                        Spell.Cast("Smite", mov => true, on => Me.CurrentTarget, req => true, cancel => false)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Priest, WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscCombatNormalCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMediumRange(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateDiscDiagnosticOutputBehavior(CompositeBuilder.CurrentBehaviorType.ToString()),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        // Artifact Weapon
                        // Light's Wrath notes:  UseDPSArtifactWeaponWhen.AtHighestDPSOpportunity would be good if the player has a larger number of attonement stacks.  However, this would only apply for dungeon context.
                        new Decorator(
                            ret => PriestSettings.UseArtifactOnlyInAoE && Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() > 1,
                            new PrioritySelector(
                                Spell.Cast("Light's Wrath", ret => PriestSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown)
                            )
                        ),
                        Spell.Cast("Light's Wrath", ret => !PriestSettings.UseArtifactOnlyInAoE && PriestSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown),

                        Dispelling.CreatePurgeEnemyBehavior("Dispel Magic"),
                        Spell.Buff("Shadow Word: Pain", req => Me.CurrentTarget.HasAuraExpired("Shadow Word: Pain", 1) && Me.CurrentTarget.TimeToDeath(99) >= 8),
                        Spell.Buff("Shadow Word: Pain", true, on =>
                        {
                            WoWUnit unit = Unit.NearbyUnfriendlyUnits
                                .FirstOrDefault(
                                    u => (u.TaggedByMe || u.Aggro)
                                        && u.Guid != Me.CurrentTargetGuid
                                        && u.IsTargetingMeOrPet
                                        && !u.HasMyAura("Shadow Word: Pain")
                                        && !u.IsCrowdControlled()
                                    );
                            return unit;
                        }),

                        Spell.Buff("Schism", on => Me.CurrentTarget),
                        Spell.Cast("Penance", mov => true, on => Me.CurrentTarget, req => true, cancel => false),
                        Spell.Cast("Smite", mov => true, on => Me.CurrentTarget, req => true, cancel => false)
                        )
                    )
                );
        }

        private static WoWUnit GetBestPowerWordBarrierTarget()
        {
#if ORIGINAL
            return Clusters.GetBestUnitForCluster(Unit.NearbyFriendlyPlayers.Cast<WoWUnit>(), ClusterType.Radius, 10f);
#else
            if (!Me.IsInGroup() || !Me.Combat)
                return null;

            if (!Spell.CanCastHack("Power Word: Barrier", Me, skipWowCheck: true))
            {
                // Logger.WriteDebug("GetBestHealingRainTarget: CanCastHack says NO to Healing Rain");
                return null;
            }

            // build temp list of targets that could use shield and are in range + radius
            List<WoWUnit> coveredTargets = HealerManager.Instance.TargetList
                .Where(u => u.IsAlive && u.DistanceSqr < 47 * 47 && u.HealthPercent < PriestSettings.DiscHeal.PowerWordBarrier)
                .ToList();

            // search all targets to find best one in best location to use as anchor for cast on ground
            var t = Unit.NearbyGroupMembersAndPets
                .Select(p => new
                {
                    Player = p,
                    Count = coveredTargets
                        .Count(pp => pp.IsAlive && pp.Location.DistanceSquared(p.Location) < 7 * 7)
                })
                .OrderByDescending(v => v.Count)
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (t != null && t.Count >= PriestSettings.DiscHeal.CountPowerWordBarrier)
            {
                Logger.WriteDebug("PowerWordBarrier Target:  found {0} with {1} nearby under {2}%", t.Player.SafeName(), t.Count, PriestSettings.DiscHeal.PowerWordBarrier);
                return t.Player;
            }

            return null;
#endif
        }

        public static Composite CreateSpiritShellCancel()
        {
            if (PriestSettings.DiscHeal.SpiritShell <= 0 || !SpellManager.HasSpell(SPIRIT_SHELL_SPELL))
                return new ActionAlwaysFail();

            return new Decorator(
                req => Me.HasMyAura("Spirit Shell") && !Me.Combat && !HealerManager.Instance.TargetList.Any(m => m.Combat && m.SpellDistance() < 50),
                new Action(r =>
                {
                    Logger.Write(LogColor.Cancel, "^Spirit Shell: cancel since not in Combat");
                    Me.CancelAura("Spirit Shell");
                })
                );
        }

        private static Composite CastBuffsBehavior(string castFor)
        {
            return new PrioritySelector(
                new Sequence(
                    Spell.BuffSelf("Power Infusion", req => Me.Combat && IsSpiritShellEnabled()),
                    new ActionAlwaysFail()
                    )
                );
        }

        private static Composite CreateDiscAtonementMovement()
        {
            // return Helpers.Common.EnsureReadyToAttackFromMediumRange();

            if (SingularSettings.Instance.StayNearTank)
                return Movement.CreateFaceTargetBehavior();

            return CreateDiscAtonementMovement();
        }

        #region Diagnostics

        private static DateTime _LastDiag = DateTime.MinValue;

        private static Composite CreateDiscDiagnosticOutputBehavior(string context)
        {
            return new Sequence(
                new Decorator(
                    ret => SingularSettings.Debug,
                    new ThrottlePasses(1, 1,
                        new Action(ret =>
                        {
                            WoWAura chakra = Me.GetAllAuras().Where(a => a.Name.Contains("Chakra")).FirstOrDefault();

                            string line = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, combat={3}, evang={4}, archa={5}, spish={6}, brwtim={7}",
                                context,
                                Me.HealthPercent,
                                Me.ManaPercent,
                                Me.Combat.ToYN(),
                                (int) Me.GetAuraStacks("Evangelism"),
                                (long) Me.GetAuraTimeLeft("Archangel").TotalMilliseconds,
                                (long) Me.GetAuraTimeLeft(SPIRIT_SHELL_SPELL).TotalMilliseconds,
                                (long) Me.GetAuraTimeLeft("Borrowed Time").TotalMilliseconds
                               );

                            if (HealerManager.Instance == null || HealerManager.Instance.FirstUnit == null || !HealerManager.Instance.FirstUnit.IsValid)
                                line += ", target=(null)";
                            else
                            {
                                WoWUnit healtarget = HealerManager.Instance.FirstUnit;
                                line += string.Format(", target={0} th={1:F1}%/{2:F1}% @ {3:F1} yds, combat={4}, tlos={5}, pw:s={6}, spish={7}",
                                    healtarget.SafeName(),
                                    healtarget.HealthPercent,
                                    healtarget.PredictedHealthPercent(includeMyHeals: true),
                                    healtarget.SpellDistance(),
                                    healtarget.Combat.ToYN(),
                                    healtarget.InLineOfSpellSight.ToYN(),
                                    (long)healtarget.GetAuraTimeLeft("Power Word: Shield").TotalMilliseconds,
                                    (long)healtarget.GetAuraTimeLeft(SPIRIT_SHELL_ABSORB).TotalMilliseconds
                                    );
                            }

                            Logger.WriteDebug(Color.LightYellow, line);
                            return RunStatus.Failure;
                        }))
                    )
                );
        }

        #endregion
    }
}
