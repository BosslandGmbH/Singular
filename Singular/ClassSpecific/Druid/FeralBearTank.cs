using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public class FeralBearTank
    {
        [Spec(TalentSpec.FeralTankDruid)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        [Class(WoWClass.Druid)]
        public static Composite CreateBearTankCombat()
        {
            TankManager.NeedTankTargeting = true;
            return new PrioritySelector(
                ctx => TankManager.Instance.FirstUnit ?? StyxWoW.Me.CurrentTarget,
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift != ShapeshiftForm.Bear,
                    Spell.BuffSelf("Bear Form")),

                Movement.CreateFaceTargetBehavior(),

                // Defensive CDs are hard to 'roll' from this type of logic, so we'll simply use them more as 'oh shit' buttons, than anything.
                // Barkskin should be kept on CD, regardless of what we're tanking
                Spell.BuffSelf("Barkskin"),

                // Since Enrage no longer makes us take additional damage, just keep it on CD. Its a rage boost, and coupled with King of the Jungle, a DPS boost for more threat.
                Spell.BuffSelf("Enrage"),

                // Only pop SI if we're taking a bunch of damage.
                Spell.BuffSelf("Survival Instincts", ret => StyxWoW.Me.HealthPercent < 55),
                // We only want to pop FR < 30%. Users should not be able to change this value, as FR automatically pushes us to 30% hp.
                Spell.BuffSelf("Frenzied Regeneration", ret => StyxWoW.Me.HealthPercent < 30),

                // Make sure we deal with interrupts...
                //Spell.Cast(80964 /*"Skull Bash (Bear)"*/, ret => (WoWUnit)ret, ret => ((WoWUnit)ret).IsCasting),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                new Decorator(
                    ret => Targeting.GetAggroOnMeWithin(StyxWoW.Me.Location, 15f) > 1,
                    new PrioritySelector(
                        Spell.Cast("Thrash"),
                        Spell.Cast("Swipe (Bear)"),
                        Spell.Cast("Maul")
                        )),

                // If we have 3+ units not targeting us, and are within 10yds, then pop our AOE taunt. (These are ones we have 'no' threat on, or don't hold solid threat on)
                Spell.Cast("Challenging Roar", ret => TankManager.Instance.NeedToTaunt.First(), ret => TankManager.Instance.NeedToTaunt.Count(u => u.Distance <= 10) >= 3),
                // If there's a unit that needs taunting, do it.
                Spell.Cast("Growl", ret => TankManager.Instance.NeedToTaunt.First(), ret => TankManager.Instance.NeedToTaunt.FirstOrDefault() != null),

                Spell.Cast("Pulverize", ret => ((WoWUnit)ret).HasAura("Lacerate", 3)),

                Spell.Cast("Demoralizing Roar", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 10 && !u.HasAnyAura("Demoralizing Roar", "Demoralizing Shout"))),
                Spell.Cast("Faerie Fire (Feral)", ret => !StyxWoW.Me.CurrentTarget.HasAura("Faerie Fire", 3)),

                Spell.Cast("Mangle (Bear)"),
                // Maul is our rage dump... don't pop it unless we have to, or we still have > 2 targets.
                Spell.Cast("Maul", ret => StyxWoW.Me.RagePercent > 60 || (Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 6) >= 2 && TalentManager.HasGlyph("Maul"))),
                Spell.Cast("Thrash"),
                Spell.Cast("Lacerate")

                );
        }


        // Quick wrapper to make some logic for non-instances. Bear form really sucks outside of them!

        //[Spec(TalentSpec.FeralTankDruid)]
        //[Behavior(BehaviorType.Combat)]
        //[Behavior(BehaviorType.Pull)]
        //[Class(WoWClass.Druid)]
        //[Priority(500)]
        //[Context(WoWContext.Normal | WoWContext.Battlegrounds)]
        //public static Composite CreateFeralCatInstanceCombat()
        //{
        //    return FeralCat.CreateFeralCatCombat();
        //}
    }
}
