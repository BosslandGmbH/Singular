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

using System.Collections.Generic;
using System.Linq;

using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using CommonBehaviors.Actions;

namespace Singular.ClassSpecific.Shaman
{
    // temporary enum until HB updated
    public enum ShamTotem : long
    {
        None = 0,

        EarthElemental = (((long)WoWTotemType.Earth) << 32) + 2062,
        Earthbind = (((long)WoWTotemType.Earth) << 32) + 2484,
        Earthgrab = (((long)WoWTotemType.Earth) << 32) + 51485,
        StoneBulwark = (((long)WoWTotemType.Earth) << 32) + 108270,
        Tremor = (((long)WoWTotemType.Earth) << 32) + 8143,

        FireElemental = (((long)WoWTotemType.Fire) << 32) + 2894,
        Magma = (((long)WoWTotemType.Fire) << 32) + 8190,
        Searing = (((long)WoWTotemType.Fire) << 32) + 3599,

        HealingStream = (((long)WoWTotemType.Water) << 32) + 5394,
        HealingTide = (((long)WoWTotemType.Water) << 32) + 108280,
        ManaTide = (((long)WoWTotemType.Water) << 32) + 16190,

        Capacitor = (((long)WoWTotemType.Air) << 32) + 108269,
        Grounding = (((long)WoWTotemType.Air) << 32) + 8177,
        Stormlash = (((long)WoWTotemType.Air) << 32) + 120668,
        Windwalk = (((long)WoWTotemType.Air) << 32) + 108273,
        SpiritLink = (((long)WoWTotemType.Air) << 32) + 98008,
    }
    
    internal static class Totems
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        // temporary func until HB updated
        public static ShamTotem GetShamTotem(this WoWTotemInfo ti)
        {
            return (ShamTotem) (ti.Unit == null ? 0 : ((long) ti.Type << 32) + ti.Unit.CreatedBySpellId );
        }

        // temporary func until HB updated
        public static int GetTotemSpellId(this ShamTotem totem)
        {
            return (int) (((long) totem) & ((1 << 32) - 1));
        }

        public static WoWTotemType GetTotemType(this ShamTotem totem)
        {
            return (WoWTotemType) ((long) totem >> 24);
        }


        public static Composite CreateTotemsBehavior()
        {
            Composite tb;
            tb = CreateTotemsNormalBehavior();

            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                tb = CreateTotemsPvPBehavior();
            else if (SingularRoutine.CurrentWoWContext == WoWContext.Instances )
                tb = CreateTotemsInstanceBehavior();
            else
                tb = CreateTotemsNormalBehavior();

            return tb;
        }

        private const int StressMobCount = 3;

