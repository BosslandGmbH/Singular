using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Rogue
{
    public class Combat
    {
        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.CombatRogue)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateCombatRoguePull()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Sprint", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.HasAura("Stealth")),
                Spell.BuffSelf("Stealth"),
                new PrioritySelector(
                    ret => WoWMathHelper.CalculatePointBehind(StyxWoW.Me.CurrentTarget.Location, StyxWoW.Me.CurrentTarget.Rotation, 1f),
                    new Decorator(
                        ret => ((WoWPoint)ret).Distance2D(StyxWoW.Me.Location) > 3f && Navigator.CanNavigateFully(StyxWoW.Me.Location, ((WoWPoint)ret)),
                        new Action(ret => Navigator.MoveTo(((WoWPoint)ret))))
                    ),
                Spell.Cast("Cheap Shot"),
                Spell.Cast("Sinister Strike"),
                Helpers.Common.CreateAutoAttack(true),
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.CombatRogue)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateCombatRogueCombat()
        {
            return new PrioritySelector
                (
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                // Kick/Defensive cooldowns/Recuperation
                CreateCombatRogueDefense(),
                // Redirect if we have CP left
                Spell.Cast(
                    "Redirect", ret => StyxWoW.Me.RawComboPoints > 0 &&
                                       StyxWoW.Me.ComboPoints < 1),

                // CP generators, put em at start, since they're strictly conditional
                // and will help burning energy on Adrenaline Rush
                new Decorator(
                    ret => (Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6) <= 1 ||
                            Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 6 * 6 && u.HasAura("Blind"))) && StyxWoW.Me.HasAura("Blade Flurry"),
                    new Sequence(
                        new Action(ret => Lua.DoString("RunMacroText(\"/cancelaura Blade Flurry\")")),
                        new Action(ret => StyxWoW.SleepForLagDuration()))),
                Common.CreateRogueBlindOnAddBehavior(),
                Spell.Cast("Blade Flurry", ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.HasAura("Blind")) && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6) > 1),
                Spell.Cast("Eviscerate", ret => !StyxWoW.Me.CurrentTarget.Elite && StyxWoW.Me.CurrentTarget.HealthPercent <= 40 && StyxWoW.Me.ComboPoints > 2),
                // Always keep Slice and Dice up
                Spell.BuffSelf("Slice and Dice", ret => StyxWoW.Me.RawComboPoints > 0),
                // Sinister Strike till 4 CP
                Spell.Cast("Sinister Strike", ret => StyxWoW.Me.ComboPoints < 4),
                // Revealing Strike if we're at 4 CP and target does not have it already
                Spell.Buff("Revealing Strike", ret => StyxWoW.Me.ComboPoints > 3 && StyxWoW.Me.ComboPoints < 5),
                //
                // Cooldowns:

                Spell.BuffSelf("Adrenaline Rush", ret => StyxWoW.Me.CurrentEnergy < 20),
                // Killing Spree if we are at highest level of Bandit's Guise ( Shallow Insight / Moderate Insight / Deep Insight )
                Spell.Cast("Killing Spree", ret => StyxWoW.Me.CurrentEnergy < 30 && StyxWoW.Me.HasAura("Deep Insight")),
                // Finishers:
                new Decorator(
                    ret => StyxWoW.Me.ComboPoints > 4,
                    new PrioritySelector(
                // wait out low SnD duration to cast it at it's finish
                // through not casting other finishers meanwhile and launching SnD below 1 sec duration
                        new Decorator(
                            ret => StyxWoW.Me.Auras["Slice and Dice"].TimeLeft.TotalSeconds > 5 ||
                                   StyxWoW.Me.CurrentEnergy > 85,
                            new PrioritySelector(
                // Check for >our own< Rupture debuff on target since there may be more rogues in party/raid!
                                Spell.Cast(
                                    "Rupture", ret => !StyxWoW.Me.CurrentTarget.GetAllAuras().Any(a => a.Name == "Rupture" && a.CreatorGuid == StyxWoW.Me.Guid)),
                                Spell.Cast("Eviscerate"))),
                        Spell.Cast("Slice and Dice", ret => StyxWoW.Me.Auras["Slice and Dice"].TimeLeft.TotalSeconds < 0.9))));
        }

        public static Composite CreateCombatRogueDefense()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsCasting,
                    new PrioritySelector(
                        Spell.Cast("Kick"),
                        Spell.Cast("Gouge"),
                        Spell.Cast("Cloak of Shadows"))),
                // Recuperate to keep us at high health
                //Spell.Cast("Recuperate", ret => StyxWoW.Me.HealthPercent < 50 && StyxWoW.Me.RawComboPoints > 3),
                Spell.Cast("Evasion", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6) > 1 || StyxWoW.Me.HealthPercent < 50),
                // Recuperate to not let us down
                //Spell.Cast("Recuperate", ret => StyxWoW.Me.HealthPercent < 20 && StyxWoW.Me.RawComboPoints > 1),
                // Cloak of Shadows as really last resort 
                Spell.Cast("Cloak of Shadows", ret => StyxWoW.Me.HealthPercent < 10)
                );
        }
    }
}
