using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.TreeSharp;
using System.Collections.Generic;
using Styx.CommonBot;

namespace Singular.ClassSpecific.Monk
{
    public class Windwalker
    {

        [Behavior(BehaviorType.Combat | BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkWindwalker)]
 
        public static Composite CreateWindwalkerMonkCombat()
        {
            return new PrioritySelector(
               Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.Cast("Tiger Palm", ret => !SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1),
                //So when we get our kick spell we dont waste chi on stacking a buff more then we have too.
                //The OR part is so if we dont have the buff we can cast Tiger Palm and apply it, then the Or will make sure once we have it, it wont go over 3 stacks since that will be a waste of chi.
                Spell.Cast("Tiger Palm", ret => SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1 && (!StyxWoW.Me.HasAura("Tiger Power") || StyxWoW.Me.HasAura("Tiger Power") && StyxWoW.Me.Auras["Tiger Power"].StackCount < 3)),
                Spell.Cast("Blackout Kick", ret => StyxWoW.Me.CurrentChi >= 2),
                Spell.Cast("Jab"),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll", ret => StyxWoW.Me.CurrentTarget.Distance >= 5 && StyxWoW.Me.CurrentTarget.Distance <= 20),
                 Movement.CreateMoveToMeleeBehavior(true)
                );
        }



    }

}