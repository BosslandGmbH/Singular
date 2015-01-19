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
using Styx.Common;
using System.Drawing;
using CommonBehaviors.Actions;
using Styx.Pathing;

namespace Singular.ClassSpecific.Warrior
{
    /// <summary>
    /// plaguerized from Apoc's simple Arms Warrior CC 
    /// see http://www.thebuddyforum.com/honorbuddy-forum/combat-routines/warrior/79699-arms-armed-quick-dirty-simple-fast.html#post815973
    /// </summary>
    public class Arms
    {

        #region Common

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }
        private static bool HasTalent(WarriorTalents tal) { return TalentManager.IsSelected((int)tal); }


        [Behavior(BehaviorType.Rest, WoWClass.Warrior, WoWSpec.WarriorArms)]
        public static Composite CreateArmsRest()
        {
            return new PrioritySelector(

                Common.CheckIfWeShouldCancelBladestorm(),

                Singular.Helpers.Rest.CreateDefaultRestBehaviour(),

                CheckThatWeaponIsEquipped()
                );
        }


        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.All)]
        public static Composite CreateArmsNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateDiagnosticOutputBehavior("Pull"),

                        new Throttle( 2, Spell.BuffSelf(Common.SelectedShoutAsSpellName)),

                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Common.CreateChargeBehavior(),

                        Spell.Cast("Mortal Strike")
                        )
                    )
                );
        }

        #endregion

        #region Normal

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Normal)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Instances)]
        public static Composite CreateArmsCombatBuffsNormal()
        {
            return new Throttle(
                new Decorator(
                    ret => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange && !Unit.IsTrivial(Me.CurrentTarget),

                    new PrioritySelector(

                        Common.CreateWarriorEnragedRegeneration(),

                        Common.CreateDieByTheSwordBehavior(),

                        new Decorator(
                            ret => {
                                if (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss())
                                    return true;

                                if ( SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                                    return false;

                                if (Me.CurrentTarget.TimeToDeath() > 40)
                                    return true;

                                return Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= 4;
                                },
                            new PrioritySelector(
                                Spell.HandleOffGCD(Spell.BuffSelf("Avatar", req => true, 0, HasGcd.No)),
                                Spell.HandleOffGCD(Spell.BuffSelf("Bloodbath", req => true, 0, HasGcd.No))
                                )
                            ),

                        Spell.HandleOffGCD( Spell.BuffSelf("Rallying Cry", req => !Me.IsInGroup() && Me.HealthPercent < 50, 0, HasGcd.No)),

                        Spell.HandleOffGCD(Spell.BuffSelf("Recklessness", ret => (Spell.CanCastHack("Execute") || Common.Tier14FourPieceBonus || PartyBuff.WeHaveBloodlust) && (StyxWoW.Me.CurrentTarget.TimeToDeath() > 40 || StyxWoW.Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances), 0, HasGcd.No)),

                        Spell.Cast("Storm Bolt"),  // in normal rotation

                        // Execute is up, so don't care just cast
                        Spell.HandleOffGCD(
                            Spell.BuffSelf(
                                "Berserker Rage", 
                                req => 
                                {
                                    if (Me.CurrentTarget.HealthPercent <= 20)
                                        return true;
                                    if (!Common.IsEnraged && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds > 6)
                                        return true;
                                    return false;
                                },
                                0,
                                HasGcd.No
                                )
                            ),


                        Spell.BuffSelf(Common.SelectedShoutAsSpellName)

                        )
                    )
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Normal)]
        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Instances )]
        public static Composite CreateArmsCombatNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(FaceDuring.Yes),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        CreateDiagnosticOutputBehavior("Combat"),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateVictoryRushBehavior(),

                        // special "in combat" pull logic for mobs not tagged and out of melee range
                        Common.CreateWarriorCombatPullMore(),

                        Common.CreateExecuteOnSuddenDeath(),

                        new Throttle(
                            new Decorator(
                                ret => Me.HasAura("Glyph of Cleave"),
                                Spell.Cast("Heroic Strike")
                                )
                            ),

                        new Sequence(
                            new Decorator(
                                req => Common.IsSlowNeeded(Me.CurrentTarget),
                                new PrioritySelector(
                                    Spell.Buff("Hamstring")
                                    )
                                ),
                            new Wait(TimeSpan.FromMilliseconds(500), until => !Common.IsSlowNeeded(Me.CurrentTarget), new ActionAlwaysSucceed())
                            ),

                        CreateArmsAoeCombat(ret => Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() < (Common.DistanceWindAndThunder(8)))),

                        // Noxxic
                        //----------------
                        new Decorator(
                            ret => Me.GotTarget(), // WarriorSettings.ArmsSpellPriority == WarriorSettings.SpellPriority.Noxxic,
                            new PrioritySelector(

                                new Decorator(
                                    ret => Spell.UseAOE && Me.GotTarget() && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss()) && Me.CurrentTarget.SpellDistance() < 8,
                                    new PrioritySelector(
                                        Spell.Cast("Storm Bolt"),
                                        Spell.BuffSelf("Bladestorm"),
                                        Spell.Cast("Shockwave"),
                                        Spell.Cast("Dragon Roar")
                                        )
                                    ),

                                new Decorator(
                                    req => !Me.CurrentTarget.HasAura("Colossus Smash"),
                                    new PrioritySelector(
                                        // 1 Rend maintained at all times. Refresh with < 5 sec remaining.
                                        Spell.Cast( "Rend", req => Me.CurrentTarget.HasAuraExpired("Rend", 4)),

                                        // 2 Execute with >= 60 Rage and target is below 20% health.
                                        Spell.Cast( "Execute", req => Me.CurrentRage > 60 && Me.CurrentTarget.HealthPercent <= 20),

                                        // 3 Mortal Strike on cooldown when target is above 20% health.
                                        Spell.Cast( "Mortal Strike", req => Me.CurrentTarget.HealthPercent > 20),

                                        // 4 Colossus Smash as often as possible.
                                        Spell.Cast( "Colossus Smash"),

                                        // 5 Whirlwind as a filler ability when target is above 20% health.
                                        Spell.Cast("Whirlwind", req => Me.CurrentTarget.HealthPercent > 20 && Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8)),

                                        // Done here
                                        new ActionAlwaysFail()
                                        )
                                    ),

                                new Decorator(
                                    req => Me.CurrentTarget.HasAura("Colossus Smash"),
                                    new PrioritySelector(
                                        // 1 Execute on cooldown when target is below 20% health.
                                        Spell.Cast( "Execute", req => Me.CurrentTarget.HealthPercent <= 20),

                                        // 2 Mortal Strike on cooldown when target is above 20% health.
                                        Spell.Cast( "Mortal Strike", req => Me.HealthPercent > 20),

                                        // 3 Whirlwind as a filler ability when target is above 20% health.
                                        Spell.Cast("Whirlwind", req => Me.CurrentTarget.HealthPercent > 20 && Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8)),

                                        // Done here
                                        new ActionAlwaysFail()
                                        )
                                    ),

                                Spell.Cast("Dragon Roar", req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8),
                                Spell.Cast("Storm Bolt"),

                                // if we are low-level with low rage regen, do any damage we can
                                new Decorator(
                                    req => !SpellManager.HasSpell("Whirlwind"),
                                    new PrioritySelector(
                                        Spell.Cast("Rend"),
                                        Spell.Cast("Thunder Clap", req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8)
                                        )
                                    )
                                )
                            ),

                        Common.CreateChargeBehavior()
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Composite CreateArmsAoeCombat(SimpleIntDelegate aoeCount)
        {
            return new PrioritySelector(
                Spell.HandleOffGCD( Spell.BuffSelf("Sweeping Strikes", ret => aoeCount(ret) >= 2, 0, HasGcd.No) ),
                new Decorator(ret => Spell.UseAOE && aoeCount(ret) >= 3,
                    new PrioritySelector(
                        Spell.Cast( "Thunder Clap" ),

                        Spell.Cast("Bladestorm", ret => aoeCount(ret) >= 4),
                        Spell.Cast("Shockwave", ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 3),
                        Spell.Cast("Dragon Roar"),

                        Spell.Cast("Whirlwind"),
                        Spell.Cast("Mortal Strike"),
                        Spell.Cast("Colossus Smash", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Colossus Smash")),
                        Spell.Cast("Overpower")
                        )
                    )
                );
        }


        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Battlegrounds)]
        public static Composite CreateArmsCombatBuffsBattlegrounds()
        {
            return new Throttle(
                new Decorator(
                    ret => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange,

                    new PrioritySelector(
                        Spell.BuffSelf(Common.SelectedShoutAsSpellName),

                        Common.CreateDieByTheSwordBehavior(),

                        Spell.BuffSelf("Rallying Cry", req => Me.HealthPercent < 60),

                        new Decorator(
                            ret => Me.CurrentTarget.IsWithinMeleeRange && Me.CurrentTarget.IsCrowdControlled(),
                            new PrioritySelector(
                                Spell.HandleOffGCD( Spell.BuffSelf("Avatar", req => true, 0, HasGcd.No)),
                                Spell.HandleOffGCD(Spell.BuffSelf("Bloodbath", req => true, 0, HasGcd.No)),
                                Spell.HandleOffGCD(Spell.BuffSelf("Recklessness", req => true, 0, HasGcd.No))
                                )
                            ),

                        // Execute is up, so don't care just cast
                        // try to avoid overlapping Enrages
                        Spell.HandleOffGCD(
                            Spell.BuffSelf("Berserker Rage", 
                                req => !Common.IsEnraged
                                    && Spell.GetSpellCooldown("Mortal Strike").TotalSeconds > 4
                                    && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds > 6,
                                0,
                                HasGcd.No
                                )
                            ),

                        Spell.Cast( "Colossus Smash", req => !Me.CurrentTarget.HasAura("Colossus Smash")),
                        Spell.Cast( "Rend", req => Me.CurrentTarget.HasAuraExpired("Rend", 4)),
                        Spell.Cast( "Execute", req => Me.CurrentRage > 60 && Me.CurrentTarget.HealthPercent <= 20),
                        Spell.Cast( "Mortal Strike"),
                        Spell.Cast( "Slam"),
                        Spell.Cast("Whirlwind", req => Me.CurrentTarget.SpellDistance() < Common.DistanceWindAndThunder(8)),

                        new Decorator(
                            req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8,
                            new PrioritySelector(
                                Spell.Cast("Dragon Roar", req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8),
                                Spell.Cast("Storm Bolt"),
                                Spell.BuffSelf("Bladestorm"),
                                Spell.Cast("Shockwave")
                                )
                            ),

                        // if we are low-level with low rage regen, do any damage we can
                        new Decorator(
                            req => !SpellManager.HasSpell("Whirlwind"),
                            new PrioritySelector(
                                Spell.Cast("Rend"),
                                Spell.Cast("Thunder Clap", req => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8)
                                )
                            )
                        )
                    )
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Battlegrounds)]
        public static Composite CreateArmsCombatBattlegrounds()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && !StyxWoW.Me.HasAura("Bladestorm"),

                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        CreateDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Spell.Cast("Shattering Throw",
                            ret => Me.CurrentTarget.IsPlayer
                                && Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection")),

                        Common.CreateVictoryRushBehavior(),
                      
            #region Stun

                // charge them now
                        Common.CreateChargeBehavior(),

                        // another stun on them if possible
                        new Decorator(
                            ret => !Me.CurrentTarget.Stunned && !Me.HasAura("Charge"),
                            new PrioritySelector(
                                Spell.Cast("Shockwave", req => Me.CurrentTarget.SpellDistance() < 10 && Me.IsSafelyFacing(Me.CurrentTarget, 90f)),
                                Spell.Cast("Storm Bolt", req => Spell.IsSpellOnCooldown("Shockwave") || Me.CurrentTarget.SpellDistance() > 10)
                                )
                            ),

            #endregion

            #region Slow

                // slow them down
                        new Sequence(
                            new Decorator(
                                ret => Common.IsSlowNeeded(Me.CurrentTarget),
                                new PrioritySelector(
                                    Spell.Buff("Hamstring")
                                    )
                                ),  
                            new Wait( TimeSpan.FromMilliseconds(500), until => !Common.IsSlowNeeded(Me.CurrentTarget), new ActionAlwaysSucceed())
                            ),

            #endregion

            #region Damage

                         Common.CreateExecuteOnSuddenDeath(),

                // see if we can get debuff on them
                        Spell.Cast("Colossus Smash", ret => Me.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModDecreaseSpeed) && Me.CurrentTarget.GetAuraTimeLeft("Colossus Smash").TotalMilliseconds < 1500),

                        Spell.Cast("Heroic Strike", req => Me.RagePercent > 85),
                        Spell.Cast("Mortal Strike"),
                        Spell.Cast("Overpower"),
                        Spell.Cast("Slam", req => Me.RagePercent > 65 && Me.CurrentTarget.HasAura("Colossus Smash")),

                        Spell.Cast("Thunder Clap",
                            req => {
                                if (Me.CurrentTarget.SpellDistance() <= Common.DistanceWindAndThunder(8))
                                {
                                    // cast only if out of melee or behind us
                                    if (!Me.CurrentTarget.IsWithinMeleeRange || !Me.IsSafelyFacing(Me.CurrentTarget))
                                        return true;
                                }

                                return false;
                            })

            #endregion

                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }


        private static void UseTrinkets()
        {
            var firstTrinket = StyxWoW.Me.Inventory.Equipped.Trinket1;
            var secondTrinket = StyxWoW.Me.Inventory.Equipped.Trinket2;
            var hands = StyxWoW.Me.Inventory.Equipped.Hands;

            if (firstTrinket != null && CanUseEquippedItem(firstTrinket))
                firstTrinket.Use();


            if (secondTrinket != null && CanUseEquippedItem(secondTrinket))
                secondTrinket.Use();

            if (hands != null && CanUseEquippedItem(hands))
                hands.Use();

        }
        private static bool CanUseEquippedItem(WoWItem item)
        {
            string itemSpell = Lua.GetReturnVal<string>("return GetItemSpell(" + item.Entry + ")", 0);
            if (string.IsNullOrEmpty(itemSpell))
                return false;

            return item.Usable && item.Cooldown <= 0;
        }

