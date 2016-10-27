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
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Drawing;
using System;
using Styx.Common;

namespace Singular.ClassSpecific.Paladin
{
    public class Protection
    {

        #region Properties & Fields

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PaladinSettings PaladinSettings { get { return SingularSettings.Instance.Paladin(); } }
        private const int ShieldOfTheRighteous = 132403;
        private static int _aoeCount;

        #endregion

        [Behavior(BehaviorType.Rest, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreateProtectionRest()
        {
            return new PrioritySelector(
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour( "Light of the Protector", "Redemption")
                );
        }


        [Behavior(BehaviorType.Pull, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreatePaladinProtectionPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.Cast("Divine Steed", req => PaladinSettings.UseDivineSteed && Me.CurrentTarget.Distance > 15d),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Spell.Cast("Judgment"),
                        Spell.Cast("Avenger's Shield", ret => Spell.UseAOE),
                        Spell.Cast("Reckoning", ret => !Me.CurrentTarget.IsPlayer)
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreatePaladinProtectionCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Devotion Aura", req => Me.Silenced),

                Spell.Cast("Divine Steed", req => PaladinSettings.UseDivineSteed && Me.CurrentTarget.Distance > 15d),

                new Decorator(
                    req => !Unit.IsTrivial(Me.CurrentTarget),
                    new PrioritySelector(

                        // Defensive
                        Spell.Cast("Eye of Tyr", ret => PaladinSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None && Me.HealthPercent <= PaladinSettings.ArtifactHealthPercent),

                        Spell.BuffSelf("Hand of Freedom",
                            ret => Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                                    WoWSpellMechanic.Disoriented,
                                                                    WoWSpellMechanic.Frozen,
                                                                    WoWSpellMechanic.Incapacitated,
                                                                    WoWSpellMechanic.Rooted,
                                                                    WoWSpellMechanic.Slowed,
                                                                    WoWSpellMechanic.Snared)),

                        Spell.BuffSelf("Sacred Shield"),

                        Spell.BuffSelf("Divine Shield",
                            ret => Me.CurrentMap.IsBattleground && Me.HealthPercent <= 20 && !Me.HasAura("Forbearance")),

                        Spell.BuffSelf(
                            "Guardian of Ancient Kings",
                            ret => Me.GotTarget()
                                && Me.CurrentTarget.IsWithinMeleeRange
                                && (Me.HealthPercent <= PaladinSettings.GoAKHealth || _aoeCount > 3)
                            ),

                        Spell.BuffSelf(
                            "Ardent Defender",
                            ret => Me.HealthPercent <= PaladinSettings.ArdentDefenderHealth)
                        )
                    ),

                // Heal up after Defensive CDs used if needed
                Spell.BuffSelf( "Lay on Hands",
                    ret => Me.HealthPercent <= PaladinSettings.SelfLayOnHandsHealth && !Me.HasAura("Forbearance")),

                Spell.Cast("Flash of Light",
                    mov => false,
                    on => Me,
                    req => SingularRoutine.CurrentWoWContext != WoWContext.Instances && Me.PredictedHealthPercent(includeMyHeals: true) <= PaladinSettings.SelfFlashOfLightHealth,
                    cancel => Me.HealthPercent > 90),

                // now any Offensive CDs
                new Decorator(
                    req => !Unit.IsTrivial(Me.CurrentTarget),
                    new PrioritySelector(

                        Spell.BuffSelf("Avenging Wrath",
                            ret => Me.GotTarget()
                                && Me.CurrentTarget.IsWithinMeleeRange
                                && (Me.CurrentTarget.TimeToDeath() > 25 || _aoeCount > 1)
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreateProtectionCombat()
        {
            TankManager.NeedTankTargeting = (SingularRoutine.CurrentWoWContext == WoWContext.Instances);

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        new Action(r =>
                        {
                            // Paladin AOE count should be those near paladin (consecrate, holy wrath) and those near target (avenger's shield)
                            _aoeCount =
                                TankManager.Instance.TargetList.Count(
                                    u => u.SpellDistance() < 10 || u.Location.Distance(Me.CurrentTarget.Location) < 10);
                            return RunStatus.Failure;
                            }),

                        CreateProtDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreatePaladinPullMore(),

                        Common.CreatePaladinBlindingLightBehavior(),

                        // Taunts - if reckoning on cooldown, throw some damage at them
                        new Decorator(
                            ret => SingularSettings.Instance.EnableTaunting
                                && TankManager.Instance.NeedToTaunt.Any()
                                && TankManager.Instance.NeedToTaunt.FirstOrDefault().InLineOfSpellSight,
                            new Throttle(TimeSpan.FromMilliseconds(1500),
                                new PrioritySelector(
                                    ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(e => e.SpellDistance() < 30),
                                    Spell.Cast("Reckoning", ctx => (WoWUnit)ctx),
                                    Spell.Cast("Avenger's Shield", ctx => (WoWUnit) ctx, req => Spell.UseAOE),
                                    Spell.Cast("Judgment", ctx => (WoWUnit)ctx)
                                    )
                                )
                            ),

                        // Soloing move - open with stun to reduce incoming damage (best to take Fist of Justice talent if doing this
                        Spell.Cast("Hammer of Justice",
                            ret =>
                                PaladinSettings.StunMobsWhileSolo &&
                                SingularRoutine.CurrentWoWContext == WoWContext.Normal),

                        //Multi target
                        new Decorator(
                            ret => _aoeCount >= 4 && Spell.UseAOE,
                            new PrioritySelector(
                                Spell.Cast("Avenger's Shield"),
                                Spell.Cast("Consecration", req => Unit.UnfriendlyUnits(8).Any()),
                                Spell.Cast("Blessed Hammer"),
                                Spell.Cast("Judgment"),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        Spell.Cast("Blessed Hammer"),

                        //Single target
                            // The buff below gives us a 20% damage reduction if we have KnightTemplar talent.
                            // However, something is removing the buff as soon as its cast as it believes the player is using a mount.
                            // Spell.BuffSelf("Divine Steed", req => Common.HasTalent(PaladinTalents.KnightTemplar)),
                        Spell.HandleOffGCD(Spell.Cast("Shield of the Righteous", req => !Me.HasAura(ShieldOfTheRighteous))),
                        Spell.HandleOffGCD(Spell.Cast("Light of the Protector", req => Me.HealthPercent <= 85)),
                        Spell.Cast("Consecration", req => Unit.UnfriendlyUnits(8).Any()),
                        Spell.Cast("Bastion of Light", req => Spell.GetCharges("Shield of the Righteous") == 0 && !Me.HasAura(ShieldOfTheRighteous) && Me.HealthPercent <= 80),
                        Spell.Cast("Judgment"),
                        Spell.Cast("Hammer of the Righteous"),
                        Spell.Cast("Avenger's Shield", ret => Spell.UseAOE)
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true));
        }

        private static Composite CreateProtDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new Action(ret => { return RunStatus.Failure; });

            return new Sequence(
                new ThrottlePasses(1, 1,
                    new Action(ret =>
                    {
                        string sMsg;
                        sMsg = string.Format(".... h={0:F1}%, m={1:F1}%, moving={2}, hpower={3}, grandcru={4}, divpurp={5}, sacshld={6}, mobs={7}",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.IsMoving,
                            Me.CurrentHolyPower,
                            (long)Me.GetAuraTimeLeft("Grand Crusader").TotalMilliseconds,
                            (long)Me.GetAuraTimeLeft("Divine Purpose").TotalMilliseconds,
                            (long)Me.GetAuraTimeLeft("Sacred Shield").TotalMilliseconds,

                            _aoeCount
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            sMsg += string.Format(
                                ", {0}, {1:F1}%, {2:F1} yds, threat={3}% loss={4}",
                                target.SafeName(),
                                target.HealthPercent,
                                target.Distance,
                                (int) target.ThreatInfo.RawPercent,
                                target.InLineOfSpellSight
                                );
                        }

                        Logger.WriteDebug(Color.LightYellow, sMsg);
                        return RunStatus.Failure;
                    })
                    )
                );
        }
    }
}
