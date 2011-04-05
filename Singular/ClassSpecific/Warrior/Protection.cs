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

using System.Linq;

using Singular.Settings;

using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ProtectionWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        public Composite CreateProtectionWarriorCombat()
        {
            NeedTankTargeting = true;
            return
                new PrioritySelector(
                    CreateEnsureTarget(),
                    // Note: this will make sure we charge at any unit within proper range. (Like an actual tank would)
                    // We can cast Charge while in combat if we have the talent. [Notice that I am NOT checking for the talent!]
                    // chargeSpell.CanCast should return false if we can't cast it.
                    CreateSpellCast("Charge", ret => Me.CurrentTarget.Distance.Between(8f, TalentManager.HasGlyph("Long Charge") ? 30f : 25f)),
                    CreateSpellCast("Intercept", ret => Me.CurrentTarget.Distance.Between(8f, 25f)),
                    CreateMoveToAndFace(5f, ret => Me.CurrentTarget),
                    CreateAutoAttack(true),
                    CreateSpellCast("Heroic Strike", ret => Me.CurrentRage >= 60),
                    CreateSpellCast("Revenge"),

                    // If we have Blood and Thunder talented, then try and apply Rend via tclap. (This will apply rend to all targets!)
                    CreateSpellCast(
                        "Thunder Clap", ret => NearbyUnfriendlyUnits.Any(u => HasAuraStacks("Rend", 1, u)) && TalentManager.GetCount(3, 3) != 0),
                    CreateSpellBuff("Rend"),
                    // To be honest, we should be throwing this whenever its off CD. But we'll use it for the 20% hit speed debuff for now.
                    CreateSpellCast("Thunder Clap"),
                    new Decorator(
                        ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 6) > 2,
                        new PrioritySelector(
                            CreateSpellCast("War Stomp"),
                            CreateSpellCast("Thunder Clap"),
                            CreateSpellCast("Shockwave"),
                            CreateSpellCast("Shield Block")
                            )),
                    CreateSpellCast("Victory Rush"),
                    CreateSpellCast("Arcane Torrent", ret => Me.CurrentTarget.IsCasting),
                    CreateSpellCast("Shield Bash", ret => Me.CurrentTarget.IsCasting),
                    CreateSpellCast("Shield Slam"),
                    CreateSpellBuff("Demoralizing Shout", ret => Me.CurrentRage > 30 && Me.CurrentTarget.HealthPercent > 30),
                    // Get sunders up, Devastate if we have it (extra damage) or just plain Sunder.
                    new Decorator(
                        ret => !Me.CurrentTarget.HasAura("Sunder Armor") || Me.CurrentTarget.Auras["Sunder Armor"].StackCount < 3,
                        new PrioritySelector(
                            CreateSpellCast("Devastate"),
                            CreateSpellCast("Sunder Armor")))
                    );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ProtectionWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateProtectionWarriorPull()
        {
            return
                new PrioritySelector(
                    CreateEnsureTarget(),
                    CreateAutoAttack(true),
                    CreateSpellCast("Charge"),
                    // Throws a throwing weapon, shoots a bow, gun, crossbow, or wand. (Yes, Wand)
                    CreateFireRangedWeapon(),
                    CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
                    );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ProtectionWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.CombatBuffs)]
        public Composite CreateProtectionWarriorCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellCast(
                        "Taunt", ret => TankTargeting.Instance.NeedToTaunt.Count != 0, ret => TankTargeting.Instance.NeedToTaunt.FirstOrDefault()),
                    CreateSpellCast("Challenging Shout", ret => TankTargeting.Instance.NeedToTaunt.Count(u => u.Distance < 10) > 2),
                    CreateSpellBuffOnSelf(
                        "Berserker Rage",
                        ret => Me.Auras.Any(
                            aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Fleeing ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Sapped ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Incapacitated ||
                                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Horrified)),
                    CreateSpellBuffOnSelf(
                        "Commanding Shout", ret => !Me.HasAura("Qiraji Fortitude") &&
                                                   !Me.HasAura("Power Word: Fortitude") &&
                                                   !Me.HasAura("Blood Pact")),
                    CreateSpellBuffOnSelf(
                        "Enraged Regeneration",
                        ret => Me.GetAllAuras().Exists(a => a.Spell.Mechanic == WoWSpellMechanic.Enraged) &&
                               Me.HealthPercent <= SingularSettings.Instance.Warrior.WarriorEnragedRegenerationHealth),
                    CreateSpellBuffOnSelf(
                        "Shield Wall",
                        ret => Me.HealthPercent <= SingularSettings.Instance.Warrior.WarriorProtShieldWallHealth)
                    );
        }

        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ProtectionWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        public Composite CreateProtectionWarriorPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellBuffOnSelf("Defensive Stance")
                    );
        }
    }
}