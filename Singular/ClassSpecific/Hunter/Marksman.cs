using System;
using System.Linq;
using System.Threading;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Hunter
{
    public class Marksman
    {
        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.MarksmanshipHunter)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateMarksmanshipCombat()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.GotAlivePet,
                    new Action(ret => PetManager.CallPet(SingularSettings.Instance.Hunter.PetSlot))),
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Common.CreateHunterBackPedal(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Spell.Cast("Raptor Strike", ret => StyxWoW.Me.CurrentTarget.DistanceSqr < 5 * 5),
                //Interupt
                Spell.Cast("Silencing Shot", ret => StyxWoW.Me.CurrentTarget.IsCasting),
                // Heal pet when below 70
                Spell.Cast("Mend Pet", ret => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.HealthPercent < 70 && !StyxWoW.Me.Pet.HasAura("Mend Pet")),

                // Solo & leveling helpers
                Spell.Cast(
                    "Concussive Shot",
                    ret => (StyxWoW.Me.CurrentTarget.CurrentTarget == null && !StyxWoW.Me.IsInInstance) || StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me),
                Spell.Cast("Kill Command", ret => StyxWoW.Me.CurrentTarget.DistanceSqr < 5 * 5 && !StyxWoW.Me.IsInInstance),

                new Decorator(ret => Clusters.GetClusterCount(StyxWoW.Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 10f) >= 3,
                    new PrioritySelector(
                        new Decorator(
                            ret => SpellManager.CanCast("Explosive Trap") && StyxWoW.Me.PowerPercent >= 20,
                            new PrioritySelector(
                                Spell.BuffSelf("Trap Launcher"),
                                new Sequence(
                                    new Action(ret => Lua.DoString("RunMacroText(\"/cast Explosive Trap\")")),
                                    new Action(ret => LegacySpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location))))),
                        Spell.Cast("Multi-Shot", ret => !SpellManager.CanCast("Explosive Trap")),
                        Spell.Cast("Aimed Shot", ret => StyxWoW.Me.HasAura("Fire!")),
                        Spell.Cast("Kill Shot"),
                        Spell.Cast("Steady Shot", ret => !StyxWoW.Me.IsMoving)
                    )
                ),

                // Always keep it up on our target (for single target)!
                Spell.Buff("Hunter's Mark", ret => StyxWoW.Me.CurrentTarget.IsBoss() && !SpellManager.CanCast("Chimera Shot") && !StyxWoW.Me.CurrentTarget.HasAura("Marked for Death")),
                Spell.Buff("Serpent Sting"),
                Spell.Cast("Chimera Shot", ret =>
                    !StyxWoW.Me.IsInInstance ||
                    (
                        StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting") &&
                        (
                            StyxWoW.Me.CurrentTarget.Auras.Values.First(v => v.Name == "Serpent Sting" && v.CreatorGuid == StyxWoW.Me.Guid).TimeLeft.TotalSeconds < 5
                            || StyxWoW.Me.CurrentTarget.CurrentHealth < 90
                        )
                    ) ||
                    (
                        !StyxWoW.Me.CurrentTarget.HasAura("Hunter's Mark")
                        && !StyxWoW.Me.CurrentTarget.HasAura("Marked for Death")
                    )
                ),
                Spell.Cast("Aimed Shot", ret => StyxWoW.Me.HasAura("Fire!")),
                Spell.Cast("Steady Shot", ret => !StyxWoW.Me.HasAura("Improved Steady Shot") && !StyxWoW.Me.IsMoving),
                Spell.Cast("Kill Shot", ret => StyxWoW.Me.CurrentTarget.CurrentHealth < 20),
                Spell.Cast("Rapid Fire", ret => StyxWoW.Me.IsInInstance && !StyxWoW.Me.HasAura("Rapid Fire")),
                Spell.Cast("Readiness", ret => StyxWoW.Me.IsInInstance && Spell.GetSpellCooldown("Rapid Fire").TotalSeconds > 60),
                // Focus Dump
                new Decorator(ret => StyxWoW.Me.PowerPercent >= 65,
                    new PrioritySelector(
                        Spell.Cast("Aimed Shot", ret => StyxWoW.Me.CurrentTarget.CurrentHealth > 90 && !StyxWoW.Me.IsMoving),
                        Spell.Cast("Arcane Shot")
                    )
                ),
                Spell.Cast("Kill Command", ret => StyxWoW.Me.CurrentTarget.HasAura("Resistance is Futile")),
                Spell.Cast("Steady Shot", ret => !StyxWoW.Me.IsMoving),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.MarksmanshipHunter)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateMarksmanshipPull()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.Buff("Hunter's Mark"),
                new Decorator(
                    ret => !SpellManager.CanCast("Aimed Shot"),
                    Helpers.Common.CreateAutoAttack(true)),
                Spell.Cast("Aimed Shot"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }
    }
}
