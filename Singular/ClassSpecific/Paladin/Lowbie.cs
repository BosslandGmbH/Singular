using System.Linq;

using Styx.Combat.CombatRoutine;

using Singular.Settings;

using TreeSharp;
using Styx.Logic.Combat;

namespace Singular
{
	partial class SingularRoutine
	{
		[Class(WoWClass.Paladin)]
		[Spec(TalentSpec.Lowbie)]
		[Behavior(BehaviorType.Combat)]
		[Context(WoWContext.All)]
		public Composite CreateLowbiePaladinCombat()
		{
			return
				new PrioritySelector(
					CreateEnsureTarget(),
					CreateAutoAttack(true),
					CreateRangeAndFace(5f, ret => Me.CurrentTarget),
					CreateSpellCast("Crusader Strike"),
					CreateSpellCast("Judgement")
				);
		}

		[Class(WoWClass.Paladin)]
		[Spec(TalentSpec.Lowbie)]
		[Behavior(BehaviorType.Pull)]
		[Context(WoWContext.All)]
		public Composite CreateLowbiePaladinPull()
		{
			return
				new PrioritySelector(
					CreateLosAndFace(ret => Me.CurrentTarget),
					CreateSpellCast("Judgement"),
					CreateRangeAndFace(5f, ret => Me.CurrentTarget)
				);
		}

		[Class(WoWClass.Paladin)]
		[Spec(TalentSpec.Lowbie)]
		[Behavior(BehaviorType.Heal)]
		[Context(WoWContext.All)]
		public Composite CreateLowbiePaladinHeal()
		{
			return
				new PrioritySelector(
					CreateSpellCastOnSelf("Word of Glory", ret => Me.HealthPercent < 50),
					CreateSpellCastOnSelf("Holy Light", ret => Me.HealthPercent < 40)
				);
		}

		[Class(WoWClass.Paladin)]
		[Spec(TalentSpec.Lowbie)]
		[Behavior(BehaviorType.PreCombatBuffs)]
		[Context(WoWContext.All)]
		public Composite CreateLowbiePaladinPreCombatBuffs()
		{
			return
				new PrioritySelector(
					CreateSpellBuffOnSelf("Seal of Righteousness"),
					CreateSpellBuffOnSelf("Devotion Aura")
				);
		}

		[Class(WoWClass.Paladin)]
		[Spec(TalentSpec.Lowbie)]
		[Behavior(BehaviorType.CombatBuffs)]
		[Context(WoWContext.All)]
		public Composite CreateLowbiePaladinCombatBuffs()
		{
			return
				new PrioritySelector(
					CreateSpellBuffOnSelf("Seal of Righteousness"),
					CreateSpellBuffOnSelf("Devotion Aura")
				);
		}
	}
}