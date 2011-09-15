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
    public class Blood
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
                // Mostly for Army of the Dead
                Spell.WaitForCast(),

                Spell.BuffSelf("Blood Presence"),
                // Blood DKs are tanks. NOT DPS. If you're DPSing as blood, go respec right now, because you fail hard.
                // Death Grip is used at all times in this spec, so don't bother with an instance check, like the other 2 specs.
                Spell.Cast("Death Grip", ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 15 * 15),
                //Make sure we're in range, and facing the damned target. (LOS check as well)
                Spell.BuffSelf("Bone Shield"),
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
                // Defensive CDs
                // The big "oh shit" button. ERW -> Army. Lets try and live as long as possible. (Note: Only really works on non-raid bosses :()
                new Decorator(
                    ret => StyxWoW.Me.HealthPercent < 20,
                    new Sequence(
                        Spell.Cast("Empower Rune Weapon"),
                        Spell.Cast("Army of the Dead"))),

                // DG if we can, DC if we can't. DC is our 10s taunt. DG is our "get the fuck over here" taunt
                Spell.Cast(
                    "Death Grip", ret => TankManager.Instance.NeedToTaunt.First(), ret => TankManager.Instance.NeedToTaunt.FirstOrDefault() != null),
                Spell.Cast(
                    "Dark Command", ret => TankManager.Instance.NeedToTaunt.First(), ret => TankManager.Instance.NeedToTaunt.FirstOrDefault() != null),

                Spell.Cast("Rune Tap", ret => StyxWoW.Me.HealthPercent < 85),
                Spell.Cast("Death Coil", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 70 && StyxWoW.Me.HasAura("Lichborne")),
                Spell.Cast("Icebound Fortitude", ret => StyxWoW.Me.CurrentHealth < 60),
                Spell.Cast("Vampiric Blood", ret => StyxWoW.Me.HealthPercent < 40),
                // Just keep it on CD. We should always have a depleted rune anyway. May need tweaking.
                Spell.Cast("Blood Tap", ret => StyxWoW.Me.BloodRuneCount == 0),

                // Threat & Debuffs
                Spell.Cast("Outbreak"), // If we got it, pop it.
                Spell.Cast("Icy Touch", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever")),
                Spell.Cast("Plague Strike", ret => !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                Spell.Cast("Death Strike"),
                Spell.Cast("Blood Boil", ret => Unit.NearbyUnfriendlyUnits.Count(a => a.Distance < 8) > 3),
                Spell.Cast("Heart Strike"),
                Spell.Cast("Rune Strike"),
                // If we don't have RS yet, just resort to DC. Its not the greatest, but oh well. Make sure we keep enough RP banked for a self-heal if need be.
                Spell.Cast("Death Coil", ret => !SpellManager.HasSpell("Rune Strike") && StyxWoW.Me.CurrentRunicPower >= 80),
                Spell.Cast("Death Coil", ret => !StyxWoW.Me.CurrentTarget.IsWithinMeleeRange),
                Movement.CreateMoveToTargetBehavior(true, 5f));
        }
    }
}
