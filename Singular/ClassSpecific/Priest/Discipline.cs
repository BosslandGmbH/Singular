#define FIND_LOWEST_AT_THE_MOMENT

using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
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

                Rest.CreateDefaultRestBehaviour(SingularRoutine.CurrentWoWContext == WoWContext.Normal ? "Flash Heal" : null, "Resurrection"),
                CreateDiscOutOfCombatHeal(),
                Common.CreatePriestMovementBuffOnTank("Rest")
                );
        }

        public static Composite CreateDiscOutOfCombatHeal()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown(),
                new PrioritySelector(ctx => HealerManager.FindLowestHealthTarget(),
                    Spell.Cast("Flash Heal", on => (WoWUnit)on, req => ((WoWUnit)req).HealthPercent < 95),
                    Spell.Cast("Plea", mov => false, on => (WoWUnit)on, req => ((WoWUnit)req).HealthPercent < 100)
                    )
            );
        }

        public static Composite CreateDiscHealOnlyBehavior()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                return new ActionAlwaysFail();

            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();
            int cancelHeal = (int)Math.Max(SingularSettings.Instance.IgnoreHealTargetsAboveHealth, PriestSettings.DiscHeal.Heal);
            cancelHeal = (int)Math.Max(cancelHeal, PriestSettings.DiscHeal.FlashHeal);

            Logger.WriteDebugInBehaviorCreate("Priest Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);

            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
            {
                int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
                behavs.AddBehavior(dispelPriority, "Dispel", null, Common.CreatePriestDispelBehavior());
            }

            #region Save the Group

            behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.PainSuppression) + PriEmergencyBase, "Pain Suppression @ " + PriestSettings.DiscHeal.PainSuppression + "%", "Pain Suppression",
                new Decorator(
                    req => Me.Combat,
                    Spell.Cast("Pain Suppression",
                        mov => false,
                        on => (WoWUnit)on,
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
                                new Action(ret => {
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

            behavs.AddBehavior(HealthToPriority(99) + PriHighBase, "Power Word: Shield - Tank", "Power Word: Shield",
                new Decorator(
                    req => Me.Combat,
                    Spell.Buff(
                        "Power Word: Shield",
                        on => Group.Tanks
                            .FirstOrDefault(u => u.IsAlive && CanWePwsUnit(u) && Spell.CanCastHack("Power Word: Shield", u))
                        )
                    )
                );

            behavs.AddBehavior(HealthToPriority(99) + PriHighBase, "Plea - Tank Attonement", "Plea",
                new Decorator(
                    req => Me.Combat,
                    Spell.Cast(
                        "Plea",
                        on => Group.Tanks
                            .FirstOrDefault(u => u.IsAlive && !u.HasActiveAura("Atonement") && Spell.CanCastHack("Plea", u))
                        )
                    )
                );


            #endregion

            #region Save the Highest Priority Targets
            const int SaveEssential = 30;
            behavs.AddBehavior(HealthToPriority(SaveEssential) + PriSaveEssential, "Save Highest Priority Target below " + SaveEssential + "%", "Shadow Mend",
                new Decorator(
                    req => Me.Combat,
                    new PrioritySelector(
                        ctx => HealerManager.FindHighestPriorityTarget(),

                        Spell.HandleOffGCD(Spell.Buff("Power Infusion", on => Me, req => req != null, gcd: HasGcd.No)),

                        Spell.Buff(
                            "Power Word: Shield",
                            on => (WoWUnit)on,
                            req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.PowerWordShield
                                && CanWePwsUnit((WoWUnit)req)
                            ),
                        Spell.Cast(
                            "Plea",
                            on => (WoWUnit)on,
                            req => !((WoWUnit)req).HasActiveAura("Atonement")
                            ),
                        Spell.Cast(
                            "Shadow Mend",
                            on => (WoWUnit)on,
                            req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.ShadowMend
                            )
                        )
                    )
                );
            #endregion

            #region AoE Heals
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
                                ret => ret != null && Spell.CanCastHack("Cascade", (WoWUnit)ret),
                                new PrioritySelector(
                                    Spell.HandleOffGCD(Spell.BuffSelf("Archangel", req => Me.HasAura("Evangelism", 5))),
                                    Spell.Cast("Cascade", mov => true, on => (WoWUnit)on, req => true)
                                    )
                                )
                            )
                        )
                    );

            if (PriestSettings.DiscHeal.PowerWordRadiance != 0)
                behavs.AddBehavior(99 + PriAoeBase, "Power Word: Radiance", "Power Word: Radiance",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        new PrioritySelector(
                            context => HealerManager.GetBestCoverageTarget("Power Word: Radiance", PriestSettings.DiscHeal.PowerWordRadiance, 40, 30, PriestSettings.DiscHeal.CountPowerWordRadiance, o => o != null && !((WoWUnit)o).HasActiveAura("Atonement")),
                            CastBuffsBehavior("Power Word: Radiance"),
                            Spell.Cast("Power Word: Radiance", on => (WoWUnit)on)
                            )
                        )
                    );

            #endregion

            #region Direct Heals

            if (PriestSettings.DiscHeal.Plea != 0)
            {
                behavs.AddBehavior(100 + PriSingleBase,
                    "Atonement Plea @ " + PriestSettings.DiscHeal.Plea + "%", "Plea",
                    new Decorator(
                        req => !((WoWUnit)req).HasActiveAura("Atonement"),
                        new PrioritySelector(
                            CastBuffsBehavior("Plea"),
                            Spell.Cast("Plea",
                                mov => false,
                                on => (WoWUnit)on,
                                req => true,
                                cancel => false
                            )
                        )
                    )
                );
            }

            if (HasReflectiveShield)
            {
                behavs.AddBehavior(100 + PriSingleBase, "Power Word: Shield Self (Glyph of Reflective Shield)", "Power Word: Shield",
                    Spell.BuffSelf("Power Word: Shield", req => CanWePwsUnit(Me) && Unit.NearbyUnitsInCombatWithUsOrOurStuff.Any())
                    );
            }

            if (PriestSettings.DiscHeal.PowerWordShield != 0)
            {
                behavs.AddBehavior(99 + PriSingleBase, "Power Word: Shield @ " + PriestSettings.DiscHeal.PowerWordShield + "%", "Power Word: Shield",
                    Spell.Buff("Power Word: Shield", on => (WoWUnit)on, req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.PowerWordShield && CanWePwsUnit((WoWUnit)req))
                    );
            }

            #endregion

            behavs.OrderBehaviors();
            behavs.ListBehaviors();

            return new PrioritySelector(
                ctx => HealerManager.FindHighestPriorityTarget(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && ret != null,
                    behavs.GenerateBehaviorTree()
                    )
                );
        }

        private static int HealthToPriority(int nHealth)
        {
            return nHealth == 0 ? 0 : 200 - nHealth;
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
                            req => !ObjectManager.GetObjectsOfType<WoWUnit>(true, false).Any(u => u.Aggro || (u.IsPlayer && !u.IsMe && u.DistanceSqr < 60 * 60))
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

        [Behavior(BehaviorType.Combat | BehaviorType.Heal, WoWClass.Priest, WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscCombatNormalCombat()
        {
            return new PrioritySelector(

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => Unit.NearbyGroupMembers.Any(m => m.IsAlive),
                    CreateDiscHealOnlyBehavior()
                    ),

                Helpers.Common.EnsureReadyToAttackFromMediumRange(),
                Movement.CreateFaceTargetBehavior(),

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
                                (int)Me.GetAuraStacks("Evangelism"),
                                (long)Me.GetAuraTimeLeft("Archangel").TotalMilliseconds,
                                (long)Me.GetAuraTimeLeft(SPIRIT_SHELL_SPELL).TotalMilliseconds,
                                (long)Me.GetAuraTimeLeft("Borrowed Time").TotalMilliseconds
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
