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

using System;
using System.Linq;

using CommonBehaviors.Actions;

using Styx;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular.Helpers
{
    public delegate WoWUnit UnitSelectionDelegate(object context);

    public delegate bool SimpleBooleanDelegate(object context);

    internal static class Spell
    {
        /// <summary>Temporary wrapper for compatibility till next HB release. Returns the cast time left on the currenty casting spell.</summary>
        /// <value>The current cast time left.</value>
        private static TimeSpan CurrentCastTimeLeft
        {
            get
            {
                return
                    Styx.Helpers.Utilities.PerformanceCounterToDateTime(
                        ObjectManager.Wow.Read<uint>(StyxWoW.Me.BaseAddress + 0xA44)) -
                    Styx.Helpers.Utilities.PerformanceCounterToDateTime(StyxWoW.WoWClient.PerformanceCounter());
            }
        }


        #region Properties

        internal static string LastSpellCast { get; set; }

        #endregion

        private static WoWSpell GetSpellByName(string spellName)
        {
            WoWSpell spell;
            if (!SpellManager.Spells.TryGetValue(spellName, out spell))
                spell = SpellManager.RawSpells.FirstOrDefault(s => s.Name == spellName);

            return spell;
        }

        #region StopAndCast

        public static Composite StopAndCast(string name)
        {
            return StopAndCast(name, ret => StyxWoW.Me.CurrentTarget);
        }

        public static Composite StopAndCast(string name, SimpleBooleanDelegate requirements)
        {
            return StopAndCast(name, ret => StyxWoW.Me.CurrentTarget, requirements);
        }

        public static Composite StopAndCast(string name, UnitSelectionDelegate onUnit)
        {
            return StopAndCast(name, onUnit, ret => true);
        }

        public static Composite StopAndCast(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new Decorator(
               ret =>
               {
                   return requirements(ret) && onUnit(ret) != null && SpellManager.CanCast(name, onUnit(ret), true, false);
               },
               new Action(
                   ret =>
                       {
                           WoWSpell spell = GetSpellByName(name);

                           if (spell.CastTime > 0 && StyxWoW.Me.IsMoving)
                           {
                               WoWMovement.MoveStop();
                           }

                           Logger.Write("Casting " + name + " on " + onUnit(ret).SafeName());
                           SpellManager.Cast(name, onUnit(ret));
                        })
               );
        }

        #endregion

        #region StopAndBuff

        public static Composite StopAndBuff(string name)
        {
            return StopAndBuff(name, ret => StyxWoW.Me.CurrentTarget);
        }

        public static Composite StopAndBuff(string name, SimpleBooleanDelegate requirements)
        {
            return StopAndBuff(name, ret => StyxWoW.Me.CurrentTarget, requirements);
        }

        public static Composite StopAndBuff(string name, UnitSelectionDelegate onUnit)
        {
            return StopAndBuff(name, onUnit, ret => true);
        }

        public static Composite StopAndBuff(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return
                new Decorator(
                    ret => onUnit(ret) != null && !onUnit(ret).HasAura(name),
                    StopAndCast(name, onUnit, requirements));
        }

        #endregion

        #region Wait
        /// <summary>
        ///   Creates a composite that will return a success, so long as you are currently casting. (Use this to prevent the CC from
        ///   going down to lower branches in the tree, while casting.)
        /// </summary>
        /// <remarks>
        ///   Created 13/5/2011.
        /// </remarks>
        /// <returns>.</returns>
        public static Composite WaitForCast()
        {
            return WaitForCast(false);
        }

        /// <summary>
        ///   Creates a composite that will return a success, so long as you are currently casting. (Use this to prevent the CC from
        ///   going down to lower branches in the tree, while casting.)
        /// </summary>
        /// <remarks>
        ///   Created 13/5/2011.
        /// </remarks>
        /// <param name = "faceDuring">Whether or not to face during casting</param>
        /// <returns></returns>
        public static Composite WaitForCast(bool faceDuring)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => StyxWoW.Me.IsCasting && !StyxWoW.Me.IsWanding() && CurrentCastTimeLeft.TotalMilliseconds > 500,
                    new PrioritySelector(
                        // This is here to avoid double casting spells with dots/debuffs (like Immolate)
                        // Note: This needs testing.
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget != null &&
                                   StyxWoW.Me.NonChanneledCastingSpellId == StyxWoW.Me.CastingSpellId &&
                                   StyxWoW.Me.CurrentTarget.Auras.Any(
                                       a =>
                                       a.Value.SpellId == StyxWoW.Me.CastingSpellId &&
                                       a.Value.CreatorGuid == StyxWoW.Me.Guid),
                            new Action(ret => SpellManager.StopCasting())),
                        new Decorator(
                            ret => faceDuring,
                            Movement.CreateFaceTargetBehavior()),
                        new ActionAlwaysSucceed()
                        )));
        }
        #endregion

        #region Cast - by name

        /// <summary>
        ///   Creates a behavior to cast a spell by name. Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <returns>.</returns>
        public static Composite Cast(string name)
        {
            return Cast(name, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by name, with special requirements. Returns RunStatus.Success if successful,
        ///   RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Cast(string name, SimpleBooleanDelegate requirements)
        {
            return Cast(name, ret => StyxWoW.Me.CurrentTarget, requirements);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by name, on a specific unit. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name = "onUnit">The on unit.</param>
        /// <returns>.</returns>
        public static Composite Cast(string name, UnitSelectionDelegate onUnit)
        {
            return Cast(name, onUnit, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by name, with special requirements, on a specific unit. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name = "onUnit">The on unit.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Cast(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new Decorator(
                ret =>
                    {
                        //Logger.WriteDebug("Casting spell: " + name);
                        //Logger.WriteDebug("Requirements: " + requirements(ret));
                        //Logger.WriteDebug("OnUnit: " + onUnit(ret));
                        //Logger.WriteDebug("CanCast: " + SpellManager.CanCast(name, onUnit(ret), false));

                        return requirements != null && onUnit != null && requirements(ret) && onUnit(ret) != null && SpellManager.CanCast(name, onUnit(ret), true);
                    },
                new Action(
                    ret =>
                        {
                            Logger.Write("Casting " + name + " on " + onUnit(ret).SafeName());
                            SpellManager.Cast(name, onUnit(ret));
                        })
                );
        }

        #endregion

        #region Cast - by ID

        /// <summary>
        ///   Creates a behavior to cast a spell by ID. Returns RunStatus.Success if successful,
        ///   RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "spellId">Identifier for the spell.</param>
        /// <returns>.</returns>
        public static Composite Cast(int spellId)
        {
            return Cast(spellId, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by ID, with special requirements. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "spellId">Identifier for the spell.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Cast(int spellId, SimpleBooleanDelegate requirements)
        {
            return Cast(spellId, ret => StyxWoW.Me.CurrentTarget, requirements);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by ID, on a specific unit. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "spellId">Identifier for the spell.</param>
        /// <param name = "onUnit">The on unit.</param>
        /// <returns>.</returns>
        public static Composite Cast(int spellId, UnitSelectionDelegate onUnit)
        {
            return Cast(spellId, onUnit, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by ID, with special requirements, on a specific unit.
        ///   Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "spellId">Identifier for the spell.</param>
        /// <param name = "onUnit">The on unit.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Cast(int spellId, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new Decorator(
                ret =>
                requirements != null && requirements(ret) && onUnit != null && onUnit(ret) != null && SpellManager.CanCast(spellId, onUnit(ret), true),
                new Action(
                    ret =>
                        {
                            Logger.Write("Casting " + spellId + " on " + onUnit(ret).SafeName());
                            SpellManager.Cast(spellId);
                        })
                );
        }

        #endregion

        #region Buff - by name

        /// <summary>
        ///   Creates a behavior to cast a buff by name on current target. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name of the buff</param>
        /// <returns></returns>
        public static Composite Buff(string name)
        {
            return Buff(name, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a buff by name, with special requirements, on current target. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name of the buff</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns></returns>
        public static Composite Buff(string name, SimpleBooleanDelegate requirements)
        {
            return Buff(name, ret => StyxWoW.Me.CurrentTarget, requirements);
        }

        /// <summary>
        ///   Creates a behavior to cast a buff by name on a specific unit. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name of the buff</param>
        /// <param name = "onUnit">The on unit</param>
        /// <returns></returns>
        public static Composite Buff(string name, UnitSelectionDelegate onUnit)
        {
            return Buff(name, onUnit, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a buff by name, with special requirements, on a specific unit. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name of the buff</param>
        /// <param name = "onUnit">The on unit</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns></returns>
        public static Composite Buff(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return
                new Decorator(
                    ret => onUnit(ret) != null && !onUnit(ret).HasAura(name),
                    Cast(name, onUnit, requirements));
        }

        #endregion

        #region BuffSelf - by name

        /// <summary>
        ///   Creates a behavior to cast a buff by name on yourself. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/6/2011.
        /// </remarks>
        /// <param name = "name">The buff name.</param>
        /// <returns>.</returns>
        public static Composite BuffSelf(string name)
        {
            return Buff(name, ret => StyxWoW.Me, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a buff by name on yourself with special requirements. Returns RunStatus.Success if
        ///   successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/6/2011.
        /// </remarks>
        /// <param name = "name">The buff name.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite BuffSelf(string name, SimpleBooleanDelegate requirements)
        {
            return Buff(name, ret => StyxWoW.Me, requirements);
        }

        #endregion

        #region Buff - by ID

        /// <summary>
        ///   Creates a behavior to cast a buff by name on current target. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "spellId">The ID of the buff</param>
        /// <returns></returns>
        public static Composite Buff(int spellId)
        {
            return Buff(spellId, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a buff by name, with special requirements, on current target. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "spellId">The ID of the buff</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns></returns>
        public static Composite Buff(int spellId, SimpleBooleanDelegate requirements)
        {
            return Buff(spellId, ret => StyxWoW.Me.CurrentTarget, requirements);
        }

        /// <summary>
        ///   Creates a behavior to cast a buff by name on a specific unit. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "spellId">The ID of the buff</param>
        /// <param name = "onUnit">The on unit</param>
        /// <returns></returns>
        public static Composite Buff(int spellId, UnitSelectionDelegate onUnit)
        {
            return Buff(spellId, onUnit, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a buff by name, with special requirements, on a specific unit. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "spellId">The ID of the buff</param>
        /// <param name = "onUnit">The on unit</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns></returns>
        public static Composite Buff(int spellId, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return
                new Decorator(
                    ret => onUnit(ret) != null && !onUnit(ret).Auras.Values.Any(a => a.SpellId == spellId),
                    Cast(spellId, onUnit, requirements));
        }

        #endregion

        #region BufSelf - by ID

        /// <summary>
        ///   Creates a behavior to cast a buff by ID on yourself. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/6/2011.
        /// </remarks>
        /// <param name = "spellId">The buff ID.</param>
        /// <returns>.</returns>
        public static Composite BuffSelf(int spellId)
        {
            return Buff(spellId, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a buff by ID on yourself with special requirements. Returns RunStatus.Success if
        ///   successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/6/2011.
        /// </remarks>
        /// <param name = "spellId">The buff ID.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite BuffSelf(int spellId, SimpleBooleanDelegate requirements)
        {
            return Buff(spellId, ret => StyxWoW.Me, requirements);
        }

        #endregion

        #region CastOnGround - placeable spell casting

        /// <summary>
        ///   Creates a behavior to cast a spell by name, on the ground at the specified location. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "spell">The spell.</param>
        /// <param name = "onLocation">The on location.</param>
        /// <returns>.</returns>
        public static Composite CastOnGround(string spell, LocationRetriever onLocation)
        {
            return CastOnGround(spell, onLocation, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by name, on the ground at the specified location. Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "spell">The spell.</param>
        /// <param name = "onLocation">The on location.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite CastOnGround(string spell, LocationRetriever onLocation, SimpleBooleanDelegate requirements)
        {
            return new Decorator(
                ret =>
                requirements(ret) && onLocation != null && SpellManager.CanCast(spell) &&
                SpellManager.Spells[spell].MaxRange < StyxWoW.Me.Location.Distance(onLocation(ret)),
                new Action(
                    ret =>
                        {
                            Logger.Write("Casting " + spell + " at location " + onLocation(ret));
                            SpellManager.Cast(spell);
                            LegacySpellManager.ClickRemoteLocation(onLocation(ret));
                        })
                );
        }

        #endregion
    }
}