using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
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
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Sprint", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.HasAura("Stealth")),
                Spell.BuffSelf("Stealth"),
                new PrioritySelector(
                    ret => WoWMathHelper.CalculatePointBehind(StyxWoW.Me.CurrentTarget.Location, StyxWoW.Me.CurrentTarget.Rotation, 1f),
                    new Decorator(
                        ret => ((WoWPoint)ret).Distance2D(StyxWoW.Me.Location) > 3f && Navigator.CanNavigateFully(StyxWoW.Me.Location, ((WoWPoint)ret)),
                        new Action(ret => Navigator.MoveTo(((WoWPoint)ret))))
                    ),
                Spell.Cast("Cheap Shot"),
                // Ambush if we can, SS is kinda meh as an opener.
                Spell.Cast("Ambush"),
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
                Spell.Cast("Redirect", ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.ComboPoints < 1),

                // CP generators, put em at start, since they're strictly conditional
                // and will help burning energy on Adrenaline Rush
                new Decorator(
                    ret => (Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) <= 1 ||
                            Unit.NearbyUnfriendlyUnits.Any(u => u.IsWithinMeleeRange && u.HasAura("Blind"))) && StyxWoW.Me.HasAura("Blade Flurry"),
                    new Sequence(
                        new Action(ret => Lua.DoString("RunMacroText(\"/cancelaura Blade Flurry\")")),
                        new Action(ret => StyxWoW.SleepForLagDuration()))),
                Common.CreateRogueBlindOnAddBehavior(),
                Spell.BuffSelf(
                    "Blade Flurry",
                    ret =>
                    !Unit.NearbyUnfriendlyUnits.Any(u => u.HasAura("Blind")) && Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) > 1),
                Spell.Cast(
                    "Eviscerate", ret => !StyxWoW.Me.CurrentTarget.Elite && StyxWoW.Me.CurrentTarget.HealthPercent <= 40 && StyxWoW.Me.ComboPoints > 2),
                // Always keep Slice and Dice up
                Spell.Cast(
                    "Slice and Dice", ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds < 3),
                // Sinister Strike till 4 CP
                // Sometimes we'll refresh SnD with 5pts (which is after popping RvS) so this will bug out at 4pts since the below wants to buff Rvs
                // but we really just want to hit 5pts. If we have 4pts, and already have Rvs on the target, just SS for 5pts
                Spell.Cast(
                    "Sinister Strike",
                    ret => StyxWoW.Me.ComboPoints < 4 || (StyxWoW.Me.CurrentTarget.HasMyAura("Revealing Strike") && StyxWoW.Me.ComboPoints == 4)),
                // Revealing Strike if we're at 4 CP and target does not have it already
                Spell.Buff("Revealing Strike", ret => StyxWoW.Me.ComboPoints == 4),
                //
                // Cooldowns:

                // Drop aggro if we're in trouble.
                new Decorator(
                    ret => (StyxWoW.Me.IsInRaid || StyxWoW.Me.IsInParty) && StyxWoW.Me.CurrentTarget.ThreatInfo.RawPercent > 50,
                    Spell.Cast("Feint")),

                // WE GOT TRIX! And no, they're not just for kids.
                Spell.Cast(
                    "Tricks of the Trade", ret => Common.BestTricksTarget,
                    ret => SingularSettings.Instance.Rogue.UseTricksOfTheTrade && Common.BestTricksTarget != null),

                Spell.BuffSelf("Adrenaline Rush", ret => StyxWoW.Me.CurrentEnergy < 20),
                // Killing Spree if we are at highest level of Bandit's Guise ( Shallow Insight / Moderate Insight / Deep Insight )
                Spell.Cast("Killing Spree", ret => StyxWoW.Me.CurrentEnergy < 30 && StyxWoW.Me.HasAura("Deep Insight")),
                // Finishers:
                new Decorator(
                    ret => StyxWoW.Me.ComboPoints > 4,
                    new PrioritySelector(
                        // This one is more for a group DPS boost, than anything. (Can be useful for ourselves as well, but its really experimental!)
                        Spell.Buff("Expose Armor", ret => StyxWoW.Me.CurrentTarget.IsBoss() && !StyxWoW.Me.CurrentTarget.HasSunders()),


                        // Check for >our own< Rupture debuff on target since there may be more rogues in party/raid!
                        // NOTE: Rupture is only a DPS increase if there's a bleed debuff on the target (Mangle, etc) Otherwise just stick to evisc...
                        // You shouldn't always listen to EJ! Rupture is a DPS increase assuming you can actually let it run its full duration! (Thus; really only useful on bosses, or trash with a bunch
                        // of health. Obviously, never use it with BF active)
                        Spell.Cast(
                            "Rupture",
                            ret => SingularSettings.Instance.Rogue.CombatUseRuptureFinisher && !StyxWoW.Me.CurrentTarget.HasMyAura("Rupture") &&
                                   !StyxWoW.Me.HasAura("Blade Flurry") && StyxWoW.Me.CurrentTarget.IsBoss() && !WillEnergyCap),
                        Spell.Cast("Eviscerate"))),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        private static readonly WaitTimer _interruptTimer = new WaitTimer(TimeSpan.FromMilliseconds(500));
        private static bool WillEnergyCap
        {
            get { return Lua.GetReturnVal<float>("return GetPowerRegen()", 1) > 25f; }
        }
        private static bool PreventDoubleInterrupt
        {
            get
            {
                var tmp = _interruptTimer.IsFinished;
                if (tmp)
                    _interruptTimer.Reset();
                return tmp;
            }
        }

        public static Composite CreateCombatRogueDefense()
        {
            return new PrioritySelector(
                new Decorator(
                    ret =>
                    SingularSettings.Instance.Rogue.InterruptSpells && StyxWoW.Me.CurrentTarget.IsCasting &&
                    StyxWoW.Me.CurrentTarget.CanInterruptCurrentSpellCast,
                    new PrioritySelector(
                        Spell.Cast("Kick"),
                        Spell.Cast("Gouge", ret => !StyxWoW.Me.IsBehind(StyxWoW.Me.CurrentTarget))
                        )),
                new Decorator(
                    ret =>
                    StyxWoW.Me.CurrentTarget.IsCasting && StyxWoW.Me.CurrentTarget.CanInterruptCurrentSpellCast &&
                    StyxWoW.Me.CurrentTarget.IsTargetingMeOrPet,
                    Spell.Cast("Cloak of Shadows")),
                // Recuperate to keep us at high health
                Spell.Buff("Recuperate", ret => StyxWoW.Me.HealthPercent < 70 && StyxWoW.Me.RawComboPoints > 3),
                Spell.Cast(
                    "Evasion",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6 && u.IsTargetingMeOrPet) > 1 || StyxWoW.Me.HealthPercent < 50),
                // Recuperate to not let us down
                //Spell.Cast("Recuperate", ret => StyxWoW.Me.HealthPercent < 20 && StyxWoW.Me.RawComboPoints > 1),
                // Cloak of Shadows as really last resort 
                Spell.Cast("Cloak of Shadows", ret => StyxWoW.Me.HealthPercent < 20),

                Spell.Cast("Smoke Bomb", ret => StyxWoW.Me.HealthPercent < 15),
                // Pop vanish if the shit really hits the fan...
                Spell.Cast("Vanish", ret => StyxWoW.Me.HealthPercent < 10)
                );
        }
    }
}
