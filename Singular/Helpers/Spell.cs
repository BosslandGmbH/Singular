#define NO_LATENCY_ISSUES_WITH_GLOBAL_COOLDOWN
//#define HONORBUDDY_GCD_IS_WORKING
//#define USE_LUA_RANGECHECK
//#define USE_LUA_POWERCHECK

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
using Styx.Patchables;
using Styx.Common;

namespace Singular.Helpers
{
    enum LagTolerance
    {
        No = 0,
        Yes
    };

    enum FaceDuring
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
        // type casting method for context dereferencing within Spell class
        private static CastContext CastContext(this object ctx) { return (CastContext)ctx; }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        public static void LogCast(string sname, WoWUnit unit)
        {
            LogCast(sname, unit, unit.HealthPercent, unit.Distance);
        }

        public static void LogCast(string sname, WoWUnit unit, double health, double dist)
        {
            if (unit.IsMe)
                Logger.Write("Casting {0} on Me @ {1:F1}%", sname, health);
            else
                Logger.Write("Casting {0} on {1} @ {2:F1}% at {3:F1} yds", sname, unit.SafeName(), health, dist);
        }

        public static WoWDynamicObject GetGroundEffectBySpellId(int spellId)
        {
            return ObjectManager.GetObjectsOfType<WoWDynamicObject>().FirstOrDefault(o => o.SpellId == spellId);
        }

        public static bool IsStandingInGroundEffect(this WoWUnit unit, bool harmful = true)
        {
            foreach (var obj in ObjectManager.GetObjectsOfType<WoWDynamicObject>())
            {
                // We're standing in this.
                if (obj.Caster != null && obj.Caster.IsValid)
                {
                    if (!harmful && obj.Caster.IsFriendly && obj.Distance <= obj.Radius)
                        return true;
                    if (harmful && obj.Caster.IsHostile && obj.Distance <= obj.Radius)
                        return true;
                }
            }
            return false;
        }

