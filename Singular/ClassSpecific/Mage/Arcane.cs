using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Pathing;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Mage
{
    public class Arcane
    {
        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.ArcaneMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateArcaneMageCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Helpers.Common.CreateAutoAttack(true),
                //Move away from frozen targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.HasAura("Frost Nova") && StyxWoW.Me.CurrentTarget.DistanceSqr < 5 * 5,
                    new Action(
                        ret =>
                            {
                                Logger.Write("Getting away from frozen target");
                                WoWPoint moveTo = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, StyxWoW.Me.CurrentTarget.Location, 10f);

                                if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                                {
                                    Navigator.MoveTo(moveTo);
                                }
                            })),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 10 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8)),
                Spell.WaitForCast(),
                Spell.Cast("Evocation", ret => StyxWoW.Me.ManaPercent < 20),
                new Decorator(
                    ret => Common.HaveManaGem() && StyxWoW.Me.ManaPercent <= 30,
                    new Action(ctx => Common.UseManaGem())),
                Common.CreateMagePolymorphOnAddBehavior(),
                Spell.Cast("Counterspell", ret => StyxWoW.Me.CurrentTarget.IsCasting),
                Spell.Cast("Mirror Image", ret => StyxWoW.Me.CurrentTarget.HealthPercent > 20),
                Spell.Cast("Time Warp", ret => StyxWoW.Me.CurrentTarget.HealthPercent > 20),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent > 50,
                    new Sequence(
                        new Action(ctx => StyxWoW.Me.CurrentTarget.Face()),
                        new Action(ctx => StyxWoW.SleepForLagDuration()),
                        Spell.Cast("Flame Orb")
                        )),
                Spell.BuffSelf("Mana Shield", ret => !StyxWoW.Me.Auras.ContainsKey("Mana Shield") && StyxWoW.Me.HealthPercent <= 75),
                Spell.Cast(
                    "Slow",
                    ret =>
                    TalentManager.GetCount(1, 18) < 2 && !StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Slow") &&
                    StyxWoW.Me.CurrentTarget.Distance > 5),
                Spell.Cast(
                    "Arcane Missiles",
                    ret =>
                    StyxWoW.Me.ActiveAuras.ContainsKey("Arcane Missiles!") && StyxWoW.Me.ActiveAuras.ContainsKey("Arcane Blast") &&
                    StyxWoW.Me.ActiveAuras["Arcane Blast"].StackCount >= 2),
                Spell.Cast(
                    "Arcane Barrage",
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Arcane Blast") && StyxWoW.Me.ActiveAuras["Arcane Blast"].StackCount >= 3),
                Spell.BuffSelf("Presence of Mind"),
                Spell.Cast("Arcane Blast"),
                Helpers.Common.CreateUseWand(),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.ArcaneMage)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateArcaneMagePull()
        {
            return
                new PrioritySelector(
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.Cast("Arcane Blast"),
                    Movement.CreateMoveToTargetBehavior(true, 35f)
                    );
        }
    }
}
