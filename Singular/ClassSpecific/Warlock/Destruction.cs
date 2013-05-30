using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Singular.Settings;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.Common;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using System.Drawing;


namespace Singular.ClassSpecific.Warlock
{
    // wowraids.org 
    public class Destruction
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }

        private static int _mobCount;

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.All)]
        public static Composite CreateWarlockDestructionNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateWarlockDiagnosticOutputBehavior( "Pull" ),
                        Helpers.Common.CreateAutoAttack(true),
                        Spell.Buff("Immolate", true, on => Me.CurrentTarget, ret => true, 3),
                        Spell.Cast("Incinerate")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.All, priority: 999)]
        public static Composite CreateAfflictionHeal()
        {
            return new PrioritySelector(
                CreateWarlockDiagnosticOutputBehavior("Combat")
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.All)]
        public static Composite CreateWarlockDestructionNormalCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        Helpers.Common.CreateAutoAttack(true),

                        Helpers.Common.CreateInterruptBehavior(),

                        new Action(ret =>
                        {
                            _mobCount = TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateAoeBehavior(),

                        new Decorator(
                            req => WarlockSettings.DestructionSpellPriority != 1,
                            new PrioritySelector(
                                // Icy Veins
                                Spell.Cast("Shadowburn", ret =>
                                {
                                    if (Me.CurrentTarget.HealthPercent < 20)
                                    {
                                        if (CurrentBurningEmbers >= 35)
                                            return true;
                                        if (Me.HasAnyAura("Dark Soul: Instability", "Skull Banner"))
                                            return true;
                                        if (Me.CurrentTarget.TimeToDeath(99) < 3)
                                            return true;
                                        if (Me.ManaPercent < 5)
                                            return true;
                                    }
                                    return false;
                                }),

                                Spell.Buff("Immolate", true, on => Me.CurrentTarget, ret => true, 3),
                                Spell.Cast("Conflagrate", req => Spell.GetCharges("Conflagrate") >= 2),
                                Spell.CastOnGround("Rain of Fire", on => Me.CurrentTarget, req => !Me.CurrentTarget.IsMoving && Me.CurrentTarget.HasAuraExpired("Rain of Fire", 1, true), false),

                                Spell.Cast("Chaos Bolt", ret =>
                                {
                                    if (BackdraftStacks >= 3)
                                    {
                                        if (CurrentBurningEmbers >= 35)
                                            return true;
                                        if (Me.HasAnyAura("Dark Soul: Instability", "Skull Banner"))
                                            return true;
                                    }
                                    return false;
                                }),

                                Spell.Cast("Conflagrate", req => Spell.GetCharges("Conflagrate") == 1),

                                Spell.Cast("Incinerate"),

                                Spell.Cast("Fel Flame", ret => Me.IsMoving && Me.CurrentTarget.GetAuraTimeLeft("Immolate").TotalMilliseconds.Between(300, 3000))
                                )
                            ),


                        new Decorator(
                            req => WarlockSettings.DestructionSpellPriority == 1,
                            new PrioritySelector(
                                // Noxxic
                                Spell.Cast("Shadowburn", ret => Me.CurrentTarget.HealthPercent < 20),
                                Spell.Buff("Immolate", true, on => Me.CurrentTarget, ret => true, 3),
                                Spell.Cast("Conflagrate"),
                                Spell.CastOnGround("Rain of Fire", on => Me.CurrentTarget, req => !Me.CurrentTarget.IsMoving && !Me.CurrentTarget.HasMyAura("Rain of Fire"), false),

                                Spell.Cast("Chaos Bolt", ret => Me.CurrentTarget.HealthPercent >= 20 && BackdraftStacks < 3),
                                Spell.Cast("Incinerate"),

                                Spell.Cast("Fel Flame", ret => Me.IsMoving && Me.CurrentTarget.GetAuraTimeLeft("Immolate").TotalMilliseconds.Between(300, 3000))
                                )
                            ),

                        Spell.Cast("Drain Life", ret => Me.HealthPercent <= WarlockSettings.DrainLifePercentage && !Group.AnyHealerNearby),
                        Spell.Cast("Shadow Bolt")
                        )
                    )
                );

        }

        public static Composite CreateAoeBehavior()
        {
            return new Decorator( 
                ret => Spell.UseAOE,
                new PrioritySelector(

                    new Decorator(
                        ret => _mobCount.Between( 2, 3),
                        Spell.Buff("Immolate", true, on => Unit.NearbyUnitsInCombatWithMe.FirstOrDefault( u => u.HasAuraExpired("Immolate", 3) && Spell.CanCastHack("Immolate", u) && Me.IsSafelyFacing(u, 150)), req => true)
                        ),

                    new Decorator(
                        ret => _mobCount >= 4,
                        new PrioritySelector(
                            new PrioritySelector(
                                ctx => Clusters.GetBestUnitForCluster( Unit.NearbyUnfriendlyUnits.Where(u => Me.IsSafelyFacing(u)), ClusterType.Radius, 8f),
                                Spell.CastOnGround( "Rain of Fire", 
                                    loc => ((WoWUnit)loc).Location, 
                                    req => req != null 
                                        && !Me.HasAura( "Rain of Fire")
                                        && 3 <= Unit.NearbyUnfriendlyUnits.Count(u => ((WoWUnit)req).Location.Distance(u.Location) <= 8))
                                ),
                            new PrioritySelector(
                                ctx => Clusters.GetBestUnitForCluster( Unit.NearbyUnfriendlyUnits.Where(u => Me.IsSafelyFacing(u)), ClusterType.Radius, 15f),
                                new Sequence(
                                    Spell.BuffSelf( "Fire and Brimstone", req => req != null && 3 <= Unit.NearbyUnfriendlyUnits.Count(u => ((WoWUnit)req).Location.Distance(u.Location) <= 15f)),
                                    new PrioritySelector(
                                        Spell.BuffSelf("Havoc"),
                                        Spell.Cast("Conflagarate", onUnit => (WoWUnit) onUnit),
                                        Spell.Buff("Immolate", onUnit => (WoWUnit)onUnit),
                                        Spell.Cast("Incinerate", onUnit => (WoWUnit) onUnit)
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        #endregion

        public static double CurrentBurningEmbers
        {
            get
            {
                return Me.GetPowerInfo(WoWPowerType.BurningEmbers).Current;
            }
        }

        static double ImmolateTime(WoWUnit u = null)
        {
            return (u ?? Me.CurrentTarget).GetAuraTimeLeft("Immolate", true).TotalSeconds;
        }

        static IEnumerable<WoWUnit> TargetsInCombat
        {
            get
            {
                return Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && u.IsTargetingUs() && !u.IsCrowdControlled() && StyxWoW.Me.IsSafelyFacing(u));
            }
        }

        static int BackdraftStacks
        {
            get
            {
                return (int) Me.GetAuraStacks("Backdraft");
            }
        }

        private static Composite CreateWarlockDiagnosticOutputBehavior(string s)
        {
            return new Throttle(1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        WoWUnit target = Me.CurrentTarget ?? Me;
                        Logger.WriteFile(LogLevel.Diagnostic, ".... [{0}] h={1:F1}%/m={2:F1}%, embers={3}, backdraft={4}, immolate={5}, enemy={6}% @ {7:F1} yds, mobcnt={8}",
                            s,
                            Me.HealthPercent,
                            Me.ManaPercent,
                            CurrentBurningEmbers,
                            BackdraftStacks,
                            (long)target.GetAuraTimeLeft("Immolate", true).TotalMilliseconds,
                            (int)target.HealthPercent,
                            target.Distance,
                            _mobCount
                            );
                        return RunStatus.Failure;
                    })
                )
            );
        }
    }
}