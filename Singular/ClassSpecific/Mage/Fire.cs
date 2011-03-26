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

using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances | WoWContext.Normal)]
        public Composite CreateFireMageCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateMoveToAndFace(40f, ret => Me.CurrentTarget),
				CreateWaitForCast(true),
                CreateSpellCast("Evocation", ret => Me.ManaPercent < 40),
				//Armour swaping for low or high mana when evo on cd
				CreateSpellBuffOnSelf("Molten Armor", ret => !Me.HasAura("Molten Armor") && Me.ManaPercent > 50),
				CreateSpellBuffOnSelf("Mage Armor", ret => !Me.HasAura("Mage Armor") && Me.ManaPercent < 30),
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
        [Context(WoWContext.Instances | WoWContext.Normal)]
        public Composite CreateFireMagePull()
        {
            return
                new PrioritySelector(
                    // Make sure we're in range, and facing the damned target. (LOS check as well)
                    CreateMoveToAndFace(40f, ret => Me.CurrentTarget),
					CreateWaitForCast(true),
                    CreateSpellCast("Fireball")
                    );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.Instances | WoWContext.Normal)]
        public Composite CreateFireMagePreCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellBuffOnSelf(
                        "Arcane Brilliance",
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