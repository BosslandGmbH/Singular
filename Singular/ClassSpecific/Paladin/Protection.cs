using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using TreeSharp;

namespace Singular.ClassSpecific.Paladin
{
    public class Protection
    {
        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateProtectionPaladinCombat()
        {
            TankManager.NeedTankTargeting = true;
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),

                Spell.BuffSelf("Seal of Truth", ret => StyxWoW.Me.ManaPercent >= 5),
                Spell.BuffSelf("Seal of Insight", ret => StyxWoW.Me.ManaPercent < 5),
                
                Spell.Cast("Word of Glory", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 25 && StyxWoW.Me.CurrentHolyPower == 3),
                Spell.Cast("Word of Glory", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 15 && StyxWoW.Me.CurrentHolyPower == 2),
                Spell.Cast("Word of Glory", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 5 && StyxWoW.Me.CurrentHolyPower == 1),

                Spell.Cast("Hammer of Wrath"),
                Spell.Cast("Avenger's Shield"),
                // Same rotation for both.
                Spell.Cast("Shield of the Righteous", ret => StyxWoW.Me.CurrentHolyPower == 3),
                //Multi target
                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(a => a.Distance < 8) > 1,
                    new PrioritySelector(
                        Spell.Cast("Hammer of the Righteous"),
                        Spell.Cast("Crusader Strike"),
                        Spell.Cast("Consecration"),
                        Spell.Cast("Holy Wrath"),
                        Spell.Cast("Judgement"))),
                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(a => a.Distance < 8) <= 1,
                    new PrioritySelector(
                        //Single target
                        Spell.Cast("Crusader Strike"),
                        Spell.Cast("Judgement"),
                        Spell.Cast("Consecration"),
                        Spell.Cast("Holy Wrath"))),
                Movement.CreateMoveToTargetBehavior(true, 5f));
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateProtectionPaladinPull()
        {
            return
                new PrioritySelector(
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateAutoAttack(true),
                    Spell.Cast("Avenger's Shield"),
                    Spell.Cast("Judgement"),
                    Movement.CreateMoveToTargetBehavior(true, 5f)
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateProtectionPaladinCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.Cast(
                        "Hand of Reckoning",
                        ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(), 
                        ret => TankManager.Instance.NeedToTaunt.Count != 0),
                    Spell.BuffSelf("Avenging Wrath"),
                    Spell.BuffSelf(
                        "Lay on Hands",
                        ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.LayOnHandsHealth && !StyxWoW.Me.HasAura("Forbearance")),
                    Spell.BuffSelf(
                        "Guardian of Ancient Kings",
                        ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.GoAKHealth),
                    Spell.BuffSelf(
                        "Ardent Defender",
                        ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.ArdentDefenderHealth),
                    Spell.BuffSelf(
                        "Divine Protection",
                        ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.DivineProtectionHealthProt)
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.PullBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateProtectionPaladinPullBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Divine Plea")
                    );
        }
    }
}
