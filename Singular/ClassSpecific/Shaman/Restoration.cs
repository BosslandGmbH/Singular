#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author: $
// $Date: $
// $HeadURL: $
// $LastChangedBy: $
// $LastChangedDate: $
// $LastChangedRevision:  $
// $Revision:  $

#endregion

using System;
using System.Collections.Generic;
using System.Linq;

using Singular.ClassSpecific.Shaman;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.RestorationShaman)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public Composite CreateRestoShamanHealRest()
        {
            return new PrioritySelector(
                // Heal self before resting. There is no need to eat while we have 100% mana
                CreateRestoShamanHealOnlyBehavior(true),
                // Rest up! Do this first, so we make sure we're fully rested.
                CreateDefaultRestComposite(SingularSettings.Instance.DefaultRestHealth, SingularSettings.Instance.DefaultRestMana),
                // Make sure we're healing OOC too!
                CreateRestoShamanHealOnlyBehavior(),
                // Can we res people?
                new Decorator(
                    ret => ResurrectablePlayers.Count != 0,
                    CreateSpellCast("Ancestral Spirit", ret => true, ret => ResurrectablePlayers.FirstOrDefault()))
                );
        }

        private Composite CreateRestoShamanHealOnlyBehavior()
        {
            return CreateRestoShamanHealOnlyBehavior(false);
        }

        private Composite CreateRestoShamanHealOnlyBehavior(bool selfOnly)
        {
            NeedHealTargeting = true;

            return new
                PrioritySelector(
                    CreateWaitForCastWithCancel(SingularSettings.Instance.IgnoreHealTargetsAboveHealth),

                    new Decorator(
                        ret => SpellManager.HasSpell("Call of the Elements")
                            && RaFHelper.Leader != null
                            && TotemManager.TotemsInRangeOf(RaFHelper.Leader) == 0
                            && (Me.Combat || RaFHelper.Leader.Combat),
                        new Sequence(
                            new DecoratorContinue(ret => (Me.Totems().Count(t => t.Unit != null) != 0),
                                new Action(ret => TotemManager.RecallTotems())),
                            new Action(ret => TotemManager.SetupTotemBar()),
                            new Action(ret => TotemManager.CallTotems()))),

                    new Decorator(
                        ret => HealTargeting.Instance.FirstUnit != null,
                        new PrioritySelector(
                            ctx => selfOnly ? Me : HealTargeting.Instance.FirstUnit,

                            CreateSpellCast(
                                "Earth Shield",
                                ret => RaFHelper.Leader != null && (WoWUnit)ret == RaFHelper.Leader
                                   && (!RaFHelper.Leader.HasAura("Earth Shield") || RaFHelper.Leader.Auras["Earth Shield"].StackCount < (Me.Combat ? 1 : 4)),
                                ret => RaFHelper.Leader),

                            CreateSpellCast(
                                "Healing Surge",
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Shaman.RAF_HealingSurge_Health,
                                ret => (WoWUnit)ret),

                            CreateSpellCast(
                                "Riptide",
                                ret => !HasAuraStacks("Tidal Waves", 1, Me),
                                ret => (WoWUnit)ret),

                            new Decorator(ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Shaman.RAF_GreaterHealingWave_Health,
                                new Sequence(
                                    CreateSpellCast(
                                        "Unleash Elements",
                                        ret => true,
                                        ret => (WoWUnit)ret),
                                    CreateSpellCast(
                                        "Greater Healing Wave",
                                        ret => true,
                                        ret => (WoWUnit)ret)
                                    )
                                ),

                            CreateSpellCast(
                                "Chain Heal",
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Shaman.RAF_ChainHeal_Health && WillChainHealHop((WoWUnit) ret),
                                ret => (WoWUnit)ret),

                            CreateSpellCast(
                                "Riptide",
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Shaman.RAF_Riptide_Health,
                                ret => (WoWUnit)ret),

                            CreateSpellCast(
                                "Healing Wave",
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Shaman.RAF_HealingWave_Health,
                                ret => (WoWUnit)ret)
                        )
                    )
                );
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.RestorationShaman)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateRestoShamanCombatBuffs()
        {
            return new PrioritySelector(
                
                            CreateSpellCast(
                                "Water Shield",
                                ret => !Me.HasAura("Earth Shield") 
                                   && (!Me.Auras.ContainsKey("Water Shield") || Me.Auras["Water Shield"].StackCount < (Me.Combat ? 1 : 3)),
                                ret => Me)

                );
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.RestorationShaman)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateRestoShamanCombat()
        {
            return
                new PrioritySelector(
                // Firstly, deal with healing people!
                    CreateRestoShamanHealOnlyBehavior(),
                    new Decorator(
                        ret => !Me.IsInParty,
                        new PrioritySelector(
                            CreateEnsureTarget(),
                            CreateMoveToAndFace(39, ret => Me.CurrentTarget),
                            CreateWaitForCast(true),
                            CreateSpellBuff("Flame Shock"),
                            CreateSpellCast("Lava Burst"),
                            CreateSpellCast("Earth Shock", ret => ((WoWUnit)ret).HasAura("Flame Shock")),
                            CreateSpellCast("Lightning Bolt")
                            )
                        )
                    );
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.RestorationShaman)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateRestoShamanPull()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => !Me.IsInParty,
                        CreateSpellCast("Lightning Bolt"))
                );
        }

    }
}