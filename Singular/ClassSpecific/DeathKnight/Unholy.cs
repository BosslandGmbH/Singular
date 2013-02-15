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

namespace Singular.ClassSpecific.DeathKnight
{
    public class Unholy
    {
        private const int SuddenDoom = 81340;
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DeathKnightSettings DeathKnightSettings { get { return SingularSettings.Instance.DeathKnight(); } }

        #region Normal Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Normal)]
        public static Composite CreateDeathKnightUnholyNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateGetOverHereBehavior(),

                        Common.CreateDarkSuccorBehavior(),

                        // Symbiosis
                        Spell.CastOnGround("Wild Mushroom: Plague", ret => StyxWoW.Me.CurrentTarget.Location, ret => Spell.UseAOE, false),

                        // *** Cool downs ***
                        Spell.BuffSelf("Unholy Frenzy",
                            ret => Me.CurrentTarget.IsWithinMeleeRange 
                                && !PartyBuff.WeHaveBloodlust
                                && Common.UseLongCoolDownAbility),

                        Spell.Cast("Summon Gargoyle", ret => DeathKnightSettings.UseSummonGargoyle && Common.UseLongCoolDownAbility),

                        // aoe
                        new Decorator(
                            ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= DeathKnightSettings.DeathAndDecayCount,
                            new PrioritySelector(
                                // Spell.Cast("Gorefiend's Grasp"),
                                Spell.Cast("Remorseless Winter"),
                                CreateUnholyAoeBehavior(),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        // Single target rotation.
                        // Diseases
                        Common.CreateApplyDiseases(),

                        // Execute
                        Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),

                        Spell.Cast("Dark Transformation",
                            ret => StyxWoW.Me.GotAlivePet
                                && !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                                && StyxWoW.Me.HasAura("Shadow Infusion", 5)),

                        Spell.Cast("Festering Strike", ret => StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2),
                        Spell.Cast("Scourge Strike"),

                        Spell.CastOnGround("Death and Decay",
                            ret => StyxWoW.Me.CurrentTarget.Location,
                            ret => StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount > 0, false),

                        Spell.Cast("Blood Tap"),

                        Spell.Cast("Death Coil", 
                            ret => Me.HasAura(SuddenDoom) 
                                || Me.CurrentRunicPower >= 80 
                                || (Me.BloodRuneCount+Me.FrostRuneCount+Me.UnholyRuneCount+Me.DeathRuneCount == 0)),

                        Spell.Cast("Horn of Winter"),

                        // *** 3 Lowbie Cast what we have Priority
                        new Decorator(
                            ret => !SpellManager.HasSpell("Scourge Strike"),
                            new PrioritySelector(
                                Spell.Buff("Icy Touch", true, "Frost Fever"),
                                Spell.Buff("Plague Strike", true, "Blood Plague"),
                                Spell.Cast("Death Strike", ret => Me.HealthPercent < 90),
                                Spell.Cast("Death Coil"),
                                Spell.Cast("Blood Strike"),
                                Spell.Cast("Icy Touch"),
                                Spell.Cast("Plague Strike")
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Composite CreateUnholyAoeBehavior()
        {
            return new PrioritySelector(
                // Spell.Cast("Gorefiend's Grasp", on => Me, ret => Unit.NearbyUnfriendlyUnits.Count( u => u.Distance.Between(10,20) && u.IsTargetingMeOrPet ),
                Spell.Cast("Remorseless Winter"),

            // Diseases
                Common.CreateApplyDiseases(),

                Spell.Cast("Dark Transformation",
                    ret => StyxWoW.Me.GotAlivePet
                        && !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                        && StyxWoW.Me.HasAura("Shadow Infusion", 5)),

            // spread the disease around.
                Spell.Cast("Blood Boil",
                    ret => Common.HasTalent( DeathKnightTalents.RollingBlood)
                        && StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10
                        && !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases),

                Spell.Cast("Pestilence",
                    ret => !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases),

                Spell.CastOnGround(
                    "Death and Decay",
                    ret => StyxWoW.Me.CurrentTarget.Location,
                    ret => StyxWoW.Me.UnholyRuneCount == 2,
                    false),

                Spell.Cast("Blood Boil",
                    ret => StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10
                        && StyxWoW.Me.DeathRuneCount > 0 || (StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2)),

                Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),

                Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount == 2),

                Spell.Cast("Death Coil",
                            ctx =>
                            StyxWoW.Me.HasAura(SuddenDoom) || StyxWoW.Me.RunicPowerPercent >= 80 ||
                            !StyxWoW.Me.GotAlivePet ||
                            !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
                Spell.Cast("Horn of Winter")
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightUnholyPvPCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateGetOverHereBehavior(),

                        Common.CreateDarkSuccorBehavior(),

                        // Symbiosis
                        Spell.CastOnGround("Wild Mushroom: Plague", ret => StyxWoW.Me.CurrentTarget.Location, ret => Spell.UseAOE, false),

                        // *** Cool downs ***
                        Spell.BuffSelf("Unholy Frenzy",
                                       ret =>
                                       StyxWoW.Me.CurrentTarget.IsWithinMeleeRange &&
                                       !PartyBuff.WeHaveBloodlust &&
                                       Common.UseLongCoolDownAbility),
                        Spell.Cast("Summon Gargoyle",
                            ret => DeathKnightSettings.UseSummonGargoyle && Common.UseLongCoolDownAbility),


                        // *** Single target rotation. ***
                        // Execute
                                Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                        // Diseases
                                Spell.Cast("Outbreak",
                                           ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                  !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                           "Frost Fever"),
                                Spell.Buff("Plague Strike", true, "Blood Plague"),

                        Spell.Cast("Dark Transformation",
                                   ret => StyxWoW.Me.GotAlivePet &&
                                          !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation") &&
                                          StyxWoW.Me.HasAura("Shadow Infusion") &&
                                          StyxWoW.Me.Auras["Shadow Infusion"].StackCount >= 5),
                        Spell.CastOnGround("Death and Decay",
                                           ret => StyxWoW.Me.CurrentTarget.Location,
                                           ret => StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount > 0, false),
                        Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount > 0),
                        Spell.Cast("Festering Strike", ret => StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2),
                        Spell.Cast("Death Coil",
                                   ret =>
                                   StyxWoW.Me.HasAura(SuddenDoom) || StyxWoW.Me.CurrentRunicPower >= 80),
                        Spell.Buff("Necrotic Strike"),
                        Spell.Cast("Scourge Strike"),
                        Spell.Cast("Festering Strike"),
                        Spell.Cast("Death Coil",
                                   ret =>
                                   !StyxWoW.Me.GotAlivePet || !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
                        Spell.Cast("Horn of Winter")
                        )
                    ),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotations

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Instances)]
        public static Composite CreateDeathKnightUnholyInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        // Symbiosis
                        Spell.CastOnGround("Wild Mushroom: Plague", ret => StyxWoW.Me.CurrentTarget.Location, ret => Spell.UseAOE, false),

