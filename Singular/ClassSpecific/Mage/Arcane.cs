using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Mage
{
    public class Arcane
    {
        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Normal)]
        public static Composite CreateMageArcaneNormalPull()
        {
            return new PrioritySelector(
                    Safers.EnsureTarget(),
                    Common.CreateStayAwayFromFrozenTargetsBehavior(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),

                    Spell.WaitForCast(true),


                    Spell.Cast("FrostFire Bolt", ret => !SpellManager.HasSpell("Arcane Blast")),
                    Movement.CreateMoveToTargetBehavior(true, 39f),
                    Spell.Cast("Arcane Blast")
                    );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Normal)]
        public static Composite CreateMageArcaneNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 10 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),
                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.HealthPercent <= 75),
                Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 11 && !u.HasAura("Frost Nova"))),
                Common.CreateMagePolymorphOnAddBehavior(),





                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.ManaPercent < 30),
                Spell.BuffSelf("Evocation", ret => StyxWoW.Me.ManaPercent < 30 && (StyxWoW.Me.HasAura("Mana Shield") || !SpellManager.HasSpell("Mana Shield"))),
                Spell.BuffSelf("Arcane Power"),
                Spell.BuffSelf("Mirror Image"),
                Spell.Cast("Arcane Missiles", ret => StyxWoW.Me.HasAura("Arcane Missiles!")),
                Spell.Cast("Arcane Barrage", ret => StyxWoW.Me.GetAuraByName("Arcane Charge") != null && StyxWoW.Me.GetAuraByName("Arcane Charge").StackCount >= 4),
                Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast")),

                Spell.Cast("Arcane Blast"),
                Movement.CreateMoveToTargetBehavior(true, 39f)

                );
        }

        #endregion

        #region Battleground Rotation
        [Behavior(BehaviorType.Pull|BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Battlegrounds)]
        public static Composite CreateArcaneMagePvPPullAndCombat()
        {
            return new PrioritySelector(
                           Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
       
                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 10 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),
                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.HealthPercent <= 75),
                Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 11 && !u.HasAura("Frost Nova"))),
                Common.CreateMagePolymorphOnAddBehavior(),





                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.ManaPercent < 30),
                Spell.BuffSelf("Evocation", ret => StyxWoW.Me.ManaPercent < 30 && (StyxWoW.Me.HasAura("Mana Shield") || !SpellManager.HasSpell("Mana Shield"))),
                Spell.BuffSelf("Arcane Power"),
                Spell.BuffSelf("Mirror Image"),
                Spell.BuffSelf("Flame Orb"),
                Spell.Cast("Arcane Missiles", ret => StyxWoW.Me.HasAura("Arcane Missiles!")),
                Spell.Cast("Arcane Barrage", ret => StyxWoW.Me.GetAuraByName("Arcane Charge") != null && StyxWoW.Me.GetAuraByName("Arcane Charge").StackCount >= 4),
                Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast")),
                Spell.Cast("Arcane Blast"),
                Movement.CreateMoveToTargetBehavior(true, 39f)
                
                );
        }
        

        #endregion

        #region Instance Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Instances)]
        public static Composite CreateMageArcaneInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 10 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),
                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.HealthPercent <= 75),
                Spell.Cast("Focus Magic",
                    ret => StyxWoW.Me.RaidMemberInfos.
                                        Where(m => m.HasRole(WoWPartyMember.GroupRole.Damage) && m.ToPlayer() != null).
                                        Select(m => m.ToPlayer()).
                                        FirstOrDefault(),
                    ret => !StyxWoW.Me.RaidMemberInfos.
                                        Any(m => m.ToPlayer() != null && m.ToPlayer().HasMyAura("Focus Magic"))),
                // AoE comes first
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    new PrioritySelector(
                        Spell.CastOnGround("Flamestrike",
                            ret => Clusters.GetBestUnitForCluster(StyxWoW.Me.Combat ? Unit.NearbyUnitsInCombatWithMe : Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f).Location,
                            ret => !ObjectManager.GetObjectsOfType<WoWDynamicObject>().Any(o =>
                                        o.CasterGuid == StyxWoW.Me.Guid && o.Spell.Name == "Flamestrike" &&
                                        o.Location.Distance(
                                            Clusters.GetBestUnitForCluster(StyxWoW.Me.Combat ? Unit.NearbyUnitsInCombatWithMe : Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f).Location) < o.Radius)),
                        Spell.Cast("Arcane Explosion",
                            ret => StyxWoW.Me.HasAura("Arcane Blast", 3) &&
                                   Clusters.GetClusterCount(StyxWoW.Me,
                                                            Unit.NearbyUnfriendlyUnits,
                                                            ClusterType.Radius,
                                                            10f) >= 3),
                        Spell.CastOnGround("Blizzard",
                            ret => Clusters.GetBestUnitForCluster(StyxWoW.Me.Combat ? Unit.NearbyUnitsInCombatWithMe : Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f).Location,
                            ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),

                        Spell.Cast("Arcane Blast"),
                        Movement.CreateMoveToTargetBehavior(true, 39f)
                        )),

                Spell.BuffSelf("Time Warp",
                    ret => !StyxWoW.Me.IsInRaid && StyxWoW.Me.CurrentTarget.HealthPercent > 20 && StyxWoW.Me.CurrentTarget.IsBoss() &&
                           !StyxWoW.Me.HasAura("Temporal Displacement")),

                // Burn mana phase
                new Decorator(
                    ret => SpellManager.HasSpell("Evocation") && SpellManager.Spells["Evocation"].CooldownTimeLeft.TotalSeconds < 30 &&
                           StyxWoW.Me.ManaPercent > 10,
                    new PrioritySelector(
                        Common.CreateUseManaGemBehavior(ret => StyxWoW.Me.ManaPercent < 80),

                        Spell.BuffSelf("Arcane Power"),
                        Spell.BuffSelf("Mirror Image"),
                        Spell.Cast("Arcane Blast"),
                        Movement.CreateMoveToTargetBehavior(true, 35f)
                        )),

                // Reserve mana phase

                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.ManaPercent < 30),
                Spell.BuffSelf("Evocation", ret => StyxWoW.Me.ManaPercent < 30 && (StyxWoW.Me.HasAura("Mana Shield") || !SpellManager.HasSpell("Mana Shield"))),

                Spell.Cast("Arcane Missiles", ret => StyxWoW.Me.HasAura("Arcane Missiles!")),
                Spell.Cast("Arcane Barrage", ret => StyxWoW.Me.GetAuraByName("Arcane Charge") != null && StyxWoW.Me.GetAuraByName("Arcane Charge").StackCount >= 4),
                Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast")),
                Movement.CreateMoveToTargetBehavior(true, 39f),
                Spell.Cast("Arcane Blast")
                );
        }

        #endregion
    }
}
