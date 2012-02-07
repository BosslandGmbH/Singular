using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Mage
{
    public class Fire
    {
        #region Normal Rotation

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.Normal)]
        public static Composite CreateFireMageNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),
                Spell.Cast("Pyroblast"),
                Spell.Cast("Fireball"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateFireMageNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 20 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),

                // Cooldowns
                Spell.BuffSelf("Evocation",
                    ret => StyxWoW.Me.ManaPercent < 30 || (TalentManager.HasGlyph("Evocation") && StyxWoW.Me.HealthPercent < 50)),
                Spell.BuffSelf("Mage Ward", ret => StyxWoW.Me.HealthPercent <= 80),
                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.HealthPercent <= 60),

                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 3,
                    new PrioritySelector(
                        Spell.BuffSelf("Mirror Image")
                        )),
                new Decorator(ret => StyxWoW.Me.ManaPercent < 80 && Common.HaveManaGem() && !Common.ManaGemNotCooldown(),
                    new Action(ctx => Common.UseManaGem())),

                // Rotation
                Spell.Cast("Dragon's Breath",
                    ret => StyxWoW.Me.IsSafelyFacing(StyxWoW.Me.CurrentTarget, 90) &&
                           StyxWoW.Me.CurrentTarget.DistanceSqr <= 8 * 8),

                Spell.Cast("Fire Blast",
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Impact")),

                new Decorator(
                    ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 10 * 10 && u.IsCrowdControlled()),
                    new PrioritySelector(
                        Spell.BuffSelf("Frost Nova",
                            ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                            u.DistanceSqr <= 8 * 8 && !u.HasAura("Freeze") &&
                                            !u.HasAura("Frost Nova") && !u.Stunned))
                        )),

                Common.CreateMagePolymorphOnAddBehavior(),
                // Rotation
                Spell.Cast("Combustion",
                    ret => (StyxWoW.Me.CurrentTarget.HasMyAura("Living Bomb") || !SpellManager.HasSpell("Living Bomb")) &&
                           (StyxWoW.Me.CurrentTarget.HasMyAura("Ignite") || TalentManager.GetCount(2, 4) == 0) &&
                           StyxWoW.Me.CurrentTarget.HasMyAura("Pyroblast!")),
                Spell.Cast("Scorch", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Critical Mass", true).TotalSeconds < 1 && TalentManager.GetCount(2, 20) != 0),
                Spell.Cast("Pyroblast", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Hot Streak")),
                Spell.BuffSelf("Flame Orb"),
                Spell.Buff("Living Bomb", true),
                Spell.Cast("Fireball"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateFireMagePvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 10 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),
                Spell.BuffSelf("Blink", ret => StyxWoW.Me.IsStunned() || StyxWoW.Me.IsRooted()),
                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.HealthPercent <= 75),
                Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8 && !u.HasAura("Freeze") && !u.HasAura("Frost Nova") && !u.Stunned)),
                new Decorator(ret => StyxWoW.Me.ManaPercent < 80 && Common.HaveManaGem() && !Common.ManaGemNotCooldown(),
                    new Action(ctx => Common.UseManaGem())),
                // Cooldowns
                Spell.BuffSelf("Evocation", ret => StyxWoW.Me.ManaPercent < 30),
                Spell.BuffSelf("Mirror Image"),
                Spell.BuffSelf("Mage Ward", ret => StyxWoW.Me.HealthPercent <= 75),

                Spell.Cast("Dragon's Breath",
                    ret => StyxWoW.Me.IsSafelyFacing(StyxWoW.Me.CurrentTarget, 90) && 
                           StyxWoW.Me.CurrentTarget.DistanceSqr <= 8*8),

                Spell.Cast("Fire Blast",
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Impact")),
                // Rotation
                Spell.Cast("Combustion",
                    ret => (StyxWoW.Me.CurrentTarget.HasMyAura("Living Bomb") || !SpellManager.HasSpell("Living Bomb")) &&
                           (StyxWoW.Me.CurrentTarget.HasMyAura("Ignite") || TalentManager.GetCount(2, 4) == 0) &&
                           StyxWoW.Me.CurrentTarget.HasMyAura("Pyroblast!")),
                Spell.Cast("Scorch", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Critical Mass", true).TotalSeconds < 1 && TalentManager.GetCount(2, 20) != 0),
                Spell.Cast("Pyroblast", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Hot Streak")),
                Spell.BuffSelf("Flame Orb"),
                Spell.Buff("Living Bomb", true),
                Spell.Cast("Fireball"),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Instance Rotation

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateFireMageInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 20 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),

                // Cooldowns
                Spell.BuffSelf("Evocation", ret => StyxWoW.Me.ManaPercent < 30),
                Spell.BuffSelf("Mirror Image"),
                Spell.BuffSelf("Mage Ward", ret => StyxWoW.Me.HealthPercent <= 75),

                new Decorator(ret => StyxWoW.Me.ManaPercent < 80 && Common.HaveManaGem() && !Common.ManaGemNotCooldown(),
                    new Action(ctx => Common.UseManaGem())),
                // AoE comes first
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    new PrioritySelector(
                        Spell.Cast("Fire Blast", 
                            ret => StyxWoW.Me.ActiveAuras.ContainsKey("Impact") && 
                                   (StyxWoW.Me.CurrentTarget.HasMyAura("Combustion") || TalentManager.GetCount(2, 13) == 0)),
                        Spell.CastOnGround("Blast Wave", 
                            ret => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f).Location),
                        Spell.Cast("Dragon's Breath",
                            ret => Clusters.GetClusterCount(StyxWoW.Me.CurrentTarget,
                                                            Unit.NearbyUnfriendlyUnits,
                                                            ClusterType.Cone, 15f) >= 3),
                        Spell.CastOnGround("Flamestrike", 
                            ret => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f).Location,
                            ret => !ObjectManager.GetObjectsOfType<WoWDynamicObject>().Any(o => 
                                        o.CasterGuid == StyxWoW.Me.Guid && o.Spell.Name == "Flamestrike" &&
                                        o.Location.Distance(
                                            Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f).Location) < o.Radius))
                        )),

                Spell.BuffSelf("Time Warp",
                    ret => !StyxWoW.Me.IsInRaid && StyxWoW.Me.CurrentTarget.HealthPercent > 20 && StyxWoW.Me.CurrentTarget.IsBoss() &&
                           !StyxWoW.Me.HasAura("Temporal Displacement")),

                // Rotation
                Spell.Cast("Combustion", 
                    ret => (StyxWoW.Me.CurrentTarget.HasMyAura("Living Bomb") || !SpellManager.HasSpell("Living Bomb")) &&
                           (StyxWoW.Me.CurrentTarget.HasMyAura("Ignite") || TalentManager.GetCount(2,4) == 0) &&
                           StyxWoW.Me.CurrentTarget.HasMyAura("Pyroblast!")),
                Spell.Cast("Scorch", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Critical Mass", true).TotalSeconds < 1 && TalentManager.GetCount(2, 20) != 0),
                Spell.Cast("Pyroblast", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Hot Streak")),
                Spell.BuffSelf("Flame Orb"),
                Spell.Buff("Living Bomb", true),
                Spell.Cast("Fireball"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion



        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateFireMageCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 10 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),
                new Decorator(ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                   new ActionIdle()),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.WaitForCast(),
                Spell.Buff("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8)),
                Spell.Cast("Evocation", ret => StyxWoW.Me.ManaPercent < 20),
                new Decorator(ret => Common.HaveManaGem() && StyxWoW.Me.ManaPercent <= 30,
                   new Action(ctx => Common.UseManaGem())),
                Spell.BuffSelf("Mana Shield", ret => !StyxWoW.Me.Auras.ContainsKey("Mana Shield") && StyxWoW.Me.HealthPercent <= 75),
                Common.CreateMagePolymorphOnAddBehavior(),
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
            if (TalentManager.GetCount(2,7) > 0)
            {
                // Now that we know its even worth spreading dots, lets make sure the current target has dots worth spreading!
                return StyxWoW.Me.CurrentTarget.HasAllMyAuras("Ignite", "Combustion");
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
    }
}
