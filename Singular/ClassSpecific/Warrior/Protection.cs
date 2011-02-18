﻿#region Revision Info

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

using Styx.Combat.CombatRoutine;

using TreeSharp;
using Styx;
using Styx.Logic.Combat;
using Singular.Settings;

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
			return
				new PrioritySelector(
					CreateSpellCast("Taunt", ret => Me.CurrentTarget.CurrentTarget != null && !Me.CurrentTarget.IsTargetingMeOrPet),
					CreateSpellCast("Challenging Shout", ret => Me.CurrentTarget.CurrentTarget != null && !Me.CurrentTarget.IsTargetingMeOrPet),
					CreateSpellCast("Heroic Strike", ret => Me.CurrentRage >= 60),
					CreateSpellCast("Revenge"),
					new Decorator(
						ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 6) > 2,
						new PrioritySelector(
							CreateSpellCast("Thunder Clap"),
							CreateSpellCast("Shockwave"),
							CreateSpellCast("Shield Block")
						)),
					CreateSpellCast("Victory Rush"),
					CreateSpellCast("Shield Bash", ret => Me.CurrentTarget.IsCasting),
					CreateSpellCast("Shield Slam"),
					CreateSpellBuff("Rend"),
					CreateSpellBuff("Demoralizing Shout", 
						ret => Me.CurrentRage > 30 &&
							   Me.CurrentTarget.HealthPercent > 30),
					new Decorator(
						ret => !Me.CurrentTarget.HasAura("Sunder Armor") ||
								Me.CurrentTarget.Auras["Sunder Armor"].StackCount < 3,
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
					CreateSpellCast("Charge"),
					CreateSpellCast("Throw",
						ret => Me.Inventory.Equipped.Ranged != null &&
							   Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Thrown),
					CreateSpellCast("Shoot",
						ret => Me.Inventory.Equipped.Ranged != null &&
							   (Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Bow ||
							   Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Crossbow))
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
					CreateSpellBuffOnSelf("Berserker Rage",
							ret => Me.Auras.Any(
								aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Fleeing ||
										aura.Value.Spell.Mechanic == WoWSpellMechanic.Sapped ||
										aura.Value.Spell.Mechanic == WoWSpellMechanic.Incapacitated ||
										aura.Value.Spell.Mechanic == WoWSpellMechanic.Horrified)),
					CreateSpellBuffOnSelf("Commanding Shout", ret => !Me.HasAura("Qiraji Fortitude") &&
                                                                     !Me.HasAura("Power Word: Fortitude") &&
                                                                     !Me.HasAura("Blood Pact")),
					CreateSpellBuffOnSelf("Enraged Regeneration", 
						ret => Me.GetAllAuras().Exists(a => a.Spell.Mechanic == WoWSpellMechanic.Enraged) && 
								Me.HealthPercent <= SingularSettings.Instance.Warrior.WarriorEnragedRegenerationHealth),
					CreateSpellBuffOnSelf("Shield Wall", 
						ret => Me.HealthPercent <= SingularSettings.Instance.Warrior.WarriorProtShieldWallHealth)
				);
		}

		[Class(WoWClass.Warrior)]
		[Spec(TalentSpec.ProtectionWarrior)]
		[Context(WoWContext.All)]
		[Behavior(BehaviorType.CombatBuffs)]
		public Composite CreateProtectionWarriorPreCombatBuffs()
		{
			return
				new PrioritySelector(
					CreateSpellBuffOnSelf("Defensive Stance")
				);
		}
	}
}