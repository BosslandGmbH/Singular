using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Singular.ClassSpecific.Priest
{
    public class Common
    {
        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.DisciplineHealingPriest)]
        [Spec(TalentSpec.DisciplinePriest)]
        [Spec(TalentSpec.HolyPriest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreatePriestPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Shadowform"),
                Spell.BuffSelf("Vampiric Embrace"),
                Spell.BuffSelf("Power Word: Fortitude", ret => Unit.NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && CanCastFortitudeOn(u))),
                Spell.BuffSelf("Shadow Protection", ret => SingularSettings.Instance.Priest.UseShadowProtection && Unit.NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && !Unit.HasAura(u, "Shadow Protection", 0))),
                Spell.BuffSelf("Inner Fire", ret => SingularSettings.Instance.Priest.UseInnerFire),
                Spell.BuffSelf("Inner Will", ret => !SingularSettings.Instance.Priest.UseInnerFire),
                Spell.BuffSelf("Fear Ward", ret => SingularSettings.Instance.Priest.UseFearWard)
     
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
        public static Composite CreatePriestCombatBuffs()
        {
            return new PrioritySelector(
                Item.CreateUsePotionAndHealthstone(10, 10),
                Spell.BuffSelf("Shadowform"),
                Spell.BuffSelf("Vampiric Embrace")
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
        public static Composite CreatePriestCommonCombatSpells()
        {
            return new PrioritySelector(
                // use our shadowfiend if we're in combat (so not on a pull or resting), if we're below the mana threshold, and our target is okydoky
                Spell.Cast(
                    "Shadowfiend",
                    ret =>
                    StyxWoW.Me.Combat && StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Priest.ShadowfiendMana &&
                    StyxWoW.Me.CurrentTarget != null &&
                    (StyxWoW.Me.CurrentTarget.HealthPercent > 60 || Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro) > 1)),
                // use hymn of hope if we're shielded or no one is targetting us
                Spell.Cast(
                    "Hymn of Hope",
                    ret =>
                    StyxWoW.Me.ManaPercent < SingularSettings.Instance.Priest.HymnofHopeMana &&
                    (Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) <= 0 || Unit.HasAura(StyxWoW.Me, "Power Word: Shield", 0))),
                // use archangel if we can
                Spell.Buff(
                    "Archangel",
                    ret =>
                    (Unit.HasAura(StyxWoW.Me, "Dark Evangelism", 5) || Unit.HasAura(StyxWoW.Me, "Evangelism", 5)) &&
                    StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Priest.ArchangelMana),
                // cast psychic scream if it's on
                Spell.Cast(
                    "Psychic Scream",
                    ret =>
                    SingularSettings.Instance.Priest.UsePsychicScream && !StyxWoW.Me.IsInInstance &&
                    Unit.NearbyUnfriendlyUnits.Count(unit => unit.Aggro && unit.DistanceSqr <= 8 * 8) >=
                    SingularSettings.Instance.Priest.PsychicScreamAddCount),
                new Decorator(
                    ret => !StyxWoW.Me.Combat,
                    Rest.CreateDefaultRestBehaviour())
                );
        }

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.DisciplineHealingPriest)]
        [Spec(TalentSpec.DisciplinePriest)]
        [Spec(TalentSpec.HolyPriest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PullBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreatePriestCommonPullBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Power Word: Shield", ret => SingularSettings.Instance.Priest.UseShieldPrePull && !Unit.HasAura(StyxWoW.Me, "Weakened Soul", 0))
                );
        }

        private static bool CanCastFortitudeOn(WoWUnit unit)
        {
            //return !unit.HasAura("Blood Pact") &&
            return !unit.HasAura("Power Word: Fortitude") &&
                   !unit.HasAura("Qiraji Fortitude") &&
                   !unit.HasAura("Commanding Shout");
        }
    }
}
