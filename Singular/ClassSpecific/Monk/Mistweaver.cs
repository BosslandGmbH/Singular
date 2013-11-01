using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Lists;
using Singular.Managers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Singular.Settings;


namespace Singular.ClassSpecific.Monk
{
    // wowraids.org/mistweaver
    public class Mistweaver
    {
        private const int SOOTHING_MIST = 115175;
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MonkSettings MonkSettings { get { return SingularSettings.Instance.Monk(); } }
        public static bool HasTalent(MonkTalents tal) { return TalentManager.IsSelected((int)tal); }


        [Behavior(BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite CreateMistweaverMonkHealBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                    CreateMistweaverMonkHealing(false)
                    ),

                new Decorator(
                    ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe) && Me.HealthPercent < 65,
                    CreateMistweaverMonkHealing(true)
                    )
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite CreateMistweaverMonkRestBehavior()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateMistweaverMonkHealing(true),

                        // cast Mana Tea if solo (farming, grinding, etc.) and low on Mana
                        new Sequence(
                            Spell.Cast( "Mana Tea", ctx => Me, ret => !Me.IsInGroup() && Me.HasAura( "Mana Tea") && Me.ManaPercent < SingularSettings.Instance.MinMana),
                            new WaitContinue( TimeSpan.FromMilliseconds(500), ret => !Me.HasAura("Mana Tea"), new ActionAlwaysSucceed()),
                            Helpers.Common.CreateWaitForLagDuration()
                            ),

                        Rest.CreateDefaultRestBehaviour( null, "Resuscitate"),

                        CreateMistweaverMonkHealing(false)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite CreateMistweaverMonkPullBehavior()
        {
            return new Decorator(
                ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),

                new PrioritySelector(
                    Helpers.Common.EnsureReadyToAttackFromMelee(),
                    Helpers.Common.CreateAutoAttack(true),

                    Spell.WaitForCast(),

                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(), 

                        new PrioritySelector(
                            Common.CreateGrappleWeaponBehavior(),
                            Spell.Cast("Crackling Jade Lightning", ret => !Me.IsMoving && Me.CurrentTarget.Distance < 40),
                            Spell.Cast("Provoke", ret => !Me.CurrentTarget.Combat && Me.CurrentTarget.Distance < 40),
                            Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && !MonkSettings.DisableRoll && !Me.CurrentTarget.IsAboveTheGround() && Me.CurrentTarget.Distance > 12),
                            Spell.Cast("Jab")
                            )
                        ),

                    Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && !MonkSettings.DisableRoll && Me.CurrentTarget.Distance > 12),

