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
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        public string WantedPet { get; set; }

        protected bool NeedToCreateHealthStone
        {
            get
            {
                if (Me.CarriedItems.Any(i => i.ItemSpells.Any(s => s.ActualSpell.Name == "Healthstone")))
                {
                    return false;
                }

                return true;
            }
        }

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Spec(TalentSpec.DemonologyWarlock)]
        [Spec(TalentSpec.DestructionWarlock)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateWarlockPreCombatBuffs()
        {
            return new PrioritySelector(
                CreateWaitForCast(),
                CreateSpellBuffOnSelf("Create Healthstone", ret => NeedToCreateHealthStone),
                CreateSpellBuffOnSelf("Demon Armor", ret => !Me.HasAura("Demon Armor") && !SpellManager.HasSpell("Fel Armor")),
                CreateSpellBuffOnSelf("Fel Armor", ret => !Me.HasAura("Fel Armor")),
                CreateSpellBuffOnSelf("Soul Link", ret => !Me.HasAura("Soul Link") && Me.GotAlivePet),
                new Decorator(
                    ret => !Me.GotAlivePet,
                    new Action(ret => PetManager.CallPet(WantedPet))),
                CreateSpellBuffOnSelf("Health Funnel", ret => Me.GotAlivePet && Me.Pet.HealthPercent < 60 && Me.HealthPercent > 40)
                );
        }

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Spec(TalentSpec.DemonologyWarlock)]
        [Spec(TalentSpec.DestructionWarlock)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateWarlockCombatBuffs()
        {
            return new PrioritySelector(
                CreateUsePotionAndHealthstone(50, 10)
                );
        }

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Spec(TalentSpec.DemonologyWarlock)]
        [Spec(TalentSpec.DestructionWarlock)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public Composite CreateWarlockRest()
        {
            return new PrioritySelector(
                new Decorator(
                    ctx => Me.CastingSpell != null && Me.CastingSpell.Name == "Summon " + WantedPet && Me.GotAlivePet,
                    new Action(ctx => SpellManager.StopCasting())),
                CreateWaitForCast(),
                CreateSpellBuffOnSelf("Life Tap", ret => Me.ManaPercent < 80 && Me.HealthPercent > 40),
                CreateSpellBuffOnSelf("Soul Harvest", ret => Me.CurrentSoulShards < 2 || Me.HealthPercent <= 55),
                CreateDefaultRestComposite(40, 0)
                );
        }
    }
}