                        // *** Cool downs ***
                        Spell.BuffSelf("Unholy Frenzy",
                                       ret =>
                                       StyxWoW.Me.CurrentTarget.IsWithinMeleeRange &&
                                       !PartyBuff.WeHaveBloodlust &&
                                       Common.UseLongCoolDownAbility),
                        Spell.Cast("Summon Gargoyle",
                                   ret =>
                                   DeathKnightSettings.UseSummonGargoyle && Common.UseLongCoolDownAbility),

                        // Start AoE section
                        new Decorator(
                            ret =>
                            Spell.UseAOE && DeathKnightSettings.UseAoeInInstance && Unit.UnfriendlyUnitsNearTarget(12f).Count() >=
                            DeathKnightSettings.DeathAndDecayCount,
                            new PrioritySelector(
                                // Diseases
                                Spell.BuffSelf("Unholy Blight",
                                ret =>
                                    Common.HasTalent( DeathKnightTalents.UnholyBlight) &&
                                    StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10 &&
                                    !StyxWoW.Me.HasAura("Unholy Blight")),

                                Spell.Cast("Outbreak",
                                           ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                  !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                           "Frost Fever"),
                                Spell.Buff("Plague Strike", true, "Blood Plague"),
                                // spread the disease around.
                                Spell.Cast("Blood Boil",
                                           ret => TalentManager.IsSelected((int) DeathKnightTalents.RollingBlood) &&
                                                  !StyxWoW.Me.HasAura("Unholy Blight") &&
                                                  StyxWoW.Me.CurrentTarget.DistanceSqr <= 10*10 && Common.ShouldSpreadDiseases),
                                Spell.Cast("Pestilence",
                                           ret => !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases),
                                Spell.Cast("Dark Transformation",
                                           ret => StyxWoW.Me.GotAlivePet &&
                                                  !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation") &&
                                                  StyxWoW.Me.HasAura("Shadow Infusion") &&
                                                  StyxWoW.Me.Auras["Shadow Infusion"].StackCount >= 5),
                                Spell.CastOnGround("Death and Decay",
                                                   ret => StyxWoW.Me.CurrentTarget.Location,
                                                   ret => StyxWoW.Me.UnholyRuneCount == 2, false),
                                Spell.Cast("Blood Boil",
                                           ret =>
                                           StyxWoW.Me.CurrentTarget.DistanceSqr <= 10*10 &&
                                           StyxWoW.Me.DeathRuneCount > 0 ||
                                           (StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2)),
                                // Execute
                                Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount == 2),
                                Spell.Cast("Death Coil",
                                           ctx =>
                                           StyxWoW.Me.HasAura(SuddenDoom) || StyxWoW.Me.RunicPowerPercent >= 80 ||
                                           !StyxWoW.Me.GotAlivePet ||
                                           !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
                                Spell.Cast("Remorseless Winter", ret => Common.HasTalent( DeathKnightTalents.RemoreselessWinter)),
                                Spell.Cast("Horn of Winter"),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )),
                
                        // *** Single target rotation. ***
                        // Execute
                                Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                        // Diseases
                                Spell.Cast("Outbreak",
                                           ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                  !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                           "Frost Fever"),
                                Spell.Buff("Plague Strike", true, "Blood Plague"),

                        Spell.Cast("Dark Transformation",
                                   ret => StyxWoW.Me.GotAlivePet &&
                                          !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation") &&
                                          StyxWoW.Me.HasAura("Shadow Infusion") &&
                                          StyxWoW.Me.Auras["Shadow Infusion"].StackCount >= 5),
                        Spell.CastOnGround("Death and Decay",
                                           ret => StyxWoW.Me.CurrentTarget.Location,
                                           ret => Spell.UseAOE && DeathKnightSettings.UseAoeInInstance &&
                                                  StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount > 0, false),
                        Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount > 0),
                        Spell.Cast("Festering Strike", ret => StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2),
                        Spell.Cast("Death Coil",
                                   ret =>
                                   StyxWoW.Me.HasAura(SuddenDoom) || StyxWoW.Me.CurrentRunicPower >= 80),
                        Spell.Cast("Scourge Strike"),
                        Spell.Cast("Festering Strike"),
                        Spell.Cast("Death Coil",
                                   ret =>
                                   !StyxWoW.Me.GotAlivePet || !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
                        Spell.Cast("Horn of Winter")
                        )
                    ),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
    }
}