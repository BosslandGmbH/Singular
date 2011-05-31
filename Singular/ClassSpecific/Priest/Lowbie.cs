﻿using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx.Combat.CombatRoutine;
using TreeSharp;

namespace Singular.ClassSpecific.Priest
{
    public class Lowbie
    {
        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbiePriestCombat()
        {
            return new PrioritySelector(
                Discipline.CreateDiscHealOnlyBehavior(),
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.BuffSelf("Arcane Torrent", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.IsCasting && u.DistanceSqr < 8 * 8)),
                Spell.Buff("Shadow Word: Pain"),
                Spell.Cast("Mind Blast"),
                Spell.Cast("Smite"),
                Helpers.Common.CreateUseWand(),
                Movement.CreateMoveToTargetBehavior(true, 25f)
                );
        }
    }
}
