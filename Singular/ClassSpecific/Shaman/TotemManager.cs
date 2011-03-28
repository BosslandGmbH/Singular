using System;
using System.Linq;

using Styx;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular.ClassSpecific.Shaman
{
    class TotemManager
    {
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

        private static bool _totemsSet;

        public static void SetupTotemBar()
        {
            if (_totemsSet)
                return;

            var fire = Singular.Settings.SingularSettings.Instance.Shaman.FireTotem;
            var earth = Singular.Settings.SingularSettings.Instance.Shaman.EarthTotem;
            var air = Singular.Settings.SingularSettings.Instance.Shaman.AirTotem;
            var water = Singular.Settings.SingularSettings.Instance.Shaman.WaterTotem;

            if (fire != WoWTotem.None)
                SetTotemBarSlot(MultiCastSlot.ElementsFire, fire);
            else
                SetTotemBarSlot(MultiCastSlot.ElementsFire, GetTotemForSpec(WoWTotemType.Fire));

            if (earth != WoWTotem.None)
                SetTotemBarSlot(MultiCastSlot.ElementsEarth, earth);
            else
                SetTotemBarSlot(MultiCastSlot.ElementsEarth, GetTotemForSpec(WoWTotemType.Earth));

            if (air != WoWTotem.None)
                SetTotemBarSlot(MultiCastSlot.ElementsAir, air);
            else
                SetTotemBarSlot(MultiCastSlot.ElementsAir, GetTotemForSpec(WoWTotemType.Air));

            if (water != WoWTotem.None)
                SetTotemBarSlot(MultiCastSlot.ElementsWater, water);
            else
                SetTotemBarSlot(MultiCastSlot.ElementsWater, GetTotemForSpec(WoWTotemType.Water));

            _totemsSet = true;
        }

        public static void CallTotems(WoWTotem fire, WoWTotem earth, WoWTotem air, WoWTotem water)
        {
            SetTotemBarSlot(MultiCastSlot.ElementsFire, fire);
            SetTotemBarSlot(MultiCastSlot.ElementsAir, air);
            SetTotemBarSlot(MultiCastSlot.ElementsWater, water);
            SetTotemBarSlot(MultiCastSlot.ElementsAir, earth);
            StyxWoW.SleepForLagDuration();
            CallTotems();
            _totemsSet = false;
        }

        public static void CallTotems()
        {
            if (SpellManager.CanCast("Call of the Elements"))
            {
                Logger.Write("Calling totems!");
                SpellManager.Cast("Call of the Elements");
            }
        }

        public static int TotemsInRange
        {
            get
            {
                return StyxWoW.Me.Totems().Where(t => t.Unit != null).Count(t => t.Unit.Distance < GetTotemRange(t.WoWTotem));
            }
        }

        public static int TotemsInRangeOf(WoWUnit unit)
        {
            return StyxWoW.Me.Totems().Where(t => t.Unit != null).Count(t => unit.Location.Distance(t.Unit.Location) < GetTotemRange(t.WoWTotem));
        }

        public static bool NeedToRecallTotems
        {
            get { return TotemsInRange == 0 && StyxWoW.Me.Totems().Count(t => t.Unit != null) != 0; }
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
                    return 15f * talentFactor;
            }
            return 0f;
        }

        private static WoWTotem GetTotemForSpec(WoWTotemType type)
        {
            switch (TalentManager.CurrentSpec)
            {
                case TalentSpec.ElementalShaman:
                    switch (type)
                    {
                        case WoWTotemType.Fire:
                            if (CanPlaceTotem(WoWTotem.Searing))
                                return WoWTotem.Searing;
                            break;

                        case WoWTotemType.Earth:
                            if (CanPlaceTotem(WoWTotem.Stoneskin))
                                return WoWTotem.Stoneskin;
                            break;

                        case WoWTotemType.Water:
                            if (Battlegrounds.IsInsideBattleground)
                            {
                                if (CanPlaceTotem(WoWTotem.HealingStream) && TalentManager.HasGlyph("Healing Stream Totem"))
                                    return WoWTotem.HealingStream;
                                if (CanPlaceTotem(WoWTotem.ElementalResistance))
                                    return WoWTotem.ElementalResistance;
                            }
                            if (CanPlaceTotem(WoWTotem.HealingStream))
                                return WoWTotem.HealingStream;
                            if (CanPlaceTotem(WoWTotem.ManaSpring))
                                return WoWTotem.ManaSpring;
                            break;

                        case WoWTotemType.Air:
                            if (CanPlaceTotem(WoWTotem.WrathOfAir))
                                return WoWTotem.WrathOfAir;
                            break;
                    }
                    break;

                case TalentSpec.RestorationShaman:
                    switch (type)
                    {
                        case WoWTotemType.Fire:
                            if (CanPlaceTotem(WoWTotem.Flametongue))
                                return WoWTotem.Flametongue;
                            break;

                        case WoWTotemType.Earth:
                            if (CanPlaceTotem(WoWTotem.Stoneskin))
                                return WoWTotem.Stoneskin;
                            break;

                        case WoWTotemType.Water:
                            if (CanPlaceTotem(WoWTotem.HealingStream))
                                return WoWTotem.HealingStream;
                            if (CanPlaceTotem(WoWTotem.ManaSpring))
                                return WoWTotem.ManaSpring;
                            break;

                        case WoWTotemType.Air:
                            if (CanPlaceTotem(WoWTotem.Grounding))
                                return WoWTotem.Grounding;
                            if (CanPlaceTotem(WoWTotem.WrathOfAir))
                                return WoWTotem.WrathOfAir;
                            break;
                    }
                    break;

                case TalentSpec.EnhancementShaman:
                    switch (type)
                    {
                        case WoWTotemType.Fire:
                            if (CanPlaceTotem(WoWTotem.Searing))
                                return WoWTotem.Grounding;
                            break;

                        case WoWTotemType.Earth:

                            if (CanPlaceTotem(WoWTotem.StrengthOfEarth))
                                return WoWTotem.StrengthOfEarth;
                            if (CanPlaceTotem(WoWTotem.Stoneskin))
                                return WoWTotem.Stoneskin;
                            break;

                        case WoWTotemType.Water:
                            if (Battlegrounds.IsInsideBattleground)
                            {
                                if (CanPlaceTotem(WoWTotem.HealingStream) && TalentManager.HasGlyph("Healing Stream Totem"))
                                    return WoWTotem.HealingStream;
                                if (CanPlaceTotem(WoWTotem.ElementalResistance))
                                    return WoWTotem.ElementalResistance;
                            }
                            if (CanPlaceTotem(WoWTotem.HealingStream))
                                return WoWTotem.HealingStream;
                            if (CanPlaceTotem(WoWTotem.ManaSpring))
                                return WoWTotem.ManaSpring;
                            break;

                        case WoWTotemType.Air:
                            if (CanPlaceTotem(WoWTotem.Windfury))
                                return WoWTotem.Windfury;
                            break;
                    }
                    break;
            }
            return WoWTotem.None;
        }

        /// <summary>Sets a totem bar slot to the specified totem!.</summary>
        /// <remarks>Created 3/26/2011.</remarks>
        /// <param name="slot">The slot.</param>
        /// <param name="totem">The totem.</param>
        public static void SetTotemBarSlot(MultiCastSlot slot, WoWTotem totem)
        {
            // Make sure we have the totem bars to set. Highest first kthx
            if (slot >= MultiCastSlot.SpiritsFire && !SpellManager.HasSpell("Call of the Spirits"))
                return;
            if (slot >= MultiCastSlot.AncestorsFire && !SpellManager.HasSpell("Call of the Ancestors"))
                return;
            if (!SpellManager.HasSpell("Call of the Elements"))
                return;

            Logger.Write("Setting totem slot Call of the" + slot.ToString().CamelToSpaced() + " to " + totem.ToString().CamelToSpaced());

            Lua.DoString("SetMultiCastSpell({0}, {1})", (int)slot, totem.GetTotemSpellId());
        }

        /// <summary>Clears the totem bar slot described by slot.</summary>
        /// <remarks>Created 3/26/2011.</remarks>
        /// <param name="slot">The slot.</param>
        public static void ClearTotemBarSlot(MultiCastSlot slot)
        {
            // Make sure we have the totem bars to set. Highest first kthx
            if (slot >= MultiCastSlot.SpiritsFire && !SpellManager.HasSpell("Call of the Spirits"))
                return;
            if (slot >= MultiCastSlot.AncestorsFire && !SpellManager.HasSpell("Call of the Ancestors"))
                return;
            if (!SpellManager.HasSpell("Call of the Elements"))
                return;

            Lua.DoString("SetMultiCastSpell({0})", (int)slot);
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

            var totems = StyxWoW.Me.Totems();
            foreach (var t in totems)
            {
                if (t != null && t.Unit != null)
                    DestroyTotem(t.Type);
            }
        }

        /// <summary>Destroys the totem described by type.</summary>
        /// <remarks>Created 3/26/2011.</remarks>
        /// <param name="type">The type.</param>
        public static void DestroyTotem(WoWTotemType type)
        {
            if (type == WoWTotemType.None)
                return;

            Lua.DoString("DestroyTotem({0})", (int)type);
        }

        /// <summary>Gets a totem information for the specified type of totem.</summary>
        /// <remarks>Created 3/26/2011.</remarks>
        /// <param name="type">The type.</param>
        /// <returns>The totem information.</returns>
        public static WoWTotemInfo GetTotemInfo(WoWTotemType type)
        {
            return StyxWoW.Me.Totems().FirstOrDefault(t => t.Type == type);
        }

        public static bool CanPlaceTotem(WoWTotem totem)
        {
            var spell = totem.GetTotemSpellId();
            return StyxWoW.Me.GetTotemBarSpells((int)totem.GetTotemType() - 1).Any(s => s.Id == spell);
        }
    }
}
