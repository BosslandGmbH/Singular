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
using Styx.Logic;
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
        public static TimeSpan GetSpellCooldown(string spell)
        {
            if (SpellManager.HasSpell(spell))
                return SpellManager.Spells[spell].CooldownTimeLeft();
            return TimeSpan.MaxValue;
        }

        // Temp wrapper for upcoming HB API
        public static TimeSpan CooldownTimeLeft(this WoWSpell spell)
        {
            var luaTime = Lua.GetReturnVal<double>(string.Format("local x,y=GetSpellCooldown({0}); return x+y-GetTime()", spell.Id), 0);
            if (luaTime <= 0)
                return TimeSpan.Zero;
            return TimeSpan.FromSeconds(luaTime);
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
        /// <returns></returns>
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
            return
                new Action(ret =>
                            {
                                if (!StyxWoW.Me.IsCasting)
                                    return RunStatus.Failure;

                                if (StyxWoW.Me.IsWanding())
                                    return RunStatus.Failure;

                                if (StyxWoW.Me.CurrentCastTimeLeft.TotalMilliseconds < 500)
                                    return RunStatus.Failure;

                                if (faceDuring && StyxWoW.Me.ChanneledCastingSpellId == 0)
                                    Movement.CreateFaceTargetBehavior();

                                return RunStatus.Running;
                            });
        }

        #endregion

        #region PreventDoubleCast

        /// <summary>
        /// Creates a composite to avoid double casting spells on current target. Mostly usable for spells like Immolate, Devouring Plague etc.
        /// </summary>
        /// <remarks>
        /// Created 19/12/2011 raphus
        /// </remarks>
        /// <param name="spellNames"> Spell names to check </param>
        /// <returns></returns>
        public static Composite PreventDoubleCast(params string[] spellNames)
        {
            return PreventDoubleCast(ret => StyxWoW.Me.CurrentTarget, spellNames);
        }

        /// <summary>
        /// Creates a composite to avoid double casting spells on specified unit. Mostly usable for spells like Immolate, Devouring Plague etc.
        /// </summary>
        /// <remarks>
        /// Created 19/12/2011 raphus
        /// </remarks>
        /// <param name="unit"> Unit to check </param>
        /// <param name="spellNames"> Spell names to check </param>
        /// <returns></returns>
        public static Composite PreventDoubleCast(UnitSelectionDelegate unit, params string[] spellNames)
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => StyxWoW.Me.IsCasting && spellNames.Contains(StyxWoW.Me.CastingSpell.Name) &&
                               unit != null && unit(ret) != null && unit(ret).Auras.Any(a => a.Value.SpellId == StyxWoW.Me.CastingSpellId &&
                               a.Value.CreatorGuid == StyxWoW.Me.Guid),
                        new Action(ret => SpellManager.StopCasting())));
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
            return Cast(name, ret=>true, ret => StyxWoW.Me.CurrentTarget, requirements);
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
            return Cast(name, ret => true, onUnit, ret => true);
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
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Cast(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return Cast(name, ret => true, onUnit, requirements);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by name, with special requirements, on a specific unit. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name="checkMovement"></param>
        /// <param name = "onUnit">The on unit.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Cast(string name, SimpleBooleanDelegate checkMovement, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new Decorator(
                ret =>
                    {
                        //Logger.WriteDebug("Casting spell: " + name);
                        //Logger.WriteDebug("Requirements: " + requirements(ret));
                        //Logger.WriteDebug("OnUnit: " + onUnit(ret));
                        //Logger.WriteDebug("CanCast: " + SpellManager.CanCast(name, onUnit(ret), false));

                        var minReqs = requirements != null && onUnit != null && requirements(ret) && onUnit(ret) != null;
                        var canCast = false;
                        var inRange = false;
                        if (minReqs)
                        {
                            canCast = SpellManager.CanCast(name, onUnit(ret), false, checkMovement(ret));

                            if (canCast)
                            {
                                var target = onUnit(ret);
                                // We're always in range of ourselves. So just ignore this bit if we're casting it on us
                                if (target.IsMe)
                                {
                                    inRange = true;
                                }
                                else
                                {
                                    WoWSpell spell;
                                    if (SpellManager.Spells.TryGetValue(name, out spell))
                                    {
                                        var rangeId = spell.InternalInfo.SpellRangeId;
                                        var minRange = spell.MinRange;
                                        var maxRange = spell.MaxRange;
                                        // RangeId 1 is "Self Only". This should make life easier for people to use self-buffs, or stuff like Starfall where you cast it as a pseudo-buff.
                                        if (rangeId == 1)
                                            inRange = true;
                                        // RangeId 2 is melee range. Huzzah :)
                                        else if (rangeId == 2)
                                            inRange = target.Distance < MeleeRange;
                                        else
                                            inRange = target.Distance < maxRange &&
                                                      target.Distance > (minRange == 0 ? minRange : minRange + 3);
                                    }
                                }
                            }
                        }

                        return minReqs && canCast && inRange;
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

        //private static string _lastBuffCast = string.Empty;
        //private static System.Diagnostics.Stopwatch _castTimer = new System.Diagnostics.Stopwatch();
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
            //if (name == _lastBuffCast && _castTimer.IsRunning && _castTimer.ElapsedMilliseconds < 250)
            //{
            //    return new Action(ret => RunStatus.Success);
            //}
            
            //if (name == _lastBuffCast && StyxWoW.Me.IsCasting)
            //{
            //    _castTimer.Reset();
            //    _castTimer.Start();
            //    return new Action(ret => RunStatus.Success);
            //}

            return
                
                new Decorator(
                    ret => onUnit(ret) != null && !onUnit(ret).HasAura(name),
                    new Sequence(
                        // new Action(ctx => _lastBuffCast = name),
                        Cast(name, onUnit, requirements)));
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

        #region Heal - by name

        /// <summary>
        ///   Creates a behavior to cast a heal spell by name. Heal behaviors will make sure
        ///   we don't double cast. Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <returns>.</returns>
        public static Composite Heal(string name)
        {
            return Heal(name, ret => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a heal spell by name, with special requirements. Heal behaviors will make sure
        ///   we don't double cast. Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Heal(string name, SimpleBooleanDelegate requirements)
        {
            return Heal(name, ret => true, ret => StyxWoW.Me.CurrentTarget, requirements);
        }

        /// <summary>
        ///   Creates a behavior to cast a heal spell by name, on a specific unit. Heal behaviors will make sure
        ///   we don't double cast. Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name = "onUnit">The on unit.</param>
        /// <returns>.</returns>
        public static Composite Heal(string name, UnitSelectionDelegate onUnit)
        {
            return Heal(name, ret => true, onUnit, ret => true);
        }
        /// <summary>
        ///   Creates a behavior to cast a heal spell by name, on a specific unit. Heal behaviors will make sure
        ///   we don't double cast. Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name = "onUnit">The on unit.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Heal(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return Heal(name, ret => true, onUnit, requirements);
        }

        /// <summary>
        ///   Creates a behavior to cast a heal spell by name, with special requirements, on a specific unit. Heal behaviors will make sure
        ///   we don't double cast. Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name="checkMovement"></param>
        /// <param name = "onUnit">The on unit.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Heal(string name, SimpleBooleanDelegate checkMovement, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return
                new Sequence(
                    Cast(name, checkMovement, onUnit, requirements),
                    // Little bit wait here to catch casting
                    new WaitContinue(
                        1,
                        ret =>
                            {
                                WoWSpell spell;
                                if (SpellManager.Spells.TryGetValue(name, out spell))
                                {
                                    if (spell.CastTime == 0)
                                        return true;

                                    return StyxWoW.Me.IsCasting;
                                }

                                return true;
                            },
                        new ActionAlwaysSucceed()),
                    new WaitContinue(
                        10,
                        ret =>
                            {
                                // Let channeled heals been cast till end.
                                if (StyxWoW.Me.ChanneledCastingSpellId != 0)
                                {
                                    return false;
                                }

                                // Interrupted or finished casting. Continue
                                if (!StyxWoW.Me.IsCasting)
                                {
                                    return true;
                                }

                                // 500ms left till cast ends. Shall continue for next spell
                                //if (StyxWoW.Me.CurrentCastTimeLeft.TotalMilliseconds < 500)
                                //{
                                //    return true;
                                //}

                                // If requirements don't meet anymore, stop casting and let it continue
                                if (!requirements(ret))
                                {
                                    SpellManager.StopCasting();
                                    return true;
                                }
                                return false;
                            },
                        new ActionAlwaysSucceed()));
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
                requirements(ret) && onLocation != null && SpellManager.CanCast(spell) 
                /*&&
                SpellManager.Spells[spell].MaxRange < StyxWoW.Me.Location.Distance(onLocation(ret))*/,
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

        #region Resurrect

        /// <summary>
        ///   Creates a behavior to resurrect dead players around. This behavior will res each player once in every 10 seconds.
        ///   Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 16/12/2011.
        /// </remarks>
        /// <param name = "spellName">The name of resurrection spell.</param>
        /// <returns>.</returns>
        public static Composite Resurrect(string spellName)
        {
            return
                new PrioritySelector(
                    ctx =>  Unit.ResurrectablePlayers.FirstOrDefault(u => !Blacklist.Contains(u)),
                    new Decorator(
                        ctx => ctx != null && SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds,
                        new Sequence(
                            Cast(spellName, ctx => (WoWPlayer)ctx),
                            new Action(ctx => Blacklist.Add((WoWPlayer)ctx, TimeSpan.FromSeconds(30))))));
        }

        #endregion

        public static float MeleeRange
        {
            get
            {
                // If we have no target... then give nothing.
                if (StyxWoW.Me.CurrentTargetGuid == 0)
                    return 0f;

                return Math.Max(5f, StyxWoW.Me.CombatReach + 1.3333334f + StyxWoW.Me.CurrentTarget.CombatReach);
            }
        }

        public static float SafeMeleeRange { get { return Math.Max(MeleeRange - 1f, 5f); } }
    }
}