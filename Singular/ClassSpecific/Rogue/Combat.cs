using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using CommonBehaviors.Actions;

namespace Singular.ClassSpecific.Rogue
{
    public class Combat
    {
        #region Normal Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueCombat, WoWContext.Normal)]
        public static Composite CreateRogueCombatNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Sprint", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.HasAura("Stealth")),
                Spell.BuffSelf("Stealth"),
                // Garrote if we can, SS is kinda meh as an opener.
                Spell.Cast("Garrote", ret => StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Cheap Shot", ret => !SpellManager.HasSpell("Garrote") || !StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Ambush", ret => !SpellManager.HasSpell("Cheap Shot") && StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Sinister Strike", ret => !SpellManager.HasSpell("Cheap Shot") && !StyxWoW.Me.CurrentTarget.MeIsBehind),

                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying || StyxWoW.Me.CurrentTarget.Distance2DSqr < 5 * 5 && Math.Abs(StyxWoW.Me.Z - StyxWoW.Me.CurrentTarget.Z) >= 5,
                    new PrioritySelector(
                        Spell.Cast("Deadly Throw", ret => SpellManager.HasSpell("Deadly Throw")),
                        Spell.Cast("Throw"),
                        Spell.Cast("Stealth", ret => StyxWoW.Me.HasAura("Stealth"))
                        )),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueCombat, WoWContext.Normal)]
        public static Composite CreateRogueCombatNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => !StyxWoW.Me.HasAura("Vanish"),
                    Helpers.Common.CreateAutoAttack(true)),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Don't do anything if we casted vanish
                new Decorator(
                    ret => StyxWoW.Me.HasAura("Vanish"),
                    new ActionAlwaysSucceed()),

                // Defensive
                Spell.BuffSelf("Evasion",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6 && u.IsTargetingMeOrPet) >= 2),

                Spell.BuffSelf("Cloak of Shadows",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet && u.IsCasting) >= 1),

                Spell.BuffSelf("Smoke Bomb", ret => StyxWoW.Me.HealthPercent < 15),

                Common.CreateRogueBlindOnAddBehavior(),

                // Redirect if we have CP left
                Spell.Cast("Redirect", ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.ComboPoints < 1),

                Spell.BuffSelf("Vanish",
                    ret => StyxWoW.Me.HealthPercent < 20),

                Spell.BuffSelf("Adrenaline Rush",
                    ret => StyxWoW.Me.CurrentEnergy < 20 && !StyxWoW.Me.HasAura("Killing Spree")),

                // Killing Spree if we are at highest level of Bandit's Guise ( Shallow Insight / Moderate Insight / Deep Insight )
                Spell.Cast("Killing Spree",
                    ret => StyxWoW.Me.CurrentEnergy < 30 && StyxWoW.Me.HasAura("Deep Insight") && !StyxWoW.Me.HasAura("Adrenaline Rush")),

                Spell.BuffSelf("Blade Flurry",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8 * 8) > 1 &&
                           !Unit.NearbyUnfriendlyUnits.Any(u => u.HasMyAura("Blind"))),

                Spell.Cast("Revealing Strike", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Revealing Strike")),
                Spell.BuffSelf("Slice and Dice", ret => StyxWoW.Me.RawComboPoints > 0 && !StyxWoW.Me.HasAura("Slice and Dice")),
                Spell.Buff("Rupture", true,
                    ret => SingularSettings.Instance.Rogue.CombatUseRuptureFinisher && StyxWoW.Me.ComboPoints >= 4 &&
                           StyxWoW.Me.CurrentTarget.Elite && StyxWoW.Me.CurrentTarget.HasBleedDebuff()),
                
                Spell.Cast("Eviscerate", ret => StyxWoW.Me.ComboPoints == 5),
                Spell.Cast("Sinister Strike",ret => StyxWoW.Me.ComboPoints <= 4 ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueCombat, WoWContext.Battlegrounds)]
        public static Composite CreateRogueCombatPvPPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Sprint", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.HasAura("Stealth")),
                Spell.BuffSelf("Stealth"),
                // Garrote if we can, SS is kinda meh as an opener.
                Spell.Cast("Garrote",
                    ret => StyxWoW.Me.CurrentTarget.MeIsBehind && StyxWoW.Me.CurrentTarget.PowerType == WoWPowerType.Mana),
                Spell.Cast("Cheap Shot",
                    ret => !SpellManager.HasSpell("Garrote") || !StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Ambush", ret => !SpellManager.HasSpell("Cheap Shot") && StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Sinister Strike", ret => !SpellManager.HasSpell("Cheap Shot") && !StyxWoW.Me.CurrentTarget.MeIsBehind),

                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying || StyxWoW.Me.CurrentTarget.Distance2DSqr < 5 * 5 && Math.Abs(StyxWoW.Me.Z - StyxWoW.Me.CurrentTarget.Z) >= 5,
                    new PrioritySelector(
                        Spell.Cast("Deadly Throw", ret => SpellManager.HasSpell("Deadly Throw")),
                        Spell.Cast("Throw"),
                        Spell.Cast("Stealth", ret => StyxWoW.Me.HasAura("Stealth"))
                        )),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueCombat, WoWContext.Battlegrounds)]
        public static Composite CreateRogueCombatPvPCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => !StyxWoW.Me.HasAura("Vanish"),
                    Helpers.Common.CreateAutoAttack(true)),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive
                Spell.BuffSelf("Evasion",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6 && u.IsTargetingMeOrPet) >= 1),

                Spell.BuffSelf("Cloak of Shadows",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet && u.IsCasting) >= 1),

                Spell.BuffSelf("Smoke Bomb", ret => StyxWoW.Me.HealthPercent < 15),

                // Redirect if we have CP left
                Spell.Cast("Redirect", ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.ComboPoints < 1),

                Spell.BuffSelf("Vanish",
                    ret => StyxWoW.Me.HealthPercent < 20),

                Spell.BuffSelf("Adrenaline Rush",
                    ret => StyxWoW.Me.CurrentEnergy < 20 && !StyxWoW.Me.HasAura("Killing Spree")),

                // Killing Spree if we are at highest level of Bandit's Guise ( Shallow Insight / Moderate Insight / Deep Insight )
                Spell.Cast("Killing Spree",
                    ret => StyxWoW.Me.CurrentEnergy < 30 && StyxWoW.Me.HasAura("Deep Insight") && !StyxWoW.Me.HasAura("Adrenaline Rush")),

                Spell.BuffSelf("Blade Flurry",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8 * 8) > 1 &&
                           !Unit.NearbyUnfriendlyUnits.Any(u => u.HasMyAura("Blind"))),

                Spell.Cast("Revealing Strike", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Revealing Strike")),
                Spell.BuffSelf("Slice and Dice", ret => StyxWoW.Me.RawComboPoints > 0 && !StyxWoW.Me.HasAura("Slice and Dice")),
                Spell.Buff("Rupture", true,
                    ret => SingularSettings.Instance.Rogue.CombatUseRuptureFinisher && StyxWoW.Me.ComboPoints >= 4 &&
                           StyxWoW.Me.CurrentTarget.Elite && StyxWoW.Me.CurrentTarget.HasBleedDebuff()),

                Spell.Cast("Eviscerate", ret => StyxWoW.Me.ComboPoints == 5),
                Spell.Cast("Sinister Strike", ret => StyxWoW.Me.ComboPoints <= 4),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotation


        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueCombat, WoWContext.Instances)]
        public static Composite CreateRogueCombatInstancePull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Sprint", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.HasAura("Stealth")),
                Spell.BuffSelf("Stealth"),
                // Garrote if we can, SS is kinda meh as an opener.
                Spell.Cast("Garrote", ret => StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Cheap Shot", ret => !SpellManager.HasSpell("Garrote") || !StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Ambush", ret => !SpellManager.HasSpell("Cheap Shot") && StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Sinister Strike", ret => !SpellManager.HasSpell("Cheap Shot") && !StyxWoW.Me.CurrentTarget.MeIsBehind),

                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying || StyxWoW.Me.CurrentTarget.Distance2DSqr < 5 * 5 && Math.Abs(StyxWoW.Me.Z - StyxWoW.Me.CurrentTarget.Z) >= 5,
                    new PrioritySelector(
                        Spell.Cast("Deadly Throw", ret => SpellManager.HasSpell("Deadly Throw")),
                        Spell.Cast("Throw"),
                        Spell.Cast("Stealth", ret => StyxWoW.Me.HasAura("Stealth"))
                        )),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueCombat, WoWContext.Instances)]
        public static Composite CreateRogueCombatInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => !StyxWoW.Me.HasAura("Vanish"),
                    Helpers.Common.CreateAutoAttack(true)),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive
                Spell.BuffSelf("Evasion",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6 && u.IsTargetingMeOrPet) >= 1),

                Spell.BuffSelf("Cloak of Shadows",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet && u.IsCasting) >= 1),

                // Redirect if we have CP left
                Spell.Cast("Redirect", ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.ComboPoints < 1),

                // Agro management
                Spell.Cast("Tricks of the Trade", ret => Common.BestTricksTarget, ret => SingularSettings.Instance.Rogue.UseTricksOfTheTrade),

                Spell.Cast("Feint", ret => StyxWoW.Me.CurrentTarget.ThreatInfo.RawPercent > 80),

                Movement.CreateMoveBehindTargetBehavior(),
                
                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8 * 8) >= 7,
                    Spell.BuffSelf("Fan of Knives", ret => Item.RangedIsType(WoWItemWeaponClass.Thrown))),

                Spell.BuffSelf("Adrenaline Rush", 
                    ret => StyxWoW.Me.CurrentEnergy < 20 && !StyxWoW.Me.HasAura("Killing Spree")),


                Spell.Cast("Redirect", ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.ComboPoints < 1),

                // Killing Spree if we are at highest level of Bandit's Guise ( Shallow Insight / Moderate Insight / Deep Insight )
                Spell.Cast("Killing Spree",
                    ret => StyxWoW.Me.CurrentEnergy < 30 && StyxWoW.Me.HasAura("Deep Insight") && !StyxWoW.Me.HasAura("Adrenaline Rush")),

                Spell.BuffSelf("Blade Flurry",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8 * 8) > 1 &&
                           !Unit.NearbyUnfriendlyUnits.Any(u => u.HasMyAura("Blind"))),

                Spell.Cast("Revealing Strike", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Revealing Strike")),
                Spell.BuffSelf("Slice and Dice", ret => StyxWoW.Me.RawComboPoints > 0 && !StyxWoW.Me.HasAura("Slice and Dice")),
                Spell.Buff("Rupture", true,
                    ret => SingularSettings.Instance.Rogue.CombatUseRuptureFinisher && StyxWoW.Me.ComboPoints >= 4 &&
                           StyxWoW.Me.CurrentTarget.Elite && StyxWoW.Me.CurrentTarget.HasBleedDebuff()),

                Spell.Cast("Eviscerate", ret => StyxWoW.Me.ComboPoints == 5),
                Spell.Cast("Sinister Strike", ret => StyxWoW.Me.ComboPoints <= 4),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
    }
}
