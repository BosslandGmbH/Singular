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
                CreateSpellBuffOnSelf("Soul Link", ret => !Me.HasAura("Soul Link") && Me.GotAlivePet),
                new Decorator(
                    ret => !Me.GotAlivePet,
                    new Action(ret => PetManager.CallPet(WantedPet))),
                CreateSpellCast("Health Funnel", ret => Me.GotAlivePet && Me.Pet.HealthPercent < 30 && Me.HealthPercent > 20)
                );
        }

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Spec(TalentSpec.DemonologyWarlock)]
        [Spec(TalentSpec.DestructionWarlock)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public Composite CreateWarlockRest()
        {
            return new PrioritySelector(
                CreateSpellCast("Life Tap", ret => Me.ManaPercent < 90 && Me.HealthPercent > 20),
                CreateSpellCast("Soul Harvest", ret => !Me.Combat && (Me.CurrentSoulShards < 2 || Me.HealthPercent <= 55)),
                CreateDefaultRestComposite(40, 0)
                );
        }
    }
}