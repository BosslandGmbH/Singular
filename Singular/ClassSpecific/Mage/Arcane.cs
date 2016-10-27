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
using Singular.ClassSpecific.DemonHunter;
using Styx.Common;

namespace Singular.ClassSpecific.Mage
{
    public class Arcane
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static MageSettings MageSettings => SingularSettings.Instance.Mage();

	    private static uint ArcaneCharges => Me.GetAllAuras().Where(a => a.Name == "Arcane Charge").Select(a => a.StackCount).DefaultIfEmpty(0u).Max();

        #region Normal Rotation

        private static bool useArcaneNow;

        [Behavior(BehaviorType.Pull, WoWClass.Mage, WoWSpec.MageArcane)]
        public static Composite CreateMageArcaneNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateArcaneDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateMagePullBuffs(),
                        Spell.BuffSelf("Arcane Power"),
                        Spell.BuffSelf("Mirror Image", ret => Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= MageSettings.MirrorImageCount),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        new Action(r =>
                        {
                            useArcaneNow = false;
                            uint ac = ArcaneCharges;
                            if (ac >= 4)
                                useArcaneNow = true;
                            else
                            {
                                long ttd = Me.CurrentTarget.TimeToDeath();
                                if (ttd > 6)
                                    useArcaneNow = ac >= 2;
                                else if (ttd > 3)
                                    useArcaneNow = ac >= 1;
                                else
                                    useArcaneNow = ttd >= 0;
                            }
                            return RunStatus.Failure;
                        }),

                        Spell.Cast("Arcane Missiles", ret => useArcaneNow && Me.HasActiveAura("Arcane Missiles!")),
                        Spell.Cast("Arcane Barrage", ret => useArcaneNow),

                        // grinding or questing, if target meets these cast Flame Shock if possible
                        // 1. mob is less than 12 yds, so no benefit from delay in Lightning Bolt missile arrival
                        // 2. area has another player competing for mobs (we want to tag the mob quickly)
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.Distance < 12
                                || ObjectManager.GetObjectsOfType<WoWPlayer>(true, false).Any(p => p.Location.DistanceSquared(StyxWoW.Me.CurrentTarget.Location) <= 40 * 40),
                            new PrioritySelector(
                                Spell.Cast("Fire Blast"),
                                Spell.Cast("Arcane Barrage")
                                )
                            ),

                        Spell.Cast("Arcane Blast"),
                        Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast"))
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Mage, WoWSpec.MageArcane)]
        public static Composite CreateMageArcaneHeal()
        {
            return new PrioritySelector(
                CreateArcaneDiagnosticOutputBehavior("Combat")
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane)]
        public static Composite CreateMageArcaneNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Common.CreateMageAvoidanceBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

						Common.CreateMageRuneOfPowerBehavior(),

                        // Artifact Weapon
                        new Decorator(
                            ret => MageSettings.UseArtifactOnlyInAoE && Unit.UnfriendlyUnitsNearTarget(15).Count() > 1,
                            new PrioritySelector(
                                Spell.Cast("Fury of the Illidari",  ret => MageSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None)
                            )
                        ),
                        Spell.Cast("Fury of the Illidari", ret => !MageSettings.UseArtifactOnlyInAoE && MageSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None),


                        Spell.BuffSelf("Arcane Power", ret => ArcaneCharges >= 4),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        // AoE comes first
                        new Decorator(
                            ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(
                                Spell.Cast("Arcane Barrage", ret => ArcaneCharges >= 4),
								Spell.Cast("Arcane Orb"),
                                Spell.Cast(
                                    "Arcane Explosion",
                                    req => Unit.UnfriendlyUnits(8).Count() >= 3
                                    )
                                )
                            ),

						// Burn phase
                        new Decorator(
                            req => Me.HasAura("Arcane Power"),
                            new PrioritySelector(
								Spell.BuffSelf("Charged Up", ret => ArcaneCharges <= 0),
                                Spell.Cast("Arcane Missiles"),
                        Spell.Cast("Arcane Blast"),
								Spell.BuffSelf("Presence of Mind", ret => Me.GetAuraTimeLeft("Arcane Power").TotalSeconds < 4.5)
                        )
                                    ),

						Spell.Cast("Arcane Orb", ret => ArcaneCharges <= 0),
						Spell.Cast("Nether Tempest", ret => ArcaneCharges >= 4 && Me.CurrentTarget.GetAuraTimeLeft("Nether Tempest").TotalSeconds < 4),
                        Spell.Cast("Arcane Missiles", ret => Me.GetAuraStacks("Arcane Missiles!") >= 3 || Me.ManaPercent < 100 && Me.GetAuraStacks("Arcane Missiles!") >= 2),
						Spell.Cast("Supernova"),
						Spell.Cast("Arcane Barrage", ret => Me.ManaPercent < 20),
                                Spell.Cast("Arcane Blast")
                                )
                        )
                );
        }

        #endregion
		
        #region Diagnostics

        private static Composite CreateArcaneDiagnosticOutputBehavior(string state = null)
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    string line = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, moving={3}, arcchg={4} {5:F0} ms, arcmiss={6} {7:F0} ms",
                        state ?? Dynamics.CompositeBuilder.CurrentBehaviorType.ToString(),
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
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }
}
