using System.Linq;
using System.Runtime.Remoting.Contexts;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;

using Styx.Helpers;
using System;
using Styx.WoWInternals;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;

using Singular.Helpers;
using Styx.Common;

namespace Singular.ClassSpecific.Warrior
{
    public class Protection
    {

        #region Common

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateFaceTargetBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Helpers.Common.CreateAutoAttack(false),

                new Decorator(ret => Me.Mounted, Helpers.Common.CreateDismount("Pulling")),

                //Shoot flying targets
                new Decorator(
                    ret => Me.CurrentTarget.IsFlying,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Spell.Cast("Heroic Throw"),
                        Spell.Cast("Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 27f)
                        )
                    ),


                //Buff up (or use to generate Rage)
                new Throttle( TimeSpan.FromSeconds(1), 
                    new PrioritySelector(

                        Spell.Cast("Battle Shout", 
                            ret => !Me.HasMyAura("Commanding Shout") 
                                && (!Me.HasPartyBuff(PartyBuffType.AttackPower) || Me.CurrentRage < 20)),

                        Spell.Cast("Commanding Shout", 
                            ret => !Me.HasMyAura("Battle Shout") 
                                && (!Me.HasPartyBuff(PartyBuffType.Stamina) || Me.CurrentRage < 20))
                        )
                    ),

                Common.CreateChargeBehavior(),

                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        #endregion

        #region Normal

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalPreCombatBuffs()
        {
            return new PrioritySelector(

                Spell.BuffSelf("Defensive Stance"),

                PartyBuff.BuffGroup( "Battle Shout", ret => WarriorSettings.Shout == WarriorShout.BattleShout ),

                PartyBuff.BuffGroup( "Commanding Shout", ret => WarriorSettings.Shout == WarriorShout.CommandingShout )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Throttle(    // throttle these because most are off the GCD
                    new Decorator( ret => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(
                            Spell.Cast("Demoralizing Shout", ret => Unit.NearbyUnfriendlyUnits.Any( m => m.Distance < (m.MeleeDistance() + 5))),
                            Spell.BuffSelf("Shield Wall", ret => Me.HealthPercent < WarriorSettings.WarriorShieldWallHealth),
                            Spell.BuffSelf("Shield Barrier", ret => Me.HealthPercent < WarriorSettings.WarriorShieldBarrierHealth),
                            Spell.BuffSelf("Shield Block", ret => Me.HealthPercent < WarriorSettings.WarriorShieldBlockHealth),
                            Spell.BuffSelf("Last Stand", ret => Me.HealthPercent < WarriorSettings.WarriorLastStandHealth),
                            Spell.BuffSelf("Enraged Regeneration",
                                ret => Me.HealthPercent < 10 || (Me.ActiveAuras.ContainsKey("Enrage") && Me.HealthPercent < WarriorSettings.WarriorEnragedRegenerationHealth)),

                            new Decorator(
                                ret => Me.GotTarget && (Me.CurrentTarget.IsBoss || Me.CurrentTarget.IsPlayer || (!Me.IsInGroup() && AoeCount >= 3)),
                                new PrioritySelector(
                                    Spell.Cast("Recklessness"),
                                    Spell.Cast("Skull Banner"),
                                    // Spell.Cast("Demoralizing Banner", ret => !Me.CurrentTarget.IsBoss && UseAOE),
                                    Spell.Cast("Avatar")
                                    )
                                ),

                            // cast above rage dump so we are sure have rage to do damage
                            Spell.Cast("Bloodbath"),
                            Spell.Cast("Berserker Rage"),
                            // new Action(ret => { UseTrinkets(); return RunStatus.Failure; }),
                            Spell.Cast("Deadly Calm", ret => TalentManager.HasGlyph("Incite") || Me.CurrentRage >= RageDump)
                            )
                        )
                    )
                );
        }

