using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Pathing;
using Styx.TreeSharp;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Common
    {
        #region Pull

        // All DKs should be throwing death grip when not in intances. It just speeds things up, and makes a mess for PVP :)
        [Class(WoWClass.DeathKnight)]
        [Behavior(BehaviorType.Pull)]
        [Spec(WoWSpec.DeathKnightBlood)]
        [Spec(WoWSpec.DeathKnightFrost)]
        [Spec(WoWSpec.DeathKnightUnholy)]
        [Spec((WoWSpec)0)]
        [Context(WoWContext.Battlegrounds | WoWContext.Normal)]
        public static Composite CreateDeathKnightNormalAndPvPPull()
        {
            return
                new PrioritySelector(
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    new Sequence(
                        Spell.Cast("Death Grip",
                                    ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),
                        new DecoratorContinue(
                            ret => StyxWoW.Me.IsMoving,
                            new Action(ret => Navigator.PlayerMover.MoveStop())),
                        new WaitContinue(1, new ActionAlwaysSucceed())),
                    Spell.Cast("Howling Blast"),
                    Spell.Cast("Icy Touch"),
                    Movement.CreateMoveToMeleeBehavior(true)
                    );
        }

        // Non-blood DKs shouldn't be using Death Grip in instances. Only tanks should!
        // You also shouldn't be a blood DK if you're DPSing. Thats just silly. (Like taking a prot war as DPS... you just don't do it)
        [Class(WoWClass.DeathKnight)]
        [Behavior(BehaviorType.Pull)]
        [Spec(WoWSpec.DeathKnightFrost)]
        [Spec(WoWSpec.DeathKnightUnholy)]
        [Spec((WoWSpec)0)]
        [Context(WoWContext.Instances)]
        public static Composite CreateDeathKnightFrostAndUnholyInstancePull()
        {
            return
                new PrioritySelector(
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.Cast("Howling Blast"),
                    Spell.Cast("Icy Touch"),
                    Movement.CreateMoveToMeleeBehavior(true)
                    );
        }



        #endregion

        #region PreCombatBuffs

        [Class(WoWClass.DeathKnight)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Spec(WoWSpec.DeathKnightBlood)]
        [Spec(WoWSpec.DeathKnightFrost)]
        [Spec(WoWSpec.DeathKnightUnholy)]
        [Spec((WoWSpec)0)]
        [Context(WoWContext.All)]
        public static Composite CreateDeathKnightPreCombatBuffs()
        {
            // Note: This is one of few places where this is slightly more valid than making multiple functions.
            // Since this type of stuff is shared, we are safe to do this. Jus leave as-is.
            return
                new PrioritySelector(
                    Spell.BuffSelf(
                        "Frost Presence",
                        ret => TalentManager.CurrentSpec == (WoWSpec)0),
                    Spell.BuffSelf(
                        "Blood Presence",
                        ret => TalentManager.CurrentSpec == WoWSpec.DeathKnightBlood),
                    Spell.BuffSelf(
                        "Unholy Presence",
                        ret => TalentManager.CurrentSpec == WoWSpec.DeathKnightUnholy || TalentManager.CurrentSpec == WoWSpec.DeathKnightFrost),
                    Spell.BuffSelf(
                        "Horn of Winter",
                        ret => !StyxWoW.Me.HasAura("Horn of Winter") && !StyxWoW.Me.HasAura("Battle Shout") && !StyxWoW.Me.HasAura("Roar of Courage"))
                    );
        }

        #endregion
    }
}
