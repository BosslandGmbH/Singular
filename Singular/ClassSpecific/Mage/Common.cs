using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Styx;
using Styx.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using Styx.CommonBot.Routines;
using Singular.Utilities;

namespace Singular.ClassSpecific.Mage
{
    public static class Common
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static MageSettings MageSettings => SingularSettings.Instance.Mage();

	    private static DateTime _cancelIceBlockForCauterize = DateTime.MinValue;

        public static bool TreatAsFrozen(this WoWUnit unit)
        {
            return Me.HasAura("Brain Freeze") || unit.IsFrozen();
        }

        public static bool IsFrozen(this WoWUnit unit)
        {
            return unit.GetAllAuras().Any(a => a.Spell.Mechanic == WoWSpellMechanic.Frozen || (a.Spell.School == WoWSpellSchool.Frost && a.Spell.SpellEffects.Any(e => e.AuraType == WoWApplyAuraType.ModRoot)));
        }

        [Behavior(BehaviorType.Initialize, WoWClass.Mage)]
        public static Composite CreateMageInitialize()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal || SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                Kite.CreateKitingBehavior( CreateSlowMeleeBehavior(), null, null);

            return null;
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Mage)]
        public static Composite CreateMagePreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // Defensive 
                        Spell.BuffSelf("Slow Fall", req => MageSettings.UseSlowFall && Me.IsFalling),

                        PartyBuff.BuffGroup("Dalaran Brilliance", "Arcane Brilliance"),
                        PartyBuff.BuffGroup("Arcane Brilliance", "Dalaran Brilliance"),
						
                        Spell.BuffSelf("Conjure Refreshment", ret => !Gotfood && !StyxWoW.Me.GroupInfo.IsInParty)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Mage)]
        public static Composite CreateMageLossOfControlBehavior()
        {
            return new Decorator(
                req => Me.Combat,
                new PrioritySelector(

                    // deal with Ice Block here (a stun of our own doing)
                    new Decorator(
                        ret => Me.ActiveAuras.ContainsKey("Ice Block"),
                        new PrioritySelector(
                            new Throttle(10, new Action(r => Logger.Write(Color.DodgerBlue, "^Ice Block for 10 secs"))),
                            new Decorator(
                                ret => DateTime.UtcNow < _cancelIceBlockForCauterize && !Me.ActiveAuras.ContainsKey("Cauterize"),
                                new Action(ret => {
                                    Logger.Write(LogColor.Cancel, "/cancel Ice Block since Cauterize has expired");
                                    _cancelIceBlockForCauterize = DateTime.MinValue ;
                                    // Me.GetAuraByName("Ice Block").TryCancelAura();
                                    Me.CancelAura("Ice Block");
                                    return RunStatus.Success;
                                    })
                                ),
                            new ActionIdle()
                            )
                        ),

                    Spell.BuffSelf("Cold Snap", req => Me.Combat && Me.HealthPercent < MageSettings.ColdSnapHealthPct)
                    )
                );
        }

        /// <summary>
        /// PullBuffs that must be called only when in Pull and in range of target
        /// </summary>
        /// <returns></returns>
        public static Composite CreateMagePullBuffs()
        {
            return new Decorator(
                req => Me.GotTarget() && Me.CurrentTarget.SpellDistance() < 40,
                new PrioritySelector(
					Spell.BuffSelf("Ice Barrier"),
                    CreateMageRuneOfPowerBehavior()
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Mage, priority: 1)]
        public static Composite CreateMageCombatHeal()
        {
            return new PrioritySelector(
                // handle Cauterize debuff if we took talent and get it
                new Decorator(
                    ret => Me.ActiveAuras.ContainsKey("Cauterize"),
                    new PrioritySelector(
                        Spell.BuffSelf("Ice Block",
                            ret =>
                            {
                                _cancelIceBlockForCauterize = DateTime.UtcNow.AddSeconds(10);
                                return true;
                            }),

                        Spell.BuffSelf("Ice Barrier"),

                        new Throttle(8, Item.CreateUsePotionAndHealthstone(100, 0))
                        )
                    ),

                // Ice Block cast if we didn't take Cauterize
                Spell.BuffSelf("Ice Block",
                    ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances
                        && !SpellManager.HasSpell("Cauterize")
                        && StyxWoW.Me.HealthPercent < 20
                        && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")
                    ),

                Spell.BuffSelf("Slow Fall", req => MageSettings.UseSlowFall && Me.IsFalling),

                Spell.BuffSelf(
                    "Evanesce",
                    req =>
                    {
                        if (EventHandlers.TimeSinceAttackedByEnemyPlayer.TotalSeconds < 3)
                            return true;
                        if (!Me.Combat)
                            return false;
                        int cntMobs = Unit.UnfriendlyUnits(40).Count(u => u.Combat && u.CurrentTargetGuid == Me.Guid);
                        if (cntMobs == 0)
                        {
                            return false;
                        }
                        if (cntMobs < 3 && Me.HealthPercent > MageSettings.EvanesceHealthPct)
                        {
                            return false;
                        }

                        return true;
                    }),

                Spell.BuffSelf("Cold Snap", req => Me.Combat && Me.HealthPercent < MageSettings.ColdSnapHealthPct),
                Spell.BuffSelf("Ice Ward"),

                // cast Evocation for Heal or Mana
                new Decorator(
                    req => Me.Specialization == WoWSpec.MageArcane,
                    new Throttle(3, Spell.Cast("Evocation", mov => true, on => Me, ret => NeedEvocation, cancel => false))
                    ),

                Spell.BuffSelf("Ice Barrier")

                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Mage)]
        public static Composite CreateMageCombatBuffs()
        {
            return new Decorator(
                req => !Me.CurrentTarget.IsTrivial(),
                new PrioritySelector(

                    // Defensive 
                    CastAlterTime(),

                    // new Wait( 1, until => !HasTalent(MageTalents.Invocation) || Me.HasAura("Invoker's Energy"), new ActionAlwaysSucceed())

                    Dispelling.CreatePurgeEnemyBehavior("Spellsteal"),
                    // CreateMageSpellstealBehavior(),

                    CreateMageRuneOfPowerBehavior(),
					
                    Spell.BuffSelf("Time Warp", ret => MageSettings.UseTimeWarp && NeedToTimeWarp)
                    )
                );
        }

        private static readonly uint[] MageFoodIds = new uint[]
                                                         {
                                                             65500,
                                                             65515,
                                                             65516,
                                                             65517,
                                                             43518,
                                                             43523,
                                                             65499, //Conjured Mana Cake - Pre Cata Level 85
                                                             80610, //Conjured Mana Pudding - MoP Lvl 85+
                                                             80618  //Conjured Mana Buns 
                                                             //This is where i made a change.
                                                         };
		
        /// <summary>
        /// True if config allows conjuring tables, we have the spell, are not moving, group members
        /// are within 15 yds, and no table within 40 yds
        /// </summary>
        private static bool ShouldSummonTable
        {
            get
            {
                return MageSettings.SummonTableIfInParty 
                    && SpellManager.HasSpell("Conjure Refreshment Table") 
                    && !StyxWoW.Me.IsMoving
                    && MageTable == null
                    && Unit.GroupMembers.Any(p => !p.IsMe && p.DistanceSqr < 15 * 15);
            }
        }

       static readonly Dictionary<uint, uint> RefreshmentTableIds = new Dictionary<uint,uint>() 
                                         {
                                             { 186812, 70 }, //Level 70
                                             { 207386, 80 }, //Level 80
                                             { 207387, 85 }, //Level 85
                                             { 211363, 90 }, //Level 90
                                         };

        /// <summary>
        /// finds a level appropriate Mage Table if one exists.
        /// </summary>
        static public WoWGameObject MageTable
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWGameObject>()
                        .Where(
                            i => RefreshmentTableIds.ContainsKey(i.Entry) 
                                && RefreshmentTableIds[i.Entry] <= Me.Level 
                                && (StyxWoW.Me.PartyMembers.Any(p => p.Guid == i.CreatedByGuid) || StyxWoW.Me.Guid == i.CreatedByGuid)
                                && i.Distance <= SingularSettings.Instance.TableDistance
                            )
                        .OrderByDescending( t => t.Level )
                        .FirstOrDefault();
            }
        }
        
   
        public static bool Gotfood { get { return StyxWoW.Me.BagItems.Any(item => MageFoodIds.Contains(item.Entry)); } }

        public static Composite CreateUseManaGemBehavior(SimpleBooleanDelegate requirements)
        {
            return new Throttle( 2, 
                new PrioritySelector(
                    ctx => StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == 36799 || i.Entry == 81901),
                    new Decorator(
                        ret => ret != null && StyxWoW.Me.ManaPercent < 100 && ((WoWItem)ret).Cooldown == 0 && requirements(ret),
                        new Sequence(
                            new Action(ret => Logger.Write("Using {0}", ((WoWItem)ret).Name)),
                            new Action(ret => ((WoWItem)ret).Use())
                            )
                        )
                    )
                );
        }

        public static Composite CreateStayAwayFromFrozenTargetsBehavior()
        {
            return Avoidance.CreateAvoidanceBehavior(
                "Blink", 
                TalentManager.HasGlyph("Blink") ? 28 : 20, 
                Disengage.Direction.Frontwards, 
                crowdControl: CreateSlowMeleeBehavior(),
                needDisengage: nd => Me.GotTarget() && Me.CurrentTarget.IsCrowdControlled() && Me.CurrentTarget.SpellDistance() < SingularSettings.Instance.KiteAvoidDistance,
                needKiting: nk => Me.GotTarget() && (Me.CurrentTarget.IsCrowdControlled() || Me.CurrentTarget.IsSlowed(60)) && Me.CurrentTarget.SpellDistance() < SingularSettings.Instance.KiteAvoidDistance
                );
        }
		
        public static Composite CreateMagePolymorphOnAddBehavior()
        {
            if (!MageSettings.UsePolymorphOnAdds)
                return new ActionAlwaysFail();

            return new Decorator(
                req => !Unit.NearbyUnfriendlyUnits.Any(u => u.HasMyAura("Polymorph")),
                Spell.Buff(
                    "Polymorph", 
                    on => Unit.UnfriendlyUnits()
                        .Where(IsViableForPolymorph)
                        .OrderByDescending(u => u.CurrentHealth)
                        .FirstOrDefault()
                    )
                );
        }

        private static bool IsViableForPolymorph(WoWUnit unit)
        {
            if (StyxWoW.Me.CurrentTargetGuid == unit.Guid)
                return false;

            if (!unit.Combat)
                return false;

            if (unit.CreatureType != WoWCreatureType.Beast && unit.CreatureType != WoWCreatureType.Humanoid)
                return false;

            if (unit.IsCrowdControlled())
                return false;

            if (!unit.IsTargetingMeOrPet && !unit.IsTargetingMyPartyMember)
                return false;

            if (StyxWoW.Me.RaidMembers.Any(m => m.CurrentTargetGuid == unit.Guid && m.IsAlive))
                return false;

            if (!unit.SpellDistance().Between(14, 30))
                return false;

            return true;
        }

        public static bool NeedToTimeWarp
        {
            get
            {
                if ( !MageSettings.UseTimeWarp || MovementManager.IsMovementDisabled)
                    return false;

                if (Me.HasAnyAura("Temporal Displacement", PartyBuff.SatedDebuffName))
                    return false;

                if (!Spell.CanCastHack("Time Warp", Me))
                    return false;

                if (Battlegrounds.IsInsideBattleground && Shaman.Common.IsPvpFightWorthLusting)
                {
                    Logger.Write(LogColor.Hilite, "^Time Warp: using in balanced PVP fight");
                    return true;
                }

                if (Me.GotTarget() && Unit.ValidUnit(Me.CurrentTarget) && !Me.CurrentTarget.IsTrivial())
                {
                    if (SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.CurrentTarget.IsPlayer && Me.HealthPercent > Math.Max(65, Me.HealthPercent ))
                    {
                        Logger.Write(LogColor.Hilite, "^Time Warp: using due to combat with enemy player");
                        return true;
                    }

                    if (Me.CurrentTarget.IsBoss())
                    {
                        Logger.Write(LogColor.Hilite, "^Time Warp: using for Boss encounter with '{0}'", Me.CurrentTarget.SafeName());
                        return true;
                    }

                    if (Me.HealthPercent > 50)
                    {
                        int count = Unit.UnitsInCombatWithUsOrOurStuff(40).Count();
                        if ( count >= 4)
                        {
                            Logger.Write(LogColor.Hilite, "^Time Warp: using due to combat with {0} enemy targets", count);
                            return true;
                        }
                        if ( Me.CurrentTarget.TimeToDeath() > 45)
                        {
                            Logger.Write(LogColor.Hilite, "^Time Warp: using for since {0} expected to live {1:F0} seconds", Me.CurrentTarget.SafeName(), Me.CurrentTarget.TimeToDeath());
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        public static bool HasTalent( MageTalents tal)
        {
            return TalentManager.IsSelected((int)tal);
        }

        private static int _secsBeforeBattle;

        public static int SecsBeforeBattle
        {
            get
            {
                if (_secsBeforeBattle == 0)
                    _secsBeforeBattle = new Random().Next(30, 60);

                return _secsBeforeBattle;
            }

            set
            {
                _secsBeforeBattle = value;
            }
        }

        public static bool NeedTableForBattleground 
        {
            get
            {
                return SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds 
                    && PVP.PrepTimeLeft < SecsBeforeBattle && Me.HasAnyAura("Preparation", "Arena Preparation");
            }
        }

        #region Avoidance and Disengage

        /// <summary>
        /// creates a Mage specific avoidance behavior based upon settings.  will check for safe landing
        /// zones before using Blink or Rocket Jump.  will additionally do a running away or jump turn
        /// attack while moving away from attacking mob if behaviors provided
        /// </summary>
        /// <param name="nonfacingAttack">behavior while running away (back to target - instants only)</param>
        /// <param name="jumpturnAttack">behavior while facing target during jump turn (instants only)</param>
        /// <returns></returns>
        public static Composite CreateMageAvoidanceBehavior()
        {
            int distBlink = TalentManager.HasGlyph("Blink") ? 28 : 20;
            return Avoidance.CreateAvoidanceBehavior(
                "Blink", 
                distBlink, 
                Disengage.Direction.Frontwards, 
                crowdControl: CreateSlowMeleeBehavior(),
                needDisengage: nd => false,
                needKiting: nk => Me.GotTarget() && Me.CurrentTarget.IsFrozen() && Me.CurrentTarget.SpellDistance() < 8
                );
        }

        public static Composite CreateSlowMeleeBehavior()
        {
            return new PrioritySelector(
                ctx => SafeArea.NearestEnemyMobAttackingMe,
                new Action( ret => {
                    if (SingularSettings.Debug)
                    {
                        if (ret == null)
                            Logger.WriteDebug("SlowMelee: no nearest mob found");
                        else
                            Logger.WriteDebug("SlowMelee: crowdcontrolled: {0}, slowed: {1}", ((WoWUnit)ret).IsCrowdControlled(), ((WoWUnit)ret).IsSlowed());
                    }
                    return RunStatus.Failure;
                    }),
                new Decorator(
                    // ret => ret != null && !((WoWUnit)ret).Stunned && !((WoWUnit)ret).Rooted && !((WoWUnit)ret).IsSlowed(),
                    ret => ret != null,
                    new PrioritySelector(
                        new Decorator(
                            req => ((WoWUnit)req).IsCrowdControlled(),
                            new SeqDbg( 1f, s => "SlowMelee: target already crowd controlled")
                            ),
                        Spell.CastOnGround("Ring of Frost", onUnit => (WoWUnit)onUnit, req => ((WoWUnit)req).SpellDistance() < 30, true),
                        Spell.Cast("Frost Nova", mov => true, onUnit => (WoWUnit)onUnit, req => ((WoWUnit)req).SpellDistance() < 12, cancel => false),
                        new Decorator(
                            ret => TalentManager.CurrentSpec == WoWSpec.MageFrost,
                            Mage.Frost.CastFreeze(on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.SpellDistance() < 8), ClusterType.Radius, 8))
                            ),
                        Spell.Cast("Frostjaw", mov => true, onUnit => (WoWUnit)onUnit, req => true, cancel => false)
                        )
                    )
                );
        }

        #endregion

        public static bool NeedEvocation 
        { 
            get 
            {
                if (!Spell.CanCastHack("Evoation"))
                    return false;

                // always evocate if low mana
                if (Me.ManaPercent <= MageSettings.EvocationManaPct)
                {
                    Logger.Write( LogColor.Hilite, "^Evocation: casting due to low Mana @ {0:F1}%", Me.ManaPercent);
                    return true;
                }
                return false;
            }
        }

        private static Composite _runeOfPower;

        public static Composite CreateMageRuneOfPowerBehavior()
        {
            if (!Common.HasTalent(MageTalents.RuneOfPower))
                return new ActionAlwaysFail();

            if (_runeOfPower == null)
            {
                _runeOfPower = new ThrottlePasses(
                    1,
                    TimeSpan.FromSeconds(6),
                    RunStatus.Failure,
                    Spell.CastOnGround("Rune of Power", 
						on => Me, 
						req => !Me.IsMoving && !Me.InVehicle && 
								!Me.HasAura("Rune of Power") && Spell.IsSpellOnCooldown("Combustion") && 
								EventHandlers.LastNoPathFailure.AddSeconds(15) < DateTime.UtcNow, false)
                    );
            }

            return _runeOfPower;
        }

        /// <summary>
        /// handle Alter Time cast (both initial and secondary to reset)
        /// </summary>
        /// <returns></returns>
        public static Composite CastAlterTime()
        {
            return new Throttle(
                1,
                new PrioritySelector(
                    ctx => Me.HasAura("Alter Time"),
                    new Sequence(
                        Spell.BuffSelf(
                            "Alter Time", 
                            req =>
                            {
                                if ((bool) req)
                                    return false;

                                int countEnemy = Unit.UnitsInCombatWithMeOrMyStuff(40).Count();
                                if (countEnemy >= MageSettings.AlterTimeMobCount)
                                    return true;
                                int countPlayers = Unit.UnfriendlyUnits(45).Count(u => u.IsPlayer);
                                if (countPlayers >= MageSettings.AlterTimePlayerCount)
                                    return true;

                                return false;
                            }),
                            new Action( r => {
                                _healthAlterTime = (int)Me.HealthPercent;
                                _locAlterTime = Me.Location;
                                Logger.Write( LogColor.Hilite, "^Alter Time: cast again if health falls below {0}%", (_healthAlterTime * MageSettings.AlterTimeHealthPct) / 100);
                                })
                            ),

                    new Decorator(
                        req => ((bool)req) && Me.HealthPercent <= ((_healthAlterTime * MageSettings.AlterTimeHealthPct) / 100),
                        new Action( r => {
                            Logger.Write( LogColor.Hilite, "^Alter Time: restoring to {0}% at {1:F1} yds away", _healthAlterTime, _locAlterTime.Distance(Me.Location));
                            Spell.LogCast("Alter Time", Me, true);
                            Spell.CastPrimative("Alter Time");
                            })
                        )
                    )
                );
        }

        private static WoWPoint _locAlterTime { get; set; }
        private static int _healthAlterTime { get; set; }

    }

    public enum MageTalents
    {
		ArcaneFamiliar = 1,
		PresenceOfMind,
		WordsOfPower,

		Pyromaniac = ArcaneFamiliar,
		Conflagration = PresenceOfMind,
		Firestarter = WordsOfPower,

		RayOfFrost = ArcaneFamiliar,
		LonelyWinter = PresenceOfMind,
		BoneChilling = WordsOfPower,

		Shimmer = 4,
		Cauterize,
		ColdSnap,

		MirrorImage = 7,
		RuneOfPower,
		IncantersFlow,

		Supernova = 10,
		ChargedUp,
		Resonance,

		BlastWave = Supernova,
		FlameOn = ChargedUp,
		ControlledBurn = Resonance,

		IceNova = Supernova,
		FrozenTouch = ChargedUp,
		SplittingIce = Resonance,

		IceFloes = 14,
		RingOfFrost,
		IceWard,

		NetherTempest = 17,
		UnstableMagic,
		Erosion,

		LivingBomb = NetherTempest,
		FlamePatch = Erosion,

		FrostBomb = NetherTempest,
		ArcticGale = Erosion,

		Overpowered = 20,
		Quickening,
		ArcaneOrb,

		Kindling = Overpowered,
		Cinderstorm = Quickening,
		Meteor = ArcaneOrb,

		ThermalVoid = Overpowered,
		GlacialSpike = Quickening,
		CometStorm = ArcaneOrb,
    }
}