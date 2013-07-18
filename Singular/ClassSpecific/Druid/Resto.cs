using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.WoWInternals;
using Styx.CommonBot;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;

using Rest = Singular.Helpers.Rest;
using Action = Styx.TreeSharp.Action;
using CommonBehaviors.Actions;
using Styx.Common.Helpers;
using System.Collections.Generic;

namespace Singular.ClassSpecific.Druid
{
    public class Resto
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DruidSettings Settings { get { return SingularSettings.Instance.Druid(); } }

        const int PriEmergencyBase = 500;
        const int PriHighBase = 400;
        const int PriHighAtone = 300;
        const int PriAoeBase = 200;
        const int PriSingleBase = 100;
        const int PriLowBase = 0;

        const int MUSHROOM_ID = 47649;

        static IEnumerable<WoWUnit> Mushrooms
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.Entry == MUSHROOM_ID && o.CreatedByUnitGuid == StyxWoW.Me.Guid && o.Distance <= 40 ); 
            }
        }

        static int MushroomCount
        {
            get { return Mushrooms.Count(); }
        }


        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidHealRest()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateRestoNonCombatHeal(true),
                        Rest.CreateDefaultRestBehaviour(),
                        Spell.Resurrect("Revive"),
                        CreateRestoNonCombatHeal(false)
                        )              
                    )
                );
        }

        public static Composite CreateRestoDruidHealOnlyBehavior()
        {
            return CreateRestoDruidHealOnlyBehavior(false, true);
        }

        public static Composite CreateRestoDruidHealOnlyBehavior(bool selfOnly)
        {
            return CreateRestoDruidHealOnlyBehavior(selfOnly, false);
        }

        public static Composite CreateRestoDruidHealOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;
            const uint mapleSeedId = 17034;

            return CreateHealingOnlyBehavior(selfOnly, moveInRange);

            return new
                PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                    new Decorator(
                        ret => ret != null && (moveInRange || ((WoWUnit)ret).InLineOfSpellSight && ((WoWUnit)ret).DistanceSqr < 40 * 40),
                        new PrioritySelector(
                        Spell.WaitForCastOrChannel(),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToLosBehavior(ret => (WoWUnit)ret)),
                        // Ensure we're in range of the unit to heal, and it's in LOS.
                        //CreateMoveToAndFace(35f, ret => (WoWUnit)ret),
                        //Cast Lifebloom on tank if
                        //1- Tank doesn't have lifebloom
                        //2- Tank has less then 3 stacks of lifebloom
                        //3- Tank has 3 stacks of lifebloom but it will expire in 3 seconds
                        Spell.Cast(
                            "Lifebloom",
                            ret => (WoWUnit)ret,
                            ret =>
                            StyxWoW.Me.Combat &&
                                // Keep 3 stacks up on the tank/leader at all times.
                                // If we're in ToL form, we can do rolling LBs for everyone. So ignore the fact that its the leader or not.
                                // LB is cheap, and VERY powerful in ToL form since you can spam it on the entire raid, for a cheap HoT and quite good 'bloom'
                            ((RaFHelper.Leader != null && (WoWUnit)ret == RaFHelper.Leader) || StyxWoW.Me.Shapeshift == ShapeshiftForm.TreeOfLife) &&
                            ((WoWUnit)ret).HealthPercent > 60 &&
                            (!((WoWUnit)ret).HasAura("Lifebloom") || ((WoWUnit)ret).Auras["Lifebloom"].StackCount < 3 ||
                             ((WoWUnit)ret).Auras["Lifebloom"].TimeLeft <= TimeSpan.FromSeconds(3))),
                        //Cast rebirth if the tank is dead. Check for Unburdened Rebirth glyph or Maple seed reagent
                        Spell.Cast(
                            "Rebirth",
                            ret => (WoWUnit)ret,
                            ret => Settings.UseRebirth && StyxWoW.Me.Combat && RaFHelper.Leader != null && (WoWUnit)ret == RaFHelper.Leader &&
                                   ((WoWUnit)ret).IsDead && (TalentManager.HasGlyph("Unburdened Rebirth") || StyxWoW.Me.BagItems.Any(i => i.Entry == mapleSeedId))),
                        Spell.Cast(
                            "Tranquility",
                            mov => true,
                            on => Me,
                            ret => StyxWoW.Me.Combat && StyxWoW.Me.GroupInfo.IsInParty && Unit.NearbyFriendlyPlayers.Count(
                                p =>
                                p.IsAlive && p.HealthPercent <= Settings.TranquilityHealth && p.Distance <= 30) >=
                                   Settings.TranquilityCount,
                            cancel => false
                            ),
                        //Use Innervate on party members if we have Glyph of Innervate
                        Spell.Buff(
                            "Innervate",
                            ret => (WoWUnit)ret,
                            ret =>
                            TalentManager.HasGlyph("Innervate") && StyxWoW.Me.Combat && (WoWUnit)ret != StyxWoW.Me &&
                            StyxWoW.Me.ManaPercent <= Settings.InnervateMana &&
                            ((WoWUnit)ret).PowerType == WoWPowerType.Mana && ((WoWUnit)ret).ManaPercent <= Settings.InnervateMana),
                        Spell.Cast(
                            "Swiftmend",
                            ret => (WoWUnit)ret,
                            ret => StyxWoW.Me.Combat && ((WoWUnit)ret).HealthPercent <= Settings.Swiftmend &&
                                   (((WoWUnit)ret).HasAura("Rejuvenation") || ((WoWUnit)ret).HasAura("Regrowth"))),
                        Spell.Cast(
                            "Wild Growth",
                            ret => (WoWUnit)ret,
                            ret => StyxWoW.Me.GroupInfo.IsInParty && Unit.NearbyFriendlyPlayers.Count(
                                p => p.IsAlive && p.HealthPercent <= Settings.WildGrowthHealth &&
                                     p.Location.DistanceSqr(((WoWUnit)ret).Location) <= 30*30) >= Settings.WildGrowthCount),
                        Spell.Cast(
                            "Regrowth",
                            ret => (WoWUnit)ret,
                            ret => !((WoWUnit)ret).HasMyAura("Regrowth") && ((WoWUnit)ret).HealthPercent <= Settings.Regrowth),
                        Spell.Cast(
                            "Healing Touch",
                            ret => (WoWUnit)ret,
                            ret => ((WoWUnit)ret).HealthPercent <= Settings.HealingTouch),
                        Spell.Cast(
                            "Nourish",
                            ret => (WoWUnit)ret,
                            ret => ((WoWUnit)ret).HealthPercent <= Settings.Nourish &&
                                   ((((WoWUnit)ret).HasAura("Rejuvenation") || ((WoWUnit)ret).HasAura("Regrowth") ||
                                    ((WoWUnit)ret).HasAura("Lifebloom") || ((WoWUnit)ret).HasAura("Wild Growth")))),
                        Spell.Cast(
                            "Rejuvenation",
                            ret => (WoWUnit)ret,
                            ret => !((WoWUnit)ret).HasMyAura("Rejuvenation") &&
                                   ((WoWUnit)ret).HealthPercent <= Settings.Rejuvenation),
                        new Decorator(
                            ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0,
                            new PrioritySelector(
                                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                                Helpers.Common.CreateInterruptBehavior(),
                                Spell.Buff("Moonfire"),
                                Spell.Cast("Starfire", ret => StyxWoW.Me.HasAura("Fury of Stormrage")),
                                Spell.Cast("Wrath"),
                                Movement.CreateMoveToUnitBehavior(35f, on=> Me.CurrentTarget )
                                )),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToUnitBehavior(35f, ret => (WoWUnit)ret))
                        )));
        }

        private static WoWUnit _moveToHealTarget = null;
        private static WoWUnit _lastMoveToTarget = null;

        private static int HealthToPriority(int nHealth)
        {
            return nHealth == 0 ? 0 : 200 - nHealth;
        }

        
        // temporary lol name ... will revise after testing
        public static Composite CreateHealingOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                return new ActionAlwaysFail();

            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();
            int cancelHeal = (int)Math.Max(SingularSettings.Instance.IgnoreHealTargetsAboveHealth, Math.Max(Settings.Heal.Rejuvenation, Math.Max(Settings.Heal.HealingTouch, Math.Max(Settings.Heal.Nourish, Settings.Heal.Regrowth))));

            Logger.WriteFile("Druid Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);

            #region Cleanse

            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
            {
                int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
                behavs.AddBehavior(dispelPriority, "Nature's Cure", "Nature's Cure", Dispelling.CreateDispelBehavior());
            }

            #endregion

            #region Save the Group

            // Tank: Rebirth
            if (Settings.UseRebirth )
            {
                behavs.AddBehavior(799, "Rebirth Tank/Healer", "Rebirth",
                    Common.CreateRebirthBehavior(ctx => Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead) ?? Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead))
                    );
            }

            if (Settings.Heal.NaturesSwiftness != 0)
            {
                behavs.AddBehavior(797, "Nature's Swiftness Heal @ " + Settings.Heal.NaturesSwiftness + "%", "Nature's Swiftness",
                    new Decorator(
                        req => ((WoWUnit)req).HealthPercent < Settings.Heal.NaturesSwiftness 
                            && !Spell.IsSpellOnCooldown("Nature's Swiftness")
                            && Spell.CanCastHack("Rejuvenation", (WoWUnit)req, skipWowCheck: true),
                        new Sequence(
                            Spell.BuffSelf("Nature's Swiftness" ),
                            new PrioritySelector(
                                Spell.Cast("Regrowth", on => (WoWUnit)on, req => true, cancel => false),
                                Spell.Cast("Healing Touch", on => (WoWUnit)on, req => true, cancel => false),
                                Spell.Cast("Nourish", on => (WoWUnit)on, req => true, cancel => false)
                                )
                            )
                        )
                    );
            }

            if (Settings.Heal.Tranquility != 0)
                behavs.AddBehavior(798, "Tranquility @ " + Settings.Heal.Tranquility + "% MinCount: " + Settings.Heal.CountTranquility, "Tranquility",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        Spell.Cast(
                            "Tranquility", 
                            mov => true,
                            on => (WoWUnit)on,
                            req => HealerManager.Instance.TargetList.Count( h => h.IsAlive && h.HealthPercent < Settings.Heal.Tranquility && h.SpellDistance() < 40) >= Settings.Heal.CountTranquility,
                            cancel => false
                            )
                        )
                    );

            if (Settings.Heal.SwiftmendDirectHeal != 0)
            {
                behavs.AddBehavior(797, "Swiftmend Direct @ " + Settings.Heal.SwiftmendDirectHeal + "%", "Swiftmend",
                    new Decorator(
                        ret => (!Spell.IsSpellOnCooldown("Swiftmend") || Spell.GetCharges("Force of Nature") > 0)
                            && ((WoWUnit)ret).GetPredictedHealthPercent(true) < Settings.Heal.SwiftmendDirectHeal
                            && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid)
                            && Spell.CanCastHack("Rejuvenation", (WoWUnit)ret, skipWowCheck: true),
                        new Sequence(
                            new DecoratorContinue(
                                req => !((WoWUnit)req).HasAnyAura("Rejuvenation", "Regrowth"),
                                new PrioritySelector(
                                    Spell.Buff("Rejuvenation", on => (WoWUnit)on),
                                    Spell.Cast("Regrowth", on => (WoWUnit)on, req => true, cancel => false)
                                    )
                                ),
                            new Wait(2, until => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(), new ActionAlwaysSucceed()),
                            new PrioritySelector(
                                Spell.Cast("Force of Nature", on => (WoWUnit)on, req => Spell.GetCharges("Force of Nature") > 1),
                                Spell.Cast("Swiftmend", on => (WoWUnit)on)
                                )
                            )
                        )
                    );
            }

            #endregion

            #region Tank Buffing

            // Tank: Lifebloom
            behavs.AddBehavior(99 + PriHighBase, "Lifebloom - Tank", "Lifebloom",
                Spell.Cast("Lifebloom", on =>
                {
                    WoWUnit unit = GetLifebloomTarget();
                    if (unit != null && (unit.Combat || Me.Combat) 
                        && (unit.GetAuraStacks("Lifebloom") < 3 || unit.GetAuraTimeLeft("Lifebloom").TotalMilliseconds < 2800) 
                        && Spell.CanCastHack("Lifebloom", unit, skipWowCheck: true))
                    {
                        Logger.WriteDebug("Buffing Lifebloom ON TANK: {0}", unit.SafeName());
                        return unit;
                    }
                    return null;
                })
                );

            // Tank: Rejuv if Lifebloom not trained yet
            if (Settings.Heal.Rejuvenation != 0 && !SpellManager.HasSpell("Lifebloom"))
            {
                behavs.AddBehavior(98 + PriHighBase, "Rejuvenation - Tank", "Rejuvenation",
                    Spell.Cast("Rejuvenation", on =>
                    {
                        WoWUnit unit = GetBestTankTargetFor("Rejuvenation");
                        if (unit != null && Spell.CanCastHack("Rejuvenation", unit, skipWowCheck: true))
                        {
                            Logger.WriteDebug("Buffing Rejuvenation ON TANK: {0}", unit.SafeName());
                            return unit;
                        }
                        return null;
                    })
                    );
            }

            if (Settings.Heal.Ironbark != 0)
            {
                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                    behavs.AddBehavior(HealthToPriority(Settings.Heal.Ironbark) + PriHighBase, "Ironbark @ " + Settings.Heal.Ironbark + "%", "Ironbark",
                        Spell.Buff("Ironbark", on => (WoWUnit)on, req => ((WoWUnit)req).HealthPercent < Settings.Heal.Ironbark)
                        );
                else
                    behavs.AddBehavior(HealthToPriority(Settings.Heal.Ironbark) + PriHighBase, "Ironbark - Tank @ " + Settings.Heal.Ironbark + "%", "Ironbark",
                        Spell.Buff("Ironbark", on => RaFHelper.Leader == (WoWUnit)on && ((WoWUnit)on).IsAlive && ((WoWUnit)on).HealthPercent < Settings.Heal.Ironbark)
                        );
            }

            if (Settings.Heal.CenarionWard != 0)
            {
                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                    behavs.AddBehavior(HealthToPriority(Settings.Heal.CenarionWard) + PriHighBase, "Cenarion Ward @ " + Settings.Heal.CenarionWard + "%", "Cenarion Ward",
                        Spell.Buff("Cenarion Ward", on => (WoWUnit)on, req => ((WoWUnit)req).HealthPercent < Settings.Heal.CenarionWard)
                        );
                else
                    behavs.AddBehavior(HealthToPriority(Settings.Heal.CenarionWard) + PriHighBase, "Cenarion Ward - Tank @ " + Settings.Heal.CenarionWard + "%", "Cenarion Ward",
                        Spell.Buff("Cenarion Ward", on => RaFHelper.Leader == (WoWUnit)on && ((WoWUnit)on).IsAlive && ((WoWUnit)on).HealthPercent < Settings.Heal.CenarionWard)
                        );
            }

            if (Settings.Heal.NaturesVigil != 0)
            {
                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                    behavs.AddBehavior(HealthToPriority(Settings.Heal.NaturesVigil) + PriHighBase, "Nature's Vigil @ " + Settings.Heal.NaturesVigil + "%", "Nature's Vigil",
                        Spell.Buff("Nature's Vigil", on => (WoWUnit)on, req => ((WoWUnit)req).HealthPercent < Settings.Heal.NaturesVigil)
                        );
                else
                    behavs.AddBehavior(HealthToPriority(Settings.Heal.NaturesVigil) + PriHighBase, "Nature's Vigil - Tank @ " + Settings.Heal.NaturesVigil + "%", "Nature's Vigil",
                        Spell.Buff("Nature's Vigil", on => RaFHelper.Leader == (WoWUnit)on && ((WoWUnit)on).IsAlive && ((WoWUnit)on).HealthPercent < Settings.Heal.NaturesVigil)
                        );
            }

            if (Settings.Heal.TreeOfLife != 0)
            {
                behavs.AddBehavior(HealthToPriority(Settings.Heal.TreeOfLife) + PriHighBase, "Incarnation: Tree of Life @ " + Settings.Heal.TreeOfLife + "% MinCount: " + Settings.Heal.CountTreeOfLife, "Incarnation: Tree of Life",
                    Spell.BuffSelf("Incarnation: Tree of Life", 
                        req => ((WoWUnit)req).HealthPercent < Settings.Heal.TreeOfLife
                            && Settings.Heal.CountTreeOfLife <= HealerManager.Instance.TargetList.Count(h => h.IsAlive && h.HealthPercent < Settings.Heal.TreeOfLife))
                    );
            }

            #endregion

            #region AoE Heals

            int maxDirectHeal = Math.Max(Settings.Heal.Nourish, Math.Max(Settings.Heal.HealingTouch, Settings.Heal.Regrowth));

            if (Settings.Heal.WildGrowth != 0)
                behavs.AddBehavior(HealthToPriority(Settings.Heal.WildGrowth) + PriAoeBase, "Wild Growth @ " + Settings.Heal.WildGrowth + "% MinCount: " + Settings.Heal.CountWildGrowth, "Wild Growth",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        new PrioritySelector(
                    // ctx => HealerManager.GetBestCoverageTarget("Wild Growth", Settings.Heal.WildGrowth, 40, 30, Settings.Heal.CountWildGrowth),
                            Spell.Cast(
                                "Wild Growth",
                                on => (WoWUnit)on,
                                req => ((WoWUnit)req).HealthPercent < Settings.Heal.WildGrowth
                                    && Settings.Heal.CountWildGrowth <= HealerManager.Instance.TargetList
                                        .Count(p => p.IsAlive && p.HealthPercent <= Settings.Heal.WildGrowth && p.Location.DistanceSqr(((WoWUnit)req).Location) <= 30 * 30))
                            )
                        )
                    );

            if (Settings.Heal.WildMushroomBloom != 0)
                behavs.AddBehavior(HealthToPriority(Settings.Heal.WildMushroomBloom) + PriAoeBase, "Wild Mushroom: Bloom @ " + Settings.Heal.WildMushroomBloom + "% MinCount: " + Settings.Heal.CountMushroomBloom, "Wild Mushroom: Bloom",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        CreateMushroomBloom()
                        ) 
                    );
