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

using TreeSharp;
using Singular.Settings;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateShadowPriestCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateMoveToAndFace(30, ret => Me.CurrentTarget),
                CreateWaitForCast(true),
                CreateDiscHealOnlyBehavior(true),
                CreateSpellBuffOnSelf("Shadowform"),
                CreateSpellCast("Shadowfiend",
                    ret => Me.Combat && Me.ManaPercent <= SingularSettings.Instance.Priest.ShadowfiendMana &&
                           (Me.CurrentTarget.HealthPercent > 60 || NearbyUnfriendlyUnits.Count(u => u.Aggro) > 1)),
                CreateSpellCast("Mind Blast", ret => Me.CurrentTarget.CreatureType == Styx.WoWCreatureType.Totem),
                CreateSpellBuff("Vampiric Touch", true),
                CreateSpellBuff("Devouring Plague"),
                CreateSpellBuff("Shadow Word: Pain"),
                CreateSpellBuff("Archangel", ret => HasAuraStacks("Evangelism", 5) && Me.ManaPercent <= 75),
                CreateSpellCast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent < 25),
                CreateSpellCast("Shadow Fiend", ret => Me.ManaPercent < 50),
                CreateSpellCast("Mind Blast", ret => Me.HasAura("Shadow Orb") && !Me.HasAura("Empowered Shadow")),
                CreateSpellCast("Mind Flay", ret => !Me.IsMoving)
                );
        }

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.PullBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateShadowPriestPullBuffs()
        {
            return new PrioritySelector(
                CreateSpellBuffOnSelf("Power Word: Shield", ret=> !HasAuraStacks("Weakened Soul", 1))
                );
        }
    }
}