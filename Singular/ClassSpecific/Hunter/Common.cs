using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Drawing;
using Styx.CommonBot.POI;
using Styx.Common.Helpers;
using System.Collections.Generic;

namespace Singular.ClassSpecific.Hunter
{
    public class Common
    {
        #region Common

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WoWUnit Pet { get { return StyxWoW.Me.Pet; } }
        private static WoWUnit Target { get { return StyxWoW.Me.CurrentTarget; } }
        private static HunterSettings HunterSettings { get { return SingularSettings.Instance.Hunter(); } }
        private static bool HasTalent(HunterTalents tal) { return TalentManager.IsSelected((int)tal); }

        #endregion

        #region Manage Growl for Instances

        static Common()
        {
            // Lets hook this event so we can disable growl
            SingularRoutine.OnWoWContextChanged += SingularRoutine_OnWoWContextChanged;
        }

        // Disable pet growl in instances but enable it outside.
        static void SingularRoutine_OnWoWContextChanged(object sender, WoWContextEventArg e)
        {
            Lua.DoString(e.CurrentContext == WoWContext.Instances
                             ? "DisableSpellAutocast(GetSpellInfo(2649))"
                             : "EnableSpellAutocast(GetSpellInfo(2649))");
        }

        #endregion

        [Behavior(BehaviorType.Rest, WoWClass.Hunter)]
        public static Composite CreateHunterRest()
        {
            return new PrioritySelector(
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCastOrChannel(),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.Buff("Mend Pet", onUnit => Me.Pet, req => Me.GotAlivePet && Pet.HealthPercent < 85),

                        Singular.Helpers.Rest.CreateDefaultRestBehaviour(),

                        CreateHunterCallPetBehavior(true),

                        new Decorator(ctx => SingularSettings.Instance.DisablePetUsage && Me.GotAlivePet,
                            new Sequence(
                                new Action(ctx => SpellManager.Cast("Dismiss Pet")),
                                new WaitContinue(1, ret => !Me.GotAlivePet, new ActionAlwaysSucceed())
                                )
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Hunter)]
        public static Composite CreateHunterPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.BuffSelf("Track Hidden"),
                        Spell.BuffSelf("Aspect of the Hawk", ret => !Me.IsMoving && !Me.HasAnyAura("Aspect of the Hawk", "Aspect of the Iron Hawk")),

                        Spell.Buff("Mend Pet", onUnit => Me.Pet, req => Me.GotAlivePet && Pet.HealthPercent < 85),
                        CreateHunterCallPetBehavior(true)
                        )
                    )
                );
        }


        [Behavior(BehaviorType.PullBuffs, WoWClass.Hunter, (WoWSpec)int.MaxValue, WoWContext.Battlegrounds)]
        public static Composite CreateHunterPullBuffsBattlegrounds()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        /*
                        Spell.Buff("Hunter's Mark", 
                            ret => Unit.ValidUnit(Target) 
                                && !TalentManager.HasGlyph("Marked for Death")
                                && !Unit.NearbyUnfriendlyUnits.Any( u => u.Guid != Target.Guid))
                         */
                        )
                    )
                );
        }

        [Behavior(BehaviorType.PullBuffs, WoWClass.Hunter, (WoWSpec)int.MaxValue, WoWContext.Normal | WoWContext.Instances )]
        public static Composite CreateHunterPullBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateMisdirectionBehavior( buffForPull: true)
                        
                        // , Spell.Buff("Hunter's Mark", ret => Target != null && Unit.ValidUnit(Target) && !TalentManager.HasGlyph("Marked for Death") && !Me.CurrentTarget.IsImmune(WoWSpellSchool.Arcane))
                        )
                    )
                );
        }

        private static bool ScaryNPC
        {
            get
            {
                return Target.MaxHealth > (StyxWoW.Me.MaxHealth * 3) && !Me.IsInInstance;
            }
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Hunter)]
        public static Composite CreateHunterCombatBuffs()
        {
            return new PrioritySelector(
                // Normal context, use FD at low-health
                new Decorator(
                    ret => SingularRoutine.CurrentWoWContext == WoWContext.Normal,
                    CreateFeignDeath(() => TimeSpan.FromSeconds(10), cancel => !Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < 25))
                    ),

                // in battlegrounds, only FD when we have 2 or more player pets attacking us
                new Decorator(
                    ret => SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds,
                    CreateFeignDeath(() => TimeSpan.FromSeconds((new Random()).Next(3)), ret => false)
                    ),

                // cast Pet survival abilities
                new Decorator(
                    ret => Me.GotAlivePet && (Pet.HealthPercent < 35 || Pet.HealthPercent < 60 && Unit.NearbyUnfriendlyUnits.Count(u => u.CurrentTargetGuid == Pet.Guid) >= 2),
                    new PrioritySelector(
                        new Decorator(ret => PetManager.CanCastPetAction("Bullheaded"), new Action(ret => PetManager.CastPetAction("Bullheaded"))),
                        new Decorator(ret => PetManager.CanCastPetAction("Last Stand"), new Action(ret => PetManager.CastPetAction("Last Stand")))
                        )
                    ),

                new Throttle(2, Spell.Buff("Mend Pet", onUnit => Pet, ret => Me.GotAlivePet && Pet.HealthPercent < HunterSettings.MendPetPercent)),

                Common.CreateHunterCallPetBehavior(true),

                Spell.Buff("Deterrence",
                    ret => (Me.HealthPercent < 30 || 3 <= Unit.NearbyUnfriendlyUnits.Count(u => u.Combat && u.CurrentTargetGuid == Me.Guid))
                        && TalentManager.HasGlyph("Mirrored Blades")),

                Spell.BuffSelf("Aspect of the Hawk", ret => !Me.IsMoving && !Me.HasAnyAura("Aspect of the Hawk", "Aspect of the Iron Hawk")),

                new Decorator(
                    ret => SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds,
                    CreateMisdirectionBehavior()
                    ),

                // don't use Hunter's Mark in Battlegrounds unless soloing someone
                /*                        Spell.Buff("Hunter's Mark",
                                    ret => Unit.ValidUnit(Target)
                                        && !TalentManager.HasGlyph("Marked for Death")
                                        && (SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds || !Unit.NearbyUnfriendlyUnits.Any(u => u.Guid != Target.Guid))
                                        && !Me.CurrentTarget.IsImmune(WoWSpellSchool.Arcane)),
                */
                Spell.BuffSelf("Exhilaration", ret => Me.HealthPercent < 35 || (Pet != null && Pet.HealthPercent < 25)),

                Spell.Buff("Widow Venom", ret => HunterSettings.UseWidowVenom && Target.IsPlayer && Me.IsSafelyFacing(Target) && Target.InLineOfSpellSight),

                // Buffs - don't stack Bestial Wrath and Rapid Fire
                Spell.Buff("Bestial Wrath", true, ret => Spell.GetSpellCooldown("Kill Command") == TimeSpan.Zero && !Me.HasAura("Rapid Fire"), "The Beast Within"),

                Spell.Cast("Stampede",
                    ret => PartyBuff.WeHaveBloodlust
                        || (!Me.IsInGroup() && SafeArea.AllEnemyMobsAttackingMe.Count() > 2)
                        || (Me.GotTarget && Me.CurrentTarget.IsPlayer && Me.CurrentTarget.ToPlayer().IsHostile)),

                // Level 75 Talents
                Spell.Cast("A Murder of Crows"),
                Spell.Cast("Blink Strike", on => Me.CurrentTarget, ret => Me.GotAlivePet && Me.Pet.SpellDistance(Me.CurrentTarget) < 40),
                Spell.Cast("Lynx Rush", ret => Pet != null && Unit.NearbyUnfriendlyUnits.Any(u => Pet.Location.Distance(u.Location) <= 10)),

                // Level 60 Talents
                Spell.Cast("Dire Beast"),
                Spell.Cast("Fervor", ctx => Me.CurrentFocus < 50),

                // Level 90 Talents
                Spell.Cast("Glaive Toss"),
                Spell.Cast("Powershot"),
                Spell.Cast("Barrage"),

                // for long cooldowns, spend only when worthwhile                      
                new Decorator(
                    ret => Pet != null && Target != null && Target.IsAlive
                        && (Target.IsBoss() || Target.IsPlayer || ScaryNPC || 3 <= Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet)),
                    new PrioritySelector(
                        Spell.Buff("Rapid Fire", ret => !Me.HasAura("The Beast Within")),
                        Spell.Cast("Rabid", ret => Me.HasAura("The Beast Within")),
                        Spell.Cast("Readiness", ret =>
                        {
                            bool readyForReadiness = true;
                            if (SpellManager.HasSpell("Bestial Wrath"))
                                readyForReadiness = readyForReadiness && Spell.GetSpellCooldown("Bestial Wrath").TotalSeconds.Between(5, 50);
                            if (SpellManager.HasSpell("Rapid Fire"))
                                readyForReadiness = readyForReadiness && Spell.GetSpellCooldown("Rapid Fire").TotalSeconds.Between(30, 165);
                            return readyForReadiness;
                        })
                        )
                    )
                );
        }


        #region Traps

        public static Composite CreateHunterTrapBehavior(string trapName, bool useLauncher, UnitSelectionDelegate onUnit, SimpleBooleanDelegate req = null)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => onUnit != null && onUnit(ret) != null 
                        && (req == null || req(ret))
                        && onUnit(ret).DistanceSqr < (40*40) 
                        && SpellManager.HasSpell(trapName) && Spell.GetSpellCooldown(trapName) == TimeSpan.Zero,
                    new Sequence(
                        new Action(ret => Logger.WriteDebug("Trap: use trap launcher requested: {0}", useLauncher )),
                        new PrioritySelector(
                            new Decorator(ret => useLauncher && Me.HasAura("Trap Launcher"), new ActionAlwaysSucceed()),
                            Spell.BuffSelf("Trap Launcher", ret => useLauncher ),
                            new Decorator(ret => !useLauncher, new Action( ret => Me.CancelAura( "Trap Launcher")))
                            ),
                        // new Wait(TimeSpan.FromMilliseconds(500), ret => !useLauncher && Me.HasAura("Trap Launcher"), new ActionAlwaysSucceed()),
                        new Wait(TimeSpan.FromMilliseconds(500),
                            ret => (!useLauncher && !Me.HasAura("Trap Launcher"))
                                || (useLauncher && Me.HasAura("Trap Launcher")),
                            new ActionAlwaysSucceed()),
                        new Action(ret => Logger.WriteDebug("Trap: launcher aura present = {0}", Me.HasAura("Trap Launcher"))),
                        new Action(ret => Logger.WriteDebug("Trap: cancast = {0}", SpellManager.CanCast(trapName, onUnit(ret)))),
                        
                        new Action(ret => Logger.Write( Color.PowderBlue, "^{0} trap: {1} on {2}", useLauncher ? "Launch" : "Set", trapName, onUnit(ret).SafeName())),
                        // Spell.Cast( trapName, ctx => onUnit(ctx)),
                        new Action(ret => SpellManager.Cast(trapName, onUnit(ret))),

                        Helpers.Common.CreateWaitForLagDuration(),
                        new Action(ctx => SpellManager.ClickRemoteLocation(onUnit(ctx).Location)),
                        new Action(ret => Logger.WriteDebug("Trap: Complete!"))
                        )
                    )
                );
        }

        public static Composite CreateHunterTrapOnAddBehavior(string trapName)
        {
            return new PrioritySelector(
                ctx => Unit.NearbyUnfriendlyUnits.OrderBy(u => u.DistanceSqr)
                    .FirstOrDefault( u => u.Combat && u != Target && (!u.IsMoving || u.IsPlayer) && u.DistanceSqr < 40 * 40 && !u.IsCrowdControlled()),
                new Decorator(
                    ret => ret != null && SpellManager.HasSpell(trapName) && Spell.GetSpellCooldown(trapName) == TimeSpan.Zero ,
                    new Sequence(
                        new Action(ret => Logger.WriteDebug("AddTrap: make sure we have trap launcher")),
                        new PrioritySelector(
                            Spell.BuffSelf("Trap Launcher"),
                            new ActionAlwaysSucceed()
                            ),
                        new Wait( TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Trap Launcher"), new ActionAlwaysSucceed()),
                        new Action(ret => Logger.WriteDebug("AddTrap: launcher aura present = {0}", Me.HasAura("Trap Launcher"))),
                        new Action(ret => Logger.WriteDebug("AddTrap: cancast = {0}", SpellManager.CanCast(trapName, (WoWUnit)ret))),
                        // Spell.Cast( trapName, ctx => (WoWUnit) ctx),
                        new Action(ret => Logger.Write( Color.PowderBlue , "^Launch add trap: {0} on {1}", trapName, ((WoWUnit) ret).SafeName())),
                        new Action(ret => SpellManager.Cast( trapName, (WoWUnit) ret)),
                        Helpers.Common.CreateWaitForLagDuration(),
                        new Action(ctx => SpellManager.ClickRemoteLocation(((WoWUnit)ctx).Location)),
                        new Action(ret => Logger.WriteDebug("AddTrap: Complete!"))
                        )
                    )
                );
        }

        #endregion


        public static Composite CreateHunterCallPetBehavior(bool reviveInCombat)
        {
            return new Decorator(
                ret =>  !SingularSettings.Instance.DisablePetUsage 
                    && !Me.GotAlivePet 
                    && PetManager.PetSummonAfterDismountTimer.IsFinished 
                    && !Me.Mounted && !Me.OnTaxi,

                new PrioritySelector(

                    Spell.WaitForCast(),

                    new Action( r => {
                        Logger.WriteDebug("CreateHunterCallPetBehavior({0}):  {1}", reviveInCombat, Pet == null ? "no pet currently" : "pet is dead");
                        return RunStatus.Failure;
                        }),

                    // try instant rez for tenacity pets
                    new Decorator(
                        ret => Pet != null && !Pet.IsAlive && Me.Combat && reviveInCombat && SpellManager.CanCast("Heart of the Phoenix"),
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("CallPet: attempting Heart of the Phoenix")),
                            Spell.Cast("Heart of the Phoenix", mov => true, on => Me, req => true, cancel => false),
                            Helpers.Common.CreateWaitForLagDuration(),
                            new Wait(TimeSpan.FromMilliseconds(350), ret => Me.GotAlivePet, new ActionAlwaysSucceed())
                            )
                        ),

                    // try Revive always (since sometimes pet is dead and we don't get ptr to it)
                    new Throttle( 3, new Decorator(
                        ret => (Pet == null || !Pet.IsAlive) && (!Me.Combat || reviveInCombat),
                        new Sequence(
                            new PrioritySelector(
                                Movement.CreateEnsureMovementStoppedBehavior(reason:"to call pet"),
                                new ActionAlwaysSucceed()
                                ),
                            new Action(ret => Logger.WriteDebug("CallPet: attempting Revive Pet - cancast={0}", SpellManager.CanCast("Revive Pet"))),
                            Spell.Cast("Revive Pet", mov => true, on => Me, req => true, cancel => Me.GotAlivePet ),
                            Helpers.Common.CreateWaitForLagDuration(),
                            new Wait(TimeSpan.FromMilliseconds(500), ret => Me.GotAlivePet, new ActionAlwaysSucceed())
                            )
                        )),

                    // lastly, we Call Pet
                    new Decorator(
                        ret => Pet == null,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("CallPet: attempting Call Pet {0} - cancast={1}", HunterSettings.PetNumber, SpellManager.CanCast("Call Pet " + HunterSettings.PetNumber.ToString(), Pet))),
                            // new Action(ret => PetManager.CallPet(HunterSettings.PetNumber.ToString())),
                            Spell.Cast(ret => "Call Pet " + HunterSettings.PetNumber.ToString(), on => Me),

                            Helpers.Common.CreateWaitForLagDuration(),
                            new WaitContinue(1, ret => Me.GotAlivePet, new ActionAlwaysSucceed())
#if DONT_APPEAR_TO_NEED
                            ,
                            new Decorator(
                                ret => !Me.GotAlivePet && (!Me.Combat || reviveInCombat),
                                new Sequence(
                                    new Action( ret => Logger.WriteDebug( "CallPet: attempting fall through Revive Pet - cancast={0}", SpellManager.CanCast("Revive Pet", Pet))),
                                    Spell.BuffSelf("Revive Pet")
                                    )
                                )
#endif
                            )
                        )
                    )
                );
        }

        #region Avoidance and Disengage

        /// <summary>
        /// creates a Hunter specific avoidance behavior based upon settings.  will check for safe landing
        /// zones before using disengage or rocket jump.  will additionally do a running away or jump turn
        /// attack while moving away from attacking mob if behaviors provided
        /// </summary>
        /// <param name="nonfacingAttack">behavior while running away (back to target - instants only)</param>
        /// <param name="jumpturnAttack">behavior while facing target during jump turn (instants only)</param>
        /// <returns></returns>
        public static Composite CreateHunterAvoidanceBehavior(Composite nonfacingAttack, Composite jumpturnAttack)
        {
            Kite.CreateKitingBehavior(null, nonfacingAttack, jumpturnAttack);

            return new Decorator(
                ret => MovementManager.IsClassMovementAllowed,
                new PrioritySelector(
                    new Decorator(
                        ret => HunterSettings.UseDisengage, 
                        new PrioritySelector(
                            Disengage.CreateDisengageBehavior("Disengage", Disengage.Direction.Backwards, 20, CreateSlowMeleeBehaviorForDisengage()),
                            Disengage.CreateDisengageBehavior("Rocket Jump", Disengage.Direction.Frontwards, 20, CreateSlowMeleeBehavior())
                            )
                        ),
                    new Decorator(
                        ret => Common.NextDisengageAllowed <= DateTime.Now && HunterSettings.AllowKiting,
                        Kite.BeginKitingBehavior()
                        )
                    )
                );
        }

        private static bool useRocketJump;
        private static WoWUnit mobToGetAwayFrom;
        private static WoWPoint origSpot;
        private static WoWPoint safeSpot;
        private static float needFacing;
        public static DateTime NextDisengageAllowed = DateTime.Now;

        public static Composite CreateDisengageBehavior()
        {
            return
                new Decorator(
                    ret => IsDisengageNeeded(),
                    new Sequence(
                        new ActionDebugString(ret => "face away from or towards safespot as needed"),
                        new Action(ret =>
                        {
                            origSpot = new WoWPoint( Me.Location.X, Me.Location.Y, Me.Location.Z);
                            if (useRocketJump)
                                needFacing = Styx.Helpers.WoWMathHelper.CalculateNeededFacing(Me.Location, safeSpot);
                            else
                                needFacing = Styx.Helpers.WoWMathHelper.CalculateNeededFacing(safeSpot, Me.Location);

                            needFacing = WoWMathHelper.NormalizeRadian(needFacing);
                            float rotation = WoWMathHelper.NormalizeRadian(Math.Abs(needFacing - Me.RenderFacing));
                            Logger.WriteDebug(Color.Cyan, "DIS: turning {0:F0} degrees {1} safe landing spot",
                                WoWMathHelper.RadiansToDegrees(rotation), useRocketJump ? "towards" : "away from");
                            Me.SetFacing(needFacing);
                        }),

                        new ActionDebugString(ret => "wait for facing to complete"),
                        new PrioritySelector(
                            new Wait(new TimeSpan(0, 0, 1), ret => Me.IsDirectlyFacing(needFacing), new ActionAlwaysSucceed()),
                            new Action(ret =>
                            {
                                Logger.WriteDebug(Color.Cyan, "DIS: timed out waiting to face safe spot - need:{0:F4} have:{1:F4}", needFacing, Me.RenderFacing);
                                return RunStatus.Failure;
                            })
                            ),

                        // stop facing
                        new Action(ret =>
                        {
                            Logger.WriteDebug(Color.Cyan, "DIS: cancel facing now we point the right way");
                            WoWMovement.StopFace();
                        }),

                        new PrioritySelector(
                            new Sequence(
                                new ActionDebugString(ret => "attempting to slow"),
                                CreateSlowMeleeBehavior(),
                                new WaitContinue( 1, rdy => !Me.IsCasting && !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed())
                                ),
                            new ActionAlwaysSucceed()
                            ),

                        new ActionDebugString(ret => "set time of disengage just prior"),
                        new Sequence(
                            new PrioritySelector(
                                    new Decorator(ret => !useRocketJump, Spell.BuffSelf("Disengage")),
                                    new Decorator(ret => useRocketJump, Spell.BuffSelf("Rocket Jump")),
                                    new Action(ret => {
                                        Logger.WriteDebug(Color.Cyan, "DIS: {0} cast appears to have failed", useRocketJump ? "Rocket Jump" : "Disengage");
                                        return RunStatus.Failure;
                                        })
                                    ),
                            new WaitContinue( 1, req => !Me.IsAlive || !Me.IsFalling, new ActionAlwaysSucceed()),
                            new Action(ret =>
                            {
                                NextDisengageAllowed = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 750));
                                Logger.WriteDebug(Color.Cyan, "DIS: finished {0} cast", useRocketJump ? "Rocket Jump" : "Disengage");
                                Logger.WriteDebug(Color.Cyan, "DIS: jumped {0:F1} yds away from orig={1} to curr={2}", Me.Location.Distance(safeSpot), origSpot, Me.Location);
                                if (Kite.IsKitingActive())
                                    Kite.EndKiting(String.Format("BP: Interrupted by {0}", useRocketJump ? "Rocket Jump" : "Disengage"));
                                return RunStatus.Success;
                            })
                            )

                    )
                );
        }

        public static bool IsDisengageNeeded()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                return false;

            if (!Me.IsAlive || Me.IsFalling || Me.IsCasting)
                return false;

            if (Me.Stunned || Me.Rooted || Me.IsStunned() || Me.IsRooted())
                return false;

            if (NextDisengageAllowed > DateTime.Now)
                return false;

            useRocketJump = false;
            if (!SpellManager.CanCast("Disengage", Me, false, false))
            {
                if (!SingularSettings.Instance.UseRacials || Me.Race != WoWRace.Goblin || !SpellManager.CanCast("Rocket Jump", Me, false, false))
                    return false;

                useRocketJump = true;
            }

            mobToGetAwayFrom = SafeArea.NearestEnemyMobAttackingMe;
            if (mobToGetAwayFrom == null)
                return false;


            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal )
            {
                List<WoWUnit> attackers = SafeArea.AllEnemyMobsAttackingMe.ToList();
                if ((attackers.Sum( a => a.MaxHealth ) / 4) < Me.MaxHealth &&  Me.HealthPercent > 40)
                    return false;
            }
            else if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
            {
                switch (mobToGetAwayFrom.Class)
                {
                    default:
                        return false;

                    case WoWClass.DeathKnight:
                    case WoWClass.Druid:
                    case WoWClass.Monk:
                    case WoWClass.Paladin:
                    case WoWClass.Rogue:
                    case WoWClass.Shaman:
                        break;
                }
            }

            if (mobToGetAwayFrom.Distance > mobToGetAwayFrom.MeleeDistance() + 3f)
                return false;

            SafeArea sa = new SafeArea();
            sa.MinScanDistance = TalentManager.HasGlyph("Disengage") ? 21 : 16;    // average disengage distance on flat ground
            sa.MaxScanDistance = sa.MinScanDistance;
            sa.RaysToCheck = 36;
            sa.LineOfSightMob = Target;
            sa.MobToRunFrom = mobToGetAwayFrom;
            sa.CheckLineOfSightToSafeLocation = true;
            sa.CheckSpellLineOfSightToMob = false;

            safeSpot = sa.FindLocation();
            if (safeSpot == WoWPoint.Empty)
            {
                Logger.WriteDebug(Color.Cyan, "DIS: no safe landing spots found for {0}", useRocketJump ? "Rocket Jump" : "Disengage");
                return false;
            }

            Logger.WriteDebug(Color.Cyan, "DIS: Attempt safe {0} due to {1} @ {2:F1} yds",
                useRocketJump ? "Rocket Jump" : "Disengage",
                mobToGetAwayFrom.SafeName(),
                mobToGetAwayFrom.Distance);

            return true;
        }

        public static Composite CreateHunterBackPedal()
        {
            return
                new Decorator(
                    ret => MovementManager.IsClassMovementAllowed && Target.Distance <= Spell.MeleeRange + 5f &&
                           Target.IsAlive &&
                           (Target.CurrentTarget == null ||
                            Target.CurrentTarget != Me ||
                            Target.IsStunned()),
                    new Action(
                        ret =>
                        {
                            var moveTo = WoWMathHelper.CalculatePointFrom(Me.Location, Target.Location, Spell.MeleeRange + 10f);

                            if (Navigator.CanNavigateFully(Me.Location, moveTo))
                            {
                                Navigator.MoveTo(moveTo);
                                StopMoving.AtLocation(moveTo);
                                return RunStatus.Success;
                            }

                            return RunStatus.Failure;
                        }));
        }

        #endregion

        /// <summary>
        /// creates composite that buffs Misdirection on appropriate target.  always cast on Pet for Normal, never cast at all in PVP, 
        /// conditionally cast in Instances based upon parameter value
        /// </summary>
        /// <param name="buffForPull">applies to Instances only.  true = call is for pull behavior so allow use in instances; 
        /// false = disabled in instances</param>
        /// <returns></returns>
        public static Composite CreateMisdirectionBehavior(bool buffForPull = false)
        {
            // Normal - misdirect onto Pet on cooldown
            if ( SingularRoutine.CurrentWoWContext == WoWContext.Normal )
            {
                return new ThrottlePasses( 5,
                    new Decorator( 
                        ret => SingularRoutine.CurrentWoWContext == WoWContext.Normal && !Me.HasAura("Misdirection"),
                        new Sequence(
                            Spell.Cast("Misdirection", ctx => Me.Pet, req => Me.GotAlivePet && Pet.Distance < 100),
                            new ActionAlwaysFail()  // not on the GCD, so continue immediately
                            )
                        )
                    );
            }

            // Instances - misdirect only if pullCheck == true
            if (buffForPull && SingularRoutine.CurrentWoWContext == WoWContext.Instances)
            {
                return new ThrottlePasses(5,
                    new Decorator(
                        ret => buffForPull && SingularRoutine.CurrentWoWContext == WoWContext.Instances && !Me.HasAura("Misdirection"),
                        new Sequence(
                            Spell.Cast("Misdirection", on => Group.Tanks.FirstOrDefault(t => t.IsAlive && t.Distance < 100)),
                            new ActionAlwaysFail()  // not on the GCD, so continue immediately
                            )
                        )
                    );
            }

            return new ActionAlwaysFail();
        }

        private static Composite CreateSlowMeleeBehaviorForDisengage()
        {
            return new Decorator(
                ret => !HasTalent(HunterTalents.NarrowEscape),
                CreateSlowMeleeBehavior()
                );
        }

        private static Composite CreateSlowMeleeBehavior()
        {
            return new PrioritySelector(
                ctx => SafeArea.NearestEnemyMobAttackingMe,
                new Decorator(
                    ret => ret != null,
                    new PrioritySelector(
                        new Throttle( 2,
                            new PrioritySelector(
                                CreateHunterTrapBehavior("Ice Trap", false, onUnit => (WoWUnit)onUnit),
                                CreateHunterTrapBehavior("Snake Trap", false, onUnit => (WoWUnit)onUnit, ret => SpellManager.HasSpell("Entrapment")),
                                CreateHunterTrapBehavior("Explosive Trap", false, onUnit => (WoWUnit)onUnit, ret => TalentManager.HasGlyph("Explosive Trap")),
                                CreateHunterTrapBehavior("Freezing Trap", false, onUnit => (WoWUnit)onUnit, ret => SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds || Me.FocusedUnit == null)
                                )
                            ),
                        new Decorator(
                            ret => Me.IsSafelyFacing((WoWUnit)ret),
                            new PrioritySelector(
                                Spell.CastOnGround("Binding Shot", ret => ((WoWUnit)ret).Location, ret => true, false),
                                Spell.Cast("Concussive Shot", on => (WoWUnit)on),
                                Spell.Cast("Scatter Shot", on => (WoWUnit)on, ret => SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds || Me.FocusedUnit == null)
                                )
                            )
                        )
                    )
                );
        }

        private static Composite CreateFeignDeath(WaitGetTimeSpanTimeoutDelegate timeOut, SimpleBooleanDelegate cancel)
        {
            return new Decorator(
                ret => HunterSettings.UseFeignDeath,
                new Sequence(
                    Spell.BuffSelf("Feign Death", ret => 2 <= Unit.NearbyUnfriendlyUnits.Count(u => u.GotAlivePet && u.Pet.CurrentTargetGuid == Me.Guid)),
                    new Action(ret => waitToCancelFeignDeath = new WaitTimer(timeOut())),
                    new Action(ret => Logger.Write("... wait at most {0} seconds before cancelling Feign Death", (waitToCancelFeignDeath.EndTime - waitToCancelFeignDeath.StartTime).TotalSeconds)),
                    new Action(ret => waitToCancelFeignDeath.Reset()),
                    new WaitContinue(TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Feign Death"), new ActionAlwaysSucceed()),
                    new WaitContinue(360, ret => cancel(ret) || waitToCancelFeignDeath.IsFinished || !Me.HasAura("Feign Death"), new ActionAlwaysSucceed()),
                    new DecoratorContinue( 
                        ret => Me.HasAura( "Feign Death"),
                        new Sequence(
                            new Action(ret => Logger.Write("/cancel Feign Death after {0} seconds", (DateTime.Now - waitToCancelFeignDeath.StartTime).TotalSeconds)),
                            new Action(ret => Me.CancelAura("Feign Death"))
                            )
                        ),
                    new Action(ret => waitToCancelFeignDeath = null)
                    )
                );
        }



        /// <summary>
        /// workaround for Spell.Cast("Steady Shot").  fails continuously resulting
        /// in no casts because it never passes the SpellManager.CanCast() test.
        /// Had similar results with traps which are also directly cast because of it
        /// </summary>
        /// <param name="onUnit"></param>
        /// <returns></returns>
        public static Composite CastSteadyShot(UnitSelectionDelegate onUnit, SimpleBooleanDelegate req = null)
        {
            return new Decorator(
                ret => onUnit(ret) != null
                    && (req == null || req(ret))
                    && onUnit(ret).SpellDistance() < 40
                    && SpellManager.HasSpell("Steady Shot"),
                new Sequence(
                    new Action(ret => Logger.Write("Casting Steady Shot on {0}", onUnit(ret).SafeName())),
                    new Action(ret => SpellManager.Cast("Steady Shot", onUnit(ret)))
                    )
                );
        }

        private static WaitTimer waitToCancelFeignDeath;

        private static WoWUnit ccUnit = null;

        internal static Composite CreateHunterNormalCrowdControl()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                return new ActionAlwaysFail();

            return new PrioritySelector(
                new Action(ret =>
                {
                    if (!HunterSettings.CrowdControlFocus)
                        ccUnit = Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => NeedsNormalCrowdControl(u));
                    else if (Me.FocusedUnit != null && Unit.ValidUnit(Me.FocusedUnit) && NeedsNormalCrowdControl(Me.FocusedUnit))
                        ccUnit = Me.FocusedUnit;
                    else
                        ccUnit = null;
                    return RunStatus.Failure;
                }),
                new Decorator(
                    ret => ccUnit != null,
                    new Sequence(
                        new PrioritySelector(
                            Common.CreateHunterTrapBehavior("Freezing Trap", true, on => ccUnit),
                            Spell.Cast("Scatter Shot", on => ccUnit),
                            Spell.CastOnGround("Binding Shot", ret => ccUnit.Location, ret => true, false),
                            Common.CreateHunterTrapBehavior("Explosive Trap", true, on => ccUnit, ret => TalentManager.HasGlyph("Explosive Trap")),
                            Common.CreateHunterTrapBehavior("Snake Trap", true, on => ccUnit, ret => TalentManager.HasGlyph("Entrapment")),
                            Common.CreateHunterTrapBehavior("Ice Trap", true, on => ccUnit, ret => TalentManager.HasGlyph("Ice Trap")),
                            Spell.Cast("Concussive Shot", on => ccUnit)
                            ),
                        new Action(ret => { if (ccUnit != null) Blacklist.Add(ccUnit, BlacklistFlags.Combat, TimeSpan.FromSeconds(2)); })
                        )
                    )
                );
        }

        internal static Composite CreateHunterPvpCrowdControl()
        {
            return new PrioritySelector(
                new Action(ret =>
                {
                    if (!HunterSettings.CrowdControlFocus)
                        ccUnit = Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => NeedsPvpCrowdControl(u));
                    else if (Me.FocusedUnit != null && Unit.ValidUnit(Me.FocusedUnit) && NeedsPvpCrowdControl(Me.FocusedUnit))
                        ccUnit = Me.FocusedUnit;
                    else
                        ccUnit = null;
                    return RunStatus.Failure;
                }),
                new Decorator(
                    ret => ccUnit != null,
                    new Sequence(
                        new PrioritySelector(
                            Common.CreateHunterTrapBehavior("Freezing Trap", true, on => ccUnit),
                            Spell.Cast("Scatter Shot", on => ccUnit),
                            Spell.CastOnGround("Binding Shot", ret => ccUnit.Location, ret => true, false),
                            Common.CreateHunterTrapBehavior("Snake Trap", true, on => ccUnit, ret => TalentManager.HasGlyph("Entrapment")),
                            Common.CreateHunterTrapBehavior("Ice Trap", true, on => ccUnit, ret => TalentManager.HasGlyph("Ice Trap")),
                            Common.CreateHunterTrapBehavior("Explosive Trap", true, on => ccUnit, ret => TalentManager.HasGlyph("Explosive Trap")),
                            Spell.Cast("Concussive Shot", on => ccUnit)
                            ),
                        new Action(ret => { if (ccUnit != null) Blacklist.Add(ccUnit, BlacklistFlags.Combat, TimeSpan.FromSeconds(2)); })
                        )
                    )
                );
        }

        //
        private static bool NeedsNormalCrowdControl(WoWUnit u)
        {
            bool good = u.Combat 
                && u.IsTargetingMyPartyMember 
                && u.Distance <= 40 
                && !u.IsCrowdControlled()
                // && !u.HasAnyAura("Explosive Trap", "Ice Trap", "Freezing Trap", "Snake Trap")
                && u.Guid != Me.CurrentTargetGuid
                && !Blacklist.Contains(u.Guid, BlacklistFlags.Combat)                   
                && !Unit.NearbyFriendlyPlayers.Any( g => g.CurrentTargetGuid == u.Guid);
            return good;
        }

        //
        private static bool NeedsPvpCrowdControl(WoWUnit u)
        {
            bool good = u.Distance <= 40 && !u.IsCrowdControlled() && u.Guid != Me.CurrentTargetGuid && !Blacklist.Contains(u.Guid, BlacklistFlags.Combat);
            // && !Unit.NearbyGroupMembers.Any( g => g.CurrentTargetGuid == u.Guid);
            return good;
        }
    }

    enum HunterTalents
    {
        None = 0,
        Posthaste,
        NarrowEscape,
        CrouchingTiger,
        SilencingShot,
        WyvernSting,
        BlindingShot,
        Exhiliration,
        AspectOfTheIronHawk,
        SpiritBond,
        Fervor,
        DireBeast,
        ThrillOfTheHunt,
        MurderOfCrows,
        BlinkStrike,
        LynxRush,
        GlaiveToss,
        Powershot,
        Barrage
    }

}
