using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;

namespace Singular.ClassSpecific.Priest
{
    public class Common
    {
        [Class(WoWClass.Priest)]
        [Spec(WoWSpec.PriestDiscipline)]
        [Spec(WoWSpec.PriestHoly)]
        [Spec(WoWSpec.PriestShadow)]
        [Spec((WoWSpec)0)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreatePriestPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Shadowform"),
                //Spell.BuffSelf("Vampiric Embrace"), // VE is now a CD, not a normal buff
                Spell.BuffSelf("Power Word: Fortitude", ret => Unit.NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && CanCastFortitudeOn(u))),
                //Spell.BuffSelf("Shadow Protection", ret => SingularSettings.Instance.Priest.UseShadowProtection && Unit.NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && !Unit.HasAura(u, "Shadow Protection", 0))), // we no longer have Shadow resist
                Spell.BuffSelf("Inner Fire", ret => SingularSettings.Instance.Priest.UseInnerFire),
                Spell.BuffSelf("Inner Will", ret => !SingularSettings.Instance.Priest.UseInnerFire),
                Spell.BuffSelf("Fear Ward", ret => SingularSettings.Instance.Priest.UseFearWard)
     
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
