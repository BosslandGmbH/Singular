using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Singular.ClassSpecific.Priest
{
    public class Common
    {
        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.Any)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreatePriestPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.Buff("Power Word: Fortitude", ret => Unit.NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && CanCastFortitudeOn(u))),
                Spell.Buff("Shadow Protection", ret => SingularSettings.Instance.Priest.UseShadowProtection && Unit.NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && !Unit.HasAura(u, "Shadow Protection"))),
                Spell.Buff("Inner Fire", ret => SingularSettings.Instance.Priest.UseInnerFire),
                Spell.Buff("Inner Will", ret => !SingularSettings.Instance.Priest.UseInnerFire),
                Spell.Buff("Fear Ward", ret => SingularSettings.Instance.Priest.UseFearWard),
                Spell.Buff("Shadowform"),
                Spell.Buff("Vampiric Embrace")
                );
        }

        private static bool CanCastFortitudeOn(WoWUnit unit)
        {
            //return !unit.HasAura("Blood Pact") && // who cares about this, really
            return !unit.HasAura("Power Word: Fortitude") &&
                   !unit.HasAura("Qiraji Fortitude");
                   //!unit.HasAura("Commanding Shout"); // does this really matter ?
        }
    }
}
