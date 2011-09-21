using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;

namespace Singular.Helpers
{
    class Group
    {
        public static WoWUnit Tank
        {
            get
            {
                var unit = StyxWoW.Me.PartyMemberInfos.FirstOrDefault(p => p.Role == WoWPartyMember.GroupRole.Tank);
                if (unit != null)
                    return unit.ToPlayer();
                return null;
            }
        }
        public static WoWUnit Healer
        {
            get
            {
                var unit = StyxWoW.Me.PartyMemberInfos.FirstOrDefault(p => p.Role == WoWPartyMember.GroupRole.Healer);
                if (unit != null)
                    return unit.ToPlayer();
                return null;
            }
        }

        /// <summary>Gets a player by class priority. The order of which classes are passed in, is the priority to find them.</summary>
        /// <remarks>Created 9/9/2011.</remarks>
        /// <param name="range"></param>
        /// <param name="includeDead"></param>
        /// <param name="classes">A variable-length parameters list containing classes.</param>
        /// <returns>The player by class prio.</returns>
        public static WoWUnit GetPlayerByClassPrio(float range, bool includeDead, params WoWClass[] classes)
        {
            foreach (var woWClass in classes)
            {

                var unit =
                    StyxWoW.Me.PartyMemberInfos.FirstOrDefault(
                        p => p.ToPlayer() != null && p.ToPlayer().Distance < range && p.ToPlayer().Class == woWClass);

                if (unit != null)
                    if (!includeDead && unit.Dead || unit.Ghost)
                        return unit.ToPlayer();
            }
            return null;
        }
    }
}
