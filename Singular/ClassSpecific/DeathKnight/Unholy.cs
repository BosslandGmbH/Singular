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

               Spell.Buff("Chains of Ice", ret => StyxWoW.Me.CurrentTarget.Fleeing && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
               new Sequence(
                    Spell.Cast("Death Grip",
                                ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),

                // *** Cool downs ***
                // Anti-magic shell - no cost and doesnt trigger GCD 
                    Spell.BuffSelf("Anti-Magic Shell",
                                    ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                                u.CurrentTargetGuid == StyxWoW.Me.Guid)),

               Spell.BuffSelf("Raise Dead", ret => !StyxWoW.Me.GotAlivePet),

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

               Spell.Cast("Outbreak",
                    ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                            !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
               Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
               Spell.Buff("Plague Strike", true, "Blood Plague"),

                // Start AoE section
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                        new PrioritySelector(
                            Spell.Cast("Summon Gargoyle", ret => SingularSettings.Instance.DeathKnight.UseSummonGargoyle),
                            Spell.Cast("Pestilence",
                                        ret => StyxWoW.Me.CurrentTarget.HasMyAura("Blood Plague") &&
                                            StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") &&
                                            Unit.NearbyUnfriendlyUnits.Count(u =>
                                                    u.DistanceSqr < 10 * 10 && !u.HasMyAura("Blood Plague") &&
                                                    !u.HasMyAura("Frost Fever")) > 0),
                            Spell.Cast("Dark Transformation",
                                        ret => StyxWoW.Me.GotAlivePet &&
                                            !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
                            Spell.CastOnGround("Death and Decay",
                                ret => StyxWoW.Me.CurrentTarget.Location,
                                ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
                            Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount >= 2),
                            Spell.Cast("Blood Boil", ret => StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2),
                            Spell.Cast("Icy Touch", ret => StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2),
                            Spell.Cast("Death Coil", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Sudden Doom") || StyxWoW.Me.CurrentRunicPower >= 80),
                            Spell.Cast("Scourge Strike"),
                            Spell.Cast("Blood Boil"),
                            Spell.Cast("Icy Touch", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                            Spell.Cast("Death Coil"),
                            Spell.Cast("Horn of Winter"),
                            Movement.CreateMoveToMeleeBehavior(true)
                            )),

               Spell.CastOnGround("Death and Decay",
                                  ret => StyxWoW.Me.CurrentTarget.Location,
                                  ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay &&
                                         StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount >= 2),
               Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount >= 2),
               Spell.Cast("Festering Strike", ret => StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2),
               Spell.Cast("Death Coil", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Sudden Doom") || StyxWoW.Me.CurrentRunicPower >= 80),
               Spell.CastOnGround("Death and Decay",
                                  ret => StyxWoW.Me.CurrentTarget.Location,
                                  ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
               Spell.Cast("Scourge Strike"),
               Spell.Cast("Festering Strike"),
               Spell.Cast("Death Coil"),
               Spell.Cast("Horn of Winter"),
                                                    */
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
                                ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),
               Spell.Buff("Chains of Ice"),
               Spell.BuffSelf("Raise Dead", ret => !StyxWoW.Me.GotAlivePet),

               // *** Cool downs ***
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
                                                   StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.HealPercent)),

                // *** Self Heals ***
               Spell.BuffSelf("Death Coil",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.HealPercent &&
                               StyxWoW.Me.HasAura("Lichborne")),

                /*
               Spell.Cast("Death Pact",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.HealPercent && TalentManager.IsSelected(1)),

               Spell.Cast("Outbreak",
                    ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                            !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
               Spell.Buff("Icy Touch", true, "Frost Fever"),
               Spell.Buff("Plague Strike", true, "Blood Plague"),

               Spell.Cast("Summon Gargoyle", ret => SingularSettings.Instance.DeathKnight.UseSummonGargoyle),

               Spell.Buff("Lichborne", ret => StyxWoW.Me.HealthPercent < 50),
               Spell.Cast("Death Coil", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 50),
               Spell.Cast("Dark Transformation", ret => StyxWoW.Me.GotAlivePet && !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),

               Spell.Cast("Death Strike", ret => StyxWoW.Me.HealthPercent < 30),
               Spell.CastOnGround("Death and Decay",
                                  ret => StyxWoW.Me.CurrentTarget.Location,
                                  ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
               Spell.Cast("Festering Strike", ret => StyxWoW.Me.DeathRuneCount < 2),
               Spell.Buff("Necrotic Strike"),
               Spell.Cast("Scourge Strike"),
               Spell.Cast("Death Coil"),
               Spell.Cast("Horn of Winter"),
                 */
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
                // Anti-magic shell - no cost and doesnt trigger GCD 
                Spell.BuffSelf("Anti-Magic Shell",
                                ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                            (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                            u.CurrentTargetGuid == StyxWoW.Me.Guid)),

               Spell.BuffSelf("Raise Dead", ret => !StyxWoW.Me.GotAlivePet),

               /*
               Spell.Cast("Outbreak",
                    ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                            !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
               Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
               Movement.CreateMoveBehindTargetBehavior(),
               Spell.Buff("Plague Strike", true, "Blood Plague"),

                // Start AoE section
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                        new PrioritySelector(
                            Spell.Cast("Pestilence",
                                        ret => StyxWoW.Me.CurrentTarget.HasMyAura("Blood Plague") &&
                                            StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") &&
                                            Unit.UnfriendlyUnitsNearTarget(10f).Count(u =>
                                                    !u.HasMyAura("Blood Plague") &&
                                                    !u.HasMyAura("Frost Fever")) > 0),
                            Spell.Cast("Dark Transformation",
                                        ret => StyxWoW.Me.GotAlivePet &&
                                            !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
                            Spell.CastOnGround("Death and Decay",
                                ret => StyxWoW.Me.CurrentTarget.Location,
                                ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
                            Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount >= 2),
                            Spell.Cast("Blood Boil", ret => StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2),
                            Spell.Cast("Icy Touch", ret => StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2 && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                            Spell.Cast("Death Coil", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Sudden Doom") || StyxWoW.Me.CurrentRunicPower >= 80),
                            Spell.Cast("Scourge Strike"),
                            Spell.Cast("Blood Boil"),
                            Spell.Cast("Icy Touch",ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                            Spell.Cast("Death Coil"),
                            Spell.Cast("Horn of Winter"),
                            Movement.CreateMoveToMeleeBehavior(true)
                            )),

               Spell.Cast("Dark Transformation", ret => StyxWoW.Me.GotAlivePet && !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
               Spell.Cast("Summon Gargoyle", ret => SingularSettings.Instance.DeathKnight.UseSummonGargoyle),
               Spell.CastOnGround("Death and Decay",
                                  ret => StyxWoW.Me.CurrentTarget.Location,
                                  ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay &&
                                         StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount >= 2),
               Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount >= 2),
               Spell.Cast("Festering Strike", ret => StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2),
               Spell.Cast("Death Coil", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Sudden Doom") || StyxWoW.Me.CurrentRunicPower >= 80),
               Spell.CastOnGround("Death and Decay",
                                  ret => StyxWoW.Me.CurrentTarget.Location,
                                  ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
               Spell.Cast("Scourge Strike"),
               Spell.Cast("Festering Strike"),
               Spell.Cast("Death Coil"),
               Spell.Cast("Horn of Winter"),
                */
               Movement.CreateMoveToMeleeBehavior(true)
               );
        }

        #endregion
    }
}
