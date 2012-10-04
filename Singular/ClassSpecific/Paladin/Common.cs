using System.Collections.Generic;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;


namespace Singular.ClassSpecific.Paladin
{

    enum PaladinBlessings
    {
        Auto, Kings, Might
    }

    public class Common
    {
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Paladin)]
        public static Composite CreatePaladinPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreatePaladinBlessBehavior(),
                    new Decorator(
                        ret => TalentManager.CurrentSpec == WoWSpec.PaladinHoly,
                        new PrioritySelector(
                            
                            Spell.BuffSelf("Seal of Insight"),
                            Spell.BuffSelf("Seal of Righteousness", ret => !SpellManager.HasSpell("Seal of Insight"))
                            )),
                    new Decorator(
                        ret => TalentManager.CurrentSpec != WoWSpec.PaladinHoly,
                        new PrioritySelector(
                            Spell.BuffSelf("Righteous Fury", ret => TalentManager.CurrentSpec == WoWSpec.PaladinProtection && StyxWoW.Me.GroupInfo.IsInParty)
                            /*
                            Spell.BuffSelf("Seal of Truth"),
                            Spell.BuffSelf("Seal of Righteousness", ret => !SpellManager.HasSpell("Seal of Truth"))
                             */
                            ))

                    );
        }

        private static Composite CreatePaladinBlessBehavior()
        {
            return
                new PrioritySelector(
                    Spell.Cast("Blessing of Kings",
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            if (SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Might)
                                return false;
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
                                             !p.HasAura("Embrace of the Shale Spider") &&
                                             !p.HasAura("Legacy of the Emperor")
                                             );
                        }),
                    Spell.Cast("Blessing of Might",
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
                                             !p.HasAura("Blessing of Might") &&
                                             (SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Might ||
                                             ((p.HasAura("Blessing of Kings") && !p.HasMyAura("Blessing of Kings")) ||
                                               p.HasAura("Mark of the Wild") || 
                                               p.HasAura("Grace of Air")
                                               )));
                        })
                    );
        }
    }
}
