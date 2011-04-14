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
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Settings;
using Styx.Combat.CombatRoutine;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular
{
    partial class SingularRoutine
    {

        private DateTime _lastMindBlast = DateTime.Now;

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

                // always try to be in shadow form, but not if we're below the above health % (stops in and out shadowform spam)
                CreateSpellBuffOnSelf("Shadowform", ret => SingularSettings.Instance.Priest.DontShadowFormHealth && Me.HealthPercent < SingularSettings.Instance.Priest.DontHealPercent),

                // finish the guy off first if we can
                CreateSpellCast("Shadow Word: Death", ret => Me.CurrentTarget.HealthPercent < 25, false),

                // if we've got 2+ unfriendly units beating on us, psychic horror on one
                CreateSpellCast("Psychic Horror",
                    ret => SingularSettings.Instance.Priest.UsePsychicHorrorAdds && NearbyUnfriendlyUnits.Count(unit => unit.Aggro && CanCast("Psychic Horror", unit, false)) >= 2,
                    ret => NearbyUnfriendlyUnits.FirstOrDefault(unit => Me.CurrentTargetGuid != unit.Guid && unit.Aggro && CanCast("Psychic Horror", unit, false)), 
                    false),

                // stop person casting
                CreateSpellCast("Silence", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.CastingSpell != null, false),
                CreateSpellCast("Psychic Horror", ret => SingularSettings.Instance.Priest.UsePsychicHorrorInterrupt && Me.CurrentTarget.IsCasting || Me.CurrentTarget.CastingSpell != null),

                // use dispersion if we can
                CreateSpellCast("Dispersion", ret => Me.ManaPercent < SingularSettings.Instance.Priest.DispersionMana, false),
                new Decorator(ret => HasAuraStacks("Dispersion", 0),
                    new ActionAlwaysSucceed()),

                CreateSpellCast("Archangel", ret => SingularSettings.Instance.Priest.AlwaysArchangel5 && HasAuraStacks("Dark Evangelism", 5)),

                // open with spike or if its a totem
                CreateSpellCast("Mind Spike", ret => !HasMyAura("Mind Trauma", Me.CurrentTarget, 0) && (Me.CurrentTarget.CreatureType == Styx.WoWCreatureType.Totem || !Me.Combat), true),
                // use mind blast after 2+ spikes, or if orbs, 
                CreateSpellCast("Mind Blast", ret => HasMyAura("Mind Spike", Me.CurrentTarget, 2)),
                // use spike a second time if we can, either after pull or after dots have run out for whatever reason
                CreateSpellCast("Mind Spike", ret => !HasMyAura("Mind Trauma", Me.CurrentTarget, 0) && !HasMyAura("Vampiric Touch", Me.CurrentTarget) && !HasMyAura("Devouring Plague", Me.CurrentTarget) && !HasMyAura("Shadow Word: Pain", Me.CurrentTarget)),
               
                // start up with the dots
                CreateSpellBuff("Vampiric Touch", true),
                CreateSpellBuff("Devouring Plague"),
                CreateSpellBuff("Shadow Word: Pain"),

                // blast for shadow orbs or timer
                new Decorator(ret => ((HasAuraStacks("Shadow Orb", SingularSettings.Instance.Priest.MindBlastOrbs) && !HasAuraStacks("Empowered Shadow", 0)) || _lastMindBlast + TimeSpan.FromSeconds(SingularSettings.Instance.Priest.MindBlastTimer) < DateTime.Now),
                    new Sequence(
                        new Action(ret => _lastMindBlast = DateTime.Now),
                        CreateSpellCast("Mind Blast"))),

                // attempt to cast shield before flay, if we need to
                CreateSpellBuffOnSelf("Power Word: Shield", ret => !HasAuraStacks("Weakened Soul", 0) && NearbyUnfriendlyUnits.Count(u => u.CurrentTargetGuid == Me.Guid) > 0),
                // flay if we have shield or if no one's beating on us
                CreateSpellCast("Mind Flay", ret => !Me.IsMoving && (NearbyUnfriendlyUnits.Count(u => u.CurrentTargetGuid == Me.Guid) <= 0 || HasAuraStacks("Power Word: Shield", 0))),
                // maybe try a spike if there's none of our dots on it
                CreateSpellCast("Mind Spike", ret => !HasMyAura("Vampiric Touch", Me.CurrentTarget) && !HasMyAura("Devouring Plague", Me.CurrentTarget) && !HasMyAura("Shadow Word: Pain", Me.CurrentTarget)),

                // finally, no mana?, try to use archangel if we have _any_ stacks of evangelism
                CreateSpellCast("Archangel", ret => HasAuraStacks("Dark Evangelism", 0) && Me.ManaPercent <= SingularSettings.Instance.Priest.ArchangelMana),
                
                // try to do _something_
                CreateSpellCast("Mind Blast"),
                // use wand
                CreateUseWand(ret => SingularSettings.Instance.Priest.UseWand)
                );
        }
    }
}