        public static bool IsStandingInBadStuff(this WoWUnit unit)
        {
            foreach (var obj in ObjectManager.GetObjectsOfType<WoWDynamicObject>())
            {
                if (obj.Caster != null && obj.Caster.IsValid && obj.Caster.IsHostile && unit.Location.Distance(obj.Location) <= obj.Radius)
                {
                    Logger.Write(Color.White, "^Yikes: {0} is standing in {1}", unit.SafeName(), obj.Name);
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
                return HotkeyDirector.IsAoeEnabled && SingularSettings.Instance.AllowAOE;
            }
        }

        public static float MeleeDistance(this WoWUnit unit)
        {
            return Me.MeleeDistance(unit);
        }

        /// <summary>
        /// get melee distance between two units
        /// </summary>
        /// <param name="unit">unit</param>
        /// <param name="other">Me if null, otherwise second unit</param>
        /// <returns></returns>
        public static float MeleeDistance(this WoWUnit unit, WoWUnit other = null)
        {
            // abort if mob null
            if (unit == null)
                return 0;

            if (other == null)
            {
                if (unit.IsMe)
                    return 0;
                other = StyxWoW.Me;
            }

            // pvp, then keep it close
            if (unit.IsPlayer && other.IsPlayer)
                return 3.5f;

            return Math.Max(5f, other.CombatReach + 1.3333334f + unit.CombatReach);
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
        /// <param name="other">Me if null, otherwise second unit</param>
        /// <returns></returns>
        public static float SpellDistance(this WoWUnit unit, WoWUnit other = null)
        {
            // abort if mob null
            if (unit == null)
                return 0;

            // optional arg implying Me, then make sure not Mob also
            if (other == null)
                other = StyxWoW.Me;

            // pvp, then keep it close
            float dist = other.Location.Distance(unit.Location);
            dist -= other.CombatReach + unit.CombatReach;
            return Math.Max(0, dist);
        }

        /// <summary>
        /// get the  combined base spell and hitbox range of <c>unit</c>.
        /// </summary>
        /// <param name="unit">unit</param>
        /// <param name="baseSpellRange"></param>
        /// <param name="other">Me if null, otherwise second unit</param>
        /// <returns></returns>
        public static float SpellRange(this WoWUnit unit, float baseSpellRange, WoWUnit other = null)
        {
            // abort if mob null
            if (unit == null)
                return 0;

            // optional arg implying Me, then make sure not Mob also
            if (other == null)
                other = StyxWoW.Me;
            return baseSpellRange + other.CombatReach + unit.CombatReach;
        }

        public static TimeSpan GetSpellCastTime(string s)
        {
            SpellFindResults sfr;
            if (SpellManager.FindSpell(s, out sfr))
                return TimeSpan.FromMilliseconds((sfr.Override ?? sfr.Original).CastTime);
            return TimeSpan.Zero;
        }

        /// <summary>
        /// gets the current Cooldown remaining for the spell. indetermValue returned if spell not known
        /// </summary>
        /// <param name="spell">spell to retrieve cooldown for</param>
        /// <param name="indetermValue">value returned if spell not defined</param>
        /// <returns>TimeSpan representing cooldown remaining, indetermValue if spell unknown</returns>
        public static TimeSpan GetSpellCooldown(string spell, int indetermValue = int.MaxValue)
        {
            SpellFindResults sfr;
            if (SpellManager.FindSpell(spell, out sfr))
                return (sfr.Override ?? sfr.Original).CooldownTimeLeft;

            if (indetermValue == int.MaxValue)
                return TimeSpan.MaxValue;

            return TimeSpan.FromSeconds(indetermValue);
        }

        public static bool IsSpellOnCooldown(string castName)
        {
            SpellFindResults sfr;
            if (!SpellManager.FindSpell(castName, out sfr))
                return true;

            WoWSpell spell = sfr.Override ?? sfr.Original;
            if (Me.ChanneledCastingSpellId != 0)
                return true;

            uint num = StyxWoW.WoWClient.Latency * 2u;
            if (StyxWoW.Me.IsCasting && Me.CurrentCastTimeLeft.TotalMilliseconds > num)
                return true;

            if (spell.CooldownTimeLeft.TotalMilliseconds > num)
                return true;

            return false;
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
            // 0.1 margin for error
            return unit != null ? spell.MaxRange + unit.CombatReach + StyxWoW.Me.CombatReach : spell.MaxRange;
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

            // some code was using 1.66666675f instead of Me.CombatReach ?
            return unit != null ? spell.MinRange + unit.CombatReach + StyxWoW.Me.CombatReach : spell.MinRange;
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

        #region Fix HonorBuddys GCD Handling

#if HONORBUDDY_GCD_IS_WORKING
#else

        private static WoWSpell _gcdCheck = null;

        public static string FixGlobalCooldownCheckSpell
        {
            get
            {
                return _gcdCheck == null ? null : _gcdCheck.Name;
            }
            set
            {
                SpellFindResults sfr;
                if (!SpellManager.FindSpell(value, out sfr))
                {
                    _gcdCheck = null;
                    Logger.Write("GCD check fix spell {0} not known", value);
                }
                else
                {
                    _gcdCheck = sfr.Original;
                    Logger.Write("GCD check fix spell set to: {0}", value);
                }
            }
        }

#endif

        public static bool GcdActive
        {
            get
            {
#if HONORBUDDY_GCD_IS_WORKING
                return SpellManager.GlobalCooldown;
#else
                if (_gcdCheck == null)
                    return SpellManager.GlobalCooldown;

                return _gcdCheck.Cooldown;
#endif
            }
        }

        public static TimeSpan GcdTimeLeft
        {
            get
            {
#if HONORBUDDY_GCD_IS_WORKING
                return SpellManager.GlobalCooldownLeft;
#else
                try
                {
                    if (_gcdCheck != null)
                        return _gcdCheck.CooldownTimeLeft;
                }
                catch (System.AccessViolationException)
                {
                    Logger.WriteFile("GcdTimeLeft: handled access exception, reinitializing gcd spell");
                    GcdInitialize();
                }
                catch (Styx.InvalidObjectPointerException)
                {
                    Logger.WriteFile("GcdTimeLeft: handled invobj exception, reinitializing gcd spell");
                    GcdInitialize();
                }

                // use default value here (reinit should fix _gcdCheck for next call)
                return SpellManager.GlobalCooldownLeft;
#endif
            }
        }

        public static void GcdInitialize()
        {
#if HONORBUDDY_GCD_IS_WORKING
            Logger.WriteDebug("GcdInitialize: using HonorBuddy GCD");
#else
            Logger.WriteDebug("FixGlobalCooldownInitialize: using Singular GCD");
            switch (StyxWoW.Me.Class)
            {
                case WoWClass.DeathKnight:
                    FixGlobalCooldownCheckSpell = "Frost Presence";
                    break;
                case WoWClass.Druid:
                    FixGlobalCooldownCheckSpell = "Cat Form";
                    break;
                case WoWClass.Hunter:
                    FixGlobalCooldownCheckSpell = "Hunter's Mark";
                    break;
                case WoWClass.Mage:
                    FixGlobalCooldownCheckSpell = "Polymorph";
                    break;
                case WoWClass.Monk:
                    FixGlobalCooldownCheckSpell = "Stance of the Fierce Tiger";
                    break;
                case WoWClass.Paladin:
                    FixGlobalCooldownCheckSpell = "Righteous Fury";
                    break;
                case WoWClass.Priest:
                    FixGlobalCooldownCheckSpell = "Inner Fire";
                    break;
                case WoWClass.Rogue:
                    FixGlobalCooldownCheckSpell = "Sap";
                    break;
                case WoWClass.Shaman:
                    FixGlobalCooldownCheckSpell = "Lightning Shield";
                    break;
                case WoWClass.Warlock:
                    FixGlobalCooldownCheckSpell = "Corruption";
                    break;
                case WoWClass.Warrior:
                    FixGlobalCooldownCheckSpell = "Sunder Armor";
                    break;
            }

            if (FixGlobalCooldownCheckSpell != null)
                return;

            switch (StyxWoW.Me.Class)
            {
                case WoWClass.DeathKnight:
                    // FixGlobalCooldownCheckSpell = "";
                    break;
                case WoWClass.Druid:
                    FixGlobalCooldownCheckSpell = "Wrath";
                    break;
                case WoWClass.Hunter:
                    FixGlobalCooldownCheckSpell = "Arcane Shot";
                    break;
                case WoWClass.Mage:
                    FixGlobalCooldownCheckSpell = "Frostfire Bolt";
                    break;
                case WoWClass.Monk:
                    //FixGlobalCooldownCheckSpell = "";
                    break;
                case WoWClass.Paladin:
                    FixGlobalCooldownCheckSpell = "Seal of Command";
                    break;
                case WoWClass.Priest:
                    FixGlobalCooldownCheckSpell = "Smite";
                    break;
                case WoWClass.Rogue:
                    FixGlobalCooldownCheckSpell = "Sinister Strike";
                    break;
                case WoWClass.Shaman:
                    FixGlobalCooldownCheckSpell = "Lightning Bolt";
                    break;
                case WoWClass.Warlock:
                    FixGlobalCooldownCheckSpell = "Shadow Bolt";
                    break;
                case WoWClass.Warrior:
                    FixGlobalCooldownCheckSpell = "Heroic Strike";
                    break;
            }
#endif
        }

        #endregion

        #region Wait

        public static Composite WaitForGlobalCooldown(FaceDuring faceDuring = FaceDuring.No, LagTolerance allow = LagTolerance.Yes)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => faceDuring == FaceDuring.Yes,
                    Movement.CreateFaceTargetBehavior()
                    ),
                new Action(ret =>
                {
                    if (IsGlobalCooldown(allow))
                        return RunStatus.Success;

                    return RunStatus.Failure;
                })
                );
        }

        public static bool IsGlobalCooldown(LagTolerance allow = LagTolerance.Yes)
        {
#if NO_LATENCY_ISSUES_WITH_GLOBAL_COOLDOWN
            uint latency = allow == LagTolerance.Yes ? StyxWoW.WoWClient.Latency : 0;
            TimeSpan gcdTimeLeft = Spell.GcdTimeLeft;
            return gcdTimeLeft.TotalMilliseconds > latency;
#else
            return Spell.FixGlobalCooldown;
#endif
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
        public static Composite WaitForCast(FaceDuring faceDuring = FaceDuring.No, LagTolerance allow = LagTolerance.Yes)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => faceDuring == FaceDuring.Yes,
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
        public static Composite WaitForChannel(FaceDuring faceDuring = FaceDuring.No, LagTolerance allow = LagTolerance.Yes)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => faceDuring == FaceDuring.Yes,
                    Movement.CreateFaceTargetBehavior()
                    ),
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

        public static Composite WaitForCastOrChannel(FaceDuring faceDuring = FaceDuring.No, LagTolerance allow = LagTolerance.Yes)
        {
            return new PrioritySelector(
                WaitForCast(faceDuring, allow),
                WaitForChannel(faceDuring, allow)
                );
        }

        public static Composite WaitForGcdOrCastOrChannel(FaceDuring faceDuring = FaceDuring.No, LagTolerance allow = LagTolerance.Yes)
        {
            return new PrioritySelector(
                WaitForGlobalCooldown(faceDuring,allow),
                WaitForCast(faceDuring, allow),
                WaitForChannel(faceDuring, allow)
                );
        }

