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
using Singular.Settings;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.DisciplineHealingPriest)]
        [Spec(TalentSpec.DisciplinePriest)]
        [Spec(TalentSpec.HolyPriest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreatePriestPreCombatBuffs()
        {
            return new PrioritySelector(
                CreateSpellBuffOnSelf("Power Word: Fortitude", ret => NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && CanCastFortitudeOn(u))),
                CreateSpellBuffOnSelf("Shadow Protection", ret => SingularSettings.Instance.Priest.UseShadowProtection && NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && !HasAuraStacks("Shadow Protection", 0))),
                CreateSpellBuffOnSelf("Inner Fire", ret => SingularSettings.Instance.Priest.UseInnerFire),
                CreateSpellBuffOnSelf("Inner Will", ret => !SingularSettings.Instance.Priest.UseInnerFire),
                CreateSpellBuffOnSelf("Fear Ward", ret => SingularSettings.Instance.Priest.UseFearWard),
                CreateSpellBuffOnSelf("Shadowform"),
                CreateSpellBuffOnSelf("Vampiric Embrace")
                );
        }

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.DisciplineHealingPriest)]
        [Spec(TalentSpec.DisciplinePriest)]
        [Spec(TalentSpec.HolyPriest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreatePriestCombatBuffs()
        {
            return new PrioritySelector(
                CreateUsePotionAndHealthstone(10, 10),
                CreateSpellBuffOnSelf("Shadowform"),
                CreateSpellBuffOnSelf("Vampiric Embrace")
                );
        }

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.DisciplineHealingPriest)]
        [Spec(TalentSpec.DisciplinePriest)]
        [Spec(TalentSpec.HolyPriest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        [Priority(999)]
        public Composite CreatePriestCommonCombatSpells()
        {
            return new PrioritySelector(
                    // use our shadowfiend if we're in combat (so not on a pull or resting), if we're below the mana threshold, and our target is okydoky
                    CreateSpellCast("Shadowfiend",
                        ret => Me.Combat && Me.ManaPercent <= SingularSettings.Instance.Priest.ShadowfiendMana &&
                               (Me.CurrentTarget.HealthPercent > 60 || NearbyUnfriendlyUnits.Count(u => u.Aggro) > 1)),
                    // use hymn of hope if we're shielded or no one is targetting us
                    CreateSpellCast("Hymn of Hope", ret => Me.ManaPercent < SingularSettings.Instance.Priest.HymnofHopeMana && (NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) <= 0 || HasAuraStacks("Power Word: Shield", 0))),
                    // use archangel if we can
                    CreateSpellBuff("Archangel", ret => (HasAuraStacks("Dark Evangelism", 5) || HasAuraStacks("Evangelism", 5)) && Me.ManaPercent <= SingularSettings.Instance.Priest.ArchangelMana),
                    // cast psychic scream if it's on
                    CreateSpellCast("Psychic Scream", ret => SingularSettings.Instance.Priest.UsePsychicScream && NearbyUnfriendlyUnits.Count(unit => unit.Aggro && unit.Distance <= 8) >= SingularSettings.Instance.Priest.PsychicScreamAddCount)
                );
        }

        public bool CanCastFortitudeOn(WoWUnit unit)
        {
            //return !unit.HasAura("Blood Pact") &&
            return !unit.HasAura("Power Word: Fortitude") &&
                   !unit.HasAura("Qiraji Fortitude") &&
                   !unit.HasAura("Commanding Shout");
        }
    }
}