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
    public static class Frost
    {
        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.FrostDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateFrostDeathKnightCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                // Note: You should have this in 2 different methods. Hence the reason for WoWContext being a [Flags] enum.
                // In this case, since its only one spell being changed, we can live with it.
                Spell.Cast("Death Grip", ret => StyxWoW.Me.CurrentTarget.Distance > 15 && !StyxWoW.Me.IsInInstance),
                //Make sure we're in range, and facing the damned target. (LOS check as well)
                Spell.Cast("Raise Dead", ret => !StyxWoW.Me.GotAlivePet),
                Spell.Cast("Rune Strike"),
                Spell.Cast("Mind Freeze", ret => StyxWoW.Me.CurrentTarget.IsCasting || StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                Spell.Cast("Strangulate", ret => StyxWoW.Me.CurrentTarget.IsCasting || StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                Spell.Cast("Death Strike", ret => StyxWoW.Me.HealthPercent < 80),
                Spell.Cast("Pillar of Frost"),
                Spell.Cast("Howling Blast", ret => StyxWoW.Me.HasAura("Freezing Fog") || !StyxWoW.Me.CurrentTarget.HasAura("Frost Fever")),
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
                Spell.Cast("Outbreak", ret => StyxWoW.Me.CurrentTarget.HasAura("Frost Fever") || StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                Spell.Cast("Plague Strike", ret => !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                Spell.Cast(
                    "Obliterate",
                    ret => (StyxWoW.Me.FrostRuneCount == 2 && StyxWoW.Me.UnholyRuneCount == 2) || StyxWoW.Me.DeathRuneCount == 2 || StyxWoW.Me.HasAura("Killing Machine")),
                Spell.Cast("Blood Strike", ret => StyxWoW.Me.BloodRuneCount == 2),
                Spell.Cast("Frost Strike", ret => StyxWoW.Me.HasAura("Freezing Fog") || StyxWoW.Me.CurrentRunicPower == StyxWoW.Me.MaxRunicPower),
                Spell.Cast("Blood Tap", ret => StyxWoW.Me.BloodRuneCount < 2),
                Spell.Cast("Obliterate"),
                Spell.Cast("Blood Strike"),
                Spell.Cast("Frost Strike"),
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }
    }
}
