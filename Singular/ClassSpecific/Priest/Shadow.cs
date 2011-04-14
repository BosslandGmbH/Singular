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
using CommonBehaviors.Actions;
using Singular.Settings;
using Styx.Combat.CombatRoutine;
using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateShadowPriestCombat()
        {
            return new PrioritySelector(
                // targetting behaviours
                CreateEnsureTarget(),
                CreateMoveToAndFace(30, ret => Me.CurrentTarget),
                CreateWaitForCast(true),
                
                // cast devouring plague first if option is set
                CreateSpellBuff("Devouring Plague", ret => SingularSettings.Instance.Priest.DevouringPlageuFirst, true),

                // don't attempt to heal unless below a certain percentage health
                new Decorator(ret => Me.HealthPercent < SingularSettings.Instance.Priest.DontHealPercent,
                    CreateDiscHealOnlyBehavior(true)),
                new Decorator(ret => Me.IsTargetingMeOrPet || Me.CurrentTarget.IsFriendly,
                    new Action(ret => Me.ClearTarget())),
                
                // always try to be in shadow form, but not if we're below the above health % (stops in and out shadowform spam)
                CreateSpellBuffOnSelf("Shadowform", ret => SingularSettings.Instance.Priest.DontShadowFormHealth && Me.HealthPercent < SingularSettings.Instance.Priest.DontHealPercent),

                // finish the guy off first if we can
                CreateSpellCast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent < 25),

                // if we've got 2+ unfriendly units beating on us, psychic horror on one
                CreateSpellCast("Psychic Horror", 
                    ret => NearbyUnfriendlyUnits.Count(unit => SingularSettings.Instance.Priest.UsePsychicHorrorAdds && unit.Aggro && CanCast("Psychic Horror", unit, false)) >= 2,
                    ret => NearbyUnfriendlyUnits.FirstOrDefault(unit => Me.CurrentTargetGuid != unit.Guid && unit.Aggro && CanCast("Psychic Horror", unit, false))),

                // stop person casting
                CreateSpellCast("Silence", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.CastingSpell != null),
                CreateSpellCast("Psychic Horror", ret => SingularSettings.Instance.Priest.UsePsychicHorrorInterrupt && Me.CurrentTarget.IsCasting || Me.CurrentTarget.CastingSpell != null),

                // use dispersion if we can
                CreateSpellCast("Dispersion", ret => Me.ManaPercent < SingularSettings.Instance.Priest.DispersionMana),
                new Decorator(ret => HasAuraStacks("Dispersion", 1),
                    new ActionAlwaysSucceed()),

                // if it's a totem or we're not in combat (ie, pulling), use spike
                CreateSpellBuff("Mind Spike", ret => Me.CurrentTarget.CreatureType == Styx.WoWCreatureType.Totem || !Me.Combat, true),
                // use spike a second time if we can, either after pull or after dots have run out for whatever reason
                CreateSpellCast("Mind Spike", ret => HasMyAura("Mind Spike", Me.CurrentTarget, 1), true),
                // finally use mind blast to finish off the pull
                CreateSpellCast("Mind Blast", ret => HasMyAura("Mind Spike", Me.CurrentTarget, 2)),

                // start up with the dots
                CreateSpellBuff("Vampiric Touch", true),
                CreateSpellBuff("Devouring Plague"),
                CreateSpellBuff("Shadow Word: Pain"),

                // mind blast if it's appropriate
                CreateSpellCast("Mind Blast", ret => HasAuraStacks("Shadow Orb", 1) && !HasAuraStacks("Empowered Shadow", 1)),
                // attempt to cast shield before flay
                CreateSpellBuffOnSelf("Power Word: Shield", ret => !HasAuraStacks("Weakened Soul", 0)),
                // flay if we have shield or if no one's beating on us
                CreateSpellCast("Mind Flay", ret => !Me.IsMoving && (NearbyUnfriendlyUnits.Count(u => u.CurrentTargetGuid == Me.Guid) <= 0 || HasAuraStacks("Power Word: Shield", 0))),
                // maybe try a spike if there's none of our dots on it
                CreateSpellBuff("Mind Spike", ret => !HasMyAura("Vampiric Touch", Me.CurrentTarget) && !HasMyAura("Devouring Plague", Me.CurrentTarget) && !HasMyAura("Shadow Word: Pain", Me.CurrentTarget), true),
                // attempt to blast, nothing else to do
                CreateSpellCast("Mind Blast"),

                // finally, no mana, try to use archangel if we have _any_ stacks of evangelism
                CreateSpellBuff("Archangel", ret => (HasAuraStacks("Dark Evangelism", 1)) && Me.ManaPercent <= SingularSettings.Instance.Priest.ArchangelMana),
                // use wand
                new Decorator(ret => SingularSettings.Instance.Priest.UseWand && !IsWanding,
                    CreateUseWand()),
                // if all else fails .... (even tho we prolly won't have range)
                CreateAutoAttack(false)
                );
        }

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.PullBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateShadowPriestPullBuffs()
        {
            return new PrioritySelector(
                CreateSpellBuffOnSelf("Power Word: Shield", ret => !HasAuraStacks("Weakened Soul", 0))
                );
        }
    }
}