using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Frost
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
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // Note: You should have this in 2 different methods. Hence the reason for WoWContext being a [Flags] enum.
                // In this case, since its only one spell being changed, we can live with it.
                Spell.Cast("Death Grip", ret => StyxWoW.Me.CurrentTarget.Distance > 15 && !StyxWoW.Me.IsInInstance),

                Movement.CreateMoveBehindTargetBehavior(),

                Spell.Cast("Rune Strike"),
                Spell.Cast("Death Strike", ret => StyxWoW.Me.HealthPercent < 30),

                // Cooldowns
                Spell.Cast("Pillar of Frost", ret => SingularSettings.Instance.DeathKnight.UsePillarOfFrost),
                Spell.Cast("Raise Dead", ret => SingularSettings.Instance.DeathKnight.UseRaiseDead && !StyxWoW.Me.GotAlivePet && StyxWoW.Me.CurrentTarget.IsBoss() && StyxWoW.Me.HasAura("Pillar of Frost")),
                Spell.Cast("Empower Rune Weapon", ret => SingularSettings.Instance.DeathKnight.UseEmpowerRuneWeapon && StyxWoW.Me.UnholyRuneCount == 0 && StyxWoW.Me.FrostRuneCount == 0 && StyxWoW.Me.DeathRuneCount == 0 && !SpellManager.CanCast("Frost Strike") && StyxWoW.Me.CurrentTarget.IsBoss()),

                // Start AoE section
                new Decorator(ret => Unit.NearbyUnfriendlyUnits.Count(a => a.Distance < 8) >= 3,
                    new PrioritySelector(
                        Spell.Cast("Howling Blast", ret => StyxWoW.Me.FrostRuneCount == 2 || StyxWoW.Me.DeathRuneCount == 2),
                        new Decorator(
                            ret => SpellManager.CanCast("Death and Decay") && StyxWoW.Me.UnholyRuneCount == 2,
                            new Action(
                                ret =>
                                {
                                    SpellManager.Cast("Death and Decay");
                                    LegacySpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                                })),
                        Spell.Cast("Plague Strike", ret => StyxWoW.Me.UnholyRuneCount == 2),
                        Spell.Cast("Frost Strike", ret => StyxWoW.Me.CurrentRunicPower == StyxWoW.Me.MaxRunicPower),
                        Spell.Cast("Howling Blast"),
                        new Decorator(
                            ret => SpellManager.CanCast("Death and Decay"),
                            new Action(
                                ret =>
                                {
                                    SpellManager.Cast("Death and Decay");
                                    LegacySpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                                })),
                        Spell.Cast("Plague Strike"),
                        Spell.Cast("Frost Strike"),
                        Spell.Cast("Horn of Winter")
                    )
                ),

                // Start single target section
                Spell.Cast("Outbreak", ret => StyxWoW.Me.CurrentTarget.HasAura("Frost Fever") || StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                Spell.Cast("Howling Blast", ret => !StyxWoW.Me.CurrentTarget.HasAura("Frost Fever")),
                Spell.Cast("Plague Strike", ret => !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                Spell.Cast(
                    "Pestilence", ret => StyxWoW.Me.CurrentTarget.HasAura("Blood Plague") && StyxWoW.Me.CurrentTarget.HasAura("Frost Fever") &&
                                         (from add in Unit.NearbyUnfriendlyUnits
                                          where !add.HasAura("Blood Plague") && !add.HasAura("Frost Fever") && add.Distance < 10
                                          select add).Count() > 0),
                Spell.Cast(
                    "Obliterate",
                    ret => (StyxWoW.Me.FrostRuneCount == 2 && StyxWoW.Me.UnholyRuneCount == 2) || StyxWoW.Me.DeathRuneCount == 2 || StyxWoW.Me.HasAura("Killing Machine")),
                Spell.Cast("Frost Strike", ret => StyxWoW.Me.CurrentRunicPower == StyxWoW.Me.MaxRunicPower),
                Spell.Cast("Howling Blast", ret => StyxWoW.Me.HasAura("Freezing Fog")),
                Spell.Cast("Obliterate"),
                Spell.Cast("Frost Strike"),
                Spell.Cast("Blood Tap"),
                Spell.Cast("Howling Blast", ret => StyxWoW.Me.CurrentRunicPower < 32),
                Spell.Cast("Horn of Winter"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}
