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
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MageSettings MageSettings { get { return SingularSettings.Instance.Mage(); } }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFire, WoWContext.Normal)]
        public static Composite CreateMageFireNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Spell.Cast("Dragon's Breath",
                            ret => Me.IsSafelyFacing(Me.CurrentTarget, 90) &&
                                   Me.CurrentTarget.DistanceSqr <= 8 * 8),

                        new Decorator(
                            ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 10 * 10 && u.IsCrowdControlled()),
                            new PrioritySelector(
                                Spell.BuffSelf("Frost Nova",
                                    ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                    u.DistanceSqr <= 8 * 8 && !u.HasAura("Freeze") &&
                                                    !u.HasAura("Frost Nova") && !u.Stunned))
                                )),

                        // AoE comes first
                        new Decorator(
                            ret => Spell.UseAOE && Me.Level >= 25 && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(
                                Spell.CastOnGround("Flamestrike", loc => Me.CurrentTarget.Location),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 3),
                                Movement.CreateMoveToTargetBehavior(true, 10f)
                                )
                            ),

                        Common.CreateMagePolymorphOnAddBehavior(),

                        Spell.Cast("Deep Freeze",
                             ret => Me.CurrentTarget.HasAura("Frost Nova")),

                        Spell.Cast("Counterspell", ret => Me.CurrentTarget.IsCasting && Me.CurrentTarget.CanInterruptCurrentSpellCast),

                        // Rotation
                        Spell.Cast("Frostfire Bolt", ret => Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire)),

                        Spell.Cast("Combustion", ret => Me.CurrentTarget.HasMyAura("Ignite")),
                        Spell.Cast("Pyroblast", ret => Me.ActiveAuras.ContainsKey("Pyroblast!")),
                        Spell.Cast("Inferno Blast", ret => Me.ActiveAuras.ContainsKey("Heating Up")),
                        Spell.Cast("Fireball"),

                        Spell.Cast("Frostfire Bolt")
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 35f)
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
                    ret => Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => Me.HealthPercent < 10 && !Me.ActiveAuras.ContainsKey("Hypothermia")),
                Spell.BuffSelf("Blink", ret => MovementManager.IsClassMovementAllowed && (Me.IsStunned() || Me.IsRooted() || Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 2 * 2))), //Dist check for Melee beating me up.
                Spell.BuffSelf("Mana Shield", ret => Me.HealthPercent <= 75),
                Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8 && !u.HasAura("Freeze") && !u.HasAura("Frost Nova") && !u.Stunned)),
                Common.CreateUseManaGemBehavior(ret => Me.ManaPercent < 80),
                // Cooldowns
                Spell.BuffSelf("Evocation", ret => Me.ManaPercent < 30),
                Spell.BuffSelf("Mirror Image"),
                Spell.BuffSelf("Mage Ward", ret => Me.HealthPercent <= 75),
                Spell.Cast("Deep Freeze", ret => 
                            Me.ActiveAuras.ContainsKey("Fingers of Frost") ||
                            Me.CurrentTarget.HasAura("Freeze") ||
                            Me.CurrentTarget.HasAura("Frost Nova")),
                Spell.Cast("Counter Spell", ret => (Me.CurrentTarget.Class == WoWClass.Paladin ||Me.CurrentTarget.Class == WoWClass.Priest || Me.CurrentTarget.Class == WoWClass.Druid || Me.CurrentTarget.Class == WoWClass.Shaman) && Me.CurrentTarget.IsCasting && Me.CurrentTarget.HealthPercent >= 20),
                Spell.Cast("Dragon's Breath",
                    ret => Me.IsSafelyFacing(Me.CurrentTarget, 90) &&
                           Me.CurrentTarget.DistanceSqr <= 8 * 8),

                Spell.Cast("Fire Blast",
                    ret => Me.ActiveAuras.ContainsKey("Impact")),
                // Rotation
                
                Spell.Cast("Mage Bomb", ret => !Me.CurrentTarget.HasAura("Living Bomb") || (Me.CurrentTarget.HasAura("Living Bomb") && Me.CurrentTarget.GetAuraTimeLeft("Living Bomb", true).TotalSeconds <= 2)),
                 Spell.Cast("Inferno Blast", ret => Me.HasAura("Heating Up")),
                 Spell.Cast("Frost Bomb", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 1),
                Spell.Cast("Combustion", ret => Me.CurrentTarget.HasMyAura("Ignite") && Me.CurrentTarget.HasMyAura("Pyroblast")),

                Spell.Cast("Pyroblast", ret => Me.ActiveAuras.ContainsKey("Pyroblast!")),
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

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // AoE comes first
                        new Decorator(
                            ret => Spell.UseAOE && Me.Level >= 25 && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(
                                Spell.CastOnGround("Flamestrike", loc => Me.CurrentTarget.Location),
                                Spell.Cast("Fire Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 3),
                                Movement.CreateMoveToTargetBehavior(true, 10f)
                                )
                            ),

                        // Rotation
                        Spell.Cast("Frostfire Bolt", ret => Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire)),

                        Spell.Cast("Combustion", ret => Me.CurrentTarget.HasMyAura("Ignite") && Me.CurrentTarget.HasMyAura("Pyroblast")),
                        Spell.Cast("Pyroblast", ret => Me.ActiveAuras.ContainsKey("Pyroblast!")),
                        Spell.Cast("Inferno Blast", ret => Me.ActiveAuras.ContainsKey("Heating Up")),
                        Spell.Cast("Fireball"),

                        Spell.Cast("Frostfire Bolt")
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion
    }
}
