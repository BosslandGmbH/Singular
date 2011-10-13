using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Lists;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;
using TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public class FeralCat
    {

        [Spec(TalentSpec.FeralDruid)]
        //[Spec(TalentSpec.FeralTankDruid)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Druid)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateFeralCatCombat()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift != ShapeshiftForm.Cat,
                    Spell.BuffSelf("Cat Form")),

                // Ensure we're facing the target. Kthx.
                Movement.CreateFaceTargetBehavior(),
                //Spell.Cast("Faerie Fire (Feral)", ret=>!Unit.HasAura(StyxWoW.Me.CurrentTarget, "Faerie Fire", 3)),
                //new Action(ret=>Logger.WriteDebug("Done with FF Going into boss check")),
                // 
                Spell.Cast(
                    "Faerie Fire (Feral)", ret => !StyxWoW.Me.CurrentTarget.HasSunders() && !StyxWoW.Me.CurrentTarget.HasAura("Faerie Fire", 3)),
                Singular.Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Throw out swipe if we have more than 2 mobs to DPS.
                Spell.Cast("Swipe", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) > 2),

                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsBoss(),
                    new PrioritySelector(
                        Spell.Buff("Mangle (Cat)"),
                        Spell.Cast("Tiger's Fury"),
                        Spell.Cast("Berserk"),
                        Spell.Cast("Shred", ret => StyxWoW.Me.HasAura("Omen of Clarity")),
                        Spell.Buff("Rip", ret => StyxWoW.Me.ComboPoints == 5),
                        Spell.Buff("Rake"),
                        Spell.BuffSelf("Savage Roar", ret => StyxWoW.Me.ComboPoints == 5),
                        Spell.Cast("Shred", ret => StyxWoW.Me.CurrentTarget.MeIsSafelyBehind),
                        Spell.Cast("Mangle", ret => !StyxWoW.Me.CurrentTarget.MeIsSafelyBehind),
                        // Here's how this works. If the mob is a boss, try and get behind it. If we *can't*
                        // get behind it, we should try to move to it. Its really that simple.
                        Movement.CreateMoveBehindTargetBehavior(3f),
                        Movement.CreateMoveToMeleeBehavior(true))),

                new Decorator(
                    ret => !StyxWoW.Me.CurrentTarget.IsBoss(),
                    new PrioritySelector(
                        // For normal 'around town' grinding, this is all we really need.
                        Spell.Cast("Ferocious Bite", ret => StyxWoW.Me.ComboPoints >= 4),
                        Spell.Cast("Berserk"),
                        Spell.Cast("Tiger's Fury"),
                        // Get rake up just because we can.
                        Spell.Buff("Rake"),
                        Spell.Cast("Mangle (Cat)"))),

                // Since we can't get 'behind' mobs, just do this, kaythanks
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}
