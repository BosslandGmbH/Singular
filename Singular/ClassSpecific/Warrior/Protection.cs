using System.Linq;
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
    public class Protection
    {

        #region Common
        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalPull()
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
                               !StyxWoW.Me.HasPartyBuff(PartyBuffType.AttackPower)),
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

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalPreCombatBuffs()
        {
            return new PrioritySelector();
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Defensive Stance"),
                Spell.Cast("Demoralizing Shout"),
                Spell.BuffSelf("Shield Wall", ret => StyxWoW.Me.CurrentHealth < SingularSettings.Instance.Warrior.WarriorShieldWallHealth),
                Spell.BuffSelf("Shield Block", ret => StyxWoW.Me.CurrentHealth < SingularSettings.Instance.Warrior.WarriorShieldBlockHealth),
                Spell.BuffSelf("Last Stand", ret => StyxWoW.Me.CurrentHealth < SingularSettings.Instance.Warrior.WarriorLastStandHealth),
                Spell.BuffSelf("Enraged Regeneration", ret => StyxWoW.Me.CurrentHealth < SingularSettings.Instance.Warrior.WarriorEnragedRegenerationHealth)

                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalCombat()
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
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 4,
                    new PrioritySelector(
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Shield Slam"),
                        Spell.Cast("Revenge"),
                        Spell.Cast("Devastate"),
                        Movement.CreateMoveToMeleeBehavior(true)
                        )),
                //Single target
                Spell.Cast("Shield Slam"),
                Spell.Cast("Revenge"),
                Spell.Cast("Devastate"),
                Spell.Cast("Thunder Clap", ret => !StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Weakened Blows")),
                Spell.Cast("Heroic Strike", ret => (StyxWoW.Me.CurrentRage >= RageDump(80))),




                Movement.CreateMoveToMeleeBehavior(true)
                );
        }


        static int RageDump(int baserage)
        {
            if (TalentManager.HasGlyph("Unending Rage"))
            {
                baserage = baserage - 20;
            }

            return baserage;
        }
        #endregion
    }
}