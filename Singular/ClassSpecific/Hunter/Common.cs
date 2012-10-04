using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Hunter
{
    public class Common
    {
        static Common()
        {
            // Lets hook this event so we can disable growl
            SingularRoutine.OnWoWContextChanged += SingularRoutine_OnWoWContextChanged;
        }

        // Disable pet growl in instances but enable it outside.
        static void SingularRoutine_OnWoWContextChanged(object sender, WoWContextEventArg e)
        {
            Lua.DoString(e.CurrentContext == WoWContext.Instances
                             ? "DisableSpellAutocast(GetSpellInfo(2649))"
                             : "EnableSpellAutocast(GetSpellInfo(2649))");
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Hunter)]
        public static Composite CreateHunterBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                Spell.BuffSelf("Aspect of the Hawk", ret => !StyxWoW.Me.HasAura("Aspect of the Iron Hawk") && !StyxWoW.Me.HasAura("Aspect of the Hawk")),
                Spell.BuffSelf("Track Hidden"),
                new Decorator(ctx => SingularSettings.Instance.DisablePetUsage && StyxWoW.Me.GotAlivePet,
                    new Action(ctx => SpellManager.Cast("Dismiss Pet"))),

                new Decorator(ctx => !SingularSettings.Instance.DisablePetUsage,
                    new PrioritySelector(
                        CreateHunterCallPetBehavior(true),
                        Spell.Cast("Mend Pet", ret => (StyxWoW.Me.Pet.HealthPercent < 70 || (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet"))) && !StyxWoW.Me.Pet.HasAura("Mend Pet"))
                        )
                    )
                );
        }

        public static Composite CreateHunterBackPedal()
        {
            return
                new Decorator(
                    ret => !SingularSettings.Instance.DisableAllMovement && StyxWoW.Me.CurrentTarget.Distance <= Spell.MeleeRange + 5f &&
                           StyxWoW.Me.CurrentTarget.IsAlive &&
                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null ||
                            StyxWoW.Me.CurrentTarget.CurrentTarget != StyxWoW.Me ||
                            StyxWoW.Me.CurrentTarget.IsStunned()),
                    new Action(
                        ret =>
                        {
                            var moveTo = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, StyxWoW.Me.CurrentTarget.Location, Spell.MeleeRange + 10f);

                            if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                            {
                                Navigator.MoveTo(moveTo);
                                return RunStatus.Success;
                            }

                            return RunStatus.Failure;
                        }));
        }

        public static Composite CreateHunterTrapBehavior(string trapName)
        {
            return CreateHunterTrapBehavior(trapName, ret => StyxWoW.Me.CurrentTarget);
        }

        public static Composite CreateHunterTrapBehavior(string trapName, bool useLauncher)
        {
            return CreateHunterTrapBehavior(trapName, useLauncher, ret => StyxWoW.Me.CurrentTarget);
        }

        public static Composite CreateHunterTrapBehavior(string trapName, UnitSelectionDelegate onUnit)
        {
            return CreateHunterTrapBehavior(trapName, true, onUnit);
        }

        public static Composite CreateHunterTrapBehavior(string trapName, bool useLauncher, UnitSelectionDelegate onUnit)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => onUnit != null && onUnit(ret) != null && onUnit(ret).DistanceSqr < 40 * 40 &&
                           SpellManager.HasSpell(trapName) && !SpellManager.Spells[trapName].Cooldown,
                    new PrioritySelector(
                        Spell.BuffSelf(trapName, ret => !useLauncher),
                        Spell.BuffSelf("Trap Launcher", ret => useLauncher),
                        new Decorator(
                            ret => StyxWoW.Me.HasAura("Trap Launcher"),
                            new Sequence(
                /*new Switch<string>(ctx => trapName,
                    new SwitchArgument<string>("Freezing Trap",
                        new Action(ret => SpellManager.CastSpellById(1499))),
                    new SwitchArgument<string>("Explosive Trap",
                        new Action(ret => SpellManager.CastSpellById(13813))),
                    new SwitchArgument<string>("Ice Trap",
                        new Action(ret => SpellManager.CastSpellById(13809))),
                    new SwitchArgument<string>("Snake Trap",
                        new Action(ret => SpellManager.CastSpellById(34600)))
                    ),*/

                                new Action(ret => Lua.DoString(string.Format("CastSpellByName(\"{0}\")", trapName))),
                                new WaitContinue(TimeSpan.FromMilliseconds(200), ret => false, new ActionAlwaysSucceed()),
                                new Action(ret => SpellManager.ClickRemoteLocation(onUnit(ret).Location)))))));
        }

        public static Composite CreateHunterTrapOnAddBehavior(string trapName)
        {
            return new PrioritySelector(
                ctx => Unit.NearbyUnfriendlyUnits.OrderBy(u => u.DistanceSqr).
                                                  FirstOrDefault(
                                                        u => u.Combat && u != StyxWoW.Me.CurrentTarget &&
                                                             (!u.IsMoving || u.IsPlayer) && u.DistanceSqr < 40 * 40),
                new Decorator(
                    ret => ret != null && SpellManager.HasSpell(trapName) && !SpellManager.Spells[trapName].Cooldown,
                    new PrioritySelector(
                        Spell.BuffSelf("Trap Launcher"),
                        new Decorator(
                            ret => StyxWoW.Me.HasAura("Trap Launcher"),
                            new Sequence(
                                new Action(ret => Lua.DoString(string.Format("CastSpellByName(\"{0}\")", trapName))),
                                new WaitContinue(TimeSpan.FromMilliseconds(200), ret => false, new ActionAlwaysSucceed()),
                                new Action(ret => SpellManager.ClickRemoteLocation(((WoWUnit)ret).Location)))))));
        }

        public static Composite CreateHunterCallPetBehavior(bool reviveInCombat)
        {
            return new Decorator(
                ret =>  !SingularSettings.Instance.DisablePetUsage && !StyxWoW.Me.GotAlivePet && PetManager.PetTimer.IsFinished,
                new PrioritySelector(
                    Spell.WaitForCast(),
                    new Decorator(
                        ret => StyxWoW.Me.Pet != null && (!StyxWoW.Me.Combat || reviveInCombat),
                        new PrioritySelector(
                            Movement.CreateEnsureMovementStoppedBehavior(),
                            Spell.BuffSelf("Revive Pet"))),
                    new Sequence(
                        new Action(ret => PetManager.CallPet(SingularSettings.Instance.Hunter.PetSlot)),
                        Helpers.Common.CreateWaitForLagDuration(),
                        new WaitContinue(2, ret => StyxWoW.Me.GotAlivePet || StyxWoW.Me.Combat, new ActionAlwaysSucceed()),
                        new Decorator(
                            ret => !StyxWoW.Me.GotAlivePet && (!StyxWoW.Me.Combat || reviveInCombat),
                            Spell.BuffSelf("Revive Pet")))
                    )
                );
        }
    }
}
