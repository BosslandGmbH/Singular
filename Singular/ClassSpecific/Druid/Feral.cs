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

using System;
using System.Linq;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;

using TreeSharp;

using Action = TreeSharp.Action;
using Singular.Settings;
using Styx.Logic.Pathing;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Druid)]
        [Context(WoWContext.Instances)]
        [Behavior(BehaviorType.Combat)]
        [Spec(TalentSpec.FeralDruid)]
        public Composite CreateFeralCatInstanceCombat()
        {
            return CreateFeralCatCombat();
        }


        [Class(WoWClass.Druid)]
        [Context(WoWContext.Battlegrounds | WoWContext.Normal)]
        [Behavior(BehaviorType.Combat)]
        [Spec(TalentSpec.FeralDruid)]
        [Spec(TalentSpec.FeralTankDruid)]
        public Composite CreateFeralCatCombat()
        {
            // Get us in cat form pl0x
            WantedDruidForm = ShapeshiftForm.Cat;

            return new PrioritySelector(
                // Make sure we're in cat form first, period.
                new Decorator(
                    ret => Me.Shapeshift != WantedDruidForm,
                    CreateSpellCast("Cat Form")),
                CreateEnsureTarget(),
                CreateSpellBuffOnSelf("Rejuvenation", ret => !Me.IsInParty && !Me.IsInRaid && Me.HealthPercent < 60),
                CreateSpellCast("Berserk", ret => Me.Fleeing),
                CreateSpellCast("Survival Instincts", ret => Me.HealthPercent <= 45),
                CreateFaceUnit(),
                CreateAutoAttack(false),
                CreateSpellCast("Feral Charge (Cat)", ret => Me.CurrentTarget.Distance >= 8 && Me.CurrentTarget.Distance <= 25),
                CreateSpellCast("Skull Bash (Cat)", ret => Me.CurrentTarget.IsCasting),
                // Kudos to regecksqt for the dash/stampeding roar logic. Slightly changed for reading purposes.
                new Decorator(
                    ret =>
                    Me.CurrentTarget.Distance > 5 && Me.Combat &&
                    !Me.CurrentTarget.IsSafelyFacing(Me.Location) &&
                    Me.CurrentTarget.IsMoving && Me.CurrentTarget.MovementInfo.RunSpeed > Me.MovementInfo.RunSpeed,
                    new PrioritySelector(
                        CreateSpellCast("Dash"),
                        CreateSpellCast("Stampeding Roar (Cat)", ret => Me.CurrentEnergy >= 50))),
                new Decorator(
                    ret => Me.CurrentTarget.Distance <= 5,
                    new PrioritySelector(
                        //new Decorator(
                        //    ret => StyxWoW.Me.IsMoving,
                        //    // We use the player mover, since people can override it. This lets us support their stuff.
                        //    new Action(ret => Navigator.PlayerMover.MoveStop())),

                        CreateSpellCast("Barkskin", ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 5) > 0),
                        CreateSpellCast("Tiger's Fury", ret => Me.CurrentEnergy <= 50),
                        new Decorator(
                            ret => Me.ComboPoints == 5 || Me.ComboPoints > 2 && Me.CurrentTarget.HealthPercent < 40 && !CurrentTargetIsElite,
                            new PrioritySelector(
                                CreateSpellBuffOnSelf("Savage Roar", ret => Me.HealthPercent >= 75),
                                CreateSpellCast("Maim", ret => !Me.CurrentTarget.Stunned),
                                CreateSpellCast(
                                    "Rip", ret => !Me.CurrentTarget.HasAura("Rip") || Me.CurrentTarget.GetAuraByName("Rip").CreatorGuid != Me.Guid),
                                CreateSpellCast("Ferocious Bite"))),
                        // Handle Ravage! proc. Cast from spell ID here. Ignore the SpellManager!
                        new Decorator(
                            ret => /*IsBehind(Me.CurrentTarget) &&*/ Me.HasAura("Stampede"),
                            new Action(a => WoWSpell.FromId(81170).Cast())),
                        new Decorator(
                            ret => !Me.CurrentTarget.HasAura("Mangle") && SpellManager.CanCast("Mangle (Cat)"),
                            new Action(ret => SpellManager.Cast("Mangle (Cat)"))),
                        CreateSpellCast(
                            "Rake", ret => !Me.CurrentTarget.HasAura("Rake") || Me.CurrentTarget.GetAuraByName("Rake").CreatorGuid != Me.Guid),
                        CreateSpellCast("Shred", ret => Me.IsBehind(Me.CurrentTarget)),
                        // Don't swipe if we don't have more than 2 people/mobs on us, within range.
                        CreateSpellCast("Swipe (Cat)", ret => NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 5 * 5) >= 2),
                        //new ActionLog("Mangle"),
                        CreateSpellCast("Mangle (Cat)"))),
                CreateSpellBuff("Faerie Fire (Feral)", ret => !Me.HasAura("Prowl")),
                // We're putting movement at the bottom. Since we want the stuff above, to happen first. If we're out of range, we'll automatically fall
                // back to here and get within melee range to fuck shit up.
                CreateMoveToAndFace(4, ret => Me.CurrentTarget)
                );
        }

        [Class(WoWClass.Druid)]
        [Context(WoWContext.Battlegrounds|WoWContext.Normal)]
        [Behavior(BehaviorType.Pull)]
        [Spec(TalentSpec.FeralDruid)]
        [Spec(TalentSpec.FeralTankDruid)]
        public Composite CreateFeralCatPull()
        {
            return new PrioritySelector(
                // Make sure we're in cat form first, period.
                new Decorator(
                    ret => Me.Shapeshift != WantedDruidForm,
                    CreateSpellCast("Cat Form")),
                CreateEnsureTarget(),
                CreateSpellBuffOnSelf("Dash", ret => Me.IsMoving && Me.HasAura("Prowl")),
                CreateSpellBuffOnSelf("Prowl"),
                new PrioritySelector(
                    ret => WoWMathHelper.CalculatePointBehind(Me.CurrentTarget.Location, Me.CurrentTarget.Rotation, 1f),
                    new Decorator(
                        ret => ((WoWPoint)ret).Distance2D(Me.Location) > 3f && Navigator.CanNavigateFully(Me.Location, ((WoWPoint)ret)),
                        new Action(ret => Navigator.MoveTo(((WoWPoint)ret)))),
                    CreateMoveToAndFace()),
                new Decorator(
                    ret => Me.HasAura("Prowl"),
                    new PrioritySelector(
                        CreateSpellCast("Pounce"),
                        CreateSpellCast("Ravage", ret => Me.CurrentTarget.MeIsSafelyBehind))),
                CreateSpellCast("Mangle (Cat)"),
                CreateAutoAttack(true)
                );
        }

        [Class(WoWClass.Druid)]
        [Spec(TalentSpec.FeralDruid)]
        [Spec(TalentSpec.FeralTankDruid)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public Composite CreateFeralCatRest()
        {
            return new PrioritySelector(
                // Heal self before resting. There is no need to eat while we have 100% mana
                CreateRestoDruidHealOnlyBehavior(true),
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                CreateDefaultRestComposite(SingularSettings.Instance.DefaultRestHealth, SingularSettings.Instance.DefaultRestMana),
                // Can we res people?
                new Decorator(
                    ret => ResurrectablePlayers.Count != 0,
                    CreateSpellCast("Revive", ret => true, ret => ResurrectablePlayers.FirstOrDefault()))
                );
        }
    }
}