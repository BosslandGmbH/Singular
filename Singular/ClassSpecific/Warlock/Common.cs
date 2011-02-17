using Singular.Composites;

using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        public string WantedPet { get; set; }

        [Class(WoWClass.Warlock), 
        Spec(TalentSpec.AfflictionWarlock), 
        Spec(TalentSpec.DemonologyWarlock), 
        Spec(TalentSpec.DestructionWarlock), 
        Behavior(BehaviorType.PreCombatBuffs),
        Context(WoWContext.All)]
        public Composite CreateWarlockBuffs()
        {
            return new PrioritySelector(
                CreateWaitForCast(),
                CreateSpellBuffOnSelf("Demon Armor", ret => !Me.HasAura("Demon Armor") && !SpellManager.HasSpell("Fel Armor")),
                CreateSpellBuffOnSelf("Fel Armor",  ret => !Me.HasAura("Fel Armor")),
				
                // Soul Link is considered "passive". It has no duration (thus, no end-time)
                // We have to check it this way, or we'll keep casting it over and over and over
                CreateSpellBuffOnSelf("Soul Link", ret => !Me.HasAura("Soul Link") && Me.GotAlivePet),

                CreateSpellCast("Soul Harvest", ret=> Me.CurrentSoulShards < 2),

                //new ActionLogMessage(false, "Checking for pet"),
                new Decorator(
                    ret => !Me.GotAlivePet,
                    new Action(ret => PetManager.CallPet(WantedPet)))
                );
        }
    }
}
