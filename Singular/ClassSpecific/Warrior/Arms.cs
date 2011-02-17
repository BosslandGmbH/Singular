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

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Warrior)]
        [Spec(TalentSpec.ArmsWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        public Composite CreateArmsWarriorCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateRangeAndFace(4f, ret => Me.CurrentTarget),
                new Decorator(
                    ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 6) > 2,
                    new PrioritySelector(
                        CreateSpellBuff("Rend"),
                        CreateSpellCast("Thunderclap"),
                        CreateSpellCast("Sweeping Strikes"),
                        CreateSpellCast("Bladestorm"),
                        CreateSpellCast("Cleave"),
                        CreateSpellCast("Whirlwind")
                        )),
                CreateSpellCast("Rend", ret => HasAuraStacks("Overpower", 1)),
                CreateSpellCast("Collossus Smash"),
                CreateSpellCast("Execute", ret => Me.CurrentTarget.HealthPercent < 20),
                CreateSpellCast("Overpower"),
                CreateSpellCast("Mortal Strike"),
                CreateSpellCast("Slam"),
                CreateSpellCast("Heroic Strike", ret => Me.RagePercent > 60)
                );
        }
    }
}