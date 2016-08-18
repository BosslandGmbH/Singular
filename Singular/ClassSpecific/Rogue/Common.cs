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
using System;
using Styx.CommonBot.POI;
using System.Collections.Generic;
using Styx.Helpers;
using System.Drawing;
using Styx.Common;
using Singular.Utilities;
using Styx.CommonBot.Frames;

namespace Singular.ClassSpecific.Rogue
{
    public class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static RogueSettings RogueSettings { get { return SingularSettings.Instance.Rogue(); } }
        private static bool HasTalent(RogueTalents tal) { return TalentManager.IsSelected((int)tal); }

        /// <summary>
        /// checks if we are stealthed or an ability granting use of stealth only abilities is active
        /// </summary>
        public static bool AreStealthAbilitiesAvailable { get { return Me.HasAnyAura("Stealth", "Shadow Dance", "Vanish"); } }
        
        /// <summary>
        /// checks if we are stealthed.  does not include buffs which grant use of stealth abilities
        /// while not stealthed
        /// </summary>
        public static bool IsStealthed { get { return Me.HasAnyAura("Stealth", "Vanish"); } }

        [Behavior(BehaviorType.Initialize, WoWClass.Rogue)]
        public static Composite CreateRogueInitialize()
        {
            // describe configured Stealth Behavior           
            if (RogueSettings.Stealth == StealthMode.Always || RogueSettings.Stealth == StealthMode.Never)
            {
                Logger.Write(LogColor.Init, "Stealth: will stealth '{0}'", RogueSettings.Stealth);
            }
            else if (RogueSettings.Stealth == StealthMode.PVP)
            {
                Logger.Write(LogColor.Init, "Stealth only for 'PVP'");
            }
            else if (RogueSettings.Stealth == StealthMode.Auto)
            {
                if (RogueSettings.PickPocketOnlyPull)
                    Logger.Write(LogColor.Init, "Stealth: 'Auto' cast when approaching aggro range");
                else
                    Logger.Write(LogColor.Init, "Stealth: 'Auto' cast for targets Level {0}+", Unit.TrivialLevel + 1);
            }

            return null;
        }

        public static Composite CreateRogueDismount(string reason)
        {
            // TODO: account for Rogue specific checks for dismount (Stealth, Pick Pocket, etc.)
            return Helpers.Common.CreateDismount(reason);
        }

        /// <summary>
        /// determines if we should use Cloak and Dagger ability.  this allows encapsulating check for 
        /// pick pocket mode, etc
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        /// 
        [Behavior(BehaviorType.Rest, WoWClass.Rogue)]
        public static Composite CreateRogueRest()
        {
            return new PrioritySelector(
                CreateRogueOpenBoxes(),

                CreateStealthBehavior(ret => RogueSettings.StealthIfEating && Helpers.Rest.IsEatingOrDrinking),
                Rest.CreateDefaultRestBehaviour( ),
                CreateStealthBehavior(ret => RogueSettings.Stealth == StealthMode.Always),

                CheckThatDaggersAreEquippedIfNeeded()
                );
        }

        private static Composite _checkDaggers = null;

        public static Composite CheckThatDaggersAreEquippedIfNeeded()
        {
            if (_checkDaggers == null)
            {
                _checkDaggers = new ThrottlePasses(60,
                    new Sequence(
                        new DecoratorContinue(
                            ret => !Me.Disarmed && !Common.HasDaggerInMainHand && SpellManager.HasSpell("Dispatch"),
                            new Action(ret => Logger.Write(Color.HotPink, "User Error: a{0} requires a dagger in mainhand to cast Dispatch", SingularRoutine.SpecName()))
                            ),
                        new DecoratorContinue(
                            ret => !Me.Disarmed && !Common.HasTwoDaggers && SpellManager.HasSpell("Mutilate"),
                            new Action(ret => Logger.Write(Color.HotPink, "User Error: a{0} requires two daggers equipped to cast Mutilate", SingularRoutine.SpecName()))
                            ),
                        new DecoratorContinue(
                            ret => !Me.Disarmed && !Common.HasDaggerInMainHand && SpellManager.HasSpell("Backstab"),
                            new Action(ret => Logger.Write(Color.HotPink, "User Error: a{0} requires a dagger in mainhand to cast Backstab", SingularRoutine.SpecName()))
                            ),
                        new ActionAlwaysFail()
                        )
                    );
            }
            return _checkDaggers;
        }

