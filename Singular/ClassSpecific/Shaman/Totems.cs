using System.Collections.Generic;
using System.Linq;

using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular.ClassSpecific.Shaman
{
    internal class Totems
    {
        private static bool _totemsSet;

        public static void SetupTotemBar()
        {
            if (_totemsSet)
            {
                return;
            }

            WoWTotem fire = SingularSettings.Instance.Shaman.FireTotem;
            WoWTotem earth = SingularSettings.Instance.Shaman.EarthTotem;
            WoWTotem air = SingularSettings.Instance.Shaman.AirTotem;
            WoWTotem water = SingularSettings.Instance.Shaman.WaterTotem;

            SetTotemBarSlot(MultiCastSlot.ElementsFire, fire != WoWTotem.None ? fire : GetFireTotem());
            SetTotemBarSlot(MultiCastSlot.ElementsEarth, earth != WoWTotem.None ? earth : GetEarthTotem());
            SetTotemBarSlot(MultiCastSlot.ElementsAir, air != WoWTotem.None ? air : GetAirTotem());
            SetTotemBarSlot(MultiCastSlot.ElementsWater, water != WoWTotem.None ? water : GetWaterTotem());

            _totemsSet = true;
        }

        /// <summary>Recalls any currently 'out' totems. This will use Totemic Recall if its known, otherwise it will destroy each totem one by one.</summary>
        /// <remarks>Created 3/26/2011.</remarks>
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

        /// <summary>Destroys the totem described by type.</summary>
        /// <remarks>Created 3/26/2011.</remarks>
        /// <param name="type">The type.</param>
        public static void DestroyTotem(WoWTotemType type)
        {
            if (type == WoWTotemType.None)
            {
                return;
            }

            Lua.DoString("DestroyTotem({0})", (int)type);
        }

        /// <summary>Sets a totem bar slot to the specified totem!.</summary>
        /// <remarks>Created 3/26/2011.</remarks>
        /// <param name="slot">The slot.</param>
        /// <param name="totem">The totem.</param>
        public static void SetTotemBarSlot(MultiCastSlot slot, WoWTotem totem)
        {
            // Make sure we have the totem bars to set. Highest first kthx
            if (slot >= MultiCastSlot.SpiritsFire && !SpellManager.HasSpell("Call of the Spirits"))
            {
                return;
            }
            if (slot >= MultiCastSlot.AncestorsFire && !SpellManager.HasSpell("Call of the Ancestors"))
            {
                return;
            }
            if (!SpellManager.HasSpell("Call of the Elements"))
            {
                return;
            }

            Logger.Write("Setting totem slot Call of the" + slot.ToString().CamelToSpaced() + " to " + totem.ToString().CamelToSpaced());

            Lua.DoString("SetMultiCastSpell({0}, {1})", (int)slot, totem.GetTotemSpellId());
        }

        private static WoWTotem GetEarthTotem()
        {
            LocalPlayer me = StyxWoW.Me;
            bool isEnhance = TalentManager.CurrentSpec == TalentSpec.EnhancementShaman;
            if (!me.IsInParty && !me.IsInRaid)
            {
                if (isEnhance)
                {
                    return WoWTotem.StrengthOfEarth;
                }
                return WoWTotem.Stoneskin;
            }

            // If we have wars/DKs in the party/raid. Then just drop Stoneskin, as they provide a buff anyway.
            if (StyxWoW.Me.PartyMembers.Any(p => p.Class == WoWClass.DeathKnight || p.Class == WoWClass.Warrior)
                || StyxWoW.Me.RaidMembers.Any(p => p.Class == WoWClass.DeathKnight || p.Class == WoWClass.Warrior))
            {
                return WoWTotem.Stoneskin;
            }

            return WoWTotem.StrengthOfEarth;
        }

        public static WoWTotem GetAirTotem()
        {
            LocalPlayer me = StyxWoW.Me;
            bool isEnhance = TalentManager.CurrentSpec == TalentSpec.EnhancementShaman;
            if (!me.IsInParty && !me.IsInRaid)
            {
                if (isEnhance)
                {
                    return WoWTotem.Windfury;
                }
                return WoWTotem.WrathOfAir;
            }

            if (StyxWoW.Me.RaidMembers.Any(p => p.Class == WoWClass.Druid && p.Shapeshift == ShapeshiftForm.Moonkin) ||
                StyxWoW.Me.PartyMembers.Any(p => p.Class == WoWClass.Druid && p.Shapeshift == ShapeshiftForm.Moonkin))
            {
                return WoWTotem.Windfury;
            }

            return WoWTotem.WrathOfAir;
        }

        public static WoWTotem GetFireTotem()
        {
            // If we popped the elemental, keep it out.
            if (StyxWoW.Me.Totems.Any(t => t.WoWTotem == WoWTotem.FireElemental))
            {
                return WoWTotem.FireElemental;
            }

            if (StyxWoW.Me.HasAnyAura("Blood Lust", "Heroism", "Time Warp", "Ancient Hysteria"))
            {
                return WoWTotem.FireElemental;
            }

            if (TalentManager.CurrentSpec == TalentSpec.RestorationShaman)
            {
                return WoWTotem.Flametongue;
            }

            return WoWTotem.Searing;
        }

        public static WoWTotem GetWaterTotem()
        {
            if (TalentManager.CurrentSpec == TalentSpec.RestorationShaman)
            {
                return WoWTotem.HealingStream;
            }

            if (!StyxWoW.Me.HasAura("Blessing of Might"))
            {
                return WoWTotem.ManaSpring;
            }

            return WoWTotem.HealingStream;
        }

        #region Helper shit
        public static int TotemsInRangeOf(WoWUnit unit)
        {
            return StyxWoW.Me.Totems.Where(t => t.Unit != null).Count(t => unit.Location.Distance(t.Unit.Location) < GetTotemRange(t.WoWTotem));
        }

        public static bool NeedToRecallTotems
        {
            get { return TotemsInRange == 0 && StyxWoW.Me.Totems.Count(t => t.Unit != null) != 0; }
        }
        public static int TotemsInRange
        {
            get
            {
                return StyxWoW.Me.Totems.Where(t => t.Unit != null).Count(t => t.Unit.Distance < GetTotemRange(t.WoWTotem));
            }
        }
        /// <summary>Finds the max range of a specific totem, where you'll still receive the buff.</summary>
        /// <remarks>Created 3/26/2011.</remarks>
        /// <param name="totem">The totem.</param>
        /// <returns>The calculated totem range.</returns>
        public static float GetTotemRange(WoWTotem totem)
        {
            // 15% extra range if talented for Totemic Reach for each point
            float talentFactor = (TalentManager.GetCount(2, 7) * 0.15f) + 1;

            switch (totem)
            {
                case WoWTotem.Flametongue:
                case WoWTotem.Stoneskin:
                case WoWTotem.StrengthOfEarth:
                case WoWTotem.Windfury:
                case WoWTotem.WrathOfAir:
                case WoWTotem.ManaSpring:
                    return 40f * talentFactor;

                case WoWTotem.ElementalResistance:
                case WoWTotem.HealingStream:
                case WoWTotem.TranquilMind:
                case WoWTotem.Tremor:
                    return 30f * talentFactor;

                case WoWTotem.Searing:
                    return 20f * talentFactor;

                case WoWTotem.Earthbind:
                    return 10f * talentFactor;

                case WoWTotem.Magma:
                    return 8f * talentFactor;

                case WoWTotem.Stoneclaw:
                    // stoneclaw isn't effected by Totemic Reach (according to basically everything online)
                    return 8f;

                case WoWTotem.EarthElemental:
                case WoWTotem.FireElemental:
                case WoWTotem.Grounding:
                    // Not really sure about these 3.
                    return 20f;
                case WoWTotem.ManaTide:
                    // Again... not sure :S
                    return 30f * talentFactor;
            }
            return 0f;
        }

        #endregion

        #region Nested type: MultiCastSlot

        /// <summary>A small enum to make specifying specific totem bar slots easier.</summary>
        /// <remarks>Created 3/26/2011.</remarks>
        internal enum MultiCastSlot
        {
            // Enums increment by 1 after the first defined value. So don't touch this. Its the way it is for a reason.
            // If these numbers change in the future, feel free to fill this out completely. I'm too lazy to do it - Apoc
            //
            // Note: To get the totem 'slot' just do MultiCastSlot & 3 - will return 0-3 for the totem slot this is for.
            // I'm not entirely sure how WoW shows which ones are 'current' in the slot, so we'll just set it up for ourselves
            // and remember which is which.
            ElementsFire = 133,
            ElementsEarth,
            ElementsWater,
            ElementsAir,

            AncestorsFire,
            AncestorsEarth,
            AncestorsWater,
            AncestorsAir,

            SpiritsFire,
            SpiritsEarth,
            SpiritsWater,
            SpiritsAir
        }

        #endregion
    }
}