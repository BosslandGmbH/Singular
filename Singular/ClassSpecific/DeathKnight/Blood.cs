using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.DeathKnight
{
    public static class Blood
    {
        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.BloodDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateBloodDeathKnightCombat()
        {
            TankManager.NeedTankTargeting = true;
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Helpers.Common.CreateAutoAttack(true),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                // Blood DKs are tanks. NOT DPS. If you're DPSing as blood, go respec right now, because you fail hard.
                // Death Grip is used at all times in this spec, so don't bother with an instance check, like the other 2 specs.
                Spell.Cast("Death Grip", ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 15*15),
                //Make sure we're in range, and facing the damned target. (LOS check as well)
                Spell.BuffSelf("Bone Shield"),
                Spell.Cast("Rune Strike"),
                Spell.Cast("Mind Freeze", ret => StyxWoW.Me.CurrentTarget.IsCasting || StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                Spell.Cast("Strangulate", ret => StyxWoW.Me.CurrentTarget.IsCasting || StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                Spell.BuffSelf("Rune Tap", ret => StyxWoW.Me.HealthPercent <= 60),
                Spell.Cast(
                    "Pestilence", ret => StyxWoW.Me.CurrentTarget.HasAura("Blood Plague") && StyxWoW.Me.CurrentTarget.HasAura("Frost Fever") &&
                                         (from add in Unit.NearbyUnfriendlyUnits
                                          where !add.HasAura("Blood Plague") && !add.HasAura("Frost Fever") && add.Distance < 10
                                          select add).Count() > 0),
                new Decorator(
                    ret => SpellManager.CanCast("Death and Decay") && Unit.NearbyUnfriendlyUnits.Count(a => a.Distance < 8) > 1,
                    new Action(
                        ret =>
                        {
                            SpellManager.Cast("Death and Decay");
                            LegacySpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                        })),
                Spell.Cast("Icy Touch"),
                Spell.Cast("Plague Strike", ret => !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                Spell.Cast("Death Strike", ret => StyxWoW.Me.HealthPercent < 80),
                Spell.Cast("Blood Boil", ret => Unit.NearbyUnfriendlyUnits.Count(a => a.Distance < 8) > 1),
                Spell.Cast("Heart Strike"),
                Spell.Cast("Death Coil"),
                Movement.CreateMoveToTargetBehavior(true, 5f));
        }
    }
}
