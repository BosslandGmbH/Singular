using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using TreeSharp;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Common
    {
        #region Pull

        // All DKs should be throwing death grip when not in intances. It just speeds things up, and makes a mess for PVP :)
        [Class(WoWClass.DeathKnight)]
        [Behavior(BehaviorType.Pull)]
        [Spec(TalentSpec.BloodDeathKnight)]
        [Spec(TalentSpec.FrostDeathKnight)]
        [Spec(TalentSpec.UnholyDeathKnight)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.Battlegrounds | WoWContext.Normal)]
        public static Composite CreateDeathKnightPvpNormalPull()
        {
            return
                new PrioritySelector(
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.Cast("Death Grip", ret => StyxWoW.Me.CurrentTarget.Distance > 15),
                    Spell.Cast("Howling Blast"),
                    Spell.Cast("Icy Touch"),
                    Movement.CreateMoveToTargetBehavior(true, 5f)
                    );
        }

        // Non-blood DKs shouldn't be using Death Grip in instances. Only tanks should!
        // You also shouldn't be a blood DK if you're DPSing. Thats just silly. (Like taking a prot war as DPS... you just don't do it)
        [Class(WoWClass.DeathKnight)]
        [Behavior(BehaviorType.Pull)]
        [Spec(TalentSpec.FrostDeathKnight)]
        [Spec(TalentSpec.UnholyDeathKnight)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.Instances)]
        public static Composite CreateDeathKnightInstancePull()
        {
            return
                new PrioritySelector(
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.Cast("Howling Blast"),
                    Spell.Cast("Icy Touch"),
                    Movement.CreateMoveToTargetBehavior(true, 5f)
                    );
        }

        // Blood DKs should be DG'ing everything it can when pulling. ONLY IN INSTANCES.
        [Class(WoWClass.DeathKnight)]
        [Behavior(BehaviorType.Pull)]
        [Spec(TalentSpec.BloodDeathKnight)]
        [Context(WoWContext.Instances)]
        public static Composite CreateBloodDeathKnightInstancePull()
        {
            return
                new PrioritySelector(
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.Cast("Death Grip", ret => StyxWoW.Me.CurrentTarget.Distance > 15),
                    Spell.Cast("Howling Blast"),
                    Spell.Cast("Icy Touch"),
                    Movement.CreateMoveToTargetBehavior(true, 5f),
                    Helpers.Common.CreateAutoAttack(true)
                    );
        }

        #endregion

        #region PreCombatBuffs

        [Class(WoWClass.DeathKnight)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Spec(TalentSpec.BloodDeathKnight)]
        [Spec(TalentSpec.FrostDeathKnight)]
        [Spec(TalentSpec.UnholyDeathKnight)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]
        public static Composite CreateDeathKnightPreCombatBuffs()
        {
            // Note: This is one of few places where this is slightly more valid than making multiple functions.
            // Since this type of stuff is shared, we are safe to do this. Jus leave as-is.
            return
                new PrioritySelector(
                    Spell.BuffSelf(
                        "Frost Presence",
                        ret => TalentManager.CurrentSpec == TalentSpec.Lowbie),
                    Spell.BuffSelf(
                        "Blood Presence",
                        ret => TalentManager.CurrentSpec == TalentSpec.BloodDeathKnight),
                    Spell.BuffSelf(
                        "Unholy Presence",
                        ret => TalentManager.CurrentSpec == TalentSpec.UnholyDeathKnight || TalentManager.CurrentSpec == TalentSpec.FrostDeathKnight),
                    Spell.BuffSelf(
                        "Horn of Winter",
                        ret => !StyxWoW.Me.HasAura("Horn of Winter") && !StyxWoW.Me.HasAura("Battle Shout") && !StyxWoW.Me.HasAura("Roar of Courage")),
                    Spell.BuffSelf(
                        "Bone Shield",
                        ret => TalentManager.CurrentSpec == TalentSpec.BloodDeathKnight)
                    );
        }

        #endregion
    }
}
