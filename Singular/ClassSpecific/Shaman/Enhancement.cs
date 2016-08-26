using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Singular.Lists;
using Styx.WoWInternals.WoWObjects;
using Rest = Singular.Helpers.Rest;
using System.Drawing;
using CommonBehaviors.Actions;
using Styx.CommonBot.POI;

namespace Singular.ClassSpecific.Shaman
{
    public class Enhancement
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman(); } }

        private static bool NeedFeralSpirit
        {
            get 
            {
                return ShamanSettings.FeralSpiritCastOn == CastOn.All
                    || (ShamanSettings.FeralSpiritCastOn == CastOn.Bosses && StyxWoW.Me.CurrentTarget.Elite)
                    || (ShamanSettings.FeralSpiritCastOn == CastOn.Players && Unit.NearbyUnfriendlyUnits.Any(u => u.IsPlayer && u.Combat && u.IsTargetingMeOrPet));
            }
        }

        #region Common

        [Behavior(BehaviorType.PreCombatBuffs|BehaviorType.CombatBuffs, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Instances | WoWContext.Normal)]
        public static Composite CreateShamanEnhancementPreCombatBuffs()
        {
            return new PrioritySelector(

                Common.CreateShamanDpsShieldBehavior(),

                Totems.CreateRecallTotems()
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs|BehaviorType.CombatBuffs, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Battlegrounds)]
        public static Composite CreateShamanEnhancementPvpPreCombatBuffs()
        {
            return new PrioritySelector(

                Common.CreateShamanDpsShieldBehavior(),

                Totems.CreateRecallTotems()
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanEnhancement)]
        public static Composite CreateShamanEnhancementRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        new Decorator(
                            ret => !Helpers.Rest.IsEatingOrDrinking,
                            Common.CreateShamanDpsHealBehavior()
                            ),

                        Rest.CreateDefaultRestBehaviour("Healing Surge", "Ancestral Spirit"),

                        Common.CreateShamanMovementBuff()
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Normal)]
        public static Composite CreateShamanEnhancementHeal()
        {
            return new PrioritySelector(
                Spell.CastOnGround("Rainfall", on => Me, ret => Me.HealthPercent < ShamanSettings.Rainfall),
                Spell.Cast("Healing Surge", on => Me, 
                    ret => Me.PredictedHealthPercent(true) < ShamanSettings.MaelHealingSurge && StyxWoW.Me.HasAura("Maelstrom Weapon", 5)),

                Common.CreateShamanDpsHealBehavior()
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Instances)]
        public static Composite CreateShamanEnhancementHealInstances()
        {
            return Common.CreateShamanDpsHealBehavior();
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Battlegrounds)]
        public static Composite CreateShamanEnhancementHealPvp()
        {
            return new PrioritySelector(
                new Decorator(ret => StyxWoW.Me.HasAura("Maelstrom Weapon", 5),
                    new PrioritySelector(
                        Spell.Cast("Healing Surge", ret => StyxWoW.Me, ret => StyxWoW.Me.PredictedHealthPercent() < ShamanSettings.MaelHealingSurge),
                        Spell.Cast("Healing Surge", ret => (WoWPlayer)Unit.GroupMembers.Where(p => p.IsAlive && p.PredictedHealthPercent() < ShamanSettings.MaelPvpOffHeal && p.Distance < 40).FirstOrDefault())
                        )
                    ),

                new Decorator(
                    ret => !StyxWoW.Me.Combat || (!Me.IsMoving && !Unit.NearbyUnfriendlyUnits.Any()),
                    Common.CreateShamanDpsHealBehavior( )
                    )
                );
        }

        #endregion

        #region Normal Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Shaman, WoWSpec.ShamanEnhancement)]
        public static Composite CreateShamanEnhancementNormalPull()
        {
            return new PrioritySelector(
                new Decorator(req => Me.Level < 20, Helpers.Common.EnsureReadyToAttackFromMediumRange()),
                new Decorator(req => Me.Level >= 20, Helpers.Common.EnsureReadyToAttackFromMelee()),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateEnhanceDiagnosticOutputBehavior(),

                        Common.CreateShamanDpsShieldBehavior(),

                        Totems.CreateTotemsBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Spell.Cast("Feral Lunge", ret => ShamanSettings.UseFeralLunge),

                        Spell.Cast("Lightning Bolt", ret => !ShamanSettings.AvoidMaelstromDamage && StyxWoW.Me.HasAura("Maelstrom Weapon", 5)),
                        Spell.Cast("Unleash Elements", 
                            ret => StyxWoW.Me.Inventory.Equipped.OffHand != null 
                                && StyxWoW.Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id == 5),
                        new Decorator(
                            req => Spell.UseAOE,
                            new PrioritySelector(
                                Spell.Cast("Flame Shock", req => StyxWoW.Me.HasAura("Unleash Flame")),
                                Spell.Buff("Flame Shock", true, req => Me.CurrentTarget.Elite || (!Me.CurrentTarget.IsTrivial() && Unit.UnfriendlyUnits(12).Count() > 1) )
                                )
                            ),

                        Spell.Cast("Frost Shock"),

                        Spell.Cast("Lightning Bolt", ret => Me.Level < 20 || Me.CurrentTarget.IsFlying || !Styx.Pathing.Navigator.CanNavigateFully(Me.Location, Me.CurrentTarget.Location))
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanEnhancement)]
        public static Composite CreateShamanEnhancementNormalCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        CreateEnhanceDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Totems.CreateTotemsBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Spell.BuffSelf("Feral Spirit", ret => !Me.CurrentTarget.IsTrivial() && NeedFeralSpirit),

                        new Decorator(
                            req => AttackEvenIfGhostWolf,
                            new PrioritySelector(
                               
                                Dispelling.CreatePurgeEnemyBehavior("Purge"),

                                Common.CreateShamanDpsShieldBehavior(),
                                // Spell.BuffSelf("Spiritwalker's Grace", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.Combat),

                                // pull more logic (use instants first, then ranged pulls if possible)

                                Spell.Cast("Ascendance", req => Me.HealthPercent <= ShamanSettings.AscendanceHealthPercent),
                                Spell.Cast("Lava Lash", req => Me.HasActiveAura("Hot Hand")),
                                Spell.Cast("Windsong"),
                                Spell.Cast("Rockbiter", 
									req => (Common.HasTalent(ShamanTalents.Boulderfist) && !Me.HasActiveAura("Boulderfist")) || 
											(Common.HasTalent(ShamanTalents.Landslide) && !Me.HasActiveAura("Landslide"))),
                                Spell.Cast("Frostbrand", req => Common.HasTalent(ShamanTalents.Hailstorm) && !Me.HasActiveAura("Frostbrand")),
								Spell.Cast("Boulderfist", req => Me.CurrentMaelstrom < 130 && Spell.GetCharges("Boulderfist") >= 2),
                                Spell.Cast("Flametongue", req => !Me.HasActiveAura("Flametongue")),
                                Spell.Cast("Feral Spirit"),
                                Spell.Cast("Earthen Spike"),
                                Spell.Cast("Crash Lightning", when => Unit.UnfriendlyUnitsNearTarget(10).Count(u => u.TaggedByMe || !u.TaggedByOther) >= 2),
                                Spell.Cast("Stormstrike"),
								Spell.Cast("Crash Ligthning", req => Common.HasTalent(ShamanTalents.CrashingStorm)),
                                Spell.Cast("Lava Lash", req => Me.CurrentMaelstrom > 110),
                                Spell.Cast("Sundering", req => SingularRoutine.CurrentWoWContext != WoWContext.Instances),
                                Spell.Cast("Rockbiter"),
                                Spell.Cast("Lightning Bolt", req => !Me.CurrentTarget.IsWithinMeleeRange),
                                // won't happen often, but if at range and no abilities enter ghost wolf 
                                CreateInCombatGapCloser()
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static bool AttackEvenIfGhostWolf
        {
            get
            {
                if (!Me.GotTarget())
                    return false;

                if (Me.CurrentTarget.SpellDistance() > 10 || Me.CurrentTarget.IsMovingAway())
                {
                    if (!Me.CurrentTarget.IsAboveTheGround())
                    {
                        if (Me.IsMoving && Me.HasAura("Ghost Wolf"))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        #endregion


        public static Composite CreateInCombatGapCloser()
        {
            if (!ShamanSettings.UseGhostWolf && !ShamanSettings.UseFeralLunge)
                return new ActionAlwaysFail();

            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                return new ActionAlwaysFail();

            if (!SpellManager.HasSpell("Ghost Wolf") && !SpellManager.HasSpell("Feral Lunge"))
                return new ActionAlwaysFail();

            if (Me.Specialization != WoWSpec.ShamanEnhancement)
                return new ActionAlwaysFail();

            return new Decorator(
                req => Unit.ValidUnit(Me.CurrentTarget)
                    && !Me.CurrentTarget.IsWithinMeleeRange
                    && !Me.Mounted,
                new PrioritySelector(

                    Spell.Cast("Feral Lunge", ret => ShamanSettings.UseFeralLunge), // Instantly get to target.

                    // slow or root based on distance and cooldown
                    new Decorator(
                        req => !Me.CurrentTarget.IsSlowed()
                            && !Me.CurrentTarget.IsRooted()
                            && !Me.CurrentTarget.IsStunned(),
                        new PrioritySelector(
							// quick single spell root
                            Spell.Cast("Frost Shock")

                            )
                        ),

                    // speed boost if needed
                    new Throttle(
                        2,
                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed
                                && !Me.HasAura("Ghost Wolf")
                                && Me.IsMoving // (DateTime.UtcNow - GhostWolfRequest).TotalMilliseconds < 1000
                                && !Me.OnTaxi && !Me.InVehicle 
                                && !Utilities.EventHandlers.IsShapeshiftSuppressed,

                            Spell.BuffSelfAndWait("Ghost Wolf")
                            )
                        )
                    )
                );
        }


        #region Diagnostics

        private static Composite CreateEnhanceDiagnosticOutputBehavior()
        {
            return new ThrottlePasses(1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        uint maelStacks = 0;
                        WoWAura aura = Me.ActiveAuras.Where( a => a.Key == "Maelstrom Weapon").Select( d => d.Value ).FirstOrDefault();
                        if (aura != null)
                        {
                            maelStacks = aura.StackCount;
                            if (maelStacks == 0)
                                Logger.WriteDebug(Color.MediumVioletRed, "Inconsistancy Error:  Maelstrom Weapon buff exists with 0 stacks !!!!");
                            else if ( !Me.HasAura("Maelstrom Weapon", (int)maelStacks))
                                Logger.WriteDebug(Color.MediumVioletRed, "Inconsistancy Error:  Me.HasAura('Maelstrom Weapon', {0}) was False!!!!", maelStacks );
                        }

                        string line = string.Format(".... h={0:F1}%/m={1:F1}%, mov={2}, mael={3}",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.IsMoving.ToYN(),
                            maelStacks
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target == null)
                            line += ", target=(null)";
                        else
                            line += string.Format(", target={0} @ {1:F1} yds, th={2:F1}%, tmelee={3}, tface={4}, tloss={5}, flame={6}, frost={7}", 
                                target.SafeName(), 
                                target.SpellDistance(), 
                                target.HealthPercent,
                                target.IsWithinMeleeRange.ToYN(), 
                                Me.IsSafelyFacing(target,180).ToYN(),
                                target.InLineOfSpellSight.ToYN(),
                                (long) target.GetAuraTimeLeft("Flame Shock").TotalMilliseconds,
                                (long) target.GetAuraTimeLeft("Frost Shock").TotalMilliseconds
                                );

                        Logger.WriteDebug(line);
                        return RunStatus.Failure;
                    }))
                );
        }

        #endregion
    }
}
