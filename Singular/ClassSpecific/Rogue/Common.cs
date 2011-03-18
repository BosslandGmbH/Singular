
using Styx.Combat.CombatRoutine;

using TreeSharp;
using Styx;
using Styx.Logic.Pathing;

using Styx.WoWInternals;
using CommonBehaviors.Actions;
using Singular.Composites;
using System.Threading;
using System.Linq;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.CombatRogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Spec(TalentSpec.SubtletyRogue)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateRoguePreCombatBuffs()
        {
            return new PrioritySelector(
                CreateApplyPoisons(),
                CreateSpellBuffOnSelf("Recuperate", ret => Me.RawComboPoints > 0 && Me.HealthPercent < 70)
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.CombatRogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Spec(TalentSpec.SubtletyRogue)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateRogueCombatBuffs()
        {
            return new PrioritySelector(
                CreateUsePotionAndHealthstone(30, 0),
                CreateSpellBuffOnSelf("Vanish", ret => Me.HealthPercent < 10 && NearbyUnfriendlyUnits.Count(u => u.Aggro) > 0)
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.CombatRogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Spec(TalentSpec.SubtletyRogue)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public Composite CreateRogueRest()
        {
            return new PrioritySelector(
                CreateSpellBuffOnSelf("Stealth", ret => Me.HasAura("Food")),
                CreateDefaultRestComposite(60,0)
                );
        }

        protected Composite CreateApplyPoisons()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Poisons.MainHandNeedsPoison && Poisons.MainHandPoison != null,
                    new Sequence(
                        new ActionLogMessage(false, string.Format("Applying {0} to main hand", Poisons.MainHandPoison.Name)),
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
                        new ActionLogMessage(false, string.Format("Applying {0} to off hand", Poisons.OffHandPoison.Name)),
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
    }
}