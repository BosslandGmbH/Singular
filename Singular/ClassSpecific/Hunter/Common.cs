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

namespace Singular.ClassSpecific.Hunter
{
    public class Common
    {
        #region Common

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WoWUnit Pet { get { return StyxWoW.Me.Pet; } }
        private static WoWUnit Target { get { return StyxWoW.Me.CurrentTarget; } }
        private static HunterSettings HunterSettings { get { return SingularSettings.Instance.Hunter; } }

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
                Spell.WaitForCast(),
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
                Spell.WaitForCast(true),
                Spell.BuffSelf("Track Hidden"),
                Spell.BuffSelf("Aspect of the Hawk", ret => !Me.IsMoving && !Me.HasAnyAura("Aspect of the Hawk", "Aspect of the Iron Hawk")),

                Spell.Buff("Mend Pet", onUnit => Me.Pet, req => Me.GotAlivePet && Pet.HealthPercent < 85),
                CreateHunterCallPetBehavior(true)
                );
        }

        [Behavior(BehaviorType.PullBuffs, WoWClass.Hunter)]
        public static Composite CreateHunterPullBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Spell.Cast("Misdirection",
                            ctx => Pet,
                            ret => Me.GotAlivePet
                                && !Me.HasAura("Misdirection")
                                && !Group.Tanks.Any(t => t.IsAlive && t.Distance < 100)),

                        new Throttle(Spell.Buff("Hunter's Mark", ret => Unit.ValidUnit(Target)))
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
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Common.CreateHunterCallPetBehavior(true),

                        Spell.BuffSelf("Aspect of the Hawk", ret => !Me.IsMoving && !Me.HasAnyAura("Aspect of the Hawk", "Aspect of the Iron Hawk")),

                        Spell.Cast("Misdirection",
                            ctx => Pet,
                            ret => Me.GotAlivePet
                                && !Me.HasAura("Misdirection")
                                && !Group.Tanks.Any(t => t.IsAlive && t.Distance < 100)),

                        new Throttle(Spell.Buff("Hunter's Mark", ret => Target != null && Unit.ValidUnit(Target))),

                        Spell.Buff("Mend Pet", onUnit => Pet, ret => Me.GotAlivePet && Pet.HealthPercent < HunterSettings.MendPetPercent),
                        Spell.BuffSelf("Exhilaration", ret => Me.HealthPercent < 30 || Pet.HealthPercent < 10 ),

                        // Buffs - don't stack the next two
                        Spell.Buff("Bestial Wrath", true, ret => Spell.GetSpellCooldown("Kill Command") == TimeSpan.Zero, "The Beast Within"),

                        Spell.Cast("Stampede", ret => PartyBuff.WeHaveBloodlust || !Me.IsInGroup()),

                        Spell.Cast("A Murder of Crows"),
                        Spell.Cast("Blink Strike", ctx => Me.GotAlivePet),
                        Spell.Cast("Lynx Rush", ret => Unit.NearbyUnfriendlyUnits.Any(u => Pet.Location.Distance(u.Location) <= 10)),

                        Spell.Cast("Glaive Toss"),
                        Spell.Cast("Powershot"),
                        Spell.Cast("Barrage"),

                        Spell.Cast("Dire Beast"),
                        Spell.Cast("Fervor", ctx => Me.FocusPercent < 50),

                        // for long cooldowns, spend only when worthwhile                      
                        new Decorator(
                            ret => Pet != null && Target != null && Target.IsAlive 
                                && (Target.IsBoss || Target.IsPlayer || ScaryNPC || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                            new PrioritySelector(
                                Spell.Buff("Rapid Fire", ret => !Me.HasAura("The Beast Within")),
                                Spell.Cast("Rabid", ret => Me.HasAura("The Beast Within")),
                                Spell.Buff("Readiness", ret => Spell.GetSpellCooldown("Rapid Fire").TotalSeconds > 30)
                                )
                            )
                        )
                    )
                );
        }


        public static Composite CreateHunterBackPedal()
        {
            return
                new Decorator(
                    ret => !SingularSettings.Instance.DisableAllMovement && Target.Distance <= Spell.MeleeRange + 5f &&
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
                                return RunStatus.Success;
                            }

                            return RunStatus.Failure;
                        }));
        }

        public static Composite CreateHunterTrapBehavior(string trapName)
        {
            return CreateHunterTrapBehavior(trapName, ret => Target);
        }

        public static Composite CreateHunterTrapBehavior(string trapName, bool useLauncher)
        {
            return CreateHunterTrapBehavior(trapName, useLauncher, ret => Target);
        }

        public static Composite CreateHunterTrapBehavior(string trapName, UnitSelectionDelegate onUnit)
        {
            return CreateHunterTrapBehavior(trapName, true, onUnit);
        }

        public static Composite CreateHunterTrapBehavior(string trapName, bool useLauncher, UnitSelectionDelegate onUnit)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => onUnit != null && onUnit(ret) != null && onUnit(ret).DistanceSqr < 40 * 40 &&
                           SpellManager.HasSpell(trapName) && !SpellManager.Spells[trapName].Cooldown,
                    new PrioritySelector(
                        Spell.BuffSelf(trapName, ret => !useLauncher),
                        Spell.BuffSelf("Trap Launcher", ret => useLauncher),
                        new Decorator(
                            ret => Me.HasAura("Trap Launcher"),
                            new Sequence(
                /*new Switch<string>(ctx => trapName,
                    new SwitchArgument<string>("Freezing Trap",
                        new Action(ret => SpellManager.CastSpellById(1499))),
                    new SwitchArgument<string>("Explosive Trap",
                        new Action(ret => SpellManager.CastSpellById(13813))),
                    new SwitchArgument<string>("Ice Trap",
                        new Action(ret => SpellManager.CastSpellById(13809))),
                    new SwitchArgument<string>("Snake Trap",
                        new Action(ret => SpellManager.CastSpellById(34600)))
                    ),*/
                                Spell.Cast( trapName, onUnit),
                                new WaitContinue(TimeSpan.FromMilliseconds(200), ret => false, new ActionAlwaysSucceed()),
                                new Action(ret => SpellManager.ClickRemoteLocation(onUnit(ret).Location)))))));
        }

        public static Composite CreateHunterTrapOnAddBehavior(string trapName)
        {
            return new PrioritySelector(
                ctx => Unit.NearbyUnfriendlyUnits.OrderBy(u => u.DistanceSqr)
                    .FirstOrDefault( u => u.Combat && u != Target && (!u.IsMoving || u.IsPlayer) && u.DistanceSqr < 40 * 40),
                new Decorator(
                    ret => ret != null && SpellManager.HasSpell(trapName) && Spell.GetSpellCooldown(trapName) != TimeSpan.Zero ,
                    new PrioritySelector(
                        Spell.BuffSelf("Trap Launcher"),
                        new Decorator(
                            ret => Me.HasAura("Trap Launcher"),
                            new Sequence(
                                Spell.Cast(trapName, onUnit => (WoWUnit)onUnit),
                                new WaitContinue(TimeSpan.FromMilliseconds(200), ret => false, new ActionAlwaysSucceed()),
                                new Action(ret => SpellManager.ClickRemoteLocation(((WoWUnit)ret).Location))
                                )
                            )
                        )
                    )
                );
        }

        public static Composite CreateHunterCallPetBehavior(bool reviveInCombat)
        {
            return new Decorator(
                ret =>  !SingularSettings.Instance.DisablePetUsage && !Me.GotAlivePet && PetManager.PetTimer.IsFinished && !Me.Mounted && !Me.OnTaxi,
                new PrioritySelector(
                    Spell.WaitForCast(),
                    new Decorator(
                        ret => Pet != null && (!Me.Combat || reviveInCombat),
                        new PrioritySelector(
                            Movement.CreateEnsureMovementStoppedBehavior(),
                            Spell.BuffSelf("Revive Pet")
                            )
                        ),
                    new Sequence(
                        new Action(ret => PetManager.CallPet(SingularSettings.Instance.Hunter.PetNumber.ToString())),
                        Helpers.Common.CreateWaitForLagDuration(),
                        new WaitContinue(2, ret => Me.GotAlivePet || Me.Combat, new ActionAlwaysSucceed()),
                        new Decorator(
                            ret => !Me.GotAlivePet && (!Me.Combat || reviveInCombat),
                            Spell.BuffSelf("Revive Pet")
                            )
                        )
                    )
                );
        }


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
            return new Decorator(
                ret => SingularSettings.Instance.IsCombatRoutineMovementAllowed(),
                new PrioritySelector(
                    new Decorator(
                        ret => HunterSettings.UseDisengage, 
                        Common.CreateDisengageBehavior()
                        ),
                    new Decorator(ret => Common.NextDisengageAllowed <= DateTime.Now && HunterSettings.AllowKiting,
                        new PrioritySelector(
                            Kite.CreateKitingBehavior(nonfacingAttack, jumpturnAttack),
                            CreateHunterBackPedal()
                            )
                        )
                    )
                );
        }

        private static bool useRocketJump;
        private static WoWUnit mobToGetAwayFrom;
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
                            if (useRocketJump)
                                needFacing = Styx.Helpers.WoWMathHelper.CalculateNeededFacing(Me.Location, safeSpot);
                            else
                                needFacing = Styx.Helpers.WoWMathHelper.CalculateNeededFacing(safeSpot, Me.Location);

                            needFacing = WoWMathHelper.NormalizeRadian(needFacing);
                            float rotation = WoWMathHelper.NormalizeRadian(Math.Abs(needFacing - Me.RenderFacing));
                            Logger.Write(Color.Cyan, "DIS: turning {0:F0} degrees {1} safe landing spot",
                                WoWMathHelper.RadiansToDegrees(rotation), useRocketJump ? "towards" : "away from");
                            Me.SetFacing(needFacing);
                        }),

                        new ActionDebugString(ret => "wait for facing to complete"),
                        new PrioritySelector(
                            new Wait(new TimeSpan(0, 0, 1), ret => Me.IsDirectlyFacing(needFacing), new ActionAlwaysSucceed()),
                            new Action(ret =>
                            {
                                Logger.Write(Color.Cyan, "DIS: timed out waiting to face safe spot - need:{0:F4} have:{1:F4}", needFacing, Me.RenderFacing);
                                return RunStatus.Failure;
                            })
                            ),

                        // stop facing
                        new Action(ret =>
                        {
                            Logger.Write(Color.Cyan, "DIS: cancel facing now we point the right way");
                            WoWMovement.StopFace();
                        }),

                        new ActionDebugString(ret => "set time of disengage just prior"),
                        new Sequence(
                            new PrioritySelector(
                                    new Decorator(ret => !useRocketJump, Spell.BuffSelf("Disengage")),
                                    new Decorator(ret => useRocketJump, Spell.BuffSelf("Rocket Jump")),
                                    new Action(ret => {
                                        Logger.Write(Color.Cyan, "DIS: {0} cast appears to have failed", useRocketJump ? "Rocket Jump" : "Disengage");
                                        return RunStatus.Failure;
                                        })
                                    ),
                            new WaitContinue( 1, req => !Me.IsAlive || !Me.IsFalling, new ActionAlwaysSucceed()),
                            new Action(ret =>
                            {
                                NextDisengageAllowed = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 750));
                                Logger.Write(Color.Cyan, "DIS: finished {0} cast", useRocketJump ? "Rocket Jump" : "Disengage");
                                Logger.WriteDebug("DIS: {0:F1} yds from current {1} to safespot {2}", Me.Location.Distance(safeSpot), Me.Location, safeSpot);
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

            if (mobToGetAwayFrom.Distance > mobToGetAwayFrom.MeleeDistance() + 3f)
                return false;

            if (Me.Level > (mobToGetAwayFrom.Level + (mobToGetAwayFrom.Elite ? 10 : 5)) && Me.HealthPercent > 20)
                return false;

            SafeArea sa = new SafeArea();
            sa.MinScanDistance = 16;    // average disengage distance on flat ground
            sa.MaxScanDistance = sa.MinScanDistance;
            sa.RaysToCheck = 36;
            sa.LineOfSightMob = Target;
            sa.MobToRunFrom = mobToGetAwayFrom;
            sa.CheckLineOfSightToSafeLocation = true;
            sa.CheckSpellLineOfSightToMob = false;

            safeSpot = sa.FindLocation();
            if (safeSpot == WoWPoint.Empty)
            {
                Logger.Write(Color.Cyan, "DIS: no safe landing spots found for {0}", useRocketJump ? "Rocket Jump" : "Disengage");
                return false;
            }

            Logger.Write(Color.Cyan, "DIS: Attempt safe {0} due to {1} @ {2:F1} yds",
                useRocketJump ? "Rocket Jump" : "Disengage",
                mobToGetAwayFrom.Name,
                mobToGetAwayFrom.Distance);

            return true;
        }

    }
}
