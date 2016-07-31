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
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Singular.Settings;
using Styx.Common.Helpers;


namespace Singular.ClassSpecific.Monk
{
    // wowraids.org/mistweaver
    public class Mistweaver
    {
        private const int SOOTHING_MIST = 193884;

        private static LocalPlayer Me => StyxWoW.Me;
	    private static MonkSettings MonkSettings => SingularSettings.Instance.Monk();

        private static bool RangedAttacks { get; set; }

        public static WoWUnit MyHealTarget
        {
            get
            {
                return HealerManager.SavingHealUnit ?? HealerManager.FindHighestPriorityTarget();
            }
        }

        #region INITIALIZE

        private static string SpinningCraneKick { get; set; }
        private static int minDistRollAllowed { get; set; }

        [Behavior(BehaviorType.Initialize, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite MonkMistweaverInitialize()
        {
            SpinningCraneKick = Common.HasTalent(MonkTalents.RushingJadeWind) ? "Rushing Jade Wind" : "Spinning Crane Kick";
            if (Common.HasTalent(MonkTalents.RushingJadeWind))
                Logger.Write(LogColor.Init, "[spinning crane kick] Using Rushing Jade Wind");

            RangedAttacks = SpellManager.HasSpell("Crackling Jade Lightning");
            Logger.Write(LogColor.Init, "[dps distance] Will DPS from {0}", RangedAttacks ? "Range" : "Melee");

            minDistRollAllowed = RangedAttacks ? 45 : 12;
            Logger.Write(LogColor.Init, "[roll distance] Must be atleast {0} yds away for Roll", minDistRollAllowed);
            
            return null;
        }

        #endregion  

        #region REST

        [Behavior(BehaviorType.Rest, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite CreateMistweaverRest()
        {
            return new PrioritySelector(

                CancelSoothingMistAsNeeded(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(
                        CreateMistweaverMonkHealing(true),

                        Rest.CreateDefaultRestBehaviour(null, "Resuscitate"),

                        CreateMistweaverMonkHealing(false)

                        )
                    ),

                Spell.WaitForCastOrChannel(),

                // HealerManager.CreateStayNearTankBehavior(),

                Spell.WaitForGlobalCooldown()
                );
        }

        #endregion

        #region BUFFS

        private static WoWUnit _statue;

        private static Composite CreateSummonJadeSerpentStatueBehavior()
        {
            if (!SpellManager.HasSpell("Summon Jade Serpent Statue"))
                return new ActionAlwaysFail();

            return new Throttle(
                8,
                new Decorator(
                    req => !Spell.IsSpellOnCooldown("Summon Jade Serpent Statue") ,

                    new PrioritySelector(
                        ctx => _statue = FindStatue(),

                        new Decorator(

                            req => (_statue == null || (Me.GotTarget() && Me.CurrentTarget.Location.Distance(_statue.Location) > 35)),

                            new PrioritySelector(
                                ctx => Clusters.GetBestUnitForCluster( Unit.GroupMembers.Where(g => g.IsAlive && g.Combat && g.IsMelee()), ClusterType.Radius, 30f),

                                new Decorator(
                                    req =>
                                    {
                                        if (req == null)
                                            return false;

                                        if (_statue == null)
                                            Logger.WriteDebug("JadeStatue:  my statue does not exist");
                                        else
                                        {
                                            float dist = _statue.Location.Distance((req as WoWUnit).Location);
                                            if (dist > 40)
                                                Logger.WriteDebug("JadeStatue:  my statue is {0:F1} yds away from {1} (max 40 yds)", dist, (req as WoWUnit).SafeName());
                                            else if (_statue.Distance > 40)
                                                Logger.WriteDebug("JadeStatue:  my statue is {0:F1} yds away from {1} (max 40 yds)", dist, Me.SafeName());
                                            else
                                                return false;
                                        }

                                        // yep we need to cast
                                        return true;
                                    },

                                    Spell.CastOnGround(
                                        "Summon Jade Serpent Statue",
                                        loc => {
                                            WoWPoint locTank = (loc as WoWUnit).Location;
                                            WoWPoint locMe = Me.Location;
                                            float dist = (float) locMe.Distance(locTank) * 2f / 3f;
                                            dist = Math.Min(dist, 35f);
                                            if (dist < 10)
                                                dist = -10f;    // plant past tank if he is close to us
                                            return Styx.Helpers.WoWMathHelper.CalculatePointFrom( locMe, locTank, (float)dist);
                                            },
                                        req => true,
                                        false
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static WoWUnit FindStatue()
        {
            const uint JADE_SERPENT_STATUE = 60849;
            WoWGuid guidMe = Me.Guid;
            return ObjectManager.GetObjectsOfType<WoWUnit>()
                .FirstOrDefault(u => u.Entry == JADE_SERPENT_STATUE && u.CreatedByUnitGuid == guidMe);
        }

        #endregion

        #region HEAL

        [Behavior(BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkMistweaver, WoWContext.Normal)]
        public static Composite CreateMistweaverHealBehaviorNormal()
        {
            return CreateMistweaverMonkHealing(false);
        }

        #endregion

        #region NORMAL

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkMistweaver, WoWContext.Normal)]
        public static Composite CreateMistweaverCombatBehaviorSolo()
        {
            return new Decorator(
                ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),

                new PrioritySelector(

                    CancelSoothingMistAsNeeded(),

                    CreateMistweaverMoveToEnemyTarget(),

                    new Decorator(
                        req => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange,
                        Helpers.Common.CreateAutoAttack()
                        ),

                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),

                        new PrioritySelector(
                            CreateMistWeaverDiagnosticOutputBehavior(on => HealerManager.Instance.FirstUnit),

                            Helpers.Common.CreateInterruptBehavior(),

                            Movement.WaitForFacing(),
                            Movement.WaitForLineOfSpellSight(),

                            Spell.Cast("Paralysis",
                                onu => Unit.NearbyUnfriendlyUnits
                                    .FirstOrDefault(u => u.IsCasting && u.Distance.Between(9, 20) && Me.IsSafelyFacing(u))),

                            Spell.Cast("Leg Sweep", ret => Spell.UseAOE && MonkSettings.StunMobsWhileSolo && SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.CurrentTarget.IsWithinMeleeRange),
							
                            Spell.Cast(
                                SpinningCraneKick, 
                                req => Spell.UseAOE 
                                    && Unit.UnitsInCombatWithUsOrOurStuff(8).Count() >= MonkSettings.SpinningCraneKickCnt
                                    && !Unit.UnfriendlyUnits(8).Any(u => !u.Combat || u.IsPlayer || u.IsCrowdControlled() || u.IsTargetingMyStuff())
                                ),

							Spell.Cast("Rising Sun Kick"),
							Spell.Cast("Blackout Kick", req => Me.GetAuraStacks("Teachings of the Monastery") >= 3),
							Spell.Cast("Tiger Palm")
							)
                        ),

                    // Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && !MonkSettings.DisableRoll && Me.CurrentTarget.Distance > minDistRollAllowed)
                    Common.CreateMonkCloseDistanceBehavior()
                    )
                );
        }

        #endregion

        #region BATTLEGROUNDS

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkMistweaver, WoWContext.Battlegrounds)]
        public static Composite CreateMistweaverCombatBehaviorBattlegrounds()
        {
            return new Decorator(
                ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),

                new PrioritySelector(

                    CreateMistWeaverDiagnosticOutputBehavior(on => MyHealTarget ),

                    CancelSoothingMistAsNeeded(),

                    HealerManager.CreateMeleeHealerMovementBehavior(),

                    new Decorator(
                        req => HealerManager.AllowHealerDPS(),

                        new PrioritySelector(

                            CreateMistweaverMoveToEnemyTarget(),

                            new Decorator(
                                ret => !Spell.IsGlobalCooldown(),
                                new PrioritySelector(

                                    new Decorator(
                                        req => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange,
                                        Helpers.Common.CreateAutoAttack()
                                        ),

                                    Helpers.Common.CreateInterruptBehavior(),

                                    Spell.Cast("Paralysis",
                                        onu => Unit.NearbyUnfriendlyUnits
                                            .FirstOrDefault(u => u.IsCasting && u.Distance.Between(9, 20) && Me.IsSafelyFacing(u))),

                                    Spell.Cast("Leg Sweep", ret => Spell.UseAOE && SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.CurrentTarget.IsWithinMeleeRange),
									
                                    Spell.Cast(SpinningCraneKick, ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= MonkSettings.SpinningCraneKickCnt),

                                    Spell.Cast("Rising Sun Kick"),
									Spell.Cast("Blackout Kick", req => Me.GetAuraStacks("Teachings of the Monastery") >= 3),
                                    Spell.Cast("Tiger Palm")
                                )
							),

                            Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && !MonkSettings.DisableRoll && Me.CurrentTarget.Distance > minDistRollAllowed)
                            )
                        ),

                    new Decorator(
                        ret => Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                        new PrioritySelector(
                            CreateMistweaverMonkHealing(selfOnly: false),
                            Helpers.Common.CreateInterruptBehavior()
                            )
                        )
                    )
                );

        }

        #endregion

        #region INSTANCES

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkMistweaver, WoWContext.Instances)]
        public static Composite CreateMistweaverCombatBehaviorInstances()
        {
            return new PrioritySelector(

                CancelSoothingMistAsNeeded(),
                CreateMistWeaverDiagnosticOutputBehavior(on => MyHealTarget),

                new Decorator(
                    req => true,
                    new PrioritySelector(
                        // HealerManager.CreateMeleeHealerMovementBehavior( Common.CreateMonkCloseDistanceBehavior( min => 30, on => (WoWUnit) on)),
                        HealerManager.CreateStayNearTankBehavior(Common.CreateMonkCloseDistanceBehavior(min => 30, on => (WoWUnit)on)),
        /*
                            new Decorator(
                                unit => MovementManager.IsClassMovementAllowed
                                    && !MonkSettings.DisableRoll
                                    && (unit as WoWUnit).SpellDistance() > 10
                                    && Me.IsSafelyFacing(unit as WoWUnit, 5f),
                                Spell.Cast("Roll")
                                )
                            ),
        */
                        new Decorator(
                            ret => Me.Combat && HealerManager.AllowHealerDPS(),
                            new PrioritySelector(

                                CreateMistweaverMoveToEnemyTarget(),
                                new Decorator(
                                    req => Me.GotTarget() && Me.CurrentTarget.IsAlive,
                                    Movement.CreateFaceTargetBehavior()
                                    ),

                                Spell.WaitForCastOrChannel(),

                    #region Spinning Crane Kick progress handler

                                new Decorator(
                                    req => Me.HasActiveAura("Spinning Crane Kick"),   // don't wait for Rushing Jade Wind since we can cast
                                    new PrioritySelector(
                                        new Action(r =>
                                        {
                                            Logger.WriteFile( SpinningCraneKick + ": in progress with {0} ms left", (long)Me.GetAuraTimeLeft(SpinningCraneKick).TotalMilliseconds);
                                            return RunStatus.Failure;
                                        }),
                                        new Decorator(
                                            req =>
                                            {
                                                if (Me.GetAuraTimeLeft(SpinningCraneKick).TotalMilliseconds < 333)
                                                    return false;

                                                int countFriendly = Unit.NearbyGroupMembersAndPets.Count(u => u.SpellDistance() <= 8);
                                                if (countFriendly >= 3)
                                                    return false;

                                                if (HealerManager.CancelHealerDPS())
                                                {
                                                    Logger.Write(LogColor.Cancel, "/cancel {0} since only {1} friendly targets hit and cannot DPS", SpinningCraneKick, countFriendly);
                                                    return true;
                                                }

                                                int countEnemy = Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() <= 8);
                                                if ((countFriendly + countEnemy) < 3)
                                                {
                                                    Logger.Write(LogColor.Cancel, "/cancel {0} since only {1} friendly and {2} enemy targets hit", SpinningCraneKick, countFriendly, countEnemy);
                                                    return true;
                                                }
                                                return false;
                                            },
                                            new Sequence(
                                                new Action(r => Me.CancelAura(SpinningCraneKick)),
                                                new Wait( 1, until => !Me.HasActiveAura(SpinningCraneKick), new ActionAlwaysFail())
                                                )
                                            ),

                                        // dont go past here if SCK active
                                        new ActionAlwaysSucceed()
                                        )
                                    ),
                    #endregion

                                new Decorator(
                                    ret => !Spell.IsGlobalCooldown(),
                                    new PrioritySelector(

                                        new Decorator(
                                            req => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange,
                                            Helpers.Common.CreateAutoAttack()
                                            ),
										        
                                        Helpers.Common.CreateInterruptBehavior(),

                                        Spell.Cast("Leg Sweep", ret => Spell.UseAOE && SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.CurrentTarget.IsWithinMeleeRange),

                                        Spell.Cast(
                                            SpinningCraneKick,
                                            ret => Spell.UseAOE && HealerManager.AllowHealerDPS() && Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() <= 8) >= MonkSettings.SpinningCraneKickCnt
                                            ),

										Spell.Cast("Rising Sun Kick"),
										Spell.Cast("Blackout Kick", req => Me.GetAuraStacks("Teachings of the Monastery") >= 3),
										Spell.Cast("Tiger Palm")
										)
									),

                                Spell.Cast("Roll", 
                                    req => MovementManager.IsClassMovementAllowed
                                        && !MonkSettings.DisableRoll 
                                        && Me.CurrentTarget.Distance > minDistRollAllowed
                                    )
                                )
                            )
                        )
                    ),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                    new PrioritySelector(
                        CreateMistweaverMonkHealing(selfOnly: false),
                        new Decorator(
                            req => true,
                            Helpers.Common.CreateInterruptBehavior()
                            )
                        )
                    ),

