﻿using System.Linq;
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

namespace Singular.ClassSpecific.Mage
{
    public static class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MageSettings MageSettings { get { return SingularSettings.Instance.Mage(); } }

        private static DateTime _cancelIceBlockForCauterize = DateTime.MinValue;
        private static WoWPoint locLastFrostArmor = WoWPoint.Empty;
        private static WoWPoint locLastIceBarrier = WoWPoint.Empty;

        public static bool IsFrozen(this WoWUnit unit)
        {
            return unit.GetAllAuras().Any(a => a.Spell.Mechanic == WoWSpellMechanic.Frozen || (a.Spell.School == WoWSpellSchool.Frost && a.Spell.SpellEffects.Any(e => e.AuraType == WoWApplyAuraType.ModRoot)));
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Mage)]
        public static Composite CreateMagePreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        PartyBuff.BuffGroup("Dalaran Brilliance", "Arcane Brilliance"),
                        PartyBuff.BuffGroup("Arcane Brilliance", "Dalaran Brilliance"),

                        // Additional armors/barriers for BGs. These should be kept up at all times to ensure we're as survivable as possible.
                        new Decorator(
                            ret => SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds,
                            new PrioritySelector(
                                // Don't put up mana shield if we're arcane. Since our mastery works off of how much mana we have!
                                Spell.BuffSelf("Mana Shield", ret => TalentManager.CurrentSpec != WoWSpec.MageArcane)
                                )
                            ),

                        CreateMageArmorBehavior(),

                        new PrioritySelector(
                            ctx => MageTable,
                            new Decorator(
                                ctx => ctx != null && CarriedMageFoodCount < 80 && StyxWoW.Me.FreeNormalBagSlots > 1,
                                new Sequence(
                                    new Action(ctx => Logger.Write("Getting Mage food")),
                // Move to the Mage table
                                    new DecoratorContinue(
                                        ctx => ((WoWGameObject)ctx).DistanceSqr > 5 * 5,
                                        new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, ((WoWGameObject)ctx).Location, 5))))
                                        ),
                // interact with the mage table
                                    new Action(ctx => ((WoWGameObject)ctx).Interact()),
                                    new WaitContinue(2, ctx => false, new ActionAlwaysSucceed())
                                    )
                                )
                            ),

                        new Decorator(
                            ctx => ShouldSummonTable && (!Gotfood || NeedTableForBattleground) && SpellManager.CanCast("Conjure Refreshment Table"),
                            new Sequence(
                                new DecoratorContinue(
                                    ctx => StyxWoW.Me.IsMoving,
                                    new Sequence(
                                        new Action(ctx => StopMoving.Now()),
                                        new WaitContinue(2, ctx => !StyxWoW.Me.IsMoving, new ActionAlwaysSucceed())
                                        )
                                    ),
                                new Action(ctx => SpellManager.Cast("Conjure Refreshment Table")),
                                new WaitContinue(2, ctx => StyxWoW.Me.IsCasting, new ActionAlwaysSucceed()),
                                new WaitContinue(10, ctx => !StyxWoW.Me.IsCasting, new ActionAlwaysSucceed())
                                )
                            ),

                        Spell.BuffSelf("Conjure Refreshment", ret => !Gotfood && !ShouldSummonTable),
                        Spell.BuffSelf("Conjure Mana Gem", ret => !HaveManaGem )