/*
            if (Settings.Heal.SwiftmendAOE != 0)
                behavs.AddBehavior(HealthToPriority(Settings.Heal.SwiftmendAOE) + PriAoeBase, "Swiftmend @ " + Settings.Heal.SwiftmendAOE + "% MinCount: " + Settings.Heal.CountSwiftmendAOE, "Swiftmend",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        new PrioritySelector(
                            ctx => HealerManager.GetBestCoverageTarget("Swiftmend", Settings.Heal.SwiftmendAOE, 40, 10, Settings.Heal.CountSwiftmendAOE
                                , mainTarget: Unit.NearbyGroupMembersAndPets.Where(p => p.HealthPercent < Settings.Heal.SwiftmendAOE && p.SpellDistance() <= 40 && p.IsAlive && p.HasAnyOfMyAuras("Rejuvenation", "Regrowth"))),
                            Spell.Cast("Force of Nature", on => (WoWUnit)on, req => Spell.GetCharges("Force of Nature") > 1),
                            Spell.Cast(spell => "Swiftmend", mov => false, on => (WoWUnit)on, req => true, skipWowCheck: true)
                            )
                        )
                    );
*/
            #endregion

            #region Direct Heals

            // Regrowth above ToL: Lifebloom so we use Clearcasting procs 
            behavs.AddBehavior(200 + PriSingleBase, "Regrowth on Clearcasting", "Regrowth",
                new PrioritySelector(
                    Spell.Cast("Regrowth",
                        mov => !Me.HasAnyAura("Nature's Swiftness", "Incarnation: Tree of Life"),
                        on => {
                            WoWUnit target = (WoWUnit)on;
                            if (target.HealthPercent > 95)
                            {
                                WoWUnit lbTarget = GetLifebloomTarget();
                                if (lbTarget != null && lbTarget.GetAuraStacks("Lifebloom") >= 3 && lbTarget.GetAuraTimeLeft("Lifebloom").TotalMilliseconds.Between(500,10000))
                                {
                                    return lbTarget;
                                }
                            }
                            return target;
                        },
                        req => Me.GetAuraTimeLeft("Clearcasting").TotalMilliseconds > 1500,
                        cancel => false
                        )
                    )
                );

            // ToL: Lifebloom
            if (Settings.Heal.TreeOfLife != 0 && Common.HasTalent(DruidTalents.Incarnation))
            {
                behavs.AddBehavior(199 + PriSingleBase, "Lifebloom - Tree of Life", "Lifebloom",
                    Spell.Cast("Lifebloom", 
                        mov => false,
                        on => HealerManager.Instance.TargetList.FirstOrDefault( h => (h.GetAuraStacks("Lifebloom") < 3 || h.GetAuraTimeLeft("Lifebloom").TotalMilliseconds < 2500) && Spell.CanCastHack("Lifebloom", h, skipWowCheck: true)),
                        req => Me.GetAuraTimeLeft("Incarnation") != TimeSpan.Zero,
                        cancel => false
                        )
                    );
            }

            behavs.AddBehavior(198 + PriSingleBase, "Rejuvenation @ " + Settings.Heal.Rejuvenation + "%", "Rejuvenation",
                new PrioritySelector(
                    Spell.Buff("Rejuvenation",
                        true,
                        on => (WoWUnit)on,
                        req => ((WoWUnit)req).HealthPercent < Settings.Heal.Rejuvenation,
                        1
                        )
                    )
                );

            if (Settings.Heal.Nourish != 0)
            {
                // roll 3 Rejuvs if Glyph of Rejuvenation equipped
                if (TalentManager.HasGlyph("Rejuvenation"))
                {
                    // make priority 1 higher than Noursh (-1 here due to way HealthToPriority works)
                    behavs.AddBehavior(HealthToPriority(Settings.Heal.Nourish-1) + PriSingleBase, "Roll 3 Rejuvenations for Glyph", "Rejuvenation",
                        new PrioritySelector(
                            Spell.Buff("Rejuvenation",
                                true,
                                on =>
                                {
                                    // iterate through so we can stop at either 3 with rejuv or first without
                                    int cntHasAura = 0;
                                    foreach (WoWUnit h in HealerManager.Instance.TargetList)
                                    {
                                        if (h.IsAlive)
                                        {
                                            if (!h.HasKnownAuraExpired("Rejuvenation", 1))
                                            {
                                                cntHasAura++;
                                                if (cntHasAura >= 3)
                                                    return null;
                                            }
                                            else
                                            {
                                                if (h.InLineOfSpellSight)
                                                {
                                                    return h;
                                                }
                                            }
                                        }
                                    }

                                    return null;
                                },
                                req => true,
                                1
                                )
                            )
                        );
                }

                behavs.AddBehavior(HealthToPriority(Settings.Heal.Nourish) + PriSingleBase, "Nourish @ " + Settings.Heal.Nourish + "%", "Nourish",
                new PrioritySelector(
                    Spell.Cast("Nourish",
                        mov => true,
                        on => (WoWUnit)on,
                        req => ((WoWUnit)req).HealthPercent < Settings.Heal.Nourish && ((WoWUnit)req).HasAnyOfMyAuras("Rejuvenation", "Regrowth", "Lifebloom", "Wild Growth"),
                        cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                        )
                    )
                );
            }

            if (Settings.Heal.HealingTouch != 0)
            {
                if ( SpellManager.HasSpell("Regrowth"))
                {
                    string whyRegrowth = "";
                    if ( !SpellManager.HasSpell("Healing Touch"))
                        whyRegrowth = "Regrowth (since Healing Touch unknown) @ ";
                    else if ( TalentManager.HasGlyph("Regrowth"))
                        whyRegrowth = "Glyphed Regrowth (instead of Healing Touch) @ ";

                    if (whyRegrowth != "" )
                    {
                    behavs.AddBehavior(HealthToPriority(Settings.Heal.HealingTouch) + PriSingleBase, whyRegrowth + Settings.Heal.HealingTouch + "%", "Regrowth",
                        new PrioritySelector(
                            Spell.Cast("Regrowth",
                                mov => true,
                                on => (WoWUnit)on,
                                req => ((WoWUnit)req).HealthPercent < Settings.Heal.HealingTouch,
                                cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                                )
                            )
                        );
                    }
                }
                else
                {
                    behavs.AddBehavior(HealthToPriority(Settings.Heal.HealingTouch) + PriSingleBase, "Healing Touch @ " + Settings.Heal.HealingTouch + "%", "Healing Touch",
                        new PrioritySelector(
                            Spell.Cast("Healing Touch",
                                mov => true,
                                on => (WoWUnit)on,
                                req => ((WoWUnit)req).HealthPercent < Settings.Heal.HealingTouch,
                                cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                                )
                            )
                        );
                }
            }

            if (Settings.Heal.Regrowth != 0)
                behavs.AddBehavior(HealthToPriority(Settings.Heal.Regrowth) + PriSingleBase, "Regrowth @ " + Settings.Heal.Regrowth + "%", "Regrowth",
                new PrioritySelector(
                    Spell.Cast("Regrowth",
                        mov => true,
                        on => (WoWUnit)on,
                        req => ((WoWUnit)req).HealthPercent < Settings.Heal.Regrowth,
                        cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                        )
                    )
                );

            #endregion

            #region Lowest Priority Healer Tasks

            behavs.AddBehavior(2, "Rejuvenation while Moving @ " + Settings.Heal.Rejuvenation + "%", "Rejuvenation",
                new Decorator(
                    req => Me.IsMoving,
                    Spell.Cast("Rejuvenation",
                        mov => false,
                        on => HealerManager.Instance.TargetList.FirstOrDefault(h => h.IsAlive && h.HealthPercent < 100 && !h.HasMyAura("Rejuvenation") && Spell.CanCastHack("Rejuvenation", h, true)),
                        req => true
                        )
                    )
                );

            if (Settings.Heal.WildMushroomBloom != 0)
                behavs.AddBehavior(1, "Wild Mushroom: Set", "Wild Mushroom",
                    CreateMushroomSetBehavior()
                    );

            #endregion

            behavs.OrderBehaviors();

            if (selfOnly == false && Singular.Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Heal)
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

        /// <summary>
        /// non-combat heal to top people off and avoid lifebloom, buffs, etc.
        /// </summary>
        /// <param name="selfOnly"></param>
        /// <returns></returns>
        public static Composite CreateRestoNonCombatHeal(bool selfOnly = false)
        {
            return new PrioritySelector(
                ctx => selfOnly || !Me.IsInGroup() ? StyxWoW.Me : HealerManager.FindLowestHealthTarget(), // HealerManager.Instance.FirstUnit,

                new Decorator(
                    req => !Me.Combat && req != null && !((WoWUnit)req).Combat && ((WoWUnit)req).GetPredictedHealthPercent(true) < SingularSettings.Instance.IgnoreHealTargetsAboveHealth,

                    new Sequence(
                        new Action(on => Logger.WriteDebug("NonCombatHeal on {0}: health={1:F1}% predicted={2:F1}% +mine={3:F1}", ((WoWUnit)on).SafeName(), ((WoWUnit)on).HealthPercent, ((WoWUnit)on).GetPredictedHealthPercent(false), ((WoWUnit)on).GetPredictedHealthPercent(true))),
                        new PrioritySelector(
                            // BUFFS First
                            Spell.Buff("Rejuvenation", true, on => (WoWUnit)on, req => ((WoWUnit)req).GetPredictedHealthPercent(true) < 95, 1),
                            Spell.Buff("Regrowth", true, on => (WoWUnit)on, req => ((WoWUnit)req).GetPredictedHealthPercent(true) < 80 && !TalentManager.HasGlyph("Regrowth"), 1),
                            // Direct Heals After
                            Spell.Cast("Healing Touch", on => (WoWUnit)on, req => ((WoWUnit)req).GetPredictedHealthPercent(true) < 65),
                            Spell.Cast("Regrowth", on => (WoWUnit)on, req => ((WoWUnit)req).GetPredictedHealthPercent(true) < 75),
                            Spell.Cast("Nourish", on => (WoWUnit)on, req => ((WoWUnit)req).GetPredictedHealthPercent(true) < 85)
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidHealBehavior()
        {
            return new Decorator(
                ret => HealerManager.Instance.TargetList.Any( h => h.Distance < 40 && h.IsAlive && !h.IsMe),
                new PrioritySelector(
                    CreateRestoDruidHealOnlyBehavior()
                    )
                );
        }


        [Behavior(BehaviorType.Pull|BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidCombat()
        {
            return new Decorator(
                req => !HealerManager.Instance.TargetList.Any(h => h.IsAlive && !h.IsMe && h.Distance < 40),

                new PrioritySelector(
                    Helpers.Common.EnsureReadyToAttackFromLongRange(),
                    Helpers.Common.CreateInterruptBehavior(),
                    Spell.Buff("Moonfire"),
                    Spell.Cast("Wrath"),
                    Movement.CreateMoveToUnitBehavior(on => Me.CurrentTarget, 35f, 30f)
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Innervate", ret => StyxWoW.Me.ManaPercent < 15 || StyxWoW.Me.ManaPercent <= Settings.InnervateMana),
                Spell.BuffSelf("Barkskin", ret => StyxWoW.Me.HealthPercent <= Settings.Barkskin),

                // Symbiosis
                Common.SymbBuff(Symbiosis.IceboundFortitude, on => Me, ret => Me.HealthPercent < Settings.Barkskin),
                Common.SymbBuff(Symbiosis.Deterrence, on => Me, ret => Me.HealthPercent < Settings.Barkskin),
                Common.SymbBuff(Symbiosis.Evasion, on => Me, ret => Me.HealthPercent < Settings.Barkskin),
                Common.SymbBuff(Symbiosis.FortifyingBrew, on => Me, ret => Me.HealthPercent < Settings.Barkskin),
                Common.SymbBuff(Symbiosis.IntimidatingRoar, on => Me.CurrentTarget, ret => Me.CurrentTarget.SpellDistance() < 10 && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8 * 8) > 1),

                Common.SymbBuff(Symbiosis.SpiritwalkersGrace, on => Me, ret => Me.IsMoving && Me.Combat)
                );
        }


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidRestoration, WoWContext.Battlegrounds | WoWContext.Instances, 2)]
        public static Composite CreateRestoPreCombatBuffForSymbiosis(UnitSelectionDelegate onUnit)
        {
            return Common.CreateDruidCastSymbiosis(on => GetRestoBestSymbiosisTarget());
        }

        public static WoWUnit GetBestTankTargetFor(string hotName, int stacks = 1, float health = 100f)
        {
            // fast test unless RaFHelper.Leader is whacked
            try
            {
                if (RaFHelper.Leader != null && RaFHelper.Leader.SpellDistance() < 40 && RaFHelper.Leader.IsAlive)
                {
                    if (SingularSettings.Debug)
                        Logger.WriteDebug("GetBestTankTargetFor('{0}'): found Leader {1} @ {2:F1}%, hasmyaura={3}", hotName, RaFHelper.Leader.SafeName(), RaFHelper.Leader.HealthPercent, RaFHelper.Leader.HasMyAura(hotName));
                    return RaFHelper.Leader;
                }
            }
            catch { }

            WoWUnit hotTarget = null;
            hotTarget = Group.Tanks
                .Where(u => u.IsAlive && u.Combat && u.HealthPercent < health && u.SpellDistance() < 40
                    && (u.GetAuraStacks(hotName) < stacks || u.GetAuraTimeLeft(hotName).TotalSeconds < 3) && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent)
                .FirstOrDefault();

            if (hotTarget != null && SingularSettings.Debug)
                Logger.WriteDebug("GetBestTankTargetFor('{0}'): found Tank {1} @ {2:F1}%, hasmyaura={3}", hotName, hotTarget.SafeName(), hotTarget.HealthPercent, hotTarget.HasMyAura(hotName));

            return hotTarget;
        }

        public static WoWUnit GetLifebloomTarget(float health = 100f)
        {
            string hotName = "Lifebloom";
            int stacks = 3;

            // fast test unless RaFHelper.Leader is whacked
            try
            {
                if (RaFHelper.Leader != null && RaFHelper.Leader.SpellDistance() < 40 && RaFHelper.Leader.IsAlive)
                {
                    if (SingularSettings.Debug)
                        Logger.WriteDebug("GetLifebloomTarget({0:F1}%): tank {1} @ {2:F1}%, stacks={3}", health, RaFHelper.Leader.SafeName(), RaFHelper.Leader.HealthPercent, RaFHelper.Leader.GetAuraStacks(hotName));
                    return RaFHelper.Leader;
                }
            }
            catch { }

            WoWUnit hotTarget = Group.Tanks.FirstOrDefault(u => u.IsAlive && RaFHelper.Leader.SpellDistance() < 40 && u.HasMyAura(hotName) && u.InLineOfSpellSight);
            if (hotTarget == null)
            {
                hotTarget = Group.Tanks
                    .Where(u => u.IsAlive && u.HealthPercent < health && u.SpellDistance() < 40 && u.InLineOfSpellSight)
                    .OrderBy(u => u.HealthPercent)
                    .FirstOrDefault();
            }

            if (hotTarget != null && SingularSettings.Debug)
                Logger.WriteDebug("GetLifebloomTarget({0:F1}%): tank {1} @ {2:F1}%, stacks={3}", health, hotTarget.SafeName(), hotTarget.HealthPercent, hotTarget.GetAuraStacks(hotName));

            return hotTarget;
        }


        private static WoWUnit GetRestoBestSymbiosisTarget()
        {
            WoWUnit target = null;

            if ( SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds )
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Warrior);

            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.DeathKnight);
            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Hunter);
            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Rogue);
            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Monk);

            return target;
        }

        private static int checkMushroomCount { get; set; }

        private static Composite CreateMushroomSetBehavior()
        {
            return new Decorator(
                req => checkMushroomCount < 3,
                new PrioritySelector(
                    ctx => Group.Tanks.FirstOrDefault(t => t.IsAlive && t.Combat && !t.IsMoving && Me.SpellDistance(t) < 40 && t.GotTarget && t.CurrentTarget.Combat && t.SpellDistance(t.CurrentTarget) < 15),

                    // Make sure we arenIf bloom is coming off CD, make sure we drop some more shrooms. 3 seconds is probably a little late, but good enough.
                    // .. also, waitForSpell must be false since Wild Mushroom does not stop targeting after click like other click on ground spells
                    // .. will wait locally and fall through to cancel targeting regardless
                    new Sequence(
                        Spell.CastOnGround("Wild Mushroom", on => (WoWUnit) on, req => true, false),
                        new Action(ctx => Lua.DoString("SpellStopTargeting()"))
                        )
                    )
                );

        }

        private static Composite CreateMushroomBloom()
        {
            return new PrioritySelector(

                new Action(r => {
                        checkMushroomCount = Mushrooms.Count();
                        return RunStatus.Failure;
                    }),

                Spell.Cast("Wild Mushroom: Bloom", req => {
                    if (checkMushroomCount == 0 || ((WoWUnit)req).HealthPercent >= Settings.Heal.WildMushroomBloom)
                        return false;

                    List<WoWUnit> shrooms = Mushrooms.ToList();
                    int nearBy = HealerManager.Instance.TargetList.Where( h => h.HealthPercent < Settings.Heal.WildMushroomBloom && shrooms.Any( m => m.SpellDistance(h) < 10)).Count();
                    Logger.WriteDebug("MushroomBloom: {0} shrooms near {1} targets needing heal", shrooms.Count(), nearBy);
                    return nearBy >= Settings.Heal.CountMushroomBloom;
                    })
                );
        }
    }
}
