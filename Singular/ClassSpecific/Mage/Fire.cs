using System;
using System.Drawing;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Mage
{
    public class Fire
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static MageSettings MageSettings => SingularSettings.Instance.Mage();
        private static bool HasArtifact => Me.Inventory.Equipped.MainHand.Entry == 128820;
	    #region Normal Rotation

        [Behavior(BehaviorType.Heal, WoWClass.Mage, WoWSpec.MageFire)]
        public static Composite CreateMageFireHeal()
        {
            return new PrioritySelector(
                CreateFireDiagnosticOutputBehavior()
                );
        }


        private static DateTime _lastPyroPull = DateTime.MinValue;

        [Behavior(BehaviorType.Pull, WoWClass.Mage, WoWSpec.MageFire)]
        public static Composite CreateMageFireNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Common.CreateMagePullBuffs(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Spell.Cast("Combustion", ret => Me.CurrentTarget.HasMyAura("Ignite")),
                        Spell.Cast("Fire Blast", ret => Me.HasActiveAura("Heating Up")),
                        new Sequence(
                            Spell.Cast("Pyroblast", req =>
                            {
                                if (Me.CurrentTarget.IsTrivial())
                                    return false;
                                if (Me.CurrentTarget.SpellDistance() > 18)
                                    return true;
                                if (DateTime.UtcNow > _lastPyroPull.AddMilliseconds(3000))
                                    return true;

                                return false;
                                }),
                            new Action( r => _lastPyroPull = DateTime.UtcNow )
                            ),
                        Spell.Cast("Blast Wave"), // Slows movement, 100% increased damage to primary target.
                        Spell.Cast("Fire Blast", ret => !SpellManager.HasSpell("Inferno Blast")),
                        Spell.Cast("Scorch"),
                        Spell.Cast("Fireball")
                       )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFire)]
        public static Composite CreateMageFireNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        new Action( r => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; } ),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        // Uncomment when MeshTrace is working.
                        //
                        //Common.CreateMageAvoidanceBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        // Uncomment when MeshTrace is working.
                        //
                        //
                        // new PrioritySelector(
                        //     ctx => Unit.UnitsInCombatWithMeOrMyStuff(12)
                        //         .FirstOrDefault(
                        // u => u.IsTargetingMeOrPet
                        // && (u.IsStressful() || (u.Guid == Me.CurrentTargetGuid && u.TimeToDeath() > 6))
                        // && !u.IsCrowdControlled()
                        //  ),
                        // Common.CreateSlowMeleeBehavior()
                        // ),

                        Spell.Cast("Flame On", ret => Spell.GetCharges("Fire Blast") <= 0 && !Me.HasActiveAura("Heating Up") && !Me.HasActiveAura("Hot Streak!")),
                        Spell.HandleOffGCD(Spell.Cast("Fire Blast", ret => Me.HasActiveAura("Heating Up"))), // Add HandleWhileCasting support?
                        Spell.BuffSelf("Mirror Image", ret => Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= MageSettings.MirrorImageCount),
						Spell.BuffSelf("Combustion"),
						Spell.CastOnGround("Flamestrike",
							on => Me.CurrentTarget,
							ret => Me.HasActiveAura("Hot Streak!") && Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3),
						Spell.Cast("Pyroblast", ret => Me.HasActiveAura("Hot Streak!")),

                        // Artifact Weapon
                        new Decorator(
                            ret => MageSettings.UseArtifactOnlyInAoE && Unit.UnfriendlyUnitsNearTarget(15).Count() > 1,
                            new PrioritySelector(
                                Spell.Cast("Phoenix's Flames",
                                    ret =>
                                        MageSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown
                                        || (MageSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.AtHighestDPSOpportunity && Me.HasActiveAura("Combustion") && Spell.GetCharges("Phoenix's Flames") >= 1 && (SpellManager.CanCast("Flame On") || Me.HasActiveAura("Flame On")))
                                        || (MageSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown || MageSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.None)
                                )
                            )
                        ),
                        Spell.Cast("Phoenix's Flames",
                            ret =>
                                !MageSettings.UseArtifactOnlyInAoE && MageSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown
                                || (MageSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.AtHighestDPSOpportunity && Me.HasActiveAura("Combustion") && Spell.GetCharges("Phoenix's Flames") >= 1 && (SpellManager.CanCast("Flame On") || Me.HasActiveAura("Flame On")))
                                || (MageSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown || MageSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.None) || (MageSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.AtHighestDPSOpportunity && Spell.GetCharges("Phoenix's Flames") >= 2 && (SpellManager.CanCast("Flame On") || Me.HasActiveAura("Flame On")))
                        ),

                        Spell.CastOnGround("Meteor", on => Me.CurrentTarget.Location),
                        Spell.Cast("Cinderstorm", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count(u => u.HasAura("Ignite")) > MageSettings.CinderstormCount),
						Spell.Cast("Dragon's Breath",
							ret => Me.GetAuraTimeLeft("Combustion") < Spell.GetSpellCastTime("Fireball") &&
									Unit.UnfriendlyUnits(12).Any(u => Me.IsSafelyFacing(u))),
						Spell.Cast("Living Bomb", ret => Me.CurrentTarget.TimeToDeath() > 12 && Unit.UnfriendlyUnitsNearTarget(10).Count(u => u.TimeToDeath() > 12) >= 2),
						Spell.Cast("Dragon's Breath", ret => Unit.UnfriendlyUnits(12).Any(u => Me.IsSafelyFacing(u))),
						Spell.Cast("Fireball", ret => !Me.HasActiveAura("Heating Up")),
						Spell.Cast("Scorch", ret => Me.IsMoving && (!Common.HasTalent(MageTalents.IceFloes) || Spell.GetCharges("Ice Floes") <= 0)),
                        Spell.Cast("Fireball"), // Last resort filler to prevent bot from standing around.

						new ActionAlwaysFail()
                        )
                    )
                );
        }

        #endregion

        #region Diagnostics

        private static Composite CreateFireDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new Throttle(1,
                new Action(ret =>
                {
                    string line = string.Format(".... h={0:F1}%/m={1:F1}%, moving={2}, heatup={3} {4:F0} ms, pyroblst={5} {6:F0} ms",
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving,
                        Me.GetAuraStacks("Heating Up"),
                        Me.GetAuraTimeLeft("Heating Up").TotalMilliseconds,
                        Me.GetAuraStacks("Pyroblast!"),
                        Me.GetAuraTimeLeft("Pyroblast!").TotalMilliseconds
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target == null)
                        line += ", target=(null)";
                    else
                    {
                        line += string.Format(", target={0} @ {1:F1} yds, h={2:F1}%, face={3}, loss={4}, frozen={5}",
                            target.SafeName(),
                            target.Distance,
                            target.HealthPercent,
                            Me.IsSafelyFacing(target),
                            target.InLineOfSpellSight,
                            target.TreatAsFrozen()
                            );

                        if (Common.HasTalent(MageTalents.NetherTempest))
                            line += string.Format(", nethtmp={0}", (long)target.GetAuraTimeLeft("Nether Tempest", true).TotalMilliseconds);
                        else if (Common.HasTalent(MageTalents.LivingBomb))
                            line += string.Format(", livbomb={0}", (long)target.GetAuraTimeLeft("Living Bomb", true).TotalMilliseconds);
                        else if (Common.HasTalent(MageTalents.FrostBomb))
                            line += string.Format(", frstbmb={0}", (long)target.GetAuraTimeLeft("Frost Bomb", true).TotalMilliseconds);
                    }

                    Logger.WriteDebug(Color.Wheat, line);
                    return RunStatus.Success;
                })
                );
        }

        #endregion
    }
}
