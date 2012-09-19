using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;



using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Monk
{ /*
    public class Common
    {
        [Spec(WoWSpec.WindwalkerMonk)]
        [Spec(WoWSpec.MistweaverMonk)]
        [Spec(WoWSpec.BrewmasterMonk)]

        [Spec((WoWSpec)0)]
        [Context(WoWContext.All)]
        private static Composite CreateMonkPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.Cast("Legacy of the Emperor",
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            var players = new List<WoWPlayer>();

                            if (StyxWoW.Me.GroupInfo.IsInRaid)
                                players.AddRange(StyxWoW.Me.RaidMembers);
                            else if (StyxWoW.Me.GroupInfo.IsInParty)
                                players.AddRange(StyxWoW.Me.PartyMembers);

                            players.Add(StyxWoW.Me);

                            return players.Any(
                                        p => p.DistanceSqr < 40 * 40 && p.IsAlive &&        
                                             !p.HasAura("Blessing of Kings") &&
                                             !p.HasAura("Mark of the Wild") &&
                                             !p.HasAura("Legacy of the Emperor")
                                             );
                        }),
                    Spell.Cast("Legacy of the White Tiger",
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            var players = new List<WoWPlayer>();

                            if (StyxWoW.Me.GroupInfo.IsInRaid)
                                players.AddRange(StyxWoW.Me.RaidMembers);
                            else if (StyxWoW.Me.GroupInfo.IsInParty)
                                players.AddRange(StyxWoW.Me.PartyMembers);

                            players.Add(StyxWoW.Me);

                            return players.Any(
                                        p => p.DistanceSqr < 40 * 40 && p.IsAlive &&
                                             !p.HasAura("Legacy of the White Tiger") &&
                                              ((p.HasAura("Legacy of the Emperor)") && !p.HasMyAura("Legacy of the Emperor)")) ||
                                               p.HasAura("Blessing of Kings") ||
                                               p.HasAura("Mark of the Wild")));
                        })
                    );
        }
    }
   */
}