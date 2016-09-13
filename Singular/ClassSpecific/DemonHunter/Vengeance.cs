using System.Linq;
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
    public class Vengeance
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static DemonHunterSettings DemonHunterSettings => SingularSettings.Instance.DemonHunter();
        private static uint MaxPain => StyxWoW.Me.GetPowerInfo(WoWPowerType.Pain).Max;
        private static uint CurrentPain => StyxWoW.Me.GetPowerInfo(WoWPowerType.Pain).Current;
        public static uint PainDeficit => MaxPain - CurrentPain;

        #region Normal Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DemonHunter, WoWSpec.DemonHunterVengeance)]
        public static Composite CreateDemonHunterVengeanceCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        ctx =>
                            TankManager.Instance.TargetList.FirstOrDefault(u => u.IsWithinMeleeRange) ??
                            Me.CurrentTarget,

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateDemonHunterPullMore(),

                        new Decorator(
                            ret =>
                                SingularSettings.Instance.EnableTaunting &&
                                SingularRoutine.CurrentWoWContext == WoWContext.Instances
                                && TankManager.Instance.NeedToTaunt.Any()
                                && TankManager.Instance.NeedToTaunt.FirstOrDefault().InLineOfSpellSight,
                            new Throttle(TimeSpan.FromMilliseconds(1500),
                                new PrioritySelector(
                                    // Direct Taunt
                                    Spell.Cast("Torment",
                                        ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                                        ret => true),

                                    new Decorator(
                                        ret => TankManager.Instance.NeedToTaunt.Any() && Me.SpellDistance(TankManager.Instance.NeedToTaunt.FirstOrDefault()) > 10,
                                            Common.CreateThrowGlaiveBehavior()
                                        )
                                    )
                                )
                            ),

                        Common.CreateThrowGlaiveBehavior(),

                        // Mitigation
                        Spell.BuffSelf("Metamorphosis", ret => Me.HealthPercent <= DemonHunterSettings.VengeanceMetamorphosisHealthPercent),
                        Spell.BuffSelf("Empower Wards", ret => Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == StyxWoW.Me.Guid)),
                        Spell.BuffSelf("Darkness", ret => Me.HealthPercent <= DemonHunterSettings.VengeanceDarknessHealthPercent),
                        Spell.Cast("Soul Cleave", on => (WoWUnit)on, ret => Vengeance.CurrentPain >= 30 && Me.HealthPercent <= DemonHunterSettings.SoulCleaveHealthPercent),
                        Spell.BuffSelf("Demon Spikes", ret => Spell.GetCharges("Demon Spikes") > 1 || Me.HealthPercent <= DemonHunterSettings.DemonSpikesHealthPercent),
                        Spell.Cast("Fiery Brand", on => (WoWUnit)on, ret => Me.HealthPercent <= DemonHunterSettings.FieryBrandHealthPercent),

                        // High Priority Single+AoE
                        Spell.Cast("Soul Carver", on => (WoWUnit)on, ret => !DemonHunterSettings.UseArtifactOnlyInAoE && DemonHunterSettings.UseArtifactWeaponWhen != UseArtifactWeaponWhen.None),
                        Spell.Cast("Fel Devastation", on => (WoWUnit)on),
                        Spell.BuffSelf("Immolation Aura", ret => Unit.UnfriendlyUnits(8).Any()),
                        Spell.Cast("Soul Cleave", on => (WoWUnit)on, ret => CurrentPain >= 50),
                        Spell.HandleOffGCD(Spell.CastOnGround("Infernal Strike", on => (WoWUnit)on, ret => DemonHunterSettings.DPSInfernalStrike && Spell.GetCharges("Infernal Strike") > 1)),

                        // Average Priority AoE
                        new Decorator(
                            ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.MeleeDistance() < 10) > 1,
                            new PrioritySelector(
                                Spell.Cast("Soul Carver", on => (WoWUnit)on, ret => DemonHunterSettings.UseArtifactWeaponWhen != UseArtifactWeaponWhen.None),
                                Spell.Cast("Spirit Bomb", on => (WoWUnit)on, ret => !Me.CurrentTarget.HasAura("Frality") && Common.FindFragments(39).Any()),
                                Spell.Cast("Felblade", on => (WoWUnit)on),
                                Spell.Cast("Shear", on => (WoWUnit)on, ret => Me.HasActiveAura("Blade Turning")),
                                Spell.CastOnGround("Sigil of Flame", on => (WoWUnit)on, ret => Unit.UnfriendlyUnitsNearTarget(30).Count() >= DemonHunterSettings.SigilOfFlameCount, false),
                                Spell.Cast("Fiery Brand", on => (WoWUnit)on, ret => Common.HasTalent(DemonHunterTalents.BurningAlive)),
                                Spell.Cast("Fel Erruption", on => (WoWUnit)on)
                            )
                        ),

                        // Average Priority Single Target
                        Spell.Cast("Felblade", on => (WoWUnit)on),
                        Spell.Cast("Fel Erruption", on => (WoWUnit)on),
                        Spell.Cast("Spirit Bomb", on => (WoWUnit)on, ret => !Me.CurrentTarget.HasAura("Frality") && Common.FindFragments(39).Any()),
                        Spell.Cast("Shear", on => (WoWUnit)on, ret => Me.HasActiveAura("Blade Turning")),
                        Spell.Cast("Fracture", on => (WoWUnit)on, ret => CurrentPain >= 60),
                        Spell.CastOnGround("Sigil of Flame", on => (WoWUnit)on, ret => Unit.UnfriendlyUnitsNearTarget(30).Count() >= DemonHunterSettings.SigilOfFlameCount, false),


                        // Low Priority single target filler pain generator.
                        Spell.Cast("Shear", on => (WoWUnit)on)
                    )
                )

            );

        }
        #endregion
    }
}