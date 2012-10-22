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

namespace Singular.ClassSpecific.Mage
{
    public class Fire
    {
        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Mage, WoWSpec.MageFire, WoWContext.Normal)]
        public static Composite CreateMageFireNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire),
                               Spell.Cast("Frostfire Bolt")),
                Spell.Cast("Fireball"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFire, WoWContext.Normal)]
        public static Composite CreateMageFireNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 20 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),

                // Cooldowns
                Spell.BuffSelf("Evocation",
                    ret => StyxWoW.Me.ManaPercent < 30 || (TalentManager.HasGlyph("Evocation") && StyxWoW.Me.HealthPercent < 50)),
                Spell.BuffSelf("Ice Barrier", ret => StyxWoW.Me.HealthPercent <= 99),

                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 3,
                    new PrioritySelector(
                        Spell.BuffSelf("Mirror Image")
                        )),
                Common.CreateUseManaGemBehavior(ret => StyxWoW.Me.ManaPercent < 80),

                // Rotation
                Spell.Cast("Dragon's Breath",
                    ret => StyxWoW.Me.IsSafelyFacing(StyxWoW.Me.CurrentTarget, 90) &&
                           StyxWoW.Me.CurrentTarget.DistanceSqr <= 8 * 8),

                Spell.Cast("Incanter's Ward", ret => StyxWoW.Me.CurrentTarget.DistanceSqr <= 6 * 6),

                new Decorator(
                    ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 10 * 10 && u.IsCrowdControlled()),
                    new PrioritySelector(
                        Spell.BuffSelf("Frost Nova",
                            ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                            u.DistanceSqr <= 8 * 8 && !u.HasAura("Freeze") &&
                                            !u.HasAura("Frost Nova") && !u.Stunned))
                        )),

                Common.CreateMagePolymorphOnAddBehavior(),
                // Rotation
                Spell.Cast("Deep Freeze",
                     ret => StyxWoW.Me.CurrentTarget.HasAura("Frost Nova")),

                Spell.Cast("Counterspell", ret => StyxWoW.Me.CurrentTarget.IsCasting && StyxWoW.Me.CurrentTarget.CanInterruptCurrentSpellCast),
                Spell.Cast("Mage Bomb", ret => !StyxWoW.Me.CurrentTarget.HasAura("Living Bomb") || (StyxWoW.Me.CurrentTarget.HasAura("Living Bomb") && StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Living Bomb", true).TotalSeconds <= 2)),
                Spell.Cast("Fire Blast", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Heating Up")),
                Spell.Cast("Combustion", ret => StyxWoW.Me.CurrentTarget.HasMyAura("Ignite") && StyxWoW.Me.CurrentTarget.HasMyAura("Pyroblast")),
                Spell.Cast("Pyroblast", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Pyroblast!")),
                Spell.Cast("Fireball", ret => !SpellManager.HasSpell("Scorch")),
                Spell.Cast("Scorch"),
                Movement.CreateMoveToTargetBehavior(true, 39f)
                );
        }

        #endregion

        #region Battleground Rotation
        [Behavior(BehaviorType.Pull|BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFire, WoWContext.Battlegrounds)]
        public static Composite CreateMageFirePvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 10 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),
                Spell.BuffSelf("Blink", ret => SingularSettings.Instance.IsCombatRoutineMovementAllowed() && (StyxWoW.Me.IsStunned() || StyxWoW.Me.IsRooted() || Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 2 * 2))), //Dist check for Melee beating me up.
                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.HealthPercent <= 75),
                Spell.BuffSelf("Ice Barrier", ret => StyxWoW.Me.HealthPercent <= 90),
                Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8 && !u.HasAura("Freeze") && !u.HasAura("Frost Nova") && !u.Stunned)),
                Common.CreateUseManaGemBehavior(ret => StyxWoW.Me.ManaPercent < 80),
                // Cooldowns
                Spell.BuffSelf("Evocation", ret => StyxWoW.Me.ManaPercent < 30),
                Spell.BuffSelf("Mirror Image"),
                Spell.BuffSelf("Mage Ward", ret => StyxWoW.Me.HealthPercent <= 75),
                Spell.Cast("Deep Freeze", ret => 
                            StyxWoW.Me.ActiveAuras.ContainsKey("Fingers of Frost") ||
                            StyxWoW.Me.CurrentTarget.HasAura("Freeze") ||
                            StyxWoW.Me.CurrentTarget.HasAura("Frost Nova")),
                Spell.Cast("Counter Spell", ret => (StyxWoW.Me.CurrentTarget.Class == WoWClass.Paladin ||StyxWoW.Me.CurrentTarget.Class == WoWClass.Priest || StyxWoW.Me.CurrentTarget.Class == WoWClass.Druid || StyxWoW.Me.CurrentTarget.Class == WoWClass.Shaman) && StyxWoW.Me.CurrentTarget.IsCasting && StyxWoW.Me.CurrentTarget.HealthPercent >= 20),
                Spell.Cast("Dragon's Breath",
                    ret => StyxWoW.Me.IsSafelyFacing(StyxWoW.Me.CurrentTarget, 90) &&
                           StyxWoW.Me.CurrentTarget.DistanceSqr <= 8 * 8),

                Spell.Cast("Fire Blast",
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Impact")),
                // Rotation
                
                Spell.Cast("Mage Bomb", ret => !StyxWoW.Me.CurrentTarget.HasAura("Living Bomb") || (StyxWoW.Me.CurrentTarget.HasAura("Living Bomb") && StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Living Bomb", true).TotalSeconds <= 2)),
                 Spell.Cast("Inferno Blast", ret => StyxWoW.Me.HasAura("Heating Up")),
                 Spell.Cast("Frost Bomb", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 1),
                Spell.Cast("Combustion", ret => StyxWoW.Me.CurrentTarget.HasMyAura("Ignite") && StyxWoW.Me.CurrentTarget.HasMyAura("Pyroblast")),

                Spell.Cast("Pyroblast", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Pyroblast!")),
                Spell.Cast("Fireball", ret => !SpellManager.HasSpell("Scorch")),
                Spell.Cast("Frostfire bolt", ret => !SpellManager.HasSpell("Fireball")),
                Spell.Cast("Scorch"),
                Movement.CreateMoveToTargetBehavior(true, 39f)
                );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFire, WoWContext.Instances)]
        public static Composite CreateMageFireInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 20 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),

                // Cooldowns
                Spell.BuffSelf("Evocation", ret => StyxWoW.Me.ManaPercent < 30),
                Spell.BuffSelf("Mirror Image"),
                Spell.BuffSelf("Mage Ward", ret => StyxWoW.Me.HealthPercent <= 75),

                Common.CreateUseManaGemBehavior(ret => StyxWoW.Me.ManaPercent < 80),
                // AoE comes first
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    new PrioritySelector(
                        Spell.Cast("Fire Blast",
                            ret => StyxWoW.Me.ActiveAuras.ContainsKey("Impact") &&
                                   (StyxWoW.Me.CurrentTarget.HasMyAura("Combustion") || TalentManager.IsSelected(13) )),
                        Spell.CastOnGround("Blast Wave",
                            ret => Clusters.GetBestUnitForCluster(Unit.NearbyUnitsInCombatWithMe, ClusterType.Radius, 8f).Location),
                        Spell.Cast("Dragon's Breath",
                            ret => Clusters.GetClusterCount(StyxWoW.Me,
                                                            Unit.NearbyUnitsInCombatWithMe,
                                                            ClusterType.Cone, 15f) >= 3),
                        Spell.CastOnGround("Flamestrike",
                            ret => Clusters.GetBestUnitForCluster(Unit.NearbyUnitsInCombatWithMe, ClusterType.Radius, 8f).Location,
                            ret => !ObjectManager.GetObjectsOfType<WoWDynamicObject>().Any(o =>
                                        o.CasterGuid == StyxWoW.Me.Guid && o.Spell.Name == "Flamestrike" &&
                                        o.Location.Distance(
                                            Clusters.GetBestUnitForCluster(Unit.NearbyUnitsInCombatWithMe, ClusterType.Radius, 8f).Location) < o.Radius))
                        )),

                Spell.BuffSelf("Time Warp",
                    ret => !StyxWoW.Me.GroupInfo.IsInRaid && StyxWoW.Me.CurrentTarget.HealthPercent > 20 && StyxWoW.Me.CurrentTarget.IsBoss() &&
                           !StyxWoW.Me.HasAura("Temporal Displacement")),

                // Rotation
                  Spell.Cast("Frostfire Bolt", ret => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire)),
                 Spell.Cast("Living Bomb", ret => !StyxWoW.Me.CurrentTarget.HasAura("Living Bomb") || (StyxWoW.Me.CurrentTarget.HasAura("Living Bomb") && StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Living Bomb", true).TotalSeconds <= 2)),
                 Spell.Cast("Inferno Blast", ret => StyxWoW.Me.HasAura("Heating Up")),
                 Spell.Cast("Frost Bomb", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 1),
                Spell.Cast("Combustion", ret => StyxWoW.Me.CurrentTarget.HasMyAura("Ignite") && StyxWoW.Me.CurrentTarget.HasMyAura("Pyroblast")),

                Spell.Cast("Pyroblast", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Pyroblast!")),
                Spell.Cast("Fireball", ret => !SpellManager.HasSpell("Scorch")),
                Spell.Cast("Frostfire bolt", ret => !SpellManager.HasSpell("Fireball")),
                Spell.Cast("Scorch"),
                Movement.CreateMoveToTargetBehavior(true, 39f)
                );
        }

        #endregion
    }
}