/*
        static bool NeedTasteForBloodDump
        {
            get
            {
                var tfb = Me.GetAllAuras().FirstOrDefault(a => a.Name == "Taste for Blood" && a.TimeLeft > TimeSpan.Zero && a.StackCount > 0);
                if (tfb != null)
                {
                    // If we have more than 3 stacks, pop HS
                    if (tfb.StackCount >= 3)
                    {
                        Logger.WriteDebug(Color.White, "^Taste for Blood");
                        return true;
                    }

                    // If it's about to drop, and we have at least 2 stacks, then pop HS.
                    // If we have 1 stack, then a slam is better used here.
                    if (tfb.TimeLeft.TotalSeconds < 1 && tfb.StackCount >= 2)
                    {
                        Logger.WriteDebug(Color.White, "^Taste for Blood (falling off)");
                        return true;
                    }
                }
                return false;
            }
        }
*/
        static bool NeedHeroicStrikeDumpIcyVeins
        {
            get
            {
                if (Me.GotTarget() && Me.RagePercent >= 70 && Spell.CanCastHack("Heroic Strike", Me.CurrentTarget, skipWowCheck: true))
                {
                    if (Me.RagePercent >= (Me.MaxRage - 15) && (Me.CurrentTarget.HealthPercent > 20 || !SpellManager.HasSpell("Colossus Smash")))
                    {
                        Logger.Write( LogColor.Hilite, "^Heroic Strike - Rage Dump @ {0}%", (int)Me.RagePercent);
                        return true;
                    }

                    if (Me.CurrentTarget.HasAura("Colossus Smash"))
                    {
                        Logger.Write( LogColor.Hilite, "^Heroic Strike - Rage Dump @ {0}% with Colossus Smash active", (int)Me.RagePercent);
                        return true;
                    }
                }

                return false;
            }
        }

        static bool NeedHeroicStrikeDumpNoxxic
        {
            get
            {
                if (Me.GotTarget() && Me.RagePercent >= 70 && Spell.CanCastHack("Heroic Strike", Me.CurrentTarget, skipWowCheck: true))
                {
                    if (Me.CurrentTarget.HasAura("Colossus Smash") || !SpellManager.HasSpell("Colossus Smash") || Me.CurrentTarget.TimeToDeath() < 8)
                    {
                        Logger.Write( LogColor.Hilite, "^Heroic Strike - Rage Dump @ {0}%", (int)Me.RagePercent);
                        return true;
                    }
                }
                return false;
            }
        }

        private static Composite CreateDiagnosticOutputBehavior(string context = null)
        {
            if (context == null)
                context = Dynamics.CompositeBuilder.CurrentBehaviorType.ToString();
            
            context = "<<" + context + ">>";

            return new Decorator(
                ret => SingularSettings.Debug,
                new ThrottlePasses(1,
                    new Action(ret =>
                    {
                        string log;
                        log = string.Format(context + " h={0:F1}%/r={1:F1}%, stance={2}, Enrage={3} Coloss={4} MortStrk={5}",
                            Me.HealthPercent,
                            Me.CurrentRage,
                            Me.Shapeshift,
                            Me.ActiveAuras.ContainsKey("Enrage"),
                            (int) Spell.GetSpellCooldown("Colossus Smash", -1).TotalMilliseconds,
                            (int) Spell.GetSpellCooldown("Mortal Strike", -1).TotalMilliseconds
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            log += string.Format(", th={0:F1}%, dist={1:F1}, inmelee={2}, face={3}, loss={4}, dead={5} secs, flying={6}",
                                target.HealthPercent,
                                target.Distance,
                                target.IsWithinMeleeRange.ToYN(),
                                Me.IsSafelyFacing(target).ToYN(),
                                target.InLineOfSpellSight.ToYN(),
                                target.TimeToDeath(),
                                target.IsFlying.ToYN()
                                );
                        }

                        Logger.WriteDebug(Color.AntiqueWhite, log);
                        return RunStatus.Failure;
                    })
                    )
                );
        }

        #endregion

        private static Composite _checkWeapons = null;
        public static Composite CheckThatWeaponIsEquipped()
        {
            if (_checkWeapons == null)
            {
                _checkWeapons = new ThrottlePasses(60,
                    new Sequence(
                        new DecoratorContinue(
                            ret => !Me.Disarmed && !IsWeapon2H(Me.Inventory.Equipped.MainHand),
                            new Action(ret => Logger.Write(Color.HotPink, "User Error: a{0} requires a Two Handed Weapon equipped to be effective", SingularRoutine.SpecAndClassName()))
                            ),
                        new ActionAlwaysFail()
                        )
                    );
            }
            return _checkWeapons;
        }
        public static bool IsWeapon2H(WoWItem hand)
        {
            return hand != null 
                && hand.ItemInfo.ItemClass == WoWItemClass.Weapon
                && hand.ItemInfo.InventoryType == InventoryType.TwoHandWeapon;
        }
    }
}