        static WoWUnit intTarget;

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalCombat()
        {
            return new PrioritySelector(
                ctx => TankManager.Instance.FirstUnit ?? Me.CurrentTarget,
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
        
                    new PrioritySelector(

                        CreateDiagnosticOutputBehavior(),

                        Spell.Cast("Impending Victory"),
                        Spell.Cast("Victory Rush", ret => Me.HasAura("Victorious")),

                        Spell.Cast("Execute", 
                            ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances 
                                && Me.CurrentRage > RageDump 
                                && Me.CurrentTarget.HealthPercent < 20),

                        new Decorator( 
                            ret => SingularSettings.Instance.EnableTaunting && Me.IsInInstance,
                            CreateTauntBehavior()
                            ),

                        Spell.Buff("Piercing Howl", ret => Me.CurrentTarget.Distance < 10 && Me.CurrentTarget.IsPlayer && !Me.CurrentTarget.HasAnyAura("Piercing Howl", "Hamstring") && SingularSettings.Instance.Warrior().UseWarriorSlows),
                        Spell.Buff("Hamstring", ret => Me.CurrentTarget.IsPlayer && !Me.CurrentTarget.HasAnyAura("Piercing Howl", "Hamstring") && SingularSettings.Instance.Warrior().UseWarriorSlows),

                        CreateProtectionInterrupt(),

                        // Handle Ultimatum procs 
                        // Handle Glyph of Incite procs
                        // Dump Rage
                        new Throttle(
                            new Decorator(
                                ret => HasUltimatum || Me.HasAura("Glyph of Incite") || Me.CurrentRage > RageDump,
                                new PrioritySelector(
                                    Spell.Cast("Cleave", ret => Me.IsInGroup() && UseAOE),
                                    Spell.Cast("Heroic Strike")
                                    )
                                )
                            ),

                        // Handle proccing Glyph of Incite buff
                        Spell.Cast( "Devastate", ret => TalentManager.HasGlyph("Incite") && Me.HasAura("Deadly Calm") && !Me.HasAura("Glyph of Incite")),

                        // Multi-target?  get the debuff on them
                        new Decorator(
                            ret => UseAOE,
                            new PrioritySelector(
                                Spell.Cast("Thunder Clap"),
                                Spell.Cast("Bladestorm", ret => AoeCount >= 4),
                                Spell.Cast("Shockwave", ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 3),
                                Spell.Cast("Dragon Roar", ret => Me.CurrentTarget.Distance <= 8 || Me.CurrentTarget.IsWithinMeleeRange)
                                )
                            ),

                        // Generate Rage
                        Spell.Cast("Shield Slam", ret => Me.CurrentRage < RageBuild ),
                        Spell.Cast("Revenge", ret => Me.CurrentRage < RageBuild ),
                        Spell.Cast("Devastate", ret => !((WoWUnit)ret).HasAura("Weakened Armor", 3) && Unit.NearbyGroupMembers.Any(m => m.Class == WoWClass.Druid)),
                        Spell.Cast("Thunder Clap", ret => ((WoWUnit)ret).Distance < 8f && !((WoWUnit)ret).ActiveAuras.ContainsKey("Weakened Blows")),

                        // Filler
                        Spell.Cast("Devastate"),

                        //Charge
                        Common.CreateChargeBehavior(),

                        new Action( ret => {
                            if ( Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange && Me.IsSafelyFacing(Me.CurrentTarget))
                                Logger.WriteDebug("--- we did nothing!");
                            return RunStatus.Failure;
                            })
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        static Composite CreateTauntBehavior()
        {
            // limit all taunt attempts to 1 per second max since Mocking Banner and Taunt have no GCD
            // .. it will keep us from casting both for the same mob we lost aggro on
            return new Throttle( 1, 1,
                new PrioritySelector(
                    Spell.CastOnGround("Mocking Banner",
                        ret => (TankManager.Instance.NeedToTaunt.FirstOrDefault() ?? Me).Location,
                        ret => TankManager.Instance.NeedToTaunt.Any() && Clusters.GetCluster(TankManager.Instance.NeedToTaunt.FirstOrDefault(), TankManager.Instance.NeedToTaunt, ClusterType.Radius, 15f).Count() >= 2),

                    Spell.Cast("Taunt", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault()),

                    Spell.Cast("Storm Bolt", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(i => i.Distance < 30 && Me.IsSafelyFacing(i))),

                    Spell.Cast("Intervene", 
                        ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(
                            m => Group.Healers.Any( h => m.CurrentTargetGuid == h.Guid && h.Distance < 25)),
                        ret => MovementManager.IsClassMovementAllowed && Group.Healers.Count( h => h.IsAlive && h.Distance < 40) == 1
                        )
                    )
                );
        }

        static Composite CreateProtectionInterrupt()
        {
            return new Throttle(
                new PrioritySelector(
                    new Action(ret =>
                    {
                        intTarget = Unit.NearbyUnfriendlyUnits.FirstOrDefault(i => i.IsCasting && i.CanInterruptCurrentSpellCast && i.IsWithinMeleeRange && Me.IsSafelyFacing(i));
                        return RunStatus.Failure;
                    }),

                    Spell.Cast("Pummel", ctx => intTarget),

                    new Action(ret =>
                    {
                        intTarget = Unit.NearbyUnfriendlyUnits.FirstOrDefault(i => i.IsCasting && i.CanInterruptCurrentSpellCast && i.Distance < 10);
                        return RunStatus.Failure;
                    }),

                    Spell.Cast("Disrupting Shout", ctx => intTarget),

                    new Action(ret =>
                    {
                        intTarget = Unit.NearbyUnfriendlyUnits.FirstOrDefault(i => i.IsCasting && i.CanInterruptCurrentSpellCast && i.Distance < 30 && Me.IsSafelyFacing(i));
                        return RunStatus.Failure;
                    }),

                    Spell.Cast("Storm Bolt", ctx => intTarget)
                    )
                );
        }

        static bool UseAOE
        {            get
            {
                if (Me.GotTarget && Me.CurrentTarget.IsPlayer)
                    return false;

                return AoeCount >= 2 && Spell.UseAOE;
            }
        }

        static int AoeCount
        {
            get
            {
                return Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 8f);
            }
        }

        static int RageBuild
        {
            get
            {
                return (int)Me.MaxRage - 5;
            }
        }

        static int RageDump
        {
            get
            {
                return (int)Me.MaxRage - 20;
            }
        }

        static bool HasUltimatum
        {
            get
            {
                return Me.ActiveAuras.ContainsKey("Ultimatum");
            }
        }

        private static Composite CreateDiagnosticOutputBehavior()
        {
            return new ThrottlePasses( 1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                        {
                        Logger.WriteDebug(".... h={0:F1}%/r={1:F1}%, Ultim={2}, Targ={3} {4:F1}% @ {5:F1} yds Melee={6} Facing={7}",
                            Me.HealthPercent,
                            Me.CurrentRage,
                            HasUltimatum,
                            !Me.GotTarget ? "(null)" : Me.CurrentTarget.Name,
                            !Me.GotTarget ? 0 : Me.CurrentTarget.HealthPercent,
                            !Me.GotTarget ? 0 : Me.CurrentTarget.Distance,
                            !Me.GotTarget ? false : Me.CurrentTarget.IsWithinMeleeRange ,
                            !Me.GotTarget ? false : Me.IsSafelyFacing( Me.CurrentTarget  )
                            );
                        return RunStatus.Failure;
                        })
                    )
                );
        }

        #endregion
    }
}