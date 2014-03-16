﻿//#define USE_ISFLEEING
#define USE_MECHANIC

using System;
using System.Collections.Generic;
using System.Linq;

using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using CommonBehaviors.Actions;
using Action = Styx.TreeSharp.Action;
using System.Drawing;

namespace Singular.ClassSpecific.Shaman
{
    internal static class Totems
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman(); } }

        private static bool ShouldWeDropPullTotems
        {
            get
            {
                if (Me.GotTarget)
                {
                    if (Me.Specialization == WoWSpec.ShamanRestoration && Me.IsInGroup())
                        return false;

                    if (Me.Specialization == WoWSpec.ShamanEnhancement)
                        return Me.CurrentTarget.Distance < Me.MeleeDistance(Me.CurrentTarget) + 10;

                    float distCheck = Totems.GetTotemRange(WoWTotem.Searing);
                    if (Me.CurrentTarget.IsMoving)
                        distCheck -= 3.5f;

                    if (Me.SpellDistance(Me.CurrentTarget) <= distCheck  )
                        return true;
                }

                return false;
            }
        }

        private static bool ShouldWeDropTotemsYet
        {
            get
            {
                if ( !Me.Combat )
                    return false;

                if ( Me.Specialization == WoWSpec.ShamanEnhancement )
                    return Me.GotTarget && Me.CurrentTarget.Distance < Me.MeleeDistance(Me.CurrentTarget) + 10;

                if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                {
                    if ( !Me.GotTarget )
                        return false;
                    
                    if ( Me.CurrentTarget.IsMoving )
                        return false;
                    
                    if (Me.SpellDistance(Me.CurrentTarget) > Totems.GetTotemRange(WoWTotem.Searing))
                        return false;
                    
                    return true;
                }

                return !Me.GotTarget || Me.CurrentTarget.SpellDistance() < 40;
            }
        }

        public static Composite CreateTotemsBehavior()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
            {
                if (Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull)
                    return CreateTotemsNormalPullBehavior();

                return CreateTotemsNormalBehavior();
            }

            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds )
                return CreateTotemsPvPBehavior();

            return CreateTotemsInstanceBehavior();
        }

        public static Composite CreateTotemsNormalPullBehavior()
        {
            if (Dynamics.CompositeBuilder.CurrentBehaviorType != BehaviorType.Pull)
                return new ActionAlwaysFail();

            return new PrioritySelector(              
                // check in case more sophisticated totem cast needed
                CreateTotemsNormalBehavior(),

                // otherwise drop the DPS totem 
                Spell.BuffSelf("Searing Totem", ret => ShouldWeDropPullTotems && !Exist(WoWTotemType.Fire))
                );

        }

        public static Composite CreateTotemsNormalBehavior()
        {
            // create Fire Totems behavior first, then wrap if needed
            PrioritySelector fireTotemBehavior = new PrioritySelector();
            fireTotemBehavior.AddChild(
                Spell.BuffSelf("Fire Elemental",
                    ret => Common.StressfulSituation && !SpellManager.CanBuff(WoWTotem.EarthElemental.ToSpellId(), false))
                );

            if (Me.Specialization == WoWSpec.ShamanEnhancement)
            {
                fireTotemBehavior.AddChild( 
                    Spell.BuffSelf("Magma Totem",
                        ret => Spell.UseAOE 
                            && !Exist( WoWTotem.FireElemental, WoWTotem.Magma )
                            && Unit.NearbyUnitsInCombatWithUsOrOurStuff.Count( u => u.DistanceSqr < 10 * 10) >= 3
                        )
                    );
            }

            fireTotemBehavior.AddChild(
                Spell.BuffSelf("Searing Totem",
                    ret => {
                        if (Me.GotTarget && (Me.CurrentTarget.SpellDistance() < GetTotemRange(WoWTotem.Searing) - 2f) && TotemIsKnown(WoWTotem.Searing))
                        {
                            if (!Exist(WoWTotemType.Fire))
                            {
                                Logger.WriteDebug("CreateNormalTotems: no Fire Totem so setting Searing Totem");
                                return true;
                            }
                            WoWTotemInfo ti = GetTotem(WoWTotem.Searing);
                            if (ti != null && ti.Expires < DateTime.Now + TimeSpan.FromSeconds(2))
                            {
                                Logger.WriteDebug("CreateNormalTotems: Searing Totem expires in {0} ms, refreshing", (int)(ti.Expires - DateTime.Now).TotalMilliseconds);
                                return true;
                            }
                            ti = GetTotem(WoWTotem.Magma);
                            if (ti != null)
                            {
                                int count = Unit.NearbyUnitsInCombatWithUsOrOurStuff.Count(u => u.DistanceSqr < 15 * 15);
                                if (count < 2)
                                {
                                    Logger.WriteDebug("CreateNormalTotems: only {0} mobs nearby, replacing Magma Totem with Searing Totem", count);
                                    return true;
                                }
                            }
                        }
                        return false;
                    }
                    )
                );

            if (Me.Specialization == WoWSpec.ShamanRestoration)
            {
                fireTotemBehavior = new PrioritySelector(
                    new Decorator(
                        ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid), 
                        fireTotemBehavior
                        )
                    );
            }

            // now 
            return new PrioritySelector(

                new Throttle(1,
                    new Action(r =>
                    {
                        bool ccMechanic = Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing | WoWSpellMechanic.Polymorphed | WoWSpellMechanic.Asleep);
                        bool ccEffect = Me.HasAuraWithEffect(WoWApplyAuraType.ModFear | WoWApplyAuraType.ModPacify | WoWApplyAuraType.ModPacifySilence);
                        bool ccAttrib = Me.Fleeing;
                        if (ccMechanic || ccEffect || ccAttrib)
                            Logger.WriteDebug(Color.Pink, "... FEAR CHECKED OUT --  Mechanic={0}  Effect={1}  Attrib={2}", ccMechanic, ccEffect, ccAttrib);
                        return RunStatus.Failure;
                    })
                    ),


                Spell.BuffSelf(WoWTotem.Tremor.ToSpellId(),
                    ret => Unit.GroupMembers.Any(f => f.Fleeing && f.Distance < Totems.GetTotemRange(WoWTotem.Tremor))
                        && !Exist(WoWTotem.StoneBulwark, WoWTotem.EarthElemental)),

                new Decorator(
                    ret => ShouldWeDropTotemsYet,

                    new PrioritySelector(

                        // check for stress - enemy player or elite within 8 levels nearby
                // .. dont use NearbyUnitsInCombatWithMe since it checks .Tagged and we only care if we are being attacked 
                        ctx => Common.StressfulSituation,

                        // earth totems
                        Spell.BuffSelf(WoWTotem.EarthElemental.ToSpellId(),
                            ret => ((bool)ret || Group.Tanks.Any(t => t.IsDead && t.Distance < 40)) && !Exist(WoWTotem.StoneBulwark)),

                        Spell.BuffSelf(WoWTotem.StoneBulwark.ToSpellId(),
                            ret => Me.Combat && Me.HealthPercent < ShamanSettings.StoneBulwarkTotemPercent && !Exist(WoWTotem.EarthElemental)),

                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits.Any(u => u.IsTargetingMeOrPet && u.IsPlayer && u.Combat),

                            Spell.BuffSelf(WoWTotem.Earthgrab.ToSpellId(),
                                ret => (bool)ret && !Exist(WoWTotemType.Earth)),

                            Spell.BuffSelf(WoWTotem.Earthbind.ToSpellId(),
                                ret => (bool)ret && !Exist(WoWTotemType.Earth))
                            ),

                        // fire totems
                        fireTotemBehavior,

                        // water totems
                        Spell.BuffSelf("Mana Tide Totem",
                            ret =>
                            {
                                if (Me.Specialization != WoWSpec.ShamanRestoration)
                                    return false;

                                // Logger.WriteDebug("Mana Tide Totem Check:  current mana {0:F1}%", Me.ManaPercent);
                                if (Me.ManaPercent > ShamanSettings.ManaTideTotemPercent )
                                    return false;
                                if (Exist(WoWTotem.HealingTide, WoWTotem.HealingStream))
                                    return false;
                                return true;
                            }),

        /* Healing...: handle within Helaing logic
                        Spell.Cast("Healing Tide Totem", ret => ((bool)ret) && StyxWoW.Me.HealthPercent < 50
                            && !Exist(WoWTotem.ManaTide)),

                        Spell.Cast("Healing Stream Totem", ret => ((bool)ret) && StyxWoW.Me.HealthPercent < 80
                            && !Exist( WoWTotemType.Water)),
        */

                        // air totems
                        Spell.Cast("Stormlash Totem", ret => PartyBuff.WeHaveBloodlust && !Me.HasAura("Stormlash Totem")),

                        new Decorator(
                            ret => !Exist(WoWTotemType.Air),
                            new PrioritySelector(
                                Spell.Cast("Grounding Totem",
                                    ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < 40 && u.IsTargetingMeOrPet && u.IsCasting)),

                                Spell.Cast("Capacitor Totem",
                                    ret => ((bool)ret)
                                        && Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < GetTotemRange(WoWTotem.Capacitor))),

                                Spell.BuffSelf("Windwalk Totem",
                                    ret => Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Rooted, WoWSpellMechanic.Snared))
                                )
                            )
                        )
                    )
                );

        }

        public static Composite CreateTotemsPvPBehavior()
        {
            return CreateTotemsNormalBehavior();
        }

        public static Composite CreateTotemsInstanceBehavior()
        {
            // create Fire Totems behavior first, then wrap if needed
            PrioritySelector fireTotemBehavior = new PrioritySelector();

            fireTotemBehavior.AddChild(
                    Spell.Cast("Fire Elemental", ret => Me.CurrentTarget.IsBoss())
                    );

            if (Me.Specialization == WoWSpec.ShamanEnhancement)
            {
                fireTotemBehavior.AddChild(
                    Spell.Cast("Magma Totem",
                        on => Me.CurrentTarget ?? Me,
                        ret =>
                        {
                            if (!Spell.UseAOE)
                                ;
                            else if (!TotemIsKnown(WoWTotem.Searing))
                                ;
                            else if (Exist(WoWTotem.FireElemental))
                                ; // Logger.WriteDebug("CreateInstanceTotems: FireElemental Totem exists, skipping Magma");
                            else if (Exist(WoWTotem.Magma) && GetTotem(WoWTotem.Magma).Unit != null && Unit.NearbyUnitsInCombatWithUsOrOurStuff.Count(u => GetTotem(WoWTotem.Magma).Unit.SpellDistance(u) < 10) >= 3)
                                ; // Logger.WriteDebug("CreateInstanceTotems: Magma Totem already exists, skipping Magma");
                            else
                            {
                                int count = Unit.NearbyUnitsInCombatWithUsOrOurStuff.Count(u => u.SpellDistance() < 10);
                                if (count < 3)
                                    ; // Logger.WriteDebug("CreateInstanceTotems: only {0} mobs attacking us, skipping Magma Totem", count);
                                else
                                {
                                    ; // Logger.WriteDebug("CreateInstanceTotems: {0} mobs attacking us, setting a Magma Totem now!!!", count);
                                    return true;
                                }
                            }

                            return false;
                        }
                        )
                    );
            }

            if (Me.Specialization == WoWSpec.ShamanRestoration)
                fireTotemBehavior = new PrioritySelector(
                    new Decorator(
                        ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && !HealerManager.Instance.TargetList.Any(m => m.IsAlive), 
                        fireTotemBehavior
                        )
                    );
            else
                fireTotemBehavior.AddChild(
                    Spell.Cast("Searing Totem",
                        on => Me.CurrentTarget,
                        ret => {
                            if (Me.GotTarget && TotemIsKnown(WoWTotem.Searing))
                            {
                                float currDist = Me.CurrentTarget.SpellDistance();
                                if ( currDist > GetTotemRange(WoWTotem.Searing))
                                {
                                    Logger.WriteDebug("CreateInstanceTotems: dist of {0:F1} out of range for Searing Totem, not setting", currDist);
                                    return false;
                                }

                                if (!Exist(WoWTotemType.Fire))
                                {
                                    Logger.WriteDebug("CreateInstanceTotems: no Fire Totem so setting Searing Totem");
                                    return true;
                                }

                                WoWTotemInfo ti = GetTotem(WoWTotem.Searing);
                                if (ti != null && ti.Expires < DateTime.Now + TimeSpan.FromSeconds(2))
                                {
                                    Logger.WriteDebug("CreateInstanceTotems: Searing Totem expires in {0} ms, refreshing", (int)(ti.Expires - DateTime.Now).TotalMilliseconds);
                                    return true;
                                }
                                ti = GetTotem(WoWTotem.Magma);
                                if (ti != null)
                                {
                                    int count = Unit.NearbyUnitsInCombatWithUsOrOurStuff.Count(u => u.DistanceSqr < 15 * 15);
                                    if (count < 2)
                                    {
                                        Logger.WriteDebug("CreateInstanceTotems: only {0} mobs nearby, replacing Magma Totem with Searing Totem", count);
                                        return true;
                                    }
                                }
                            }
                            return false;
                        }
                        )
                    );


            // now 
            return new PrioritySelector(

                Spell.BuffSelf(WoWTotem.Tremor.ToSpellId(),
                    ret => Unit.GroupMembers.Any(f => f.Fleeing && f.Distance < Totems.GetTotemRange(WoWTotem.Tremor))
                        && !Exist(WoWTotem.StoneBulwark, WoWTotem.EarthElemental)),

                new Decorator(
                    ret => ShouldWeDropTotemsYet,

                    new PrioritySelector(

                        // earth totems
                        Spell.Cast(WoWTotem.EarthElemental.ToSpellId(),
                            on => Me.CurrentTarget ?? Me,
                            ret => (Group.Tanks.Any(t => t.IsDead && t.Distance < 40)) && !Exist(WoWTotem.StoneBulwark)),

                        // Stone Bulwark handled in CombatBuffs with Astral Shift

                        // fire totems
                        fireTotemBehavior,

                        // water totems
                        Spell.BuffSelf("Mana Tide Totem",
                            ret =>
                            {
                                if (Me.Specialization != WoWSpec.ShamanRestoration)
                                    return false;

                                // Logger.WriteDebug("Mana Tide Totem Check:  current mana {0:F1}%", Me.ManaPercent);
                                if (Me.ManaPercent > ShamanSettings.ManaTideTotemPercent)
                                    return false;
                                if (Exist(WoWTotem.HealingTide, WoWTotem.HealingStream))
                                    return false;
                                return true;
                            }),


        /* Healing...: handle within Helaing logic
                        Spell.Cast("Healing Tide Totem", ret => ((bool)ret) && StyxWoW.Me.HealthPercent < 50
                            && !Exist(WoWTotem.ManaTide)),

                        Spell.Cast("Healing Stream Totem", ret => ((bool)ret) && StyxWoW.Me.HealthPercent < 80
                            && !Exist( WoWTotemType.Water)),
        */

                        // air totems
                        Spell.Cast("Stormlash Totem", ret => PartyBuff.WeHaveBloodlust && !Me.HasAura("Stormlash Totem")),

                        new Decorator(
                            ret => !Exist(WoWTotemType.Air),
                            new PrioritySelector(
                                Spell.Cast("Grounding Totem",
                                    on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.Distance < 40 && u.IsTargetingMeOrPet && u.IsCasting)),

                                Spell.Cast("Capacitor Totem",
                                    on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.Distance < GetTotemRange(WoWTotem.Capacitor))),

                                Spell.BuffSelf("Windwalk Totem",
                                    ret => Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Rooted, WoWSpellMechanic.Snared))
                                )
                            )
                        )
                    )
                );

        }

        /// <summary>
        ///   Recalls any currently 'out' totems. This will use Totemic Recall if its known, otherwise it will destroy each totem one by one.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        public static Composite CreateRecallTotems()
        {
            return new Decorator(
                ret => Totems.NeedToRecallTotems,
                new Throttle(
                    new Styx.TreeSharp.Action( delegate
                    {
                        Logger.Write("Recalling totems!");
                        if (SpellManager.HasSpell("Totemic Recall"))
                        {
                            SpellManager.Cast("Totemic Recall");
                            return;
                        }

                        List<WoWTotemInfo> totems = StyxWoW.Me.Totems;
                        foreach (WoWTotemInfo t in totems)
                        {
                            if (t != null && t.Unit != null)
                            {
                                DestroyTotem(t.Type);
                            }
                        }
                    })
                    )
                );
        }

        /// <summary>
        ///   Destroys the totem described by type.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        /// <param name = "type">The type.</param>
        public static void DestroyTotem(WoWTotemType type)
        {
            if (type == WoWTotemType.None)
            {
                return;
            }

            Lua.DoString("DestroyTotem({0})", (int)type);
        }


        private static readonly Dictionary<WoWTotemType, WoWTotem> LastSetTotems = new Dictionary<WoWTotemType, WoWTotem>();


        #region Helper shit

        public static bool NeedToRecallTotems 
        { 
            get 
            { 
                return TotemsInRange == 0 
                    && StyxWoW.Me.Totems.Count(t => t.Unit != null) != 0
                    && !Unit.NearbyFriendlyPlayers.Any( f => f.Combat )
                    && !StyxWoW.Me.Totems.Any(t => totemsWeDontRecall.Any( twl => twl == t.WoWTotem )); 
            } 
        }

        public static bool TotemIsKnown(WoWTotem totem)
        {
            return SpellManager.HasSpell(totem.ToSpellId());
        }


        #region Totem Existance

        public static bool IsRealTotem(WoWTotem ti)
        {
            return ti != WoWTotem.None
                && ti != WoWTotem.DummyAir
                && ti != WoWTotem.DummyEarth
                && ti != WoWTotem.DummyFire
                && ti != WoWTotem.DummyWater;
        }

        /// <summary>
        /// check if a specific totem (ie Mana Tide Totem) exists
        /// </summary>
        /// <param name="wtcheck"></param>
        /// <returns></returns>
        public static bool Exist(WoWTotem wtcheck)
        {
            WoWTotemInfo tiexist = GetTotem(wtcheck);
            return tiexist != null;
        }

        /// <summary>
        /// check if a WoWTotemInfo object references a real totem (other than None or Dummies)
        /// </summary>
        /// <param name="ti"></param>
        /// <returns></returns>
        public static bool Exist(WoWTotemInfo ti)
        {
            return IsRealTotem(ti.WoWTotem);
        }

        /// <summary>
        /// check if a type of totem (ie Air Totem) exists
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool Exist(WoWTotemType type)
        {
            WoWTotem wt = GetTotem(type).WoWTotem;
            return IsRealTotem(wt);
        }

        /// <summary>
        /// check if any of several specific totems exist
        /// </summary>
        /// <param name="wt"></param>
        /// <returns></returns>
        public static bool Exist(params WoWTotem[] wt)
        {
            return wt.Any(t => Exist(t));
        }

        /// <summary>
        /// check if a specific totem exists within its max range of a given location
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="tt"></param>
        /// <returns></returns>
        public static bool ExistInRange(WoWPoint pt, WoWTotem tt)
        {
            if ( !Exist(tt))
                return false;

            WoWTotemInfo ti = GetTotem(tt);
            return ti.Unit != null && ti.Unit.Location.Distance(pt) < GetTotemRange(tt);
        }

        /// <summary>
        /// check if any of several totems exist in range
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="awt"></param>
        /// <returns></returns>
        public static bool ExistInRange(WoWPoint pt, params WoWTotem[] awt)
        {
            return awt.Any(t => ExistInRange(pt, t));
        }

        /// <summary>
        /// check if type of totem (ie Air Totem) exists in range
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool ExistInRange(WoWPoint pt, WoWTotemType type)
        {
            WoWTotemInfo ti = GetTotem(type);
            return Exist(ti) && ti.Unit != null && ti.Unit.Location.Distance(pt) < GetTotemRange(ti.WoWTotem);
        }

        #endregion

        /// <summary>
        /// gets reference to array element in Me.Totems[] corresponding to WoWTotemType of wt.  Return is always non-null and does not indicate totem existance
        /// </summary>
        /// <param name="wt">WoWTotem of slot to reference</param>
        /// <returns>WoWTotemInfo reference</returns>
        public static WoWTotemInfo GetTotem(WoWTotem wt)
        {
            WoWTotemInfo ti = GetTotem(wt.ToType());
            if (ti.WoWTotem != wt)
                return null;
            return ti;
        }

        /// <summary>
        /// gets reference to array element in Me.Totems[] corresponding to type.  Return is always non-null and does not indicate totem existance
        /// </summary>
        /// <param name="type">WoWTotemType of slot to reference</param>
        /// <returns>WoWTotemInfo reference</returns>
        public static WoWTotemInfo GetTotem(WoWTotemType type)
        {
            return Me.Totems[(int)type - 1];
        }

        /// <summary>
        /// check if all totems are within range of Shaman's location
        /// </summary>
        public static int TotemsInRange 
        { 
            get 
            {
                return TotemsInRangeOf(StyxWoW.Me);
            }
        }

        /// <summary>
        /// check if all totems are within range of a WoWUnit
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        public static int TotemsInRangeOf(WoWUnit unit)
        {
            return StyxWoW.Me.Totems.Where(t => t.Unit != null).Count(t => unit.Location.Distance(t.Unit.Location) < GetTotemRange(t.WoWTotem));
        }

        /// <summary>
        ///   Finds the max range of a specific totem, where you'll still receive the buff.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        /// <param name = "totem">The totem.</param>
        /// <returns>The calculated totem range.</returns>
        public static float GetTotemRange(WoWTotem totem)
        {
            switch (totem)
            {
                case WoWTotem.Tremor:
                    return 30f;

                case WoWTotem.Searing:
                    if (Me.Specialization == WoWSpec.ShamanElemental )
                        return 35f;
                    return 20f;

                case WoWTotem.Earthbind:
                    return 10f;

                case WoWTotem.Grounding:
                case WoWTotem.Magma:
                    return 8f;

                case WoWTotem.EarthElemental:
                case WoWTotem.FireElemental:
                    // Not really sure about these 3.
                    return 20f;

                case WoWTotem.ManaTide:
                    // Again... not sure :S
                    return 40f;


                case WoWTotem.Earthgrab:
                    return 10f;

                case WoWTotem.StoneBulwark:
                    // No idea, unlike former glyphed stoneclaw it has a 5 sec pluse shield component so range is more important
                    return 40f;

                case WoWTotem.HealingStream:
                    return 40f;

                case WoWTotem.HealingTide:
                    return 40f;

                case WoWTotem.Capacitor:
                    return 8f;

                case WoWTotem.Stormlash:
                    return 30f;

                case WoWTotem.Windwalk:
                    return 40f;

                case WoWTotem.SpiritLink:
                    return 10f;
            }

            return 0f;
        }


        public static int ToSpellId(this WoWTotem totem)
        {
            return (int)(((long)totem) & ((1L << 32) - 1));
        }

        public static WoWTotemType ToType(this WoWTotem totem)
        {
            return (WoWTotemType)((long)totem >> 32);
        }


        static WoWTotem[] totemsWeDontRecall = new WoWTotem[] 
        {
            WoWTotem.FireElemental , 
            WoWTotem.EarthElemental  , 
            WoWTotem.HealingTide , 
            WoWTotem.ManaTide 
        };

        #endregion

    }

}