#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author: apoc $
// $Date: 2011-03-18 10:36:36 -0600 (Fri, 18 Mar 2011) $
// $HeadURL: http://svn.apocdev.com/singular/trunk/Singular/ClassSpecific/Mage/Frost.cs $
// $LastChangedBy: apoc $
// $LastChangedDate: 2011-03-18 10:36:36 -0600 (Fri, 18 Mar 2011) $
// $LastChangedRevision: 190 $
// $Revision: 190 $

#endregion

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Pathing;

using TreeSharp;
using CommonBehaviors.Actions;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.ArcaneMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateArcaneMageCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                //Move away from frozen targets
                new Decorator(
                    ret => Me.CurrentTarget.HasAura("Frost Nova") && Me.CurrentTarget.DistanceSqr < 5 * 5,
                    new Action(
                        ret =>
                        {
                            Logger.Write("Getting away from frozen target");
                            WoWPoint moveTo = WoWMathHelper.CalculatePointFrom(Me.Location, Me.CurrentTarget.Location, 10f);

                            if (Navigator.CanNavigateFully(Me.Location, moveTo))
                            {
                                Navigator.MoveTo(moveTo);
                            }
                        })),
                        
                CreateMoveToAndFace(34f, ret => Me.CurrentTarget),
                CreateSpellBuffOnSelf("Ice Block", ret => Me.HealthPercent < 10 && !Me.ActiveAuras.ContainsKey("Hypothermia")),
                new Decorator(ret => Me.ActiveAuras.ContainsKey("Ice Block"),
                   new ActionIdle()),
                CreateSpellBuff("Frost Nova", ret => !Me.IsInInstance && !GotSheep && Me.CurrentTarget.DistanceSqr <= 8 * 8),
                CreateWaitForCast(true),
                CreateSpellCast("Evocation", ret => Me.ManaPercent < 20),
                new Decorator(ret => HaveManaGem() && Me.ManaPercent <= 30,
                   new Action(ctx => UseManaGem())),
                new Decorator(ret => (!SheepTimer.IsRunning || SheepTimer.Elapsed.Seconds > 5) && NeedToSheep() && !Me.IsInInstance && !Styx.Logic.Battlegrounds.IsInsideBattleground,
                   new Action(ctx => SheepLogic())),
                CreateSpellCast("Counterspell", ret => Me.CurrentTarget.IsCasting),
                CreateSpellCast("Mirror Image", ret => Me.CurrentTarget.HealthPercent > 20),
                CreateSpellCast("Time Warp", ret => Me.CurrentTarget.HealthPercent > 20),
                new Decorator(
                    ret => Me.CurrentTarget.HealthPercent > 50,
                    new Sequence(
                        new Action(ctx => Me.CurrentTarget.Face()),
                        new Action(ctx => StyxWoW.SleepForLagDuration()),
                        new PrioritySelector(CreateSpellCast("Flame Orb"))
                        )),
                CreateSpellBuffOnSelf("Mana Shield", ret => !Me.Auras.ContainsKey("Mana Shield") && Me.HealthPercent <= 75),
                CreateSpellCast("Slow", ret => TalentManager.GetCount(1, 18) < 2 && !Me.CurrentTarget.ActiveAuras.ContainsKey("Slow") && Me.CurrentTarget.Distance > 5),
                CreateSpellCast("Arcane Missiles", ret => Me.ActiveAuras.ContainsKey("Arcane Missiles!") && Me.ActiveAuras.ContainsKey("Arcane Blast") && Me.ActiveAuras["Arcane Blast"].StackCount >= 2),
                CreateSpellCast("Arcane Barrage", ret => Me.ActiveAuras.ContainsKey("Arcane Blast") && Me.ActiveAuras["Arcane Blast"].StackCount >= 3 ),
                CreateSpellBuffOnSelf("Presence of Mind"),
                CreateSpellCast("Arcane Blast"),
                CreateSpellCast("Shoot", ret=> IsNotWanding)//Wand if all else fails
                );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.ArcaneMage)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateArcaneMagePull()
        {
            return
                new PrioritySelector(
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                    CreateMoveToAndFace(34f, ret => Me.CurrentTarget),
                    CreateSpellCast("Arcane Blast")
                    );
        }

        //[Class(WoWClass.Mage)]
        //[Spec(TalentSpec.ArcaneMage)]
        //[Behavior(BehaviorType.PreCombatBuffs)]
        //[Context(WoWContext.All)]
        //public Composite CreateArcaneMagePreCombatBuffs()
        //{
        //    return
        //        new PrioritySelector(
        //            CreateSpellBuffOnSelf("Arcane Brilliance", ret => (!Me.HasAura("Arcane Brilliance") && !Me.HasAura("Fel Intelligence"))),
        //            CreateSpellBuffOnSelf("Molten Armor", ret => (!Me.HasAura("Molten Armor"))));
        //}
    }
}