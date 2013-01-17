using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.TreeSharp;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Drawing;

namespace Singular.ClassSpecific.Shaman
{
    class Lowbie
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman(); } }

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, 0)]
        public static Composite CreateShamanElementalRest()
        {
            return Rest.CreateDefaultRestBehaviour("Healing Surge", "Ancestral Spirit");
        }

        [Behavior(BehaviorType.PreCombatBuffs | BehaviorType.CombatBuffs, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbiePreCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.BuffSelf("Lightning Shield")
                    )
                );
        }
        [Behavior(BehaviorType.Pull, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbiePull()
        {
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.WaitForCast(true),
                    CreateLowbieDiagnosticOutputBehavior(),
                    Spell.Cast("Lightning Bolt"),
                    Movement.CreateMoveToTargetBehavior(true, 25f));
        }
        [Behavior(BehaviorType.Heal, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbieHeal()
        {
            return new Decorator(
                ret => !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown(),
                Spell.Cast("Healing Surge", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 35)
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbieCombat()
        {
            return 
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.WaitForCast(true),
                    CreateLowbieDiagnosticOutputBehavior(),
                    Helpers.Common.CreateAutoAttack(true),
                    Spell.Cast("Earth Shock"),      // always use
                    Spell.Cast("Primal Strike"),    // always use
                    Spell.Cast("Lightning Bolt"),                   
                    Movement.CreateMoveToTargetBehavior(true, 25f)
                    );
        }

        #region Diagnostics

        private static Composite CreateLowbieDiagnosticOutputBehavior()
        {
            return new Throttle(1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        uint lstks = !Me.HasAura("Lightning Shield") ? 0 : Me.ActiveAuras["Lightning Shield"].StackCount;

                        string line = string.Format(".... h={0:F1}%/m={1:F1}%, lstks={2}",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            lstks
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target == null)
                            line += ", target=(null)";
                        else
                            line += string.Format(", target={0} @ {1:F1} yds, th={2:F1}%, tlos={3}, tloss={4}",
                                target.SafeName(),
                                target.Distance,
                                target.HealthPercent,
                                target.InLineOfSight,
                                target.InLineOfSpellSight
                                );

                        Logger.WriteDebug(Color.PaleVioletRed, line);
                        return RunStatus.Success;
                    }))
                );
        }

        #endregion
    }
}
