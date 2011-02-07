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
                new Action(delegate { }));
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
                            return RunStatus.Failure;
                        return RunStatus.Success;
                    });
        }

        protected Composite CreateEnsureTarget()
        {
            return new Decorator(
                ret => Me.CurrentTarget == null || Me.CurrentTarget.Dead,
                new PrioritySelector(
                    new Decorator(
                        ret => Targeting.Instance.FirstUnit != null,
                        new Sequence(
                            new Action(ret => Logger.Write("Target is invalid. Switching to the first one in the target list!")),
                            new Action(ret => Targeting.Instance.FirstUnit.Target())))));
        }

        protected Composite CreateRangeAndFace(float maxRange, UnitSelectionDelegate distanceFrom)
        {
            return new Decorator(
                ret => distanceFrom(ret) != null,
                new PrioritySelector(
                    new Decorator(
                        // Either get in range, or get in LOS.
                        ret => StyxWoW.Me.Location.DistanceSqr(distanceFrom(ret).Location) > maxRange*maxRange || !distanceFrom(ret).InLineOfSight,
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
            Log("Casting " + spellName);
            SpellManager.Cast(spellName, onTarget);
        }

        private void CastWithLog(int spellId, WoWUnit onTarget)
        {
            Log("Casting " + WoWSpell.FromId(spellId).Name);
            SpellManager.Cast(spellId, onTarget);
        }

        #region Cast By Name

        public Composite CreateSpellCast(string spellName, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector)
        {
            return new Decorator(
                ret => extra(ret) && unitSelector(ret) != null && SpellManager.CanCast(spellName, unitSelector(ret)),
                new Action(ret => CastWithLog(spellName, unitSelector(ret))));
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
            return CreateSpellCast(spellName, ret => true, ret => Me);
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
            return new Decorator(
                ret => extra(ret) && unitSelector(ret) != null && SpellManager.CanBuff(spellName, unitSelector(ret)),
                new Action(ret => CastWithLog(spellName, unitSelector(ret))));
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
            return CreateSpellBuff(spellName, ret => true);
        }

        public Composite CreateSpellBuffOnSelf(string spellName, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellBuff(spellName, ret => true, ret => Me);
        }

        #endregion

        #region Cast By ID

        public Composite CreateSpellBuff(int spellId, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector)
        {
            return new Decorator(
                ret => extra(ret) && SpellManager.CanBuff(spellId, unitSelector(ret)),
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
            return CreateSpellCast(spellId, ret => true, ret => Me);
        }

        #endregion
    }
}