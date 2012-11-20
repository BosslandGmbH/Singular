using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Mage
{
    public class Frost
    {
        #region Normal Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Normal)]
        public static Composite CreateMageFrostNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),
                new Decorator(ctx => SingularSettings.Instance.DisablePetUsage && StyxWoW.Me.GotAlivePet,
                    new Action(ctx => Lua.DoString("PetDismiss()"))),
                // We want our pet alive !
                new Decorator(
                    ret => !SingularSettings.Instance.DisablePetUsage && !StyxWoW.Me.GotAlivePet && PetManager.PetTimer.IsFinished && SpellManager.CanCast("Summon Water Elemental"),
                    new Sequence(
                        new Action(ret => PetManager.CallPet("Summon Water Elemental")),
                        Helpers.Common.CreateWaitForLagDuration())),
                Spell.Cast("Frostbolt", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                Spell.Cast("Frostfire Bolt"),
                Movement.CreateMoveToTargetBehavior(true, 39f)
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Normal)]
        public static Composite CreateMageFrostNormalCombat()
        {
            return new PrioritySelector(
                 Safers.EnsureTarget(),
                 Common.CreateStayAwayFromFrozenTargetsBehavior(),
                 Movement.CreateMoveToLosBehavior(),
                 Movement.CreateFaceTargetBehavior(),
                 Helpers.Common.CreateAutoAttack(true),
                 Spell.WaitForCast(true),

                 // We want our pet alive !
                 new Decorator(
                     ret => !StyxWoW.Me.GotAlivePet && PetManager.PetTimer.IsFinished && SpellManager.CanCast("Summon Water Elemental"),
                     new Sequence(
                         new Action(ret => PetManager.CallPet("Summon Water Elemental")),
                         Helpers.Common.CreateWaitForLagDuration())),

                 // Defensive stuff
                 new Decorator(
                     ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                     new ActionIdle()),
                 Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 20 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),

                 // Cooldowns
                 Spell.BuffSelf("Evocation",
                     ret => StyxWoW.Me.ManaPercent < 30 || (TalentManager.HasGlyph("Evocation") && StyxWoW.Me.HealthPercent < 50)),
                 Spell.BuffSelf("Mage Ward", ret => StyxWoW.Me.HealthPercent <= 80),
                 Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.HealthPercent <= 60),

                 new Decorator(
                     ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 3,
                     new PrioritySelector(
                         Spell.BuffSelf("Mirror Image"),
                         Spell.BuffSelf("Icy Veins")
                         )),
                 Common.CreateUseManaGemBehavior(ret => StyxWoW.Me.ManaPercent < 80),

                 new Decorator(
                     ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 15 && !u.IsCrowdControlled()),
                     new PrioritySelector(
                         Pet.CreateCastPetActionOnLocation("Freeze", ret => !StyxWoW.Me.Mounted && !StyxWoW.Me.CurrentTarget.HasAura("Frost Nova") && StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.ManaPercent >= 12),
                         Spell.BuffSelf("Frost Nova",
                             ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 11 && !u.HasAura("Frost Nova") && !u.HasAura("Freeze")
                         )))),

                 Common.CreateMagePolymorphOnAddBehavior(),
                // Rotation
                 Spell.Cast("Frost Bomb", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3),
                 Spell.Cast("Deep Freeze",
                     ret => StyxWoW.Me.ActiveAuras.ContainsKey("Fingers of Frost") ||
                            StyxWoW.Me.CurrentTarget.HasAura("Freeze") ||
                            StyxWoW.Me.CurrentTarget.HasAura("Frost Nova")),

                 new Decorator(
                     ret => StyxWoW.Me.ActiveAuras.ContainsKey("Brain Freeze"),
                     new PrioritySelector(
                         Spell.Cast("Frostfire Bolt")
                         )),
                 Spell.Cast("Ice Lance",
                     ret => StyxWoW.Me.ActiveAuras.ContainsKey("Fingers of Frost") ||
                            StyxWoW.Me.CurrentTarget.HasAura("Freeze") ||
                            StyxWoW.Me.CurrentTarget.HasAura("Frost Nova") ||
                            StyxWoW.Me.IsMoving),
                 Spell.Cast("Frostbolt", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                 Spell.Cast("Frostfire Bolt"),
                 Movement.CreateMoveToTargetBehavior(true, 39f)
                 );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull|BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Battlegrounds)]
        public static Composite CreateMageFrostPvPPullAndCombat()
        {
            return new PrioritySelector(
                 Safers.EnsureTarget(),
                 Common.CreateStayAwayFromFrozenTargetsBehavior(),
                 Movement.CreateMoveToLosBehavior(),
                 Movement.CreateFaceTargetBehavior(),
                 Helpers.Common.CreateAutoAttack(true),
                 Spell.WaitForCast(true),

                 // We want our pet alive !
                 new Decorator(
                     ret => !StyxWoW.Me.GotAlivePet && PetManager.PetTimer.IsFinished && SpellManager.CanCast("Summon Water Elemental"),
                     new Sequence(
                         new Action(ret => PetManager.CallPet("Summon Water Elemental")),
                         Helpers.Common.CreateWaitForLagDuration())),

                 // Defensive stuff
                 new Decorator(
                     ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                     new ActionIdle()),
                 Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 10 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),
                 Spell.BuffSelf("Blink", ret => SingularSettings.Instance.IsCombatRoutineMovementAllowed() && (StyxWoW.Me.IsStunned() || StyxWoW.Me.IsRooted())),
                 Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.HealthPercent <= 75),
                 Pet.CreateCastPetActionOnLocation("Freeze", ret => !StyxWoW.Me.Mounted && !StyxWoW.Me.CurrentTarget.HasAura("Frost Nova") && StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.ManaPercent >= 12),
                 Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8 && !u.HasAura("Freeze") && !u.HasAura("Frost Nova") && !u.Stunned)),

                 Common.CreateUseManaGemBehavior(ret => StyxWoW.Me.ManaPercent < 80),
                // Cooldowns
                 Spell.BuffSelf("Evocation", ret => StyxWoW.Me.ManaPercent < 30),
                 Spell.BuffSelf("Mirror Image"),
                 Spell.BuffSelf("Mage Ward", ret => StyxWoW.Me.HealthPercent <= 75),
                 Spell.BuffSelf("Icy Veins"),

                 // Rotation
                 Spell.Cast("Frost Bomb", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3),
                 Spell.Cast("Deep Freeze",
                     ret => StyxWoW.Me.ActiveAuras.ContainsKey("Fingers of Frost") ||
                            StyxWoW.Me.CurrentTarget.HasAura("Freeze") ||
                            StyxWoW.Me.CurrentTarget.HasAura("Frost Nova")),

                 new Decorator(
                     ret => StyxWoW.Me.ActiveAuras.ContainsKey("Brain Freeze"),
                     new PrioritySelector(
                         Spell.Cast("Frostfire Bolt"),
                         Spell.Cast("Fireball")
                         )),
                 Spell.Cast("Ice Lance",
                     ret => StyxWoW.Me.ActiveAuras.ContainsKey("Fingers of Frost") ||
                            StyxWoW.Me.CurrentTarget.HasAura("Freeze") ||
                            StyxWoW.Me.CurrentTarget.HasAura("Frost Nova") ||
                            StyxWoW.Me.IsMoving),
                 Spell.Cast("Frostbolt"),

                 Movement.CreateMoveToTargetBehavior(true, 39f)
                 );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Instances)]
        public static Composite CreateMageFrostInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),

                new Decorator(
                    ret => !StyxWoW.Me.GotAlivePet && PetManager.PetTimer.IsFinished && SpellManager.CanCast("Summon Water Elemental"),
                    new Sequence(
                        new Action(ret => PetManager.CallPet("Summon Water Elemental")),
                        Helpers.Common.CreateWaitForLagDuration())),

                new Decorator(
                    ret => StyxWoW.Me.GotAlivePet && PetManager.PetTimer.IsFinished && SpellManager.CanCast("Summon Water Elemental") && StyxWoW.Me.Pet.Distance > 40,
                    new Sequence(
                        new Action(ret => PetManager.CallPet("Summon Water Elemental")),
                        Helpers.Common.CreateWaitForLagDuration())),

                new Decorator(ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 20 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),

                Common.CreateUseManaGemBehavior(ret => StyxWoW.Me.ManaPercent < 20),

                new Decorator(ret => Unit.UnfriendlyUnitsNearTarget(10).Count() >= 4,
                    new PrioritySelector(
                        Spell.Cast("Frost Bomb"),
                        Spell.Cast("Flamestrike"),
                        Spell.Cast("Frozen Orb"),
                        Spell.Cast("Ice Lance", ret => StyxWoW.Me.HasAura("Fingers of Frost")),
                        Spell.Cast("Arcane Explosion"),
                        Movement.CreateMoveToTargetBehavior(true, 10f))),

                Spell.Cast("Frost Bomb"),
                Spell.Cast("Forzen Orb"),

                Spell.Cast("Alter Time", ret => StyxWoW.Me.HasAura("Brain Freeze") && StyxWoW.Me.HasAura("Fingers of Frost") && StyxWoW.Me.HasAura("Invocation")),
                Spell.Cast("Deep Freeze", ret => StyxWoW.Me.HasAura("Fingers of Frost") ||
                    StyxWoW.Me.CurrentTarget.HasAura("Freeze") || StyxWoW.Me.CurrentTarget.HasAura("Frost Nova")),

                Spell.Cast("Frostbolt", ret => (StyxWoW.Me.CurrentTarget.HasAura("Frostbolt") && StyxWoW.Me.CurrentTarget.Auras["Frostbolt"].StackCount < 3) || StyxWoW.Me.HasAura("Presence of Mind")),
                Pet.CreateCastPetActionOnLocation("Freeze", ret => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget(8), ClusterType.Radius, 8).Location),

                Spell.Cast("Evocation"),
                Spell.Cast("Icy Veins"),
                Spell.Cast("Mirror Image"),

                Spell.Cast("Frostfire Bolt", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) || StyxWoW.Me.HasAura("Brain Freeze")),
                Spell.Cast("Ice Lance", ret => StyxWoW.Me.HasAura("Fingers of Frost")),
                Spell.Cast("Frostbolt"),
                Movement.CreateMoveToTargetBehavior(true, 30f)
                );
        }

        #endregion
    }
}
