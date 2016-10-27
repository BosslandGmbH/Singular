﻿using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot;
using System;
using System.Collections.Generic;
using System.Drawing;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.DemonHunter
{
    public class Havoc
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static DemonHunterSettings DemonHunterSettings => SingularSettings.Instance.DemonHunter();
        private static uint MaxFury => StyxWoW.Me.GetPowerInfo(WoWPowerType.Fury).Max;
        private static uint CurrentFury => StyxWoW.Me.GetPowerInfo(WoWPowerType.Fury).Current;
        private static uint FuryDeficit => MaxFury - CurrentFury;

        #region Normal Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DemonHunter, WoWSpec.DemonHunterHavoc)]
        public static Composite CreateDemonHunterHavocCombat()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => DemonHunterSettings.HavocUseSoulFragments &&
                    (Me.HealthPercent <= DemonHunterSettings.HavocSoulFragmentHealthPercent || CurrentFury <= DemonHunterSettings.HavocSoulFragmentFuryPercent),
                        Common.CreateCollectFragmentsBehavior(DemonHunterSettings.HavocSoulFragmentRange)
                    ),

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateDemonHunterPullMore(),

                        // Buffs + Mitigation
                        Spell.CastOnGround("Metamorphosis", on => Me.CurrentTarget, ret => Me.HealthPercent <= DemonHunterSettings.HavocMetamorphosisHealthPercent),
                        Spell.BuffSelf("Blur", ret => Me.HealthPercent <= DemonHunterSettings.BlurHealthPercent),
                        Spell.BuffSelf("Darkness", ret => Me.HealthPercent <= DemonHunterSettings.HavocDarknessHealthPercent),
                        Spell.BuffSelf("Chaos Blades", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        // Out of Range
                        Common.CreateThrowGlaiveBehavior(),

                        // Artifact Weapon
                        new Decorator(
                            ret => DemonHunterSettings.UseArtifactOnlyInAoE && Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 6) > 1,
                            new PrioritySelector(
                                Spell.Cast("Fury of the Illidari",
                                    ret =>
                                        DemonHunterSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown
                                        || (DemonHunterSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.AtHighestDPSOpportunity && Me.HasActiveAura("Momentum"))
                                )
                            )
                        ),
                        Spell.Cast("Fury of the Illidari",
                            ret =>
                                !DemonHunterSettings.UseArtifactOnlyInAoE &&
                                ( DemonHunterSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown
                                || (DemonHunterSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.AtHighestDPSOpportunity && Me.HasActiveAura("Momentum")) )
                        ),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.MeleeDistance() < 10) > 1,
                            new PrioritySelector(
                                Spell.Cast("Nemesis"),
                                Spell.Cast("Chaos Nova", ret => Common.HasTalent(DemonHunterTalents.UnleashedPower) || Me.HealthPercent <= 75),
                                Spell.Cast("Fel Rush",
                                    ret =>
                                        DemonHunterSettings.DPSWithFelRush && Me.CurrentTarget.Distance <= 15 && Me.IsSafelyFacing(Me.CurrentTarget, 5f) &&
                                        ( (Common.HasTalent(DemonHunterTalents.FelMastery) && CurrentFury <= 70) || (Unit.NearbyUnfriendlyUnits.Count() >= 3 && Spell.GetCharges("Fel Rush") > 1) )),
                                Spell.Cast("Vengeful Retreat",
                                    ret =>
                                        DemonHunterSettings.UseVengefulRetreat && Me.CurrentTarget.IsWithinMeleeRange &&
                                        (Common.HasTalent(DemonHunterTalents.Prepared) || CurrentFury <= 85)),
                                Spell.Cast("Fel Barrage", ret =>
                                    (Common.HasTalent(DemonHunterTalents.Momentum) && Me.HasActiveAura("Momentum") && Spell.GetCharges("Fel Barrage") >= 4)
                                    || (!Common.HasTalent(DemonHunterTalents.Momentum) && Spell.GetCharges("Fel Barrage") >= 4)
                                ),
                                Spell.Cast("Eye Beam", ret => (!Common.HasTalent(DemonHunterTalents.Momentum) || Me.HasActiveAura("Momentum"))), // WaitForFacing will rotate us around for this.
                                Spell.Cast("Chaos Strike", ret => Common.HasTalent(DemonHunterTalents.ChaosCleave) && Unit.NearbyUnfriendlyUnits.Count() <= 3),
                                Spell.Cast("Blade Dance", ret => Unit.UnfriendlyUnits(8).Count() >= 3),
                                Spell.Cast("Throw Glaive", ret =>
                                    (!Common.HasTalent(DemonHunterTalents.Momentum) && Common.HasTalent(DemonHunterTalents.Bloodlet))
                                    || Me.HasActiveAura("Momentum")
                                ),
                                Spell.Cast("Chaos Strike", ret => CurrentFury >= 70),
                                Spell.Cast("Throw Glaive"),
                                Spell.Cast("Chaos Strike", ret =>
                                    (!Common.HasTalent(DemonHunterTalents.DemonBlades) && CurrentFury >= 70)
                                    || (Common.HasTalent(DemonHunterTalents.DemonBlades) && CurrentFury >= 60)
                                ),
                                Spell.Cast("Demon's Bite", ret => !Common.HasTalent(DemonHunterTalents.DemonBlades))
                                )
                        ),

                        // Single Target Rotation
                        Spell.Cast("Vengeful Retreat",
                            ret =>
                                DemonHunterSettings.UseVengefulRetreat && Me.CurrentTarget.IsWithinMeleeRange &&
                                (CurrentFury <= 85 || Common.HasTalent(DemonHunterTalents.Prepared))),
                        Spell.Cast("Fel Rush",
                            ret =>
                                DemonHunterSettings.DPSWithFelRush && Me.CurrentTarget.Distance <= 15 && Me.IsSafelyFacing(Me.CurrentTarget, 5f) &&
                                ( (Common.HasTalent(DemonHunterTalents.FelMastery) && CurrentFury <= 70)
                                || Common.HasTalent(DemonHunterTalents.Momentum) && !Me.HasActiveAura("Momentum")
                                || Spell.GetCharges("Fel Rush") > 1)
                        ),
                        Spell.Cast("Eye Beam", ret => Common.HasTalent(DemonHunterTalents.Demonic)),
                        Spell.Cast("Fel Eruption"),
                        Spell.Cast("Blade Dance", ret =>
                            (!Common.HasTalent(DemonHunterTalents.Momentum) && Common.HasTalent(DemonHunterTalents.FirstBlood))
                            || Me.HasActiveAura("Momentum")
                        ),
                        Spell.Cast("Felblade", ret => FuryDeficit >= 30),
                        Spell.Cast("Throw Glaive", ret =>
                            (!Common.HasTalent(DemonHunterTalents.Momentum) && Common.HasTalent(DemonHunterTalents.Bloodlet))
                            || Me.HasActiveAura("Momentum")
                        ),
                        Spell.Cast("Fel Barrage", ret =>
                            (!Common.HasTalent(DemonHunterTalents.Momentum) && Spell.GetCharges("Fel Barrage") >= 5)
                            || (Me.HasActiveAura("Momentum") && Spell.GetCharges("Fel Barrage") >= 5)
                        ),
                        Spell.Cast("Annihilation", ret => FuryDeficit <= 30),
                        Spell.Cast("Chaos Strike", ret => CurrentFury >= 70),
                        Spell.Cast("Fel Barrage", ret =>
                            (!Common.HasTalent(DemonHunterTalents.Momentum) && Spell.GetCharges("Fel Barrage") >= 4)
                            || (Me.HasActiveAura("Momentum") && Spell.GetCharges("Fel Barrage") >= 4)
                        ),
                        Spell.Cast("Demon's Bite", ret => !Common.HasTalent(DemonHunterTalents.DemonBlades))
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion


    }
}