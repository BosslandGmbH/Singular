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
    public static class Unholy
    {
        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.UnholyDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateUnholyDeathKnightCombat()
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
                Spell.Cast("Unholy Frenzy", ret => StyxWoW.Me.HealthPercent >= 80),
                Spell.Cast("Death Strike", ret => StyxWoW.Me.HealthPercent < 80),
                Spell.Cast("Outbreak", ret => StyxWoW.Me.CurrentTarget.HasAura("Frost Fever") || StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                Spell.Cast("Icy Touch"),
                Spell.Cast("Plague Strike", ret => !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                Spell.Cast(
                    "Pestilence", ret => StyxWoW.Me.CurrentTarget.HasAura("Blood Plague") && StyxWoW.Me.CurrentTarget.HasAura("Frost Fever") &&
                                         (Unit.NearbyUnfriendlyUnits.Where(
                                             add => !add.HasAura("Blood Plague") && !add.HasAura("Frost Fever") && add.Distance < 10)).Count() > 0),
                Spell.CastOnGround("Death and Decay", ret => StyxWoW.Me.CurrentTarget.Location, ret => Unit.NearbyUnfriendlyUnits.Count(a => a.DistanceSqr < 8*8) > 1),
                Spell.Cast("Summon Gargoyle"),
                Spell.Cast("Dark Transformation", ret => StyxWoW.Me.GotAlivePet && !StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),
                Spell.Cast("Scourge Strike", ret => StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2),
                Spell.Cast("Festering Strike", ret => StyxWoW.Me.BloodRuneCount == 2 && StyxWoW.Me.FrostRuneCount == 2),
                Spell.Cast("Death Coil", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Sudden Doom") || StyxWoW.Me.CurrentRunicPower >= 80),
                Spell.Cast("Scourge Strike"),
                Spell.Cast("Festering Strike"),
                Spell.Cast("Death Coil"),
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }
    }
}
