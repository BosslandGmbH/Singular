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

using CommonBehaviors.Actions;

using Singular.Composites;

using Styx;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular
{
    public delegate WoWPoint LocationRetrievalDelegate(object context);

    partial class SingularRoutine
    {
        #region Delegates

        public delegate bool SimpleBoolReturnDelegate(object context);

        public delegate WoWUnit UnitSelectionDelegate(object context);

        #endregion

        protected Composite CreateWaitForCast()
        {
            return new Decorator(
                ret => Me.IsCasting,
                new ActionAlwaysSucceed());
        }

        protected Composite CreateCastPetAction(PetAction action, bool parentIsSelector)
        {
            return CreateCastPetActionOn(action, parentIsSelector, ret => Me.CurrentTarget);
        }

        protected Composite CreateCastPetActionOn(PetAction action, bool parentIsSelector, UnitSelectionDelegate onUnit)
        {
            return new Action(
                delegate(object context)
                    {
                        PetManager.CastPetAction(action, onUnit(context));

                        // Purposely fail here, we want to 'skip' down the tree.
                        if (parentIsSelector)
                        {
                            return RunStatus.Failure;
                        }
                        return RunStatus.Success;
                    });
        }

        protected Composite CreateEnsureTarget()
        {
            return new Decorator(
                ret => Me.CurrentTarget == null || Me.CurrentTarget.Dead,
                new PrioritySelector(
                    // Set our context to the RaF leaders target, or the first in the target list.
                    ctx => RaFHelper.Leader != null ? RaFHelper.Leader.CurrentTarget : Targeting.Instance.FirstUnit,
                    // Make sure the target is VALID. If not, then ignore this next part. (Resolves some silly issues!)
                    new Decorator(
                        ret => ret != null,
                        new Sequence(
                            new Action(ret => Logger.Write("Target is invalid. Switching to " + ((WoWUnit)ret).Name + "!")),
                            new Action(ret => ((WoWUnit)ret).Target()))),
                    // In order to resolve getting "stuck" on a target, we'll clear it if there's nothing viable.
                    new Action(
                        ret =>
                            {
                                Me.ClearTarget();
                                // Force a failure, just so we can move down the branch.
                                return RunStatus.Failure;
                            }),
                    new ActionLogMessage(false, "No viable target! NOT GOOD!")));
        }

        protected Composite CreateRangeAndFace(float maxRange, UnitSelectionDelegate distanceFrom)
        {
            return new Decorator(
                ret => distanceFrom(ret) != null,
                new PrioritySelector(
                    new Decorator(
                        // Either get in range, or get in LOS.
                        ret => StyxWoW.Me.Location.DistanceSqr(distanceFrom(ret).Location) > maxRange * maxRange || !distanceFrom(ret).InLineOfSight,
                        new Action(ret => Navigator.MoveTo(distanceFrom(ret).Location))),
                    new Decorator(
                        ret => Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new Decorator(
                        ret => Me.CurrentTarget != null && !Me.IsSafelyFacing(Me.CurrentTarget, 70),
                        new Action(ret => Me.CurrentTarget.Face()))
                    ));
        }

        protected Composite CreateAutoAttack(bool includePet)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !Me.IsAutoAttacking,
                    new Action(ret => Me.ToggleAttack())),
                new Decorator(
                    ret => includePet && Me.GotAlivePet && !Me.Pet.IsAutoAttacking,
                    new Action(ret => PetManager.CastPetAction(PetAction.Attack)))
                );
        }

        private void CastWithLog(string spellName, WoWUnit onTarget)
        {
            Logger.Write("Casting " + spellName + " on " + onTarget.Name);
            SpellManager.Cast(spellName, onTarget);
        }

        private void CastWithLog(int spellId, WoWUnit onTarget)
        {
            Logger.Write("Casting " + WoWSpell.FromId(spellId).Name + " on " + onTarget.Name);
            SpellManager.Cast(spellId, onTarget);
        }

        #region Cast By Name

        public Composite CreateSpellCast(string spellName, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector)
        {
            return new Decorator(
                ret => extra(ret) && unitSelector(ret) != null && SpellManager.CanCast(spellName, unitSelector(ret)),
                new Sequence(
                    new DecoratorContinue(
                        ret => SpellManager.Spells[spellName].CastTime != 0,
                        new Action(
                            ret =>
                                {
                                    Navigator.PlayerMover.MoveStop();
                                    StyxWoW.SleepForLagDuration();
                                })),
                    new Action(ret => CastWithLog(spellName, unitSelector(ret)))));
        }

        public Composite CreateSpellCast(string spellName)
        {
            return CreateSpellCast(spellName, ret => true);
        }

        public Composite CreateSpellCast(string spellName, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellCast(spellName, extra, ret => Me.CurrentTarget);
        }

        public Composite CreateSpellCastOnSelf(string spellName)
        {
            return CreateSpellCast(spellName, ret => true);
        }

        public Composite CreateSpellCastOnSelf(string spellName, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellCast(spellName, extra, ret => Me);
        }

        #endregion

        #region Cast By ID

        public Composite CreateSpellCast(int spellId, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector)
        {
            return new Decorator(
                ret => extra(ret) && SpellManager.CanCast(spellId, unitSelector(ret)),
                new Action(ret => CastWithLog(spellId, unitSelector(ret))));
        }

        public Composite CreateSpellCast(int spellId)
        {
            return CreateSpellCast(spellId, ret => true);
        }

        public Composite CreateSpellCast(int spellId, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellCast(spellId, extra, ret => Me.CurrentTarget);
        }

        public Composite CreateSpellCastOnSelf(int spellId)
        {
            return CreateSpellCast(spellId, ret => true);
        }

        public Composite CreateSpellCastOnSelf(int spellId, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellCast(spellId, ret => true, ret => Me);
        }

        #endregion

        #region Buff By Name

        public Composite CreateSpellBuff(string spellName, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector)
        {
            // BUGFIX: HB currently doesn't check ActiveAuras in the spell manager. So this'll break on new spell procs
            return CreateSpellCast(
                spellName, ret => extra(ret) && unitSelector(ret) != null && !HasAuraStacks(spellName, 0, unitSelector(ret)), unitSelector);
        }

        public Composite CreateSpellBuff(string spellName)
        {
            return CreateSpellBuff(spellName, ret => true);
        }

        public Composite CreateSpellBuff(string spellName, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellBuff(spellName, extra, ret => Me.CurrentTarget);
        }

        public Composite CreateSpellBuffOnSelf(string spellName)
        {
            return CreateSpellBuffOnSelf(spellName, ret => true);
        }

        public Composite CreateSpellBuffOnSelf(string spellName, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellBuff(spellName, extra, ret => Me);
        }

        #endregion

        #region Cast By ID

        public Composite CreateSpellBuff(int spellId, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector)
        {
            return new Decorator(
                ret => extra(ret) && unitSelector(ret) != null && SpellManager.CanBuff(spellId, unitSelector(ret)),
                new Action(ret => CastWithLog(spellId, unitSelector(ret))));
        }

        public Composite CreateSpellBuff(int spellId)
        {
            return CreateSpellCast(spellId, ret => true);
        }

        public Composite CreateSpellBuff(int spellId, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellCast(spellId, extra, ret => Me.CurrentTarget);
        }

        public Composite CreateSpellBuffOnSelf(int spellId)
        {
            return CreateSpellCast(spellId, ret => true);
        }

        public Composite CreateSpellBuffOnSelf(int spellId, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellCast(spellId, extra, ret => Me);
        }

        #endregion

		#region Party Buff By Name

		/// <summary>
		/// To cast buffs on party members like Dampen Magic and such.
		/// </summary>
		/// <param name="spellName">Name of the buff</param>
		/// <returns></returns>
		public Composite CreateSpellPartyBuff(string spellName)
		{
			return
				new PrioritySelector(
					new Decorator(
						ret => Me.IsInParty && Me.PartyMembers.Any(p => p.IsAlive && !p.HasAura(spellName)),
						new PrioritySelector(
							ctx => Me.PartyMembers.First(p => p.IsAlive && !p.HasAura(spellName)),
							CreateRangeAndFace(35, ret => (WoWUnit)ret),
							CreateSpellCast(spellName, ret => true, ret => (WoWUnit)ret)))
				);
		}

		#endregion
	}
}