                Spell.WaitForCastOrChannel()
                );

        }

        #endregion

        private static WoWUnit _moveToHealUnit = null;

        public static Composite CreateMistweaverMonkHealing(bool selfOnly = false)
        {
            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();

            int cancelHeal = SingularSettings.Instance.IgnoreHealTargetsAboveHealth;
            cancelHeal = (int)Math.Max(cancelHeal, MonkSettings.MistHealSettings.RenewingMist);
            cancelHeal = (int)Math.Max(cancelHeal, MonkSettings.MistHealSettings.EnvelopingMist);
            cancelHeal = (int)Math.Max(cancelHeal, MonkSettings.MistHealSettings.Effuse);

            bool moveInRange = false;
            if (!selfOnly)
                moveInRange = (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds);

            Logger.WriteDebugInBehaviorCreate("Monk Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);

            int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
                behavs.AddBehavior(dispelPriority, "Detox", "Detox", Dispelling.CreateDispelBehavior());

            CreateMistweaverHealingRotation(selfOnly, behavs);

            behavs.OrderBehaviors();

            if (selfOnly == false && Singular.Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Combat)
                behavs.ListBehaviors();


            return new PrioritySelector(
                // ctx => selfOnly ? StyxWoW.Me : HealerManager.FindLowestHealthTarget(),
                // ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                ctx => selfOnly ? StyxWoW.Me : MyHealTarget,

                CreateMistWeaverDiagnosticOutputBehavior(ret => (WoWUnit)ret),

                new Decorator(
                    ret => ret != null 
                        && (Me.Combat || ((WoWUnit)ret).Combat || ((WoWUnit)ret).PredictedHealthPercent() <= 99),
                        // && HealerManager.SavingHealUnit == null
                        // && (selfOnly || !MonkSettings.MistHealSettings.HealFromMelee || !Me.GotTarget() || Me.CurrentTarget.IsWithinMeleeRange),

                    new PrioritySelector(
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                behavs.GenerateBehaviorTree(),

                                new Decorator(
                                    ret => moveInRange,
                                    new Sequence(
                                        new Action(r => _moveToHealUnit = (WoWUnit)r),
                                        new PrioritySelector(
                                            Movement.CreateMoveToLosBehavior(on => _moveToHealUnit),
                                            Movement.CreateMoveToUnitBehavior(on => _moveToHealUnit, 40f, 34f)
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        private static void CreateMistweaverHealingRotation(bool selfOnly, PrioritizedBehaviorList behavs)
        {
            CreateMistweaverHealingWowhead(selfOnly, behavs);
        }
		private static void CreateMistweaverHealingWowhead(bool selfOnly, PrioritizedBehaviorList behavs)
        {
            // save a group
            if (!selfOnly)
            {
                behavs.AddBehavior(
                    HealthToPriority(MonkSettings.MistHealSettings.Revival) + 900,
                    String.Format("Revival on {0} targets @ {1}%", MonkSettings.MistHealSettings.CountRevival, MonkSettings.MistHealSettings.Revival),
                    "Revival",
                    new Decorator(
                        req => (req as WoWUnit).HealthPercent < MonkSettings.MistHealSettings.Revival,
                        Spell.Cast("Revival", on =>
                        {
                            if (Spell.IsSpellOnCooldown("Revival"))
                                return null;

                            List<WoWUnit> revlist = HealerManager.Instance.TargetList
                                .Where(u => u.HealthPercent < MonkSettings.MistHealSettings.Revival && u.DistanceSqr < 100 * 100 && u.InLineOfSpellSight)
                                .ToList();
                            if (revlist.Count() < MonkSettings.MistHealSettings.CountRevival)
                                return null;

                            Logger.Write( LogColor.Hilite, "Revival: found {0} heal targets below {1}%", revlist.Count(), MonkSettings.MistHealSettings.Revival);
                            return revlist.FirstOrDefault(u => u != null && u.IsValid);
                        })
                        )
                    );
            }

            // save a player
            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.LifeCocoon) + 800,
                String.Format("Life Cocoon @ {0}%", MonkSettings.MistHealSettings.LifeCocoon),
                "Life Cocoon",
                Spell.Buff("Life Cocoon", on => (WoWUnit)on, req => (Me.Combat || (req as WoWUnit).Combat) && (req as WoWUnit).PredictedHealthPercent(includeMyHeals: true) < MonkSettings.MistHealSettings.LifeCocoon)
                );

            if (!selfOnly)
            {
                behavs.AddBehavior(
                    HealthToPriority(1) + 700,
                    "Summon Jade Serpent Statue",
                    "Summon Jade Serpent Statue",
                    new Decorator(
                        req => Group.Tanks.Any(t => t.Combat && !t.IsMoving && t.GotTarget() && t.CurrentTarget.IsHostile && t.SpellDistance(t.CurrentTarget) < 10),
                        CreateSummonJadeSerpentStatueBehavior()
                        )
                    );
            }

            if (!selfOnly)
            {
                behavs.AddBehavior(
                    HealthToPriority(99) + 700,
                    "Thunder Focus Tea on me", "Thunder Focus Tea",
                    new Decorator(
                        req => (Me.Combat && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid)),
                        Spell.Cast("Thunder Focus Tea", on => Me)
                        )
                    );

                behavs.AddBehavior(
                    HealthToPriority(100) + 700,
                    String.Format("Roll Renewing Mist on at least {0} targets", MonkSettings.MistHealSettings.RollRenewingMistCount),
                    "Renewing Mist",
                    new Decorator(
                        req => true,
                        CreateMistweaverRollRenewingMist()
                        )
                    );

            }

            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.ChiWave) + 600,
                String.Format("Chi Wave on {0} targets @ {1}%", MonkSettings.MistHealSettings.CountChiWave, MonkSettings.MistHealSettings.ChiWave),
                "Chi Wave",
                new Decorator(
                    req => (req as WoWUnit).PredictedHealthPercent() < MonkSettings.MistHealSettings.ChiWave,
                    Spell.Cast("Chi Wave", on => GetBestChiWaveTarget())
                    )
                );
			
            behavs.AddBehavior(
                HealthToPriority(1) + 500,
                "Expel Harm in Combat for Chi",
                "Expel Harm",
                Spell.Buff("Expel Harm", on => Common.BestExpelHarmTarget(), 
                    req => Me.Combat
                    )
                );

            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.Effuse),
                String.Format("Effuse @ {0}%", MonkSettings.MistHealSettings.Effuse),
				"Effuse",
                new Decorator(
                    req => ((WoWUnit)req).PredictedHealthPercent() < MonkSettings.MistHealSettings.Effuse
                        && Spell.CanCastHack("Effuse", (WoWUnit)req),
                        new PrioritySelector(
                            Spell.Cast("Effuse", on => (WoWUnit)on),
                            new Action(r =>
                            {
                                Logger.WriteDebug("Effuse: failed to ensure Soothing Mist first");
                                return RunStatus.Failure;
                            })
                            )
                        )
                );

            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.Vivify),
                String.Format("Vivify @ {0}%", MonkSettings.MistHealSettings.Vivify),
                "Vivify",
                new Decorator(
                    req => ((WoWUnit)req).PredictedHealthPercent() < MonkSettings.MistHealSettings.Vivify
                        && Spell.CanCastHack("Vivify", (WoWUnit)req),
                        new PrioritySelector(
                            Spell.Cast("Vivify", on => (WoWUnit)on),
                            new Action(r =>
                            {
                                Logger.WriteDebug("Vivify: failed to ensure Soothing Mist first");
                                return RunStatus.Failure;
                            })
                            )
                        )
                );

            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.EnvelopingMist),
                String.Format("Enveloping Mist @ {0}%", MonkSettings.MistHealSettings.EnvelopingMist),
                "Enveloping Mist",
                new Decorator(
                    req => ((WoWUnit)req).HasAuraExpired("Enveloping Mist")
                        && ((WoWUnit)req).PredictedHealthPercent() < MonkSettings.MistHealSettings.EnvelopingMist
                        && Spell.CanCastHack("Enveloping Mist", (WoWUnit)req),
                        new PrioritySelector(
                            Spell.Cast("Enveloping Mist", on => (WoWUnit)on),
                            new Action(r =>
                            {
                                Logger.WriteDebug("EnvelopingMist: failed to ensure Soothing Mist first");
                                return RunStatus.Failure;
                            })
                            )
                        )
                );
        }


        public static WoWUnit GetBestChiWaveTarget()
        {
            const int ChiWaveHopRange = 20;

            if (!Me.IsInGroup())
                return null;

            if (!Spell.CanCastHack("Chi Wave", Me, skipWowCheck: true))
            {
                Logger.WriteDebug("GetBestChiWaveTarget: CanCastHack says NO to Chi Wave");
                return null;
            }

            var targetInfo = HealerManager.Instance.TargetList
                .Select(p => new { Unit = p, Count = Clusters.GetClusterCount(p, ChiWavePlayers, ClusterType.Chained, ChiWaveHopRange) })
                .OrderByDescending(v => v.Count)
                .ThenByDescending(v => Group.Tanks.Any(t => t.Guid == v.Unit.Guid))
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            WoWUnit target = targetInfo?.Unit;
            int count = targetInfo?.Count ?? 0;

            // too few hops? then search any group member
            if (count < MonkSettings.MistHealSettings.CountChiWave)
            {
                target = Clusters.GetBestUnitForCluster(ChiWavePlayers, ClusterType.Chained, ChiWaveHopRange);
                if (target != null)
                {
                    count = Clusters.GetClusterCount(target, ChiWavePlayers, ClusterType.Chained, ChiWaveHopRange);
                    if (count < MonkSettings.MistHealSettings.CountChiWave)
                        target = null;
                }
            }

            if (target != null)
                Logger.WriteDebug("Chi Wave Target:  found {0} with {1} nearby under {2}%", target.SafeName(), count, MonkSettings.MistHealSettings.ChiWave);

            return target;
        }

        private static IEnumerable<WoWUnit> ChiWavePlayers
        {
            get
            {
                // TODO: Decide if we want to do this differently to ensure we take into account the T12 4pc bonus. (Not removing RT when using CH)
                return HealerManager.Instance.TargetList
                    .Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && u.PredictedHealthPercent() < MonkSettings.MistHealSettings.ChiWave)
                    .Select(u => (WoWUnit)u);
            }
        }

        private static Composite CreateMistweaverRollRenewingMist()
        {
            return new Decorator(
                req => !Spell.IsSpellOnCooldown("Renewing Mist"),
                new PrioritySelector(
                    ctx => GetBestRenewingMistTankTarget(),
                    new Decorator(
                        req =>
                        {
                            if (req != null)
                            {
                                Logger.WriteDebug("RenewingMistTarget:  tank {0} needs Renewing Mist", (req as WoWUnit).SafeName());
                                return true;
                            }

                            int rollCount = HealerManager.Instance.TargetList.Count(u => u.IsAlive && u.HasMyAura("Renewing Mist") && u.SpellDistance() < 40);
                            if (rollCount < MonkSettings.MistHealSettings.RollRenewingMistCount)
                            {
                                Logger.WriteDebug("RenewingMistTarget:  currently {0} have my Renewing Mist, need {1}", rollCount, MonkSettings.MistHealSettings.RollRenewingMistCount);
                                return true;
                            }
                            return false;
                        },
                        Spell.Cast(
                            "Renewing Mist",
                            on =>
                            {
                                WoWUnit unit = (on as WoWUnit) ?? GetBestRenewingMistTarget();
                                if (unit != null)
                                    Logger.WriteDebug(Color.White, "ROLLING Renewing Mist on: {0}", unit.SafeName());
                                return unit;
                            }
                            )
                        )
                    )
                );
        }

        private static WoWUnit GetBestRenewingMistTankTarget()
        {
            WoWUnit rewnewTarget = null;
            rewnewTarget = Group.Tanks
                .Where(u => u.IsAlive && u.Combat && u.DistanceSqr < 40 * 40 && !u.HasMyAura("Renewing Mist") && u.InLineOfSpellSight)
                .OrderBy(u => u.HealthPercent)
                .FirstOrDefault();

            if (rewnewTarget != null)
                Logger.WriteDebug("GetBestRenewingMistTank: found tank {0}, hasmyaura={1} with {2} ms left", rewnewTarget.SafeName(), rewnewTarget.HasMyAura("Riptide"), (int)rewnewTarget.GetAuraTimeLeft("Riptide").TotalMilliseconds);
            return rewnewTarget;
        }

        private static WoWUnit GetBestRenewingMistTarget()
        {
            int distHop = 20;
            WoWUnit ripTarget = Clusters.GetBestUnitForCluster(NonRenewingMistPlayers, ClusterType.Chained, distHop);
            if (ripTarget != null)
                Logger.WriteDebug("GetBestRenewingMistTarget: found optimal target {0}, hasmyaura={1} with {2} ms left", ripTarget.SafeName(), ripTarget.HasMyAura("Riptide"), (int)ripTarget.GetAuraTimeLeft("Riptide").TotalMilliseconds);

            return ripTarget;
        }

        private static IEnumerable<WoWUnit> NonRenewingMistPlayers
        {
            get
            {
                return HealerManager.Instance.TargetList
                    .Where(u => u.IsAlive && !u.HasMyAura("Renewing Mist") && u.DistanceSqr < 40 * 40 && u.PredictedHealthPercent() < MonkSettings.MistHealSettings.RenewingMist)
                    .Select(u => (WoWUnit)u);
            }
        }


        public static int HealthToPriority(int nHealth)
        {
            return nHealth == 0 ? 0 : 200 - nHealth;
        }


        #region Mistweaver Helpers

        private static Composite CancelSoothingMistAsNeeded()
        {
            return new Sequence(

                // fall out of sequence if not casting or channelling
                new DecoratorContinue(
                    req => !Spell.IsCastingOrChannelling(),
                    new ActionAlwaysFail()
                    ),

                // cancel channel if Soothing Mist and its our heal target and they are healed up
                new DecoratorContinue(
                    req => IsChannelingSoothingMist(),
                    new Sequence(
                        ctx => Me.ChannelObject,

                        new DecoratorContinue(
                            req => req != null,
                            new Sequence(

                                // output message at most once per second
                                new Decorator(
                                    req => SingularSettings.Debug,
                                    new ThrottlePasses(1, TimeSpan.FromMilliseconds(500), RunStatus.Success,
                                        new Action(u => Logger.WriteDebug(System.Drawing.Color.White, "MonkWaitForCast: {0} on {1} @ {2:F1}", "Soothing Mist", (u as WoWUnit).SafeName(), (u as WoWUnit).HealthPercent))
                                        )
                                    ),

                                // check if target healed and we need to cancel
                                new DecoratorContinue(
                                    req => (req as WoWUnit).HealthPercent > 99,
                                    new Sequence(
                                        new Action(u => Logger.Write(LogColor.Cancel, "/cancel: cancel {0} on {1} @ {2:F1}", Me.ChanneledSpell.Name, (u as WoWUnit).SafeName(), (u as WoWUnit).HealthPercent)),
                                        new Action(r => SpellManager.StopCasting()),

                                        // wait for channel to actual stop
                                        new Wait(
                                            TimeSpan.FromMilliseconds(500),
                                            until => !Spell.IsCastingOrChannelling(),
                                            new ActionAlwaysFail()
                                            )
                                        )
                                    ),

                                // check if higher priority target with lower health needs us, so we cancel
                                // NOTE: all health checks here should be HealthPercent, not GetPredictedHealthPercent()
                                new DecoratorContinue(
                                    req => {
                                        bool surgmistTargetNotPriority = false;
                                        string surgmistTargetMessage = "";
                                        // if channel target out of danger and not a healer/tank, check if a healer/tank needs saving heal
                                        // WoWPartyMember pm = Me.GroupInfo.RaidMembers.FirstOrDefault( p => p.Guid == (req as WoWUnit).Guid);

                                        if ( !Unit.GroupMembers.Any(m => m.Guid == (req as WoWUnit).Guid))
                                        {
                                            surgmistTargetNotPriority = true;
                                            surgmistTargetMessage = "MistweaverWaitForCast: effuse target {0} not a Raid Member";
                                        }
                                        else if ( !Group.Tanks.Any(t => t.Guid == (req as WoWUnit).Guid) && !Group.Healers.Any(t => t.Guid == (req as WoWUnit).Guid))
                                        {
                                            surgmistTargetNotPriority = true;
                                            surgmistTargetMessage = "MistweaverWaitForCast: effuse target {0} not a Tank or Healer";
                                        }
                                        else if ( (req as WoWUnit).HealthPercent > HealerManager.EmergencyHealOutOfDanger)
                                        {
                                            surgmistTargetNotPriority = true;
                                            surgmistTargetMessage = "MistweaverWaitForCast: effuse target {0} @ {1:F1}%";
                                        }
                                        
                                        if (surgmistTargetNotPriority)
                                        {
                                            WoWUnit cancelFor = Group.Tanks.FirstOrDefault(t => t.IsAlive && t.HealthPercent < HealerManager.EmergencyHealPercent && t.SpellDistance() < 40 && t.InLineOfSpellSight);
                                            if (cancelFor == null)
                                                cancelFor = Group.Healers.FirstOrDefault(h => h.IsAlive && h.HealthPercent < HealerManager.EmergencyHealPercent && h.SpellDistance() < 40 && h.InLineOfSpellSight);
                                            if (cancelFor != null)
                                            {
                                                Logger.WriteDiagnostic(surgmistTargetMessage, (req as WoWUnit).SafeName(), (req as WoWUnit).HealthPercent);
                                                if (req == null)
                                                    Logger.Write(LogColor.Cancel, "/cancel: {0} because {1} @ {2:F1}% needs saving heal", Me.ChanneledSpell.Name, cancelFor.SafeName(), cancelFor.HealthPercent);
                                                else
                                                    Logger.Write(LogColor.Cancel, "/cancel: {0} on {1} @ {2:F1}% because {3} @ {4:F1}% needs saving heal", Me.ChanneledSpell.Name, (req as WoWUnit).SafeName(), (req as WoWUnit).HealthPercent, cancelFor.SafeName(), cancelFor.HealthPercent);

                                                SpellManager.StopCasting();
                                                HealerManager.SavingHealUnit = cancelFor;
                                                return true;
                                            }
                                        }

                                        return false;
                                        },
                                    new Wait(
                                        TimeSpan.FromMilliseconds(350),
                                        until => !Spell.IsCastingOrChannelling(),
                                        new ActionAlwaysFail()
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        private static bool IsChannelingSoothingMist(WoWUnit target = null)
        {
            if (Me.ChanneledCastingSpellId == SOOTHING_MIST)
            {
                if (!Spell.IsChannelling())
                    return false;

                if (target == null)
                    return true;

                return Me.ChannelObjectGuid == target.Guid;
            }

            return false;
        }

        public static Composite CreateMistweaverMoveToEnemyTarget()
        {
            return new PrioritySelector(
                new Decorator(
                    req => RangedAttacks || SingularRoutine.CurrentWoWContext != WoWContext.Normal,
                    Helpers.Common.EnsureReadyToAttackFromLongRange()
                    ),
                new Decorator(
                    req => !RangedAttacks,
                    Helpers.Common.EnsureReadyToAttackFromMelee()
                    )
                );
        }

        private static WoWGuid guidLastHealTarget;

        #endregion

        #region Diagnostics

        private static Composite compret;

        private static Composite CreateMistWeaverDiagnosticOutputBehavior(UnitSelectionDelegate onUnit)
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            if (compret == null)
            {
                compret = new ThrottlePasses(
                    1,
                    TimeSpan.FromSeconds(1),
                    RunStatus.Failure,
                    new Action(ret =>
                    {
                        WoWUnit target = Me.CurrentTarget;

                        string line = "...";
                        line += string.Format(" h={0:F1}%/m={1:F1}%/c={2},move={3},combat={4},tigerpwr={5},mtea={6},muscle={7},statue={8:F1} yds",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.CurrentChi,
                            Me.IsMoving.ToYN(),
                            Me.Combat.ToYN(),
                            (long)target.GetAuraTimeLeft("Tiger Power").TotalMilliseconds,
                            Me.GetAuraStacks("Mana Tea"),
                            (long)target.GetAuraTimeLeft("Muscle Memory").TotalMilliseconds,
                            (FindStatue() ?? Me).Distance
                            );

                        WoWUnit healTarg = onUnit(ret);
                        if (Me.IsInGroup() || (Me.FocusedUnitGuid.IsValid && healTarg == Me.FocusedUnit))
                        {
                            if (healTarg == null)
                                line += ",heal=(null)";
                            else if (!healTarg.IsValid)
                                line += ",heal=(invalid)";
                            else
                            {
                                float hh = (float) healTarg.HealthPercent;
                                float hph= healTarg.PredictedHealthPercent();
                                line += string.Format(",vmist={0},zeal={1},heal={2} {3:F1}% @ {4:F1} yds,hph={5:F1}%,hcombat={6},tloss={7}",
                                    Me.GetAuraStacks("Vital Mists"),
                                    (long)Me.GetAuraTimeLeft("Serpent's Zeal").TotalMilliseconds,
                                    healTarg.SafeName(),
                                    hh,
                                    healTarg.SpellDistance(),
                                    hph,
                                    healTarg.Combat.ToYN(),
                                    healTarg.InLineOfSpellSight.ToYN()
                                    );
                                if (hph > 100)
                                    line += ",Error=GetPredictedHealth > 100";
                                else if (hph < 0)
                                    line += ",Error=GetPredictedHealth < 0";
                            }

                            if (SingularSettings.Instance.StayNearTank)
                            {
                                WoWUnit tank = HealerManager.TankToStayNear;
                                if (tank == null)
                                    line += ",tank=(null)";
                                else if (!tank.IsAlive)
                                    line += ",tank=(dead)";
                                else
                                {
                                    float hh = (float)tank.HealthPercent;
                                    float hph = tank.PredictedHealthPercent();
                                    line += string.Format(",tank={0} {1:F1}% @ {2:F1} yds,tph={3:F1}%,tcombat={4},tmove={5},tloss={6},tstatue={7:F1} yds",
                                        tank.SafeName(),
                                        hh,
                                        tank.SpellDistance(),
                                        hph,
                                        tank.Combat.ToYN(),
                                        tank.IsMoving.ToYN(),
                                        tank.InLineOfSpellSight.ToYN(),
                                        tank.Location.Distance((FindStatue() ?? tank).Location)
                                        );
                                }
                            }
                        }

                        if (target == null)
                            line += ", target=(null)";
                        else if (!target.IsValid)
                            line += ", target=(invalid)";
                        else
                            line += string.Format(", target={0} {1:F1}% @ {2:F1} yds, face={3} tloss={4}",
                                target.SafeName(),
                                target.HealthPercent,
                                target.Distance,
                                Me.IsSafelyFacing(target).ToYN(),
                                target.InLineOfSpellSight.ToYN()
                                );

                        Logger.WriteDebug(Color.Yellow, line);
                        return RunStatus.Failure;
                    }));
            }

            return compret;
        }

        #endregion


    }

}