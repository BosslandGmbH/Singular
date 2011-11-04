using System.Linq;

using CommonBehaviors.Actions;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public class FeralBearTank
    {
        private static DruidSettings Settings { get { return SingularSettings.Instance.Druid; } }

        private static Composite CreateBearTankManualForms()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                // If we're in caster form, and not casting anything (tranq), then fucking switch to bear.
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Normal,
                    Spell.BuffSelf("Bear Form")),
                new Decorator(
                    ret => !Settings.ManualForms && StyxWoW.Me.Shapeshift != ShapeshiftForm.Bear,
                    Spell.BuffSelf("Bear Form")),
                // If the user has manual forms enabled. Automatically switch to cat combat if they switch forms.
                new Decorator(
                    ret => Settings.ManualForms && StyxWoW.Me.Shapeshift == ShapeshiftForm.Cat,
                    new PrioritySelector(
                        FeralCat.CreateFeralCatActualCombat(),
                        new ActionAlwaysSucceed()))
                );
        }

        [Spec(TalentSpec.FeralTankDruid)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.Instances)]
        [Priority(1000)]
        [Class(WoWClass.Druid)]
        public static Composite CreateBearTankCombat()
        {
            return new PrioritySelector(
                CreateBearTankManualForms(),
                CreateBearTankActualCombat());
        }

        public static Composite CreateBearTankActualCombat()
        {
            TankManager.NeedTankTargeting = true;
            return new PrioritySelector(
                ctx => TankManager.Instance.FirstUnit ?? StyxWoW.Me.CurrentTarget,
                //((WoWUnit)ret)

                Spell.WaitForCast(),
                // If we're in caster form, and not casting anything (tranq), then fucking switch to bear.
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Normal,
                    Spell.BuffSelf("Bear Form")),
                new Decorator(
                    ret => !Settings.ManualForms && StyxWoW.Me.Shapeshift != ShapeshiftForm.Bear,
                    Spell.BuffSelf("Bear Form")),
                // If the user has manual forms enabled. Automatically switch to cat combat if they switch forms.
                new Decorator(
                    ret => Settings.ManualForms && StyxWoW.Me.Shapeshift == ShapeshiftForm.Cat,
                    new PrioritySelector(
                        FeralCat.CreateFeralCatActualCombat(),
                        new ActionAlwaysSucceed())),
                Safers.EnsureTarget(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => Settings.UseFeralChargeBear && ((WoWUnit)ret).Distance > 8f && ((WoWUnit)ret).Distance < 25f,
                    Spell.Cast("Feral Charge (Bear)", ret => ((WoWUnit)ret))),
                // Defensive CDs are hard to 'roll' from this type of logic, so we'll simply use them more as 'oh shit' buttons, than anything.
                // Barkskin should be kept on CD, regardless of what we're tanking
                Spell.BuffSelf("Barkskin", ret => StyxWoW.Me.HealthPercent < Settings.FeralBarkskin),
                // Since Enrage no longer makes us take additional damage, just keep it on CD. Its a rage boost, and coupled with King of the Jungle, a DPS boost for more threat.
                Spell.BuffSelf("Enrage"),
                // Only pop SI if we're taking a bunch of damage.
                Spell.BuffSelf("Survival Instincts", ret => StyxWoW.Me.HealthPercent < Settings.SurvivalInstinctsHealth),
                // We only want to pop FR < 30%. Users should not be able to change this value, as FR automatically pushes us to 30% hp.
                Spell.BuffSelf("Frenzied Regeneration", ret => StyxWoW.Me.HealthPercent < Settings.FrenziedRegenerationHealth),
                // Make sure we deal with interrupts...
                //Spell.Cast(80964 /*"Skull Bash (Bear)"*/, ret => (WoWUnit)ret, ret => ((WoWUnit)ret).IsCasting),
                Helpers.Common.CreateInterruptSpellCast(ret => ((WoWUnit)ret)),
                new Decorator(
                    ret => Targeting.GetAggroOnMeWithin(StyxWoW.Me.Location, 15f) > 2,
                    new PrioritySelector(
                        Spell.Cast("Berserk"),
                        Spell.Cast("Maul"),
                        Spell.Cast("Thrash"),
                        Spell.Cast("Swipe (Bear)"),
                        Spell.Cast("Mangle (Bear)")
                        )),
                // If we have 3+ units not targeting us, and are within 10yds, then pop our AOE taunt. (These are ones we have 'no' threat on, or don't hold solid threat on)
                Spell.Cast(
                    "Challenging Roar", ret => TankManager.Instance.NeedToTaunt.First(),
                    ret => SingularSettings.Instance.EnableTaunting && TankManager.Instance.NeedToTaunt.Count(u => u.Distance <= 10) >= 3),
                // If there's a unit that needs taunting, do it.
                Spell.Cast(
                    "Growl", ret => TankManager.Instance.NeedToTaunt.First(),
                    ret => SingularSettings.Instance.EnableTaunting && TankManager.Instance.NeedToTaunt.FirstOrDefault() != null),
                Spell.Cast("Pulverize", ret => ((WoWUnit)ret).HasAura("Lacerate", 3) && !StyxWoW.Me.HasAura("Pulverize")),

                Spell.Cast("Demoralizing Roar", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 10 && !u.HasDemoralizing())),

                Spell.Cast("Faerie Fire (Feral)", ret => !((WoWUnit)ret).HasSunders()),
                Spell.Cast("Mangle (Bear)"),
                // Maul is our rage dump... don't pop it unless we have to, or we still have > 2 targets.
                Spell.Cast(
                    "Maul",
                    ret =>
                    StyxWoW.Me.RagePercent > 60 || (Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 6) >= 2 && TalentManager.HasGlyph("Maul"))),
                Spell.Cast("Thrash", ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < 8 && u.IsCrowdControlled())),
                Spell.Cast("Lacerate"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}