/*
                        new Throttle( 1,
                            new Decorator(ret => !HaveManaGem && SpellManager.CanCast("Conjure Mana Gem"),
                                new Sequence(
                                    new Action(ret => Logger.Write("Casting Conjure Mana Gem")),
                                    new Action(ret => SpellManager.Cast(759))
                                    )
                                )
                            )
*/ 
                        )
                    )
                );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Mage)]
        public static Composite CreateMageLossOfControlBehavior()
        {
            return new PrioritySelector(

                // deal with Ice Block here (a stun of our own doing)
                new Decorator(
                    ret => Me.ActiveAuras.ContainsKey("Ice Block"),
                    new PrioritySelector(
                        new Throttle(10, new Action(r => Logger.Write(Color.DodgerBlue, "^Ice Block for 10 secs"))),
                        new Decorator(
                            ret => DateTime.Now < _cancelIceBlockForCauterize && !Me.ActiveAuras.ContainsKey("Cauterize"),
                            new Action(ret => {
                                Logger.Write(Color.White, "/cancel Ice Block since Cauterize has expired");
                                _cancelIceBlockForCauterize = DateTime.MinValue ;
                                // Me.GetAuraByName("Ice Block").TryCancelAura();
                                Me.CancelAura("Ice Block");
                                return RunStatus.Success;
                                })
                            ),
                        new ActionIdle()
                        )
                    ),

                Spell.BuffSelf("Blink", ret => MovementManager.IsClassMovementAllowed && Me.Stunned ),
                Spell.BuffSelf("Temporal Shield", ret => Me.Stunned)
                );
        }


        [Behavior(BehaviorType.CombatBuffs, WoWClass.Mage)]
        public static Composite CreateMageCombatBuffs()
        {
            return new Decorator(
                req => !Unit.IsTrivial(Me.CurrentTarget),
                new PrioritySelector(
                    // Defensive 

                    // handle Cauterize debuff if we took talent and get it
                    new Decorator(
                        ret => Me.ActiveAuras.ContainsKey("Cauterize"),
                        new PrioritySelector(
                            Spell.BuffSelf("Ice Block",
                                ret => {
                                    _cancelIceBlockForCauterize = DateTime.Now.AddSeconds(10);
                                    return true;
                                }),

                            Spell.BuffSelf("Temporal Shield"),
                            Spell.BuffSelf("Ice Barrier"),
                            Spell.BuffSelf("Incanter's Ward"),
                            Spell.BuffSelf("Evocation"),
                            new Decorator(
                                req => !Me.HasAnyAura( "Invoker's Energy", "Incanter's Ward"),
                                new Throttle( 8, Item.CreateUsePotionAndHealthstone(100, 0))
                                )
                            )
                        ),

                    // Ice Block cast if we didn't take Cauterize
                    Spell.BuffSelf("Ice Block",
                        ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances
                            && !SpellManager.HasSpell("Cauterize")
                            && StyxWoW.Me.HealthPercent < 20
                            && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),

                     Spell.Buff(
                        "Evocation", true, on => Me, 
                        ret => Me.ManaPercent < 30 
                            || Me.HealthPercent < 30  
                            || (Me.HealthPercent < 60 && 2 <= Unit.NearbyUnfriendlyUnits.Count( u => u.Combat && u.IsTargetingMeOrPet )),
                        "Invoker's Energy"
                        ),

                    Spell.BuffSelf("Incanter's Ward", req => Unit.NearbyUnitsInCombatWithMe.Any()),

                    Dispelling.CreatePurgeEnemyBehavior("Spellsteal"),
                    // CreateMageSpellstealBehavior(),

                    Spell.Cast("Ice Barrier", on => Me, ret => Me.HasAuraExpired("Ice Barrier", 2)),

                    new Throttle( TimeSpan.FromMilliseconds(10000), Spell.CastOnGround("Rune of Power", loc => Me.Location, req => !Me.HasAura("Rune of Power"), false) ),

                    //  Spell.CastOnGround("Rune of Power", loc => Me.Location.RayCast(Me.RenderFacing, 1.25f), ret => !Me.IsMoving),

                    Spell.Cast("Nether Tempest", ret => Me.GotTarget && Me.CurrentTarget.HasAuraExpired("Nether Tempest", 3)),
                    Spell.Cast("Living Bomb", ret => Me.GotTarget && Me.CurrentTarget.HasAuraExpired("Living Bomb", 2)),
                    Spell.Cast("Frost Bomb", ret => Me.GotTarget && !Me.CurrentTarget.HasMyAura("Frost Bomb")),

                    // Spell.Cast("Alter Time", ret => StyxWoW.Me.HasAura("Icy Veins") && StyxWoW.Me.HasAura("Brain Freeze") && StyxWoW.Me.HasAura("Fingers of Frost") && StyxWoW.Me.HasAura("Invoker's Energy")),

                    Spell.Cast("Mirror Image", 
                        req => Me.GotTarget &&  (Me.CurrentTarget.IsBoss() || (Me.CurrentTarget.Elite && SingularRoutine.CurrentWoWContext != WoWContext.Instances) || Me.CurrentTarget.IsPlayer || Unit.NearbyUnitsInCombatWithMe.Count() >= 3)),

                    Spell.BuffSelf("Time Warp", ret => MageSettings.UseTimeWarp && NeedToTimeWarp),

                    Common.CreateUseManaGemBehavior(ret => Me.ManaPercent < (SingularRoutine.CurrentWoWContext == WoWContext.Instances ? 20 : 80))
                
                    // , Spell.BuffSelf( "Ice Floes", req => Me.IsMoving)
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

        private const uint ArcanePowder = 17020;

        private static bool ShouldSummonTable
        {
            get
            {
                return MageSettings.SummonTableIfInParty 
                    && SpellManager.HasSpell("Conjure Refreshment Table") 
                    && Unit.NearbyGroupMembers.Any(p => !p.IsMe );
            }
        }

       static readonly uint[] RefreshmentTableIds = new uint[]
                                         {
                                             186812,
                                             207386,
                                             207387 //This is the one for level 85 - not sure if we need to add another at 90
                                         };

        static private WoWGameObject MageTable
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(
                        i => RefreshmentTableIds.Contains(i.Entry) && (StyxWoW.Me.PartyMembers.Any(p => p.Guid == i.CreatedByGuid) || StyxWoW.Me.Guid == i.CreatedByGuid));
            }
        }

        private static int CarriedMageFoodCount
        {
            get
            {

                return (int)StyxWoW.Me.CarriedItems.Sum(i => i != null
                                                      && i.ItemInfo != null
                                                      && i.ItemInfo.ItemClass == WoWItemClass.Consumable
                                                      && i.ItemSpells != null
                                                      && i.ItemSpells.Count > 0
                                                      && i.ItemSpells[0].ActualSpell.Name.Contains("Refreshment")
                                                          ? i.StackCount
                                                          : 0);
            }
        }
        
   
        public static bool Gotfood { get { return StyxWoW.Me.BagItems.Any(item => MageFoodIds.Contains(item.Entry)); } }

        private static bool HaveManaGem { get { return StyxWoW.Me.BagItems.Any(i => i.Entry == 36799 || i.Entry == 81901); } }

        public static Composite CreateUseManaGemBehavior() { return CreateUseManaGemBehavior(ret => true); }

        public static Composite CreateUseManaGemBehavior(SimpleBooleanDelegate requirements)
        {
            return new PrioritySelector(
                ctx => StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == 36799 || i.Entry == 81901),
                new Decorator(
                    ret => ret != null && StyxWoW.Me.ManaPercent < 100 && ((WoWItem)ret).Cooldown == 0 && requirements(ret),
                    new Sequence(
                        new Action(ret => Logger.Write("Using {0}", ((WoWItem)ret).Name)),
                        new Action(ret => ((WoWItem)ret).Use())))
                );
        }

        public static Composite CreateStayAwayFromFrozenTargetsBehavior()
        {
#if NOPE
            return new PrioritySelector(
                ctx => Unit.NearbyUnfriendlyUnits
                           .Where( u => u.IsFrozen() && Me.SpellDistance(u) < 8)
                           .OrderBy(u => u.DistanceSqr).FirstOrDefault(),
                new Decorator(
                    ret => ret != null && MovementManager.IsClassMovementAllowed,
                    new PrioritySelector(
                        new Decorator(
                            ret => Spell.GetSpellCooldown("Blink").TotalSeconds > 0
                                && Spell.GetSpellCooldown("Rocket Jump").TotalSeconds > 0,
                            new Action(
                                ret =>
                                {
                                    if (Me.IsMoving && StopMoving.Type == StopMoving.StopType.Location)
                                    {
                                        Logger.WriteDebug(Color.LightBlue, "StayAwayFromFrozen:  looks like we are already moving away");
                                        return RunStatus.Success;
                                    }

                                    WoWPoint moveTo = WoWMathHelper.CalculatePointBehind(
                                        ((WoWUnit)ret).Location,
                                        ((WoWUnit)ret).Rotation,
                                        -Me.SpellRange(12f, (WoWUnit) ret)
                                        );

                                    if (!Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                                    {
                                        Logger.WriteDebug(Color.LightBlue, "StayAwayFromFrozen:  unable to navigate to point behind me {0:F1} yds away", StyxWoW.Me.Location.Distance(moveTo));
                                        return RunStatus.Failure;
                                    }

                                    Logger.Write( Color.LightBlue, "Getting away from frozen target {0}", ((WoWUnit)ret).SafeName());
                                    Navigator.MoveTo(moveTo);
                                    StopMoving.AtLocation(moveTo);
                                    return RunStatus.Success;
                                })
                            ),

                        new Decorator(
                            ret => !Me.IsMoving,
                            new PrioritySelector(
                                Disengage.CreateDisengageBehavior("Blink", Disengage.Direction.Frontwards, 20, null),
                                Disengage.CreateDisengageBehavior("Rocket Jump", Disengage.Direction.Frontwards, 20, null)
                                )
                            )
                        )
                    )
                );
             */
#else
            return new PrioritySelector(
                ctx => Unit.NearbyUnfriendlyUnits
                           .Where(u => u.IsFrozen() && Me.SpellDistance(u) < 8)
                           .OrderBy(u => u.DistanceSqr).FirstOrDefault(),
                new Decorator(
                    req => req != null,
                    new Sequence(
                        new Action(r => Logger.WriteDebug("MageAvoidance: move away from frozen targets! requesting KITING!!!")),
                        Kite.BeginKitingBehavior()
                        )
                    )
                );
#endif
        }

        /*
        public static Composite CreateMageSpellstealBehavior()
        {
            return Spell.Cast("Spellsteal", 
                mov => false, 
                on => {
                    WoWUnit unit = GetSpellstealTarget();
                    if (unit != null)
                        Logger.WriteDebug("Spellsteal:  found {0} with a triggering aura, cancast={1}", unit.SafeName(), SpellManager.CanCast("Spellsteal", unit));
                    return unit;
                    },
                ret => SingularSettings.Instance.DispelTargets != CheckTargets.None 
                );                   
        }

        public static WoWUnit GetSpellstealTarget()
        {
            if (SingularSettings.Instance.DispelTargets == CheckTargets.Current)
            {
                if ( Me.GotTarget && null != GetSpellstealAura( Me.CurrentTarget))
                {
                    return Me.CurrentTarget;
                }
            }
            else if (SingularSettings.Instance.DispelTargets != CheckTargets.None)
            {
                WoWUnit target = Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => Me.IsSafelyFacing(u) && null != GetSpellstealAura(u));
                return target;
            }

            return null;
        }

        public static WoWAura GetSpellstealAura(WoWUnit target)
        {
            return target.GetAllAuras().FirstOrDefault(a => a.TimeLeft.TotalSeconds > 5 && a.Spell.DispelType == WoWDispelType.Magic && PurgeWhitelist.Instance.SpellList.Contains(a.SpellId) && !Me.HasAura(a.SpellId));
        }
        */

        public static Composite CreateMagePolymorphOnAddBehavior()
        {
            return
                new PrioritySelector(
                    ctx => Unit.NearbyUnfriendlyUnits.OrderByDescending(u => u.CurrentHealth).FirstOrDefault(IsViableForPolymorph),
                    new Decorator(
                        ret => ret != null && Unit.NearbyUnfriendlyUnits.All(u => !u.HasMyAura("Polymorph")),
                        new PrioritySelector(
                            Spell.Buff("Polymorph", ret => (WoWUnit)ret))));
        }

        private static bool IsViableForPolymorph(WoWUnit unit)
        {
            if (unit.IsCrowdControlled())
                return false;

            if (unit.CreatureType != WoWCreatureType.Beast && unit.CreatureType != WoWCreatureType.Humanoid)
                return false;

            if (StyxWoW.Me.CurrentTarget != null && StyxWoW.Me.CurrentTarget == unit)
                return false;

            if (!unit.Combat)
                return false;

            if (!unit.IsTargetingMeOrPet && !unit.IsTargetingMyPartyMember)
                return false;

            if (StyxWoW.Me.GroupInfo.IsInParty && StyxWoW.Me.PartyMembers.Any(p => p.CurrentTarget != null && p.CurrentTarget == unit))
                return false;

            return true;
        }

        public static bool NeedToTimeWarp
        {
            get
            {
                if ( !MageSettings.UseTimeWarp || !MovementManager.IsClassMovementAllowed)
                    return false;

                if (Battlegrounds.IsInsideBattleground && Shaman.Common.IsPvpFightWorthLusting) 
                    return true;

                if (!Me.GroupInfo.IsInRaid && Me.GotTarget)
                {
                    if (Me.CurrentTarget.IsBoss() || Me.CurrentTarget.TimeToDeath() > 45 || (Me.CurrentTarget.IsPlayer && Me.CurrentTarget.ToPlayer().IsHorde != Me.IsHorde))
                    {
                        return !Me.HasAnyAura("Temporal Displacement", Shaman.Common.SatedName);
                    }
                }

                return false;
            }
        }

        public static bool HasTalent( MageTalents tal)
        {
            return TalentManager.IsSelected((int)tal);
        }

        private static int _secsBeforeBattle = 0;

        public static int secsBeforeBattle
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
                return SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds && PVP.PrepTimeLeft < secsBeforeBattle && Me.HasAnyAura("Preparation", "Arena Preparation");
            }
        }

        /// <summary>
        /// behavior to cast appropriate Armor 
        /// </summary>
        /// <returns></returns>
        public static Composite CreateMageArmorBehavior()
        {
            return new Throttle(TimeSpan.FromMilliseconds(500),
                new Sequence(
                    new Action(ret => _Armor = GetBestArmor()),
                    new Decorator(
                        ret => _Armor != MageArmor.None
                            && !Me.HasMyAura(ArmorSpell(_Armor))
                            && SpellManager.CanCast(ArmorSpell(_Armor), Me),
                        Spell.BuffSelf(s => ArmorSpell(_Armor), ret => !Me.HasAura(ArmorSpell(_Armor)))
                        )
                    )
                );
        }

        static MageArmor _Armor;

        static string ArmorSpell(MageArmor s)
        {
            return s.ToString() + " Armor";
        }

        /// <summary>
        /// determines the best MageArmor value to use.  Attempts to use 
        /// user setting first, but defaults to something reasonable otherwise
        /// </summary>
        /// <returns>MageArmor to use</returns>
        public static MageArmor GetBestArmor()
        {
            if (MageSettings.Armor == MageArmor.None)
                return MageArmor.None;

            if (StyxWoW.Me.Specialization == WoWSpec.None)
                return MageArmor.None;

            MageArmor bestArmor;
            if (MageSettings.Armor != Settings.MageArmor.Auto)
                bestArmor = MageSettings.Armor;
            else if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                bestArmor = MageArmor.Frost;
            else
            {
                if (Me.Specialization == WoWSpec.MageArcane)
                    bestArmor = MageArmor.Mage;
                else if (Me.Specialization == WoWSpec.MageFrost)
                    bestArmor = MageArmor.Frost;
                else
                    bestArmor = MageArmor.Molten;
            }

            if (bestArmor == MageArmor.Mage && Me.Level < 80)
                bestArmor = MageArmor.Frost;

            if (bestArmor == MageArmor.Frost && Me.Level < 54)
                bestArmor = MageArmor.Molten;

            if (bestArmor == MageArmor.Molten && Me.Level < 34)
                bestArmor = MageArmor.None;

            return bestArmor;
        }

        public static Composite CreateSpellstealEnemyBehavior()
        {
            return Dispelling.CreatePurgeEnemyBehavior("Spellsteal");
        }

        #region Avoidance and Disengage

        /// <summary>
        /// creates a Druid specific avoidance behavior based upon settings.  will check for safe landing
        /// zones before using WildCharge or rocket jump.  will additionally do a running away or jump turn
        /// attack while moving away from attacking mob if behaviors provided
        /// </summary>
        /// <param name="nonfacingAttack">behavior while running away (back to target - instants only)</param>
        /// <param name="jumpturnAttack">behavior while facing target during jump turn (instants only)</param>
        /// <returns></returns>
        public static Composite CreateMageAvoidanceBehavior(Composite nonfacingAttack = null, Composite jumpturnAttack = null)
        {
            Kite.CreateKitingBehavior(CreateSlowMeleeBehavior(), nonfacingAttack, jumpturnAttack);

            return new Decorator(
                req => MovementManager.IsClassMovementAllowed,
                new PrioritySelector(
                    ctx => Unit.NearbyUnitsInCombatWithMe.Where(u=>u.SpellDistance() <= 8).Count(),
                    new Decorator(
                        ret => SingularSettings.Instance.DisengageAllowed 
                            && ((((int)ret) > 0 && Me.HealthPercent <= SingularSettings.Instance.DisengageHealth) || ((int)ret) >= SingularSettings.Instance.DisengageMobCount),
                        new PrioritySelector(
                            Disengage.CreateDisengageBehavior("Blink", Disengage.Direction.Frontwards, 20, CreateSlowMeleeBehavior()),
                            Disengage.CreateDisengageBehavior("Rocket Jump", Disengage.Direction.Frontwards, 20, CreateSlowMeleeBehavior())
                            )
                        ),
                    new Decorator(
                        ret => SingularSettings.Instance.KiteAllow 
                            && ((((int)ret) > 0 && Me.HealthPercent <= SingularSettings.Instance.KiteHealth) || ((int)ret) >= SingularSettings.Instance.KiteMobCount),
                        new Sequence(
                            new Action( r => Logger.WriteDebug("MageAvoidance: requesting KITING!!!")),
                            Kite.BeginKitingBehavior()
                            )
                        )
                    )
                );
        }

        /*
        private static Composite CreateSlowMeleeBehavior()
        {
            return new Decorator(
                ret => Unit.NearbyUnfriendlyUnits.Any(u => u.SpellDistance() <= 8 && !u.Stunned && !u.Rooted && !u.IsSlowed()),
                new PrioritySelector(
                    new Decorator(
                        ret => Me.Specialization == WoWSpec.MageFrost,
                        Mage.Frost.CastFreeze(on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.SpellDistance() < 8), ClusterType.Radius, 8))
                        ),
                    Spell.Buff("Frost Nova"),
                    Spell.Buff("Frostjaw"),
                    // Spell.CastOnGround("Ring of Frost", loc => Me.Location, req => true, false),
                    Spell.Buff("Cone of Cold")
                    )
                );
        }
        */

        private static Composite CreateSlowMeleeBehavior()
        {
            return new PrioritySelector(
                ctx => SafeArea.NearestEnemyMobAttackingMe,
                new Action( ret => {
                    if (ret == null)
                        Logger.WriteDebug("SlowMelee: no nearest mob found");
                    else
                        Logger.WriteDebug("SlowMelee: crowdcontrolled: {0}, slowed: {1}", ((WoWUnit)ret).IsCrowdControlled(), ((WoWUnit)ret).IsSlowed());
                    return RunStatus.Failure;
                    }),
                new Decorator(
                    // ret => ret != null && !((WoWUnit)ret).Stunned && !((WoWUnit)ret).Rooted && !((WoWUnit)ret).IsSlowed(),
                    ret => ret != null && !((WoWUnit)ret).IsCrowdControlled() && !((WoWUnit)ret).IsSlowed(),
                    new PrioritySelector(
                        new Throttle(2,
                            new PrioritySelector(
                                new Decorator(
                                    ret => Me.Specialization == WoWSpec.MageFrost,
                                    Mage.Frost.CastFreeze(on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.SpellDistance() < 8), ClusterType.Radius, 8))
                                    ),
                                Spell.Cast("Frost Nova", mov => true, onUnit => (WoWUnit)onUnit, req => ((WoWUnit)req).SpellDistance() < 12, cancel => false),
                                Spell.Cast("Frostjaw", mov => true, onUnit => (WoWUnit)onUnit, req => true, cancel => false),
                                Spell.Cast("Cone of Cold", mov => true, onUnit => (WoWUnit)onUnit, req => true, cancel => false),
                                Spell.Cast("Frost Bolt", mov => true, onUnit => (WoWUnit)onUnit, req => true, cancel => false),
                                Spell.Cast("Frostfire Bolt", mov => true, onUnit => (WoWUnit)onUnit, req => true, cancel => false)
                                )
                            )
                        )
                    )
                );
        }

        #endregion

    }

    public enum MageTalents
    {
        None = 0,
        PresenceOfMind,
        BlazingSpeed,
        IceFloes,
        TemporalShield,
        Flameglow,
        IceBarrier,
        RingOfFrost,
        IceWard,
        Frostjaw,
        GreaterInivisibility,
        Cauterize,
        ColdSnap,
        NetherTempest,
        LivingBomb,
        FrostBomb,
        Invocation,
        RuneOfPower,
        IncantersWard
    }
}