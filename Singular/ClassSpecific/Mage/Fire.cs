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
using System.Drawing;

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
                Helpers.Common.CreateDismount("Pulling"),

/*
                new Throttle(8,
                    new Decorator(
                        ret => Me.CastingSpell != null && Me.CastingSpell.Name == "Fireball"
                            && Me.HasAura("Heating Up")
                            && SpellManager.HasSpell("Inferno Blast"),
                        new Action(r =>
                        {
                            Logger.Write("/cancel Fireball for Heating Up proc");
                            SpellManager.StopCasting();
                        })
                        )
                    ),
*/
                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateFireDiagnosticOutputBehavior(),

                        // move to highest in priority to ensure this is cast
                        Spell.Cast("Inferno Blast", ret => Me.ActiveAuras.ContainsKey("Heating Up")),

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

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
                                Spell.Cast("Inferno Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),
                                Spell.Cast("Dragon's Breath", ret => Me.CurrentTarget.DistanceSqr <= 12 * 12),
                                Spell.CastOnGround("Flamestrike", loc => Me.CurrentTarget.Location),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 3),
                                new Decorator(
                                    ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 3,
                                    Movement.CreateMoveToTargetBehavior(true, 10f)
                                    )
                                )
                            ),

                        Spell.Cast("Dragon's Breath",
                            ret => Me.IsSafelyFacing(Me.CurrentTarget, 90) &&
                                   Me.CurrentTarget.DistanceSqr <= 12 * 12),

                        Common.CreateMagePolymorphOnAddBehavior(),

                        Spell.Cast("Deep Freeze",
                             ret => Me.CurrentTarget.HasAura("Frost Nova")),

                        Spell.Cast("Counterspell", ret => Me.CurrentTarget.IsCasting && Me.CurrentTarget.CanInterruptCurrentSpellCast),

                        // Single Target
                        // living bomb in Common
                        new Decorator(
                            ret =>  !Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire),
                            new PrioritySelector(
                                Spell.Cast("Combustion", ret => Me.CurrentTarget.HasMyAura("Ignite")),
                                Spell.Cast("Pyroblast", ret => Me.ActiveAuras.ContainsKey("Pyroblast!")),
                                Spell.Cast("Inferno Blast", ret => Me.ActiveAuras.ContainsKey("Heating Up")),
                                Spell.Cast("Fireball")
                                )
                            ),

                        // 
                        Spell.Cast("Ice Lance", ret => (Me.IsMoving || Me.CurrentTarget.HasAura("Frost Nova")) && Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire)),
                        Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Fireball") || Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire))
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
                Helpers.Common.CreateDismount("Pulling"),
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
                Helpers.Common.CreateDismount("Pulling"),
/*
                new Throttle( 8,
                    new Decorator(
                        ret => Me.CastingSpell != null && Me.CastingSpell.Name == "Fireball" 
                            && Me.HasAura("Heating Up") 
                            && SpellManager.HasSpell("Inferno Blast"),
                        new Action(r => {
                            Logger.Write("/cancel Fireball for Heating Up proc");
                            SpellManager.StopCasting();
                            })
                        )
                    ),
*/
                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateFireDiagnosticOutputBehavior(),

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                        // AoE comes first
                        new Decorator(
                            ret => Spell.UseAOE && Me.Level >= 25 && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(
                                Spell.Cast("Inferno Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),
                                Spell.Cast("Dragon's Breath", ret => Me.CurrentTarget.DistanceSqr <= 12 * 12),
                                Spell.CastOnGround("Flamestrike", loc => Me.CurrentTarget.Location),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 3),
                                new Decorator( 
                                    ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 3,
                                    Movement.CreateMoveToTargetBehavior(true, 10f)
                                    )
                                )
                            ),

                        // Single Target
                        // living bomb in Common
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

        #region Diagnostics

        private static Composite CreateFireDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new Throttle(1,
                new Action(ret =>
                {
                    string line = string.Format(".... h={0:F1}%/m={1:F1}%, moving={2}, heatup={3} {4:F0} ms, pyroblst={5} {6:F0} ms",
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving,
                        Me.GetAuraStacks("Heating Up"),
                        Me.GetAuraTimeLeft("Heating Up").TotalMilliseconds,
                        Me.GetAuraStacks("Pyroblast!"),
                        Me.GetAuraTimeLeft("Pyroblast!").TotalMilliseconds
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target == null)
                        line += ", target=(null)";
                    else
                        line += string.Format(", target={0} @ {1:F1} yds, h={2:F1}%, face={3}, loss={4}, livbomb={5:F0} ms",
                            target.SafeName(),
                            target.Distance,
                            target.HealthPercent,
                            Me.IsSafelyFacing(target),
                            target.InLineOfSpellSight,
                            target.GetAuraTimeLeft("Living Bomb").TotalMilliseconds
                            );

                    Logger.WriteDebug(Color.Wheat, line);
                    return RunStatus.Success;
                })
                );
        }

        #endregion
    }
}
