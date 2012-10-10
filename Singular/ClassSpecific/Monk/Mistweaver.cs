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
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Singular.Settings;


namespace Singular.ClassSpecific.Monk
{

    public class Mistweaver
    {
        private const int SOOTHING_MIST = 115175;
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        [Behavior(BehaviorType.Combat | BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite CreateMistweaverMonkCombatBehavior()
        {
            return new PrioritySelector(
                CreateMistweaverMonkHealing(false)
#if NOTYET
                ,
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateAutoAttack(true),

                    Common.CastLikeMonk("Blackout Kick", ret => Me.CurrentChi >= 2 && (!Me.HasAura("Serpent's Zeal", 2) || Me.GetAuraTimeLeft("Serpent's Zeal", true).TotalSeconds < 5)),
                    Common.CastLikeMonk("Tiger Palm", ret => Me.CurrentChi >= 1 && (!Me.HasAura("Vital Mists", 5) || Me.GetAuraTimeLeft("Vital Mists", true).TotalSeconds < 5)),
                    Common.CastLikeMonk("Expel Harm", ret => Me, ret => Me.HealthPercent < 90 && Me.CurrentChi < Me.MaxChi),
                    Common.CastLikeMonk("Jab", ret => Me.CurrentChi < Me.MaxChi),
                    new Decorator( ret => !Me.IsCasting,
                        new PrioritySelector(
                            Common.CastLikeMonk("Roll", ret => Me.CurrentTarget.Distance >= 10 && Me.CurrentTarget.Distance <= 25),
                            Movement.CreateMoveToMeleeBehavior(true)
                            )
                        )
                    )
#endif
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite CreateMistweaverMonkHealBehavior()
        {
            return CreateMistweaverMonkHealing(false);
        }

        [Behavior(BehaviorType.Rest, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite CreateMistweaverMonkRestBehavior()
        {
            return new PrioritySelector(
                CreateMistweaverMonkHealing(true),
                Rest.CreateDefaultRestBehaviour(),
                Spell.Resurrect("Resuscitate"),
                CreateMistweaverMonkHealing(false)
                );               
        }

        public static Composite CreateMistweaverMonkHealing(bool selfOnly)
        {
            HealerManager.NeedHealTargeting = true;

            return new PrioritySelector(

                ctx => ChooseBestMonkHealTarget(selfOnly ? Me : HealerManager.Instance.FirstUnit),
                
                // if channeling soothing mist and they aren't best target, cancel the cast
                new Decorator(ret => Me.ChanneledCastingSpellId == SOOTHING_MIST && Me.ChannelObject != null && Me.ChannelObject != (WoWObject)ret,
                    new Sequence(
                        new Action( ret => Logger.Write( System.Drawing.Color.OrangeRed, "/stopcasting: cancel {0} on {1} @ {2:F1}", Me.ChanneledSpell.Name, Me.ChannelObject.SafeName(), Me.ChannelObject.ToUnit().HealthPercent) ),
                        new Action( ret => SpellManager.StopCasting() ),
                        new WaitContinue(new TimeSpan(0, 0, 0, 0, 500), ret => Me.ChanneledCastingSpellId != SOOTHING_MIST, new ActionAlwaysSucceed())
                        )
                    ),

                new Decorator(ret => Me.IsCasting && (Me.ChanneledSpell == null || Me.ChanneledSpell.Id != SOOTHING_MIST), new Action(ret => { return RunStatus.Success; })),

                new Decorator(ret => ((WoWUnit)ret) != null && ((WoWUnit)ret).GetPredictedHealthPercent() <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth,
                
                    new PrioritySelector(

                        ShowHealTarget(ret => (WoWUnit)ret),

                        new Decorator(ret => !SpellManager.GlobalCooldown,

                            new Sequence( 

                                new PrioritySelector(
                                    Helpers.Common.CreateInterruptSpellCast(ret => Me.CurrentTarget),

                                    Common.CastLikeMonk("Fortifying Brew", ret => Me, ret => Me.HealthPercent < 40),

                                    Common.CastLikeMonk("Mana Tea", ret => Me, ret => Me.ManaPercent < 60 && Me.HasAura("Mana Tea", 2)),

                                    new PrioritySelector(
                                        Common.CastLikeMonk("Surging Mist", ret => (WoWUnit)ret,
                                            ret => ((WoWUnit)ret).HealthPercent < 30 && Me.HasAura("Vital Mists", 5)),

                                        Common.CastLikeMonk("Soothing Mist", ret => (WoWUnit)ret,
                                            ret => ((WoWUnit)ret).HealthPercent < 90 && Me.ChanneledSpell == null),

                                        Common.CastLikeMonk("Surging Mist", ret => (WoWUnit)ret,
                                            ret => ((WoWUnit)ret).HealthPercent < 60),

                                        Common.CastLikeMonk("Enveloping Mist", ret => (WoWUnit)ret,
                                            ret => ((WoWUnit)ret).HealthPercent < 89 && Me.CurrentChi >= 3)
                                        )
                                    )
                                )
                            ),

                        new Decorator(
                            ret => !Me.IsCasting,
                            Movement.CreateMoveToRangeAndStopBehavior(ret => (WoWUnit)ret, ret => 38f)
                            )
                        )
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
                            Logger.WriteDebug(Color.LightGreen, "Heal Target - none", ((WoWUnit)ret).SafeName(), ((WoWUnit)ret).HealthPercent, ((WoWUnit)ret).Distance);
                            return RunStatus.Failure;
                        })
                        ),

                    new DecoratorContinue(
                        ret => onUnit(ret) != null && guidLastHealTarget != onUnit(ret).Guid,
                        new Action(ret =>
                        {
                            guidLastHealTarget = ((WoWUnit)ret).Guid;
                            Logger.WriteDebug(Color.LightGreen, "Heal Target - {0} {1:F1}% @ {2:F1} yds", ((WoWUnit)ret).SafeName(), ((WoWUnit)ret).HealthPercent, ((WoWUnit)ret).Distance);
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