
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.TreeSharp;

namespace Singular.ClassSpecific.Warlock
{
    public class Lowbie
    {


        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Warlock, 0, priority: 1)]
        public static Composite CreateLowbieWarlockCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.PreventDoubleCast("Immolate"),
                Spell.WaitForCast(true),
                Spell.Cast("Life Tap", ret => StyxWoW.Me.ManaPercent < 50 && StyxWoW.Me.HealthPercent > 70),
                Spell.Cast("Drain Life", ret => StyxWoW.Me.HealthPercent < 70),
                Spell.Buff("Immolate"),
                Spell.Buff("Corruption"),
                Spell.Cast("Shadow Bolt"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }
    }
}