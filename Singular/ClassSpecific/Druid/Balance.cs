using System;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public class Balance
    {
        private static string _oldDps = "Wrath";

        private static int StarfallRange { get { return TalentManager.HasGlyph("Focus") ? 20 : 40; } }

        private static int CurrentEclipse { get { return BitConverter.ToInt32(BitConverter.GetBytes(StyxWoW.Me.CurrentEclipse), 0); } }

        private static string BoomkinDpsSpell
        {
            get
            {
                if (StyxWoW.Me.HasAura("Eclipse (Solar)"))
                {
                    _oldDps = "Wrath";
                }
                // This doesn't seem to register for whatever reason.
                else if (StyxWoW.Me.HasAura("Eclipse (Lunar)")) //Eclipse (Lunar) => 48518
                {
                    _oldDps = "Starfire";
                }
                //else
                //{
                //    if (oldEclipse == 0)
                //        oldEclipse = CurrentEclipse;

                //    // If our current eclipse is higher than our old one, we want to be casting Starfire.
                //    // Vice versa for Wrath.
                //    if (CurrentEclipse > oldEclipse)
                //        _oldDps = "Starfire";
                //    else if (CurrentEclipse < oldEclipse)
                //        _oldDps = "Wrath";

                //    // If we haven't changed at all... try flipping the DPS spell we wanna use.
                //    //else if (CurrentEclipse == oldEclipse)
                //    //{
                //    //    _oldDps = _oldDps == "Wrath" ? "Starfire" : "Wrath";
                //    //}

                //    oldEclipse = CurrentEclipse;
                //}

                return _oldDps;
            }
        }

        [Class(WoWClass.Druid)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Spec(TalentSpec.BalanceDruid)]
        public static Composite CreateBalanceDruidCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(
                Spell.WaitForCast(true),
                //Heals, will not heal if in a party or if disabled via setting
                Spell.Buff(
                    "Regrowth",
                    ret => (StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.RegrowthBalance
                    && !SingularSettings.Instance.Druid.NoHealBalance
                    && !StyxWoW.Me.IsInParty
                    && !StyxWoW.Me.HasAura("Regrowth"))),
                Spell.Buff(
                    "Rejuvenation",
                    ret => (StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.RejuvenationBalance
                    && !SingularSettings.Instance.Druid.NoHealBalance
                    && !StyxWoW.Me.IsInParty
                    && !StyxWoW.Me.HasAura("Rejuvenation"))),
                Spell.Cast(
                    "Healing Touch",
                    ret => (StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.HealingTouchBalance
                    && !SingularSettings.Instance.Druid.NoHealBalance
                    && !StyxWoW.Me.IsInParty)),
                //Inervate
                Spell.Buff(
                        "Innervate",
                        ret =>
                        StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Druid.InnervateMana),
                // Make sure we're in moonkin form first, period.
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift != Common.WantedDruidForm,
                    Spell.Cast("Moonkin Form")),
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                // Ensure we do /petattack if we have treants up.
                Helpers.Common.CreateAutoAttack(true),
                Spell.Cast("Starfall", ret => SingularSettings.Instance.Druid.UseStarfall),
                Spell.CastOnGround("Force of Nature", ret => StyxWoW.Me.CurrentTarget.Location),
                Spell.Cast("Solar Beam", ret => StyxWoW.Me.CurrentTarget.IsCasting),
                Spell.Cast("Starsurge"),
                Spell.Cast(
                    "Moonfire",
                    ret =>
                    StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Moonfire", true).TotalSeconds < 3 &&
                    StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Sunfire", true).Seconds < 3),
                Spell.Cast("Insect Swarm", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Insect Swarm", true).TotalSeconds < 3),
                //Spell.Cast("Wrath", ret => StyxWoW.Me.HasAura("Eclipse (Solar)")),
                //Spell.Cast("Starfire", ret => StyxWoW.Me.HasAura("Eclipse (Lunar)")),

                Spell.Cast("Wrath", ret => BoomkinDpsSpell == "Wrath"),
                Spell.Cast("Starfire", ret => BoomkinDpsSpell == "Starfire"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }
    }
}
