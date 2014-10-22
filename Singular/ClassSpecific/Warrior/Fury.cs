using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.TreeSharp;
using System;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using System.Drawing;

namespace Singular.ClassSpecific.Warrior
{
    public class Fury
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }
        private static bool HasTalent(WarriorTalents tal) { return TalentManager.IsSelected((int)tal); }

        private static string[] _slows;

        #region Normal
        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CreateFuryNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateDiagnosticOutputBehavior("Pull"),

                        //Buff up
                        Spell.BuffSelf(Common.SelectedShout),

                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Common.CreateChargeBehavior(),

                        Spell.Cast("Bloodthirst")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CreateFuryNormalCombatBuffs()
        {
            return new Throttle(
                new Decorator(
                    ret => Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange && !Unit.IsTrivial(Me.CurrentTarget),

                    new PrioritySelector(
                        Common.CreateWarriorEnragedRegeneration(),

                        Common.CreateDieByTheSwordBehavior(),

                        new Decorator(
                            ret => SingularRoutine.CurrentWoWContext == WoWContext.Normal
                                && (Me.CurrentTarget.IsPlayer || 4 <= Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < (u.MeleeDistance() + 1)) || Me.CurrentTarget.TimeToDeath() > 40),
                            new PrioritySelector(
                                Spell.CastOnGround("Demoralizing Banner", on => Me.CurrentTarget, req => true, false),
                                Spell.Cast("Skull Banner"),
                                Spell.Cast("Avatar"),
                                Spell.Cast("Bloodbath")
                                )
                            ),

                        Spell.BuffSelf("Rallying Cry", req => !Me.IsInGroup() && Me.HealthPercent < 50),

                        Spell.Cast("Recklessness", ret => (Spell.CanCastHack("Execute") || Common.Tier14FourPieceBonus) && (StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances)),

                        new Decorator(
                            ret => Me.CurrentTarget.IsBoss(),
                            new PrioritySelector(
                                Spell.Cast("Skull Banner", ret => Me.CurrentTarget.IsBoss()),
                                Spell.Cast("Avatar", ret => Me.CurrentTarget.IsBoss()),
                                Spell.Cast("Bloodbath", ret => Me.CurrentTarget.IsBoss())
                                )
                            ),

                        Spell.Cast("Storm Bolt"),  // in normal rotation

                        Spell.Cast("Berserker Rage", ret => {
                            if (Me.CurrentTarget.HealthPercent <= 20)
                                return true;
                            if (!Me.ActiveAuras.ContainsKey("Enrage") && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds > 6)
                                return true;
                            return false;
                            }),

                        Spell.BuffSelf(Common.SelectedShout)

                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CreateFuryNormalCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom", "Infected Wounds" };
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(FaceDuring.Yes),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && !StyxWoW.Me.HasAura("Bladestorm"),
                    new PrioritySelector(

                        CreateDiagnosticOutputBehavior("Combat"),

                        // special "in combat" pull logic for mobs not tagged and out of melee range
                        Common.CreateWarriorCombatPullMore(),

                        // Dispel Bubbles
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.IsPlayer && (StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")),
                            new PrioritySelector(
                                Spell.WaitForCast(),
                                Movement.CreateEnsureMovementStoppedBehavior( 30, on => StyxWoW.Me.CurrentTarget, reason:"for shattering throw"),
                                Spell.Cast("Shattering Throw"),
                                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 30f, 25f)
                                )
                            ),

                        //Heroic Leap
                        Spell.CastOnGround("Heroic Leap",
                            on => StyxWoW.Me.CurrentTarget, 
                            ret => WarriorSettings.UseWarriorCloser && MovementManager.IsClassMovementAllowed && StyxWoW.Me.CurrentTarget.Distance > 9 && PreventDoubleIntercept, 
                            false),

                        // ranged slow
                        Spell.Buff("Piercing Howl", 
                            ret => StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior().UseWarriorSlows),

                        // melee slow
                        Spell.Buff("Hamstring", 
                            ret => StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior().UseWarriorSlows),

                        //Interupts
                        Helpers.Common.CreateInterruptBehavior(),

                        // Heal up in melee
                        Common.CreateVictoryRushBehavior(),

                        // Disarm if setting enabled
                        Common.CreateDisarmBehavior(),

                        // AOE 
                        // -- check melee dist+3 rather than 8 so works for large hitboxes (8 is range of DR and WW)

                        new Decorator(  // Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3,
                            ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count( u => u.SpellDistance() < 8 ) >= 3,                       
                            new PrioritySelector(
                                Spell.BuffSelf("Bladestorm"),
                                Spell.Cast("Shockwave"),
                                Spell.Cast("Dragon Roar"),

                                // do some AOE prior to learning BT
                                Spell.Cast("Thunder Clap", req => !SpellManager.HasSpell("Whirlwind")),

                                //Bloodthirst on cooldown when not Enraged. Procs Bloodsurge.
                                Spell.Cast("Bloodthirst", ret => !IsEnraged),

                                //Whirlwind as a Rage dump and to build Raging Blow stacks.
                                Spell.Cast("Whirlwind", ret => Me.RagePercent > 80),

                                //Raging Blow with Raging Blow stacks.
                                Spell.Cast("Raging Blow", ret => Me.HasAura("Raging Blow", 1)),

                                //Wild Strike to consume Bloodsurge procs.
                                Spell.Cast("Wild Strike", ret => IsEnraged || StyxWoW.Me.HasAura("Bloodsurge"))
                                )
                            ),

                        // Use the single target rotation!
                        SingleTarget(),

                        // Charge if we can
                        Common.CreateChargeBehavior()
                        )
                    ),

                //Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }


        private static Composite SingleTarget()
        {
            return new PrioritySelector(

                new Decorator(
                    req => Me.CurrentTarget.HealthPercent > 20,
                    new PrioritySelector(

                        //Wild Strike to prevent capping your Rage.
                        Spell.Cast("Wild Strike", ret => Me.RagePercent > 80),

                        //Bloodthirst on cooldown when not Enraged. Procs Bloodsurge.
                        Spell.Cast("Bloodthirst", ret => !IsEnraged),

                        //Raging Blow when available.
                        Spell.Cast("Raging Blow"),

                        //Wild Strike when Enraged or with Bloodsurge procs.
                        Spell.Cast("Wild Strike", ret => IsEnraged || StyxWoW.Me.HasAura("Bloodsurge"))
                        )
                    ),

                new Decorator(
                    req => Me.CurrentTarget.HealthPercent <= 20,
                    new PrioritySelector(

                        //Execute to prevent capping your Rage.
                        Spell.Cast( "Execute", req => Me.RagePercent > 80),

                        //Bloodthirst on cooldown when not Enraged. Procs Bloodsurge.
                        Spell.Cast("Bloodthirst", ret => !IsEnraged),

                        //Execute while Enraged or with >= 60 Rage.
                        Spell.Cast("Execute", req => Me.RagePercent >= 60),

                        //Wild Strike when Enraged or with Bloodsurge procs.
                        Spell.Cast("Wild Strike", ret => IsEnraged || StyxWoW.Me.HasAura("Bloodsurge"))
                        )
                    ),

                // BR whenever we're not enraged, and can actually melee the target.
                // Use abilities that cost no rage, such as your tier 4 talents, etc
                new Decorator(
                    ret => Spell.UseAOE && Me.GotTarget && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss()) && Me.CurrentTarget.Distance < 8,
                    new PrioritySelector(
                        Spell.Cast("Dragon Roar"),
                        Spell.Cast("Storm Bolt"),
                        Spell.BuffSelf("Bladestorm"),
                        Spell.Cast("Shockwave")
                        )
                    ),

                Spell.BuffSelf("Berserker Rage", ret => !IsEnraged && StyxWoW.Me.CurrentTarget.IsWithinMeleeRange)
                );
        }

        #endregion


        #region Utils
        private static readonly WaitTimer InterceptTimer = new WaitTimer(TimeSpan.FromMilliseconds(2000));

        private static bool PreventDoubleIntercept
        {
            get
            {
                var tmp = InterceptTimer.IsFinished;
                if (tmp)
                    InterceptTimer.Reset();
                return tmp;
            }
        }


        #endregion

        #region Calculations - These are for the super-high DPS rotations for raiding as SMF. (TG isn't quite as good as SMF anymore!)

        static TimeSpan BTCD { get { return Spell.GetSpellCooldown("Bloodthirst"); } }
        static TimeSpan CSCD { get { return Spell.GetSpellCooldown("Colossus Smash"); } }

        static bool WithinExecuteRange { get { return StyxWoW.Me.CurrentTarget.HealthPercent <= 20; } }
        static bool IsEnraged { get { return StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Enraged); } }
        private static bool TargetSmashed { get { return StyxWoW.Me.CurrentTarget.HasAura("Colossus Smash"); } }


        static bool NeedHeroicStrike
        {
            get
            {
                if (StyxWoW.Me.CurrentTarget.HealthPercent >= 20)
                {
                    // Go based off % since we have the new glyph to add 20% rage.
                    var myRage = StyxWoW.Me.RagePercent;

                    // Basically, here's how this works.
                    // If the target is CS'd, and we have > 40 rage, then pop HS.
                    // If we *ever* have more than 90% rage, then pop HS
                    // If we popped DC and have more than 30 rage, pop HS (it's more DPR than basically anything else at 15 rage cost)
                    if (myRage >= 40 && TargetSmashed)
                        return true;
                    if (myRage >= 90)
                        return true;
//                    if (myRage >= 30 && StyxWoW.Me.HasAura("Deadly Calm"))
//                        return true;
                }
                return false;
            }
        }

        #endregion

        #region Diagnostics 

        private static Composite CreateDiagnosticOutputBehavior(string context = null)
        {
            if (context == null)
                context = "...";
            else
                context = "<<" + context + ">>";

            return new Decorator(
                ret => SingularSettings.Debug,
                new ThrottlePasses(1,
                    new Action(ret =>
                    {
                        string log;
                        log = string.Format(context + " h={0:F1}%/r={1:F1}%, stance={2}, Enrage={3} Coloss={4} MortStrk={5}, RagingBlow={6}",
                            Me.HealthPercent,
                            Me.CurrentRage,
                            Me.Shapeshift,
                            Me.ActiveAuras.ContainsKey("Enrage"),
                            (int)Spell.GetSpellCooldown("Colossus Smash", -1).TotalMilliseconds,
                            (int)Spell.GetSpellCooldown("Mortal Strike", -1).TotalMilliseconds,
                            Me.GetAuraStacks("Raging Blow")
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            log += string.Format(", th={0:F1}%, dist={1:F1}, inmelee={2}, face={3}, loss={4}, dead={5} secs",
                                target.HealthPercent,
                                target.Distance,
                                target.IsWithinMeleeRange.ToYN(),
                                Me.IsSafelyFacing(target).ToYN(),
                                target.InLineOfSpellSight.ToYN(),
                                target.TimeToDeath()
                                );
                        }

                        Logger.WriteDebug(Color.AntiqueWhite, log);
                        return RunStatus.Failure;
                    })
                    )
                );
        }

#endregion
    }
}
