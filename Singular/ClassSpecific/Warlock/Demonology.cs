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
using Styx;
using System.Threading;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.DemonologyWarlock)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateDemonologyCombat()
        {
            WantedPet = "Felguard";
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateMoveToAndFace(35f, ret => Me.CurrentTarget),
                CreateWaitForCast(true),
                CreateAutoAttack(true),
                CreateSpellBuffOnSelf("Soulburn", ret => SpellManager.HasSpell("Soul Fire") || Me.HealthPercent < 70),
                CreateSpellCast("Life Tap", ret => Me.ManaPercent < 50 && Me.HealthPercent > 70),
                new Decorator(
                    ret => Me.CurrentTarget.Fleeing,
                    CreateCastPetAction("Axe Toss", true)),
                new Decorator(
                    ret => CurrentTargetIsElite,
                    new PrioritySelector(
                        CreateSpellBuffOnSelf("Metamorphosis"),
                        CreateSpellBuffOnSelf("Demon Soul"),
                        CreateSpellCast("Immolation Aura", ret => Me.CurrentTarget.Distance < 5f),
                        CreateSpellCast("Shadowflame", ret => Me.CurrentTarget.Distance < 5)
                        )),
                CreateSpellBuff("Immolate", true),
                CreateSpellBuff("Bane of Doom", ret => CurrentTargetIsElite),
                CreateSpellBuff("Bane of Agony", ret => !Me.CurrentTarget.HasAura("Bane of Doom")),
                CreateSpellBuff("Corruption"),
                CreateSpellCast("Drain Life", ret => Me.HealthPercent < 70),
                CreateSpellCast("Health Funnel", ret => Me.GotAlivePet && Me.Pet.HealthPercent < 70),
                CreateSpellCast("Hand of Gul'dan"),
                // TODO: Make this cast Soulburn if it's available
                CreateSpellCast("Soul Fire", ret => Me.HasAura("Improved Soul Fire") || Me.HasAura("Soulburn")),
                CreateSpellCast("Soul Fire", ret => Me.HasAura("Decimation")),
                CreateSpellCast("Incinerate", ret => Me.HasAura("Molten Core")),
                CreateSpellCast("Shadow Bolt")
                );
        }
    }
}