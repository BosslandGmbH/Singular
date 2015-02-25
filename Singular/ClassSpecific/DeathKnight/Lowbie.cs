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
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DeathKnightSettings DeathKnightSettings { get { return SingularSettings.Instance.DeathKnight(); } }

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

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        // Anti-magic shell - no cost and doesnt trigger GCD 
                        Spell.BuffSelf("Anti-Magic Shell", ret => Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == StyxWoW.Me.Guid)),

                        Common.CreateGetOverHereBehavior(),
                        Spell.Cast("Death Coil"),
                        Spell.Buff(
                            "Icy Touch", 
                            3, 
                            on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.IsTargetingMeOrPet && u.NeedsFrostFever() && Me.SpellDistance(u) < 30 && Me.IsSafelyFacing(u) && u.InLineOfSpellSight), 
                            req => true, 
                            true, 
                            "Frost Fever"
                            ),
                        Spell.Buff(
                            "Plague Strike", 
                            3, 
                            on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.IsTargetingMeOrPet && u.NeedsBloodPlague() && u.IsWithinMeleeRange && Me.IsSafelyFacing(u) && u.InLineOfSpellSight), 
                            req => true, 
                            true, 
                            "Blood Plague"
                            ),
                        Spell.Buff("Icy Touch"),
                        Spell.Buff("Plague Strike")
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}
