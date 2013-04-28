using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Singular.Settings;
using System.Drawing;

namespace Singular.ClassSpecific.Mage
{
    public class Arcane
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MageSettings MageSettings { get { return SingularSettings.Instance.Mage(); } }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Normal )]
        public static Composite CreateMageArcaneNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Movement.CreateEnsureMovementStoppedBehavior(34f),

                Spell.WaitForCast(true),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateArcaneDiagnosticOutputBehavior(),

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Spell.BuffSelf("Arcane Power"),

                        new Decorator(
                            ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 10 * 10 && u.IsCrowdControlled()),
                            new PrioritySelector(
                                Spell.BuffSelf("Frost Nova",
                                    ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                    u.DistanceSqr <= 8 * 8 && !u.IsFrozen() && !u.Stunned))
                                )),

                        // AoE comes first
                        new Decorator(
                            ret => Spell.UseAOE && Me.Level >= 25 && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(
                                // Movement.CreateEnsureMovementStoppedBehavior(5f),
                                Spell.Cast("Fire Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),
                                Spell.CastOnGround("Flamestrike", loc => Me.CurrentTarget.Location),
                                Spell.Cast("Arcane Barrage", ret => Me.HasAura("Arcane Charge", Math.Min(6, Unit.UnfriendlyUnitsNearTarget(10f).Count()))),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 3),
                                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 8f, 5f)
                                )
                            ),

                        // Movement.CreateEnsureMovementStoppedBehavior(35f),
                        Common.CreateMagePolymorphOnAddBehavior(),

                        Spell.BuffSelf("Evocation", ret => Me.ManaPercent < 30),

                        // Living Bomb in CombatBuffs()
                        Spell.Cast("Arcane Blast",
                            ret => !Me.IsMoving && (!Me.HasAura("Arcane Charge", 6) || Me.HasAuraExpired("Arcane Charge", 3))),
                        Spell.Cast("Arcane Barrage",
                            ret => Me.IsMoving && Me.HasAuraExpired("Arcane Charge", 2)),
                        Spell.Cast("Arcane Missiles", ret => Me.HasAura("Arcane Missiles!", 2)),
                        Spell.Cast("Arcane Blast", ret => Me.ManaPercent >= 90),
                        // Spell.Cast("Scorch", ret => Me.ManaPercent < 90),

                        Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast"))
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 39f, 34f)
                );
        }

        #endregion

        #region Battleground Rotation
        [Behavior(BehaviorType.Pull|BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Battlegrounds)]
        public static Composite CreateArcaneMagePvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Movement.CreateEnsureMovementStoppedBehavior(34f),
       
                Spell.WaitForCast(true),

                // Defensive stuff
                Spell.BuffSelf("Ice Block", ret => Me.HealthPercent < 10 && !Me.ActiveAuras.ContainsKey("Hypothermia")),
                Spell.BuffSelf("Mana Shield", ret => Me.HealthPercent <= 75),
                Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 11 && !u.IsFrozen())),
                Common.CreateMagePolymorphOnAddBehavior(),





                Spell.BuffSelf("Mana Shield", ret => Me.ManaPercent < 30),
                Spell.BuffSelf("Evocation", ret => Me.ManaPercent < 30 && (Me.HasAura("Mana Shield") || !SpellManager.HasSpell("Mana Shield"))),
                Spell.BuffSelf("Arcane Power"),
                Spell.BuffSelf("Mirror Image"),
                Spell.BuffSelf("Flame Orb"),
                Spell.Cast("Arcane Missiles", ret => Me.HasAura("Arcane Missiles!")),
                Spell.Cast("Arcane Barrage", ret => Me.GetAuraByName("Arcane Charge") != null && Me.GetAuraByName("Arcane Charge").StackCount >= 4),
                Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast")),
                Spell.Cast("Arcane Blast"),
                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 39f, 34f)
                
                );
        }
        

        #endregion

        #region Instance Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Instances)]
        public static Composite CreateMageArcaneInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Movement.CreateEnsureMovementStoppedBehavior(34f),

                Spell.WaitForCast(true),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateArcaneDiagnosticOutputBehavior(),

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Spell.BuffSelf("Arcane Power"),

                        // AoE comes first
                        new Decorator(
                            ret => Spell.UseAOE && Me.Level >= 25 && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(
                                // Movement.CreateEnsureMovementStoppedBehavior(5f),
                                Spell.Cast("Fire Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),
                                Spell.CastOnGround("Flamestrike", loc => Me.CurrentTarget.Location),
                                Spell.Cast("Arcane Barrage", ret => Me.HasAura( "Arcane Charge", Math.Min( 6, Unit.UnfriendlyUnitsNearTarget(10f).Count()))),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 3),
                                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 8f, 5f)
                                )
                            ),

                        // Movement.CreateEnsureMovementStoppedBehavior(35f),
                        Spell.BuffSelf("Evocation", ret => Me.ManaPercent < 30),

                        // Living Bomb in CombatBuffs()
                        Spell.Cast("Arcane Blast", 
                            ret => !Me.IsMoving && (!Me.HasAura("Arcane Charge", 6) || Me.HasAuraExpired("Arcane Charge", 3))),
                        Spell.Cast("Arcane Barrage",  
                            ret => Me.IsMoving && Me.HasAuraExpired("Arcane Charge", 2)),
                        Spell.Cast("Arcane Missiles", ret => Me.HasAura("Arcane Missiles!", 2)),
                        Spell.Cast("Arcane Blast", ret => Me.ManaPercent >= 90),
                        // Spell.Cast("Scorch", ret => Me.ManaPercent < 90),

                        Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast"))
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 39f, 34f)
                );
        }

        #endregion

        #region Diagnostics

        private static Composite CreateArcaneDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new Throttle(1,
                new Action(ret =>
                {
                    string line = string.Format(".... h={0:F1}%/m={1:F1}%, moving={2}, arcchg={3} {4:F0} ms, arcmiss={5} {6:F0} ms",
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving,
                        Me.GetAuraStacks("Arcane Charge"),
                        Me.GetAuraTimeLeft("Arcane Charge").TotalMilliseconds,
                        Me.GetAuraStacks("Arcane Missiles!"),
                        Me.GetAuraTimeLeft("Arcane Missiles!").TotalMilliseconds
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target == null)
                        line += ", target=(null)";
                    else
                        line += string.Format(", target={0} @ {1:F1} yds, h={2:F1}%, face={3}, loss={4}, livbomb={5:F0} ms",
                            target.SafeName(),
                            target.Distance,
                            target.HealthPercent,
                            Me.IsSafelyFacing(target),
                            target.InLineOfSpellSight,
                            target.GetAuraTimeLeft("Living Bomb").TotalMilliseconds 
                            );

                    Logger.WriteDebug(Color.Wheat, line);
                    return RunStatus.Success;
                })
                );
        }

        #endregion
    }
}
