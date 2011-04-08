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
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.BloodDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateBloodDeathKnightCombat()
        {
            NeedTankTargeting = true;
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateAutoAttack(true),
                CreateFaceUnit(),
                // Blood DKs are tanks. NOT DPS. If you're DPSing as blood, go respec right now, because you fail hard.
                // Death Grip is used at all times in this spec, so don't bother with an instance check, like the other 2 specs.
                CreateSpellCast("Death Grip", ret => Me.CurrentTarget.Distance > 15),
                //Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateMoveToAndFace(5f, ret => Me.CurrentTarget),
                CreateSpellBuffOnSelf("Bone Shield"),
                CreateSpellCast("Rune Strike"),
                CreateSpellCast("Mind Freeze", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
                CreateSpellCast("Strangulate", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
                CreateSpellBuffOnSelf("Rune Tap", ret => Me.HealthPercent <= 60),
                CreateSpellCast(
                    "Pestilence", ret => Me.CurrentTarget.HasAura("Blood Plague") && Me.CurrentTarget.HasAura("Frost Fever") &&
                                         (from add in NearbyUnfriendlyUnits
                                          where !add.HasAura("Blood Plague") && !add.HasAura("Frost Fever") && add.Distance < 10
                                          select add).Count() > 0),
                new Decorator(
                    ret => SpellManager.CanCast("Death and Decay") && NearbyUnfriendlyUnits.Count(a => a.Distance < 8) > 1,
                    new Action(
                        ret =>
                            {
                                SpellManager.Cast("Death and Decay");
                                LegacySpellManager.ClickRemoteLocation(Me.CurrentTarget.Location);
                            })),
                CreateSpellCast("Icy Touch", ret => !Me.CurrentTarget.HasAura("Frost Fever")),
                CreateSpellCast("Plague Strike", ret => !Me.CurrentTarget.HasAura("Blood Plague")),
				CreateSpellCast("Death Strike"),
                CreateSpellCast("Blood Boil", ret => NearbyUnfriendlyUnits.Count(a => a.Distance < 8) > 1),
                CreateSpellCast("Heart Strike"),
                CreateSpellCast("Death Coil")
				);
        }
        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.BloodDeathKnight)]
        [Context(WoWContext.Instances)]
        [Behavior(BehaviorType.CombatBuffs)]
        public Composite CreateBloodTankTaunts()
        {
            return new PrioritySelector(
                CreateSpellCast("Dark Command", ret => TankTargeting.Instance.NeedToTaunt.Count != 0, ret => TankTargeting.Instance.NeedToTaunt.FirstOrDefault()),
                CreateSpellCast("Death Grip", ret => TankTargeting.Instance.NeedToTaunt.Count != 0, ret => TankTargeting.Instance.NeedToTaunt.FirstOrDefault())
                );
        }
    }		
}