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

using Styx;
using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Spec(TalentSpec.FeralTankDruid)]
        [Class(WoWClass.Druid)]
        [Context(WoWContext.Instances)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateBearTankCombat()
        {
            NeedTankTargeting = true;
            WantedDruidForm = ShapeshiftForm.Bear;
            return new PrioritySelector(
                CreateEnsureTarget(),
                new Decorator(
                    ret => Me.Shapeshift != WantedDruidForm,
                    CreateSpellCast("Bear Form")),
                // Can we charge at the unit? If so... do it
                new Decorator(
                    ret => SingularSettings.Instance.Druid.UseFeralChargeBear && Me.CurrentTarget.Distance > 8f && Me.CurrentTarget.Distance < 25f,
                    CreateSpellCast("Feral Charge (Bear)")),
                CreateMoveToAndFace(4.5f, ret => Me.CurrentTarget),
                CreateAutoAttack(false),
                CreateSpellBuffOnSelf("Barkskin"),
                CreateSpellBuffOnSelf("Survival Instincts", ret => Me.HealthPercent < 60),
                CreateSpellBuffOnSelf("Frenzied Regeneration", ret => Me.HealthPercent < 30),
                CreateSpellCast("Skull Bash (Bear)", ret => Me.CurrentTarget.IsCasting),

                CreateSpellCast("Berserk", ret => CurrentTargetIsBoss),

                new Decorator(
                    ret => NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8 * 8) > 1,
                    new PrioritySelector(
                        CreateSpellBuffOnSelf("Berserk"),
                        CreateSpellCast("Swipe (Bear)")
                        )),

                CreateSpellCast("Maul", ret => Me.RagePercent >= 50),
                CreateSpellCast("Mangle (Bear)"),
                CreateSpellBuff("Demoralizing Roar"),
                // Thrash is a giant help in threat gen, so lets keep it on CD. Kthx.
                CreateSpellBuff("Thrash"),
                CreateSpellCast("Lacerate", ret => !HasAuraStacks("Lacerate", 3, Me.CurrentTarget)),
                CreateSpellCast(
                    "Pulverize", ret => HasAuraStacks("Lacerate", 3, Me.CurrentTarget) && GetAuraTimeLeft("Pulverize", Me, true).TotalSeconds < 3),
                CreateSpellCast("Faerie Fire (Feral)"),
                // Lacerate is a filler, kthx.
                CreateSpellCast("Lacerate")

                );
        }

        [Spec(TalentSpec.FeralTankDruid)]
        [Class(WoWClass.Druid)]
        [Context(WoWContext.Instances)]
        [Behavior(BehaviorType.CombatBuffs)]
        public Composite CreateBearTankCombatBuffs()
        {
            return new PrioritySelector(
                CreateSpellCast(
                    "Growl", ret => TankTargeting.Instance.NeedToTaunt.Count != 0, ret => TankTargeting.Instance.NeedToTaunt.FirstOrDefault()),
                CreateSpellCast("Challenging Roar", ret => TankTargeting.Instance.NeedToTaunt.Count(u => u.Distance < 10) > 2)
                );
        }
    }
}