                    Movement.CreateMoveToMeleeBehavior(true)
                    )
                );

        }

        [Behavior(BehaviorType.PullBuffs | BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkMistweaver )]
        public static Composite CreateMistweaverCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Stance of the Wise Serpent"), // ret => Me.IsInGroup()),
                // Spell.BuffSelf("Stance of the Fierce Tiger", ret => !Me.IsInGroup())

                // cast Mana Tea if low on Mana
                Spell.Cast( "Mana Tea", 
                    on => Me,
                    req => Me.ManaPercent < SingularSettings.Instance.PotionMana && Me.HasAura("Stance of the Wise Serpent"), 
                    cancel => Me.ManaPercent > 95 )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite CreateMistweaverMonkCombatBehavior()
        {
            return new Decorator(
                ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),

                new PrioritySelector(
                    Helpers.Common.EnsureReadyToAttackFromMelee(),
                    Helpers.Common.CreateAutoAttack(true),

                    Spell.WaitForCastOrChannel(),

                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(), 

                        new PrioritySelector(
                            Helpers.Common.CreateInterruptBehavior(),

                            Spell.Cast("Touch of Death", ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),

                            Spell.Cast("Paralysis",
                                onu => Unit.NearbyUnfriendlyUnits
                                    .FirstOrDefault(u => u.IsCasting && u.Distance.Between(9, 20) && Me.IsSafelyFacing(u))),

                            Spell.Cast("Fists of Fury",
                                ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)) >= 2),

                            Spell.Cast("Rushing Jade Wind", ctx => HasTalent(MonkTalents.RushingJadeWind) && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= 3),
                            Spell.Cast("Spinning Crane Kick", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 3),

                            Spell.Cast("Tiger Palm", ret => Me.CurrentChi > 0 && Me.HasKnownAuraExpired("Tiger Power")),

                            // chi dump
                            Spell.Cast("Blackout Kick", ret => Me.CurrentChi == Me.MaxChi),

                            Spell.Cast("Expel Harm", ret => Me.CurrentChi < (Me.MaxChi - 2) || Me.HealthPercent < 80),
                            // Spell.Cast("Crackling Jade Lightning", req => !Me.IsMoving ),
                            Spell.Cast("Jab", ret => Me.CurrentChi < Me.MaxChi)
                            )
                        ),

                    Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && !MonkSettings.DisableRoll && Me.CurrentTarget.Distance > 12),

                    Movement.CreateMoveToMeleeBehavior(true)
                    )
                );
        }

        private static WoWUnit _targetHeal; 

        public static Composite CreateMistweaverMonkHealing(bool selfOnly)
        {
            HealerManager.NeedHealTargeting = true;

            return new PrioritySelector(

                ctx => _targetHeal = ChooseBestMonkHealTarget(selfOnly ? Me : HealerManager.Instance.FirstUnit),
                
                // if channeling soothing mist and they aren't best target, cancel the cast
                new Decorator(ret => Me.ChanneledCastingSpellId == SOOTHING_MIST && Me.ChannelObject != null && Me.ChannelObject != _targetHeal ,
                    new Sequence(
                        new Action( ret => Logger.Write( System.Drawing.Color.OrangeRed, "/stopcasting: cancel {0} on {1} @ {2:F1}", Me.ChanneledSpell.Name, Me.ChannelObject.SafeName(), Me.ChannelObject.ToUnit().HealthPercent) ),
                        new Action( ret => SpellManager.StopCasting() ),
                        new WaitContinue(new TimeSpan(0, 0, 0, 0, 500), ret => Me.ChanneledCastingSpellId != SOOTHING_MIST, new ActionAlwaysSucceed())
                        )
                    ),

                new Decorator(ret => Me.IsCasting && (Me.ChanneledSpell == null || Me.ChanneledSpell.Id != SOOTHING_MIST), new Action(ret => { return RunStatus.Success; })),

                new Decorator(ret => !Spell.GcdActive,

                    new PrioritySelector(

                        new Decorator(ret => _targetHeal != null && _targetHeal.GetPredictedHealthPercent() <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth,
                
                            new PrioritySelector(

                                ShowHealTarget(ret => _targetHeal),

                                Spell.BuffSelf("Stance of the Wise Serpent"),

                                Helpers.Common.CreateInterruptBehavior(),

                                Common.CastLikeMonk("Fortifying Brew", ret => Me, ret => Me.HealthPercent < 40),

                                Common.CastLikeMonk("Mana Tea", ret => Me, ret => Me.ManaPercent < 60 && Me.HasAura("Mana Tea", 2)),

                                Common.CastLikeMonk("Surging Mist", ret => _targetHeal,
                                    ret => _targetHeal.HealthPercent < 30 && Me.HasAura("Vital Mists", 5)),

                                Common.CastLikeMonk("Soothing Mist", ret => _targetHeal,
                                    ret => _targetHeal.HealthPercent < 90 && Me.ChanneledSpell == null),

                                Common.CastLikeMonk("Surging Mist", ret => _targetHeal,
                                    ret => _targetHeal.HealthPercent < 60),

                                Common.CastLikeMonk("Enveloping Mist", ret => _targetHeal,
                                    ret => _targetHeal.HealthPercent < 89 && Me.CurrentChi >= 3)
                                )
                            )
                        )
                    ),

                new Decorator(
                    ret => !Me.IsCasting,
                    Movement.CreateMoveToUnitBehavior( ret => _targetHeal, 38f)
                    )
                );

        }

        private static ulong guidLastHealTarget = 0;
        private static Composite ShowHealTarget(UnitSelectionDelegate onUnit)
        {
            return 
                new Sequence(
                    new DecoratorContinue( 
                        ret => onUnit(ret) == null && guidLastHealTarget != 0,
                        new Action( ret => {
                            guidLastHealTarget = 0;
                            Logger.WriteDebug(Color.LightGreen, "Heal Target - none");
                            return RunStatus.Failure;
                        })
                        ),

                    new DecoratorContinue(
                        ret => onUnit(ret) != null && guidLastHealTarget != onUnit(ret).Guid,
                        new Action(ret =>
                        {
                            guidLastHealTarget = onUnit(ret).Guid;
                            Logger.WriteDebug(Color.LightGreen, "Heal Target - {0} {1:F1}% @ {2:F1} yds", onUnit(ret).SafeName(), onUnit(ret).HealthPercent, onUnit(ret).Distance);
                            return RunStatus.Failure;
                        })),

                    new Action( ret => { return RunStatus.Failure; } )
                    );
        }

        private static WoWUnit ChooseBestMonkHealTarget(WoWUnit unit)
        {
            if (Me.ChanneledCastingSpellId == SOOTHING_MIST)
            {
                WoWUnit channelUnit = Me.ChannelObject.ToUnit();
                if (channelUnit.HealthPercent <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth)
                    return channelUnit;
            }

            if (unit != null && unit.HealthPercent > SingularSettings.Instance.IgnoreHealTargetsAboveHealth)
                unit = null;

            return unit;
        }
    }

}