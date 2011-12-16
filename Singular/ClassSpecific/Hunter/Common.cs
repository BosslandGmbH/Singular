using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Hunter
{
    public class Common
    {
        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.BeastMasteryHunter)]
        [Spec(TalentSpec.SurvivalHunter)]
        [Spec(TalentSpec.MarksmanshipHunter)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateHunterBuffs()
        {
            return new PrioritySelector(
                new Decorator(
                    ctx => StyxWoW.Me.CastingSpell != null && StyxWoW.Me.CastingSpell.Name == "Revive " + SingularSettings.Instance.Hunter.PetSlot && StyxWoW.Me.GotAlivePet,
                    new Action(ctx => SpellManager.StopCasting())),
                Spell.WaitForCast(true),
                Spell.BuffSelf("Aspect of the Hawk"),
                Spell.BuffSelf("Track Hidden"),
                //new ActionLogMessage(false, "Checking for pet"),
                new Decorator(
                    ret => !StyxWoW.Me.GotAlivePet,
                    new Sequence(
                        new Action(ret => PetManager.CallPet(SingularSettings.Instance.Hunter.PetSlot)),
                        new Action(ret => Thread.Sleep(1000)),
                        new DecoratorContinue(
                            ret => !StyxWoW.Me.GotAlivePet && SpellManager.CanCast("Revive Pet"),
                            new Sequence(
                                new Action(ret => SpellManager.Cast("Revive Pet")),
                                new Action(ret => StyxWoW.SleepForLagDuration()),
                                new WaitContinue(
                                    11,
                                    ret => !StyxWoW.Me.IsCasting,
                                    new ActionAlwaysSucceed()))))),
                Spell.Cast(
                    "Mend Pet",
                    ret =>
                    (StyxWoW.Me.Pet.HealthPercent < 70 || (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet"))) && !StyxWoW.Me.Pet.HasAura("Mend Pet"))
                );
        }

        /// <summary>
        /// hawker december 16 2011
        /// If we have a valid target, we move about 20 yards from it and kill it.
        /// </summary>
        public static Composite CreateHunterMoveToPullPoint()
        {
            return
                new PrioritySelector(
                    new Decorator(ret => !StyxWoW.Me.Combat && StyxWoW.Me.CurrentTarget.IsAlive &&
                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null || StyxWoW.Me.CurrentTarget.CurrentTarget != StyxWoW.Me) &&
                            StyxWoW.Me.CurrentTarget.Distance < 30f && StyxWoW.Me.CurrentTarget.InLineOfSight,
            new Action(ret => Helpers.Common.CreateAutoAttack(true))),
                new Decorator(
                    ret => !StyxWoW.Me.Combat && !SingularSettings.Instance.DisableAllMovement && StyxWoW.Me.CurrentTarget.IsAlive &&
                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null || StyxWoW.Me.CurrentTarget.CurrentTarget != StyxWoW.Me),
                                                      new Sequence(
                               new Action(ret => Logging.Write("Moving to pull.")),
                    new Action(
                        ret =>
                        {
                            var moveTo = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, StyxWoW.Me.CurrentTarget.Location, Spell.SafeMeleeRange + 15f);

                            if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                            {
                                return Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(moveTo));
                            }

                            return RunStatus.Failure;
                        }))));
        }

        public static Composite CreateHunterBackPedal()
        {
            return
                new Decorator(
                    ret => !SingularSettings.Instance.DisableAllMovement && StyxWoW.Me.CurrentTarget.Distance <= Spell.SafeMeleeRange + 3f && StyxWoW.Me.CurrentTarget.IsAlive &&
                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null || StyxWoW.Me.CurrentTarget.CurrentTarget != StyxWoW.Me),
                           new Sequence(
                               new Action(ret => Logging.Write("Moving out of melee distance.")),
                    new Action(
                        ret =>
                        {
                            var moveTo = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, StyxWoW.Me.CurrentTarget.Location, Spell.SafeMeleeRange + 10f);

                            if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                            {
                                return Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(moveTo));
                            }

                            return RunStatus.Failure;
                        })));
        }

        public static Composite CreateHunterTrapBehavior(string trapName)
        {
            return new PrioritySelector(
                new Decorator(
                    ctx => ctx != null && SpellManager.CanCast(trapName, (WoWUnit)ctx, false),
                    new PrioritySelector(
                        Spell.BuffSelf("Trap Launcher"),
                        new Sequence(
                            new Switch<string>(ctx => trapName,
                                new SwitchArgument<string>("Immolation Trap",
                                    new Action(ret => LegacySpellManager.CastSpellById(82945))),
                                new SwitchArgument<string>("Freezing Trap",
                                    new Action(ret => LegacySpellManager.CastSpellById(60192))),
                                new SwitchArgument<string>("Explosive Trap",
                                    new Action(ret => LegacySpellManager.CastSpellById(82939))),
                                new SwitchArgument<string>("Ice Trap",
                                    new Action(ret => LegacySpellManager.CastSpellById(82941))),
                                new SwitchArgument<string>("Snake Trap",
                                    new Action(ret => LegacySpellManager.CastSpellById(82948)))
                                ),
                            new Action(ret => LegacySpellManager.ClickRemoteLocation(((WoWUnit)ret).Location))))));
        }

        public static Composite CreateHunterTrapOnAddBehavior(string trapName)
        {
            return new PrioritySelector(
                ctx => Unit.NearbyUnfriendlyUnits.OrderBy(u => u.DistanceSqr).
                                                  FirstOrDefault(
                                                        u => u.IsTargetingMeOrPet && u != StyxWoW.Me.CurrentTarget &&
                                                             !u.IsMoving),
                new Decorator(
                    ctx => ctx != null && SpellManager.CanCast(trapName, (WoWUnit)ctx, false),
                    new PrioritySelector(
                        Spell.BuffSelf("Trap Launcher"),
                        new Sequence(
                            new Switch<string>(ctx => trapName,
                                new SwitchArgument<string>("Immolation Trap",
                                    new Action(ret => LegacySpellManager.CastSpellById(82945))),
                                new SwitchArgument<string>("Freezing Trap",
                                    new Action(ret => LegacySpellManager.CastSpellById(60192))),
                                new SwitchArgument<string>("Explosive Trap",
                                    new Action(ret => LegacySpellManager.CastSpellById(82939))),
                                new SwitchArgument<string>("Ice Trap",
                                    new Action(ret => LegacySpellManager.CastSpellById(82941))),
                                new SwitchArgument<string>("Snake Trap",
                                    new Action(ret => LegacySpellManager.CastSpellById(82948)))
                                ),
                            new Action(ret => LegacySpellManager.ClickRemoteLocation(((WoWUnit)ret).Location))))));
        }
    }
}
