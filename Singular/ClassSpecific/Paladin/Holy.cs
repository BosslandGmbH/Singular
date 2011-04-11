using System;
using System.Linq;

using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic;
using Styx.Logic.Combat;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.HolyPaladin)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public Composite CreateHolyPaladinRest()
        {
            return new PrioritySelector(
                // Heal self before resting. There is no need to eat while we have 100% mana
                CreatePaladinHealBehavior(true),
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                CreateDefaultRestComposite(SingularSettings.Instance.DefaultRestHealth, SingularSettings.Instance.DefaultRestMana),
                // Make sure we're healing OOC too!
                CreatePaladinHealBehavior(),
                // Can we res people?
                new Decorator(
                    ret => ResurrectablePlayers.Count != 0,
                    CreateSpellCast("Redemption", ret => true, ret => ResurrectablePlayers.FirstOrDefault()))
                );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.HolyPaladin)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateHolyPaladinCombatBehavior()
        {
            return 
                new PrioritySelector(
                    CreatePaladinHealBehavior(),
                    CreateEnsureTarget(),
                    CreateSpellBuff("Judgement", ret => SpellManager.HasSpell("Judgement") && Me.CurrentTarget.Distance <= SpellManager.Spells["Judgement"].MaxRange - 2),
                    new Decorator(
                        ret => !Me.IsInParty && !Me.IsInRaid,
                        new PrioritySelector(
                            CreateMoveToAndFace(28, ret => Me.CurrentTarget),
                            CreateSpellBuff("Judgement"),
                            CreateSpellCast("Hammer of Wrath"),
                            CreateSpellCast("Holy Shock"),
                            CreateSpellCast("Crusader Strike"),
                            CreateSpellCast("Exorcism"),
                            CreateSpellCast("Holy Wrath"),
                            CreateSpellCast("Consecration")
                            ))
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.HolyPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateHolyPaladinPullBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !Me.IsInParty && !Me.IsInRaid,
                    new PrioritySelector(
                        CreateEnsureTarget(),
                        CreateFaceUnit(),
                        CreateSpellCast("Judgement", false),
                        CreateAutoAttack(true),
                        CreateMoveToAndFace()
                        ))
                );
        }

        private Composite CreatePaladinHealBehavior()
        {
            return CreatePaladinHealBehavior(false);
        }

        private Composite CreatePaladinHealBehavior(bool selfOnly)
        {
            NeedHealTargeting = true;

            return 
                new PrioritySelector(
                    CreateWaitForCastWithCancel(),
                    new Decorator(
                    ret => HealTargeting.Instance.FirstUnit != null,
                        new PrioritySelector(
                            ctx => selfOnly ? Me : HealTargeting.Instance.FirstUnit,
                            CreateSpellBuff(
                                "Beacon of Light",
                                ret => (Me.IsInParty || Me.IsInRaid) && RaFHelper.Leader != null && (WoWUnit)ret == RaFHelper.Leader,
                                ret => (WoWUnit)ret),
                            CreateSpellCast(
                                "Lay on Hands",
                                ret => Me.Combat && !((WoWUnit)ret).HasAura("Forbearance") && 
                                       ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.LayOnHandsHealthHoly,
                                ret => (WoWUnit)ret),
                            CreateSpellCast(
                                "Light of Dawn",
                                ret => Me.CurrentHolyPower == 3 && 
                                       NearbyFriendlyPlayers.Count(p => 
                                           p.HealthPercent <= SingularSettings.Instance.Paladin.LightOfDawnHealth && p != Me &&
                                           p.Distance < 30 && Me.IsSafelyFacing(p.Location)) >= SingularSettings.Instance.Paladin.LightOfDawnCount,
                                ret => (WoWUnit)ret),
                            CreateSpellCast(
                                "Word of Glory",
                                ret => Me.CurrentHolyPower == 3 && ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.WordOfGloryHealth,
                                ret => (WoWUnit)ret),
                            CreateSpellCast(
                                "Holy Shock",
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.HolyLightHealth,
                                ret => (WoWUnit)ret),
                            CreateSpellCast(
                                "Flash of Light",
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.FlashOfLightHealth,
                                ret => (WoWUnit)ret),
                            CreateSpellCast(
                                "Divine Light",
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.DivineLightHealth,
                                ret => (WoWUnit)ret),
                            CreateSpellCast(
                                "Holy Light",
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin.HolyLightHealth,
                                ret => (WoWUnit)ret)
                            )));
        }
    }
}