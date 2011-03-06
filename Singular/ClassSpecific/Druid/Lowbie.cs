using System;
using System.Collections.Generic;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Spec(TalentSpec.Lowbie)]
        [Class(WoWClass.Druid)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
		[Behavior(BehaviorType.Heal)]
        public Composite CreateLowbieDruidCombat()
        {
            WantedDruidForm = Styx.ShapeshiftForm.Cat;
            return new PrioritySelector(
                CreateEnsureTarget(),
				// Make sure we're in cat form first, period.
                new Decorator(
                    ret => Me.Shapeshift != WantedDruidForm,
                    CreateSpellCast("Cat Form")),
				//Move to mele if we have cat form
				new Decorator(
                    ret => SpellManager.HasSpell("Cat Form"),
					CreateMoveToAndFace(4.5f, ret => Me.CurrentTarget)),
				//Move to range for spells
				CreateMoveToAndFace(40f, ret => Me.CurrentTarget),
				//Healing if needed in combat
				CreateSpellBuff("Rejuvenation", ret => Me.HealthPercent <= 60 && !Me.HasAura("Rejuvenation")),
                CreateAutoAttack(true),
				CreateSpellCast("Rake", ret => (!Me.CurrentTarget.HasAura("Rake") || Me.CurrentTarget.GetAuraByName("Rake").CreatorGuid != Me.Guid) && SpellManager.HasSpell("Cat Form")),
				CreateSpellCast("Ferocious Bite", ret => Me.ComboPoints > 4 && SpellManager.HasSpell("Cat Form")),
				CreateSpellCast("Claw", ret=> SpellManager.HasSpell("Cat Form")),
				//Pre Cat spells
				CreateSpellCast("Moonfire", ret=> !Me.HasAura("Cat Form") && !Me.CurrentTarget.HasAura("Moonfire")),
				CreateSpellCast("Wrath", ret=> !Me.HasAura("Cat Form"))
                );
        }
		
	    [Spec(TalentSpec.Lowbie)]
        [Class(WoWClass.Druid)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateLowbiePull()
		{
            WantedDruidForm = Styx.ShapeshiftForm.Cat;
            return new PrioritySelector(
                CreateEnsureTarget(),
				// Make sure we're in cat form first, period.
                new Decorator(
                    ret => Me.Shapeshift != WantedDruidForm,
                    CreateSpellCast("Cat Form")),
				//Move to mele if we have cat form
				new Decorator(
                    ret => SpellManager.HasSpell("Cat Form"),
					CreateMoveToAndFace(4.5f, ret => Me.CurrentTarget)),
				//Move to range for spells
				CreateMoveToAndFace(40f, ret => Me.CurrentTarget),
                CreateAutoAttack(true),
				CreateSpellCast("Rake", ret => (!Me.CurrentTarget.HasAura("Rake") || Me.CurrentTarget.GetAuraByName("Rake").CreatorGuid != Me.Guid) && SpellManager.HasSpell("Cat Form")),
				CreateSpellCast("Starfire", ret=> !Me.HasAura("Cat Form")),
				CreateSpellCast("Wrath", ret=> !Me.HasAura("Cat Form"))
				);
		}
    }
}
