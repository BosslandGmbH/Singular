using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Singular.Settings;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Lowbie
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static DeathKnightSettings DeathKnightSettings => SingularSettings.Instance.DeathKnight();

	    // Note:  in MOP we would only have Lowbie Death Knights if user doesn't select a spec when character
        // is created.  Previously, you had to complete some quests to get talent points which then
        // determined your spec, but that is no longer necessary

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, (WoWSpec)0)]
        public static Composite CreateLowbieDeathKnightCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),

                        // Anti-magic shell - no cost and doesnt trigger GCD
                        Spell.BuffSelf("Anti-Magic Shell", ret => Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == StyxWoW.Me.Guid)),

                        Spell.BuffSelf("Raise Dead", req => PetManager.IsPetSummonAllowed && !Me.Mounted && !Me.OnTaxi),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Spell.Cast("Death Strike", ret =>
                                (Me.HasActiveAura("Dark Succor") && Me.HealthPercent <= DeathKnightSettings.DeathStrikeSuccorPercent)
                                || Me.HealthPercent <= DeathKnightSettings.DeathStrikePercent
                        ),
                        Common.CreateDeathGripBehavior(),

                        Spell.CastOnGround("Death and Decay",
                            on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f).Location,
                            ret => Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() > DeathKnightSettings.DeathAndDecayCount),

                        Spell.Cast("Outbreak", ret => Me.CurrentTarget.GetAuraTimeLeft("Virulent Plague").TotalSeconds < 1.8),
                        Spell.Cast("Festering Strike", ret => Me.CurrentTarget.GetAuraStacks("Festering Wound") < 5),
                        Spell.Cast("Scourge Strike", ret => Me.CurrentTarget.GetAuraStacks("Festering Wound") >= 1),
                        Spell.Cast("Death Coil", req => Common.RunicPowerDeficit < 10 || Me.HasActiveAura("Sudden Doom"))
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}
