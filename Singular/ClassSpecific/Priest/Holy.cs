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

namespace Singular.ClassSpecific.Priest
{
    public class Holy
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PriestSettings PriestSettings { get { return SingularSettings.Instance.Priest(); } }
        public static bool HasTalent(PriestTalents tal) { return TalentManager.IsSelected((int)tal); }

        
        [Behavior(BehaviorType.Rest, WoWClass.Priest, WoWSpec.PriestHoly)]
        public static Composite CreateHolyRest()
        {
            return new PrioritySelector(
                CreateHolyDiagnosticOutputBehavior("REST"),

                // Heal self before resting. There is no need to eat while we have 100% mana
                CreateHolyHealOnlyBehavior(true, false),
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour( SingularRoutine.CurrentWoWContext == WoWContext.Normal ? "Flash Heal" : null, "Resurrection"),
                // Make sure we're healing OOC too!
                CreateHolyHealOnlyBehavior(false, false),
                // now buff our movement if possible
                Common.CreatePriestMovementBuffOnTank("Rest")
                );
        }

        private static WoWUnit _lightwellTarget = null;
        //private static WoWUnit _moveToHealTarget = null;
        //private static WoWUnit _lastMoveToTarget = null;

        public static Composite CreateHolyHealOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                return new ActionAlwaysFail();

            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();
            int cancelHeal = (int)Math.Max(SingularSettings.Instance.IgnoreHealTargetsAboveHealth, Math.Max(PriestSettings.HolyHeal.Heal, PriestSettings.HolyHeal.Renew ));

            Logger.WriteDebugInBehaviorCreate("Priest Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);

            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
            {
                int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
                behavs.AddBehavior(dispelPriority, "Dispel", null, Common.CreatePriestDispelBehavior());
            }

            #region Save the Group

            if (PriestSettings.HolyHeal.DivineHymn != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.HolyHeal.DivineHymn) + 300, "Divine Hymn @ " + PriestSettings.HolyHeal.DivineHymn + "% MinCount: " + PriestSettings.HolyHeal.CountDivineHymn, "Divine Hymn",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        context => HealerManager.GetBestCoverageTarget("Divine Hymn", PriestSettings.HolyHeal.DivineHymn, 0, 40, PriestSettings.HolyHeal.CountDivineHymn),
                        new Decorator(
                            ret => ret != null,
                            Spell.Cast("Divine Hymn", mov => false, on => (WoWUnit)on, req => true, cancel => false)
                            )
                        )
                    )
                );

            if (PriestSettings.HolyHeal.GuardianSpirit  != 0 )
            behavs.AddBehavior(HealthToPriority(PriestSettings.HolyHeal.GuardianSpirit) + 300, "Guardian Spirit @ " + PriestSettings.HolyHeal.GuardianSpirit + "%", "Guardian Spirit",
                Spell.Cast("Guardian Spirit",
                    mov => false,
                    on => (WoWUnit)on,
                    req => ((WoWUnit)req).HealthPercent < PriestSettings.HolyHeal.GuardianSpirit
                    )
                );


            #endregion

            #region Tank Buffing

            if (PriestSettings.HolyHeal.PowerWordShield  != 0 )
            behavs.AddBehavior(HealthToPriority(PriestSettings.HolyHeal.PowerWordShield) + 200, "Tank - Power Word: Shield @ " + PriestSettings.HolyHeal.PowerWordShield + "%", "Power Word: Shield",
                Spell.Cast("Power Word: Shield", on =>
                    {
                        WoWUnit unit = Common.GetBestTankTargetForPWS(PriestSettings.HolyHeal.PowerWordShield);
                        if (unit != null && Spell.CanCastHack("Power Word: Shield", unit, skipWowCheck: true))
                        {
                            Logger.WriteDebug("Buffing Power Word: Shield ON TANK: {0}", unit.SafeName());
                            return unit;
                        }
                        return null;
                    })
                );

            #endregion

            #region AoE Heals

            int maxDirectHeal = Math.Max(PriestSettings.HolyHeal.FlashHeal, PriestSettings.HolyHeal.Heal);

            if (maxDirectHeal  != 0 )
            behavs.AddBehavior(399, "Divine Insight - Prayer of Mending @ " + maxDirectHeal.ToString() + "%", "Prayer of Mending",
                new Decorator(
                    ret => (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid) && Me.HasAura("Divine Insight"),
                    new PrioritySelector(
                        context => HealerManager.GetBestCoverageTarget("Prayer of Mending", maxDirectHeal, 40, 20, 2),
                        new Decorator(
                            ret => ret != null,
                            Spell.Cast("Prayer of Mending", on => (WoWUnit)on, req => true)
                            )
                        )
                    )
                );

            // instant, so cast this first ALWAYS
            if (PriestSettings.HolyHeal.HolyWordSanctuary  != 0 )
            behavs.AddBehavior(398, "Holy Word: Sanctuary @ " + PriestSettings.HolyHeal.HolyWordSanctuary + "% MinCount: " + PriestSettings.HolyHeal.CountHolyWordSanctuary, "Holy Word: Sanctuary",
                new Decorator(
                    ret => Me.HasAura("Chakra: Sanctuary"),
                    new PrioritySelector(
                        context => HealerManager.GetBestCoverageTarget("Holy Word: Sanctuary", PriestSettings.HolyHeal.HolyWordSanctuary, 40, 8, PriestSettings.HolyHeal.CountHolyWordSanctuary),
                        new Decorator(
                            ret => ret != null,
                            Spell.CastOnGround("Holy Word: Sanctuary", on => (WoWUnit)on, req => true, false)
                            )
                        )
                    )
                );
