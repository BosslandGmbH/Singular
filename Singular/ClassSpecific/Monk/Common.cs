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
{

    public class Common
    {


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, WoWSpec.MonkWindwalker)]
        private static Composite CreateMonkPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.Cast("Legacy of the Emperor", // +5% stats
                               ret => StyxWoW.Me,
                               ret =>
                               Unit.NearbyGroupMembers.Any(
                                   p =>
                                   p.IsAlive &&
                                   !p.HasAnyAura("Blessing of Kings", "Mark of the Wild", "Legacy of the Emperor","Embrace of the Shale Spider"))),

                    Spell.Cast("Legacy of the White Tiger", // +5% crit
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
                                           p =>
                                           p.DistanceSqr < 40*40 && p.IsAlive &&
                                           !p.HasAnyAura("Legacy of the White Tiger", "Leader of the Pack",
                                                         "Dalaran Brilliance", "Arcane Brilliance"));
                                       }
                        )
                    );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Monk )]
        public static Composite CreateMonkRest()
        {
            return new PrioritySelector(

                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour(),
                // Can we res people?
                Spell.Resurrect("Resuscitate")
                );
        }


    }
}