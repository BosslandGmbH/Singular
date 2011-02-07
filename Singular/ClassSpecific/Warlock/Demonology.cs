using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Warlock), Spec(TalentSpec.DemonologyWarlock), Context(WoWContext.All), Behavior(BehaviorType.Combat), Behavior(BehaviorType.Pull)]
        public Composite CreateDemonologyCombat()
        {
            WantedPet = "Felguard";

            return new PrioritySelector(
                CreateRangeAndFace(35f, ret => Me.CurrentTarget),
                CreateAutoAttack(true),
                CreateWaitForCast(),

                CreateSpellBuffOnSelf("Soulburn"),

                new Decorator(
                    ret => Me.CurrentTarget.Fleeing,
                    CreateCastPetAction(PetAction.AxeToss, true)),

                new Decorator(
                    ret => CurrentTargetIsEliteOrBoss,
                    new PrioritySelector(
                        CreateSpellBuffOnSelf("Metamorphosis"),
                        CreateSpellBuffOnSelf("Demon Soul"),
                        CreateSpellCast("Immolation Aura", ret => Me.CurrentTarget.Distance < 5f),
                        CreateSpellCast("Shadowflame", ret => Me.CurrentTarget.Distance < 5)
                        )),

                CreateSpellBuff("Immolate", ret=> LastSpellCast != "Immolate"),
                CreateSpellBuff("Bane of Doom", ret => CurrentTargetIsEliteOrBoss),
                CreateSpellBuff("Bane of Agony", ret => !Me.CurrentTarget.HasAura("Bane of Doom")),
                CreateSpellBuff("Corruption"),
                CreateSpellCast("Handl of Gul'dan"),

                // TODO: Make this cast Soulburn if it's available
                CreateSpellCast("Soul Fire", ret => Me.HasAura("Improved Soul Fire")),

                CreateSpellCast("Soul Fire", ret => Me.HasAura("Decimate")),
                CreateSpellCast("Incinerate", ret => Me.HasAura("Molten Core")),
                CreateSpellCast("Life Tap", ret => Me.ManaPercent < 50 && Me.HealthPercent > 70),
                CreateSpellCast("Shadow Bolt")

                );
        }
    }
}
