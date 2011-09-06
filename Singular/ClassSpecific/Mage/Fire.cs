using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Pathing;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Mage
{
    public class Fire
    {
        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateFireMageCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                //Move away from frozen targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.HasAura("Frost Nova") && StyxWoW.Me.CurrentTarget.DistanceSqr < 5 * 5,
                    new Action(
                        ret =>
                        {
                            Logger.Write("Getting away from frozen target");
                            WoWPoint moveTo = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, StyxWoW.Me.CurrentTarget.Location, 10f);

                            if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                            {
                                Navigator.MoveTo(moveTo);
                            }
                        })),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 10 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),
                new Decorator(ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                   new ActionIdle()),
                Spell.WaitForCast(),
                Spell.Buff("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8)),
                Spell.Cast("Evocation", ret => StyxWoW.Me.ManaPercent < 20),
                new Decorator(ret => Common.HaveManaGem() && StyxWoW.Me.ManaPercent <= 30,
                   new Action(ctx => Common.UseManaGem())),
                Spell.BuffSelf("Mana Shield", ret => !StyxWoW.Me.Auras.ContainsKey("Mana Shield") && StyxWoW.Me.HealthPercent <= 75),
                Common.CreateMagePolymorphOnAddBehavior(),
                Spell.Cast("Counterspell", ret => StyxWoW.Me.CurrentTarget.IsCasting),
                Spell.Cast("Mirror Image", ret => StyxWoW.Me.CurrentTarget.HealthPercent > 20),
                Spell.Cast("Time Warp", ret => StyxWoW.Me.CurrentTarget.HealthPercent > 20 && StyxWoW.Me.CurrentTarget.IsBoss()),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent > 50,
                    new Sequence(
                        new Action(ctx => StyxWoW.Me.CurrentTarget.Face()),
                        new Action(ctx => StyxWoW.SleepForLagDuration()),
                        new PrioritySelector(Spell.Cast("Flame Orb"))
                        )),
                Spell.Cast("Scorch", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Critical Mass", true).TotalSeconds < 1 && TalentManager.GetCount(2, 20) != 0),
                Spell.Cast("Pyroblast", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Hot Streak") && StyxWoW.Me.ActiveAuras["Hot Streak"].TimeLeft.TotalSeconds > 1),
                // Don't bother pushing dots around w/o ignite up. Kthx.
                Spell.Cast("Fire Blast", ret => CanImpactAoe()),
                Spell.Buff("Living Bomb", ret => !StyxWoW.Me.CurrentTarget.HasAura("Living Bomb")),
                Spell.Cast("Combustion", ret => StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Living Bomb") && StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Ignite") && StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Pyroblast!")),
                Spell.Cast("Fireball"),
                //Helpers.Common.CreateUseWand(),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        private static bool CanImpactAoe()
        {
            if (StyxWoW.Me.ActiveAuras.ContainsKey("Impact"))
            {
                var loc = StyxWoW.Me.CurrentTarget.Location;
                // Impact spreads dots to 12yds away. Not effective unless 3+ mobs are around.
                if (Unit.NearbyUnfriendlyUnits.Count(u=>u.Location.DistanceSqr(loc) <= 12*12) > 2)
                {
                    // Now that we know its even worth spreading dots, lets make sure the current target has dots worth spreading!
                    return StyxWoW.Me.CurrentTarget.HasAllMyAuras("Ignite", "Combustion");
                }
            }
            return false;
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateFireMagePull()
        {
            return
                new PrioritySelector(
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.Cast("Pyroblast"),
                    Spell.Cast("Fireball"),
                    Movement.CreateMoveToTargetBehavior(true, 35f)
                    );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateFireMagePreCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf(
                        "Arcane Brilliance",
                        ret => (!StyxWoW.Me.HasAura("Arcane Brilliance") &&
                                !StyxWoW.Me.HasAura("Fel Intelligence"))),
                    Spell.BuffSelf(
                        "Molten Armor",
                        ret => (!StyxWoW.Me.HasAura("Molten Armor"))
                        )
                    );
        }
    }
}
