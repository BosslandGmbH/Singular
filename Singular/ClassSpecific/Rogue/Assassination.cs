using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CommonBehaviors.Actions;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;

using Styx.TreeSharp;

using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Rogue
{
    class Assassination
    {
        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Normal)]
        public static Composite CreateAssaRogueNormalPull()
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
                Spell.Cast("Mutilate", ret => !SpellManager.HasSpell("Cheap Shot") && !StyxWoW.Me.CurrentTarget.MeIsBehind),

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
        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Normal)]
        public static Composite CreateAssaRogueNormalCombat()
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

                Spell.BuffSelf("Vanish",ret => StyxWoW.Me.HealthPercent < 20),

                Spell.BuffSelf("Vendetta", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2),
                Spell.BuffSelf("Slice and Dice", ret => StyxWoW.Me.RawComboPoints > 0 && !StyxWoW.Me.HasAura("Slice and Dice")),
                Spell.Buff("Rupture", true, ret => (StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rupture",true).TotalSeconds < 3)),
                Spell.Buff("Envenom", true, ret => (StyxWoW.Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds < 3 && StyxWoW.Me.ComboPoints > 0) || StyxWoW.Me.ComboPoints == 5),
                Spell.Cast("Dispatch"),
                Spell.Cast("Mutilate"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Battlegrounds)]
        public static Composite CreateAssaRoguePvPPull()
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
                Spell.Cast("Mutilate", ret => !SpellManager.HasSpell("Cheap Shot") && !StyxWoW.Me.CurrentTarget.MeIsBehind),

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
        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Battlegrounds)]
        public static Composite CreateAssaRoguePvPCombat()
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
                    ret => TalentManager.IsSelected(14) && StyxWoW.Me.CurrentTarget.HasMyAura("Rupture") &&
                           StyxWoW.Me.HasAura("Slice and Dice")),
                Spell.Cast("Garrote",
                    ret => (StyxWoW.Me.HasAura("Vanish") || StyxWoW.Me.IsStealthed) &&
                           StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.BuffSelf("Vendetta"),
                Spell.BuffSelf("Slice and Dice", ret => StyxWoW.Me.RawComboPoints > 0 && !StyxWoW.Me.HasAura("Slice and Dice")),
                Spell.Buff("Rupture", true, ret => (StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rupture", true).TotalSeconds < 3)),
                Spell.Buff("Envenom", true, ret => (StyxWoW.Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds < 3 && StyxWoW.Me.ComboPoints > 0) || StyxWoW.Me.ComboPoints == 5),
                Spell.Cast("Dispatch"),
                Spell.Cast("Mutilate"),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Instances)]
        public static Composite CreateAssaRogueInstancePull()
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
                Spell.Cast("Mutilate", ret => !SpellManager.HasSpell("Cheap Shot") && !StyxWoW.Me.CurrentTarget.MeIsBehind),

                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying || StyxWoW.Me.CurrentTarget.Distance2DSqr < 5*5 && Math.Abs(StyxWoW.Me.Z - StyxWoW.Me.CurrentTarget.Z) >= 5,
                    new PrioritySelector(
                        Spell.Cast("Deadly Throw", ret => SpellManager.HasSpell("Deadly Throw")),
                        Spell.Cast("Throw"),
                        Spell.Cast("Stealth", ret => StyxWoW.Me.HasAura("Stealth"))
                        )),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueAssassination, WoWContext.Instances)]
        public static Composite CreateAssaRogueInstanceCombat()
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
                Spell.Cast(
                    "Tricks of the Trade", 
                    ret => Common.BestTricksTarget,
                    ret => SingularSettings.Instance.Rogue.UseTricksOfTheTrade),

                Spell.Cast("Feint", ret => StyxWoW.Me.CurrentTarget.ThreatInfo.RawPercent > 80),

                Movement.CreateMoveBehindTargetBehavior(),

                Spell.BuffSelf("Vanish",
                    ret => TalentManager.IsSelected(14) && StyxWoW.Me.CurrentTarget.HasMyAura("Rupture") && 
                           StyxWoW.Me.HasAura("Slice and Dice")),
                Spell.Cast("Garrote", 
                    ret => (StyxWoW.Me.HasAura("Vanish") || StyxWoW.Me.IsStealthed) &&
                           StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.BuffSelf("Vendetta", 
                    ret => StyxWoW.Me.CurrentTarget.IsBoss() && 
                           (StyxWoW.Me.CurrentTarget.HealthPercent < 35 || TalentManager.IsSelected(13))),

                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8*8) >= 3,
                    Spell.BuffSelf("Fan of Knives", ret => Item.RangedIsType(WoWItemWeaponClass.Thrown))),

                Spell.BuffSelf("Slice and Dice", ret => StyxWoW.Me.RawComboPoints > 0 && !StyxWoW.Me.HasAura("Slice and Dice")),
                Spell.Buff("Rupture", true, ret => (StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rupture", true).TotalSeconds < 3)),
                Spell.Buff("Envenom", true, ret => (StyxWoW.Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds < 3 && StyxWoW.Me.ComboPoints > 0) || StyxWoW.Me.ComboPoints == 5),
                Spell.Cast("Dispatch"),
                Spell.Cast("Mutilate"),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
    }
}
