using System.Collections.Generic;
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

namespace Singular.ClassSpecific.DeathKnight
{
    public class Frost
    {
        private const int KillingMachine = 51124;

        #region Normal Rotations

        private static List<WoWUnit> _nearbyUnfriendlyUnits;

        private static DeathKnightSettings Settings
        {
            get { return SingularSettings.Instance.DeathKnight; }
        }

        private static bool IsDualWelding
        {
            get { return StyxWoW.Me.Inventory.Equipped.MainHand != null && StyxWoW.Me.Inventory.Equipped.OffHand != null; }
        }

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Normal)]
        public static Composite CreateDeathKnightFrostNormalCombat()
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
                               ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10*10),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),
                // Cooldowns
                Spell.BuffSelf("Pillar of Frost"),
                // Start AoE section
                new PrioritySelector(ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(12f).ToList(),
                                     new Decorator(
                                         ret =>
                                         _nearbyUnfriendlyUnits.Count() >=
                                         SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                                         new PrioritySelector(
                                             Spell.Cast("Gorefiend's Grasp",
                                                        ret =>
                                                        TalentManager.IsSelected(
                                                            (int) Common.DeathKnightTalents.GorefiendsGrasp)),
                                             Spell.Cast("Remorseless Winter",
                                                        ret =>
                                                        TalentManager.IsSelected(
                                                            (int) Common.DeathKnightTalents.RemoreselessWinter)),
                                             // Diseases
                                             Spell.BuffSelf("Unholy Blight",
                                                            ret =>
                                                            TalentManager.IsSelected(
                                                                (int) Common.DeathKnightTalents.UnholyBlight) &&
                                                            StyxWoW.Me.CurrentTarget.DistanceSqr <= 10*10 &&
                                                            !StyxWoW.Me.HasAura("Unholy Blight")),
                                             Spell.Buff("Howling Blast", true,
                                                        ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                                        "Frost Fever"),
                                             Spell.Cast("Outbreak",
                                                        ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                               !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                             Spell.Buff("Icy Touch", true,
                                                        ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                                        "Frost Fever"),
                                             Spell.Buff("Plague Strike", true, "Blood Plague"),
                                             // spread the disease around.
                                             Spell.Cast("Blood Boil",
                                                        ret =>
                                                        TalentManager.IsSelected(
                                                            (int) Common.DeathKnightTalents.RollingBlood) &&
                                                        !StyxWoW.Me.HasAura("Unholy Blight") &&
                                                        StyxWoW.Me.CurrentTarget.DistanceSqr <= 10*10 &&
                                                        Common.ShouldSpreadDiseases),
                                             Spell.Cast("Pestilence",
                                                        ret =>
                                                        !StyxWoW.Me.HasAura("Unholy Blight") &&
                                                        Common.ShouldSpreadDiseases),
                                             // aoe priority
                                             // both Frost runes and/or both Death runes are up
                                             Spell.Cast("Howling Blast",
                                                        ret =>
                                                        StyxWoW.Me.FrostRuneCount == 2 || StyxWoW.Me.DeathRuneCount == 2),
                                             // both Unholy Runes are up
                                             Spell.CastOnGround("Death and Decay",
                                                                ret => StyxWoW.Me.CurrentTarget.Location,
                                                                ret => StyxWoW.Me.UnholyRuneCount == 2, false),
                                             // RP Capped 
                                             Spell.Cast("Frost Strike", ret => StyxWoW.Me.RunicPowerPercent > 80),
                                             Spell.Cast("Obliterate", ret => StyxWoW.Me.UnholyRuneCount == 2),
                                             Spell.Cast("Howling Blast"),
                                             Spell.CastOnGround("Death and Decay",
                                                                ret => StyxWoW.Me.CurrentTarget.Location, ret => true,
                                                                false),
                                             // Execute
                                             Spell.Cast("Soul Reaper",
                                                        ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                             Spell.Cast("Frost Strike",
                                                        ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                             Spell.Cast("Horn of Winter"),
                                             Movement.CreateMoveToMeleeBehavior(true)
                                             ))),
                // *** Dual Weld Single Target Priority
                new Decorator(ctx => IsDualWelding,
                              new PrioritySelector(
                                  // Execute
                                  Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                  // Diseases
                                  Spell.Buff("Howling Blast", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Cast("Outbreak",
                                             ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                    !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                  Spell.Buff("Icy Touch", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Buff("Plague Strike", true, "Blood Plague"),
                                  // Killing Machine
                                  Spell.Cast("Frost Strike",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.HasAura(KillingMachine)),
                                  Spell.Cast("Obliterate",
                                             ret =>
                                             StyxWoW.Me.HasAura(KillingMachine) && Common.UnholyRuneSlotsActive == 2),
                                  // RP Capped
                                  Spell.Cast("Frost Strike",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.RunicPowerPercent > 80),
                                  // Rime Proc
                                  Spell.Cast("Howling Blast",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.HasAura("Freezing Fog")),
                                  // both Unholy Runes are off cooldown
                                  Spell.Cast("Obliterate", ret => StyxWoW.Me.UnholyRuneCount == 2),
                                  Spell.Cast("Frost Strike"),
                                  Spell.Cast("Howling Blast"),
                                  Spell.Cast("Horn of Winter")
                                  )),
                // *** 2 Hand Single Target Priority
                new Decorator(ctx => !IsDualWelding,
                              new PrioritySelector(
                                  // Execute
                                  Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                  // Diseases
                                  // Diseases
                                  Spell.Buff("Howling Blast", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Cast("Outbreak",
                                             ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                    !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                  Spell.Buff("Icy Touch", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Buff("Plague Strike", true, "Blood Plague"),
                                  // Killing Machine
                                  Spell.Cast("Obliterate", ret => StyxWoW.Me.HasAura(KillingMachine)),
                                  // RP Capped
                                  Spell.Cast("Frost Strike",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.RunicPowerPercent > 80),
                                  // Rime Proc
                                  Spell.Cast("Howling Blast",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.HasAura("Freezing Fog")),
                                  Spell.Cast("Obliterate"),
                                  Spell.Cast("Frost Strike"),
                                  Spell.Cast("Horn of Winter")
                                  )),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightFrostPvPCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                new Sequence(
                    Spell.Cast("Death Grip",
                               ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10*10),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),
                Spell.Buff("Chains of Ice", ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10*10),
                // Cooldowns
                Spell.BuffSelf("Pillar of Frost"),
                // Start AoE section
                new PrioritySelector(ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(12f).ToList(),
                                     new Decorator(
                                         ret =>
                                         _nearbyUnfriendlyUnits.Count() >=
                                         SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                                         new PrioritySelector(
                                             Spell.Cast("Gorefiend's Grasp",
                                                        ret =>
                                                        TalentManager.IsSelected(
                                                            (int) Common.DeathKnightTalents.GorefiendsGrasp)),
                                             Spell.Cast("Remorseless Winter",
                                                        ret =>
                                                        TalentManager.IsSelected(
                                                            (int) Common.DeathKnightTalents.RemoreselessWinter)),
                                             // Diseases
                                             Spell.BuffSelf("Unholy Blight",
                                                            ret =>
                                                            TalentManager.IsSelected(
                                                                (int) Common.DeathKnightTalents.UnholyBlight) &&
                                                            StyxWoW.Me.CurrentTarget.DistanceSqr <= 10*10 &&
                                                            !StyxWoW.Me.HasAura("Unholy Blight")),
                                             Spell.Buff("Howling Blast", true,
                                                        ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                                        "Frost Fever"),
                                             Spell.Cast("Outbreak",
                                                        ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                               !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                             Spell.Buff("Icy Touch", true,
                                                        ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                                        "Frost Fever"),
                                             Spell.Buff("Plague Strike", true, "Blood Plague"),
                                             // spread the disease around.
                                             Spell.Cast("Blood Boil",
                                                        ret =>
                                                        TalentManager.IsSelected(
                                                            (int) Common.DeathKnightTalents.RollingBlood) &&
                                                        !StyxWoW.Me.HasAura("Unholy Blight") &&
                                                        StyxWoW.Me.CurrentTarget.DistanceSqr <= 10*10 &&
                                                        Common.ShouldSpreadDiseases),
                                             Spell.Cast("Pestilence",
                                                        ret =>
                                                        !StyxWoW.Me.HasAura("Unholy Blight") &&
                                                        Common.ShouldSpreadDiseases),
                                             // aoe priority
                                             // both Frost runes and/or both Death runes are up
                                             Spell.Cast("Howling Blast",
                                                        ret =>
                                                        StyxWoW.Me.FrostRuneCount == 2 || StyxWoW.Me.DeathRuneCount == 2),
                                             // both Unholy Runes are up
                                             Spell.CastOnGround("Death and Decay",
                                                                ret => StyxWoW.Me.CurrentTarget.Location,
                                                                ret => StyxWoW.Me.UnholyRuneCount == 2, false),
                                             Spell.Buff("Necrotic Strike"),
                                             // RP Capped 
                                             Spell.Cast("Frost Strike", ret => StyxWoW.Me.RunicPowerPercent > 80),
                                             Spell.Cast("Obliterate", ret => StyxWoW.Me.UnholyRuneCount == 2),
                                             Spell.Cast("Howling Blast"),
                                             Spell.CastOnGround("Death and Decay",
                                                                ret => StyxWoW.Me.CurrentTarget.Location, ret => true,
                                                                false),
                                             // Execute
                                             Spell.Cast("Soul Reaper",
                                                        ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                             Spell.Cast("Frost Strike",
                                                        ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                             Spell.Cast("Horn of Winter"),
                                             Movement.CreateMoveToMeleeBehavior(true)
                                             ))),
                // *** Dual Weld Single Target Priority
                new Decorator(ctx => IsDualWelding,
                              new PrioritySelector(
                                  // Execute
                                  Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                  // Diseases
                                  Spell.Buff("Howling Blast", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Cast("Outbreak",
                                             ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                    !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                  Spell.Buff("Icy Touch", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Buff("Plague Strike", true, "Blood Plague"),
                                  // Killing Machine
                                  Spell.Cast("Frost Strike",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.HasAura(KillingMachine)),
                                  Spell.Cast("Obliterate",
                                             ret =>
                                             StyxWoW.Me.HasAura(KillingMachine) && Common.UnholyRuneSlotsActive == 2),
                                  Spell.Buff("Necrotic Strike"),
                                  // RP Capped
                                  Spell.Cast("Frost Strike",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.RunicPowerPercent > 80),
                                  // Rime Proc
                                  Spell.Cast("Howling Blast",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.HasAura("Freezing Fog")),
                                  // both Unholy Runes are off cooldown
                                  Spell.Cast("Obliterate", ret => StyxWoW.Me.UnholyRuneCount == 2),
                                  Spell.Cast("Frost Strike"),
                                  Spell.Cast("Howling Blast"),
                                  Spell.Cast("Horn of Winter")
                                  )),
                // *** 2 Hand Single Target Priority
                new Decorator(ctx => !IsDualWelding,
                              new PrioritySelector(
                                  // Execute
                                  Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                  // Diseases
                                  // Diseases
                                  Spell.Buff("Howling Blast", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Cast("Outbreak",
                                             ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                    !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                  Spell.Buff("Icy Touch", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Buff("Plague Strike", true, "Blood Plague"),
                                  // Killing Machine
                                  Spell.Cast("Obliterate", ret => StyxWoW.Me.HasAura(KillingMachine)),
                                  Spell.Buff("Necrotic Strike"),
                                  // RP Capped
                                  Spell.Cast("Frost Strike",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.RunicPowerPercent > 80),
                                  // Rime Proc
                                  Spell.Cast("Howling Blast",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.HasAura("Freezing Fog")),
                                  Spell.Buff("Necrotic Strike"),
                                  Spell.Cast("Obliterate"),
                                  Spell.Cast("Frost Strike"),
                                  Spell.Cast("Horn of Winter")
                                  )),
                /*
                // Cooldowns
                Spell.BuffSelf("Pillar of Frost", ret => SingularSettings.Instance.DeathKnight.UsePillarOfFrost),
                Spell.BuffSelf("Raise Dead", ret => SingularSettings.Instance.DeathKnight.UseRaiseDead && !StyxWoW.Me.GotAlivePet),
                Spell.BuffSelf("Empower Rune Weapon", ret => SingularSettings.Instance.DeathKnight.UseEmpowerRuneWeapon && StyxWoW.Me.UnholyRuneCount == 0 && StyxWoW.Me.FrostRuneCount == 0 && StyxWoW.Me.DeathRuneCount == 0 && !SpellManager.CanCast("Frost Strike") && StyxWoW.Me.CurrentTarget.IsBoss()),

                // Start single target section
                Spell.Buff("Howling Blast", true, "Frost Fever"),
                Spell.Buff("Outbreak", true, "Frost Fever"),
                Spell.Buff("Icy Touch", true, "Frost Fever"),
                Spell.Buff("Plague Strike", true, "Blood Plague"),
                Spell.Cast("Blood Strike", ret => !SpellManager.HasSpell("Obliterate")),
                Spell.Buff("Necrotic Strike", ret => SingularSettings.Instance.DeathKnight.UseNecroticStrike),
                Spell.Cast("Obliterate"),
                Spell.Cast("Frost Strike"),
                Spell.Cast("Howling Blast", ret => StyxWoW.Me.HasAura("Freezing Fog")),
                Spell.Cast("Horn of Winter"),
                                         */
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotations

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Instances)]
        public static Composite CreateDeathKnightFrostInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // Cooldowns
                Spell.BuffSelf("Pillar of Frost"),
                // Start AoE section
                new PrioritySelector(ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(12f).ToList(),
                                     new Decorator(
                                         ret =>
                                         _nearbyUnfriendlyUnits.Count() >=
                                         SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                                         new PrioritySelector(
                                             // Diseases
                                             Spell.BuffSelf("Unholy Blight",
                                                            ret =>
                                                            TalentManager.IsSelected(
                                                                (int) Common.DeathKnightTalents.UnholyBlight) &&
                                                            StyxWoW.Me.CurrentTarget.DistanceSqr <= 10*10 &&
                                                            !StyxWoW.Me.HasAura("Unholy Blight")),
                                             Spell.Buff("Howling Blast", true,
                                                        ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                                        "Frost Fever"),
                                             Spell.Cast("Outbreak",
                                                        ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                               !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                             Spell.Buff("Icy Touch", true,
                                                        ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                                        "Frost Fever"),
                                             Spell.Buff("Plague Strike", true, "Blood Plague"),
                                             // spread the disease around.
                                             Spell.Cast("Blood Boil",
                                                        ret =>
                                                        TalentManager.IsSelected(
                                                            (int) Common.DeathKnightTalents.RollingBlood) &&
                                                        !StyxWoW.Me.HasAura("Unholy Blight") &&
                                                        StyxWoW.Me.CurrentTarget.DistanceSqr <= 10*10 &&
                                                        Common.ShouldSpreadDiseases),
                                             Spell.Cast("Pestilence",
                                                        ret =>
                                                        !StyxWoW.Me.HasAura("Unholy Blight") &&
                                                        Common.ShouldSpreadDiseases),
                                             // aoe priority
                                             // both Frost runes and/or both Death runes are up
                                             Spell.Cast("Howling Blast",
                                                        ret =>
                                                        StyxWoW.Me.FrostRuneCount == 2 || StyxWoW.Me.DeathRuneCount == 2),
                                             // both Unholy Runes are up
                                             Spell.CastOnGround("Death and Decay",
                                                                ret => StyxWoW.Me.CurrentTarget.Location,
                                                                ret => StyxWoW.Me.UnholyRuneCount == 2, false),
                                             // RP Capped 
                                             Spell.Cast("Frost Strike", ret => StyxWoW.Me.RunicPowerPercent > 80),
                                             Spell.Cast("Obliterate", ret => StyxWoW.Me.UnholyRuneCount == 2),
                                             Spell.Cast("Howling Blast"),
                                             Spell.CastOnGround("Death and Decay",
                                                                ret => StyxWoW.Me.CurrentTarget.Location, ret => true,
                                                                false),
                                             // Execute
                                             Spell.Cast("Soul Reaper",
                                                        ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                             Spell.Cast("Frost Strike",
                                                        ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                             Spell.Cast("Remorseless Winter",
                                                        ret =>
                                                        TalentManager.IsSelected(
                                                            (int) Common.DeathKnightTalents.RemoreselessWinter)),
                                             Spell.Cast("Horn of Winter"),
                                             Movement.CreateMoveToMeleeBehavior(true)
                                             ))),
                // *** Dual Weld Single Target Priority
                new Decorator(ctx => IsDualWelding,
                              new PrioritySelector(
                                  // Execute
                                  Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                  // Diseases
                                  Spell.Buff("Howling Blast", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Cast("Outbreak",
                                             ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                    !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                  Spell.Buff("Icy Touch", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Buff("Plague Strike", true, "Blood Plague"),
                                  // Killing Machine
                                  Spell.Cast("Frost Strike",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.HasAura(KillingMachine)),
                                  Spell.Cast("Obliterate",
                                             ret =>
                                             StyxWoW.Me.HasAura(KillingMachine) && Common.UnholyRuneSlotsActive == 2),
                                  // RP Capped
                                  Spell.Cast("Frost Strike",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.RunicPowerPercent > 80),
                                  // Rime Proc
                                  Spell.Cast("Howling Blast",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.HasAura("Freezing Fog")),
                                  // both Unholy Runes are off cooldown
                                  Spell.Cast("Obliterate", ret => StyxWoW.Me.UnholyRuneCount == 2),
                                  Spell.Cast("Frost Strike"),
                                  Spell.Cast("Howling Blast"),
                                  Spell.Cast("Horn of Winter")
                                  )),
                // *** 2 Hand Single Target Priority
                new Decorator(ctx => !IsDualWelding,
                              new PrioritySelector(
                                  // Execute
                                  Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                  // Diseases
                                  // Diseases
                                  Spell.Buff("Howling Blast", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Cast("Outbreak",
                                             ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                    !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                                  Spell.Buff("Icy Touch", true,
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),
                                             "Frost Fever"),
                                  Spell.Buff("Plague Strike", true, "Blood Plague"),
                                  // Killing Machine
                                  Spell.Cast("Obliterate", ret => StyxWoW.Me.HasAura(KillingMachine)),
                                  // RP Capped
                                  Spell.Cast("Frost Strike",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.RunicPowerPercent > 80),
                                  // Rime Proc
                                  Spell.Cast("Howling Blast",
                                             ret =>
                                             !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                             StyxWoW.Me.HasAura("Freezing Fog")),
                                  Spell.Cast("Obliterate"),
                                  Spell.Cast("Frost Strike"),
                                  Spell.Cast("Horn of Winter")
                                  )),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
    }
}