using System.Linq;
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
    public class Frost
    {
        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FrostMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateFrostMageCombat()
        {
            PetManager.WantedPet = "Water Elemental";
            return new PrioritySelector(
                Safers.EnsureTarget(),
                //Move away from frozen targets
                new Decorator(
                    ret => (StyxWoW.Me.CurrentTarget.HasAura("Frost Nova") || StyxWoW.Me.CurrentTarget.HasAura("Freeze")) && StyxWoW.Me.CurrentTarget.DistanceSqr < 5 * 5,
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
                Waiters.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Pet.CreateCastPetActionOnLocation("Freeze", ret => !StyxWoW.Me.CurrentTarget.HasAura("Frost Nova")),
                Spell.Buff("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8)),
                Common.CreateMagePolymorphOnAddBehavior(),
                new Decorator(
                    ret => !StyxWoW.Me.GotAlivePet,
                    new Action(ret => PetManager.CallPet(PetManager.WantedPet))),
                Spell.Cast("Evocation", ret => StyxWoW.Me.ManaPercent < 20),
                Spell.Cast("Counterspell", ret => StyxWoW.Me.CurrentTarget.IsCasting),
                Spell.Cast("Mirror Image"),
                Spell.Cast("Time Warp"),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent > 50,
                    new Sequence(
                        new Action(ctx => StyxWoW.Me.CurrentTarget.Face()),
                        new Action(ctx => StyxWoW.SleepForLagDuration()),
                        new PrioritySelector(Spell.Cast("Flame Orb"))
                        )),
                Spell.BuffSelf("Ice Barrier", ret => !StyxWoW.Me.Auras.ContainsKey("Mana Shield")),
                Spell.BuffSelf("Mana Shield", ret => !StyxWoW.Me.Auras.ContainsKey("Ice Barrier") && StyxWoW.Me.HealthPercent <= 50),
                Spell.Cast(
                    "Deep Freeze",
                    ret =>
                    (StyxWoW.Me.ActiveAuras.ContainsKey("Fingers of Frost") || StyxWoW.Me.CurrentTarget.HasAura("Frost Nova") || StyxWoW.Me.CurrentTarget.HasAura("Freeze"))),
                Spell.Cast(
                    "Ice Lance",
                    ret =>
                    (StyxWoW.Me.ActiveAuras.ContainsKey("Fingers of Frost") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Frost Nova") ||
                     StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Freeze"))),
                //Spell.Cast("Fireball", StyxWoW.Me.ActiveAuras.ContainsKey("Brain Freeze")),
                Spell.Cast("Arcane Missiles", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Arcane Missiles!")),
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Brain Freeze"),
                    new PrioritySelector(
                        Spell.Cast("Frostfire Bolt"),
                        Spell.Cast("Fireball")
                        )),
                Spell.Buff("Fire Blast", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 10),
                Spell.Cast("Frostbolt"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FrostMage)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateFrostMagePull()
        {
            return
                new PrioritySelector(
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.Cast("Arcane Missiles", ret => StyxWoW.Me.HasAura("Arcane Missiles!")),
                    Spell.Cast("Frostbolt"),
                    Movement.CreateMoveToTargetBehavior(true, 35f)
                    );
        }
    }
}