        public static Composite CreateTotemsNormalBehavior()
        {
            Composite fireTotemBehavior =
                new PrioritySelector(
                    Spell.BuffSelf("Fire Elemental",
                        ret => ((bool)ret)
                            || (Unit.NearbyUnitsInCombatWithMe.Count() >= StressMobCount && !SpellManager.CanBuff(ShamTotem.EarthElemental.GetTotemSpellId(), false))),
    
                    Spell.BuffSelf("Magma Totem",
                        ret => Unit.NearbyUnitsInCombatWithMe.Count(u => u.Distance <= GetTotemRange(ShamTotem.Magma)) >= StressMobCount
                            && !Exist( ShamTotem.FireElemental)),
    
                    Spell.BuffSelf("Searing Totem",
                        ret => Me.GotTarget 
                            && Me.CurrentTarget.Distance < GetTotemRange(ShamTotem.Searing) - 2f 
                            && !ExistInRange( Me.CurrentTarget.Location, ShamTotem.FireElemental, ShamTotem.Searing ))
                    );

            // resto is healing only
            if ( Me.Specialization == WoWSpec.ShamanRestoration )
                fireTotemBehavior = new Decorator(ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0, fireTotemBehavior);


            return new PrioritySelector(

                // check for stress - enemy player or elite within 8 levels nearby
                // .. dont use NearbyUnitsInCombatWithMe since it checks .Tagged and we only care if we are being attacked 
                ctx => Unit.NearbyUnitsInCombatWithMe.Count() >= StressMobCount
                    || Unit.NearbyUnfriendlyUnits.Any(u => u.IsTargetingMeOrPet && (u.IsPlayer || (u.Elite && u.Level + 8 > Me.Level))),

                // earth totems
                Spell.BuffSelf(ShamTotem.EarthElemental.GetTotemSpellId(),
                    ret => (bool) ret && !Exist( ShamTotem.StoneBulwark)),

                Spell.BuffSelf(ShamTotem.StoneBulwark.GetTotemSpellId(),
                    ret => Me.HealthPercent < 50 && !Exist( ShamTotem.EarthElemental)),

                new PrioritySelector( 
                    ctx => Unit.NearbyUnfriendlyUnits.Any(u => u.IsTargetingMeOrPet && u.IsPlayer && u.Combat ),

                    Spell.BuffSelf(ShamTotem.Earthgrab.GetTotemSpellId(),
                        ret => (bool)ret && !Exist( ShamTotem.StoneBulwark, ShamTotem.EarthElemental, ShamTotem.Earthbind )),

                    Spell.BuffSelf(ShamTotem.Earthbind.GetTotemSpellId(),
                        ret => (bool)ret && !Exist(ShamTotem.StoneBulwark, ShamTotem.EarthElemental, ShamTotem.Earthgrab))
                    ),

                Spell.BuffSelf(ShamTotem.Tremor.GetTotemSpellId(),
                    ret => Me.Fleeing && !Exist( ShamTotem.StoneBulwark, ShamTotem.EarthElemental )),
                   

                // fire totems
                fireTotemBehavior,

                // water totems
                Spell.Cast("Mana Tide Totem", ret => StyxWoW.Me.ManaPercent < 80),

                // air totems
                Spell.Cast("Grounding Totem", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < 40 && u.IsTargetingMeOrPet && u.IsCasting))


                );

#if TMPNOW
NORMAL

    HealingTide         = (WoWTotemType.Water << 32) + 108280,   
	>= 4 attacking || player
    HealingStream       = (WoWTotemType.Water << 32) + 5394,
	>= 4 attacking || player
    ManaTide            = (WoWTotemType.Water << 32) + 16190,    
	low mana and being attacked 

