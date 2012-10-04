using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using System.Collections.Generic;
using Styx.CommonBot;

namespace Singular.ClassSpecific.Monk
{

    public class Brewmaster
    {

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        public static Composite CreateBrewmasterMonkPull()
        {
            return new PrioritySelector(
               Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.CastOnGround("Dizzying Haze", ctx => StyxWoW.Me.CurrentTarget.Location, ctx => Unit.UnfriendlyUnitsNearTarget(8).Count() > 1, false),
                Spell.Cast("Clash"),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll", ret => StyxWoW.Me.CurrentTarget.Distance.Between(10, 40)),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Battlegrounds)]
        public static Composite CreateBrewmasterMonkPvpCombat()
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
                Spell.Cast("Clash"),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll", ret => StyxWoW.Me.CurrentTarget.Distance.Between(10, 40)),
                 Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #region Normal and Instance
        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Instances | WoWContext.Normal)]
        public static Composite CreateBrewmasterMonkInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // make sure I have aggro.
                Spell.Cast("Provoke", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(), ret => SingularSettings.Instance.EnableTaunting),
                Spell.Cast("Keg Smash", ctx => StyxWoW.Me.CurrentChi < 4 && Unit.NearbyUnitsInCombatWithMe.Any(u => u.DistanceSqr <= 8*8)),
                Spell.CastOnGround("Dizzying Haze", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault().Location, ctx => TankManager.Instance.NeedToTaunt.Any(), false),
                
                Spell.Cast("Tiger Palm", ret => !SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1),

                //So when we get our kick spell we dont waste chi on stacking a buff more then we have too.
                //The OR part is so if we dont have the buff we can cast Tiger Palm and apply it, then the Or will make sure once we have it, it wont go over 3 stacks since that will be a waste of chi.
                Spell.Cast(
                    "Tiger Palm",
                    ret =>
                    SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1 &&
                    (!StyxWoW.Me.HasAura("Tiger Power") || StyxWoW.Me.HasAura("Tiger Power") && StyxWoW.Me.Auras["Tiger Power"].StackCount < 3)),
                Spell.Cast("Blackout Kick", ret => StyxWoW.Me.CurrentChi >= 2),
                Spell.Cast("Jab"),

                Spell.Cast("Clash"),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll", ret => StyxWoW.Me.CurrentTarget.Distance.Between(10, 40)),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        #endregion




    }

}