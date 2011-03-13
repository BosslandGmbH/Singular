#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author$
// $Date$
// $HeadURL$
// $LastChangedBy$
// $LastChangedDate$
// $LastChangedRevision$
// $Revision$

#endregion

using System.Linq;

using Styx.Combat.CombatRoutine;

using TreeSharp;
using Styx.Helpers;
using Styx.Logic.Pathing;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.CombatRogue)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateCombatRoguePull()
        {
            return new PrioritySelector(
                CreateSpellBuffOnSelf("Stealth"),
                new Decorator(
                    ret => WoWMathHelper.CalculatePointBehind(Me.CurrentTarget.Location, Me.CurrentTarget.Rotation, 1f).Distance(Me.Location) > 3f,
                    new Action(ret => Navigator.MoveTo(WoWMathHelper.CalculatePointBehind(Me.CurrentTarget.Location, Me.CurrentTarget.Rotation, 1f)))),
                CreateFaceUnit(),
                CreateSpellCast("Ambush"),
                CreateAutoAttack(true)
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.CombatRogue)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateCombatRogueCombat()
        {
            return new PrioritySelector
                (
                CreateEnsureTarget(),
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateAutoAttack(false),
                CreateMoveToAndFace(5f, ret => Me.CurrentTarget),
                // Kick/Defensive cooldowns/Recuperation
                CreateCombatRogueDefense(),
                // Redirect if we have CP left
                CreateSpellCast(
                    "Redirect", ret => Me.RawComboPoints > 0 &&
                                       Me.ComboPoints < 1),
                /*
                // Blade Flurry
                CreateSpellCast("Blade Flurry", ret => NearbyUnfriendlyUnits.Count(a => a.Distance < 5) > 1 &&
                                                       !Me.HasAura("Blade Flurry")
                               ),
                new DecoratorContinue
                (
                    ret => NearbyUnfriendlyUnits.Count(a => a.Distance < 5) < 2,
		            new PrioritySelector
                    (
                        new TreeSharp.Action(ctx => Lua.DoString("RunMacroText(\"/cancelaura Blade Flurry\""))
                    )
                ),
                */

                // Always keep Slice and Dice up
                CreateSpellCast("Slice and Dice", ret => Me.RawComboPoints > 0 && !Me.HasAura("Slice and Dice")),
                // CP generators, put em at start, since they're strictly conditional
                // and will help burning energy on Adrenaline Rush

                CreateSpellCast("Blade Flurry", ret => NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6) > 1),
                CreateSpellCast("Eviscerate", ret => !CurrentTargetIsElite && Me.CurrentTarget.HealthPercent < 40 && Me.ComboPoints >= 3),
                // Sinister Strike till 4 CP
                CreateSpellCast("Sinister Strike", ret => Me.ComboPoints < 4),
                // Revealing Strike if we're at 4 CP and target does not have it already
                CreateSpellBuff("Revealing Strike", ret => Me.ComboPoints > 3 && Me.ComboPoints < 5),
                //
                // Cooldowns:

                CreateSpellBuffOnSelf("Adrenaline Rush", ret => Me.CurrentEnergy < 20),
                // Killing Spree if we are at highest level of Bandit's Guise ( Shallow Insight / Moderate Insight / Deep Insight )
                CreateSpellCast("Killing Spree", ret => Me.CurrentEnergy < 30 && Me.HasAura("Deep Insight")),
                // Finishers:
                new Decorator(
                    ret => Me.ComboPoints > 4,
                    new PrioritySelector(
                        // wait out low SnD duration to cast it at it's finish
                        // through not casting other finishers meanwhile and launching SnD below 1 sec duration
                        new Decorator(
                            ret => Me.Auras["Slice and Dice"].TimeLeft.TotalSeconds > 5 ||
                                   Me.CurrentEnergy > 85,
                            new PrioritySelector(
                                // Check for >our own< Rupture debuff on target since there may be more rogues in party/raid!
                                CreateSpellCast(
                                    "Rupture", ret => !Me.CurrentTarget.GetAllAuras().Any(a => a.Name == "Rupture" && a.CreatorGuid == Me.Guid)),
                                CreateSpellCast("Eviscerate"))),
                        CreateSpellCast("Slice and Dice", ret => Me.Auras["Slice and Dice"].TimeLeft.TotalSeconds < 0.9))));
        }

        public Composite CreateCombatRogueDefense()
        {
            return new PrioritySelector(
                CreateMoveToAndFace(),
                CreateSpellCast("Kick", ret => Me.CurrentTarget.IsCasting),
                CreateSpellCast("Cloak of Shadows", ret => Me.CurrentTarget.IsCasting),
                // Recuperate to keep us at high health
                CreateSpellCast("Recuperate", ret => Me.HealthPercent < 50 && Me.RawComboPoints > 3),
                CreateSpellCast("Evasion", ret => NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 *6) > 1),
                // Recuperate to not let us down
                CreateSpellCast("Recuperate", ret => Me.HealthPercent < 20 && Me.RawComboPoints > 1),
                // Cloak of Shadows as really last resort 
                CreateSpellCast("Cloak of Shadows", ret => Me.HealthPercent < 10)
                );
        }
    }
}