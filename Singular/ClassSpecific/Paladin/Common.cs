using System.Collections.Generic;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Singular.ClassSpecific.Paladin
{
    public enum PaladinAura
    {
        Auto,
        Devotion,
        Retribution,
        Resistance,
        Concentration,
    }

    public class Common
    {
        [Class(WoWClass.Paladin)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Spec(TalentSpec.HolyPaladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]
        public static Composite CreatePaladinPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    // This won't run, but it's here for changes in the future. We NEVER run this method if we're mounted.
                    Spell.BuffSelf("Crusader Aura", ret => StyxWoW.Me.Mounted),
                    CreatePaladinBlessBehavior(),
                    new Decorator(
                        ret => TalentManager.CurrentSpec == TalentSpec.HolyPaladin,
                        new PrioritySelector(
                            Spell.BuffSelf("Concentration Aura", ret => SingularSettings.Instance.Paladin.Aura == PaladinAura.Auto),
                            Spell.BuffSelf("Seal of Insight"),
                            Spell.BuffSelf("Seal of Righteousness", ret => !SpellManager.HasSpell("Seal of Insight"))
                            )),
                    new Decorator(
                        ret => TalentManager.CurrentSpec != TalentSpec.HolyPaladin,
                        new PrioritySelector(
                            Spell.BuffSelf("Righteous Fury", ret => TalentManager.CurrentSpec == TalentSpec.ProtectionPaladin),
                            Spell.BuffSelf(
                                "Devotion Aura",
                                ret =>
                                SingularSettings.Instance.Paladin.Aura == PaladinAura.Auto &&
                                (TalentManager.CurrentSpec == TalentSpec.ProtectionPaladin ||
                                 TalentManager.CurrentSpec == TalentSpec.Lowbie)),
                            Spell.BuffSelf(
                                "Retribution Aura",
                                ret =>
                                SingularSettings.Instance.Paladin.Aura == PaladinAura.Auto &&
                                TalentManager.CurrentSpec == TalentSpec.RetributionPaladin),
                            Spell.BuffSelf("Seal of Truth"),
                            Spell.BuffSelf("Seal of Righteousness", ret => !SpellManager.HasSpell("Seal of Truth"))
                            )),
                    new Decorator(
                        ret => SingularSettings.Instance.Paladin.Aura != PaladinAura.Auto,
                        new PrioritySelector(
                            Spell.BuffSelf("Devotion Aura", ret => SingularSettings.Instance.Paladin.Aura == PaladinAura.Devotion),
                            Spell.BuffSelf("Concentration Aura", ret => SingularSettings.Instance.Paladin.Aura == PaladinAura.Concentration),
                            Spell.BuffSelf("Resistance Aura", ret => SingularSettings.Instance.Paladin.Aura == PaladinAura.Resistance),
                            Spell.BuffSelf("Retribution Aura", ret => SingularSettings.Instance.Paladin.Aura == PaladinAura.Retribution)

                            ))
                    );
        }

        private static Composite CreatePaladinBlessBehavior()
        {
            return
                new PrioritySelector(
                    Spell.Cast("Blessing of Might",
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            //Ok, this is a bit complicated but it works :p /raphus
                            var players = StyxWoW.Me.IsInParty
                                              ? StyxWoW.Me.PartyMembers.Concat(new List<WoWPlayer> { StyxWoW.Me })
                                              : StyxWoW.Me.IsInRaid
                                                    ? StyxWoW.Me.RaidMembers.Concat(new List<WoWPlayer> { StyxWoW.Me })
                                                    : new List<WoWPlayer> { StyxWoW.Me };

                            var result = players.Any(
                                p => p.DistanceSqr < 40 * 40 && p.IsAlive &&
                                     (!p.HasAura("Blessing of Might") || p.Auras["Blessing of Might"].CreatorGuid != StyxWoW.Me.Guid) &&
                                     ((p.HasAura("Blessing of Kings") && p.Auras["Blessing of Kings"].CreatorGuid != StyxWoW.Me.Guid) ||
                                     p.HasAura("Mark of the Wild") || p.HasAura("Embrace of the Shale Spider")));

                            return result;
                        }),
                    Spell.Cast("Blessing of Kings",
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            var players = StyxWoW.Me.IsInParty
                                              ? StyxWoW.Me.PartyMembers.Union(new List<WoWPlayer> { StyxWoW.Me })
                                              : StyxWoW.Me.IsInRaid
                                                    ? StyxWoW.Me.RaidMembers.Union(new List<WoWPlayer> { StyxWoW.Me })
                                                    : new List<WoWPlayer> { StyxWoW.Me };

                            var result = players.Any(
                                p => p.DistanceSqr < 40*40 && p.IsAlive &&
                                     (!p.HasAura("Blessing of Kings") || p.Auras["Blessing of Kings"].CreatorGuid != StyxWoW.Me.Guid) &&
                                     !p.HasAura("Mark of the Wild") &&
                                     !p.HasAura("Embrace of the Shale Spider"));

                            return result;
                        })
                    );
        }
    }
}
