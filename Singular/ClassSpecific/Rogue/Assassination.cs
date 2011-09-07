using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;

using TreeSharp;

using Action = System.Action;

namespace Singular.ClassSpecific.Rogue
{
    class Assassination
    {
        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.AssasinationRogue)]
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
                        new TreeSharp.Action(ret => Navigator.MoveTo(((WoWPoint)ret))))
                    ),
                // Garrote if we can, SS is kinda meh as an opener.
                Spell.Cast("Garrote"),
                Spell.Cast("Cheap Shot"),
                Spell.Cast("Sinister Strike"),
                Helpers.Common.CreateAutoAttack(true),
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.AssasinationRogue)]
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

                // Deal with stealth shit first.
                new Decorator(
                    ret => StyxWoW.Me.HasAnyAura("Stealth", "Vanish"),
                    Spell.Cast("Garrote")
                    ),


                // Redirect if we have CP left
                Spell.Cast("Redirect", ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.ComboPoints < 1),

                // CP generators, put em at start, since they're strictly conditional
                // and will help burning energy on Adrenaline Rush
                new Decorator(
                    ret => (Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6) <= 1 ||
                            Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 6 * 6 && u.HasAura("Blind"))),
                    Spell.Cast("Fank of Knives")),
                Common.CreateRogueBlindOnAddBehavior(),

                // Only pop Evisc if we don't have Enven.
                Spell.Cast(
                    "Eviscerate",
                    ret =>
                    !SpellManager.HasSpell("Envenom") && !StyxWoW.Me.CurrentTarget.Elite && StyxWoW.Me.CurrentTarget.HealthPercent <= 40 &&
                    StyxWoW.Me.ComboPoints > 2),

                // Always keep Slice and Dice up
                Spell.BuffSelf("Slice and Dice", ret => StyxWoW.Me.RawComboPoints > 0),

                // Mutliate is our usual DPS filler above 35%
                Spell.Cast(
                    "Mutilate",
                    ret => StyxWoW.Me.ComboPoints < 4 && (StyxWoW.Me.CurrentTarget.HealthPercent > 35 || !StyxWoW.Me.CurrentTarget.MeIsSafelyBehind)),
                // Backstab when < 35% health for the extra DPS boost
                Spell.Buff(
                    "Backstab",
                    ret => StyxWoW.Me.ComboPoints == 4 && (StyxWoW.Me.CurrentTarget.HealthPercent <= 35 && StyxWoW.Me.CurrentTarget.MeIsSafelyBehind)),

                //
                // Cooldowns:

                // Vanish -> Garrote+Overkill+Vendetta
                new Decorator(
                    ret => StyxWoW.Me.EnergyPercent < 20,
                    Spell.Cast("Vanish")),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35 || StyxWoW.Me.CurrentTarget.MaxHealth < 500000,
                    Spell.Cast("Vendetta")),


                // Drop aggro if we're in trouble.
                new Decorator(
                    ret => (StyxWoW.Me.IsInRaid || StyxWoW.Me.IsInParty) && StyxWoW.Me.CurrentTarget.ThreatInfo.RawPercent > 50,
                    Spell.Cast("Feint")),

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
                            ret => StyxWoW.Me.Auras["Slice and Dice"].TimeLeft.TotalSeconds > 5 || StyxWoW.Me.CurrentEnergy > 85,
                            new PrioritySelector(
                                // Check for >our own< Rupture debuff on target since there may be more rogues in party/raid!
                                Spell.Cast("Rupture", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Rupture")),
                                // Pop CB only before Envenom. Its useless on rupture!
                                Spell.Cast("Cold Blood"),
                                Spell.Cast("Envenom"))),
                        Spell.Cast("Slice and Dice", ret => StyxWoW.Me.Auras["Slice and Dice"].TimeLeft.TotalSeconds < 0.9))),

                // Outside of a party/raid, we don't care where we are. So long as we're in range.
                new Decorator(
                    ret => !StyxWoW.Me.IsInRaid && !StyxWoW.Me.IsInParty,
                    Movement.CreateMoveToTargetBehavior(true, 5f)),
                // Inside a party/raid, try and get behind.
                new Decorator(
                    ret => StyxWoW.Me.IsInParty || StyxWoW.Me.IsInRaid,
                    Movement.CreateMoveBehindTargetBehavior(5f))
                );
        }

        private static readonly WaitTimer _interruptTimer = new WaitTimer(TimeSpan.FromMilliseconds(500));

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
                    ret => StyxWoW.Me.CurrentTarget.IsCasting && StyxWoW.Me.CurrentTarget.CanInterruptCurrentSpellCast,
                    new PrioritySelector(
                        Spell.Cast("Kick", ret => PreventDoubleInterrupt),
                        Spell.Cast("Gouge", ret => !StyxWoW.Me.CurrentTarget.IsPlayerBehind && PreventDoubleInterrupt)
                        )),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsCasting && !StyxWoW.Me.CurrentTarget.CanInterruptCurrentSpellCast,
                    Spell.Cast("Cloak of Shadows")),
                // Recuperate to keep us at high health
                Spell.Buff("Recuperate", ret => StyxWoW.Me.HealthPercent < 50 && StyxWoW.Me.RawComboPoints > 3),
                Spell.Cast("Evasion", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6 && u.IsTargetingMeOrPet) > 1 || StyxWoW.Me.HealthPercent < 50),
                // Recuperate to not let us down
                //Spell.Cast("Recuperate", ret => StyxWoW.Me.HealthPercent < 20 && StyxWoW.Me.RawComboPoints > 1),
                // Cloak of Shadows as really last resort 
                Spell.Cast("Cloak of Shadows", ret => StyxWoW.Me.HealthPercent < 20),

                // Pop vanish if the shit really hits the fan...
                Spell.Cast("Vanish", ret => StyxWoW.Me.HealthPercent < 10)
                );
        }
    }
}
