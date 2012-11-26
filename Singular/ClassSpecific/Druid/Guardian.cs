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
                new Decorator(ret => Me.Mounted, Helpers.Common.CreateDismount("Pulling")),

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

                // Enrage = 20 rage when popped. + 10 over time.
                // We use it as a "get to 60 asap" cooldown. That's basically it.
                Spell.BuffSelf("Enrage", ret=>StyxWoW.Me.CurrentRage <= 40),

                // Ursoc first. Gives us a total HP% boost.
                Spell.BuffSelf("Might of Ursoc", ret => Me.HealthPercent < SingularSettings.Instance.Druid.TankMightOfUrsoc),
                // Renewal after, for more health gain if we're still low with ursoc up.
                Spell.Cast("Renewal", ret => Me.HealthPercent < SingularSettings.Instance.Druid.RenewalHealth),

                Spell.BuffSelf("Survival Instincts",
                    ret => Me.HealthPercent < SingularSettings.Instance.Druid.TankSurvivalInstinctsHealth),

                Spell.BuffSelf("Barkskin", ret => Me.HealthPercent < SingularSettings.Instance.Druid.TankFeralBarkskin),

                // 2 cases for FR.
                // 1) We have > 60 rage, and below the FR setting. (70 by default)
                // 2) We're really low, and have 15+ rage. Enough to get *some* heal out of it.
                Spell.BuffSelf("Frenzied Regeneration",
                    ret =>
                    Me.HealthPercent < SingularSettings.Instance.Druid.TankFrenziedRegenerationHealth &&
                    Me.CurrentRage >= 60),

                Spell.BuffSelf("Frenzied Regeneration", ret => Me.HealthPercent < 30 && Me.CurrentRage >= 15),

                // SD basically on cooldown. Let it drop off so we can pool more rage.
                Spell.BuffSelf("Savage Defense", ret => Me.HealthPercent < SingularSettings.Instance.Druid.TankSavageDefense)
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

                new PrioritySelector(

                    Helpers.Common.CreateInterruptSpellCast(ret => Me.CurrentTarget),

                    Spell.Cast("Mangle"),
                    Spell.Cast("Thrash"),

                    Aoe(),

                    Spell.Cast("Lacerate"),
                    Spell.Buff("Faerie Fire")
                    ),


                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        static Composite Aoe()
        {
            return new Decorator(ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= 2,
                new PrioritySelector(
                    Spell.Cast("Berserk"),
                    Spell.Cast("Swipe")));
        }
    }
}