        [Behavior(BehaviorType.Heal, WoWClass.Rogue, (WoWSpec) int.MaxValue, WoWContext.Normal|WoWContext.Battlegrounds)]
        public static Composite CreateRogueHeal()
        {
            return new Decorator(
                ret => !Spell.IsCastingOrChannelling() & !Spell.IsGlobalCooldown() && !AreStealthAbilitiesAvailable && !Group.AnyHealerNearby,
                new PrioritySelector(
                    Spell.HandleOffGCD(Spell.Cast("Crimson Vail", ret => Me.HealthPercent <= RogueSettings.CrimsonVialHealth)),
                    Movement.CreateFaceTargetBehavior(),
                    new Decorator(
                        ret => SingularSettings.Instance.UseBandages
                            && StyxWoW.Me.HealthPercent < 20
                            && !Unit.NearbyUnfriendlyUnits.Any(u => u.Combat && u.Guid != StyxWoW.Me.CurrentTargetGuid && u.CurrentTargetGuid == StyxWoW.Me.Guid)
                            && Item.HasBandage(),
                        new Sequence(
                            new PrioritySelector(
                                new Decorator(
                                    ret => Me.IsMoving && !MovementManager.IsMovementDisabled,
                                    new Action(ret => { StopMoving.Now(); return RunStatus.Failure; })
                                    ),
                                new Wait(TimeSpan.FromMilliseconds(500), ret => !Me.IsMoving, new ActionAlwaysFail()),
                                new Decorator(
                                    ret => !Unit.NearbyUnfriendlyUnits.Any(u => !u.IsCrowdControlled()),
                                    new ActionAlwaysSucceed()
                                    ),
                                new Sequence(
                                    new PrioritySelector(
                                        Spell.Cast("Gouge", req => TalentManager.HasGlyph("Gouge") || Me.CurrentTarget.IsSafelyFacing(Me, 150f)),
                                        Spell.Cast("Blind")
                                        ),
                                    new WaitContinue( 1, until => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                                    new Wait( TimeSpan.FromMilliseconds(500), until => Me.CurrentTarget.HasAnyAura("Gouge", "Blind"), new ActionAlwaysSucceed())
                                    )
                                ),
                            Helpers.Common.CreateWaitForLagDuration(),
                            new WaitContinue(TimeSpan.FromMilliseconds(250), ret => Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                            new WaitContinue(TimeSpan.FromMilliseconds(1500), ret => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                            Item.CreateUseBandageBehavior()
                            )
                        )
                    )
                );

        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Rogue)]
        public static Composite CreateRoguePreCombatBuffs()
        {
            return new PrioritySelector(

                CreateStealthBehavior(
                    ret => RogueSettings.Stealth == StealthMode.Always 
                        && BotPoi.Current.Type != PoiType.Loot
                        && BotPoi.Current.Type != PoiType.Skin
                        && !ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.IsDead && ((CharacterSettings.Instance.LootMobs && u.CanLoot && u.Lootable) || (CharacterSettings.Instance.SkinMobs && u.Skinnable && u.CanSkin)) && u.Distance < CharacterSettings.Instance.LootRadius)
                        ),

                // new Action(r => { Logger.WriteDebug("PreCombatBuffs -- stealthed={0}", Stealthed); return RunStatus.Failure; }),
                CreateApplyPoisons());
        }

        [Behavior(BehaviorType.PullBuffs, WoWClass.Rogue, (WoWSpec)int.MaxValue, WoWContext.All)]
        public static Composite CreateRoguePullBuffs()
        {
            return new PrioritySelector(
                // new Action( r => { Logger.WriteDebug("PullBuffs -- stealthed={0}", Stealthed ); return RunStatus.Failure; } ),
                new Sequence(
                    new DecoratorContinue(
                        req => AreStealthAbilitiesAvailable,
                        new ActionAlwaysFail()
                        ),
                    new DecoratorContinue(
                        req => !Me.GotTarget(),
                        new PriDbg(3, LogColor.Hilite, s => "^Stealth: suppressed - no current target")
                        ),
                    new DecoratorContinue(
                        req => Me.CurrentTarget.IsTrivial() && (!RogueSettings.UsePickPocket || !IsMobPickPocketable(Me.CurrentTarget)),
                        new PriDbg(3, LogColor.Hilite, s => string.Format("^Stealth: suppressed for trivial level {0} mob {1}", Me.CurrentTarget.Level, !RogueSettings.UsePickPocket ? "" : "we cannot Pick Pocket"))
                        ),
                    CreateStealthBehavior( 
                        ret => {
                            float dist = Me.CurrentTarget.SpellDistance();

                            if (dist < 9)
                                return true;

                            if (dist < 32 && !Me.CurrentTarget.IsNeutral())
                                return true;

                            return false;
                        })
                    ));

        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Rogue, (WoWSpec)int.MaxValue, WoWContext.All)]
        public static Composite CreateRogueCombatBuffs()
        {
            UnitSelectionDelegate onGouge;
            if ( TalentManager.HasGlyph("Gouge"))
                onGouge = on => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(u => u.Guid != Me.CurrentTargetGuid && u.IsWithinMeleeRange && !u.IsCrowdControlled() && Me.IsSafelyFacing(u, 150));
            else
                onGouge = on => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(u => u.Guid != Me.CurrentTargetGuid && u.IsWithinMeleeRange && !u.IsCrowdControlled() && u.IsSafelyFacing(Me, 150) && Me.IsSafelyFacing(u, 150));

            return new PrioritySelector(

                Movement.CreateFaceTargetBehavior(),

                CreateActionCalcAoeCount(),

                new Decorator(
                    req => !Unit.IsTrivial( Me.CurrentTarget),
                    new PrioritySelector(
                        // Defensive
                        Spell.BuffSelf("Combat Readiness", ret => !Me.HasAnyAura("Feint", "Evasion") && Unit.NearbyUnfriendlyUnits.Count(u => u.CurrentTargetGuid == Me.Guid) > 2 ),

                        // stun an add 4 out of every 10 secs if possible
                        Spell.Cast("Gouge", onGouge ),

                        // Spell.BuffSelf("Feint", ret => AoeCount > 2 && !Me.HasAura("Combat Readiness") && HaveTalent(RogueTalents.Elusivenss)),
                        Spell.BuffSelf("Evasion", ret => !Me.HasAnyAura("Feint", "Combat Readiness") && Unit.UnfriendlyUnits(6).Count(u => u.CurrentTargetGuid == Me.Guid) > 1),
                        Spell.BuffSelf("Cloak of Shadows", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet && u.IsCasting) >= 1),
                        Spell.BuffSelf("Smoke Bomb", ret => StyxWoW.Me.HealthPercent < 40 && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr > 4 * 4 && u.IsAlive && u.Combat && u.IsTargetingMeOrPet) >= 1),
                        Spell.BuffSelf("Vanish", ret => StyxWoW.Me.HealthPercent < 20 && !SingularRoutine.IsQuestBotActive),
						Spell.BuffSelf("Crimson Vial", ret => Me.HealthPercent < RogueSettings.CrimsonVialHealth),
                        Spell.BuffSelf("Preparation",
                            ret => Spell.GetSpellCooldown("Vanish").TotalSeconds > 10
                                && Spell.GetSpellCooldown("Evasion").TotalSeconds > 10
                                && Spell.GetSpellCooldown("Combat Readiness").TotalSeconds > 10),

                        Spell.Cast("Shiv", ret => Me.CurrentTarget.HasAura("Enraged")),

                        // Now any enemy missing Weakened Armor
                        Spell.Buff("Expose Armor", req => {
                            if (!Me.GotTarget())
                                return false;

                            if (Me.CurrentTarget.HasAura("Weakened Armor", 3))
                                return false;

                            if (!TalentManager.HasGlyph("Expose Armor"))
                                return false;

                            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances && Unit.GroupMembers.Any( u => u.IsAlive && u.CurrentTargetGuid ==  Me.CurrentTargetGuid && (u.Class == WoWClass.Druid || u.Class == WoWClass.Warrior)))
                                return false;

                            if (Me.CurrentTarget.IsPlayer && Me.CurrentTarget.IsMelee())
                                return true;

                            if (Me.CurrentTarget.TimeToDeath() < 30)
                                return false;

                            return true; 
                            }),

                        Common.CreateRogueBlindOnAddBehavior(),

                        Spell.Cast("Marked for Death", ret => StyxWoW.Me.ComboPoints == 0),

                        Spell.Cast("Deadly Throw",
                            ret => Me.ComboPoints >= 3
                                && Me.IsSafelyFacing(Me.CurrentTarget)
                                && Me.CurrentTarget.IsCasting
                                && Me.CurrentTarget.CanInterruptCurrentSpellCast),

                        // Pursuit
                        Spell.Cast("Shadowstep", ret => MovementManager.IsClassMovementAllowed && Me.CurrentTarget.Distance > 12 && Unit.CurrentTargetIsMovingAwayFromMe),
                        Spell.Cast("Burst of Speed", ret => MovementManager.IsClassMovementAllowed && Me.IsMoving && Me.CurrentTarget.Distance > 10 && Unit.CurrentTargetIsMovingAwayFromMe),
                        Spell.Cast("Shuriken Toss", ret => !AreStealthAbilitiesAvailable && !Me.CurrentTarget.IsWithinMeleeRange && Me.IsSafelyFacing(Me.CurrentTarget)),

                        // Vanish to boost DPS if behind target, not stealthed, have slice/dice, and 0/1 combo pts
                        new Sequence(
                            Spell.BuffSelf("Vanish",
                                ret => Me.GotTarget()
                                    && !SingularRoutine.IsQuestBotActive
                                    && SingularRoutine.CurrentWoWContext != WoWContext.Normal
                                    && !AreStealthAbilitiesAvailable
                                    && !Me.HasAuraExpired("Slice and Dice", 4)
                                    && Me.ComboPoints < 2
                                    // && Me.IsSafelyBehind(Me.CurrentTarget)
                                    ),
                            new Wait(TimeSpan.FromMilliseconds(500), ret => AreStealthAbilitiesAvailable, new ActionAlwaysSucceed()),
                            CreateRogueOpenerBehavior()
                            ),

                        // Pick Pocket? for those that favor coin over combat, try here in case we restealth
                        CreateRoguePickPocket(),

                        // DPS Boost               
                        new Sequence(
                            new Throttle(TimeSpan.FromSeconds(2),
                                new Decorator(
                                    req => UseLongCoolDownAbility,
                                    Spell.BuffSelf("Shadow Blades", req =>
                                    {
                                        switch (TalentManager.CurrentSpec)
                                        {
                                            default:
                                            case WoWSpec.RogueAssassination:
                                                return Me.ComboPoints <= 2;

                                            case WoWSpec.RogueOutlaw:
                                                return Me.HasAura("Adrenaline Rush");

                                            case WoWSpec.RogueSubtlety:
                                                return Me.ComboPoints <= 2 && !Me.HasAura("Find Weakness");
                                        }
                                    }))
                                ),

                            new ActionAlwaysFail()
                            )
                        )
                    )
                );

        }

        /// <summary>
        /// replaces Spell.CanCastHack for use with Openers for Rogues.  The property information for Ambush,
        /// Garrote, and Cheap Shot does not get updated when Cloak and Dagger is taken resulting it remaining
        /// as a melee spell with max/min range of 0 and 0.  this class specific cancast routine accounts for
        /// use of those spells
        /// </summary>
        /// <param name="sfr"></param>
        /// <param name="unit"></param>
        /// <param name="skipWoWCheck"></param>
        /// <returns></returns>
        public static bool RogueCanCastOpener(SpellFindResults sfr, WoWUnit unit, bool skipWoWCheck = false)
        {
            return Spell.CanCastHack(sfr, unit, false);
        }

        public static Composite CreateRogueMoveBehindTarget()
        {
            return new Decorator(
                req => MovementManager.IsMoveBehindAllowed && RogueSettings.MoveBehindTargets,
                Movement.CreateMoveBehindTargetBehavior()
                );
        }

        
        public static Composite CreatePullMobMovingAwayFromMe()
        {
            return new Throttle( 2,
                new Decorator(
                    ret => Me.GotTarget() 
                        && Me.CurrentTarget.IsMoving && Me.IsMoving 
                        && Me.CurrentTarget.MovementInfo.CurrentSpeed >= Me.MovementInfo.CurrentSpeed
                        && Me.IsSafelyBehind( Me.CurrentTarget),
                    new Sequence(
                        new Action(r => Logger.WriteDebug("MovingAwayFromMe: Target ({0:F2}) faster than Me ({1:F2}) -- trying Sprint or Ranged Attack", Me.CurrentTarget.MovementInfo.CurrentSpeed, Me.MovementInfo.CurrentSpeed)),
                        new PrioritySelector(
                            Spell.Buff("Sap", req => AreStealthAbilitiesAvailable && IsUnitViableForSap(Me.CurrentTarget)),
                            new Decorator(
                                req => !Me.HasAnyAura("Sprint","Shadowstep"),
                                new PrioritySelector(
                                    Spell.BuffSelf("Shadowstep"),
                                    Spell.BuffSelf("Sprint")
                                    )
                                ),
                            new Decorator(
                                req => !Me.CurrentTarget.IsPlayer,
                                new PrioritySelector(
                                    Spell.Cast("Shuriken Toss"),
                                    Spell.CastOnGround("Distract", on => Me.CurrentTarget, req => true, false)
                                    )
                                )
                            )
                        )
                    )
                );

        }

        public static Composite CreateRogueControlNearbyEnemyBehavior()
        {
            if (Dynamics.CompositeBuilder.CurrentBehaviorType != BehaviorType.Pull)
                return new ActionAlwaysFail();

            if (RogueSettings.SapAddDistance == 0)
                return new ActionAlwaysFail();

            return new Decorator(
                req => AreStealthAbilitiesAvailable && Me.GotTarget() && SpellManager.HasSpell("Sap"),
                new PrioritySelector(
                    ctx => GetBestSapTarget(),
                    new Decorator(
                        ret => ret != null,
                        new PrioritySelector(
                            Movement.CreateMoveToLosBehavior( on => (WoWUnit) on),
                            Movement.CreateMoveToUnitBehavior( on => (WoWUnit) on, 10, 7, statusWhenMoving: RunStatus.Success ),
                            new Sequence(
                                new Action( on => Me.SetFocus( (WoWUnit)on)),
                                Spell.Buff("Sap", on => (WoWUnit) on),
                                new DecoratorContinue( req => ((WoWUnit)req).Guid != Me.CurrentTargetGuid, Movement.CreateEnsureMovementStoppedBehavior(reason: "to change direction to CurrentTarget")),
                                new Wait( TimeSpan.FromMilliseconds(500), until => ((WoWUnit) until).HasAura("Sap"), new ActionAlwaysFail()),
                                new ActionAlwaysFail()
                                )
                            )
                        )
                    )
                );
        }
       
        public static WoWGuid lastSapTarget { get; set; }

        private static WoWUnit GetBestSapTarget()
        {
            if (RogueSettings.PickPocketOnlyPull && RogueSettings.UsePickPocket)
                return Me.CurrentTarget;

            if (RogueSettings.SapAddDistance <= 0 && !RogueSettings.SapMovingTargetsOnPull)
                return null;

            if (!Me.GotTarget() || !AreStealthAbilitiesAvailable)
                return null;

            if (Unit.NearbyUnfriendlyUnits.Any(u => u.HasMyAura("Sap")))
                return null;

            string msg = "";
            WoWUnit closestTarget = null;

            if (RogueSettings.SapAddDistance > 0 && (!RogueSettings.PickPocketOnlyPull || !RogueSettings.UsePickPocket || SingularRoutine.CurrentWoWContext != WoWContext.Normal))
            {
                // stick with our target if we thought it was a good choice previously 
                // ... (to avoid zig zag back and forth at boundary distance conditions)
                if (lastSapTarget.IsValid && Me.CurrentTargetGuid == lastSapTarget && IsUnitViableForSap(Me.CurrentTarget))
                    closestTarget = Me.CurrentTarget;
                else
                {
                    closestTarget = Unit.UnfriendlyUnitsNearTarget(RogueSettings.SapAddDistance)
                         .Where(u => u.Guid != Me.CurrentTargetGuid && !u.IsSensitiveDamage() && IsUnitViableForSap(u))
                         .OrderBy(u => u.Location.DistanceSqr(Me.CurrentTarget.Location))
                         .ThenBy(u => u.DistanceSqr)
                         .FirstOrDefault();

                    if (closestTarget != null)
                    {
                        msg = string.Format("^Sap: {0} which is {1:F1} yds from target to avoid aggro while attacking", closestTarget.SafeName(), Me.CurrentTarget.SpellDistance(closestTarget));
                    }
                }
            }

            if (closestTarget == null)
            {
                Logger.WriteDebug(Color.White, "no nearby Sap target");
            }
            else if (lastSapTarget != closestTarget.Guid)
            {
                lastSapTarget = closestTarget.Guid;
                // reset the Melee Range check timeer to avoid timing out
                SingularRoutine.TargetTimeoutTimer.Reset();
                Logger.Write( LogColor.Hilite, msg, closestTarget.SafeName());
            }

            return closestTarget;
        }

        private static bool IsUnitViableForSap(WoWUnit unit)
        {
            if (unit.Combat)
                return false;

            if (!(unit.IsHumanoid || unit.IsBeast || unit.IsDemon || unit.IsDragon))
                return false;

            if (unit.IsBoss())
                return false;

            if (_entryImmuneToSap.Contains( unit.Entry) )
                return false;

            if (unit.IsCrowdControlled())
                return false;

            if (!unit.InLineOfSpellSight)
                return false;

            return true;
        }


        private static HashSet<uint> _entryImmuneToSap = new HashSet<uint>();

        public static void AddEntryToSapImmuneList( WoWUnit unit )
        {
            if (unit == null || _entryImmuneToSap.Contains(unit.Entry))
                return;

            Logger.Write(LogColor.Hilite, "ImmuneToSap: adding {0} #{1} to immunity list", unit.SafeName(), unit.Entry );
            _entryImmuneToSap.Add(unit.Entry);
        }

        public static Composite CreateApplyPoisons()
        {
            return new Sequence(
                new PrioritySelector(
                    Spell.BuffSelf(ret => (int)Poisons.NeedLethalPosion(), req => true ),
                    Spell.BuffSelf(ret => (int)Poisons.NeedNonLethalPosion(), req => true )
                    ),
                new Wait(1, ret => Me.IsCasting, new ActionAlwaysSucceed()),
                new Wait(4, ret => !Me.IsCasting, new ActionAlwaysSucceed()),
                Helpers.Common.CreateWaitForLagDuration()
                );
        }

        public static Composite CreateRogueOpenerBehavior()
        {
            return new Decorator(
                ret => Common.AreStealthAbilitiesAvailable,
                new PrioritySelector(
                    CreateRoguePickPocket(),

					Spell.Cast("Shadowstrike"),
                    // Cheap Shot logic
                    // for Subterfuge, wait up to 2500 secs then cast Ambush (we hope)
                    // for non-Subterfuge, melee swings on target building up energy
                    new Sequence(
                        Spell.Cast("Cheap Shot", 
                            mov => false,
                            on => Me.CurrentTarget,
                            ret => Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull && (!Me.CurrentTarget.IsStunned() || Me.CurrentTarget.HasAura("Sap") ),
                            cancel => false // only to make sure GCD has started
                            ),
                        new Wait( TimeSpan.FromMilliseconds(300), until => Me.CurrentTarget.HasAura("Cheap Shot"), new ActionAlwaysSucceed()),
                        new WaitContinue( 
                            TimeSpan.FromMilliseconds( 3700),
                            until => {
                                if (Common.AoeCount > 1)
                                    return true;
                                if (Me.CurrentTarget.GetAuraTimeLeft("Cheap Shot").TotalMilliseconds < 350)
                                    return true;
                                if (Me.GetAuraTimeLeft("Subterfuge").TotalMilliseconds.Between(1,400))
                                    return true;
                                return false;
                                },
                            new ActionAlwaysFail()
                            ),
                        new WaitContinue(
                            TimeSpan.FromMilliseconds(1400),
                            until => Me.EnergyPercent == 100 || Me.CurrentTarget.GetAuraTimeLeft("Cheap Shot").TotalMilliseconds < 500,
                            new ActionAlwaysSucceed()
                            )
                        ),

                    new Decorator(
                        req => AreStealthAbilitiesAvailable,
                        new PrioritySelector(
                            Spell.Cast(sp => "Garrote", chkMov => false, on => Me.CurrentTarget, req => IsGarroteNeeded() && (Me.CurrentTarget.IsCasting || Me.CurrentTarget.GetPrimaryStat() == StatType.Intellect), canCast: RogueCanCastOpener),
                            Spell.Cast(sp => "Ambush", chkMov => false, on => Me.CurrentTarget, req => IsAmbushNeeded(), canCast: RogueCanCastOpener),
                            Spell.Cast(sp => "Cheap Shot", chkMov => false, on => Me.CurrentTarget, req => IsCheapShotNeeded(), canCast: RogueCanCastOpener),
                            Spell.Cast(sp => "Garrote", chkMov => false, on => Me.CurrentTarget, req => IsGarroteNeeded(), canCast: RogueCanCastOpener)
                            )
                        )
                    )
                );
        }

        public static bool IsAmbushNeeded()
        {
            return AreStealthAbilitiesAvailable;
        }

        private static bool IsCheapShotNeeded()
        {
            return AreStealthAbilitiesAvailable;
        }

        private static bool IsGarroteNeeded()
        {
            return AreStealthAbilitiesAvailable;
        }

        public static Composite CreateRogueBlindOnAddBehavior()
        {
            return new PrioritySelector(
                ctx => Unit.NearbyUnfriendlyUnits
                    .FirstOrDefault(u => u.IsTargetingMeOrPet && u != StyxWoW.Me.CurrentTarget && Spell.CanCastHack("Blind", u)),
                new Decorator(
                    ret => ret != null && !StyxWoW.Me.HasAura("Blade Flurry"),
                    Spell.Buff("Blind", on => (WoWUnit) on, req => Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro) > 1)
                    )
                );
        }


        public static Composite CreateDismantleBehavior()
        {
            if (!RogueSettings.UseDimantle)
                return new ActionAlwaysFail();

            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
            {
                return new Throttle(15,
                    Spell.Cast("Dismantle", on =>
                    {
                        if (Spell.IsSpellOnCooldown("Dismantle"))
                            return null;

                        WoWUnit unit = Unit.NearbyUnfriendlyUnits.FirstOrDefault(
                            u => u.IsWithinMeleeRange
                                && (u.IsMelee() || u.Class == WoWClass.Hunter)
                                && !Me.CurrentTarget.Disarmed
                                && !Me.CurrentTarget.IsCrowdControlled()
                                && Me.IsSafelyFacing(u, 150)
                                );
                        return unit;
                    })
                    );
            }

            return new Throttle(15, Spell.Cast("Dismantle", req => !Me.CurrentTarget.Disarmed && !Me.CurrentTarget.IsCrowdControlled()));
        }

        public static WoWUnit BestTricksTarget
        {
            get
            {
                if (!StyxWoW.Me.GroupInfo.IsInParty && !StyxWoW.Me.GroupInfo.IsInRaid)
                    return null;
                
                // If the player has a focus target set, use it instead. TODO: Add Me.FocusedUnit to the HB API.
                if (StyxWoW.Me.FocusedUnitGuid.IsValid)
                    return StyxWoW.Me.FocusedUnit;

                if (StyxWoW.Me.IsInInstance)
                {
                    if (RaFHelper.Leader != null && RaFHelper.Leader.IsValid && !RaFHelper.Leader.IsMe)
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

        public static int AoeCount { get; set; }

        public static Action CreateActionCalcAoeCount( SimpleFloatDelegate aoeEnemyRange = null)
        {
            SimpleFloatDelegate enemyRange = aoeEnemyRange ?? (er => ((WoWUnit)er).MeleeDistance() + 3);
            return new Action(ret =>
            {
                if (!Spell.UseAOE || Battlegrounds.IsInsideBattleground || Unit.NearbyUnfriendlyUnits.Any(u => u.Guid != Me.CurrentTargetGuid && u.IsCrowdControlled()))
                    AoeCount = 1;
                else if (EventHandlers.TimeSinceAttackedByEnemyPlayer < TimeSpan.FromSeconds(30))
                    AoeCount = 1;
                else
                    AoeCount = Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < enemyRange(u));
                return RunStatus.Failure;
            });
        }

        public static bool HaveTalent(RogueTalents rogueTalents)
        {
            return TalentManager.IsSelected((int)rogueTalents);
        }

         
        internal static Composite CreateAttackFlyingOrUnreachableMobs()
        {
            return new Decorator(
                // changed to only do on non-player targets
                ret => {
                    if (!Me.GotTarget())
                        return false;

                    if (Me.CurrentTarget.IsPlayer)
                        return false;

                    if (Me.CurrentTarget.IsFlying)
                    {
                        Logger.Write( LogColor.Hilite, "{0} is Flying! using Ranged attack....", Me.CurrentTarget.SafeName());
                        return true;
                    }

                    if (Me.CurrentTarget.IsAboveTheGround())
                    {
                        Logger.Write( LogColor.Hilite, "{0} is {1:F1} yds above the ground! using Ranged attack....", Me.CurrentTarget.SafeName(), Me.CurrentTarget.HeightOffTheGround());
                        return true;
                    }

                    if (Me.CurrentTarget.Distance2DSqr < 5 * 5 && Math.Abs(Me.Z - Me.CurrentTarget.Z) >= 5)
                    {
                        Logger.Write( LogColor.Hilite, "{0} appears to be off the ground! using Ranged attack....", Me.CurrentTarget.SafeName());
                        return true;
                    }

                    WoWPoint dest = Me.CurrentTarget.Location;
                    if ( !Me.CurrentTarget.IsWithinMeleeRange && !Styx.Pathing.Navigator.CanNavigateFully( Me.Location, dest))
                    {
                        Logger.Write( LogColor.Hilite, "{0} is not Fully Pathable! trying ranged attack....", Me.CurrentTarget.SafeName());
                        return true;
                    }

                    return false;
                    },
                new PrioritySelector(
                    Spell.Cast("Deadly Throw", req => Me.ComboPoints > 0),
                    Spell.Cast("Shuriken Toss"),
                    Spell.Cast("Throw"),

                    // nothing else worked, so cancel stealth so we can proximity aggro
                    new Decorator(
                        ret => Me.HasAura("Stealth"),
                        new Sequence(
                            new Action(ret => Logger.Write( LogColor.Cancel, "/cancel Stealth")),
                            new Action(ret => Me.CancelAura("Stealth")),
                            new Wait(TimeSpan.FromMilliseconds(500), ret => !Me.HasAura("Stealth"), new ActionAlwaysSucceed())
                            )
                        )
                    )
                );
        }

        internal static Composite CreateStealthBehavior( SimpleBooleanDelegate requirements = null)
        {
            if (RogueSettings.Stealth == StealthMode.Never)
                return new ActionAlwaysFail();

            requirements = requirements ?? (req => true);

            BehaviorType createdByBehavior = Dynamics.CompositeBuilder.CurrentBehaviorType;
            SimpleBooleanDelegate needStealth = 
                req => 
                {
                    bool isStealthAllowed = false;
                    if (RogueSettings.Stealth == StealthMode.Always)
                        isStealthAllowed = true;
                    else if (RogueSettings.Stealth == StealthMode.Auto && (createdByBehavior == BehaviorType.Pull || createdByBehavior == BehaviorType.PullBuffs))
                        isStealthAllowed = true;
                    else if (RogueSettings.Stealth == StealthMode.PVP)
                    {
                        if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                            isStealthAllowed = true;
                        else if (StyxWoW.Me.GotTarget() && StyxWoW.Me.CurrentTarget.IsPlayer && Unit.ValidUnit(StyxWoW.Me.CurrentTarget))
                            isStealthAllowed = true;
                        else if (BotPoi.Current.Type == PoiType.Kill && BotPoi.Current.AsObject is WoWPlayer)
                            isStealthAllowed = true;
                    }

                    if (isStealthAllowed)
                    {
                        if (requirements(req) && !AreStealthAbilitiesAvailable && !Me.GetAllAuras().Any(a => a.IsHarmful))
                        {
                            return true;
                        }
                    }
                    return false;
                };

            string createdBy = createdByBehavior.ToString() + " " + Dynamics.CompositeBuilder.CurrentBehaviorName;
            return new Sequence(
                ctx => needStealth(ctx),

                //// no stealth? then throttle message
                //new DecoratorContinue(
                //    req => ! (bool) req && !Me.Mounted && !AreStealthAbilitiesAvailable,
                //    new SeqDbg( 1, s => string.Format("CreateStealthBehavior: need = {0}, called by {1}", ((bool)s).ToYN(), createdBy))
                //    ),

                Spell.BuffSelf(
                    "Stealth", 
                    req => 
                    {
                        bool need = (bool) req;
                        if (!need)
                            return false;

                        // yes stealth? message throttled by virtue of buff logic
                        Logger.WriteDebug("CreateStealthBehavior: need = {0}, called by {1}", need.ToYN(), createdBy);
                        if (!Spell.CanCastHack("Stealth"))
                            return false;
                        return true;
                    }),
                // now wait until we can Sap, Pick Pocket, etc...
                new Wait( TimeSpan.FromMilliseconds(500), ret => AreStealthAbilitiesAvailable, new ActionAlwaysSucceed())
                );
        }

        public static HashSet<uint> mobEntryWithNoPockets = new HashSet<uint>();
        public static Composite CreateRoguePickPocket()
        {
            if (!RogueSettings.UsePickPocket)
            {
                return new ActionAlwaysFail();
            }
            
            // don't create behavior if pick pocket in combat not enabled
            if (!RogueSettings.AllowPickPocketInCombat && (Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Combat || Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.CombatBuffs))
            {
                return new ActionAlwaysFail();
            }

            // issue following messagess only for Pull Behavior
            if (Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull)
            {
                if (!AutoLootIsEnabled())
                {
                    Logger.Write( LogColor.Init, "warning:  Auto Loot is off, so Pick Pocket disabled - to allow Pick Pocket by Singular, enable your Auto Loot setting");
                    return new ActionAlwaysFail();
                }
            }

            return new Throttle(5,
                new Decorator(
                    ret => (!Me.Combat || RogueSettings.AllowPickPocketInCombat)
                        && AreStealthAbilitiesAvailable
                        && Me.GotTarget()
                        && Me.CurrentTarget.IsAlive
                        && !Me.CurrentTarget.IsPlayer
                        && (Me.CurrentTarget.IsWithinMeleeRange || (TalentManager.HasGlyph("Pick Pocket") && Me.CurrentTarget.SpellDistance() < 10))
                        && IsMobPickPocketable(Me.CurrentTarget),
                    new Sequence(
                        ctx => Me.CurrentTarget, 
                        new Action( r => { StopMoving.Now(); } ),
                        new Wait( TimeSpan.FromMilliseconds(500), until => !Me.IsMoving, new ActionAlwaysSucceed()),
                        new WaitContinue(TimeSpan.FromMilliseconds(RogueSettings.PrePickPocketPause), req => false, new ActionAlwaysSucceed()),
                        Spell.Cast("Pick Pocket", on => (WoWUnit) on),
                        new WaitContinue( TimeSpan.FromMilliseconds( RogueSettings.PostPickPocketPause), req => false, new ActionAlwaysSucceed()),
                        new Action(r => Blacklist.Add(Me.CurrentTarget, BlacklistFlags.Node, TimeSpan.FromMinutes(RogueSettings.SuccessfulPostPickPocketBlacklistMinutes), string.Format("Singular: do not pick pocket {0} again for {1}", ((WoWUnit)r).SafeName(), RogueSettings.SuccessfulPostPickPocketBlacklistMinutes))),
                        new ActionAlwaysFail() // not on the GCD, so fail
                        )
                    )
                );
        }

        private static bool IsMobPickPocketable(WoWUnit unit)
        {
            return (unit.IsHumanoid || unit.IsUndead)
                && !mobEntryWithNoPockets.Contains(unit.Entry)
                && !Blacklist.Contains(unit, BlacklistFlags.Node);
        }

        public static Composite CreateRogueFeintBehavior()
        {
            return Spell.Cast("Feint",
                                        ret => Me.CurrentTarget.ThreatInfo.RawPercent > 80
                                            && Me.IsInGroup()
                                            && Group.AnyTankNearby);
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

        private static WoWItem box;

        public static Composite CreateRogueOpenBoxes()
        {
            return new Decorator(
                ret => RogueSettings.UsePickLock 
                    && !Me.IsFlying 
                    && !Me.Mounted 
                    && !AreStealthAbilitiesAvailable 
                    && SpellManager.HasSpell("Pick Lock") 
                    && AutoLootIsEnabled()
                    && !MerchantFrame.Instance.IsVisible,

                new PrioritySelector(
                    // open unlocked box
                    new Sequence(
                        new Action(r => { box = FindUnlockedBox(); return box == null ? RunStatus.Failure : RunStatus.Success; }),
                        new Action(r => Logger.Write( LogColor.Hilite, "/open: unlocked {0} #{1}", box.Name, box.Entry)),
                        new Wait(2, ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(), new ActionAlwaysSucceed()),
                        new Action(r => Logger.WriteDebug("openbox: no spell cast or gcd")),
                        new Action(r => box.UseContainerItem()),
                        new Action(r => Logger.WriteDebug("openbox: box now opened")),
                        new Action(r => Blacklist.Add(box.Guid, BlacklistFlags.Loot, TimeSpan.FromMinutes(30), "Singular: to prevent redundant open attempt")),
                        Helpers.Common.CreateWaitForLagDuration()
                        ),
                    // pick lock on a locked box
                    new Sequence(
                        new Action( r => { box = FindLockedBox();  return box == null ? RunStatus.Failure : RunStatus.Success; }),
                        new PrioritySelector(
                            Movement.CreateEnsureMovementStoppedBehavior(reason: "to Pick Lock"),
                            new ActionAlwaysSucceed()
                            ),
                        new Action(r => Logger.Write( LogColor.Hilite, "/pick lock: {0} #{1}", box.Name, box.Entry)),
                        new Action( r => { return Spell.CastPrimative( "Pick Lock", Me) ? RunStatus.Success : RunStatus.Failure; }),
                        new Action( r => Logger.WriteDebug( "picklock: wait for spell on cursor")),
                        new Wait( 1, ret => Spell.GetPendingCursorSpell != null, new ActionAlwaysSucceed()),
                        new Action( r => Logger.WriteDebug( "picklock: use item")),
                        new Action( r => box.Use() ),
                        new Action(r => Blacklist.Add(box.Guid, BlacklistFlags.Node, TimeSpan.FromSeconds(30), "Singular: to prevent redundant pick lock attempt")),
                        new Action(r => Logger.WriteDebug("picklock: wait for spell in progress")),
                        new Wait( 1, ret => Spell.IsCastingOrChannelling(), new ActionAlwaysSucceed()),
                        new Action( r => Logger.WriteDebug( "picklock: wait for spell to complete")),
                        new Wait( 6, ret => !Spell.IsCastingOrChannelling(), new ActionAlwaysSucceed()),
                        Helpers.Common.CreateWaitForLagDuration()
                        )
                    )
                );
        }

        private static bool AutoLootIsEnabled()
        {
            List<string> option = Lua.GetReturnValues("return GetCVar(\"AutoLootDefault\")");
            return option != null && !string.IsNullOrEmpty(option[0]) && option[0] == "1";
        }

        // following code not referenced
        //
        //private static bool AutoSelfCastIsEnabled()
        //{
        //    List<string> option = Lua.GetReturnValues("return GetCVar(\"autoSelfCast\")");
        //    return option != null && !string.IsNullOrEmpty(option[0]) && option[0] == "1";
        //}

        internal static bool UseLongCoolDownAbility
        {
            get
            {
                if (!Me.GotTarget() || !Me.CurrentTarget.IsWithinMeleeRange )
                    return false;

                if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                    return Me.CurrentTarget.IsBoss();

                if (Me.CurrentTarget.IsPlayer && Me.CurrentTarget.TimeToDeath() > 3)
                    return true;

                if (Me.CurrentTarget.TimeToDeath() > 20)
                    return true;

                return Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any(u => u.Guid != Me.CurrentTargetGuid && !u.IsPet && u.IsWithinMeleeRange );
            }
        }

        public static WoWItem FindLockedBox()
        {
            if (Me.CarriedItems == null)
                return null;

            return Me.CarriedItems
                .FirstOrDefault(b => b != null && b.IsValid && b.ItemInfo != null
                    && b.ItemInfo.ItemClass == WoWItemClass.Miscellaneous
                    // && b.ItemInfo.ContainerClass == WoWItemContainerClass.Container
                    && b.ItemInfo.MiscClass == WoWItemMiscClass.Junk
                    && b.ItemInfo.Level <= Me.Level
                    && !b.IsOpenable
                    && b.Usable
                    && b.Cooldown <= 0
                    && !Blacklist.Contains(b.Guid, BlacklistFlags.Node)
                    && _boxes.ContainsKey(b.Entry)
                    && (_boxes[b.Entry] <= 0 || _boxes[b.Entry] <= (Me.Level * 5)));
        }

        public static WoWItem FindUnlockedBox()
        {
            if (Me.CarriedItems == null)
                return null;

            return Me.CarriedItems
                .FirstOrDefault(b => b != null && b.IsValid && b.ItemInfo != null
                    && b.ItemInfo.ItemClass == WoWItemClass.Miscellaneous
                    // && b.ItemInfo.ContainerClass == WoWItemContainerClass.Container
                    && b.ItemInfo.MiscClass == WoWItemMiscClass.Junk
                    && b.IsOpenable
                    && b.Usable
                    && b.Cooldown <= 0
                    && !Blacklist.Contains(b.Guid, BlacklistFlags.Loot)
                    && _boxes.ContainsKey(b.Entry));
        }

        /// <summary>
        /// following collection added to work around bugs in return values for Lockboxes
        /// currently, Titanium Lockbox returns it requires Level 78.  Its lockpick 
        /// skill level required is 400, which a rogue gets at 80.  Since the Skill Level
        /// reported for this item is 0, this prevents spamming picklock attempts on
        /// a box that a 78-79 rogue cannot open
        /// </summary>
        private static Dictionary<uint, int> _boxes = new Dictionary<uint, int>()
        {
            { 4632, 1 },        // Ornate Bronze Lockbox
            { 6354, 1 },	    // Small Locked Chest
            { 16882, 1 },	    // Battered Junkbox
            { 4633, 25 },	    // Heavy Bronze Lockbox
            { 4634, 70 },	    // Iron Lockbox
            { 6355, 30 },	    // Sturdy Locked Chest
            { 16883, 70 },	    // Worn Junkbox
            { 4636, 125 },	    // Strong Iron Lockbox
            { 4637, 175 },	    // Steel Lockbox
            { 16884, 175 },	    // Sturdy Junkbox
            { 4638, 225 },	    // Reinforced Steel Lockbox
            { 13875, 175 },	    // Ironbound Locked Chest
            { 5758, 225 },	    // Mithril Lockbox
            { 5759, 225 },	    // Thorium Lockbox
            { 13918, 250 },	    // Reinforced Locked Chest
            { 5760, 225 },	    // Eternium Lockbox
            { 12033, 275 },     // Thaurissan Family Jewels
            { 16885, 250 },     // Heavy Junkbox
            { 29569, 300 },	    // Strong Junkbox
            { 31952, 325 },	    // Khorium Lockbox
            { 43575, 350 },	    // Reinforced Junkbox
            { 43622, 375 },	    // Froststeel Lockbox
            { 43624, 400 },	    // Titanium Lockbox
            { 45986, 400 },	    // Tiny Titanium Lockbox
            { 63349, 400 },	    // Flame-Scarred Junkbox
            { 68729, 425 },	    // Elementium Lockbox
            { 88567, 450 },	    // Ghost Iron Lockbox
            { 88165, 450 },	    // Vine-Cracked Junkbox
            { 116920, 500 },    // True Steel Lockbox
        };



        internal static Composite CreateRoguePullSkipNonPickPocketableMob()
        {
            return new Action( r => {
                if (RogueSettings.UsePickPocket && RogueSettings.PickPocketOnlyPull && !Me.IsInGroup())
                {
                    if (!Me.GotTarget())
                        return RunStatus.Success;

                    if (Me.CurrentTarget.IsPlayer)
                        return RunStatus.Success;

                    if (BotPoi.Current.Type == PoiType.Kill && !StyxWoW.Me.Combat && Blacklist.Contains(BotPoi.Current.Guid, BlacklistFlags.Node | BlacklistFlags.Pull))
                    {
                        if (StyxWoW.Me.CurrentTargetGuid == BotPoi.Current.Guid)
                        {
                            Logger.WriteDebug("SkipMob: ClearTarget - pickpocket only and target already picked");
                            Me.ClearTarget();
                        }

                        WoWUnit unit = BotPoi.Current.AsObject.ToUnit();
                        if (unit != null && unit.IsValid)
                            BotPoi.Clear(string.Format("Singular: pickpocket only and {0} is in Blacklist", unit.SafeName()));
                        else
                            Styx.CommonBot.POI.BotPoi.Clear("Singular: pickpocket only and invalid mob in Blacklist");

                        return RunStatus.Success;
                    }

                    if (!Me.CurrentTarget.IsHumanoid && !Me.CurrentTarget.IsUndead)
                    {
                        if (!Blacklist.Contains(Me.CurrentTarget.Guid, BlacklistFlags.Pull))
                            Blacklist.Add(Me.CurrentTarget.Guid, BlacklistFlags.Pull, TimeSpan.FromSeconds(180), "Singular: cannot Pick Pocket this mob");

                        if (Me.CurrentTarget.Guid == BotPoi.Current.Guid)
                            BotPoi.Clear("Singular: cannot pick pocket this mob");

                        Me.ClearTarget();
                        return RunStatus.Success;
                    }
                }

                return RunStatus.Failure;
            });
        }

        internal static Composite RogueEnsureReadyToAttackFromMelee()
        {
            PrioritySelector prio = new PrioritySelector(
                Movement.CreatePositionMobsInFront(),
                Safers.EnsureTarget(),
                Helpers.Common.CreatePetAttack(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior( 180, false),
                new Decorator(
                    req => Me.GotTarget() && Me.CurrentTarget.Distance < SingularSettings.Instance.MeleeDismountRange,
                    Helpers.Common.CreateDismount( Dynamics.CompositeBuilder.CurrentBehaviorType.ToString())   // should be Pull or Combat 99% of the time
                    ),
                new Decorator(
                    req => !AreStealthAbilitiesAvailable,
                    Helpers.Common.CreateAutoAttack()
                    )
                );

            if (Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull)
            {
                prio.AddChild(
                    new PrioritySelector(
                        ctx => Me.GotTarget() && Me.CurrentTarget.IsAboveTheGround(),
                        new Decorator(
                            req => (bool)req,
                            new PrioritySelector(
                                Movement.CreateMoveToUnitBehavior(on => Me.CurrentTarget, 27, 22),
                                Movement.CreateEnsureMovementStoppedBehavior(22)
                                )
                            ),
                        new Decorator(
                            req => !(bool)req,
                            new PrioritySelector(
                                Movement.CreateMoveToMeleeBehavior(true),
                                Movement.CreateEnsureMovementStoppedWithinMelee()
                                )
                            )
                        )
                    );
            }
            else
            {
                prio.AddChild( Movement.CreateMoveToMeleeBehavior(true));
                prio.AddChild(Movement.CreateEnsureMovementStoppedWithinMelee());
            }

            return prio;
        }

        internal static Composite CreateRoguePullPickPocketButDontAttack()
        {
            if (!RogueSettings.PickPocketOnlyPull || !RogueSettings.UsePickPocket)
                return new ActionAlwaysFail();

            return new PrioritySelector(
                Common.CreateRoguePickPocket(),
                new Action(r => {
                    if (Blacklist.Contains(Me.CurrentTarget, BlacklistFlags.Node) && !Blacklist.Contains(Me.CurrentTarget, BlacklistFlags.Pull))
                        Blacklist.Add(Me.CurrentTarget, BlacklistFlags.Pull, TimeSpan.FromMinutes(3), "Singular: picked mobs pocket");
                    }),
                new ActionAlwaysSucceed()
                );
        }

        internal static Composite RogueEnsureWeKillSappedMobs()
        {
            return new Sequence(
                // try to get a current target (won't include those that are CCd
                Safers.EnsureTarget(),

                // if no target, try to force it to kill a nearby CCd mob
                new PrioritySelector(
                    ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HasAnyOfMyAuras("Blind", "Sap")),
                    new Decorator(
                        req => !Unit.NearbyUnitsInCombatWithUsOrOurStuff.Any(),
                        new Action(r =>
                        {
                            Logger.Write(LogColor.Hilite, "^Break Crowd Control: time to kill {0} @ {1:F1} yds", ((WoWUnit)r).SafeName(), ((WoWUnit)r).SpellDistance());
                            Logger.WriteDebug("RogueEnsureKillSapped: setting BotPoi to PoiType.Kill");
                            BotPoi.Current = new BotPoi((WoWUnit)r, PoiType.Kill);
                            return RunStatus.Success;
                        })
                        )
                    )
                );
        }
    }


    public enum RogueTalents
    {
        MasterPoisoner = 1,
        ElaboratePlanning,
        Hemorrhage,

        GhostlyStrike = MasterPoisoner,
        Swordmaster = ElaboratePlanning,
        QuickDraw =  Hemorrhage,

        MasterOfSubtlety = MasterPoisoner,
        Weaponmaster = ElaboratePlanning,
        Gloomblade = Hemorrhage,


        Nightstalker = 4,
        Subterfuge,
        ShadowFocus,

        GrapplingHook = Nightstalker,
        AcrobaticStrikes = Subterfuge,
        HitAndRun = ShadowFocus,


        DeeperStratagem = 7,
        Anticipation,
        Vigor,


        LeechingPoison = 10,
        Elusiveness,
        CheatDeath,

        IronStomach = LeechingPoison,

        SoothingDarkness = LeechingPoison,


        Thugggee = 13,
        PreyOnTheWeak,
        InternalBleeding,

        Parley = Thugggee,
        DirtyTricks = InternalBleeding,

        StrikeFromTheShadows = Thugggee,
        TangledShadow = InternalBleeding,


        AgonizingPoison = 16,
        Alacrity,
        Exsanguinate,

        CannonballBarrage = AgonizingPoison,
        KillingSpree = Exsanguinate,

        Premeditation = AgonizingPoison,
        EnvelopingShadows = Exsanguinate,


        VenomRush = 19,
        MarkedForDeath,
        DeathFromAbove,

        SliceAndDice = VenomRush,

        MasterOfShadows = VenomRush
    }
}