    Capacitor           = (WoWTotemType.Air << 32) + 108269,  
	>= 4 attacking || player
    Grounding           = (WoWTotemType.Air << 32) + 8177,
	>= (4 attacking || player) and one casting
    Stormlash           = (WoWTotemType.Air << 32) + 120668, 
	>= 4 attacking || player
    Windwalk            = (WoWTotemType.Air << 32) + 108273, 
	feared 
    SpiritLink          = (WoWTotemType.Air << 32) + 98008,
	never

#endif
        }

        public static Composite CreateTotemsPvPBehavior()
        {
            return new Decorator(ret => false, new ActionAlwaysFail());
        }

        public static Composite CreateTotemsInstanceBehavior()
        {
            return new Decorator(ret => false, new ActionAlwaysFail());
        }

        /// <summary>
        ///   Recalls any currently 'out' totems. This will use Totemic Recall if its known, otherwise it will destroy each totem one by one.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        public static void RecallTotems()
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


        private static readonly Dictionary<WoWTotemType, ShamTotem> LastSetTotems = new Dictionary<WoWTotemType, ShamTotem>();


        #region Helper shit

        public static bool NeedToRecallTotems 
        { 
            get 
            { 
#if PRE_504_TOTEM_LOGIC
                return TotemsInRange == 0 && StyxWoW.Me.Totems.Count(t => t.Unit != null) != 0; 
#else
                return false;
#endif
            } 
        }

        public static int TotemsInRange { get { return TotemsInRangeOf(StyxWoW.Me); } }

        public static int TotemsInRangeOf(WoWUnit unit)
        { 
            return StyxWoW.Me.Totems.Where(t => t.Unit != null).Count(t => unit.Location.Distance(t.Unit.Location) < GetTotemRange(t.GetShamTotem()));
        }

        public static bool TotemIsKnown(WoWTotem totem)
        {
            return SpellManager.HasSpell(totem.GetTotemSpellId());
        }

        /// <summary>
        ///   Finds the max range of a specific totem, where you'll still receive the buff.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        /// <param name = "totem">The totem.</param>
        /// <returns>The calculated totem range.</returns>
        public static float GetTotemRange(ShamTotem totem)
        {
            switch (totem)
            {
                case ShamTotem.HealingStream:
                case ShamTotem.Tremor:
                    return 30f;

                case ShamTotem.Searing:
                    if ( SpellManager.HasSpell(29000))
                        return 35f;
                    return 20f;

                case ShamTotem.Earthbind:
                    return 10f ;

                case ShamTotem.Grounding:
                case ShamTotem.Magma:
                    return 8f ;

                case ShamTotem.EarthElemental:
                case ShamTotem.FireElemental:
                    // Not really sure about these 3.
                    return 20f;
                case ShamTotem.ManaTide:
                    // Again... not sure :S
                    return 30f ;


                case ShamTotem.Earthgrab   :
                    return 10f;

                case ShamTotem.StoneBulwark:
                    // No idea, not like glyphed stoneclaw was since there is a 5 sec pluse shield component also
                    return 40f;

                case ShamTotem.HealingTide :
                    return 40f;

                case ShamTotem.Capacitor   :
                    return 8f;

                case ShamTotem.Stormlash   :
                    return 30f;

                case ShamTotem.Windwalk    :
                    return 40f;

                case ShamTotem.SpiritLink  :
                    return 10f;
            }

            return 0f;
        }

        public static bool Exist(ShamTotem tt)
        {
#if NOPTE
            return StyxWoW.Me.Totems.Any(t => tt == t.GetShamTotem());
#else
            foreach (WoWTotemInfo t in Me.Totems)
            {
                if (tt == t.GetShamTotem())
                {
                    return true;
                }
            }
            return false;
#endif
        }

        public static bool Exist(params ShamTotem[] tt)
        {
#if NOPE
            return StyxWoW.Me.Totems.Any(t => tt.Contains(t.GetShamTotem()));
#else
            foreach (WoWTotemInfo t in Me.Totems)
            {
                foreach (ShamTotem ttt in tt)
                {
                    if (ttt == t.GetShamTotem())
                    {
                        return true;
                    }
                }
            }
            return false;
#endif
        }

        public static bool ExistInRange(WoWPoint pt, ShamTotem tt)
        {
#if NOPE
            return StyxWoW.Me.Totems.Any(t => tt == t.GetShamTotem() && t.Unit.Location.Distance(pt) < GetTotemRange(t.GetShamTotem()));
#else
            foreach (WoWTotemInfo t in Me.Totems)
            {
                if (tt == t.GetShamTotem())
                {
                    if ( t.Unit.Location.Distance(pt) < GetTotemRange(t.GetShamTotem()))
                    {
                        return true;
                    }
                }
            }
            return false;
#endif
        }

        public static bool ExistInRange(WoWPoint pt, params ShamTotem[] tt)
        {
#if NOPE
            return StyxWoW.Me.Totems.Any(t => tt.Contains(t.GetShamTotem()) && t.Unit.Location.Distance(pt) < GetTotemRange(t.GetShamTotem()));
#else
            foreach (WoWTotemInfo t in Me.Totems)
            {
                foreach (ShamTotem ttt in tt)
                {
                    if (ttt == t.GetShamTotem())
                    {
                        if ( t.Unit.Location.Distance(pt) < GetTotemRange(ttt))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
#endif
        }

        #endregion

    }

}