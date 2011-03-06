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

using Styx.Combat.CombatRoutine;

using TreeSharp;
using Styx;
using Styx.Logic.Combat;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        public Composite CreateArmsWarriorCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
				//Move to range
                CreateMoveToAndFace(25f, ret => Me.CurrentTarget),
				CreateAutoAttack(true),
				//Runner target
				new Decorator(
					ret => Me.CurrentTarget.Fleeing,
					new PrioritySelector(
						CreateSpellCast("Heroic Throw"),
						CreateSpellCast("Charge", ret => Me.CurrentTarget.Distance > 10),
						CreateSpellCast("Intercept", ret => Me.CurrentTarget.Distance > 10)
						)),
				//Move to melee
				CreateMoveToAndFace(5f, ret => Me.CurrentTarget),
				CreateSpellCast("Victory Rush"),
				CreateSpellCast("Pummel", ret => Me.CurrentTarget.IsCasting),
                new Decorator(
                    ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 6) > 2,
                    new PrioritySelector(
                        CreateSpellBuff("Rend"),
                        CreateSpellCast("Thunderclap"),
                        CreateSpellCast("Sweeping Strikes"),
                        CreateSpellCast("Bladestorm"),
                        CreateSpellCast("Cleave"),
                        CreateSpellCast("Whirlwind")
                        )),
                CreateSpellCast("Rend", ret => !Me.CurrentTarget.HasAura("Rend")),
                CreateSpellCast("Collossus Smash"),
                CreateSpellCast("Execute"),
                CreateSpellCast("Overpower"),
                CreateSpellCast("Mortal Strike"),
                CreateSpellCast("Slam"),
                CreateSpellCast("Heroic Strike", ret => Me.RagePercent > 60)
                );
        }

		[Class(WoWClass.Warrior)]
		[Spec(TalentSpec.ArmsWarrior)]
		[Context(WoWContext.All)]
		[Behavior(BehaviorType.Pull)]
		public Composite CreateArmsWarriorPull()
		{
			return 
				new PrioritySelector(
					CreateEnsureTarget(),
					CreateMoveToAndFace(25f, ret => Me.CurrentTarget),
					CreateSpellCast("Charge", ret => Me.CurrentTarget.Distance > 10),
					CreateSpellCast("Throw", 
						ret => Me.Inventory.Equipped.Ranged != null &&
                               Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Thrown),
					CreateSpellCast("Shoot",
						ret => Me.Inventory.Equipped.Ranged != null &&
                               (Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Bow ||
                               Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Crossbow)),					
					CreateAutoAttack(true),
					CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
				);
		}

		[Class(WoWClass.Warrior)]
		[Spec(TalentSpec.ArmsWarrior)]
		[Context(WoWContext.All)]
		[Behavior(BehaviorType.CombatBuffs)]
		public Composite CreateArmsWarriorCombatBuffs()
		{
			return
				new PrioritySelector(
					CreateSpellBuffOnSelf("Berserker Rage",
							ret => Me.Auras.Any(
								aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Fleeing ||
										aura.Value.Spell.Mechanic == WoWSpellMechanic.Sapped ||
										aura.Value.Spell.Mechanic == WoWSpellMechanic.Incapacitated ||
										aura.Value.Spell.Mechanic == WoWSpellMechanic.Horrified)),
					CreateSpellBuffOnSelf("Battle Shout", ret => !Me.HasAura("Horn of the Winter") &&
																 !Me.HasAura("Roar of Courage") &&
																 !Me.HasAura("Strength of Earth Totem"))
				);
		}

		[Class(WoWClass.Warrior)]
		[Spec(TalentSpec.ArmsWarrior)]
		[Context(WoWContext.All)]
		[Behavior(BehaviorType.PreCombatBuffs)]
		public Composite CreateArmsWarriorPreCombatBuffs()
		{
			return
				new PrioritySelector(
					CreateSpellBuffOnSelf("Battle Stance")
				);
		}
    }
}