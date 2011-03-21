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

using System.Linq;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        private bool NeedKings
        {
            get
            {
                return Me.PartyMembers.Any(p => p.Class == WoWClass.Druid || (p.Class == WoWClass.Paladin && p.Guid != Me.Guid)) ||
                       Me.RaidMembers.Any(p => p.Class == WoWClass.Druid || (p.Class == WoWClass.Paladin && p.Guid != Me.Guid));
            }
        }

        private bool CurrentTargetIsUndeadOrDemon { get { return Me.CurrentTarget.CreatureType == WoWCreatureType.Undead || Me.CurrentTarget.CreatureType == WoWCreatureType.Demon; } }

        [Class(WoWClass.Paladin)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Spec(TalentSpec.HolyPaladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Context(WoWContext.All)]
        public Composite CreatePaladinBuffComposite()
        {
            return
                new PrioritySelector(
                    // This won't run, but it's here for changes in the future. We NEVER run this method if we're mounted.
                    CreateSpellBuffOnSelf("Crusader Aura", ret => Me.Mounted),
                    // Ghetto Kings Check
                    CreateSpellBuffOnSelf("Blessing of Might", ret => Me.HasAura("Blessing of Forgotten Kings") && !Me.HasAura("Blessing of Might")),
                    //Party Buffs
                    new Decorator(
                        ret => Me.IsInParty || Me.IsInRaid,
                        new PrioritySelector(
                            // Might if Druid or another Pally is in Group or Raid
                            CreateSpellBuffOnSelf(
                                "Blessing of Might", ret => NeedKings &&
                                                            (!Me.HasAura("Blessing of Kings") || Me.Auras["Blessing of Kings"].CreatorGuid != Me.Guid)
                                                            && !Me.HasAura("Blessing of Might")),
                            // Kings if no Druid or another Pally is in Group or Raid
                            CreateSpellBuffOnSelf(
                                "Blessing of Kings", ret => !NeedKings &&
                                                            (!Me.HasAura("Blessing of Might") || Me.Auras["Blessing of Might"].CreatorGuid != Me.Guid) &&
                                                            !Me.HasAura("Embrace of the Shale Spider") &&
                                                            !Me.HasAura("Mark of the Wild") &&
                                                            !Me.HasAura("Blessing of Forgotten Kings") &&
                                                            !Me.HasAura("Blessing of Kings")),
                            CreateSpellBuffOnSelf("Seal of Truth"),
                            CreateSpellBuffOnSelf("Retribution Aura", ret => !Me.Mounted)
                            )),
                    // Solo Buffs
                    new Decorator(
                        ret => !Me.IsInParty && !Me.IsInRaid,
                        new PrioritySelector(
                            CreateSpellBuffOnSelf(
                                "Blessing of Might", ret =>
                                                     (!Me.HasAura("Blessing of Kings") || Me.Auras["Blessing of Kings"].CreatorGuid != Me.Guid)
                                                     && !Me.HasAura("Blessing of Might")),
                            CreateSpellBuffOnSelf(
                                "Blessing of Kings", ret =>
                                                     (!Me.HasAura("Blessing of Might") || Me.Auras["Blessing of Might"].CreatorGuid != Me.Guid) &&
                                                     !Me.HasAura("Embrace of the Shale Spider") &&
                                                     !Me.HasAura("Mark of the Wild") &&
                                                     !Me.HasAura("Blessing of Forgotten Kings") &&
                                                     !Me.HasAura("Blessing of Kings")),
                            //uncomment Truth line to use it over Insight.
                            //CreateSpellBuffOnSelf("Seal of Truth"),
                            CreateSpellBuffOnSelf("Seal of Insight", ret => !Me.HasAura("Seal of Truth")),
                            CreateSpellBuffOnSelf("Devotion Aura", ret => !Me.Mounted)
                            )),
                    CreateSpellBuffOnSelf(
                        "Seal of Righteousness",
                        ret =>
                        !SpellManager.HasSpell("Seal of Truth") && !SpellManager.HasSpell("Seal of Insight") && !Me.HasAura("Seal of Righteousness") && !Me.HasAura("Seal of Insight"))
                    );
        }
    }
}