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
                CreateSpellBuffOnSelf("Demon Armor", ret => !SpellManager.HasSpell("Fel Armor")),
                CreateSpellBuffOnSelf("Fel Armor"),
                CreateSpellBuffOnSelf("Soul Link"),

                new ActionLogMessage(false, "Checking for pet"),
                new Decorator(
                    ret => !Me.GotAlivePet,
                    new Action(ret => PetManager.CallPet(WantedPet)))
                );
        }
    }
}