        #endregion

        #region OffGCD Composite

        /// <summary>
        /// wraps a composite so that if continues immediately even if it succeeds by
        /// forcing it to always return RunStatus.Failure
        /// </summary>
        /// <param name="tree"></param>
        /// <returns></returns>
        public static Composite OffGCD(Composite tree)
        {
            return new Sequence(
                new Throttle( TimeSpan.FromMilliseconds(500), tree ),
                new ActionAlwaysFail()
                );
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
            return Cast(sp => name);
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
            return Cast(sp => name, requirements);
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
            return Cast(sp => name, onUnit);
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
            return Cast(id => spellId, onUnit, requirements);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by ID, with special requirements, on a specific unit.
        ///   Returns RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <param name = "spellId">Identifier for the spell.</param>
        /// <param name = "onUnit">The on unit.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite Cast(SimpleIntDelegate spellId, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new Decorator(ret => requirements != null && onUnit != null,
                new Throttle(
                    new Action(ret =>
                    {
                        SpellFindResults sfr;
                        if (SpellManager.FindSpell(spellId(ret), out sfr))
                        {
                            WoWUnit castOnUnit = onUnit(ret);
                            if (castOnUnit != null && requirements(ret))
                            {
                                if (Spell.CanCastHack(sfr, castOnUnit, skipWowCheck: true))
                                {
                                    WoWSpell spell = sfr.Override ?? sfr.Original;
                                    LogCast(spell.Name, castOnUnit);
                                    if (SpellManager.Cast(spell, castOnUnit))
                                    {
                                        return RunStatus.Success;
                                    }
                                }
                            }
                        }

                        return RunStatus.Failure;
                    })
                )
            );
        }

        #endregion

        #region Buff DoubleCast prevention mechanics

        public static string DoubleCastKey(ulong guid, string spellName)
        {
            return guid.ToString("X") + "-" + spellName;
        }

        public static string DoubleCastKey(WoWUnit unit, string spell)
        {
            return DoubleCastKey(unit.Guid, spell);
        }

        public static bool Contains(this Dictionary<string, DateTime> dict, WoWUnit unit, string spellName)
        {
            return dict.ContainsKey(DoubleCastKey(unit, spellName));
        }

        public static bool ContainsAny(this Dictionary<string, DateTime> dict, WoWUnit unit, params string[] spellNames)
        {
            return spellNames.Any(s => dict.ContainsKey(DoubleCastKey(unit, s)));
        }

        public static bool ContainsAll(this Dictionary<string, DateTime> dict, WoWUnit unit, params string[] spellNames)
        {
            return spellNames.All(s => dict.ContainsKey(DoubleCastKey(unit, s)));
        }

        public static readonly Dictionary<string, DateTime> DoubleCastPreventionDict =
            new Dictionary<string, DateTime>();

        #endregion

        #region Buff - by name

        public static Composite Buff(string name)
        {
            return Buff(name, ret => true);
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
        public static Composite Buff(string name, bool myBuff, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements, params string[] buffNames)
        {
            return Buff(sp => name, myBuff, onUnit, requirements, buffNames);
        }

        private static string _buffName { get; set; }
        private static WoWUnit _buffUnit { get; set; }

        public static Composite Buff(SimpleStringDelegate name, bool myBuff, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements, params string[] buffNames)
        {
            return new Decorator(
                ret =>
                {
                    if (onUnit == null || name == null || requirements == null)
                        return false;

                    _buffUnit = onUnit(ret);
                    if (_buffUnit == null)
                        return false;

                    _buffName = name(ret);
                    if (_buffName == null)
                        return false;

                    if (DoubleCastPreventionDict.Contains(_buffUnit, _buffName))
                        return false;

                    if (!buffNames.Any())
                        return !(myBuff ? _buffUnit.HasMyAura(_buffName) : _buffUnit.HasAura(_buffName));

                    bool buffFound;
                    try
                    {
                        if (myBuff)
                            buffFound = buffNames.Any(b => _buffUnit.HasMyAura(b));
                        else
                            buffFound = buffNames.Any(b => _buffUnit.HasAura(b));
                    }
                    catch
                    {
                        // mark as found buff, so we return false
                        buffFound = true;
                    }

                    return !buffFound;
                },
                new Sequence(
                // new Action(ctx => _lastBuffCast = name),
                    Cast( sp => _buffName, chkMov => true, on => _buffUnit, requirements, cancel => false /* causes cast to complete */ ),
                    new Action(ret => UpdateDoubleCastDict(_buffName, _buffUnit))
                    )
                );
        }


        public static Composite Buff(string name, bool myBuff, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements, int expirSecs, params string[] buffNames)
        {
            return Buff(sp => name, myBuff, onUnit, requirements, expirSecs, buffNames);
        }


        public static Composite Buff(SimpleStringDelegate name, bool myBuff, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements, int expirSecs, params string[] buffNames)
        {
            return new Decorator(
                ret =>
                {
                    if (onUnit == null || name == null || requirements == null)
                        return false;

                    _buffUnit = onUnit(ret);
                    if (_buffUnit == null)
                        return false;

                    _buffName = name(ret);
                    if (_buffName == null)
                        return false;

                    if (DoubleCastPreventionDict.Contains(_buffUnit, _buffName))
                        return false;

                    bool hasExpired;
                    if (!buffNames.Any())
                    {
                        hasExpired = _buffUnit.HasAuraExpired(_buffName, expirSecs, myBuff);
                        if (hasExpired)
                            Logger.WriteDebug("Spell.Buff(r=>'{0}'): hasspell={1}, auraleft={2:F1} secs", _buffName, SpellManager.HasSpell(_buffName).ToYN(), _buffUnit.GetAuraTimeLeft(_buffName, true).TotalSeconds);

                        return hasExpired;
                    }

                    hasExpired = SpellManager.HasSpell(_buffName) && buffNames.All(b => _buffUnit.HasKnownAuraExpired(b, expirSecs, myBuff));
                    if (hasExpired)
                        Logger.WriteDebug("Spell.Buff(r=>'{0}'): hasspell={1}, all auras less than {2:F1} secs", _buffName, SpellManager.HasSpell(_buffName).ToYN(), expirSecs );

                    return hasExpired;
                },
                new Sequence(
                // new Action(ctx => _lastBuffCast = name),
                    Cast(name, chkMov => true, onUnit, requirements, cancel => false /* causes cast to complete */ ),
                    new Action(ret => UpdateDoubleCastDict(name(ret), onUnit(ret)))
                    )
                );
        }


        public static void UpdateDoubleCastDict(string spellName, WoWUnit unit)
        {
            if (unit == null)
                return;

            DateTime expir = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            string key = DoubleCastKey(unit.Guid, spellName);
            if (DoubleCastPreventionDict.ContainsKey(key))
                DoubleCastPreventionDict[key] = expir;

            DoubleCastPreventionDict.Add(key, expir);
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
            return Buff(name, false, on => Me, req => true);
        }

        public static Composite BuffSelf(string name, int expirSecs)
        {
            return Buff(name, false, on => Me, req => true, expirSecs);
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
            return Buff(name, false, on => Me, requirements);
        }

        public static Composite BuffSelf(string name, SimpleBooleanDelegate requirements, int expirSecs)
        {
            return Buff(name, false, on => Me, requirements, expirSecs);
        }

        public static Composite BuffSelf(SimpleStringDelegate name, SimpleBooleanDelegate requirements)
        {
            return Buff(name, false, on => Me, requirements);
        }

        public static Composite BuffSelf(SimpleStringDelegate name, SimpleBooleanDelegate requirements, int expirSecs)
        {
            return Buff(name, false, on => Me, requirements, expirSecs);
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

        // private static WoWSpell _spell;

        // used by Spell.Cast() - save fact we are queueing this Heal spell if a spell cast/gcd is in progress already.  this could only occur during 
        // .. the period of latency at the end of a cast where Singular allows you to begin the next one

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
        public static Composite Cast(string name, UnitSelectionDelegate onUnit,
            SimpleBooleanDelegate requirements, SimpleBooleanDelegate cancel = null, LagTolerance allow = LagTolerance.Yes)
        {
            return Cast(n => name, mov => true, onUnit, requirements, cancel, allow);
        }

        public static Composite Cast(string name, SimpleBooleanDelegate checkMovement, UnitSelectionDelegate onUnit,
            SimpleBooleanDelegate requirements, SimpleBooleanDelegate cancel = null, LagTolerance allow = LagTolerance.Yes)
        {
            return Cast(n => name, checkMovement, onUnit, requirements, cancel, allow);
        }

        /// <summary>
        ///   Creates a behavior to cast a spell by name, with special requirements, on a specific unit. Will make sure any spell with
        ///   a non-zero cast time (everything not instant) will stay here until passing the latency boundary (point where .IsCasting == false while cast is in progress.)
        ///   Returns RunStatus.Success if successful, RunStatus.Failure otherwise.  Note: will return as soon as spell cast is in progress, unless cancel delegate provided
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
            SimpleBooleanDelegate requirements, SimpleBooleanDelegate cancel = null, LagTolerance allow = LagTolerance.Yes, bool skipWowCheck = false)
        {
            return new Decorator(
                ret => name != null && checkMovement != null && onUnit != null && requirements != null && name(ret) != null,
                new Throttle(
                    new PrioritySelector(

                        // create a CastContext object to save passed in context and other values
                        ctx => new CastContext(onUnit(ctx), ctx),

                        new Sequence(
                            // save flag indicating if currently in a GCD or IsCasting before queueing our cast
                            new Action(ret =>
                            {
                                CastContext cctx = ret.CastContext();
                                cctx.spell = null;
                                if (cctx.unit == null)
                                    return RunStatus.Failure;

                                if (!requirements(cctx.context))
                                    return RunStatus.Failure;

                                // find spell 
                                cctx.name = name(cctx.context);
                                SpellFindResults sfr;
                                if (!SpellManager.FindSpell(cctx.name, out sfr))
                                    return RunStatus.Failure;

                                cctx.spell = sfr.Override ?? sfr.Original;

                                if (checkMovement(cctx.context) && Me.IsMoving && !AllowMovingWhileCasting(cctx.spell))
                                {
                                    if (SingularSettings.Instance.EnableDebugLoggingGCD)
                                        Logger.WriteDebug("skipping Spell.Cast({0},[{1}]) because we are moving", cctx.unit.SafeName(), cctx.spell.Name);
                                    return RunStatus.Failure;
                                }

                                // check we can cast it on target without checking for movement
                                // if (!Spell.CanCastHack(_spell, cctx.unit, true, false, allow == LagTolerance.Yes))
                                if (!CanCastHack(cctx.name, cctx.unit, skipWowCheck))
                                {
                                    if (SingularSettings.Instance.EnableDebugLoggingGCD)
                                        Logger.WriteDebug("skipping Spell.Cast({0},[{1}]) because CanCastHack failed", cctx.unit.SafeName(), cctx.spell.Name);
                                    return RunStatus.Failure;
                                }

                                // save status of queueing spell (lag tolerance - the prior spell still completing)
                                cctx.IsSpellBeingQueued = allow == LagTolerance.Yes && (Spell.GcdActive || StyxWoW.Me.IsCasting || StyxWoW.Me.IsChanneling);

                                LogCast(cctx.spell.Name, cctx.unit, cctx.health, cctx.distance);
                                if (!SpellManager.Cast(cctx.spell, cctx.unit))
                                {
                                    Logger.WriteDebug(Color.LightPink, "cast of {0} on {1} failed!", cctx.spell.Name, cctx.unit.SafeName());
                                    return RunStatus.Failure;
                                }

                                SingularRoutine.UpdateDiagnosticCastingState();
                                return RunStatus.Success;
                            }),
#if OLD_WAY_OF_ENSURING
                            // when accountForLag = true, wait for in progress spell (if any) to complete
                            new WaitContinue(
                                TimeSpan.FromMilliseconds(500),
                                ret => SingularRoutine.UpdateDiagnosticCastingState(false) || !_IsSpellBeingQueued || !(Spell.GcdActive || StyxWoW.Me.IsCasting || StyxWoW.Me.IsChanneling),
                                new ActionAlwaysSucceed()
                                ),

                            // new Action(r => Logger.WriteDebug("Spell.Cast(\"{0}\"): waited for queued spell {1}", name(r), _IsSpellBeingQueued )),

                            // failsafe: max time we should be waiting with the prior and latter WaitContinue is latency x 2
                // .. if system is borked, could be 1 second but shouldnt notice.  
                // .. instant spells should be very quick since only prior wait applies

                            // now for non-instant spell, wait for .IsCasting to be true
                            new WaitContinue(
                                TimeSpan.FromMilliseconds(300),
                                ret =>
                                {
                                    SingularRoutine.UpdateDiagnosticCastingState();

                                    SpellFindResults sfr;
                                    if (SpellManager.FindSpell(name(ret), out sfr))
                                    {
                                        WoWSpell spell = sfr.Override ?? sfr.Original;
                                        if (spell.CastTime == 0 && !IsFunnel(spell))
                                        {
                                            return true;
                                        }
                                    }

                                    return StyxWoW.Me.IsCasting || StyxWoW.Me.IsChanneling;
                                },
                                new ActionAlwaysSucceed()
                                ),

                            /// new Action(r => Logger.WriteDebug("Spell.Cast(\"{0}\"): assume we are casting (actual={1}, gcd={2})", name(r), StyxWoW.Me.IsCasting || StyxWoW.Me.IsChanneling, Spell.GlobalCooldown )),
#else
                            // for instant spell, wait for GCD to start
                            // for non-instant spell, wait for .IsCasting / .IsChanneling to start
                            new WaitContinue(
                                TimeSpan.FromMilliseconds(350),
                                ret =>
                                {
                                    CastContext cctx = ret.CastContext();
                                    if ((cctx.spell.CastTime == 0 && Spell.GcdTimeLeft.TotalMilliseconds > 750) || Me.CurrentCastTimeLeft.TotalMilliseconds > 750 || Me.CurrentChannelTimeLeft.TotalMilliseconds > 750)
                                        return true;

                                    return false;
                                },
                                new ActionAlwaysSucceed()
                                ),
#endif

                            // now check for one of the possible done casting states
                            new PrioritySelector(

                                // for instant or no cancel method given, we are done
                                new Decorator(
                                    ret => cancel == null || ret.CastContext().spell.CastTime == 0,
                                    new Action(r =>
                                    {
                                        // Logger.WriteDebug("Spell.Cast(\"{0}\"): no cancel delegate", cctx.spell.Name);
                                        return RunStatus.Success;
                                    })
                                    ),

                                // while casting/channeling call the cancel method to see if we should abort
                                new Wait(12,
                                    ret => {
                                        CastContext cctx = ret.CastContext();
                                        SingularRoutine.UpdateDiagnosticCastingState();

                                        // Interrupted or finished casting. 
                                        if (!Spell.IsCastingOrChannelling(allow))
                                        {
                                            Logger.WriteDebug("Spell.Cast(\"{0}\"): cast has ended", cctx.spell.Name);
                                            return true;
                                        }

                                        // check cancel delegate if we are finished
                                        if (cancel(cctx.context))
                                        {
                                            SpellManager.StopCasting();
                                            Logger.Write(System.Drawing.Color.Orange, "/cancel {0} on {1} @ {2:F1}%", cctx.spell.Name, cctx.unit.SafeName(), cctx.unit.HealthPercent);
                                            return true;
                                        }
                                        // continue casting/channeling at this point
                                        return false;
                                        },
                                    new ActionAlwaysSucceed()
                                    ),

                                // if we are here, we timed out after 12 seconds (very odd)
                                new Action(r => {
                                    CastContext cctx = r.CastContext();
                                    Logger.WriteDebug("Spell.Cast(\"{0}\"): timed out waiting", cctx.spell.Name);
                                    return RunStatus.Success;
                                    })

                                ),

                                // made it this far the we are RunStatus.Success, so reset wowunit reference and return
                                new Action(ret =>
                                {
                                    CastContext cctx = ret.CastContext();
                                    cctx.unit = null;
                                    cctx.spell = null;
                                    return RunStatus.Success;
                                })
                            ),

                        // cast Sequence failed, so only thing left is to reset cached references and report failure
                        new Action(ret =>
                        {
                            CastContext cctx = ret.CastContext();
                            cctx.unit = null;
                            cctx.spell = null;
                            return RunStatus.Failure;
                        })
                        )
                    )
                );
        }

        /// <summary>
        /// cached result of onUnit delegate for Spell.Cast.  for expensive queries (such as Cluster.GetBestUnitForCluster()) we want to avoid
        /// performing them multiple times.  in some cases we were caching that locally in the context parameter of a wrapping PrioritySelector
        /// but doing it here enforces for all calls, so will reduce list scans and cycles required even for targets selected by auras present/absent
        /// </summary>
        // private static WoWUnit _castOnUnit;
        // private static WoWSpell _castSpell;

        /// <summary>
        /// checked if the spell has an instant cast, the spell is one which can be cast while moving, or we have an aura active which allows moving without interrupting casting.  
        /// does not check whether you are presently moving, only whether you could cast if you are moving
        /// </summary>
        /// <param name="spell">spell to cast</param>
        /// <returns>true if spell can be cast while moving, false if it cannot</returns>
        private static bool AllowMovingWhileCasting(WoWSpell spell)
        {
            // quick return for instant spells
            if (spell.CastTime == 0 && !IsFunnel(spell))
                return true;

            // assume we cant do that, but then check for class specific buffs which allow movement while casting
            bool allowMovingWhileCasting = false;
            if (Me.Class == WoWClass.Shaman)
                allowMovingWhileCasting = spell.Name == "Lightning Bolt";
            else if (Me.Specialization == WoWSpec.MageFire)
                allowMovingWhileCasting = spell.Name == "Scorch";
            else if (Me.Class == WoWClass.Hunter)
                allowMovingWhileCasting = spell.Name == "Steady Shot" || (spell.Name == "Aimed Shot" && TalentManager.HasGlyph("Aimed Shot")) || spell.Name == "Cobra Shot";
            else if (Me.Class == WoWClass.Warlock)
                allowMovingWhileCasting = (spell.Name == "Incinerate" || spell.Name == "Malefic Grasp" || spell.Name == "Shadow Bolt") && ClassSpecific.Warlock.Common.HasTalent(ClassSpecific.Warlock.WarlockTalents.KiljadensCunning);

            //            if (!allowMovingWhileCasting && Me.ZoneId == 5723)
            //                allowMovingWhileCasting = Me.HasAura("Molten Feather");

            if (!allowMovingWhileCasting)
            {
                allowMovingWhileCasting = HaveAllowMovingWhileCastingAura(spell);

                // we will atleast check spell cooldown... we may still end up wasting buff, but this reduces the chance
                if (!allowMovingWhileCasting && spell.CooldownTimeLeft == TimeSpan.Zero )
                {
                    bool castSuccess = CastBuffToAllowCastingWhileMoving();
                    if (castSuccess)
                        allowMovingWhileCasting = HaveAllowMovingWhileCastingAura();
                }
            }

            return allowMovingWhileCasting;
        }

        /// <summary>
        /// will cast class/specialization specific buff to allow moving without interrupting casting
        /// </summary>
        /// <returns>true if able to cast, false otherwise</returns>
        private static bool CastBuffToAllowCastingWhileMoving()
        {
            string spell = null;
            bool allowMovingWhileCasting = false;

            if (SingularSettings.Instance.UseCastWhileMovingBuffs)
            {
                if (Me.Class == WoWClass.Shaman)
                    spell = "Spiritwalker's Grace";
                else if (Me.Class == WoWClass.Mage)
                    spell = "Ice Floes";

                if (spell != null && CanCastHack(spell, Me)) // Spell.CanCastHack(spell, Me))
                {
                    LogCast(spell, Me);
                    allowMovingWhileCasting = SpellManager.Cast(spell, Me);
                    if (!allowMovingWhileCasting)
                        Logger.WriteDebug("spell cast failed!!! [{0}]", spell);
                }
            }

            return allowMovingWhileCasting;
        }

        /// <summary>
        /// check for aura which allows moving without interrupting spell casting
        /// </summary>
        /// <returns></returns>
        public static bool HaveAllowMovingWhileCastingAura(WoWSpell spell = null)
        {
            return Me.GetAllAuras().Any(a => a.ApplyAuraType == (WoWApplyAuraType)330 && (spell == null || spell.CastTime < (uint)a.TimeLeft.TotalMilliseconds));
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
        /// Creates a behavior to cast an on ground spell by name on the location occupied by the specified unit
        /// </summary>
        /// <param name="spell"></param>
        /// <param name="onUnit"></param>
        /// <param name="requirements"></param>
        /// <param name="waitForSpell"></param>
        /// <returns></returns>
        public static Composite CastOnGround(string spell, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements, bool waitForSpell = true)
        {
            return new Decorator(
                ret => onUnit != null,
                new Sequence(
                    ctx => new CastContext( onUnit(ctx), ctx),
                    new PrioritySelector(
                        new Decorator(
                            ret => ret.CastContext().unit != null && ret.CastContext().unit.Distance < Spell.ActualMaxRange(spell, null),
                            CastOnGround(spell, loc => loc.CastContext().unit.Location, requirements, waitForSpell, desc => string.Format("{0} @ {1:F1}%", desc.CastContext().unit.SafeName(), desc.CastContext().unit.HealthPercent))
                            ),
                        new Action(r => { return RunStatus.Failure; })
                        )
                    )
                );
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
            SimpleBooleanDelegate requirements, bool waitForSpell = true, SimpleStringDelegate targetDesc = null)
        {
            return
                new Decorator(
                    ret => requirements(ret)
                        && onLocation != null
                        && Spell.CanCastHack(spell, null, skipWowCheck:true)
                        && LocationInRange(spell, onLocation(ret))
                        && GameWorld.IsInLineOfSpellSight(StyxWoW.Me.GetTraceLinePos(), onLocation(ret)),
                    new Sequence(
                        new Action(ret => Logger.Write("Casting {0} {1}at location {2} at {3:F1} yds", spell, targetDesc == null ? "" : "on " + targetDesc(ret) + " ", onLocation(ret), onLocation(ret).Distance(StyxWoW.Me.Location))),

                        new Action(ret => { return SpellManager.Cast(spell) ? RunStatus.Success : RunStatus.Failure; } ),

                        new DecoratorContinue(
                            ctx => waitForSpell,
                            new PrioritySelector(
                                new WaitContinue(1,
                                    ret => GetPendingCursorSpell != null && GetPendingCursorSpell.Name == spell,
                                    new ActionAlwaysSucceed()
                                    ),
                                new Action(r =>
                                {
                                    Logger.WriteDebug("error: spell {0} not seen as pending on cursor after 1 second", spell);
                                    return RunStatus.Failure;
                                })
                                )
                            ),

                        new Action(ret => SpellManager.ClickRemoteLocation(onLocation(ret))),

                        // check for we are done status
                        new PrioritySelector(
                // done if cursor doesn't have spell anymore
                            new Decorator(
                                ret => !waitForSpell,
                                new Action(r => Lua.DoString("SpellStopTargeting()"))   //just in case
                                ),

                            new Wait(TimeSpan.FromMilliseconds(750),
                                ret => Spell.GetPendingCursorSpell == null || Me.IsCasting || Me.IsChanneling,
                                new ActionAlwaysSucceed()
                                ),

                            // otherwise cancel
                            new Action(ret =>
                            {
                                Logger.WriteDebug("/cancel {0} - click {1} failed -OR- Pending Cursor Spell API broken -- distance={2:F1} yds, loss={3}, face={4}",
                                    spell,
                                    onLocation(ret),
                                    StyxWoW.Me.Location.Distance(onLocation(ret)),
                                    GameWorld.IsInLineOfSpellSight(StyxWoW.Me.GetTraceLinePos(), onLocation(ret)),
                                    StyxWoW.Me.IsSafelyFacing(onLocation(ret))
                                    );

                                // Pending Spell Cursor API is broken... seems like we can't really check at this point, so assume it failed and worked... uggghhh
                                Lua.DoString("SpellStopTargeting()");
                                return RunStatus.Failure;
                            })
                            )
                        )
                    );
        }

        private static bool LocationInRange(string spellName, WoWPoint loc)
        {
            SpellFindResults sfr;
            if (SpellManager.FindSpell(spellName, out sfr))
            {
                WoWSpell spell = sfr.Override ?? sfr.Original;
                if (spell.HasRange)
                {
                    return spell.MinRange <= Me.Location.Distance(loc) && Me.Location.Distance(loc) < spell.MaxRange;
                }
            }

            return false;
        }

        #endregion

        #region Cast Hack - allows casting spells that CanCast returns False

        public static bool CanCastHack(string castName)
        {
            return CanCastHack(castName, Me.CurrentTarget, skipWowCheck: false);
        }

        /// <summary>
        /// CastHack following done because CanCast() wants spell as "Metamorphosis: Doom" while Cast() and aura name are "Doom"
        /// </summary>
        /// <param name="castName"></param>
        /// <param name="onUnit"></param>
        /// <param name="requirements"></param>
        /// <returns></returns>
        public static bool CanCastHack(string castName, WoWUnit unit, bool skipWowCheck = false)
        {
            SpellFindResults sfr;
            if (!SpellManager.FindSpell(castName, out sfr))
            {
                // Logger.WriteDebug("CanCast: spell [{0}] not known", castName);
                return false;
            }

            return CanCastHack(sfr, unit, skipWowCheck);
        }

        /// <summary>
        /// CastHack following done because CanCast() wants spell as "Metamorphosis: Doom" while Cast() and aura name are "Doom"
        /// </summary>
        /// <param name="castName"></param>
        /// <param name="onUnit"></param>
        /// <param name="requirements"></param>
        /// <returns></returns>
        public static bool CanCastHack(SpellFindResults sfr, WoWUnit unit, bool skipWowCheck = false)
        {
            WoWSpell spell = sfr.Override ?? sfr.Original;
            
            // check range
            if (unit != null && !spell.IsSelfOnlySpell && !unit.IsMe)
            {
#if USE_LUA_RANGECHECK
                // will exercise the IsSpellInRange LUA if it can easily
                // .. derive a UnitID for the target

                string sTarget = null;

                if (unit.Guid == Me.CurrentTargetGuid)
                    sTarget = "target";
                else if (unit.IsPlayer && unit.ToPlayer().IsInMyPartyOrRaid)
                    sTarget = unit.Name;
                else if (unit.IsPet && unit.OwnedByUnit != null && unit.OwnedByUnit.IsPlayer && unit.OwnedByUnit.ToPlayer().IsInMyPartyOrRaid)
                    sTarget = unit.OwnedByUnit.Name + "-pet";
                else if (Me.GotAlivePet)
                {
                    if (unit.Guid == Me.Pet.Guid)
                        sTarget = "pet";
                    else if (unit.Guid == Me.Pet.CurrentTargetGuid)
                        sTarget = "pettarget";
                }

                if (sTarget != null)
                {
                    // 
                    string lua = string.Format("return IsSpellInRange(\"{0}\",\"{1}\")", spell.Name, sTarget);
                    string inRange = Lua.GetReturnVal<string>(lua, 0);
                    if (inRange != "1")
                    {
                        if (SingularSettings.DebugSpellCanCast)
                            Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: target @ {1:F1} yds failed IsSpellInRange() = {2}", spell.Name, unit.Distance, inRange);
                        return false;
                    }
                }
                else 
#endif
                {
                    if (spell.IsMeleeSpell && !unit.IsWithinMeleeRange)
                    {
                        if (SingularSettings.DebugSpellCanCast)
                            Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: target @ {1:F1} yds not in melee range", spell.Name, unit.Distance);
                        return false;
                    }
                    else if (spell.HasRange)
                    {
                        if (unit.Distance > spell.ActualMaxRange(unit))
                        {
                            if (SingularSettings.DebugSpellCanCast)
                                Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: out of range - further than {1:F1}", spell.Name, spell.ActualMaxRange(unit));
                            return false;
                        }
                        if (unit.Distance < spell.ActualMinRange(unit))
                        {
                            if (SingularSettings.DebugSpellCanCast)
                                Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: out of range - closer than {1:F1}", spell.Name, spell.ActualMinRange(unit));
                            return false;
                        }
                    }
                }

                if (!unit.InLineOfSpellSight)
                {
                    if (SingularSettings.DebugSpellCanCast)
                        Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: not in spell line of {1}", spell.Name, unit.SafeName());
                    return false;
                }
            }

            if ((spell.CastTime != 0u || IsFunnel(spell)) && Me.IsMoving && !AllowMovingWhileCasting(spell))
            {
                if (SingularSettings.DebugSpellCanCast)
                    Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: cannot cast while moving", spell.Name);
                return false;
            }

            if (Me.ChanneledCastingSpellId == 0)
            {
                uint lat = StyxWoW.WoWClient.Latency * 2u;
                if (StyxWoW.Me.IsCasting && Me.CurrentCastTimeLeft.TotalMilliseconds > lat)
                {
                    if (SingularSettings.DebugSpellCanCast)
                        Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: current cast of {1} has {2:F0} ms left", spell.Name, Me.CurrentCastId, Me.CurrentCastTimeLeft.TotalMilliseconds - lat);
                    return false;
                }

                if (spell.CooldownTimeLeft.TotalMilliseconds > lat)
                {
                    if (SingularSettings.DebugSpellCanCast)
                        Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: still on cooldown for {1:F0} ms", spell.Name, spell.CooldownTimeLeft.TotalMilliseconds - lat);
                    return false;
                }
            }

#if USE_LUA_POWERCHECK
            string usablecmd = string.Format("return IsUsableSpell(\"{0}\")", spell.Name);
            List<string> ret = Lua.GetReturnValues(usablecmd);
            if ( ret == null || !ret.Any())
            {
                if (SingularSettings.DebugSpellCanCast)
                    Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: IsUsable check failed with null", spell.Name);
                return false;
            }

            if (ret[0] != "1")
            {
                if (SingularSettings.DebugSpellCanCast)
                {
                    uint currentPower = Me.CurrentPower;
                    string ptype = Me.PowerType.ToString();

                    if (Me.Class == WoWClass.Druid)
                    {
                        if (Me.Shapeshift == ShapeshiftForm.Cat || Me.Shapeshift == ShapeshiftForm.Bear || Me.Shapeshift == ShapeshiftForm.DireBear)
                        {
                            if (Me.HealingSpellIds.Contains(spell.Id))
                            {
                                ptype = "Mana";
                                currentPower = Me.CurrentMana;
                            }
                            else if (spell.PowerCost >= 100)
                            {
                                ptype = "Mana";
                                currentPower = Me.CurrentMana;
                            }
                        }
                    }
                    
                    if (ret.Count() > 1 && ret[1] == "1")
                        Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: insufficient power ({1} cost={2} have={3})", spell.Name, ptype, spell.PowerCost, currentPower);
                    else
                        Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: not usable atm ({1} cost={2} have={3})", spell.Name, ptype, spell.PowerCost, currentPower);
                }

                return false;
            }

#else
            bool formSwitch = false;
            uint currentPower = Me.CurrentPower;
            if (Me.Class == WoWClass.Druid)
            {
                if (Me.Shapeshift == ShapeshiftForm.Cat || Me.Shapeshift == ShapeshiftForm.Bear || Me.Shapeshift == ShapeshiftForm.DireBear )
                {
                    if ( Me.HealingSpellIds.Contains( spell.Id))
                    {
                        formSwitch = true;
                        currentPower = Me.CurrentMana;
                    }
                    else if (spell.PowerCost >= 100)
                    {
                        formSwitch = true;
                        currentPower = Me.CurrentMana;
                    }
                }
            }

            if (currentPower < (uint) spell.PowerCost)
            {
                if (SingularSettings.DebugSpellCanCast)
                    Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: insufficient power (need {1:F0}, have {2:F0} {3})", spell.Name, spell.PowerCost, currentPower, formSwitch ? "Mana in Form" : Me.PowerType.ToString() );
                return false;
            }
#endif
            if (SingularSettings.Instance.DisableSpellsWithCooldown != 0)
            {
                int baseCooldown = GetBaseCooldown(spell);
                if (baseCooldown >= SingularSettings.Instance.DisableSpellsWithCooldown * 1000)
                {
                    if (SingularSettings.DebugSpellCanCast)
                        Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: basecooldown of {0} exceeds max allowed user setting of {1} ms", baseCooldown, SingularSettings.Instance.DisableSpellsWithCooldown * 1000);
                    return false;
                }
            }

            // override spell will sometimes always have cancast=false, so check original also
            if (!skipWowCheck && !spell.CanCast && (sfr.Override == null || !sfr.Original.CanCast))
            {
                if (SingularSettings.DebugSpellCanCast)
                    Logger.WriteFile(LogLevel.Diagnostic, "CanCast[{0}]: spell specific CanCast failed (#{1})", spell.Name, spell.Id);

                return false;
            }

            return true;
        }

        /// <summary>
        /// CastHack following done because CanCast() wants spell as "Metamorphosis: Doom" while Cast() and aura name are "Doom"
        /// </summary>
        /// <param name="castName"></param>
        /// <param name="onUnit"></param>
        /// <param name="requirements"></param>
        /// <returns></returns>
        public static Composite CastHack(string castName)
        {
            return CastHack(castName, castName, on => Me.CurrentTarget, ret => true);
        }

        public static Composite CastHack(string castName, SimpleBooleanDelegate requirements)
        {
            return CastHack(castName, castName, on => Me.CurrentTarget, requirements);
        }

        public static Composite CastHack(string castName, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return CastHack(castName, castName, onUnit, requirements);
        }

        public static Composite CastHack(string canCastName, string castName, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new Decorator(ret => castName != null && requirements != null && onUnit != null,
                new Throttle(
                    new Action(ret =>
                    {
                        WoWUnit unit = onUnit(ret);
                        if (unit == null || !requirements(ret) || !CanCastHack(canCastName, unit, skipWowCheck:true))
                            return RunStatus.Failure;

                        LogCast(castName, unit);
                        SpellManager.Cast(castName, unit);
                        unit = null;
                        return RunStatus.Success;
                    })
                    )
                );
        }

        public static Composite BuffHack(string castName, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new Decorator(
                ret => onUnit != null
                    && onUnit(ret) != null
                    && castName != null
                    && !DoubleCastPreventionDict.Contains(onUnit(ret), castName)
                    && !onUnit(ret).HasAura(castName),
                new Sequence(
                    CastHack(castName, onUnit, requirements),
                    new DecoratorContinue(
                        ret => Spell.GetSpellCastTime(castName) > TimeSpan.Zero,
                        new WaitContinue(1, ret => StyxWoW.Me.IsCasting, new Action(ret => UpdateDoubleCastDict(castName, onUnit(ret))))
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
            return new PrioritySelector(
                ctx => Unit.ResurrectablePlayers.FirstOrDefault(u => !Blacklist.Contains(u, BlacklistFlags.Combat)),
                new Decorator(
                    ctx => ctx != null && SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds,
                    new Sequence(
                        Cast(spellName, mov => true, ctx => (WoWPlayer)ctx, req => true, cancel => 
                            {
                                if (((WoWUnit)cancel).IsAlive)
                                {
                                    Logger.Write(Color.White, "^Rez target brought back by someone else, cancelling....");
                                    return true;
                                }

                                return false;
                            }),
                        new Action(ctx => Blacklist.Add((WoWPlayer)ctx, BlacklistFlags.Combat, TimeSpan.FromSeconds(30)))
                        )
                    )
                );
        }

        public static bool IsPlayerRessurectNeeded()
        {
            return Unit.ResurrectablePlayers.Any(u => !Blacklist.Contains(u, BlacklistFlags.Combat));
        }

        #endregion

        public static bool IsFunnel(string name)
        {
            SpellFindResults sfr;
            SpellManager.FindSpell(name, out sfr);
            WoWSpell spell = sfr.Override ?? sfr.Original;
            if (spell == null)
                return false;
            return IsFunnel(spell);
        }

        public static bool IsFunnel(WoWSpell spell)
        {
            // HV has the answer... ty m8
            bool IsChanneled = false;
            var row = StyxWoW.Db[Styx.Patchables.ClientDb.Spell].GetRow((uint)spell.Id);
            if (row.IsValid)
            {
                var spellMiscIdx = row.GetField<uint>(24);
                row = StyxWoW.Db[Styx.Patchables.ClientDb.SpellMisc].GetRow(spellMiscIdx);
                var flags = row.GetField<uint>(4);
                IsChanneled = (flags & 68) != 0;
            }

            return IsChanneled;
        }

        public static WoWSpell GetPendingCursorSpell
        {
            get
            {
#if WORKING
                return Me.CurrentPendingCursorSpell;
#else
                int pendingSpellId = 0;
                var pendingSpellPtr = StyxWoW.Memory.Read<IntPtr>((IntPtr)0xC83494, true);
                if (pendingSpellPtr != IntPtr.Zero)
                    pendingSpellId = StyxWoW.Memory.Read<int>(pendingSpellPtr + 32);

                return pendingSpellId == 0 ? null : WoWSpell.FromId(pendingSpellId);
#endif
            }
        }

        public static int GetCharges(string name)
        {
            SpellFindResults sfr;
            if ( SpellManager.FindSpell(name, out sfr))
            {
                WoWSpell spell = sfr.Override ?? sfr.Original;
                return GetCharges(spell);
            }
            return 0;
        }

        public static int GetCharges(WoWSpell spell)
        {
            int charges = Lua.GetReturnVal<int>("return GetSpellCharges(" + spell.Id.ToString() + ")", 0);
            return charges;
        }

        public static int GetBaseCooldown(WoWSpell spell)
        {
            int cd = Lua.GetReturnVal<int>("return GetSpellBaseCooldown(" + spell.Id.ToString() + ")", 0);
            return cd;
        }
    }

    /// <summary>
    /// cached result of name(ret), onUnit(ret), etc for Spell.Cast.  for expensive queries (such as Cluster.GetBestUnitForCluster()) we want to avoid
    /// performing them multiple times.  in some cases we were caching that locally in the context parameter of a wrapping PrioritySelector
    /// but doing it here enforces for all calls, so will reduce list scans and cycles required even for targets selected by auras present/absent
    /// </summary>
    internal class CastContext
    {
        internal string name;
        internal WoWSpell spell;
        internal WoWUnit unit;
        internal double health;
        internal double distance;
        internal bool IsSpellBeingQueued;
        internal object context;

        // always create passing the existing context so it is preserved for delegate usage
        internal CastContext(object ctx)
        {
            context = ctx;
        }

        // always create passing the existing context so it is preserved for delegate usage
        internal CastContext(WoWUnit u, object ctx)
        {
            unit = u;
            context = ctx;

            // health/dist change quickly, so grab these now where
            // .. we check requirements so the log message we output
            // .. later reflects what they were when we were testing
            // .. as opposed to what they may have changed to
            // .. (since spell lookup, move while casting check, and cancast take time)
            if (unit != null)
            {
                health = unit.HealthPercent;
                distance = unit.Distance;
            }
        }
    }

#if SPELL_BLACKLIST_WERE_NEEDED

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

#endif

}