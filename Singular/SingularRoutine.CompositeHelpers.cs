using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        #region Delegates

        public delegate bool SimpleBoolReturnDelegate(object context);

        public delegate WoWUnit UnitSelectionDelegate(object context);

        #endregion

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
                ret => extra(ret) && SpellManager.CanCast(spellName, unitSelector(ret)),
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
                ret => extra(ret) && SpellManager.CanBuff(spellName, unitSelector(ret)),
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