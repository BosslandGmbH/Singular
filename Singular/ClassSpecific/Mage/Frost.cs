using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using System;
using System.Drawing;
using System.Collections.Generic;

namespace Singular.ClassSpecific.Mage
{
    public class Frost
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static MageSettings MageSettings => SingularSettings.Instance.Mage();

	    const int FINGERS_OF_FROST = 44544;

        [Behavior(BehaviorType.Initialize, WoWClass.Mage, WoWSpec.MageFrost)]
        public static Composite CreateMageFrostInit()
        {
            PetManager.NeedsPetSupport = true;
            return null;
        }


        [Behavior(BehaviorType.Rest, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.All, 1)]
        public static Composite CreateMageFrostRest()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Decorator(
                            ret => !Helpers.Rest.IsEatingOrDrinking,
                            CreateSummonWaterElemental()
                            ),

                        Singular.Helpers.Rest.CreateDefaultRestBehaviour(),

                        new Decorator(ctx => SingularSettings.Instance.DisablePetUsage && Me.GotAlivePet,
                            new Sequence(
                                new Action(ctx => Logger.Write("/dismiss Pet")),
                                Spell.Cast("Dismiss Pet", on => Me.Pet, req => true, cancel => false),
                                new WaitContinue(TimeSpan.FromMilliseconds(1500), ret => !Me.GotAlivePet, new ActionAlwaysSucceed())
                                )
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.All, 1)]
        public static Composite CreateMageFrostPreCombatbuffs()
        {
            return new Decorator(
                ret => !Spell.IsCasting() && !Spell.IsGlobalCooldown(),
                new PrioritySelector(
                    CreateSummonWaterElemental()
                    )
                );
        }

        #region Normal Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Mage, WoWSpec.MageFrost)]
        public static Composite CreateMageFrostNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

						Common.CreateMagePullBuffs(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

            #region TRIVIAL MOB FARM SUPPORT
                        new Decorator(
                            req => Me.CurrentTarget.IsTrivial(),
                            new PrioritySelector(
                                Spell.Cast("Cone of Cold", mov => false, on => Me.CurrentTarget, req => 
                                {
                                    IEnumerable<WoWUnit> coneUnits = Clusters.GetConeCluster(Unit.UnfriendlyUnits(12), 12);
                                    int count = coneUnits.Count();
                                    if (count > 1 && Spell.CanCastHack("Cone of Cold", Me.CurrentTarget) && coneUnits.Any(u => u.Guid == Me.CurrentTargetGuid))
                                    {
                                        if (coneUnits.All(u => u.IsTrivial()))
                                        {
                                            Logger.Write("^AOE Trivial Pull: casting Cone of Cold");
                                            return true;
                                        }
                                    }
                                    return false;
                                }),
                                CreateIceLanceBehavior( on => Me.CurrentTarget, req =>
                                {
                                    if (!Spell.CanCastHack("Ice Lance", Me.CurrentTarget))
                                        return false;

                                    Logger.Write(LogColor.Hilite, "^Trivial Pull: casting Ice Lance");
                                    return true;
                                })
                                )
                            ),
            #endregion  

            #region FAST PULL SUPPORT
                        new Decorator(
                            ret =>
                            {
                                WoWPlayer nearby = ObjectManager.GetObjectsOfType<WoWPlayer>(true, false).FirstOrDefault(p => !p.IsMe && p.DistanceSqr <= 40 * 40);
                                if (nearby != null)
                                {
                                    Logger.WriteDiagnostic("NormalPull: doing fast pull since player {0} nearby @ {1:F1} yds", nearby.SafeName(), nearby.Distance);
                                    return true;
                                }
                                return false;
                            },
                            new PrioritySelector(
                                Spell.Buff("Nether Tempest", 1, on => Me.CurrentTarget, req => true),
                                Spell.Buff("Living Bomb", 0, on => Me.CurrentTarget, req => true),
                                Spell.Cast("Ice Lance", ret =>
                                {
                                    if (Me.GotTarget && Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost))
                                        return false;

                                    if (!Spell.CanCastHack("Ice Lance", Me.CurrentTarget))
                                        return false;

                                    Logger.Write(LogColor.Hilite, "^Fast Pull: casting Ice Lance for instant pull");
                                    return true;
                                }),
                                Spell.Cast("Fire Blast", req => !SpellManager.HasSpell("Ice Lance") || Me.CurrentTarget.IsImmune( WoWSpellSchool.Frost))
                                )
                            ),
            #endregion

                        // Pull with Ice Lance if trivial or FoF built up
                        CreateIceLanceFoFBehavior(),

                        // Otherwise.... lets set a Bomb first
                        new Decorator(
                            req => !Me.CurrentTarget.IsTrivial(),
                            new PrioritySelector(
                                Spell.Buff("Nether Tempest", 1, on => Me.CurrentTarget, req => true),
                                Spell.Buff("Living Bomb", 0, on => Me.CurrentTarget, req => true),
                                Spell.Buff("Frost Bomb", 0, on => Me.CurrentTarget, req => true)
                                )
                            ),

                        Spell.Cast("Frostbolt", ret => Me.GotTarget() && !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                        Spell.Cast("Frostfire Bolt"),
                        Spell.Cast("Ice Lance", ret =>
                        {
                            if (Me.GotTarget && Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost))
                                return false;

                            if (!Spell.CanCastHack("Ice Lance", Me.CurrentTarget))
                                return false;

                            if (!Me.IsMoving)
                                return false;

                            Logger.Write(LogColor.Hilite, "^Cast on the Move: Ice Lance");
                            return true;
                        }),

                        Spell.Cast("Fire Blast", req => !SpellManager.HasSpell("Ice Lance") || Me.GotTarget() && Me.CurrentTarget.IsImmune( WoWSpellSchool.Frost))
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                );
        }

        public class ILInfo 
        {
            public WoWUnit Unit;
            public uint StacksOfFOF;
            public object SaveContext;
            public ILInfo( WoWUnit u, uint i, object ctx)
            {
                Unit = u;
                StacksOfFOF = i;
                SaveContext = ctx;
            }

            public static ILInfo Ref(object o)
            {
                return (o as ILInfo);
            }
        }
        /// <summary>
        /// cast Ice Lance only if FoF active.  waits after cast until FoF stack count changes
        /// to ensure we don't double cast Ice Lance (since we queue spells)
        /// </summary>
        /// <param name="on"></param>
        /// <returns></returns>
        private static Sequence CreateIceLanceFoFBehavior(UnitSelectionDelegate onUnit = null, SimpleBooleanDelegate requirements = null)
        {
            UnitSelectionDelegate ondel = onUnit ?? (o => Me.CurrentTarget);
            SimpleBooleanDelegate reqdel = requirements ?? (req => true);

            return new Sequence(
                ctx => new ILInfo(ondel(ctx), Me.GetAuraStacks(FINGERS_OF_FROST), ctx),
                new Decorator(
                // req => Spell.CanCastHack("Ice Lance", (req as ILInfo).Unit) && ((req as ILInfo).Unit != null && ((req as ILInfo).StacksOfFOF > 0 || (req as ILInfo).Unit.IsTrivial())),
                    req => ILInfo.Ref(req).Unit != null && ILInfo.Ref(req).StacksOfFOF > 0 && Spell.CanCastHack("Ice Lance", ILInfo.Ref(req).Unit) && !ILInfo.Ref(req).Unit.IsImmune(WoWSpellSchool.Frost),
                    new Sequence(
                        new Action(r => Logger.Write(LogColor.Hilite, "^Fingers of Frost[{0}]: casting buffed Ice Lance", ILInfo.Ref(r).StacksOfFOF)),
                        Spell.Cast("Ice Lance", mov => false, o => ILInfo.Ref(o).Unit, r => reqdel(ILInfo.Ref(r).SaveContext)),    // ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) < 4),
                        Helpers.Common.CreateWaitForLagDuration(
                            until => ILInfo.Ref(until).StacksOfFOF != Me.GetAuraStacks(FINGERS_OF_FROST)
                            )
                        )
                    )
                );
        }

        /// <summary>
        /// cast Ice Lance without requiring FoF, but if FoF active wait until buff stack count updated
        /// to ensure we don't break other Ice Lance logic that depends on FoF being accurate
        /// </summary>
        /// <param name="on"></param>
        /// <returns></returns>
        private static Sequence CreateIceLanceBehavior(UnitSelectionDelegate on = null, SimpleBooleanDelegate requirements = null)
        {
            UnitSelectionDelegate ondel = on ?? (o => Me.CurrentTarget);
            SimpleBooleanDelegate reqdel = requirements ?? (req => true);

            return new Sequence(
                ctx => new ILInfo(ondel(ctx), Me.GetAuraStacks(FINGERS_OF_FROST), ctx),
                new Decorator(
                    req => ILInfo.Ref(req).Unit != null && (ILInfo.Ref(req).StacksOfFOF > 0 || ILInfo.Ref(req).Unit.IsTrivial()) && Spell.CanCastHack("Ice Lance", ILInfo.Ref(req).Unit) && !ILInfo.Ref(req).Unit.IsImmune(WoWSpellSchool.Frost),
                    new Sequence(
                        Spell.Cast("Ice Lance", o => ((ILInfo)o).Unit, r => reqdel(ILInfo.Ref(r).SaveContext)),
                        Helpers.Common.CreateWaitForLagDuration(
                            until => ILInfo.Ref(until).StacksOfFOF != Me.GetAuraStacks(FINGERS_OF_FROST) || Me.GetAuraStacks(FINGERS_OF_FROST) == 0
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Mage, WoWSpec.MageFrost)]
        public static Composite CreateMageFrostHeal()
        {
            return new PrioritySelector(
                CreateFrostDiagnosticOutputBehavior("Combat")
                );
        }

        [Behavior(BehaviorType.PullBuffs, WoWClass.Mage, WoWSpec.MageFrost)]
        public static Composite CreateMageFrostPullBuffs()
        {
            return new PrioritySelector(
                CreateFrostDiagnosticOutputBehavior("Pull")
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFrost)]
        public static Composite CreateMageFrostNormalCombat()
        {
            return new PrioritySelector(
                 Safers.EnsureTarget(),
                 Common.CreateStayAwayFromFrozenTargetsBehavior(),
                 Helpers.Common.EnsureReadyToAttackFromLongRange(),
                 Spell.WaitForCastOrChannel(),

                 new Decorator(
                     ret => !Spell.IsGlobalCooldown(),
                     new PrioritySelector(

                        Common.CreateMageAvoidanceBehavior(),

                        CreateSummonWaterElemental(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

						// stack buffs for some burst... only every few minutes, but we'll use em if we got em
                        new Decorator(
                             req => Me.GotTarget() && !Me.CurrentTarget.IsTrivial() && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.TimeToDeath() > 40 || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),
                             new Decorator(
                                 req =>
                                    (Me.Level < 77 || Me.HasAura("Brain Freeze"))
                                    && (Me.Level < 24 || Me.HasAura(FINGERS_OF_FROST)) 
                                    && (Me.Level < 36 || Spell.CanCastHack("Icy Veins", Me, skipWowCheck: true)),
                                 new PrioritySelector(
									 Spell.BuffSelf("Mirror Image"),
									 Spell.HandleOffGCD(Spell.BuffSelf("Icy Veins"))
                                    )
                                )
                            ),

                        Common.CreateMagePolymorphOnAddBehavior(),

                        new PrioritySelector(
                            ctx => Unit.UnfriendlyUnits(12)
                                .FirstOrDefault(
                                    u => u.CurrentTargetGuid == Me.Guid 
                                    && (u.IsStressful() || (u.Guid == Me.CurrentTargetGuid && u.TimeToDeath() > 6)) 
                                    && !u.IsCrowdControlled()
                                    ),
                            new Decorator(
                                ret => ret != null && SingularRoutine.CurrentWoWContext != WoWContext.Instances,
                                new PrioritySelector(
                                    Spell.Buff("Frost Nova", on => (WoWUnit) on, req => !Unit.UnitsInCombatWithUsOrOurStuff(12).Any(u => u.IsCrowdControlled())),
                                    CastFreeze(
										on => (WoWUnit)on, 
										req => 
											Spell.IsSpellOnCooldown("Frost Nova") && !((WoWUnit)req).IsFrozen() && 
											!Unit.UnfriendlyUnitsNearTarget(12).Any(u => u.IsCrowdControlled()))
                                    )
                                )
                            ),

						Spell.Cast("Ray of Frost", req => Spell.IsSpellOnCooldown("Rune of Power")),
						Spell.BuffSelf("Icy Veins"),
						Spell.Buff("Frost Bomb", req => Me.GetAuraStacks("Fingers of Frost") >= 2),
						Spell.Cast("Frozen Orb"),
						CastFreeze(
								on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8), 
								req => Unit.UnfriendlyUnitsNearTarget((WoWUnit)req, 8).Any()),
						Spell.Cast("Frozen Touch", req => !Me.HasActiveAura("Fingers of Frost")),
						Spell.Cast("Ice Lance", ret => Me.GetAuraStacks("Fingers of Frost") >= 2),
						Spell.Cast("Flurry", req => Me.HasActiveAura("Brain Freeze") && !Me.HasActiveAura("Fingers of Frost")),
						Spell.Cast("Ice Lance", req => Me.CurrentTarget.HasMyAura("Winter's Chill")),
						Pet.CastPetAction("Water Jet", req => !Me.HasActiveAura("Fingers of Frost")),
						Spell.Cast("Ice Nova"),
						Spell.CastOnGround("Blizzard", 
							on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8), 
							req => Unit.UnfriendlyUnitsNearTarget(((WoWUnit)req), 8).Count() >= 3),
						Spell.Cast("Ice Lance", req => Me.GetAuraStacks("Fingers of Frost") == 1),
						Spell.Cast("Glacial Spike"),
                        Spell.Cast("Frostbolt", ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost))
                    )
                )
			);
        }

        #endregion
		
        public static Composite CreateSummonWaterElemental()
        {
            return new Decorator(
                ret => PetManager.IsPetSummonAllowed
                    && !Common.HasTalent(MageTalents.LonelyWinter)
                    && (!Me.GotAlivePet || Me.Pet.Distance > 40)
                    && PetManager.PetSummonAfterDismountTimer.IsFinished
                    && Spell.CanCastHack("Summon Water Elemental"),

                new Sequence(
                    // wait for possible auto-spawn if supposed to have a pet and none present
                    new DecoratorContinue(
                        ret => !Me.GotAlivePet && !SingularSettings.Instance.DisablePetUsage,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("Summon Water Elemental:  waiting briefly for live pet to appear")),
                            new WaitContinue(
                                TimeSpan.FromMilliseconds(1000),
                                ret => Me.GotAlivePet,
                                new Sequence(
                                    new Action(ret => Logger.WriteDebug("Summon Water Elemental:  live pet detected")),
                                    new ActionAlwaysFail()
                                    )
                                )
                            )
                        ),

                    // dismiss pet if not supposed to have one
                    new DecoratorContinue(
                        ret => Me.GotAlivePet && SingularSettings.Instance.DisablePetUsage,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("Summon Water Elemental:  dismissing pet")),
                            new Action(ctx => Lua.DoString("PetDismiss()")),
                            new WaitContinue(
                                TimeSpan.FromMilliseconds(1000),
                                ret => !Me.GotAlivePet,
                                new Action(ret => {
                                    Logger.WriteDebug("Summon Water Elemental:  dismiss complete");
                                    return RunStatus.Success;
                                    })
                                )
                            )
                        ),

                    // summon pet if we still need to
                    new DecoratorContinue(
                        ret => !Me.GotAlivePet && !SingularSettings.Instance.DisablePetUsage,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("Summon Water Elemental:  about to summon pet")),

                            // Heal() used intentionally here (has spell completion logic not present in Cast())
                            Spell.Cast(n => "Summon Water Elemental",
                                chkMov => true,
                                onUnit => Me,
                                req => true,
                                cncl => false),

                            // make sure we see pet alive before continuing
                            new Wait(1, ret => Me.GotAlivePet, new ActionAlwaysSucceed()),
                            new Action(ret => Logger.WriteDebug("Summon Water Elemental:  now have alive pet"))
                            )
                        )
                    )
                );
        }
        
        /// <summary>
        /// Cast "Freeze" pet ability on a target.  Uses a local store for location to
        /// avoid target position changing during cast preparation and being out of
        /// range after range check
        /// </summary>
        /// <param name="onUnit">target to cast on</param>
        /// <returns></returns>
        public static Composite CastFreeze( UnitSelectionDelegate onUnit, SimpleBooleanDelegate require = null)
        {
            if (onUnit == null)
                return new ActionAlwaysFail();

            if (require == null)
                require = req => true;

            return new Sequence(
                ctx => onUnit(ctx),
                new Decorator( 
                    req => req != null && (req as WoWUnit).SpellDistance() < 40 && require(req), 
                    new Action( r => 
                    {
                        _locFreeze = (r as WoWUnit).Location;
                        if (StyxWoW.Me.Location.Distance(_locFreeze) > 45)
                            _locFreeze = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, _locFreeze, 7f + (r as WoWUnit).CombatReach);
                        if (StyxWoW.Me.Location.Distance(_locFreeze) > 45)
                            return RunStatus.Failure;
                        return RunStatus.Success;
                    })
                    ),
                new Throttle( TimeSpan.FromMilliseconds(250),
                    Pet.CastPetActionOnLocation(
                        "Freeze",
                        on => _locFreeze,
                        ret => !Me.CurrentTarget.TreatAsFrozen()
                        )
                    )
                );
        }

        static private WoWPoint _locFreeze;

        #region Diagnostics

        private static Composite CreateFrostDiagnosticOutputBehavior(string state = null)
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    string log;

                    log = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, mov={3}, pet={4:F1}%, fof={5}, brnfrz={6}",
                        state ?? Dynamics.CompositeBuilder.CurrentBehaviorType.ToString(),
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving.ToYN(),
                        Me.GotAlivePet ? Me.Pet.HealthPercent : 0,
                        Me.GetAuraStacks(FINGERS_OF_FROST),
                        (long)Me.GetAuraTimeLeft("Brain Freeze", true).TotalMilliseconds
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        log += string.Format(", ttd={0}, th={1:F1}%, dist={2:F1}, tmov={3}, melee={4}, face={5}, loss={6}, fboltstks={7}",
                            target.TimeToDeath(),
                            target.HealthPercent,
                            target.Distance,
                            target.IsMoving.ToYN(),
                            target.IsWithinMeleeRange.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            target.InLineOfSpellSight.ToYN(),
                            target.GetAuraStacks("Frostbolt", true)
                            );

                        if (Common.HasTalent(MageTalents.NetherTempest))
                            log += string.Format(", nethtmp={0}", (long)target.GetAuraTimeLeft("Nether Tempest", true).TotalMilliseconds);
                        else if (Common.HasTalent(MageTalents.LivingBomb ))
                            log += string.Format( ", livbomb={0}", (long)target.GetAuraTimeLeft("Living Bomb", true).TotalMilliseconds);
                        else if (Common.HasTalent(MageTalents.FrostBomb))
                            log += string.Format( ", frstbmb={0}", (long)target.GetAuraTimeLeft("Frost Bomb", true).TotalMilliseconds);

                        if (target.HasAura("Freeze"))
                            log += string.Format(", freeze={0}", (long)target.GetAuraTimeLeft("Freeze", true).TotalMilliseconds);
                        else if (target.HasAura("Frost Nova"))
                            log += string.Format(", frostnova={0}", (long)target.GetAuraTimeLeft("Frost Nova", true).TotalMilliseconds);
                        else if (target.HasAura("Ring of Frost"))
                            log += string.Format(", ringfrost={0}", (long)target.GetAuraTimeLeft("Ring of Frost", true).TotalMilliseconds);
                        else if (target.HasAura("Frostjaw"))
                            log += string.Format(", frostjaw={0}", (long)target.GetAuraTimeLeft("Frostjaw", true).TotalMilliseconds);
                        else if (target.HasAura("Ice Ward"))
                            log += string.Format(", iceward={0}", (long)target.GetAuraTimeLeft("Ice Ward", true).TotalMilliseconds);

                        if (target.IsImmune(WoWSpellSchool.Frost))
                            log += ", immune=Y";

                        log += string.Format(", isfrozen={0}", target.TreatAsFrozen().ToYN());
                    }

                    Logger.WriteDebug(Color.AntiqueWhite, log);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }
}
