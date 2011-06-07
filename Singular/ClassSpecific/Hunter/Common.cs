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
            PetManager.WantedPet = "1";
            return new PrioritySelector(
                new Decorator(
                    ctx => StyxWoW.Me.CastingSpell != null && StyxWoW.Me.CastingSpell.Name == "Revive " + PetManager.WantedPet && StyxWoW.Me.GotAlivePet,
                    new Action(ctx => SpellManager.StopCasting())),
                Spell.WaitForCast(true),
                Spell.BuffSelf("Aspect of the Hawk"),
                Spell.BuffSelf("Track Hidden"),
                //new ActionLogMessage(false, "Checking for pet"),
                new Decorator(
                    ret => !StyxWoW.Me.GotAlivePet,
                    new Sequence(
                        new Action(ret => PetManager.CallPet(PetManager.WantedPet)),
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

        public static Composite CreateHunterBackPedal()
        {
            return
                new Decorator(
                    ret => !SingularSettings.Instance.DisableAllMovement && StyxWoW.Me.CurrentTarget.Distance + StyxWoW.Me.CurrentTarget.CombatReach <= 8 && StyxWoW.Me.CurrentTarget.IsAlive &&
                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null || StyxWoW.Me.CurrentTarget.CurrentTarget != StyxWoW.Me),
                    new Action(
                        ret =>
                        {
                            WoWPoint moveTo = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, StyxWoW.Me.CurrentTarget.Location, StyxWoW.Me.CurrentTarget.CombatReach + 10f);

                            if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                            {
                                Navigator.MoveTo(moveTo);
                            }
                        }));
        }

        public static Composite CreateHunterTrapOnAddBehavior()
        {
            return new PrioritySelector(
                    ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u =>
                            u.IsTargetingMeOrPet && u != StyxWoW.Me.CurrentTarget && !u.IsMoving),
                    new Decorator(
                        ret => ret != null && SpellManager.CanCast("Freezing Trap", (WoWUnit)ret, false),
                        new PrioritySelector(
                            Spell.BuffSelf("Trap Launcher"),
                            new Sequence(
                                new Action(ret => Lua.DoString("RunMacroText(\"/cast Freezing Trap\")")),
                                new Action(ret => LegacySpellManager.ClickRemoteLocation(((WoWUnit)ret).Location))))));
        }
    }
}
