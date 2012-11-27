using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

namespace Singular.ClassSpecific.Druid
{
    class Guardian
    {
        private static DruidSettings Settings
        {
            get { return SingularSettings.Instance.Druid; }
        }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        #region Common

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalPull()
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
                new Decorator(ret => Me.Mounted,
                              Helpers.Common.CreateDismount("Pulling")),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                    //Shoot flying targets
                    new Decorator(
                        ret => Me.CurrentTarget.IsFlying,
                        new PrioritySelector(
                            Spell.Cast("Moonfire"),
                            Movement.CreateMoveToTargetBehavior(true, 27f)
                            )),

                        Spell.BuffSelf("Bear Form"),
                        Spell.Cast("Faerie Fire")
                        )
                    ),

                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalPreCombatBuffs()
        {
            return new PrioritySelector();
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All, 1)]
        public static Composite CreateGuardianNormalCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Bear Form"),
                
                Spell.BuffSelf("Savage Defense", ret => Me.HealthPercent < SingularSettings.Instance.Druid.TankSavageDefense && Me.GetAuraTimeLeft("Savage Defense", true).TotalSeconds < 1),
                Spell.BuffSelf("Frenzied Regeneration", ret => Me.HealthPercent < SingularSettings.Instance.Druid.TankFrenziedRegenerationHealth && Me.CurrentRage >=60),
                Spell.BuffSelf("Frenzied Regeneration", ret => Me.HealthPercent < 30 && Me.CurrentRage >= 15),
                Spell.BuffSelf("Might of Ursoc", ret => Me.HealthPercent < SingularSettings.Instance.Druid.TankMightOfUrsoc),
                Spell.BuffSelf("Survival Instincts", ret => Me.HealthPercent < SingularSettings.Instance.Druid.TankSurvivalInstinctsHealth),
                Spell.BuffSelf("Barkskin", ret => Me.HealthPercent < SingularSettings.Instance.Druid.TankFeralBarkskin),
                Spell.Cast("Renewal", ret => Me.HealthPercent < SingularSettings.Instance.Druid.RenewalHealth)
                
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalCombat()
        {
           // Logger.Write("guardian loop.");
            return new PrioritySelector(
                //Ensure Target
                Safers.EnsureTarget(),
                //LOS check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptSpellCast(ret => Me.CurrentTarget),

                        // keep weakened blows up on targets if not present. Skip if Berserking so GCD's saved for Mangle
                        Spell.Cast("Thrash", ret => !Me.HasAura("Berserk") && Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < 8 && u.GetAuraTimeLeft("Thrash", true).TotalSeconds < 3)),

                        new Decorator(
                            ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 8) >= 2,
                            new PrioritySelector(
                                Spell.Cast("Berserk"),
                                Spell.Cast("Mangle"),
                                Spell.Cast("Thrash"),
                                Spell.Cast("Swipe"),
                                Spell.Cast("Lacerate"),
                                Spell.Cast("Faerie Fire"),
                                Movement.CreateMoveToMeleeBehavior(true),
                                new ActionAlwaysSucceed() // so we dont go down the rest of the tree?
                                )
                            ),

                        Spell.Cast("Mangle"),
                        Spell.Buff("Faerie Fire"),
                        Spell.Cast("Lacerate"),
                        Spell.Cast("Maul", ret=>Me.CurrentRage >= 90),
                        Spell.Cast("Swipe")
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
            )
            ;
        }
    }
}