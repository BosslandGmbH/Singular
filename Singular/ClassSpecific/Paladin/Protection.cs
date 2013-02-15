using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.Paladin
{
    public class Protection
    {
        [Behavior(BehaviorType.Rest, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreateProtectionRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        // Rest up damnit! Do this first, so we make sure we're fully rested.
                        Rest.CreateDefaultRestBehaviour( null, "Redemption")
                        )
                    )
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreateProtectionCombat()
        {
            return new PrioritySelector(
                ctx => TankManager.Instance.FirstUnit ?? StyxWoW.Me.CurrentTarget,
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptBehavior(),

                // Seal twisting. If our mana gets stupid low, just throw on insight to get some mana back quickly, then put our main seal back on.
                // This is Seal of Truth once we get it, Righteousness when we dont.
                Common.CreatePaladinSealBehavior(),

                // Defensive
                Spell.BuffSelf("Hand of Freedom",
                    ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                          WoWSpellMechanic.Disoriented,
                                                          WoWSpellMechanic.Frozen,
                                                          WoWSpellMechanic.Incapacitated,
                                                          WoWSpellMechanic.Rooted,
                                                          WoWSpellMechanic.Slowed,
                                                          WoWSpellMechanic.Snared)),

                Spell.BuffSelf("Divine Shield",
                    ret => StyxWoW.Me.CurrentMap.IsBattleground && StyxWoW.Me.HealthPercent <= 20 && !StyxWoW.Me.HasAura("Forbearance")),

                Spell.Cast("Reckoning",
                    ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                    ret => SingularSettings.Instance.EnableTaunting && StyxWoW.Me.IsInInstance),

                //Multi target
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 4,
                    new PrioritySelector(
                        Spell.Cast("Shield of the Righteous", ret => StyxWoW.Me.CurrentHolyPower >= 3),
                        Spell.Cast("Judgment", ret => SpellManager.HasSpell("Sanctified Wrath") && StyxWoW.Me.HasAura("Avenging Wrath")),
                        Spell.Cast("Hammer of the Righteous"),
                        Spell.Cast("Judgment"),
                        Spell.Cast("Avenger's Shield", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Grand Crusader")),
                        Spell.Cast("Consecration", ret => !StyxWoW.Me.IsMoving ),
                        Spell.Cast("Avenger's Shield"),
                        Spell.Cast("Holy Wrath"),
                        Movement.CreateMoveToMeleeBehavior(true)
                        )),
                //Single target
                Spell.Cast("Shield of the Righteous", ret => StyxWoW.Me.CurrentHolyPower >= 3),
                Spell.Cast("Hammer of the Righteous", ret => !StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Weakened Blows")),
                Spell.Cast("Judgment", ret => SpellManager.HasSpell("Sanctified Wrath") && StyxWoW.Me.HasAura("Avenging Wrath")),
                Spell.Cast("Crusader Strike"),
                Spell.Cast("Judgment"),
                Spell.Cast("Avenger's Shield", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Grand Crusader")),
                Spell.Cast("Consecration", ret => !StyxWoW.Me.IsMoving),
                Spell.Cast("Holy Wrath"),
                Spell.BuffSelf("Sacred Shield", ret => SpellManager.HasSpell("Sacred Shield")),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        [Behavior(BehaviorType.Pull, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreatePaladinProtectionPull()
        {
            return
                new PrioritySelector(
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateDismount("Pulling"),
                    Helpers.Common.CreateAutoAttack(true),
                    Spell.BuffSelf("Sacred Shield", ret => SpellManager.HasSpell("Sacred Shield")),
                    Spell.Cast("Judgment"),
                    Spell.Cast("Avenger's Shield"),
                    Movement.CreateMoveToTargetBehavior(true, 5f)
                    );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreatePaladinProtectionCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.Cast(
                        "Reckoning",
                        ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                        ret => SingularSettings.Instance.EnableTaunting && TankManager.Instance.NeedToTaunt.Count != 0),
                    Spell.BuffSelf("Avenging Wrath"),
                    Spell.BuffSelf(
                        "Lay on Hands",
                        ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin().LayOnHandsHealth && !StyxWoW.Me.HasAura("Forbearance")),
                    Spell.BuffSelf(
                        "Guardian of Ancient Kings",
                        ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin().GoAKHealth),
                    Spell.BuffSelf(
                        "Ardent Defender",
                        ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin().ArdentDefenderHealth),
                    Spell.BuffSelf(
                        "Divine Protection",
                        ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin().DivineProtectionHealthProt),
                    // Symbiosis
                    Spell.BuffSelf(
                        "Barkskin",
                        ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin().DivineProtectionHealthProt 
                            && !StyxWoW.Me.HasAura("Divine Protection")
                            && Spell.GetSpellCooldown("Divine Protection", 6).TotalSeconds > 0),

                    Spell.BuffSelf("Word of Glory", ret => StyxWoW.Me.HealthPercent < 50 && StyxWoW.Me.CurrentHolyPower == 3),
                    Spell.BuffSelf("Word of Glory", ret => StyxWoW.Me.HealthPercent < 25 && StyxWoW.Me.CurrentHolyPower == 2),
                    Spell.BuffSelf("Word of Glory", ret => StyxWoW.Me.HealthPercent < 15 && StyxWoW.Me.CurrentHolyPower == 1)
                    );
        }

        /*[Class(WoWClass.Paladin)]
        [Spec(WoWSpec.PaladinProtection)]
        [Behavior(BehaviorType.PullBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreatePaladinProtectionPullBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Divine Plea")
                    );
        }*/
    }
}
