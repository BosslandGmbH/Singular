using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public class Feral
    {
        private static DruidSettings Settings
        {
            get { return SingularSettings.Instance.Druid; }
        }

        #region Common

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
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
                Spell.Buff("Prowl"),
                Spell.Cast("Pounce"),
                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalPreCombatBuffs()
        {
            return new PrioritySelector();
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalCombatBuffs()
        {
            return new PrioritySelector();
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
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
                /*  new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 4,
                    new PrioritySelector(
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Shield Slam"),
                        Spell.Cast("Revenge"),
                        Spell.Cast("Devastate"),
                        Movement.CreateMoveToMeleeBehavior(true)
                        )),*/
                //Single target
                Spell.Cast("Savage Roar",
                           ret =>
                           (StyxWoW.Me.ComboPoints == 5 &&
                            StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds < 3)),
                Spell.Cast("Ferocious Bite",
                           ret =>
                           (StyxWoW.Me.CurrentTarget.HealthPercent <= 25 &&
                            (StyxWoW.Me.ComboPoints == 5 ||
                             (StyxWoW.Me.GetAuraTimeLeft("Rip", true).TotalSeconds < 3) &&
                             StyxWoW.Me.GetAuraTimeLeft("Rip", true).TotalSeconds > 0))),
                Spell.Cast("Rip",
                           ret =>
                           (StyxWoW.Me.ComboPoints == 5 &&
                            StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 3)),
                Spell.Cast("Shred", ret => StyxWoW.Me.CurrentTarget.MeIsSafelyBehind),
                Spell.Cast("Mangle", ret => !StyxWoW.Me.CurrentTarget.MeIsSafelyBehind),
                Spell.Cast("Rake", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 3),


            Movement.CreateMoveToMeleeBehavior(true)
            )
            ;
        }
    }
}