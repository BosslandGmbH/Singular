using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Rogue
{
    public static class Common
    {
        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.CombatRogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Spec(TalentSpec.SubtletyRogue)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateRoguePreCombatBuffs()
        {
            return new PrioritySelector(
                CreateApplyPoisons(),
                Spell.BuffSelf("Recuperate", ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.HealthPercent < 80)
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.CombatRogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Spec(TalentSpec.SubtletyRogue)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateRogueCombatBuffs()
        {
            return new PrioritySelector(
                Item.CreateUsePotionAndHealthstone(30, 0),
                Spell.BuffSelf("Vanish", ret => StyxWoW.Me.HealthPercent < 20 && Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro) > 0)
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.CombatRogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Spec(TalentSpec.SubtletyRogue)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public static Composite CreateRogueRest()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Stealth", ret => StyxWoW.Me.HasAura("Food")),
                Rest.CreateDefaultRestBehaviour()
                );
        }

        public static Composite CreateApplyPoisons()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Poisons.MainHandNeedsPoison && Poisons.MainHandPoison != null,
                    new Sequence(
                        new Action(ret => Logger.Write(string.Format("Applying {0} to main hand", Poisons.MainHandPoison.Name))),
                        new Action(ret => Navigator.PlayerMover.MoveStop()),
                        new Action(ret => StyxWoW.SleepForLagDuration()),
                        new Action(ret => Poisons.MainHandPoison.UseContainerItem()),
                        new Action(ret => StyxWoW.SleepForLagDuration()),
                        new Action(ret => Lua.DoString("UseInventoryItem(16)")),
                        new Action(ret => StyxWoW.SleepForLagDuration()),
                        new WaitContinue(10, ret => !StyxWoW.Me.IsCasting, new ActionAlwaysSucceed()),
                        new Action(ret => Thread.Sleep(1000)))),
                new Decorator(
                    ret => Poisons.OffHandNeedsPoison && Poisons.OffHandPoison != null,
                    new Sequence(
                        new Action(ret => Logger.Write(string.Format("Applying {0} to off hand", Poisons.OffHandPoison.Name))),
                        new Action(ret => Navigator.PlayerMover.MoveStop()),
                        new Action(ret => StyxWoW.SleepForLagDuration()),
                        new Action(ret => Poisons.OffHandPoison.UseContainerItem()),
                        new Action(ret => StyxWoW.SleepForLagDuration()),
                        new Action(ret => Lua.DoString("UseInventoryItem(17)")),
                        new Action(ret => StyxWoW.SleepForLagDuration()),
                        new WaitContinue(10, ret => !StyxWoW.Me.IsCasting, new ActionAlwaysSucceed()),
                        new Action(ret => Thread.Sleep(1000))))
                );
        }

        public static Composite CreateRogueBlindOnAddBehavior()
        {
            return new PrioritySelector(
                    ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u =>
                            u.IsTargetingMeOrPet && u != StyxWoW.Me.CurrentTarget),
                    new Decorator(
                        ret => ret != null,
                        Spell.Buff("Blind", ret => (WoWUnit)ret, ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro) > 1)));
        }
    }
}
