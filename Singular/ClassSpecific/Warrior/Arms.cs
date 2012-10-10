using System.Runtime.Remoting.Contexts;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;

using Styx.Helpers;
using System;
using Styx.WoWInternals;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Warrior
{
    public class Arms
    {

        #region Common

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.All)]
        public static Composite CreateArmsNormalPull()
        {
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                //face target
                Movement.CreateFaceTargetBehavior(),
                // LOS check
                Movement.CreateMoveToLosBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                //Dismount
                new Decorator(ret => StyxWoW.Me.Mounted,
                              Helpers.Common.CreateDismount("Pulling")),
                //Shoot flying targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Spell.Cast("Heroic Throw"),
                        Spell.Cast("Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 27f)
                        )),

                //Buff up
                Spell.BuffSelf("Battle Shout",
                               ret =>
                               (SingularSettings.Instance.Warrior.UseWarriorShouts) && 
                               !StyxWoW.Me.HasPartyBuff( PartyBuffType.AttackPower)),
                Spell.BuffSelf("Commanding Shout",
                               ret =>
                               StyxWoW.Me.RagePercent < 20 &&
                               SingularSettings.Instance.Warrior.UseWarriorShouts == false),

                //Charge
                Spell.Cast(
                    "Charge",
                    ret =>
                    StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance < 25 &&
                    SingularSettings.Instance.Warrior.UseWarriorCloser &&
                    Common.PreventDoubleCharge),
                //Heroic Leap
                Spell.CastOnGround(
                    "Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location,
                    ret =>
                    StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun", 1) &&
                    SingularSettings.Instance.Warrior.UseWarriorCloser &&
                    Common.PreventDoubleCharge),
                Spell.Cast(
                    "Heroic Throw",
                    ret =>
                    !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Charge Stun")),

                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Normal

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.All)]
        public static Composite CreateArmsNormalPreCombatBuffs()
        {
            return new PrioritySelector();
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.All)]
        public static Composite CreateArmsNormalCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Battle Stance"),
                Spell.BuffSelf("Recklessness"),
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HasAura("Recklessness") || StyxWoW.Me.CurrentTarget.HasAura("Colossus Smash")),
                Spell.Cast("Deadly Calm", ret=> StyxWoW.Me.HasAura("Taste for Blood"))
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.All)]
        public static Composite CreateArmsNormalCombat()
        {
            return new PrioritySelector(
                //Ensure Target
                Safers.EnsureTarget(),
                //LOS check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),
               

                Spell.Cast("Mortal Strike"),
                Spell.Cast("Colossus Smash", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Colossus Smash")),
                Spell.Cast("Execute"),
                Spell.Cast("Overpower"),
                Spell.Cast("Heroic Strike", ret=> StyxWoW.Me.HasAura("Taste for Blood")),
                Spell.Cast("Slam"),

               

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
    }
}