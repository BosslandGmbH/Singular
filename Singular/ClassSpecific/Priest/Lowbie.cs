using System.Collections.Generic;
using System.Linq;

using Singular.Settings;

using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using System;

using Action = TreeSharp.Action;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateLowbiePriestCombat()
        {
           return new PrioritySelector(
                CreateDiscHealOnlyBehavior(),
                CreateEnsureTarget(),
                CreateMoveToAndFace(28f, ret => Me.CurrentTarget),
                CreateWaitForCast(),
                CreateSpellBuffOnSelf("Arcane Torrent", ret => NearbyUnfriendlyUnits.Any(u => u.IsCasting && u.DistanceSqr < 8 * 8)),
                CreateSpellBuff("Shadow Word: Pain"),
                CreateSpellCast("Mind Blast"),
                CreateSpellCast("Smite"),
                CreateFireRangedWeapon()
                );
        }
    }
}