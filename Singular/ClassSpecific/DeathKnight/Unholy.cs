using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Unholy
    {
        private const int SuddenDoom = 81340;

        private static DeathKnightSettings Settings
        {
            get { return SingularSettings.Instance.DeathKnight; }
        }

        #region Normal Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Normal)]
        public static Composite CreateDeathKnightUnholyNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.Buff("Chains of Ice",
                           ret =>
                           StyxWoW.Me.CurrentTarget.Fleeing && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                new Sequence(
                    Spell.Cast("Death Grip",
                               ret =>
                               StyxWoW.Me.CurrentTarget.DistanceSqr > 10*10 &&
                               (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.TaggedByMe)),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),
                // *** Cool downs ***
                Spell.BuffSelf("Unholy Frenzy",
                               ret =>
                               StyxWoW.Me.CurrentTarget.IsWithinMeleeRange &&
                               !StyxWoW.Me.HasAura("Heroism") && !StyxWoW.Me.HasAura("Bloodlust") &&
                               !StyxWoW.Me.HasAura("Time Warp") && !StyxWoW.Me.HasAura("Ancient Hysteria") &&
                               Common.UseLongCoolDownAbility),
                Spell.Cast("Summon Gargoyle",
                           ret =>
                           SingularSettings.Instance.DeathKnight.UseSummonGargoyle && Common.UseLongCoolDownAbility),

                new Decorator(
                    ret =>
                     Unit.UnfriendlyUnitsNearTarget(12f).Count() >=
                    SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                    new PrioritySelector(
                            Spell.Cast("Gorefiend's Grasp", ret => TalentManager.IsSelected((int)Common.DeathKnightTalents.GorefiendsGrasp)),
                            Spell.Cast("Remorseless Winter", ret => TalentManager.IsSelected((int)Common.DeathKnightTalents.RemoreselessWinter)),
                // Diseases
                        Spell.BuffSelf("Unholy Blight",
                                       ret =>
                                       TalentManager.IsSelected((int)Common.DeathKnightTalents.UnholyBlight) &&
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
                                   ret => TalentManager.IsSelected((int)Common.DeathKnightTalents.RollingBlood) &&
                                          StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10 &&
                                          !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases),
                        Spell.Cast("Pestilence",
                                   ret => !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases),
                        Spell.Cast("Dark Transformation",
                                   ret => StyxWoW.Me.GotAlivePet &&
                                          !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation") &&
                                          StyxWoW.Me.HasAura("Shadow Infusion") &&
                                          StyxWoW.Me.Auras["Shadow Infusion"].StackCount >= 5),
                        Spell.CastOnGround("Death and Decay",
                                           ret => StyxWoW.Me.CurrentTarget.Location,
                                           ret =>StyxWoW.Me.UnholyRuneCount == 2, false),
                        Spell.Cast("Blood Boil",
                                   ret =>
                                   StyxWoW.Me.CurrentTarget.DistanceSqr <= 10*10 &&
                                   StyxWoW.Me.DeathRuneCount > 0 ||
                                   (StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2)),
                        Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                        Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount == 2),
                        Spell.Cast("Death Coil",
                                   ctx =>
                                   StyxWoW.Me.HasAura(SuddenDoom) || StyxWoW.Me.RunicPowerPercent >= 80 ||
                                   !StyxWoW.Me.GotAlivePet ||
                                   !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
                        Spell.Cast("Horn of Winter"),
                        Movement.CreateMoveToMeleeBehavior(true)
                        )),
                // Single target rotation.
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
                Spell.Cast("Scourge Strike"),
                Spell.Cast("Festering Strike"),
                Spell.Cast("Death Coil",
                           ret =>
                           !StyxWoW.Me.GotAlivePet || !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
                Spell.Cast("Horn of Winter"),
                Movement.CreateMoveToMeleeBehavior(true)
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
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                new Sequence(
                    Spell.Cast("Death Grip",
                               ret =>
                               StyxWoW.Me.CurrentTarget.DistanceSqr > 10*10 &&
                               (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.TaggedByMe)),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),
                Spell.Cast("Chains of Ice", ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 8*8),
                // *** Cool downs ***
                Spell.BuffSelf("Unholy Frenzy",
                               ret =>
                               StyxWoW.Me.CurrentTarget.IsWithinMeleeRange &&
                               !StyxWoW.Me.HasAura("Heroism") && !StyxWoW.Me.HasAura("Bloodlust") &&
                               !StyxWoW.Me.HasAura("Time Warp") && !StyxWoW.Me.HasAura("Ancient Hysteria") &&
                               Common.UseLongCoolDownAbility),
                Spell.Cast("Summon Gargoyle",
                           ret =>
                           SingularSettings.Instance.DeathKnight.UseSummonGargoyle && Common.UseLongCoolDownAbility),


                // Start AoE section
                new Decorator(
                    ret =>
                    Unit.UnfriendlyUnitsNearTarget(12f).Count() >=
                    SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                    new PrioritySelector(
                            Spell.Cast("Gorefiend's Grasp", ret => TalentManager.IsSelected((int)Common.DeathKnightTalents.GorefiendsGrasp)),
                            Spell.Cast("Remorseless Winter", ret => TalentManager.IsSelected((int)Common.DeathKnightTalents.RemoreselessWinter)),
                        // Diseases
                        Spell.BuffSelf("Unholy Blight",
                                       ret =>
                                       TalentManager.IsSelected((int)Common.DeathKnightTalents.UnholyBlight) &&
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
                                   ret => TalentManager.IsSelected((int) Common.DeathKnightTalents.RollingBlood) &&
                                          StyxWoW.Me.CurrentTarget.DistanceSqr <= 10*10 &&
                                          !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases),
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
                        Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                        Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount == 2),
                        Spell.Cast("Death Coil",
                                   ctx =>
                                   StyxWoW.Me.HasAura(SuddenDoom) || StyxWoW.Me.RunicPowerPercent >= 80 ||
                                   !StyxWoW.Me.GotAlivePet ||
                                   !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
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
                Spell.Cast("Horn of Winter"),
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
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // *** Cool downs ***
                Spell.BuffSelf("Unholy Frenzy",
                               ret =>
                               StyxWoW.Me.CurrentTarget.IsWithinMeleeRange &&
                               !StyxWoW.Me.HasAura("Heroism") && !StyxWoW.Me.HasAura("Bloodlust") &&
                               !StyxWoW.Me.HasAura("Time Warp") && !StyxWoW.Me.HasAura("Ancient Hysteria") &&
                               Common.UseLongCoolDownAbility),
                Spell.Cast("Summon Gargoyle",
                           ret =>
                           SingularSettings.Instance.DeathKnight.UseSummonGargoyle && Common.UseLongCoolDownAbility),

                // Start AoE section
                new Decorator(
                    ret =>
                    Settings.UseAoeInInstance &&  Unit.UnfriendlyUnitsNearTarget(12f).Count() >=
                    SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                    new PrioritySelector(
                        // Diseases
                        Spell.BuffSelf("Unholy Blight",
                        ret =>
                            TalentManager.IsSelected((int)Common.DeathKnightTalents.UnholyBlight) &&
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
                                   ret => TalentManager.IsSelected((int) Common.DeathKnightTalents.RollingBlood) &&
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
                        Spell.Cast("Remorseless Winter", ret => TalentManager.IsSelected((int)Common.DeathKnightTalents.RemoreselessWinter)),
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
                                   ret => SingularSettings.Instance.DeathKnight.UseAoeInInstance &&
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
                Spell.Cast("Horn of Winter"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
    }
}