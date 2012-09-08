using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Frost
    {
        #region Normal Rotations

        [Behavior(BehaviorType.Combat,WoWClass.DeathKnight,WoWSpec.DeathKnightFrost,WoWContext.Normal)]
        public static Composite CreateDeathKnightFrostNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.Buff("Chains of Ice", ret => StyxWoW.Me.CurrentTarget.Fleeing && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                new Sequence(
                    Spell.Cast("Death Grip",
                                ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),
                // Anti-magic shell - no cost and doesnt trigger GCD 
                    Spell.BuffSelf("Anti-Magic Shell",
                                    ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                                u.CurrentTargetGuid == StyxWoW.Me.Guid)),

                Spell.BuffSelf("Icebound Fortitude",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.IceboundFortitudePercent &&
                               SingularSettings.Instance.DeathKnight.UseIceboundFortitude),
                Spell.BuffSelf("Lichborne", ret => SingularSettings.Instance.DeathKnight.UseLichborne &&
                                                   (StyxWoW.Me.IsCrowdControlled() ||
                                                   StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.LichbornePercent)),
                /*
                Spell.BuffSelf("Death Coil",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent &&
                               StyxWoW.Me.HasAura("Lichborne")),
                Spell.Cast("Death Strike",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent),

                // Cooldowns
                Spell.BuffSelf("Pillar of Frost", ret => SingularSettings.Instance.DeathKnight.UsePillarOfFrost),
                Spell.BuffSelf("Raise Dead", ret => SingularSettings.Instance.DeathKnight.UseRaiseDead && !StyxWoW.Me.GotAlivePet),
                Spell.BuffSelf("Empower Rune Weapon", ret => SingularSettings.Instance.DeathKnight.UseEmpowerRuneWeapon && StyxWoW.Me.UnholyRuneCount == 0 && StyxWoW.Me.FrostRuneCount == 0 && StyxWoW.Me.DeathRuneCount == 0 && !SpellManager.CanCast("Frost Strike") && StyxWoW.Me.CurrentTarget.IsBoss()),

                // Start AoE section
                new Decorator(ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                              new PrioritySelector(
                                  Spell.Cast("Howling Blast",
                                             ret => StyxWoW.Me.FrostRuneCount == 2 || StyxWoW.Me.DeathRuneCount == 2),
                                  Spell.CastOnGround("Death and Decay",
                                        ret => StyxWoW.Me.CurrentTarget.Location,
                                        ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay && StyxWoW.Me.UnholyRuneCount == 2),
                                  Spell.Cast("Plague Strike", ret => StyxWoW.Me.UnholyRuneCount == 2),
                                  Spell.Cast("Frost Strike",
                                             ret => StyxWoW.Me.CurrentRunicPower == StyxWoW.Me.MaxRunicPower),
                                  Spell.Cast("Horn of Winter"),
                                  Spell.CastOnGround("Death and Decay",
                                        ret => StyxWoW.Me.CurrentTarget.Location,
                                        ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
                                  Spell.Cast("Plague Strike"),
                                  Spell.Cast("Frost Strike", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                  Spell.Cast("Horn of Winter"),
                                  Movement.CreateMoveToMeleeBehavior(true)
                                  )),

                // Start single target section
                Spell.Buff("Howling Blast", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
                Spell.Buff("Outbreak", true, "Frost Fever"),
                Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
                Spell.Buff("Plague Strike", true, "Blood Plague"),
                Spell.Cast("Blood Strike", ret => !SpellManager.HasSpell("Obliterate")),
                Spell.Cast("Obliterate"),
                Spell.Cast("Frost Strike", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                Spell.Cast("Howling Blast", ret => StyxWoW.Me.HasAura("Freezing Fog") && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                Spell.Cast("Horn of Winter"),
                 */
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
                                ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),
                Spell.Buff("Chains of Ice", ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),

                    // Anti-magic shell - no cost and doesnt trigger GCD 
                    Spell.BuffSelf("Anti-Magic Shell",
                                    ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                                u.CurrentTargetGuid == StyxWoW.Me.Guid)),

                Spell.BuffSelf("Icebound Fortitude",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.IceboundFortitudePercent &&
                               SingularSettings.Instance.DeathKnight.UseIceboundFortitude),
                Spell.BuffSelf("Lichborne", ret => SingularSettings.Instance.DeathKnight.UseLichborne &&
                                                   (StyxWoW.Me.IsCrowdControlled() ||
                                                   StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.LichbornePercent)),
                                        /*
                Spell.BuffSelf("Death Coil",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent &&
                               StyxWoW.Me.HasAura("Lichborne")),
                Spell.Cast("Death Strike",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent),

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

                // Anti-magic shell - no cost and doesnt trigger GCD 
                Spell.BuffSelf("Anti-Magic Shell",
                                    ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                                u.CurrentTargetGuid == StyxWoW.Me.Guid)),
/*
                // Cooldowns
                Spell.BuffSelf("Pillar of Frost", ret => SingularSettings.Instance.DeathKnight.UsePillarOfFrost),
                Spell.BuffSelf("Raise Dead",
                           ret =>
                           SingularSettings.Instance.DeathKnight.UseRaiseDead && !StyxWoW.Me.GotAlivePet &&
                           StyxWoW.Me.CurrentTarget.IsBoss() && StyxWoW.Me.HasAura("Pillar of Frost")),
                Spell.BuffSelf("Empower Rune Weapon",
                           ret =>
                           SingularSettings.Instance.DeathKnight.UseEmpowerRuneWeapon && StyxWoW.Me.UnholyRuneCount == 0 &&
                           StyxWoW.Me.FrostRuneCount == 0 && StyxWoW.Me.DeathRuneCount == 0 &&
                           !SpellManager.CanCast("Frost Strike") && StyxWoW.Me.CurrentTarget.IsBoss()),

                Spell.BuffSelf("Icebound Fortitude",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.IceboundFortitudePercent &&
                               SingularSettings.Instance.DeathKnight.UseIceboundFortitude),
                Spell.Cast("Death Strike", ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent),

                Movement.CreateMoveBehindTargetBehavior(),
                // Start AoE section
                new Decorator(ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                              new PrioritySelector(
                                  Spell.Cast("Howling Blast",
                                             ret => StyxWoW.Me.FrostRuneCount == 2 || StyxWoW.Me.DeathRuneCount == 2),
                                  Spell.CastOnGround("Death and Decay",
                                        ret => StyxWoW.Me.CurrentTarget.Location,
                                        ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay && StyxWoW.Me.UnholyRuneCount == 2),
                                  Spell.Cast("Plague Strike", ret => StyxWoW.Me.UnholyRuneCount == 2),
                                  Spell.Cast("Frost Strike",
                                             ret => StyxWoW.Me.CurrentRunicPower == StyxWoW.Me.MaxRunicPower && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                  Spell.Cast("Horn of Winter"),
                                  Spell.CastOnGround("Death and Decay",
                                        ret => StyxWoW.Me.CurrentTarget.Location,
                                        ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
                                  Spell.Cast("Plague Strike"),
                                  Spell.Cast("Frost Strike", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                  Spell.Cast("Horn of Winter"),
                                  Movement.CreateMoveToMeleeBehavior(true)
                                  )),

                // Start single target section
                Spell.Buff("Howling Blast", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
                Spell.Buff("Outbreak", true, "Frost Fever"),
                Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
                Spell.Buff("Plague Strike", true, "Blood Plague"),
                Spell.Cast("Blood Strike", ret => !SpellManager.HasSpell("Obliterate")),
                Spell.Cast(
                    "Obliterate",
                    ret =>
                    (StyxWoW.Me.FrostRuneCount == 2 && StyxWoW.Me.UnholyRuneCount == 2) ||
                    StyxWoW.Me.DeathRuneCount == 2 || StyxWoW.Me.HasAura("Killing Machine")),
                Spell.Cast("Frost Strike", ret => StyxWoW.Me.CurrentRunicPower == StyxWoW.Me.MaxRunicPower && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                Spell.Cast("Howling Blast", ret => StyxWoW.Me.HasAura("Freezing Fog") && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                Spell.Cast("Obliterate"),
                Spell.Cast("Frost Strike", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                Spell.Cast("Horn of Winter"),
 */
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
    }
}
