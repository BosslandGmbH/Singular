using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    class Guardian
    {


        private static DruidSettings Settings
        {
            get { return SingularSettings.Instance.Druid; }
        }

        #region Common

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateFeralNormalPull()
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
                        Spell.Cast("Moonfire"),
                        Movement.CreateMoveToTargetBehavior(true, 27f)
                        )),
                Spell.Buff("Bear Form"),
                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateFeralNormalPreCombatBuffs()
        {
            return new PrioritySelector();
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateFeralNormalCombatBuffs()
        {
            return new PrioritySelector(Spell.Buff("Bear Form"));
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateFeralNormalCombat()
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
                  new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 2,
                    new PrioritySelector(
                        Spell.Cast("Thrash"),
                        Spell.Cast("Mangle"),
                        Spell.Cast("Swipe"),
                        Movement.CreateMoveToMeleeBehavior(true),
                        new ActionAlwaysSucceed() // so we dont go down the rest of the tree?
                        )),

                Spell.Cast("Mangle"),
                Spell.Cast("Thrash", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Thrash", true).TotalSeconds < 3),
                Spell.Buff("Faerie Fire"),
                Spell.Cast("Lacerate"),



            Movement.CreateMoveToMeleeBehavior(true)
            )
            ;
        }
    }
}