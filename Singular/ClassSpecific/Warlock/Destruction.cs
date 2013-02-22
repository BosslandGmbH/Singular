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
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Movement.CreateEnsureMovementStoppedBehavior(35f),

                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),

                new Decorator(ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.Buff("Immolate", true, on => Me.CurrentTarget, ret => true, 3),
                        Spell.Cast("Incinerate")
                        )
                    ),

                Movement.CreateMoveToRangeAndStopBehavior(ret => Me.CurrentTarget, ret => 35f)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.All)]
        public static Composite CreateWarlockDestructionNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Movement.CreateEnsureMovementStoppedBehavior(35f),

                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),

                new Decorator(ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        Spell.BuffSelf("Flames of Xoroth", ret => !Me.GotAlivePet && !Me.HasAura("Grimoire of Sacrifice") && CurrentBurningEmbers >= 10),

                        Helpers.Common.CreateInterruptBehavior(),

                        new Action(ret =>
                        {
                            _mobCount = TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateWarlockDiagnosticOutputBehavior(),

                        CreateAoeBehavior(),

                        Spell.Cast( "Shadowburn", ret => Me.CurrentTarget.HealthPercent <= 20),
                        Spell.Cast("Chaos Bolt", ret => Me.CurrentTarget.HealthPercent > 20 && BackdraftStacks < 3),

                        Spell.Cast("Conflagrate"),
                        Spell.Buff("Immolate", true, on => Me.CurrentTarget, ret => true, 3),
                        Spell.Cast("Drain Life", ret => Me.HealthPercent < 40 && !Group.AnyHealerNearby),
                        Spell.Cast("Incinerate"),

                        Spell.Cast("Fel Flame", ret => Me.IsMoving),

                        Spell.Cast("Shadow Bolt")
                        )
                    ),

                Movement.CreateMoveToRangeAndStopBehavior(ret => Me.CurrentTarget, ret => 35f)
                );

        }

        public static Composite CreateAoeBehavior()
        {
            return new Decorator( 
                ret => Spell.UseAOE,
                new PrioritySelector(

                    new Decorator(
                        ret => _mobCount >= 3,
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

        static double CurrentBurningEmbers
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
                WoWAura bkdrft = Me.GetAuraByName("Backdraft");
                return bkdrft == null ? 0 : (int) bkdrft.StackCount;
            }
        }

        private static Composite CreateWarlockDiagnosticOutputBehavior()
        {
            return new Throttle(1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        WoWUnit target = Me.CurrentTarget ?? Me;
                        Logger.WriteFile(LogLevel.Diagnostic, ".... h={0:F1}%/m={1:F1}%, embers={2}, backdraft={3}, immolate={4}, enemy={5}% @ {6:F1} yds, mobcnt={7}",
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