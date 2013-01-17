using System;
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
    public class Arcane
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MageSettings MageSettings { get { return SingularSettings.Instance.Mage(); } }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Normal)]
        public static Composite CreateMageArcaneNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                Spell.WaitForCast(true),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Spell.BuffSelf( "Arcane Power"),

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
                                Spell.Cast("Fire Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 3),
                                Movement.CreateMoveToTargetBehavior(true, 10f)
                                )
                            ),

                        Common.CreateMagePolymorphOnAddBehavior(),

                        Spell.BuffSelf("Evocation", ret => Me.ManaPercent < 30),

                        Spell.Cast("Arcane Missiles", ret => Me.HasAura("Arcane Missiles!")),
                        Spell.Cast("Arcane Blast", ret => Me.HasAura( "Arcane Power")),

                        Spell.Cast("Scorch", ret => Me.ManaPercent < 90),
                        Spell.Cast("Arcane Barrage", ret => Me.GetAuraByName("Arcane Charge") != null && Me.GetAuraByName("Arcane Charge").StackCount >= 4),
                        Spell.Cast("Arcane Blast" ),
                        Spell.Cast("Frostfire Bolt")
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 39f)
                );
        }

        #endregion

        #region Battleground Rotation
        [Behavior(BehaviorType.Pull|BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Battlegrounds)]
        public static Composite CreateArcaneMagePvPPullAndCombat()
        {
            return new PrioritySelector(
                           Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
       
                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => Me.HealthPercent < 10 && !Me.ActiveAuras.ContainsKey("Hypothermia")),
                Spell.BuffSelf("Mana Shield", ret => Me.HealthPercent <= 75),
                Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 11 && !u.HasAura("Frost Nova"))),
                Common.CreateMagePolymorphOnAddBehavior(),





                Spell.BuffSelf("Mana Shield", ret => Me.ManaPercent < 30),
                Spell.BuffSelf("Evocation", ret => Me.ManaPercent < 30 && (Me.HasAura("Mana Shield") || !SpellManager.HasSpell("Mana Shield"))),
                Spell.BuffSelf("Arcane Power"),
                Spell.BuffSelf("Mirror Image"),
                Spell.BuffSelf("Flame Orb"),
                Spell.Cast("Arcane Missiles", ret => Me.HasAura("Arcane Missiles!")),
                Spell.Cast("Arcane Barrage", ret => Me.GetAuraByName("Arcane Charge") != null && Me.GetAuraByName("Arcane Charge").StackCount >= 4),
                Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast")),
                Spell.Cast("Arcane Blast"),
                Movement.CreateMoveToTargetBehavior(true, 39f)
                
                );
        }
        

        #endregion

        #region Instance Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Instances)]
        public static Composite CreateMageArcaneInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                Spell.WaitForCast(true),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Spell.BuffSelf( "Arcane Power"),

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

                        Spell.BuffSelf("Evocation", ret => Me.ManaPercent < 30),

                        Spell.Cast("Arcane Missiles", ret => Me.HasAura("Arcane Missiles!")),
                        Spell.Cast("Arcane Blast", ret => Me.HasAura( "Arcane Power")),

                        Spell.Cast("Scorch", ret => Me.ManaPercent < 90),
                        Spell.Cast("Arcane Barrage", ret => Me.GetAuraByName("Arcane Charge") != null && Me.GetAuraByName("Arcane Charge").StackCount >= 4),
                        Spell.Cast("Arcane Blast" ),
                        Spell.Cast("Frostfire Bolt")
                        )
                    ),

                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.ManaPercent < 30),
                Spell.BuffSelf("Evocation", ret => StyxWoW.Me.ManaPercent < 30 && (StyxWoW.Me.HasAura("Mana Shield") || !SpellManager.HasSpell("Mana Shield"))),

                Spell.Cast("Arcane Missiles", ret => StyxWoW.Me.HasAura("Arcane Missiles!")),
                Spell.Cast("Arcane Barrage", ret => StyxWoW.Me.GetAuraByName("Arcane Charge") != null && StyxWoW.Me.GetAuraByName("Arcane Charge").StackCount >= 4),
                Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast")),
                Spell.Cast("Arcane Blast"),
                Movement.CreateMoveToTargetBehavior(true, 39f)
                );
        }

        #endregion
    }
}
