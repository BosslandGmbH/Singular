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
using System.Drawing;

namespace Singular.ClassSpecific.Hunter
{
    public class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static HunterSettings HunterSettings { get { return SingularSettings.Instance.Hunter; } }

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

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Hunter)]
        public static Composite CreateHunterPreBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.BuffSelf("Aspect of the Hawk", ret => !StyxWoW.Me.HasAura("Aspect of the Iron Hawk") && !StyxWoW.Me.HasAura("Aspect of the Hawk")),
                        Spell.BuffSelf("Track Hidden"),

                        new Decorator(ctx => SingularSettings.Instance.DisablePetUsage && StyxWoW.Me.GotAlivePet,
                            new Action(ctx => SpellManager.Cast("Dismiss Pet"))),

                        new Decorator(ctx => !SingularSettings.Instance.DisablePetUsage,
                            new PrioritySelector(
                                CreateHunterCallPetBehavior(true),
                                Spell.Cast("Mend Pet", ret => Me.GotAlivePet && (StyxWoW.Me.Pet.HealthPercent < 70 || (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet"))) && !StyxWoW.Me.Pet.HasAura("Mend Pet"))
                                )
                            )
                        )
                    )
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
                        new Throttle(
                            Spell.Cast("Misdirection",
                                ctx => StyxWoW.Me.Pet,
                                ret => StyxWoW.Me.GotAlivePet
                                    && StyxWoW.Me.Combat
                                    && !StyxWoW.Me.HasAura("Misdirection")
                                    && !Group.Tanks.Any(t => t.IsAlive && t.Distance < 100))
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Hunter)]
        public static Composite CreateHunterCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Throttle(
                            Spell.Cast("Misdirection",
                                ctx => StyxWoW.Me.Pet,
                                ret => StyxWoW.Me.GotAlivePet
                                    && StyxWoW.Me.Combat
                                    && !StyxWoW.Me.HasAura("Misdirection")
                                    && !Group.Tanks.Any(t => t.IsAlive && t.Distance < 100))
                            )
                        )
                    )
                );
        }

        public static Composite CreateHunterBackPedal()
        {
            return
                new Decorator(
                    ret => !SingularSettings.Instance.DisableAllMovement && StyxWoW.Me.CurrentTarget.Distance <= Spell.MeleeRange + 5f &&
                           StyxWoW.Me.CurrentTarget.IsAlive &&
                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null ||
                            StyxWoW.Me.CurrentTarget.CurrentTarget != StyxWoW.Me ||
                            StyxWoW.Me.CurrentTarget.IsStunned()),
                    new Action(
                        ret =>
                        {
                            var moveTo = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, StyxWoW.Me.CurrentTarget.Location, Spell.MeleeRange + 10f);

                            if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                            {
                                Navigator.MoveTo(moveTo);
                                return RunStatus.Success;
                            }

                            return RunStatus.Failure;
                        }));
        }

        public static Composite CreateHunterTrapBehavior(string trapName)
        {
            return CreateHunterTrapBehavior(trapName, ret => StyxWoW.Me.CurrentTarget);
        }

        public static Composite CreateHunterTrapBehavior(string trapName, bool useLauncher)
        {
            return CreateHunterTrapBehavior(trapName, useLauncher, ret => StyxWoW.Me.CurrentTarget);
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
                            ret => StyxWoW.Me.HasAura("Trap Launcher"),
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

                                new Action(ret => Lua.DoString(string.Format("CastSpellByName(\"{0}\")", trapName))),
                                new WaitContinue(TimeSpan.FromMilliseconds(200), ret => false, new ActionAlwaysSucceed()),
                                new Action(ret => SpellManager.ClickRemoteLocation(onUnit(ret).Location)))))));
        }

        public static Composite CreateHunterTrapOnAddBehavior(string trapName)
        {
            return new PrioritySelector(
                ctx => Unit.NearbyUnfriendlyUnits.OrderBy(u => u.DistanceSqr).
                                                  FirstOrDefault(
                                                        u => u.Combat && u != StyxWoW.Me.CurrentTarget &&
                                                             (!u.IsMoving || u.IsPlayer) && u.DistanceSqr < 40 * 40),
                new Decorator(
                    ret => ret != null && SpellManager.HasSpell(trapName) && !SpellManager.Spells[trapName].Cooldown,
                    new PrioritySelector(
                        Spell.BuffSelf("Trap Launcher"),
                        new Decorator(
                            ret => StyxWoW.Me.HasAura("Trap Launcher"),
                            new Sequence(
                                new Action(ret => Lua.DoString(string.Format("CastSpellByName(\"{0}\")", trapName))),
                                new WaitContinue(TimeSpan.FromMilliseconds(200), ret => false, new ActionAlwaysSucceed()),
                                new Action(ret => SpellManager.ClickRemoteLocation(((WoWUnit)ret).Location)))))));
        }

        public static Composite CreateHunterCallPetBehavior(bool reviveInCombat)
        {
            return new Decorator(
                ret =>  !SingularSettings.Instance.DisablePetUsage && !StyxWoW.Me.GotAlivePet && PetManager.PetTimer.IsFinished,
                new PrioritySelector(
                    Spell.WaitForCast(),
                    new Decorator(
                        ret => StyxWoW.Me.Pet != null && (!StyxWoW.Me.Combat || reviveInCombat),
                        new PrioritySelector(
                            Movement.CreateEnsureMovementStoppedBehavior(),
                            Spell.BuffSelf("Revive Pet"))),
                    new Sequence(
                        new Action(ret => PetManager.CallPet(SingularSettings.Instance.Hunter.PetSlot)),
                        Helpers.Common.CreateWaitForLagDuration(),
                        new WaitContinue(2, ret => StyxWoW.Me.GotAlivePet || StyxWoW.Me.Combat, new ActionAlwaysSucceed()),
                        new Decorator(
                            ret => !StyxWoW.Me.GotAlivePet && (!StyxWoW.Me.Combat || reviveInCombat),
                            Spell.BuffSelf("Revive Pet")))
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
                ret => !SingularSettings.Instance.DisableAllMovement,
                new PrioritySelector(
                    new Decorator(
                        ret => HunterSettings.UseDisengage, 
                        Common.CreateDisengageBehavior()
                        ),
                    new Decorator(ret => Common.NextDisengageAllowed <= DateTime.Now,
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
                        new Action(delegate
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
                                    new Action(ret => Logger.Write(Color.Cyan, "DIS: {0} cast appears to have failed", useRocketJump ? "Rocket Jump" : "Disengage"))
                                    ),
                            new Action(ret =>
                            {
                                NextDisengageAllowed = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, 750));
                                Logger.Write(Color.Cyan, "DIS: finished {0} cast", useRocketJump ? "Rocket Jump" : "Disengage");
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
            sa.LineOfSightMob = Me.CurrentTarget;
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