/*
            behavs.AddBehavior(HealthToPriority(PriestSettings.HolyHeal.HolyLevel90Talent) + 200, "Divine Star", "Divine Star",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new Decorator(
                        ret => Clusters.GetClusterCount( Me, HealerManager.Instance.TargetList.Where(u => u.HealthPercent < PriestSettings.HolyHeal.CountLevel90Talent).ToList(), ClusterType.Path, 4 ) >= PriestSettings.HolyHeal.CountLevel90Talent,
                        Spell.Cast("Divine Star", on => (WoWUnit)on, req => true)
                        )
                    )
                );
*/
            if (PriestSettings.HolyHeal.HolyLevel90Talent  != 0 )
            behavs.AddBehavior(397, "Halo @ " + PriestSettings.HolyHeal.HolyLevel90Talent + "% MinCount: " + PriestSettings.HolyHeal.CountLevel90Talent, "Halo",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new Decorator(
                        ret => ret != null
                            && HealerManager.Instance.TargetList.Count(u => u.IsAlive && u.HealthPercent < PriestSettings.HolyHeal.HolyLevel90Talent && u.Distance < 30) >= PriestSettings.HolyHeal.CountLevel90Talent,
                        Spell.CastOnGround("Halo", on => (WoWUnit)on, req => true)
                        )
                    )
                );

            if (PriestSettings.HolyHeal.HolyLevel90Talent  != 0 )
            behavs.AddBehavior(397, "Cascade @ " + PriestSettings.HolyHeal.HolyLevel90Talent + "% MinCount: " + PriestSettings.HolyHeal.CountLevel90Talent, "Cascade",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        context => HealerManager.GetBestCoverageTarget("Cascade", PriestSettings.HolyHeal.HolyLevel90Talent, 40, 30, PriestSettings.HolyHeal.CountLevel90Talent),
                        new Decorator(
                            ret => ret != null,
                            Spell.Cast("Cascade", mov => true, on => (WoWUnit)on, req => true)
                            )
                        )
                    )
                );

            if (PriestSettings.HolyHeal.PrayerOfMending != 0)
            {
                if (!TalentManager.HasGlyph("Focused Mending"))
                {
                    behavs.AddBehavior(397, "Prayer of Mending @ " + PriestSettings.HolyHeal.PrayerOfMending + "% MinCount: " + PriestSettings.HolyHeal.CountPrayerOfMending, "Prayer of Mending",
                        new Decorator(
                            ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                            new PrioritySelector(
                                context => HealerManager.GetBestCoverageTarget("Prayer of Mending", PriestSettings.HolyHeal.PrayerOfMending, 40, 20, PriestSettings.HolyHeal.CountPrayerOfMending),
                                new Decorator(
                                    ret => ret != null,
                                    Spell.Cast("Prayer of Mending", on => (WoWUnit)on, req => true)
                                    )
                                )
                            )
                        );
                }
                else
                {
                    behavs.AddBehavior(397, "Prayer of Mending @ " + PriestSettings.HolyHeal.PrayerOfMending + "% (Glyph of Focused Mending)", "Prayer of Mending",
                        Spell.Cast("Prayer of Mending",
                            mov => true,
                            on => (WoWUnit)on,
                            req => !((WoWUnit)req).IsMe && ((WoWUnit)req).HealthPercent < PriestSettings.HolyHeal.PrayerOfMending && Me.HealthPercent < PriestSettings.HolyHeal.PrayerOfMending,
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                            )
                        );
                }
            }

            if ( PriestSettings.HolyHeal.BindingHeal != 0 )
            {
                if (!TalentManager.HasGlyph("Binding Heal"))
                {
                    behavs.AddBehavior(396, "Binding Heal @ " + PriestSettings.HolyHeal.BindingHeal + "% MinCount: 2", "Binding Heal",
                        Spell.Cast("Binding Heal",
                            mov => true,
                            on => (WoWUnit)on,
                            req => !((WoWUnit)req).IsMe && ((WoWUnit)req).HealthPercent < PriestSettings.HolyHeal.BindingHeal && Me.HealthPercent < PriestSettings.HolyHeal.BindingHeal,
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                            )
                        );
                }
                else
                {
                    behavs.AddBehavior(396, "Binding Heal (glyphed) @ " + PriestSettings.HolyHeal.BindingHeal + "% MinCount: 3", "Binding Heal",
                        Spell.Cast("Binding Heal",
                            mov => true,
                            on => (WoWUnit)on,
                            req => !((WoWUnit)req).IsMe 
                                && ((WoWUnit)req).HealthPercent < PriestSettings.HolyHeal.BindingHeal 
                                && Me.HealthPercent < PriestSettings.HolyHeal.BindingHeal
                                && HealerManager.Instance.TargetList.Any( h => h.IsAlive && !h.IsMe && h.Guid != ((WoWUnit)req).Guid && h.HealthPercent < PriestSettings.HolyHeal.BindingHeal && h.SpellDistance() < 20),
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                            )
                        );
                }
            }

            if (PriestSettings.HolyHeal.CircleOfHealing  != 0 )
            behavs.AddBehavior(395, "Circle of Healing @ " + PriestSettings.HolyHeal.CircleOfHealing + "% MinCount: " + PriestSettings.HolyHeal.CountCircleOfHealing, "Circle of Healing",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        context => HealerManager.GetBestCoverageTarget("Circle of Healing", PriestSettings.HolyHeal.CircleOfHealing, 40, 30, PriestSettings.HolyHeal.CountCircleOfHealing),
                        Spell.Cast("Circle of Healing", on => (WoWUnit)on)
                        )
                    )
                );

            if ( PriestSettings.HolyHeal.PrayerOfHealing != 0 )
            behavs.AddBehavior(394, "Prayer of Healing @ " + PriestSettings.HolyHeal.PrayerOfHealing + "% MinCount: " + PriestSettings.HolyHeal.CountPrayerOfHealing, "Prayer of Healing",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        context => HealerManager.GetBestCoverageTarget("Prayer of Healing", PriestSettings.HolyHeal.PrayerOfHealing, 40, 30, PriestSettings.HolyHeal.CountPrayerOfHealing),
                        Spell.Cast("Prayer of Healing", on => (WoWUnit)on)
                        )
                    )
                );

            #endregion

            /*          
HolyWordSanctuary       Holy Word: Sanctuary 
HolyWordSerenity        Holy Word: Serenity 
CircleOfHealing         Circle of Healing 
PrayerOfHealing         Prayer of Healing 
DivineHymn              Divine Hymn 
GuardianSpirit          Guardian Spirit 
VoidShift               Void Shift 
*/

            #region Direct Heals

            if (PriestSettings.HolyHeal.HolyWordSerenity != 0)
            behavs.AddBehavior(HealthToPriority(1) + 4, "Holy Word: Serenity @ " + PriestSettings.HolyHeal.HolyWordSerenity + "%", "Holy Word: Serenity",
                Spell.CastHack("Holy Word: Serenity",
                    on => (WoWUnit)on,
                    req => ((WoWUnit)req).HealthPercent < PriestSettings.HolyHeal.HolyWordSerenity
                    )
                );

            if (maxDirectHeal != 0)
                behavs.AddBehavior(HealthToPriority(1) + 3, "Surge of Light @ " + maxDirectHeal + "%", "Flash Heal",
                Spell.Cast("Flash Heal",
                    mov => true,
                    on => (WoWUnit)on,
                    req => Me.HasAura("Surge of Light") && ((WoWUnit)req).HealthPercent < maxDirectHeal
                    )
                );

            string cmt = "";
            int flashHealHealth = PriestSettings.HolyHeal.FlashHeal;
            if (!SpellManager.HasSpell("Heal"))
            {
                flashHealHealth = Math.Max(flashHealHealth, PriestSettings.HolyHeal.Heal);
                cmt = "(Adjusted for Heal)";
            }

            if (flashHealHealth != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.HolyHeal.FlashHeal), "Flash Heal @ " + flashHealHealth + "% " + cmt, "Flash Heal",
                Spell.Cast("Flash Heal",
                    mov => true,
                    on => (WoWUnit)on,
                    req => ((WoWUnit)req).HealthPercent < flashHealHealth,
                    cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                    )
                );

            if (PriestSettings.HolyHeal.Heal != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.HolyHeal.Heal), "Heal @ " + PriestSettings.HolyHeal.Heal + "%", "Heal",
                    Spell.Cast( sp => Me.GetAuraStacks("Serendipity") > 2 ? "Heal" : "Flash Heal",
                    mov => true,
                    on => (WoWUnit)on,
                    req => ((WoWUnit)req).HealthPercent < PriestSettings.HolyHeal.Heal,
                    cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                    )
                );

            #endregion

            #region Tank - Low Priority Buffs

            behavs.AddBehavior(HealthToPriority(101), "Tank - Refresh Renew w/ Serenity", "Chakra: Serenity",
                Spell.CastHack("Holy Word: Serenity", on =>
                    {
                        if (Me.HasAura("Chakra: Serenity"))
                        {
                            WoWUnit unit = Group.Tanks
                                .Where(u => u.IsAlive && u.Combat && u.DistanceSqr < 40 * 40 && u.GetAuraTimeLeft("Renew").TotalMilliseconds.Between(350, 2500) && u.InLineOfSpellSight)
                                .OrderBy(u => u.HealthPercent)
                                .FirstOrDefault();

                            if (unit != null && Spell.CanCastHack("Renew", unit, skipWowCheck: true))
                            {
                                Logger.WriteDebug("Refreshing RENEW ON TANK: {0}", unit.SafeName());
                                return unit;
                            }
                        }
                        return null;
                    },
                    req => true)
                );

            if (PriestSettings.HolyHeal.Renew != 0)
            behavs.AddBehavior(HealthToPriority(102), "Tank - Buff Renew @ " + PriestSettings.HolyHeal.Renew + "%", "Renew",
                // roll Renew on Tanks 
                Spell.Cast("Renew", on =>
                    {
                        WoWUnit unit = HealerManager.GetBestTankTargetForHOT("Renew", PriestSettings.HolyHeal.Renew);
                        if (unit != null && Spell.CanCastHack("Renew", unit, skipWowCheck: true))
                        {
                            Logger.WriteDebug("Buffing RENEW ON TANK: {0}", unit.SafeName());
                            return unit;
                        }
                        return null;
                    })
                );
            #endregion


            #region Lowest Priority Healer Tasks

            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
            {
                behavs.AddBehavior(HealthToPriority(103), "Lightwell", "Lightwell",
                    new Throttle(TimeSpan.FromSeconds(20),
                        new Sequence(
                            new Action(r =>
                            {
                                _lightwellTarget = Group.Tanks.FirstOrDefault(t =>
                                {
                                    if (t.IsAlive && t.Combat && t.GotTarget() && t.CurrentTarget.IsBoss())
                                    {
                                        if (t.Distance < 40 && t.SpellDistance(t.CurrentTarget) < 15)
                                        {
                                            Logger.WriteDiagnostic("Lightwell: found target {0}", t.SafeName());
                                            return true;
                                        }
                                    }
                                    return false;
                                });

                                return _lightwellTarget == null ? RunStatus.Failure : RunStatus.Success;
                            }),
                            Spell.CastOnGround("Lightwell",
                                location => WoWMathHelper.CalculatePointFrom(Me.Location, _lightwellTarget.Location, (float)Math.Min(10.0, _lightwellTarget.Distance / 2)),
                                req => _lightwellTarget != null,
                                false,
                                desc => string.Format("10 yds from {0}", _lightwellTarget.SafeName())
                                )
                            )
                        )
                    );
            }
            #endregion

            behavs.OrderBehaviors();

            if ( selfOnly == false && Singular.Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Combat)
                behavs.ListBehaviors();

            return new PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.FindLowestHealthTarget(), // HealerManager.Instance.FirstUnit,

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && ret != null,
                    behavs.GenerateBehaviorTree()
                    ),

                new Decorator(
                    ret => moveInRange,
                    Movement.CreateMoveToUnitBehavior( 
                        ret => Battlegrounds.IsInsideBattleground ? (WoWUnit)ret : Group.Tanks.Where(a => a.IsAlive).OrderBy(a => a.Distance).FirstOrDefault(),
                        35f
                        )
                    )
                );
        }

        private static int HealthToPriority(int nHealth)
        {
            return nHealth == 0 ? 0 : 200 - nHealth;
        }


        [Behavior(BehaviorType.Heal, WoWClass.Priest, WoWSpec.PriestHoly)]
        public static Composite CreateHolyHeal()
        {
            return new Decorator(
                ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                new PrioritySelector(
                    Spell.Cast("Desperate Prayer", ret => Me, ret => Me.Combat && Me.HealthPercent < PriestSettings.DesperatePrayerHealth),
                    Spell.BuffSelf("Power Word: Shield", ret => Me.Combat && Me.HealthPercent < PriestSettings.PowerWordShield && !Me.HasAura("Weakened Soul")),

                    // keep heal buffs on if glyphed
                    Spell.BuffSelf("Prayer of Mending", ret => Me.Combat && Me.HealthPercent <= 90),
                    Spell.BuffSelf("Renew", ret => Me.Combat && Me.HealthPercent <= 90),

                    Common.CreatePsychicScreamBehavior(),

                    Spell.Cast("Flash Heal",
                        ctx => Me,
                        ret => Me.HealthPercent <= PriestSettings.ShadowHeal),

                    Spell.Cast("Flash Heal",
                        ctx => Me,
                        ret => !Me.Combat && Me.PredictedHealthPercent(includeMyHeals: true) <= 85)
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Priest, WoWSpec.PriestHoly)]
        public static Composite CreateHolyCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(
                    req => !Me.CurrentTarget.IsTrivial(),
                    new PrioritySelector(
                        Common.CreateFadeBehavior(),

                        Spell.BuffSelf("Desperate Prayer", ret => StyxWoW.Me.HealthPercent <= PriestSettings.DesperatePrayerHealth),

                        Common.CreateShadowfiendBehavior(),

                        Common.CreateLeapOfFaithBehavior(),

                        Spell.Cast("Power Infusion", ret => StyxWoW.Me.ManaPercent <= 75 || HealerManager.Instance.TargetList.Any( h => h.HealthPercent < 40))
                        )
                    )
                );
        }

        // This behavior is used in combat/heal AND pull. Just so we're always healing our party.
        // Note: This will probably break shit if we're solo, but oh well!
        [Behavior(BehaviorType.Combat | BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestHoly)]
        public static Composite CreateHolyCombat()
        {
            return new PrioritySelector(

                CreateHolyDiagnosticOutputBehavior("COMBAT"),

                HealerManager.CreateStayNearTankBehavior(),

                new Decorator(
                    ret => Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                    CreateHolyHealOnlyBehavior(false, true)
                    ),

                new Decorator(
                    ret => HealerManager.AllowHealerDPS(),
                    new PrioritySelector(
                        Helpers.Common.EnsureReadyToAttackFromMediumRange(),
                        Spell.WaitForCastOrChannel(),

                        new Decorator( 
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                Spell.BuffSelf("Power Word: Shield", ret => Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull && PriestSettings.UseShieldPrePull && !Me.HasAura("Weakened Soul")),
                                Helpers.Common.CreateInterruptBehavior(),

                                Movement.WaitForFacing(),
                                Movement.WaitForLineOfSpellSight(),

                                Dispelling.CreatePurgeEnemyBehavior("Dispel Magic"),
                                Spell.Buff("Shadow Word: Pain", true),
                                Spell.Buff("Holy Word: Chastise"),
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
                                Common.CreateHolyFireBehavior(),
                                Spell.Cast("Smite", mov => true, on => Me.CurrentTarget, req => true, cancel => HealerManager.CancelHealerDPS())
                                )
                            )
                        )
                    )
                );
        }

        #region Diagnostics

        private static DateTime _LastDiag = DateTime.MinValue;

        private static Composite CreateHolyDiagnosticOutputBehavior( string context)
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses( 1, TimeSpan.FromSeconds(1), RunStatus.Failure,
                new Action(ret =>
                {
                    WoWAura chakra = Me.GetAllAuras().Where(a => a.Name.Contains("Chakra")).FirstOrDefault();

                    string line = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, combat={3}, {4}, surge={5}, serendip={6}",
                        context,
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.Combat.ToYN(),
                        chakra == null ? "(null)" : chakra.Name,
                        (long)Me.GetAuraTimeLeft("Surge of Light").TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Serendipity").TotalMilliseconds
                        );

                    WoWUnit healTarget = HealerManager.Instance == null ? null : HealerManager.Instance.FirstUnit;
                    if (!HealerManager.NeedHealTargeting)
                        line += ", healTargeting=disabled";
                    else if (Me.IsInGroup() || (Me.FocusedUnitGuid.IsValid && healTarget == Me.FocusedUnit))
                    {
                        if (healTarget == null || !healTarget.IsValid)
                            line += ", target=(null)";
                        else
                        {
                            line += string.Format(", target={0} th={1:F1}%/{2:F1}%,  @ {3:F1} yds, combat={4}, tloss={5}, pw:s={6}, renew={7}",
                                healTarget.SafeName(),
                                healTarget.HealthPercent,
                                healTarget.PredictedHealthPercent(includeMyHeals: true),
                                healTarget.Distance,
                                healTarget.Combat.ToYN(),
                                healTarget.InLineOfSpellSight,
                                (long)healTarget.GetAuraTimeLeft("Power Word: Shield").TotalMilliseconds,
                                (long)healTarget.GetAuraTimeLeft("Renew").TotalMilliseconds
                                );
                        }

                        if (SingularSettings.Instance.StayNearTank)
                        {
                            WoWUnit tank = HealerManager.TankToStayNear;
                            if (tank == null)
                                line += ",tank=(null)";
                            else if (!tank.IsAlive)
                                line += ",tank=(dead)";
                            else
                            {
                                float hh = (float)tank.HealthPercent;
                                float hph = tank.PredictedHealthPercent();
                                line += string.Format(",tank={0} {1:F1}% @ {2:F1} yds,tph={3:F1}%,tcombat={4},tmove={5},tloss={6}",
                                    tank.SafeName(),
                                    hh,
                                    tank.SpellDistance(),
                                    hph,
                                    tank.Combat.ToYN(),
                                    tank.IsMoving.ToYN(),
                                    tank.InLineOfSpellSight.ToYN()
                                    );
                            }
                        }
                    }

                    Logger.WriteDebug(Color.LightGreen, line);
                    return RunStatus.Failure;
                }));
        }

        #endregion
    }
}
