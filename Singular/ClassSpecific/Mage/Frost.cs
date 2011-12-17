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
        // All but BGs
        [Context(WoWContext.All & ~WoWContext.Battlegrounds)]
        public static Composite CreateFrostMageCombat()
        {
            PetManager.WantedPet = "Water Elemental";
            return new PrioritySelector(
                Safers.EnsureTarget(),
                //Move away from frozen targets
                new Decorator(
                    ret =>
                    (StyxWoW.Me.CurrentTarget.HasAura("Frost Nova") || StyxWoW.Me.CurrentTarget.HasAura("Freeze")) &&
                    StyxWoW.Me.CurrentTarget.DistanceSqr < 5 * 5,
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
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Pet.CreateCastPetActionOnLocation("Freeze", ret => !StyxWoW.Me.CurrentTarget.HasAura("Frost Nova")),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.Buff("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8)),
                Common.CreateMagePolymorphOnAddBehavior(),
                new Decorator(
                    ret => !StyxWoW.Me.GotAlivePet,
                    new Action(ret => PetManager.CallPet(PetManager.WantedPet))),

                // Pop evo if we're low on mana. If we're glyphed, also pop it for health.
                Spell.Cast("Evocation", ret => StyxWoW.Me.ManaPercent < 20 || (TalentManager.HasGlyph("Evocation") && StyxWoW.Me.HealthPercent < 50)),

                // We really should only save this for bosses, but this will work fine I suppose.
                Spell.Cast("Mirror Image"),
                // Debuff is 10min, CD is 5min. Don't recast if its not going to apply.
                Spell.Cast("Time Warp", ret => !StyxWoW.Me.HasAnyAura("Sated", "Temporal Displacement")),

                new Decorator(ret=>StyxWoW.Me.CurrentTarget.IsBoss(),
                    Spell.BuffSelf("Icy Veins")),

                // If the mob is > 50%, or a boss, make sure we pop orbs.
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent > 50 || StyxWoW.Me.CurrentTarget.IsBoss(),
                    new Sequence(
                        new Action(ctx => StyxWoW.Me.CurrentTarget.Face()),
                        new Action(ctx => StyxWoW.SleepForLagDuration()),
                        new PrioritySelector(Spell.Cast("Flame Orb"))
                        )),

                Spell.Cast("Deep Freeze", ret => (StyxWoW.Me.ActiveAuras.ContainsKey("Fingers of Frost") || StyxWoW.Me.CurrentTarget.HasAnyAura("Freeze", "Frost Nova"))),
                Spell.Cast("Ice Lance", ret => (StyxWoW.Me.ActiveAuras.ContainsKey("Fingers of Frost") || StyxWoW.Me.CurrentTarget.HasAnyAura("Freeze", "Frost Nova"))),
                Spell.Cast("Frostfire Bolt", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Brain Freeze")),
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

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FrostMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateFrostMagePvpCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),

                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                // First, deal with any procs we want popping now.
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Brain Freeze"),
                    new PrioritySelector(
                        Spell.Cast("Frostfire Bolt"),
                        Spell.Cast("Fireball")
                        )),


                // Now deal with getting out of roots, TODO: Add more shit mages can blink out of.
                Spell.Cast("Blink", ret => StyxWoW.Me.IsRooted() || StyxWoW.Me.HasAnyAura("Deep Freeze"))
                );
        }
    }
}
