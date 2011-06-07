using Singular.Dynamics;
using Singular.Helpers;
using Singular.Lists;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public class FeralCat
    {
        [Spec(TalentSpec.FeralDruid)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Druid)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateFeralCatInstanceCombat()
        {
            return CreateFeralCatCombat();
        }

        [Spec(TalentSpec.FeralTankDruid)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Druid)]
        [Priority(500)]
        [Context(WoWContext.Normal | WoWContext.Battlegrounds)]
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

                // IsCasting has been fixed a few releases ago to also check for channeled spells. Skull Bash is also not provided in the custom wrappers for the spell manager
                // So use it by ID
                Spell.Cast(80965 /*"Skull Bash (Cat)"*/, ret => StyxWoW.Me.CurrentTarget.IsCasting),

                new Decorator(
                    ret => BossList.BossIds.Contains(StyxWoW.Me.CurrentTarget.Entry),
                    new PrioritySelector(
                        Spell.Buff("Mangle (Cat)"),
                        Spell.Cast("Tiger's Fury"),
                        Spell.Cast("Berserk"),
                        Spell.Buff("Rip", ret => StyxWoW.Me.ComboPoints == 5),
                        Spell.Buff("Rake"),
                        Spell.BuffSelf("Savage Roar", ret => StyxWoW.Me.ComboPoints == 5),
                        Spell.Cast("Shred", ret => StyxWoW.Me.CurrentTarget.MeIsSafelyBehind),
                        Spell.Cast("Mangle", ret => !StyxWoW.Me.CurrentTarget.MeIsSafelyBehind),
                        // Here's how this works. If the mob is a boss, try and get behind it. If we *can't*
                        // get behind it, we should try to move to it. Its really that simple.
                        Movement.CreateMoveBehindTargetBehavior(3f),
                        Movement.CreateMoveToTargetBehavior(true, 4f))),

                // TODO: Split all this into multiple combat rotation funcs.
                // TODO: Add targeting helpers (NearbyEnemyUnits)
                // TODO: Add simple wrappers for elites, bosses, etc

                // For normal 'around town' grinding, this is all we really need.
                Spell.Cast("Ferocious Bite", ret => StyxWoW.Me.ComboPoints >= 4),
                Spell.Cast("Berserk"),
                Spell.Cast("Tiger's Fury"),
                // Get rake up just because we can.
                Spell.Buff("Rake"),
                Spell.Cast("Mangle (Cat)"),

                // Since we can't get 'behind' mobs, just do this, kaythanks
                Movement.CreateMoveToTargetBehavior(true, 4f)
                );
        }
    }
}
