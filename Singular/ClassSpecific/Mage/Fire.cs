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

using Singular.Settings;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
		[Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateFireMageCombat()
        {
            return new PrioritySelector(
			    CreateEnsureTarget(),
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateMoveToAndFace(40f, ret => Me.CurrentTarget),
				CreateSpellCast("Evocation", ret => Me.ManaPercent < 40),
                CreateSpellCast(
                    "Scorch",
                    ret =>
                    !Me.CurrentTarget.HasAura("Critical Mass") || Me.CurrentTarget.Auras["Critical Mass"].TimeLeft.TotalSeconds < 3 ||
                    // If we have the Firestarter buff, we can cast scorch on the move. Do so please!
                    (Me.IsMoving && TalentManager.GetCount(2, 15) != 0)
					&& LastSpellCast != "Scorch"),
                CreateSpellCast("Pyroblast", ret => Me.HasAura("Hot Streak") && Me.Auras["Hot Streak"].TimeLeft.TotalSeconds > 1),
                CreateSpellCast("Fire Blast", ret => Me.HasAura("Impact")),
                CreateSpellBuff("Living Bomb", ret => !Me.CurrentTarget.HasAura("Living Bomb")),
                CreateSpellCast("Fireball")
                );
        }
    
	
		[Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateFireMagePull()
        {
            return
                new PrioritySelector(
                    // Make sure we're in range, and facing the damned target. (LOS check as well)
                    CreateMoveToAndFace(40f, ret => Me.CurrentTarget),
                    CreateSpellCast("Fireball")
                    );
        }
		
		[Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateFireMagePreCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellBuffOnSelf("Arcane Brilliance",
                        ret => (!Me.HasAura("Arcane Brilliance") &&
                               !Me.HasAura("Fel Intelligence"))),
						
                    CreateSpellBuffOnSelf(
                        "Molten Armor",
                        ret => (!Me.HasAura("Molten Armor"))
                    )
					);
					
	}
	}
}