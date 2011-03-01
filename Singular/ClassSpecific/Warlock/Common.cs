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
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        public string WantedPet { get; set; }

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Spec(TalentSpec.DemonologyWarlock)]
        [Spec(TalentSpec.DestructionWarlock)]
		[Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateWarlockBuffs()
        {
			return new PrioritySelector(
				new Decorator(
					ctx => Me.CastingSpell != null && Me.CastingSpell.Name == "Summon " + WantedPet && Me.GotAlivePet,
					new Action(ctx => SpellManager.StopCasting())),

				CreateWaitForCast(),
				CreateSpellBuffOnSelf("Demon Armor", ret => !Me.HasAura("Demon Armor") && !SpellManager.HasSpell("Fel Armor")),
				CreateSpellBuffOnSelf("Fel Armor", ret => !Me.HasAura("Fel Armor")),
				// Soul Link is considered "passive". It has no duration (thus, no end-time)
				// We have to check it this way, or we'll keep casting it over and over and over
				CreateSpellBuffOnSelf("Soul Link", ret => !Me.HasAura("Soul Link") && Me.GotAlivePet),
				CreateSpellCast("Soul Harvest", ret => Me.CurrentSoulShards < 2),
				//new ActionLogMessage(false, "Checking for pet"),
				new Decorator(
					ret => !Me.GotAlivePet,
					new Action(ret => PetManager.CallPet(WantedPet)))
					);
        }
    }
}