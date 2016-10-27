using System.Linq;

using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using System;
using CommonBehaviors.Actions;
using System.Collections.Generic;


namespace Singular.ClassSpecific
{
    public static class Generic
    {
        [Behavior(BehaviorType.Initialize, WoWClass.None, priority: 9000)]
        public static Composite CreateGenericInitializeBehaviour()
        {
            SuppressGenericRacialBehavior = false;
            return null;
        }

        public static Composite CreateUseHealTrinketsBehaviour()
        {
            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.LowHealth))
            {
                return new Decorator(
                    ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.PotionHealth,
                    Item.UseEquippedTrinket(TrinketUsage.LowHealth)
                    );
            }

            return new Action(ret => { return RunStatus.Failure; });
        }

        public static Composite CreateUseTrinketsBehaviour()
        {
            // Saving Settings via GUI will now force reinitialize so we can build the behaviors
            // basead upon the settings rather than continually checking the settings in the Btree
            //
            //

            if (SingularSettings.Instance.Trinket1Usage == TrinketUsage.Never
                && SingularSettings.Instance.Trinket2Usage == TrinketUsage.Never)
            {
                return new Action(ret => { return RunStatus.Failure; });
            }

            PrioritySelector ps = new PrioritySelector();

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.OnCooldown))
            {
                ps.AddChild(Item.UseEquippedTrinket(TrinketUsage.OnCooldown));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.OnCooldownInCombat))
            {
                ps.AddChild(
                    new Decorator(
                        ret =>
                        {
                            if (!StyxWoW.Me.Combat || !StyxWoW.Me.GotTarget())
                                return false;
                            bool isMelee = StyxWoW.Me.IsMelee();
                            if (isMelee)
                                return StyxWoW.Me.CurrentTarget.IsWithinMeleeRange;
                            return !StyxWoW.Me.IsMoving && StyxWoW.Me.CurrentTarget.SpellDistance() < 40;
                        },
                        Item.UseEquippedTrinket(TrinketUsage.OnCooldownInCombat)
                        )
                    );
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.LowHealth))
            {
                ps.AddChild(new Decorator(ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.PotionHealth,
                                            Item.UseEquippedTrinket(TrinketUsage.LowHealth)));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.LowPower))
            {
                ps.AddChild(new Decorator(ret => StyxWoW.Me.PowerPercent < SingularSettings.Instance.PotionMana,
                                            Item.UseEquippedTrinket(TrinketUsage.LowPower)));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.CrowdControlled))
            {
                ps.AddChild(new Decorator(ret => Unit.IsCrowdControlled(StyxWoW.Me),
                                            Item.UseEquippedTrinket(TrinketUsage.CrowdControlled)));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.CrowdControlledSilenced))
            {
                ps.AddChild(new Decorator(ret => StyxWoW.Me.Silenced && Unit.IsCrowdControlled(StyxWoW.Me),
                                            Item.UseEquippedTrinket(TrinketUsage.CrowdControlledSilenced)));
            }

            return ps;
        }

        public static bool SuppressGenericRacialBehavior { get; set; }

        // [Behavior(BehaviorType.Combat, priority: 998)]
        public static Composite CreateRacialBehaviour()
        {
            PrioritySelector pri = new PrioritySelector();

            if (SpellManager.HasSpell("Stoneform"))
            {
                pri.AddChild(
                    new Decorator(
                        ret =>
                        {
                            if (!Spell.CanCastHack("Stoneform"))
                                return false;
                            if (StyxWoW.Me.GetAllAuras().Any(a => a.Spell.Mechanic == WoWSpellMechanic.Bleeding || a.Spell.DispelType == WoWDispelType.Disease || a.Spell.DispelType == WoWDispelType.Poison))
                                return true;
                            if (Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() > 2)
                                return true;
                            if (StyxWoW.Me.GotTarget() && StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid && StyxWoW.Me.CurrentTarget.MaxHealth > (StyxWoW.Me.MaxHealth * 2))
                                return true;
                            return false;
                        },
                        Spell.HandleOffGCD(Spell.BuffSelf("Stoneform"))
                        )
                    );
            }

            if (SpellManager.HasSpell("Escape Artist"))
            {
                pri.AddChild(
                    Spell.HandleOffGCD(Spell.BuffSelf("Escape Artist", req => Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Rooted, WoWSpellMechanic.Snared)))
                    );
            }

            if (SpellManager.HasSpell("Gift of the Naaru"))
            {
                pri.AddChild(
                    Spell.HandleOffGCD(Spell.BuffSelf("Gift of the Naaru", req => StyxWoW.Me.HealthPercent < SingularSettings.Instance.GiftNaaruHP))
                    );
            }

            if (SpellManager.HasSpell("Shadowmeld"))
            {
                pri.AddChild(
                    // even though not on GCD, return success so we resume at top of tree
                    new Sequence(
                        Spell.BuffSelf("Shadowmeld", ret => NeedShadowmeld()),
                        new Action( r => shadowMeldStart = DateTime.UtcNow )
                        )
                    );
            }

            // add racials cast within range during Combat
            Composite combatRacials = CreateCombatRacialInRangeBehavior();
            if (combatRacials != null)
                pri.AddChild(combatRacials);

            // just fail if no combat racials
            if (!SingularSettings.Instance.UseRacials || !pri.Children.Any() || SuppressGenericRacialBehavior)
                return new ActionAlwaysFail();

            return new Throttle(
                TimeSpan.FromMilliseconds(250),
                new Decorator(
                    req => !Spell.IsGlobalCooldown()
                        && !Spell.IsCastingOrChannelling()
                        && !SuppressGenericRacialBehavior,
                        pri
                    )
                );
        }

        private static Composite CreateCombatRacialInRangeBehavior()
        {
            PrioritySelector priCombat = new PrioritySelector();

            // not a racial, but best place to handle it
            if (SpellManager.HasSpell("Lifeblood"))
            {
                priCombat.AddChild(
                    Spell.HandleOffGCD(Spell.BuffSelf("Lifeblood", ret => !PartyBuff.WeHaveBloodlust))
                    );
            }

            if (SpellManager.HasSpell("Berserking"))
            {
                priCombat.AddChild(
                    Spell.HandleOffGCD(Spell.BuffSelf("Berserking", ret => !PartyBuff.WeHaveBloodlust))
                    );
            }

            if (SpellManager.HasSpell("Blood Fury"))
            {
                priCombat.AddChild(
                    Spell.HandleOffGCD(Spell.BuffSelf("Blood Fury", ret => true))
                    );
            }

            if (priCombat.Children.Any())
            {
                return new Decorator(
                    req =>
                    {
                        if (!StyxWoW.Me.Combat || !StyxWoW.Me.GotTarget())
                            return false;
                        if (StyxWoW.Me.IsMelee())
                            return StyxWoW.Me.CurrentTarget.IsWithinMeleeRange;
                        return !StyxWoW.Me.IsMoving && StyxWoW.Me.CurrentTarget.SpellDistance() < 40;
                    },
                    priCombat
                    );
            }

            return null;
        }

        private static bool NeedShadowmeld()
        {
            if (StyxWoW.Me.Race != WoWRace.NightElf)
                return false;

            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
            {
                if (SingularSettings.Instance.ShadowmeldSoloHealthPct > 0
                    && StyxWoW.Me.HealthPercent <= SingularSettings.Instance.ShadowmeldSoloHealthPct)
                    return true;
            }
            else if (SingularSettings.Instance.ShadowmeldThreatDrop)
            {
                if (Group.MeIsTank)
                    return false;

                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                {
                    if (!Unit.NearbyUnfriendlyUnits.Any(unit => unit.CurrentTargetGuid == StyxWoW.Me.Guid && (unit.Class == WoWClass.Hunter || unit.Class == WoWClass.Mage || unit.Class == WoWClass.Priest || unit.Class == WoWClass.Warlock)))
                    {
                        return true;    // since likely a ranged target
                    }
                }
                else if (Unit.NearbyUnfriendlyUnits.All(unit => unit.CurrentTargetGuid != StyxWoW.Me.Guid))
                {
                    return false;
                }

                if (Group.AnyTankNearby)
                    return true;
            }

            // need to add logic to wait for pats, or for PVP losing ranged targets may be enough
            return false;
        }

        static WoWUnit shadowMeldAggro { get; set; }
        static DateTime shadowMeldStart { get; set; }

        public static Composite CreateCancelShadowmeld()
        {
            if (StyxWoW.Me.Race != WoWRace.NightElf)
                return new ActionAlwaysFail();

            return new Sequence(
                ctx => StyxWoW.Me.GetAllAuras().FirstOrDefault( a => a.Name == "Shadowmeld"),

                // fail sequence if no aura
                new Action( r =>
                {
                    if (r == null)
                        return RunStatus.Failure;
                    return RunStatus.Success;
                }),

                new PrioritySelector(
                    new Sequence(
                        new Action( r =>
                        {
                            const int timeLimitSeconds = 20;
                            TimeSpan timeLimit = TimeSpan.FromSeconds(timeLimitSeconds);
                            DateTime now = DateTime.UtcNow;

                            if (shadowMeldStart == null || shadowMeldStart == DateTime.MinValue)
                                shadowMeldStart = now;

                            WoWAura aura = (WoWAura) r;
                            if ((now - shadowMeldStart) > timeLimit)
                            {
                                Logger.Write(LogColor.Cancel, "/cancelaura shadowmeld (exceeded {0:F1} seconds)", timeLimit.TotalSeconds);
                                return RunStatus.Success;
                            }

                            shadowMeldAggro = Unit.UnfriendlyUnits()
                                .Where(u => u.MyReaction == WoWUnitReaction.Hostile && u.SpellDistance() < (u.MyAggroRange + 5))
                                .FirstOrDefault();
                            if (shadowMeldAggro == null)
                            {
                                Logger.Write(LogColor.Cancel, "/cancelaura shadowmeld (no hostile mobs in aggro range)");
                                return RunStatus.Success;
                            }

                            // otherwise, exit sequence with failure
                            return RunStatus.Failure;
                        }),
                        new Action( r => StyxWoW.Me.CancelAura("Shadowmeld") ),
                        new PrioritySelector(
                            new Wait( 1, until => !StyxWoW.Me.HasAura("Shadowmeld"), new ActionAlwaysSucceed()),
                            new Action( r =>
                            {
                                Logger.WriteDiagnostic("Shadowmeld: aura not removed after cancel");
                                return RunStatus.Failure;
                            })
                            ),
                        new Action( r =>
                        {
                            Logger.WriteDiagnostic("Shadowmeld: removed - after {0:F1} seconds", (DateTime.UtcNow - shadowMeldStart).TotalSeconds );
                            shadowMeldStart = DateTime.MinValue;
                            return RunStatus.Success;
                        })
                        ),
                    new ThrottlePasses(
                        1,
                        TimeSpan.FromSeconds(5),
                        RunStatus.Success,
                        new Action( r =>
                        {
                            WoWAura aura = (WoWAura) r;
                            Logger.Write( LogColor.Hilite, "Shadowmeld: wait for {0} @ {1:F1} yds to clear area", shadowMeldAggro.SafeName(), shadowMeldAggro.SpellDistance());
                            Logger.WriteDiagnostic("Shadowmeld: {0} @ {1:F1} has {2:F1} aggro range with me", shadowMeldAggro.SafeName(), shadowMeldAggro.SpellDistance(), shadowMeldAggro.MyAggroRange);
                        })
                        )
                    )
                );
        }

        // [Behavior(BehaviorType.Combat, priority: 997)]
        public static Composite CreatePotionAndHealthstoneBehavior()
        {
            return Item.CreateUsePotionAndHealthstone(SingularSettings.Instance.PotionHealth, SingularSettings.Instance.PotionMana);
        }

        public static Composite CreateGarrisonAbilityBehaviour()
        {
            const string GARRISON_ABILITY = "Garrison Ability";
            const int ARTILLERY_STRIKE = 162075;

            return new PrioritySelector(
                ctx =>
                {
                    SpellFindResults sfr;
                    if (SpellManager.FindSpell(GARRISON_ABILITY, out sfr))
                    {
                        if (sfr.Override != null && usableGarrisonAbility.Contains(sfr.Override.Name))
                        {
                            return sfr.Override;
                        }
                    }
                    return null;
                },

                new Decorator(
                    req =>
                    {
                        if (req == null)
                            return false;
                        if (!Unit.ValidUnit(StyxWoW.Me.CurrentTarget))
                            return false;
                        if (!Spell.CanCastHack(GARRISON_ABILITY, StyxWoW.Me.CurrentTarget))
                            return false;

                        int mobCount = Unit.UnitsInCombatWithUsOrOurStuff(15).Count();
                        if (mobCount > 0)
                        {
                            if (mobCount >= SingularSettings.Instance.GarrisonAbilityMobCount)
                                return true;

                            if (StyxWoW.Me.HealthPercent < SingularSettings.Instance.GarrisonAbilityHealth)
                            {
                                if (mobCount > 1)
                                    return true;
                                if (StyxWoW.Me.CurrentTarget.TimeToDeath(-1) > 10)
                                    return true;
                                if (StyxWoW.Me.CurrentTarget.IsPlayer)
                                    return true;
                                if (StyxWoW.Me.CurrentTarget.MaxHealth > (StyxWoW.Me.MaxHealth * 2) && StyxWoW.Me.CurrentTarget.CurrentHealth > StyxWoW.Me.CurrentHealth)
                                    return true;
                                if (StyxWoW.Me.HealthPercent < SingularSettings.Instance.GarrisonAbilityHealth / 2)
                                    return true;
                            }
                        }

                        return false;
                    },
                    new Throttle(
                        2,
                        new Sequence(
                            new Action(r => Logger.Write(LogColor.Hilite, "^Garrison Ability: using {0} #{1} now", ((WoWSpell)r).Name, ((WoWSpell)r).Id)),
                            new PrioritySelector(
                                Spell.Cast(GARRISON_ABILITY, req => ((WoWSpell)req).Id != ARTILLERY_STRIKE),
                                Spell.CastOnGround(GARRISON_ABILITY, on => StyxWoW.Me.CurrentTarget, req => ((WoWSpell)req).Id == ARTILLERY_STRIKE),
                                new Action(r => { Logger.Write(LogColor.Hilite, "^Garrison Ability: cast failed"); return RunStatus.Failure; })
                                ),
                            new Action(r => Logger.WriteDiagnostic("Garrison Ability: successfully used"))
                            )
                        )
                    )
                );

        }

        private static List<string> usableGarrisonAbility = new List<string>()
        {
            "Call to Arms",
            "Champion's Honor",
            "Artillery Strike",
            "Guardian Orb"
        };

    }


    public static class NoContextAvailable
    {
        public static Composite CreateDoNothingBehavior()
        {
            return new Throttle(15,
                new Action(r => Logger.Write("No Context Available - do nothing while we wait"))
                );
        }
    }
}
