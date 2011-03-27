// NOTE: THIS ENTIRE FILE WILL BE REMOVED IN THE NEXT HONORBUDDY RELEASE! DO NOT MODIFY IT!

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
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Styx.WoWInternals.WoWObjects
{
    public enum WoWTotemType
    {
        None = 0,
        Fire = 1,
        Earth = 2,
        Water = 3,
        Air = 4,
    }
    public enum WoWTotem
    {
        None = 0,

        StrengthOfEarth = (WoWTotemType.Earth << 16) + 8075,
        Earthbind = (WoWTotemType.Earth << 16) + 2484,
        Stoneskin = (WoWTotemType.Earth << 16) + 8071,
        Tremor = (WoWTotemType.Earth << 16) + 8143,
        EarthElemental = (WoWTotemType.Earth << 16) + 2062,
        Stoneclaw = (WoWTotemType.Earth << 16) + 5730,

        Searing = (WoWTotemType.Fire << 16) + 3599,
        Flametongue = (WoWTotemType.Fire << 16) + 8227,
        Magma = (WoWTotemType.Fire << 16) + 8190,
        FireElemental = (WoWTotemType.Fire << 16) + 2894,

        HealingStream = (WoWTotemType.Water << 16) + 5394,
        ManaSpring = (WoWTotemType.Water << 16) + 5675,
        ElementalResistance = (WoWTotemType.Water << 16) + 8184,
        TranquilMind = (WoWTotemType.Water << 16) + 87718,
        ManaTide = (WoWTotemType.Water << 16) + 16190,

        Windfury = (WoWTotemType.Air << 16) + 8512,
        Grounding = (WoWTotemType.Air << 16) + 8177,
        WrathOfAir = (WoWTotemType.Air << 16) + 3738,
    }

    public class WoWTotemInfo
    {
        private NativeTotemInfo _info;

        internal WoWTotemInfo(int slot)
        {
            const uint TOTEM_INFO_ARRAY_PTR = 0x00D9CA40 - 0x400000;
            _info = ObjectManager.Wow.ReadStructArrayRelative<NativeTotemInfo>(TOTEM_INFO_ARRAY_PTR, 4)[slot];
        }

        /// <summary>Gets a unique identifier for the totem (Only valid if the totem is actually out!).</summary>
        /// <value>The identifier of the unique.</value>
        public ulong Guid { get { return _info.Guid; } }

        /// <summary>Returns true if the totem has expired. (Ran its full duration)</summary>
        /// <value>true if expired, false if not.</value>
        public bool Expired { get { return Expires > DateTime.Now; } }

        /// <summary>Gets the unit that this totem info represents.</summary>
        /// <value>The unit.</value>/
        public WoWUnit Unit { get { return ObjectManager.GetObjectByGuid<WoWUnit>(Guid); } }

        /// <summary>Gets the slot for this totem. (0-3).</summary>
        /// <value>The slot.</value>
        public uint Slot { get { return _info.Slot; } }

        /// <summary>Gets the duration, in milliseconds, that the totem will stay out.</summary>
        /// <value>The duration.</value>
        public uint Duration { get { return _info.Duration; } }

        /// <summary>Gets the time of when this totem was laid out.</summary>
        /// <value>The time of the start.</value>
        public DateTime StartTime { get { return DateTime.Now.Subtract(TimeSpan.FromMilliseconds(Environment.TickCount)).AddMilliseconds(_info.StartTime); } }

        /// <summary>Gets the time when this totem will expire.</summary>
        /// <value>The expires.</value>
        public DateTime Expires { get { return StartTime.AddMilliseconds(Duration); } }

        /// <summary>Gets the totem this represents. (Strength of Earth, Healing Stream, etc)</summary>
        /// <value>The wo w totem.</value>
        public WoWTotem WoWTotem
        {
            get
            {
                if (Unit == null)
                {
                    return WoWTotem.None;
                }
                return (WoWTotem)((int)Type << 16) + (int)Unit.CreatedBySpellId;
            }
        }

        /// <summary>Gets the spell that creates this totem.</summary>
        /// <value>The spell.</value>
        public WoWSpell Spell { get { return WoWSpell.FromId(WoWTotem.GetTotemSpellId()); } }

        /// <summary>Gets the full pathname of the icon file. (This is an MPQ path!)</summary>
        /// <value>The full pathname of the icon file.</value>
        public string IconPath { get { return ObjectManager.Wow.ReadString(_info.IconPtr); } }

        /// <summary>Gets the name of this totem. (This is NOT always in English! Do not use this as an identifier!)</summary>
        /// <value>The name.</value>
        public string Name { get { return ObjectManager.Wow.ReadString(_info.NamePtr); } }

        /// <summary>Gets the type of totem this represents. (Fire, Earth, Water, Air)</summary>
        /// <value>The type.</value>
        public WoWTotemType Type { get { return (WoWTotemType)Slot+1; } }

        #region Nested type: NativeTotemInfo

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct NativeTotemInfo
        {
            internal readonly uint Slot;
            internal readonly uint Dword4;
            internal readonly ulong Guid;
            internal readonly uint NamePtr;
            internal readonly uint Duration;
            internal readonly uint StartTime;
            internal readonly uint IconPtr;
        }

        #endregion
    }

    public static class WoWTotemExtensions
    {
        public static int GetTotemSpellId(this WoWTotem totem)
        {
            return (int)totem & 0xFFFF;
        }

        public static WoWTotemType GetTotemType(this WoWTotem totem)
        {
            return (WoWTotemType)((uint)totem >> 16);
        }
    }

    // This is just here so we can 'mimic' the functionality in the next HB release.
    public static class LocalPlayerTotemExtensions
    {
        public static List<WoWTotemInfo> Totems(this LocalPlayer p)
        {
            List<WoWTotemInfo> ret = new List<WoWTotemInfo>();
            for (int i = 0; i < 4; i++)
            {
                ret.Add(new WoWTotemInfo(i));
            }
            return ret;
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct PlayerTotemBar
        {
            public uint HasTotems;
            public uint NumTotems;
            public uint SpellIds;
            public uint DwordC;
        }

        /// <summary>Gets the spells in a specified slot of the totem bar. (Valid values are 0-3)</summary>
        /// <remarks>Created 3/26/2011.</remarks>
        /// <param name="totemBarSlot">The totem bar slot.</param>
        /// <returns>The totem bar spells.</returns>
        /// <exception cref="ArgumentException">Value must be between 0 and 3</exception>
        public static List<WoWSpell> GetTotemBarSpells(this LocalPlayer player, int totemBarSlot)
        {
            if (totemBarSlot < 0 || totemBarSlot > 3)
                throw new ArgumentException("Value must be between 0 and 3", "totemBarSlot");

            uint ptr = 0x00DF8F68-0x400000; // lua_GetTotemInfo - follow the function after finding the dword above (the else statement) its in the else condition. Inside that func!.
            var bars = ObjectManager.Wow.ReadStructArrayRelative<PlayerTotemBar>(ptr, 4);
            var bar = bars[totemBarSlot];

            if (bar.HasTotems == 0)
                return new List<WoWSpell>();

            //Logging.Write(bar.ToString());
            List<WoWSpell> spells = new List<WoWSpell>();
            for (uint i = 0; i < bar.NumTotems; i++)
            {
                var spellId = ObjectManager.Wow.Read<int>(bar.SpellIds + (i * 4));
                if (spellId > 0)
                    spells.Add(WoWSpell.FromId(spellId));
            }
            return spells;
        }
    }
}