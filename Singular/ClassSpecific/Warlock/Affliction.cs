using System.Linq;
using Singular.Settings;
using TreeSharp;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular
{
    partial class SingularRoutine
    {

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateAfflictionCombat()
        {
            WantedPet = "Succubus";

            return new PrioritySelector(
                CreateEnsureTarget(),
                //CreateLosAndFace(ret => Me.CurrentTarget),
                CreateMoveToAndFace(35f, ret => Me.CurrentTarget),
                CreateWaitForCast(),
                CreateAutoAttack(true),

                // Emergencies
                new Decorator(
                    ret => Me.HealthPercent < 20,
                    new PrioritySelector(
                //CreateSpellBuff("Fear", ret => !Me.CurrentTarget.HasAura("Fear")),
                        CreateSpellCast("Howl of Terror", ret => Me.CurrentTarget.Distance < 10 && Me.CurrentTarget.IsPlayer),
                        CreateSpellCast("Death Coil", ret => !Me.CurrentTarget.HasAura("Howl of Terror") && !Me.CurrentTarget.HasAura("Fear")),
                        CreateSpellBuffOnSelf("Soulburn", ret => Me.CurrentSoulShards > 0),
                        CreateSpellCast("Drain Life")
                        )),

                CreateSpellCast("Life Tap", ret => Me.ManaPercent < 10),
                CreateSpellCast("Health Funnel", ret => Me.GotAlivePet && Me.Pet.HealthPercent < 30),

                // Finishing sequence
                CreateSpellCast("Soul Swap", ret => !Me.HasAura("Soul Swap") && Me.CurrentTarget.HealthPercent < 10 && Me.CurrentTarget.HasAura("Unstable Affliction") && !CurrentTargetIsEliteOrBoss),
                CreateSpellCast("Drain Soul", ret => Me.CurrentTarget.HealthPercent < 10),

                // Elites
                new Decorator(
                    ret => CurrentTargetIsEliteOrBoss,
                    new PrioritySelector(
                        CreateSpellBuffOnSelf("Demon Soul"),
                        CreateSpellBuff("Curse of Elements", ret => !Me.CurrentTarget.HasAura("Curse of Elements")),
                        new Decorator(
                            ret => SpellManager.CanCast("Summon Infernal"),
                            new Action(
                                ret =>
                                {
                                    SpellManager.Cast("Summon Infernal");
                                    LegacySpellManager.ClickRemoteLocation(Me.CurrentTarget.Location);
                                }))
                        )),

                // AoE
                new Decorator(
                    ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 15) >= 5,
                    new PrioritySelector(
                        CreateSpellBuffOnSelf("Demon Soul"),
                        CreateSpellBuffOnSelf("Soulburn", ret => !Me.CurrentTarget.HasAura("Seed of Corruption") && Me.CurrentSoulShards > 0 && TalentManager.GetCount(1, 15) == 1),
                        CreateSpellBuff("Seed of Corruption", ret => !Me.CurrentTarget.HasAura("Seed of Corruption"))
                        )),

                // Standard Nuking
                CreateSpellCast("Shadow Bolt", ret => Me.HasAura("Shadow Trance")),
                CreateSpellBuff("Haunt"),
                CreateSpellCast("Soul Swap", ret => Me.HasAura("Soul Swap") && Me.CurrentTarget.HealthPercent > 10),
                CreateSpellBuff("Bane of Doom", ret => CurrentTargetIsEliteOrBoss && !Me.CurrentTarget.HasAura("Bane of Doom")),
                CreateSpellBuff("Bane of Agony", ret => !Me.CurrentTarget.HasAura("Bane of Agony") && !Me.CurrentTarget.HasAura("Bane of Doom")),
                CreateSpellBuff("Corruption", ret => !Me.CurrentTarget.HasAura("Corruption") && !Me.CurrentTarget.HasAura("Seed of Corruption")),
                CreateSpellBuff("Unstable Affliction", ret => !Me.CurrentTarget.HasAura("Unstable Affliction")),
                CreateSpellCast("Drain Soul", ret => Me.CurrentTarget.HealthPercent < 25),
                CreateSpellCast("Shadowflame", ret => Me.CurrentTarget.Distance < 5),
                CreateSpellBuffOnSelf("Demon Soul"),
                CreateSpellBuff("Curse of Weakness", ret => Me.CurrentTarget.IsPlayer && !Me.CurrentTarget.HasAura("Curse of Weakness")),
                CreateSpellCast("Life Tap", ret => Me.ManaPercent < 50 && Me.HealthPercent > 70),
                CreateSpellCast("Drain Life", ret => Me.HealthPercent < 70),
                CreateSpellCast("Health Funnel", ret => Me.GotAlivePet && Me.Pet.HealthPercent < 70),
                CreateSpellCast("Shadow Bolt")
                );
        }
    }
}