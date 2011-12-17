using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
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
                ctx => TankManager.Instance.FirstUnit ?? StyxWoW.Me.CurrentTarget,

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.BuffSelf("Blood Presence"),

                // DG to speed up soloing
                Spell.Cast("Death Grip", ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 15 * 15 && !StyxWoW.Me.IsInInstance),

                // Interrupts
                Spell.Cast("Mind Freeze", ret => StyxWoW.Me.CurrentTarget.IsCasting || StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                Spell.Cast("Strangulate", ret => StyxWoW.Me.CurrentTarget.IsCasting || StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0),
                Spell.Cast("Anti-Magic Shell", ret => (StyxWoW.Me.CurrentTarget.IsCasting || StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != 0) && StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me && SingularSettings.Instance.DeathKnight.UseAntiMagicShell),

                /*
                    Big cooldown section. By default, all cooldowns are priorotized by their time ascending
                    for maximum uptime in the long term. By default, all cooldowns are also exlusive. This
                    means they will be used in rotation rather than conjunction. This is required for high
                    end blood tanking.
                */
                Spell.Cast("Death Pact", ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.PetSacrificePercent && StyxWoW.Me.GotAlivePet),
                Spell.Cast("Rune Tap", ret => StyxWoW.Me.HealthPercent < 90 && StyxWoW.Me.HasAura("Will of the Necropolis")),
                Spell.Cast("Death Coil", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 70 && StyxWoW.Me.HasAura("Lichborne")),
                Spell.BuffSelf("Bone Shield", ret =>
                    SingularSettings.Instance.DeathKnight.UseBoneShield
                    && (!SingularSettings.Instance.DeathKnight.BoneShieldExclusive ||
                        (!StyxWoW.Me.HasAura("Vampiric Blood")
                        && !StyxWoW.Me.HasAura("Dancing Rune Weapon")
                        && !StyxWoW.Me.HasAura("Lichborne")
                        && !StyxWoW.Me.HasAura("Icebound Fortitude")))),
                Spell.Cast("Vampiric Blood", ret =>
                    SingularSettings.Instance.DeathKnight.UseVampiricBlood
                    && StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.VampiricBloodPercent
                    && (!SingularSettings.Instance.DeathKnight.VampiricBloodExclusive ||
                        (!StyxWoW.Me.HasAura("Bone Shield")
                        && !StyxWoW.Me.HasAura("Dancing Rune Weapon")
                        && !StyxWoW.Me.HasAura("Lichborne")
                        && !StyxWoW.Me.HasAura("Icebound Fortitude")))),
                Spell.Cast("Dancing Rune Weapon", ret =>
                    SingularSettings.Instance.DeathKnight.UseDancingRuneWeapon
                    && StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DancingRuneWeaponPercent
                    && (!SingularSettings.Instance.DeathKnight.DancingRuneWeaponExclusive ||
                        (!StyxWoW.Me.HasAura("Bone Shield")
                        && !StyxWoW.Me.HasAura("Vampiric Blood")
                        && !StyxWoW.Me.HasAura("Lichborne")
                        && !StyxWoW.Me.HasAura("Icebound Fortitude")))),
                Spell.Cast("Lichborne", ret =>
                    SingularSettings.Instance.DeathKnight.UseLichborne
                    && StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.LichbornePercent
                    && StyxWoW.Me.CurrentRunicPower >= 60
                    && (!SingularSettings.Instance.DeathKnight.LichborneExclusive ||
                        (!StyxWoW.Me.HasAura("Bone Shield")
                        && !StyxWoW.Me.HasAura("Vampiric Blood")
                        && !StyxWoW.Me.HasAura("Dancing Rune Weapon")
                        && !StyxWoW.Me.HasAura("Icebound Fortitude")))),
                Spell.Cast("Raise Dead", ret =>
                    SingularSettings.Instance.DeathKnight.UsePetSacrifice
                    && StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.PetSacrificeSummonPercent
                    && (!SingularSettings.Instance.DeathKnight.PetSacrificeExclusive ||
                        (!StyxWoW.Me.HasAura("Bone Shield")
                        && !StyxWoW.Me.HasAura("Vampiric Blood")
                        && !StyxWoW.Me.HasAura("Dancing Rune Weapon")
                        && !StyxWoW.Me.HasAura("Lichborne")
                        && !StyxWoW.Me.HasAura("Icebound Fortitude")))),
                Spell.Cast("Icebound Fortitude", ret =>
                    SingularSettings.Instance.DeathKnight.UseIceboundFortitude
                    && StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.IceboundFortitudePercent
                    && (!SingularSettings.Instance.DeathKnight.IceboundFortitudeExclusive ||
                        (!StyxWoW.Me.HasAura("Bone Shield")
                        && !StyxWoW.Me.HasAura("Vampiric Blood")
                        && !StyxWoW.Me.HasAura("Dancing Rune Weapon")
                        && !StyxWoW.Me.HasAura("Lichborne")))),
                Spell.Cast("Empower Rune Weapon", ret =>
                    StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.EmpowerRuneWeaponPercent
                    && !SpellManager.CanCast("Death Strike")),
                Spell.Cast("Army of the Dead", ret =>
                    SingularSettings.Instance.DeathKnight.UseArmyOfTheDead
                    && StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.ArmyOfTheDeadPercent),

                // AoE
                Spell.Cast(
                    "Pestilence", ret => StyxWoW.Me.CurrentTarget.HasAura("Blood Plague") && StyxWoW.Me.CurrentTarget.HasAura("Frost Fever") &&
                                         (from add in Unit.NearbyUnfriendlyUnits
                                          where !add.HasAura("Blood Plague") && !add.HasAura("Frost Fever") && add.Distance < 10
                                          select add).Count() > 0),
                Spell.CastOnGround("Death and Decay", ret => StyxWoW.Me.CurrentTarget.Location,
                        ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay &&
                               Unit.NearbyUnfriendlyUnits.Count(a => a.DistanceSqr < 10 * 10) >= SingularSettings.Instance.DeathKnight.DeathAndDecayCount),

                // DG if we can, DC if we can't. DC is our 10s taunt. DG is our "get the fuck over here" taunt
                Spell.Cast(
                    "Death Grip", ret => TankManager.Instance.NeedToTaunt.First(), ret => SingularSettings.Instance.EnableTaunting && TankManager.Instance.NeedToTaunt.FirstOrDefault() != null),
                Spell.Cast(
                    "Dark Command", ret => TankManager.Instance.NeedToTaunt.First(), ret => SingularSettings.Instance.EnableTaunting && TankManager.Instance.NeedToTaunt.FirstOrDefault() != null),

                Movement.CreateMoveBehindTargetBehavior(),
                Spell.Cast("Outbreak", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") || !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                Spell.Cast(
                    "Death Strike", ret =>
                    !StyxWoW.Me.IsInInstance
                    || StyxWoW.Me.CurrentHealth < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent	// only stack blood shield if we absolutely have to
                    || (StyxWoW.Me.UnholyRuneCount + StyxWoW.Me.FrostRuneCount + StyxWoW.Me.DeathRuneCount) >= 4	// keep runes cycling
                    || (StyxWoW.Me.HealthPercent < 93 && !StyxWoW.Me.HasAura("Blood Shield"))						// ds if it's going to be an efficient heal and not double up on blood shield
                ),

                // Convert existing blood rune to death and immediately refresh an UH/F rune for a followup Death Strike
                Spell.Cast("Blood Tap", ret => StyxWoW.Me.HealthPercent <= 93 && StyxWoW.Me.BloodRuneCount >= 1 && (StyxWoW.Me.UnholyRuneCount == 0 || StyxWoW.Me.FrostRuneCount == 0)),
                Spell.Cast("Rune Tap", ret => StyxWoW.Me.HealthPercent <= 83 && StyxWoW.Me.BloodRuneCount >= 1 && SingularSettings.Instance.DeathKnight.UseRuneTap),

                Spell.Cast("Icy Touch", ret => StyxWoW.Me.CurrentTarget.IsBoss() && !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") && Spell.GetSpellCooldown("Outbreak").TotalSeconds > 10),
                Spell.Cast("Plague Strike", ret => StyxWoW.Me.CurrentTarget.IsBoss() && !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague") && Spell.GetSpellCooldown("Outbreak").TotalSeconds > 10),

                Spell.Cast("Blood Boil", ret => Unit.NearbyUnfriendlyUnits.Count(a => a.Distance < 8) > 3 && (StyxWoW.Me.BloodRuneCount >= 1 || !StyxWoW.Me.IsInInstance)),
                Spell.Cast("Heart Strike", ret => StyxWoW.Me.BloodRuneCount == 2 || (StyxWoW.Me.BloodRuneCount == 1 && !StyxWoW.Me.IsInInstance)),

                // Only RS if we know it may proc Runic Empowerment and aren't currently stockpiling runic power
                Spell.Cast("Rune Strike", ret => (StyxWoW.Me.CurrentRunicPower >= 60 || StyxWoW.Me.HealthPercent > 90 || !StyxWoW.Me.IsInInstance) && (StyxWoW.Me.UnholyRuneCount == 0 || StyxWoW.Me.FrostRuneCount == 0)),

                // If we don't have RS yet, just resort to DC. Its not the greatest, but oh well. Make sure we keep enough RP banked for a self-heal if need be.
                Spell.Cast("Death Coil", ret => !SpellManager.HasSpell("Rune Strike") && StyxWoW.Me.CurrentRunicPower >= 80),
                Spell.Cast("Death Coil", ret => !StyxWoW.Me.CurrentTarget.IsWithinMeleeRange),
                Movement.CreateMoveToMeleeBehavior(true));
        }
    }
}
