
using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals.World;
using Action = Styx.TreeSharp.Action;
using Singular.Settings;
using Singular.Managers;
using Styx.Helpers;
using System.Drawing;

namespace Singular.Helpers
{
    enum LagTolerance
    {
        No = 0,
        Yes
    };

    public delegate WoWUnit UnitSelectionDelegate(object context);

    public delegate bool SimpleBooleanDelegate(object context);
    public delegate string SimpleStringDelegate(object context);
    public delegate int SimpleIntDelegate(object context);


    internal static class Spell
    {
        public static WoWDynamicObject GetGroundEffectBySpellId(int spellId)
        {
            return ObjectManager.GetObjectsOfType<WoWDynamicObject>().FirstOrDefault(o => o.SpellId == spellId);
        }

        public static bool IsStandingInGroundEffect(bool harmful=true)
        {
            foreach(var obj in ObjectManager.GetObjectsOfType<WoWDynamicObject>())
            {
                if (obj.Distance <= obj.Radius)
                {
                    // We're standing in this.
                    if (obj.Caster.IsFriendly && !harmful)
                        return true;
                    if (obj.Caster.IsHostile && harmful)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Property that all Combat Behaviors should check for whether AOE spells
        /// are permitted.  This will be the interface which wraps all settings
        /// toggles which may effect its on/off state
        /// </summary>
        public static bool UseAOE
        {
            get
            {
                return HotkeyManager.IsAoeEnabled;
            }
        }

        /// <summary>
        /// get melee distance between two units
        /// </summary>
        /// <param name="unit">unit</param>
        /// <param name="me">Me if null, otherwise second unit</param>
        /// <returns></returns>
        public static float MeleeDistance(this WoWUnit unit, WoWUnit me = null)
        {
            // abort if mob null
            if (unit == null)
                return 0;

            // optional arg implying Me, then make sure not Mob also
            if (me == null)
            {
                if ( unit.IsMe)
                    return 0;

                me = StyxWoW.Me;
            }

            // pvp, then keep it close
            if (unit.IsPlayer && me.IsPlayer)
                return 3.5f;

            return Math.Max(5f, me.CombatReach + 1.3333334f + unit.CombatReach);
        }

        public static float MeleeRange
        {
            get
            {
                return StyxWoW.Me.CurrentTarget.MeleeDistance();
            }
        }

        public static float SafeMeleeRange { get { return Math.Max(MeleeRange - 1f, 5f); } }

        /// <summary>
        /// get the effective distance between two mobs accounting for their 
        /// combat reaches (hitboxes)
        /// </summary>
        /// <param name="unit">unit</param>
        /// <param name="me">Me if null, otherwise second unit</param>
        /// <returns></returns>
        public static float SpellDistance(this WoWUnit unit, WoWUnit me = null)
        {
            // abort if mob null
            if (unit == null)
                return 0;

            // optional arg implying Me, then make sure not Mob also
            if (me == null)
                me = StyxWoW.Me;

            // pvp, then keep it close
            float dist = me.Location.Distance(unit.Location);
            dist -= me.CombatReach + unit.CombatReach;
            return Math.Max(0, dist);
        }



        /// <summary>
        /// gets the current Cooldown remaining for the spell
        /// </summary>
        /// <param name="spell"></param>
        /// <returns>TimeSpan representing cooldown remaining, TimeSpan.MaxValue if spell unknown</returns>
        public static TimeSpan GetSpellCooldown(string spell, int indetermValue = int.MaxValue )
        {
            SpellFindResults sfr;
            if ( SpellManager.FindSpell(spell, out sfr))
                return (sfr.Override ?? sfr.Original).CooldownTimeLeft;

            if (indetermValue == int.MaxValue)
                return TimeSpan.MaxValue;

            return TimeSpan.FromSeconds( indetermValue);
        }

        /// <summary>
        ///  Returns maximum spell range based on hitbox of unit. 
        /// </summary>
        /// <param name="spell"></param>
        /// <param name="unit"></param>
        /// <returns>Maximum spell range</returns>
        public static float ActualMaxRange(this WoWSpell spell, WoWUnit unit)
        {
            if (spell.MaxRange == 0)
                return 0;
            // 0.3 margin for error
            return unit != null ? spell.MaxRange + unit.CombatReach + 1f : spell.MaxRange;
        }

        public static float ActualMaxRange(string name, WoWUnit unit)
        {
            SpellFindResults sfr;
            if (!SpellManager.FindSpell(name, out sfr))
                return 0f;

            WoWSpell spell = sfr.Override ?? sfr.Original;
            return spell.ActualMaxRange(unit);
        }


        /// <summary>
        /// Returns minimum spell range based on hitbox of unit. 
        /// </summary>
        /// <param name="spell"></param>
        /// <param name="unit"></param>
        /// <returns>Minimum spell range</returns>

        public static float ActualMinRange(this WoWSpell spell, WoWUnit unit)
        {
            if (spell.MinRange == 0)
                return 0;
            // 0.3 margin for error
            return unit != null ? spell.MinRange + unit.CombatReach + 1.6666667f : spell.MinRange;
        }

        public static double TimeToEnergyCap()
        {

            double timetoEnergyCap;
            double playerEnergy;
            double ER_Rate;

            playerEnergy = Lua.GetReturnVal<int>("return UnitMana(\"player\");", 0); // current Energy 
            ER_Rate = EnergyRegen();
            timetoEnergyCap = (100 - playerEnergy) * (1.0 / ER_Rate); // math 

            return timetoEnergyCap;
        }

        public static double EnergyRegen()
        {
            double energyRegen;
            energyRegen = Lua.GetReturnVal<float>("return GetPowerRegen()", 1); // rate of energy regen
            return energyRegen;
        }

        public static double EnergyRegenInactive()
        {
            double energyRegen;
            energyRegen = Lua.GetReturnVal<float>("return GetPowerRegen()", 0); // rate of energy regen
            return energyRegen;
        }

        #region Properties

        internal static string LastSpellCast { get; set; }

        #endregion

        #region Wait

        public static bool IsGlobalCooldown(bool faceDuring = false, LagTolerance allow = LagTolerance.Yes )
        {
            uint latency = allow == LagTolerance.Yes ? StyxWoW.WoWClient.Latency : 0;
            TimeSpan gcdTimeLeft = SpellManager.GlobalCooldownLeft;
            return gcdTimeLeft.TotalMilliseconds > latency;
        }

        public static TimeSpan GetSpellCastTime(string s)
        {
            SpellFindResults sfr;
            if (SpellManager.FindSpell(s, out sfr))
                return TimeSpan.FromMilliseconds((sfr.Override ?? sfr.Original).CastTime);
            return TimeSpan.Zero;
        }

        /// <summary>
        ///   Creates a composite that will return a success, so long as you are currently casting. (Use this to prevent the CC from
        ///   going down to lower branches in the tree, while casting.)
        /// </summary>
        /// <remarks>
        ///   Created 13/5/2011.
        /// </remarks>
        /// <param name = "faceDuring">Whether or not to face during casting</param>
        /// <param name = "allow">Whether or not to allow lag tollerance for spell queueing</param>
        /// <returns></returns>
        public static Composite WaitForCast(bool faceDuring = false, LagTolerance allow = LagTolerance.Yes )
        {
            return new PrioritySelector(
                new Decorator(
                    ret => faceDuring,
                    Movement.CreateFaceTargetBehavior()
                    ),
                new Action(ret =>
                {
                    if (IsCasting(allow))
                        return RunStatus.Success;

                    return RunStatus.Failure;
                })
                );
        }

        public static bool IsCasting(LagTolerance allow = LagTolerance.Yes)
        {
            if (!StyxWoW.Me.IsCasting)
                return false;

            //if (StyxWoW.Me.IsWanding())
            //    return RunStatus.Failure;

            // following logic previously existed to let channels pass thru -- keeping for now
            if (StyxWoW.Me.ChannelObjectGuid > 0)
                return false;

            uint latency = StyxWoW.WoWClient.Latency * 2;
            TimeSpan castTimeLeft = StyxWoW.Me.CurrentCastTimeLeft;
            if (allow == LagTolerance.Yes // && castTimeLeft != TimeSpan.Zero 
                && StyxWoW.Me.CurrentCastTimeLeft.TotalMilliseconds < latency)
                return false;

            /// -- following code does nothing since the behaviors created are not linked to execution tree
            /// 
            // if (faceDuring && StyxWoW.Me.ChanneledSpell == null) // .ChanneledCastingSpellId == 0)
            //    Movement.CreateFaceTargetBehavior();

            // return RunStatus.Running;
            return true;
        }

        /// <summary>
        ///   Creates a composite that will return a success, so long as you are currently casting. (Use this to prevent the CC from
        ///   going down to lower branches in the tree, while casting.)
        /// </summary>
        /// <remarks>
        ///   Created 13/5/2011.
        /// </remarks>
        /// <param name = "faceDuring">Whether or not to face during casting</param>
        /// <param name = "allow">Whether or not to allow lag tollerance for spell queueing</param>
        /// <returns></returns>
        public static Composite WaitForChannel(LagTolerance allow = LagTolerance.Yes)
        {
            return new PrioritySelector(
                Movement.CreateFaceTargetBehavior(),
                new Action(ret =>
                {
                    if (IsChannelling(allow))
                        return RunStatus.Success;

                    return RunStatus.Failure;
                })
                );
        }

        public static bool IsChannelling(LagTolerance allow = LagTolerance.Yes)
        {
            if (!StyxWoW.Me.IsChanneling)
                return false;

            uint latency = StyxWoW.WoWClient.Latency * 2;
            TimeSpan timeLeft = StyxWoW.Me.CurrentChannelTimeLeft;
            if (allow == LagTolerance.Yes && timeLeft.TotalMilliseconds < latency)
                return false;

            return true;
        }

        public static bool IsCastingOrChannelling(LagTolerance allow = LagTolerance.Yes)
        {
            return IsCasting(allow) || IsChannelling();
        }

        public static Composite WaitForCastOrChannel(LagTolerance allow = LagTolerance.Yes)
        {
            return new PrioritySelector(
                WaitForCast(true, allow),
                WaitForChannel(allow)
                );
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
                        ret =>
                        StyxWoW.Me.IsCasting && spellNames.Contains(StyxWoW.Me.CastingSpell.Name) && unit != null &&
                        unit(ret) != null &&
                        unit(ret).Auras.Any(
                            a => a.Value.SpellId == StyxWoW.Me.CastingSpellId && a.Value.CreatorGuid == StyxWoW.Me.Guid),
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
            return Cast( sp => name);
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
            return Cast( sp => name, requirements);
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
            return Cast( sp => name, onUnit);
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
            return Cast(sp => name, onUnit, requirements);
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
        public static Composite Cast(string name, SimpleBooleanDelegate checkMovement, UnitSelectionDelegate onUnit, 
            SimpleBooleanDelegate requirements)
        {
            return Cast(ret => name, checkMovement, onUnit, requirements);
        }


        /// <summary>
        ///   Creates a behavior to cast a spell by name resolved during tree execution (rather than creation) on the current target.  
        ///   Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 11/25/2012.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <returns>.</returns>
        public static Composite Cast(SimpleStringDelegate name)
        {
            return Cast(name, onUnit => StyxWoW.Me.CurrentTarget);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by name resolved during tree execution (rather than creation) on a specific unit. 
        ///   Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 11/25/2012.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name = "onUnit">The on unit.</param>
        /// <returns>.</returns>
        public static Composite Cast(SimpleStringDelegate name, UnitSelectionDelegate onUnit)
        {
            return Cast(name, onUnit, req => true);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by name resolved during tree execution (rather than creation), with special requirements, 
        ///   on the current target. Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 11/25/2012.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Cast(SimpleStringDelegate name, SimpleBooleanDelegate requirements)
        {
            return Cast(name, onUnit => StyxWoW.Me.CurrentTarget, requirements);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by name resolved during tree execution (rather than creation), with special requirements, 
        ///   on a specific unit. Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 11/25/2012.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name = "onUnit">The on unit.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Cast(SimpleStringDelegate name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return Cast(name, ret => true, onUnit, requirements);
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
            return
                new Decorator(ret => requirements != null && onUnit != null && onUnit(ret) != null && requirements(ret) && SpellManager.CanCast(spellId, onUnit(ret), true),
                    new Action(ret =>
                    {
                        WoWSpell sp = WoWSpell.FromId(spellId);
                        string sname = sp != null ? sp.Name : "#" + spellId.ToString();
                        Logger.Write(string.Format("Casting {0} on {1}", sname, onUnit(ret).SafeName()));
                        SpellManager.Cast(spellId);
                    }));
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
        public static Composite Cast(SimpleIntDelegate spellId, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return
                new Decorator(ret => requirements != null && onUnit != null && onUnit(ret) != null && requirements(ret) && SpellManager.CanCast(spellId(ret), onUnit(ret), true),
                    new Action(ret =>
                    {
                        WoWSpell sp = WoWSpell.FromId(spellId(ret));
                        string sname = sp != null ? sp.Name : "#" + spellId(ret).ToString();
                        Logger.Write(string.Format("Casting {0} on {1}", sname, onUnit(ret).SafeName()));
                        SpellManager.Cast(spellId(ret));
                    }));
        }

        #endregion

        #region Buff - by name

        public static readonly Dictionary<string, DateTime> DoubleCastPreventionDict =
            new Dictionary<string, DateTime>();

        public static Composite Buff(string name, params string[] buffNames)
        {
            return buffNames.Length > 0 ? Buff(name, ret => true, buffNames) : Buff(name, ret => true, name);
        }

        public static Composite Buff(string name, bool myBuff)
        {
            return Buff(name, myBuff, ret => true);
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
            return Buff(name, false, ret => StyxWoW.Me.CurrentTarget, requirements, name);
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
            return Buff(name, false, onUnit, ret => true, name);
        }

        public static Composite Buff(string name, bool myBuff, params string[] buffNames)
        {
            return Buff(name, myBuff, ret => true, buffNames);
        }

        public static Composite Buff(string name, UnitSelectionDelegate onUnit, params string[] buffNames)
        {
            return Buff(name, onUnit, ret => true, buffNames);
        }

        public static Composite Buff(string name, SimpleBooleanDelegate requirements, params string[] buffNames)
        {
            return Buff(name, ret => StyxWoW.Me.CurrentTarget, requirements, buffNames);
        }

        public static Composite Buff(string name, bool myBuff, UnitSelectionDelegate onUnit)
        {
            return Buff(name, myBuff, onUnit, ret => true);
        }

        public static Composite Buff(string name, bool myBuff, SimpleBooleanDelegate requirements)
        {
            return Buff(name, myBuff, ret => StyxWoW.Me.CurrentTarget, requirements);
        }

        public static Composite Buff(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return Buff(name, false, onUnit, requirements);
        }

        public static Composite Buff(string name, bool myBuff, SimpleBooleanDelegate requirements,
            params string[] buffNames)
        {
            return Buff(name, myBuff, ret => StyxWoW.Me.CurrentTarget, requirements, buffNames);
        }

        public static Composite Buff(string name, bool myBuff, UnitSelectionDelegate onUnit, params string[] buffNames)
        {
            return Buff(name, myBuff, onUnit, ret => true, buffNames);
        }

        public static Composite Buff(string name, bool myBuff, UnitSelectionDelegate onUnit,
            SimpleBooleanDelegate requirements)
        {
            return Buff(name, myBuff, onUnit, requirements, name);
        }

        public static Composite Buff(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements,
            params string[] buffNames)
        {
            return Buff(name, false, onUnit, requirements, buffNames);
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
        /// <param name = "myBuff">Check for self debuffs or not</param>
        /// <param name = "onUnit">The on unit</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns></returns>
        public static Composite Buff(string name, bool myBuff, UnitSelectionDelegate onUnit,
            SimpleBooleanDelegate requirements, params string[] buffNames)
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
                    ret => onUnit != null && onUnit(ret) != null 
                    && name != null && !DoubleCastPreventionDict.ContainsKey(name) 
                    && buffNames.All(b => myBuff ? !onUnit(ret).HasMyAura(b) : !onUnit(ret).HasAura(b)),
                    new Sequence( // new Action(ctx => _lastBuffCast = name),
                        Cast(name, onUnit, requirements),
                        new DecoratorContinue(ret => Spell.GetSpellCastTime(name) > TimeSpan.Zero,
                            new Sequence(new WaitContinue(1, ret => StyxWoW.Me.IsCasting,
                                new Action(ret => UpdateDoubleCastDict(name)))))));
        }

        private static void UpdateDoubleCastDict(string spellName)
        {
            if (DoubleCastPreventionDict.ContainsKey(spellName))
                DoubleCastPreventionDict[spellName] = DateTime.UtcNow;

            DoubleCastPreventionDict.Add(spellName, DateTime.UtcNow);
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
            return new Decorator(ret => onUnit(ret) != null && onUnit(ret).Auras.Values.All(a => a.SpellId != spellId),
                Cast(spellId, onUnit, requirements));
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
        public static Composite Buff(SimpleIntDelegate spellId, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new Decorator(ret => onUnit(ret) != null && onUnit(ret).Auras.Values.All(a => a.SpellId != spellId(ret)),
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
            return Buff(spellId, ret => StyxWoW.Me, ret => true);
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
        public static Composite BuffSelf(SimpleIntDelegate spellId, SimpleBooleanDelegate requirements)
        {
            return Buff(spellId, ret => StyxWoW.Me, requirements);
        }

        #endregion

        #region Heal - by name

        private static WoWSpell _spell;

        // used by Spell.Cast() - save fact we are queueing this Heal spell if a spell cast/gcd is in progress already.  this could only occur during 
        // .. the period of latency at the end of a cast where Singular allows you to begin the next one
        private static bool _IsSpellBeingQueued = false;

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
        /// <param name="cancel">The cancel cast in progress delegate</param>
        /// <param name="allow">allow next spell to queue before this one completes</param>
        /// <returns>.</returns>
        public static Composite Cast(string name, SimpleBooleanDelegate checkMovement, UnitSelectionDelegate onUnit,
            SimpleBooleanDelegate requirements, SimpleBooleanDelegate cancel = null, LagTolerance allow = LagTolerance.Yes)
        {
            return Cast(n => name, checkMovement, onUnit, requirements, cancel, allow);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by name, with special requirements, on a specific unit. Will make sure any spell with
        ///   a non-zero cast time (everything not instant) will stay here until passing the latency boundary (point where .IsCasting == false while cast is in progress.)
        ///   Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name="checkMovement"></param>
        /// <param name = "onUnit">The on unit.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <param name="cancel">The cancel cast in progress delegate</param>
        /// <param name="allow">allow next spell to queue before this one completes</param>
        /// <returns>.</returns>
        public static Composite Cast(SimpleStringDelegate name, SimpleBooleanDelegate checkMovement, UnitSelectionDelegate onUnit,
            SimpleBooleanDelegate requirements, SimpleBooleanDelegate cancel = null, LagTolerance allow = LagTolerance.Yes)
        {
            return new Decorator(
                ret => name != null && checkMovement != null && onUnit != null && requirements != null
                    && name(ret) != null
                    && onUnit(ret) != null
                    && requirements(ret),
                new Throttle(
                    new Sequence(

                        // save flag indicating if currently in a GCD or IsCasting before queueing our cast
                        new Action(ret =>
                        {
                            // find spell 
                            SpellFindResults sfr;
                            if (!SpellManager.FindSpell(name(ret), out sfr))
                                return RunStatus.Failure;
                            _spell = sfr.Override ?? sfr.Original;

                            // check we can cast it on target
                            if (!SpellManager.CanCast(_spell, onUnit(ret), true, checkMovement(ret), allow == LagTolerance.Yes))
                                return RunStatus.Failure;

                            // save status of queueing spell (lag tolerance - the prior spell still completing)
                            _IsSpellBeingQueued = allow == LagTolerance.Yes && (SpellManager.GlobalCooldown || StyxWoW.Me.IsCasting || StyxWoW.Me.IsChanneling);

                            Logger.Write(string.Format("Casting {0} on {1}", _spell.Name, onUnit(ret).SafeName()));
                            if ( !SpellManager.Cast( _spell, onUnit(ret)))
                            {
                                Logger.WriteDebug( Color.LightPink, "cast of {0} on {1} failed!", _spell.Name, onUnit(ret).SafeName());
                                return RunStatus.Failure;
                            }

                            return RunStatus.Success;
                        }),

                        // when accountForLag = true, wait for in progress spell (if any) to complete
                        new WaitContinue(
                            TimeSpan.FromMilliseconds(500),
                            ret => !_IsSpellBeingQueued || !(SpellManager.GlobalCooldown || StyxWoW.Me.IsCasting || StyxWoW.Me.IsChanneling),
                            new ActionAlwaysSucceed()
                            ),

                        // failsafe: max time we should be waiting with the prior and latter WaitContinue is latency x 2
                // .. if system is borked, could be 1 second but shouldnt notice.  
                // .. instant spells should be very quick since only prior wait applies

                        // now for non-instant spell, wait for .IsCasting to be true
                        new WaitContinue(
                            TimeSpan.FromMilliseconds(500),
                            ret =>
                            {
                                WoWSpell spell;
                                if (SpellManager.Spells.TryGetValue(name(ret), out spell))
                                {
                                    if (spell.CastTime == 0)
                                        return true;

                                    return StyxWoW.Me.IsCasting || StyxWoW.Me.IsChanneling;
                                }

                                return true;
                            },
                            new ActionAlwaysSucceed()
                            ),

                        new PrioritySelector(

                            // when not monitoring for cancel, don't wait for completion of full cast
                            new Decorator(
                                ret => cancel == null,
                                new ActionAlwaysSucceed()
                                ),

                            // finally, wait at this point until Cast completes
                // .. always return success here since based on flags we cast something
                            new WaitContinue(3,
                                ret =>
                                {
                                    // Interrupted or finished casting. 
                                    if (!StyxWoW.Me.IsCasting && !StyxWoW.Me.IsChanneling)
                                    {
                                        return true;
                                    }

                                    // allow channel spells fall through to cancel test

                                    // for casted spells and lag tolerance enabled, check before end of cast if we are done
                                    if (allow == LagTolerance.Yes && StyxWoW.Me.IsCasting && !StyxWoW.Me.IsChanneling)
                                    {
                                        TimeSpan castTimeLeft = StyxWoW.Me.CurrentCastTimeLeft;
                                        if (castTimeLeft != TimeSpan.Zero && castTimeLeft.TotalMilliseconds < (StyxWoW.WoWClient.Latency * 2))
                                            return true;
                                    }

                                    // check cancel delegate if we are finished
                                    if (cancel(ret))
                                    {
                                        SpellManager.StopCasting();
                                        Logger.Write(System.Drawing.Color.Orange, "/cancel {0} on {1} @ {2:F1}%", name(ret), onUnit(ret).SafeName(), onUnit(ret).HealthPercent);
                                        return true;
                                    }

                                    // continue casting/channeling at this point
                                    return false;
                                },
                                new ActionAlwaysSucceed()
                                )
                            )
                        )
                    )
                );
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
        public static Composite CastOnGround(string spell, LocationRetriever onLocation,
            SimpleBooleanDelegate requirements)
        {
            return CastOnGround(spell, onLocation, requirements, true);
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
        /// <param name="waitForSpell">Waits for spell to become active on cursor if true. </param>
        /// <returns>.</returns>
        public static Composite CastOnGround(string spell, LocationRetriever onLocation,
            SimpleBooleanDelegate requirements, bool waitForSpell)
        {
            return
                new Decorator(
                    ret => requirements(ret)
                        && onLocation != null
                        && SpellManager.CanCast(spell)
                        && (StyxWoW.Me.Location.Distance(onLocation(ret)) <= SpellManager.Spells[spell].MaxRange || SpellManager.Spells[spell].MaxRange == 0)
                        && GameWorld.IsInLineOfSpellSight(StyxWoW.Me.Location, onLocation(ret)),
                    new Sequence(
                        new Action(ret =>
                        {
                            if (!SingularSettings.Debug)
                                Logger.Write("Casting {0} at location {1} @ {2:F1} yds", spell, onLocation(ret), onLocation(ret).Distance(StyxWoW.Me.Location));
                            else
                            {
                                WoWUnit unit = Unit.NearbyFriendlyPlayers.Where(f => f.IsAlive)
                                    .Union(Unit.NearbyUnfriendlyUnits)
                                    .OrderBy(u => u.Distance)
                                    .FirstOrDefault();

                                Logger.Write("Casting {0} at {1} @ {2:F1} yds (closest unit {3} {4:F1} yds away", spell, onLocation(ret), onLocation(ret).Distance(StyxWoW.Me.Location), unit.SafeName(), unit.Location.Distance(onLocation(ret)));
                            }
                        }),

                        new Action(ret => SpellManager.Cast(spell)),

                        new DecoratorContinue(
                            ctx => waitForSpell,
                            new WaitContinue(TimeSpan.FromMilliseconds(500),
                                ret => StyxWoW.Me.CurrentPendingCursorSpell != null, // && StyxWoW.Me.CurrentPendingCursorSpell.Name == spell,
                                new ActionAlwaysSucceed()
                                )
                            ),

                        new Action(ret => SpellManager.ClickRemoteLocation(onLocation(ret))),

                        // check for we are done status
                        new PrioritySelector(
                            // done if cursor doesn't have spell anymore
                            new Wait(TimeSpan.FromMilliseconds(500),
                                ret => StyxWoW.Me.CurrentPendingCursorSpell == null,
                                new ActionAlwaysSucceed()
                                ),

                            // otherwise cancel
                            new Action(ret =>
                            {
                                Logger.Write("/cancel {0} - did click {1} fail?  distance={2:F1} yds, loss={3}, face={4}",
                                    spell,
                                    onLocation(ret),
                                    StyxWoW.Me.Location.Distance(onLocation(ret)),
                                    GameWorld.IsInLineOfSpellSight(StyxWoW.Me.Location, onLocation(ret)),
                                    StyxWoW.Me.IsSafelyFacing(onLocation(ret))
                                    );
                                Lua.DoString("SpellStopTargeting()");
                                return RunStatus.Failure;
                            })
                            )
                        )
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
            return new PrioritySelector(ctx => Unit.ResurrectablePlayers.FirstOrDefault(u => !Blacklist.Contains(u)),
                new Decorator(ctx => ctx != null && SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds,
                    new Sequence(Cast(spellName, ctx => (WoWPlayer) ctx),
                        new Action(ctx => Blacklist.Add((WoWPlayer) ctx, TimeSpan.FromSeconds(30))))));
        }

        public static bool IsPlayerRessurectNeeded()
        {
            return Unit.ResurrectablePlayers.Any(u => !Blacklist.Contains(u));
        }

        #endregion
    }

    internal class SpellBlacklist
    {
        static readonly Dictionary<uint, BlacklistTime> SpellBlacklistDict = new Dictionary<uint, BlacklistTime>();
        static readonly Dictionary<string, BlacklistTime> SpellStringBlacklistDict = new Dictionary<string, BlacklistTime>();

        private SpellBlacklist()
        {
        }

        class BlacklistTime
        {
            public BlacklistTime(DateTime time, TimeSpan span)
            {
                TimeStamp = time;
                Duration = span;
            }
            public DateTime TimeStamp { get; private set; }
            public TimeSpan Duration { get; private set; }
        }

        static public bool Contains(uint spellID)
        {
            RemoveIfExpired(spellID);
            return SpellBlacklistDict.ContainsKey(spellID);
        }

        static public bool Contains(string spellName)
        {
            RemoveIfExpired(spellName);
            return SpellStringBlacklistDict.ContainsKey(spellName);
        }

        static public void Add(uint spellID, TimeSpan duration)
        {
            SpellBlacklistDict[spellID] = new BlacklistTime(DateTime.Now, duration);
        }

        static public void Add(string spellName, TimeSpan duration)
        {
            SpellStringBlacklistDict[spellName] = new BlacklistTime(DateTime.Now, duration);
        }

        static void RemoveIfExpired(uint spellID)
        {
            if (SpellBlacklistDict.ContainsKey(spellID) &&
                SpellBlacklistDict[spellID].TimeStamp + SpellBlacklistDict[spellID].Duration <= DateTime.Now)
            {
                SpellBlacklistDict.Remove(spellID);
            }
        }

        static void RemoveIfExpired(string spellName)
        {
            if (SpellStringBlacklistDict.ContainsKey(spellName) &&
                SpellStringBlacklistDict[spellName].TimeStamp + SpellStringBlacklistDict[spellName].Duration <= DateTime.Now)
            {
                SpellStringBlacklistDict.Remove(spellName);
            }
        }
    }
}