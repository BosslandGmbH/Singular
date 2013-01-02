using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Singular.Settings;
using Singular.Helpers;
using System;
using Styx.CommonBot.POI;

namespace Singular.ClassSpecific.Rogue
{
    public class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static RogueSettings RogueSettings { get { return SingularSettings.Instance.Rogue; } }

        public static bool IsStealthed { get { return Me.HasAnyAura("Stealth", "Shadow Dance", "Vanish"); } }

        [Behavior(BehaviorType.Rest, WoWClass.Rogue)]
        public static Composite CreateRogueRest()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateStealthBehavior( ret => StyxWoW.Me.HasAura("Food")),
                        Rest.CreateDefaultRestBehaviour(),
                        CreateRogueGeneralMovementBuff("Rest")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Rogue, (WoWSpec) int.MaxValue, WoWContext.Normal|WoWContext.Battlegrounds)]
        public static Composite CreateRogueHeal()
        {
            return new Decorator(
                ret => !Spell.IsCastingOrChannelling() & !Spell.IsGlobalCooldown() && !IsStealthed && !Group.Healers.Any(h => h.IsAlive && h.Distance < 60),
                new PrioritySelector(
                    Movement.CreateFaceTargetBehavior(),
                    new Decorator(
                        ret => SingularSettings.Instance.UseBandages
                            && StyxWoW.Me.HealthPercent < 20
                            && !Unit.NearbyUnfriendlyUnits.Any(u => u.Combat && u.Guid != StyxWoW.Me.CurrentTargetGuid && u.CurrentTargetGuid == StyxWoW.Me.Guid)
                            && Item.HasBandage(),
                        new Sequence(
                            new PrioritySelector(
                                Spell.Cast("Gouge"),
                                Spell.Cast("Blind"),
                                new Decorator( 
                                    ret => !Unit.NearbyUnfriendlyUnits.Any(u => !u.IsCrowdControlled()),
                                    new ActionAlwaysSucceed()
                                    )
                                ),
                            Helpers.Common.CreateWaitForLagDuration(),
                            new WaitContinue(TimeSpan.FromMilliseconds(250), ret => Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                            new WaitContinue(TimeSpan.FromMilliseconds(1500), ret => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                            Item.CreateUseBandageBehavior()
                            )
                        ),

                    new Decorator(
                        ret => RogueSettings.RecuperateHealth > 0 && Me.RawComboPoints > 0,
                        new PrioritySelector(
                            // cast regardless of combo points if we are below health level
                            Spell.BuffSelf("Recuperate", ret => Me.HealthPercent < RogueSettings.RecuperateHealth),

                            // cast at higher health level based upon number of attackers
                            Spell.BuffSelf("Recuperate",
                                ret => AoeCount > 0
                                    && Me.RawComboPoints >= Math.Min(AoeCount, 3)
                                    && Me.HealthPercent < (100 * (AoeCount - 1) + RogueSettings.RecuperateHealth) / AoeCount),

                            // cast if partially need healing and mob about to die
                            Spell.BuffSelf("Recuperate", 
                                ret => Me.GotTarget 
                                    && AoeCount == 1
                                    && Me.CurrentTarget.TimeToDeath() < 2
                                    && Me.HealthPercent < (100+RogueSettings.RecuperateHealth) / 2 )
                            )
                        )
                    )
                );

        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Rogue)]
        public static Composite CreateRoguePreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(false),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown( allowLagTollerance:false) && !IsStealthed,
                    new PrioritySelector(
                        // new Action(r => { Logger.WriteDebug("PreCombatBuffs -- stealthed={0}", Stealthed); return RunStatus.Failure; }),
                        CreateApplyPoisons(),

                        // don't waste the combo points if we have them
                        Spell.Cast("Recuperate", 
                            on => Me,
                            ret => StyxWoW.Me.RawComboPoints > 0 
                                && !SpellManager.HasSpell( "Redirect")
                                && Me.HasAuraExpired("Recuperate", 3 + Me.RawComboPoints * 6))
                        )
                    )
                );
        }

        [Behavior(BehaviorType.PullBuffs, WoWClass.Rogue, (WoWSpec)int.MaxValue, WoWContext.All)]
        public static Composite CreateRoguePullBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        // new Action( r => { Logger.WriteDebug("PullBuffs -- stealthed={0}", Stealthed ); return RunStatus.Failure; } ),
                        CreateStealthBehavior( ret => !IsStealthed ),
                        Spell.BuffSelf("Redirect", ret => StyxWoW.Me.RawComboPoints > 0),
                        Spell.BuffSelf("Recuperate", ret => StyxWoW.Me.RawComboPoints > 0 && (!SpellManager.HasSpell("Redirect") || !SpellManager.CanCast("Redirect"))),
                        Spell.Cast("Shadowstep", ret => StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.Distance > 12),
                        Spell.BuffSelf("Sprint", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.HasAura("Stealth") && StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.Distance > 15 && !(SpellManager.HasSpell("Shadowstep") || SpellManager.CanCast("Shadowstep", true)))
                        )
                    )
                );

        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Rogue, (WoWSpec)int.MaxValue, WoWContext.All)]
        public static Composite CreateRogueCombatBuffs()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => !Spell.IsCastingOrChannelling() & !Spell.IsGlobalCooldown() && !IsStealthed,
                    new PrioritySelector(
                        Movement.CreateFaceTargetBehavior(),

                        CreateActionCalcAoeCount(),

                        // Defensive
                        Spell.BuffSelf("Combat Readiness", ret => AoeCount > 2 && !Me.HasAura("Feint")),
                        Spell.BuffSelf("Feint", ret => AoeCount > 2 && !Me.HasAura("Combat Readiness") && HaveTalent(RogueTalents.Elusivenss)),
                        Spell.BuffSelf("Evasion", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6 && u.IsTargetingMeOrPet) >= 2),
                        Spell.BuffSelf("Cloak of Shadows", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet && u.IsCasting) >= 1),
                        Spell.BuffSelf("Smoke Bomb", ret => StyxWoW.Me.HealthPercent < 40 && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr > 4*4 && u.IsAlive && u.Combat && u.IsTargetingMeOrPet) >= 1),
                        Spell.BuffSelf("Vanish", ret => StyxWoW.Me.HealthPercent < 20),

                        Spell.Cast("Shiv", ret => Me.CurrentTarget.HasAura("Enraged")),

                        Common.CreateRogueBlindOnAddBehavior(),

                        // Redirect if we have CP left
                        Spell.Cast("Redirect", ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.ComboPoints < 1),

                        Spell.Cast( "Deadly Throw", 
                            ret => Me.ComboPoints >= 5 
                                && Me.GotTarget
                                && Me.CurrentTarget.IsCasting 
                                && Me.CurrentTarget.CanInterruptCurrentSpellCast ),

                        // Pursuit
                        Spell.Cast("Shadowstep", ret => Me.CurrentTarget.Distance > 12 && Unit.CurrentTargetIsMovingAwayFromMe),
                        Spell.Cast("Burst of Speed", ret => Me.IsMoving && Me.CurrentTarget.Distance > 10 && Unit.CurrentTargetIsMovingAwayFromMe),

                        // Vanish to boost DPS if behind target, not stealthed, have slice/dice, and 0/1 combo pts
                        new Sequence( 
                            Spell.BuffSelf("Vanish", 
                                ret => Me.GotTarget
                                    && !IsStealthed
                                    && !Me.HasAuraExpired( "Slice and Dice", 4)
                                    && Me.ComboPoints < 2
                                    && Me.IsSafelyBehind( Me.CurrentTarget)),
                            new Wait( TimeSpan.FromMilliseconds(500), ret => IsStealthed, new ActionAlwaysSucceed()),
                            CreateRogueOpenerBehavior()
                            )
                        )
                    )
                );

        }

        public static Composite CreateApplyPoisons()
        {
            return new PrioritySelector(
                new Decorator(
                    r => Poisons.NeedLethalPosion() != LethalPoisonType.None, 
                    new Sequence(
                        Spell.BuffSelf(ret => (int) Poisons.NeedLethalPosion(), req => true),
                        new WaitContinue( 1, ret => Me.IsCasting && Me.CastingSpellId == (int) Poisons.NeedLethalPosion(), new ActionAlwaysSucceed())
                        )
                    ),
                new Decorator(
                    r => Poisons.NeedNonLethalPosion() != NonLethalPoisonType.None,
                    new Sequence(
                        Spell.BuffSelf(ret => (int)Poisons.NeedNonLethalPosion(), req => true),
                        new WaitContinue(1, ret => Me.IsCasting && Me.CastingSpellId == (int) Poisons.NeedNonLethalPosion(), new ActionAlwaysSucceed())
                        )
                    )
                );
        }

        public static Composite CreateRogueOpenerBehavior()
        {
            return new Decorator(
                ret => Common.IsStealthed,
                new PrioritySelector(
                    Spell.Cast("Ambush", ret => Me.IsSafelyBehind(Me.CurrentTarget)),
                    Spell.Cast("Garrote", ret => !Me.IsMoving && !Me.IsSafelyBehind(Me.CurrentTarget)),
                    Spell.Cast("Cheap Shot", ret => !Me.IsMoving )
                    )
                );
        }

        public static Composite CreateRogueBlindOnAddBehavior()
        {
            return new PrioritySelector(
                    ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u =>
                            u.IsTargetingMeOrPet && u != StyxWoW.Me.CurrentTarget),
                    new Decorator(
                        ret => ret != null && !StyxWoW.Me.HasAura("Blade Flurry"),
                        Spell.Buff("Blind", ret => (WoWUnit)ret, ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro) > 1)));
        }

        public static WoWUnit BestTricksTarget
        {
            get
            {
                if (!StyxWoW.Me.GroupInfo.IsInParty && !StyxWoW.Me.GroupInfo.IsInRaid)
                    return null;

                // If the player has a focus target set, use it instead. TODO: Add Me.FocusedUnit to the HB API.
                if (StyxWoW.Me.FocusedUnitGuid != 0)
                    return StyxWoW.Me.FocusedUnit;

                if (StyxWoW.Me.IsInInstance)
                {
                    if (RaFHelper.Leader != null && !RaFHelper.Leader.IsMe)
                    {
                        // Leader first, always. Otherwise, pick a rogue/DK/War pref. Fall back to others just in case.
                        return RaFHelper.Leader;
                    }

                    if (StyxWoW.Me.GroupInfo.IsInParty)
                    {
                        var bestTank = Group.Tanks.OrderBy(t => t.DistanceSqr).FirstOrDefault(t => t.IsAlive);

                        if (bestTank != null)
                            return bestTank;
                    }

                    var bestPlayer = Group.GetPlayerByClassPrio(100f, false,
                        WoWClass.Rogue, WoWClass.DeathKnight, WoWClass.Warrior,WoWClass.Hunter, WoWClass.Mage, WoWClass.Warlock, WoWClass.Shaman, WoWClass.Druid,
                        WoWClass.Paladin, WoWClass.Priest);
                    return bestPlayer;
                }

                return null;
            }
        }

        public static Decorator CreateRogueGeneralMovementBuff(string mode, bool checkMoving = true)
        {
            return new Decorator(
                ret => SingularSettings.Instance.Priest.UseSpeedBuff
                    && !MovementManager.IsMovementDisabled
                    && StyxWoW.Me.IsAlive
                    && (!checkMoving || StyxWoW.Me.IsMoving)
                    && !StyxWoW.Me.Mounted
                    && !StyxWoW.Me.IsOnTransport
                    && !StyxWoW.Me.OnTaxi
                    && SpellManager.HasSpell("Burst of Speed")
                    && !StyxWoW.Me.HasAnyAura("Burst of Speed")
                    && (BotPoi.Current == null || BotPoi.Current.Type == PoiType.None || BotPoi.Current.Location.Distance(StyxWoW.Me.Location) > 15)
                    && !StyxWoW.Me.IsAboveTheGround(),

                new PrioritySelector(
                    Spell.WaitForCast(),
                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(
                            Spell.BuffSelf("Burst of Speed")
                            )
                        )
                    )
                );
        }


        public static int AoeCount { get; set; }

        public static Action CreateActionCalcAoeCount()
        {
            return new Action(ret =>
            {
                if (Unit.NearbyUnfriendlyUnits.Any(u => u.IsCrowdControlled()))
                    AoeCount = 1;
                else
                    AoeCount = Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < (u.MeleeDistance() + 3));
                return RunStatus.Failure;
            });
        }

        public static bool HaveTalent(RogueTalents rogueTalents)
        {
            return TalentManager.IsSelected((int)rogueTalents);
        }


        internal static Composite CreateAttackFlyingMobs()
        {
            return new Decorator(
                ret => Me.CurrentTarget.IsFlying || Me.CurrentTarget.IsAboveTheGround() || Me.CurrentTarget.Distance2DSqr < 5 * 5 && Math.Abs(Me.Z - Me.CurrentTarget.Z) >= 5,
                new PrioritySelector(
                    Spell.Cast("Deadly Throw"),
                    Spell.Cast("Throw"),

                    // nothing else worked, so cancel stealth so we can proximity aggro
                    new Decorator(
                        ret => Me.HasAura("Stealth"),
                        new Sequence(
                            new Action(ret => Logger.Write("/cancel Stealth")),
                            new Action(ret => Me.CancelAura("Stealth")),
                            new Wait(TimeSpan.FromMilliseconds(500), ret => !Me.HasAura("Stealth"), new ActionAlwaysSucceed())
                            )
                        )
                    )
                );
        }

        internal static Composite CreateStealthBehavior( SimpleBooleanDelegate req = null)
        {
            return new Sequence(
                Spell.BuffSelf("Stealth", ret => req == null || req(ret)),
                new Wait( TimeSpan.FromMilliseconds(500), ret => IsStealthed, new ActionAlwaysSucceed())
                );
        }

        public static bool HasDaggerInMainHand
        {
            get
            {
                return IsDagger( Me.Inventory.Equipped.MainHand );
            }
        }

        public static bool HasDaggerInOffHand
        {
            get
            {
                return IsDagger(Me.Inventory.Equipped.OffHand);
            }
        }

        public static bool HasTwoDaggers
        {
            get
            {
                return IsDagger(Me.Inventory.Equipped.MainHand) && IsDagger(Me.Inventory.Equipped.OffHand);
            }
        }

        public static bool IsDagger( WoWItem hand)
        {
            return hand != null && hand.ItemInfo.IsWeapon && hand.ItemInfo.WeaponClass == WoWItemWeaponClass.Dagger;
        }

    }

    public enum RogueTalents
    {
        None = 0,
        Nightstalker,
        Subterfuge,
        ShadowFocus,
        DeadlyThrow,
        NerveStrike,
        CombatReadiness,
        CheatDeath,
        LeechingPoison,
        Elusivenss,
        Perparation,
        Shadowstep,
        BurstOfSpeed,
        PreyOnTheWeak,
        ParalyticPoison,
        DirtyTricks,
        ShurikenToss,
        Versatility,
        Anticipation
    }
}
