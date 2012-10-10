using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Monk
{
    public class Windwalker
    {
        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkWindwalker)]
        public static Composite CreateWindwalkerMonkCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                CreateWindwalkerMonkCombatCommon(),
                new Decorator(ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10*10) < 5,
                              CreateWindwalkerMonkCombatSingleTarget()),
                new Decorator(ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10*10) >= 5,
                              CreateWindwalkerMonkCombatAoE())
                );
        }

        private static Composite CreateWindwalkerMonkCombatCommon()
        {
            return new PrioritySelector(
                // use chisphere if Talent Power Strikes Available, ChiSphere nearby, chi < 4

                new Decorator(
                    ret =>
                    StyxWoW.Me.HasAura("Bloodlust") || StyxWoW.Me.HasAura("Heroism") || StyxWoW.Me.HasAura("Time Warp")
                    || StyxWoW.Me.HasAura("Ancient Hysteria")
                    /*|| targettimetodie <= 60 , function still needs to be written*/,
                    new Action(abc =>
                                   {
                                       WoWItem item = StyxWoW.Me.BagItems.Find(ret => ret.Entry == 76089);
                                       if (item != null) item.Use();
                                   })),
                Spell.Cast("Chi Brew",
                           ret =>
                           TalentManager.IsSelected(9) && StyxWoW.Me.GetCurrentPower(WoWPowerType.LightForce) == 0),
                Spell.Cast("Rising Sun Kick", ret =>
                                              ((StyxWoW.Me.CurrentTarget.HasAura("Rising Sun Kick") &&
                                                StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rising Sun Kick", true).
                                                    TotalSeconds < 3)
                                               ||
                                               (!StyxWoW.Me.CurrentTarget.HasAura("Rising Sun Kick")) &&
                                               StyxWoW.Me.GetCurrentPower(WoWPowerType.LightForce) >= 2)),
                Spell.Cast("Tiger Palm", ret => (StyxWoW.Me.GetCurrentPower(WoWPowerType.LightForce) >= 1)
                                                && (
                                                       !StyxWoW.Me.HasAura("Tiger Power") ||
                                                       (StyxWoW.Me.HasAura("Tiger Power") &&
                                                        StyxWoW.Me.Auras["Tiger Power"].StackCount < 3) ||
                                                       (StyxWoW.Me.HasAura("Tiger Power") &&
                                                        StyxWoW.Me.GetAuraTimeLeft("Tiger Power", true).TotalSeconds < 3)
                                                   )),
                Spell.Cast("Tigereye Brew",
                           ret =>
                           !StyxWoW.Me.HasAura("Tigereye Bew") ||
                           (StyxWoW.Me.HasAura("Tigereye Bew") && (StyxWoW.Me.Auras["Tigereye Brew"].StackCount == 10))),
                Spell.Cast("Energizing Brew", ret => Spell.TimeToEnergyCap() > 5),
                Spell.Cast("Invoke Xuen, the White Tiger", ret => TalentManager.IsSelected(17)),
                Spell.Cast("Rushing Jade Wind", ret => TalentManager.IsSelected(16))
                );
        }

        private static Composite CreateWindwalkerMonkCombatSingleTarget()
        {
            return new PrioritySelector(
                Spell.Cast("Rising Sun Kick"),
                Spell.Cast("Fists of Fury",
                           ret =>
                           (!StyxWoW.Me.HasAura("Energizing Brew") && (Spell.TimeToEnergyCap() > 5) &&
                            StyxWoW.Me.GetAuraTimeLeft("Tiger Power", true).TotalSeconds > 4
                            && (StyxWoW.Me.HasAura("Tiger Power") && StyxWoW.Me.Auras["Tiger Power"].StackCount == 3))),
                Spell.Cast("Blackout Kick", ret => StyxWoW.Me.HasAura("Combo Breaker: Blackout Kick")),
                Spell.Cast("Blackout Kick",
                           ret =>
                           StyxWoW.Me.GetCurrentPower(WoWPowerType.LightForce) >= 3 && Spell.TimeToEnergyCap() <= 2),
                Spell.Cast("Tiger Palm", ret =>
                                         (
                                             (StyxWoW.Me.HasAura("Combobreaker: Tiger Palm") &&
                                              Spell.TimeToEnergyCap() >= 2)
                                             ||
                                             StyxWoW.Me.HasAura("Combobreaker: Tiger Palm") &&
                                             StyxWoW.Me.GetAuraTimeLeft("Combobreaker Tiger Palm", true).TotalSeconds <=
                                             2
                                         )
                    ),
                Spell.Cast("Tiger Palm",
                           ret =>
                           TalentManager.IsSelected(8) && StyxWoW.Me.GetCurrentPower(WoWPowerType.LightForce) <= 3),
                Spell.Cast("Tiger Palm",
                           ret =>
                           TalentManager.IsSelected(9) && StyxWoW.Me.GetCurrentPower(WoWPowerType.LightForce) <= 2),
                Spell.Cast("Jab", ret => TalentManager.IsSelected(7) &&
                                         (
                                             (SpellManager.Spells["Power Strikes"].CooldownTimeLeft >
                                              TimeSpan.FromSeconds(0)) &&
                                             StyxWoW.Me.GetCurrentPower(WoWPowerType.LightForce) <= 2)
                                         ||
                                         (!SpellManager.Spells["Power Strikes"].Cooldown.Equals(false) &&
                                          (StyxWoW.Me.GetCurrentPower(WoWPowerType.LightForce) <= 1))
                    ),
                Spell.Cast("Blackout Kick", ret =>
                                            ((StyxWoW.Me.CurrentEnergy +
                                              (Spell.EnergyRegen()*
                                               (SpellManager.Spells["Rising Sun Kick"].CooldownTimeLeft.TotalSeconds))) >
                                             40)
                                            ||
                                            (StyxWoW.Me.GetCurrentPower(WoWPowerType.LightForce) == 4 &&
                                             !TalentManager.IsSelected(8))
                                            ||
                                            (StyxWoW.Me.GetCurrentPower(WoWPowerType.LightForce) == 5 &&
                                             TalentManager.IsSelected(8))
                    ),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Composite CreateWindwalkerMonkCombatAoE()
        {
            return new PrioritySelector(
                Spell.Cast("Rising Sun Kick", ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.LightForce) == 4),
                Spell.Cast("Spinning Crane Kick")
                );
        }


        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkWindwalker)]
        public static Composite CreateWindwalkerMonkPull()
        {
            return new PrioritySelector(
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}