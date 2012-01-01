using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Priest
{
    public class Shadow
    {
        private static DateTime _lastMindBlast = DateTime.Now;

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateShadowPriestCombat()
        {
            return new PrioritySelector(
                // targetting behaviours
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.PreventDoubleCast("Devouring Plague", "Vampiric Touch"),
                Spell.WaitForCast(true),
                // cast devouring plague first if option is set
                Spell.StopAndBuff("Devouring Plague", ret => SingularSettings.Instance.Priest.DevouringPlagueFirst),

                // don't attempt to heal unless below a certain percentage health
                new Decorator(ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.Priest.DontHealPercent,
                    Discipline.CreateDiscHealOnlyBehavior(true)),

                // always try to be in shadow form, but not if we're below the above health % (stops in and out shadowform spam)
                Spell.BuffSelf("Shadowform", ret => SingularSettings.Instance.Priest.DontShadowFormHealth && StyxWoW.Me.HealthPercent < SingularSettings.Instance.Priest.DontHealPercent),

                // finish the guy off first if we can
                Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 25),

                // if we've got 2+ unfriendly units beating on us, psychic horror on one
                Spell.Cast("Psychic Horror",
                    ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(unit => StyxWoW.Me.CurrentTargetGuid != unit.Guid && unit.Aggro && SpellManager.CanCast("Psychic Horror", unit, false)),
                    ret => SingularSettings.Instance.Priest.UsePsychicHorrorAdds && Unit.NearbyUnfriendlyUnits.Count(unit => unit.Aggro && SpellManager.CanCast("Psychic Horror", unit, false)) >= 2),


                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.Cast("Psychic Horror", ret => SingularSettings.Instance.Priest.UsePsychicHorrorInterrupt && StyxWoW.Me.CurrentTarget.IsCasting || StyxWoW.Me.CurrentTarget.CastingSpell != null),

                // use dispersion if we can
                Spell.Cast("Dispersion", ret => StyxWoW.Me.ManaPercent < SingularSettings.Instance.Priest.DispersionMana),
                new Decorator(ret => StyxWoW.Me.HasAura("Dispersion", 0),
                    new ActionAlwaysSucceed()),

                Spell.Cast("Archangel", ret => SingularSettings.Instance.Priest.AlwaysArchangel5 && StyxWoW.Me.HasAura("Dark Evangelism", 5)),

                // open with spike or if its a totem
                Spell.Cast("Mind Spike",
                            ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Mind Trauma") &&
                                   (StyxWoW.Me.CurrentTarget.CreatureType == WoWCreatureType.Totem || !StyxWoW.Me.Combat)),
                // use mind blast after 2+ spikes, or if orbs, 
                Spell.Cast("Mind Blast", ret => StyxWoW.Me.CurrentTarget.HasMyAura("Mind Spike", 2)),
                // use spike a second time if we can, either after pull or after dots have run out for whatever reason
                Spell.Cast("Mind Spike", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Mind Trauma") && !StyxWoW.Me.CurrentTarget.HasMyAura("Vampiric Touch") && !StyxWoW.Me.CurrentTarget.HasMyAura("Devouring Plague") && !StyxWoW.Me.CurrentTarget.HasMyAura("Shadow Word: Pain")),

                // start up with the dots
                Spell.Buff("Vampiric Touch"),
                Spell.Buff("Devouring Plague", ret => !StyxWoW.Me.CurrentTarget.IsMechanical || StyxWoW.Me.CurrentTarget.IsBoss()),
                Spell.Buff("Shadow Word: Pain", ret => !SpellManager.HasSpell("Vampiric Touch") || StyxWoW.Me.IsInInstance || StyxWoW.Me.CurrentTarget.IsPlayer),

                // blast for shadow orbs or timer
                new Decorator(ret => ((StyxWoW.Me.HasAura("Shadow Orb", SingularSettings.Instance.Priest.MindBlastOrbs) && !StyxWoW.Me.HasAura("Empowered Shadow", 0)) || _lastMindBlast + TimeSpan.FromSeconds(SingularSettings.Instance.Priest.MindBlastTimer) < DateTime.Now),
                    new Sequence(
                        new Action(ret => _lastMindBlast = DateTime.Now),
                        Spell.Cast("Mind Blast"))),

                // attempt to cast shield before flay, if we need to
                Spell.BuffSelf("Power Word: Shield", ret => StyxWoW.Me.HealthPercent < 80 && StyxWoW.Me.CurrentTarget.HealthPercent > 30 &&
                    !StyxWoW.Me.HasAura("Weakened Soul", 0) && Unit.NearbyUnfriendlyUnits.Count(u => u.CurrentTargetGuid == StyxWoW.Me.Guid) > 0),
                // flay if we have shield or if no one's beating on us
                Spell.Cast("Mind Flay", ret => !StyxWoW.Me.IsMoving && (Unit.NearbyUnfriendlyUnits.Count(u => u.CurrentTargetGuid == StyxWoW.Me.Guid) <= 0 || StyxWoW.Me.HasAura("Power Word: Shield", 0))),
                // maybe try a spike if there's none of our dots on it
                Spell.Cast("Mind Spike", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Vampiric Touch") && !StyxWoW.Me.CurrentTarget.HasMyAura("Devouring Plague") && !StyxWoW.Me.CurrentTarget.HasMyAura("Shadow Word: Pain")),

                // finally, no mana?, try to use archangel if we have _any_ stacks of evangelism
                Spell.Cast("Archangel", ret => StyxWoW.Me.HasAura("Dark Evangelism") && StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Priest.ArchangelMana),

                // try to do _something_
                Spell.Cast("Mind Blast"),
                // use wand
                //Helpers.Common.CreateUseWand(ret => SingularSettings.Instance.Priest.UseWand),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }
    }
}
