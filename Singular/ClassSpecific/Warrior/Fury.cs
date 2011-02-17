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
        [Spec(TalentSpec.FuryWarrior)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        public Composite CreateWarriorFuryCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateRangeAndFace(4f, ret => Me.CurrentTarget),
                new Decorator(
                    ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 6) > 2,
                    new PrioritySelector(
                        CreateSpellCast("Cleave"),
                        CreateSpellCast("Whirlwind"),
                        CreateSpellCast("Bloodthirst")
                        )),
                CreateSpellCast("Collossus Smash"),
                CreateSpellCast("Execute", ret => Me.CurrentTarget.HealthPercent < 20),
                CreateSpellCast("Bloodthirst"),
                CreateSpellCast("Raging Blow"),
                CreateSpellCast("Slam", ret => HasAuraStacks("Blood Surge", 1)),
                CreateSpellCast("Heroic Strike", ret => Me.RagePercent > 60)
                );
        }
    }
}