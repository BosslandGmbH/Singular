#define IGNORE_TARGETING_UNLESS_SEARCHING_FOR_NEW_TARGET
#define BOT_FIRSTUNIT_GETS_PRIORITY

using System.Drawing;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Settings;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Singular.Managers;
using Action = Styx.TreeSharp.Action;
using Styx.Helpers;
using System;

namespace Singular.Helpers
{
    internal static class Safers
    {
        private static Color targetColor = Color.LightCoral;

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        private static DateTime _timeNextInvalidTargetMessage = DateTime.MinValue;

#if WE_NEED_TO_REMOVE_FRIENDLY_CURRENT_TARGETS
        // following will work, but there is a larger issue of responsibility.  DungeonBuddy needs for Singular
        // to leave a friendly NPC targeted as an indication that it should be healed.   this gets a bit contradictory
        // when we also have code intending on removing friendly npcs from the target list (which would cause Singular
        // to change targets)
        // ----
        // just commenting out for now
        //
        static Safers()
        {
            Targeting.Instance.RemoveTargetsFilter += new RemoveTargetsFilterDelegate(Instance_RemoveTargetsFilter);
        }

        static void Instance_RemoveTargetsFilter(System.Collections.Generic.List<WoWObject> units)
        {
            for (int i = units.Count - 1; i >= 0; i--)
            {
                WoWUnit unit = units[i].ToUnit();
                if (unit == null || !unit.IsValid || unit.IsFriendly )
                {
                    units.RemoveAt(i);
                    continue;
                }
            }
        }
#endif

        /// <summary>
        ///  This behavior SHOULD be called at top of the combat behavior. This behavior won't let the rest of the combat behavior to be called
        /// if you don't have a target. Also it will find a proper target, if the current target is dead or you don't have a target and still in combat.
        /// Tank targeting is also dealed in this behavior.
        /// </summary>
        /// <returns></returns>
        public static Composite EnsureTarget()
        {

            return
                new Decorator(
                    ret => !SingularSettings.DisableAllTargeting,
                    new PrioritySelector(

#region Tank Targeting

                        new Decorator(
                            // DisableTankTargeting is a user-setting. NeedTankTargeting is an internal one. Make sure both are turned on.
                            ret => !SingularSettings.Instance.DisableTankTargetSwitching && Group.MeIsTank &&
                                   TankManager.TargetingTimer.IsFinished && StyxWoW.Me.Combat && TankManager.Instance.FirstUnit != null &&
                                   (StyxWoW.Me.CurrentTarget == null || StyxWoW.Me.CurrentTarget != TankManager.Instance.FirstUnit),
                            new Sequence(
                                // pending spells like mage blizard cause targeting to fail.
                                new DecoratorContinue(ctx => StyxWoW.Me.CurrentPendingCursorSpell != null,
                                    new Action(ctx => Lua.DoString("SpellStopTargeting()"))
                                    ),
                                new Action(
                                    ret =>
                                    {
                                        Logger.WriteDebug("EnsureTarget: Targeting first unit of TankTargeting");
                                        TankManager.Instance.FirstUnit.Target();
                                    }),
                                Helpers.Common.CreateWaitForLagDuration(),
                                new Action(ret => TankManager.TargetingTimer.Reset())
                                )
                            ),

#endregion

#region Switch from Current Target if a more important one exists!

                        new PrioritySelector(

#region Validate our CurrentTarget - ctx set to null if we need a new one, non-null if ok!

                            ctx => 
                            {
                                // No target switching for tanks. They check for their own stuff above.
                                if (Group.MeIsTank && !SingularSettings.Instance.DisableTankTargetSwitching)
                                    return null;

                                // Go below if current target is null or dead. We have other checks to deal with that
                                if (StyxWoW.Me.CurrentTarget == null || StyxWoW.Me.CurrentTarget.IsDead)
                                    return null;

                                // check if current target is owned by a player
                                if (StyxWoW.Me.CurrentTarget.OwnedByRoot != null )
                                {
                                    WoWUnit newTarget = Me.CurrentTarget.OwnedByRoot;
                                    if (newTarget.IsPlayer && Unit.ValidUnit(newTarget) && !Blacklist.Contains(newTarget, BlacklistFlags.Combat))
                                    {
                                        Logger.Write(targetColor, "Current target owned by a player.  Switching to " + newTarget.SafeName() + "!");
                                        if (BotPoi.Current.Type == PoiType.Kill && BotPoi.Current.Guid == Me.CurrentTarget.Guid)
                                            BotPoi.Clear("Singular detected as Player Owned Pet");

                                        return newTarget;
                                    }
                                }

                                // check if current target is summoned by a player
                                if (StyxWoW.Me.CurrentTarget.SummonedByUnit != null)
                                {
                                    WoWUnit newTarget = StyxWoW.Me.CurrentTarget;
                                    do {
                                        newTarget = newTarget.SummonedByUnit;
                                    } while (newTarget.SummonedByUnit != null);

                                    if (newTarget.IsPlayer && Unit.ValidUnit(newTarget) && !Blacklist.Contains(newTarget, BlacklistFlags.Combat))
                                    {
                                        Logger.Write(targetColor, "Current target summoned by a player.  Switching to " + newTarget.SafeName() + "!");
                                        if (BotPoi.Current.Type == PoiType.Kill && BotPoi.Current.Guid == Me.CurrentTarget.Guid)
                                            BotPoi.Clear("Singular detected as Player Summoned Pet");

                                        return newTarget;
                                    }
                                }

                                // Check botpoi (our top priority.)  we switch to BotPoi if a kill type exists and not blacklisted
                                // .. if blacklisted, clear the poi to give bot a chance to do something smarter
                                // .. if we are already fighting it, we keep fighting it, end of story
                                WoWUnit unit;
                                if (BotPoi.Current.Type == PoiType.Kill)
                                {
                                    unit = BotPoi.Current.AsObject.ToUnit();
                                    if (unit != null && unit.IsAlive && !unit.IsMe)
                                    {
                                        if (Blacklist.Contains(unit.Guid, BlacklistFlags.Combat))
                                        {
                                            Logger.Write(targetColor, "BotPOI " + unit.SafeName() + " is blacklisted -- clearing POI!");
                                            BotPoi.Clear("Singular detected Blacklisted mob");

                                            if (unit == Me.CurrentTarget && (Me.CurrentTarget.Combat && Me.CurrentTarget.IsTargetingMeOrPet))
                                                return unit;

                                            Logger.Write(targetColor, "Not in combat with blacklisted BotPOI " + unit.SafeName() + " so picking new target!");
                                            return null;
                                        }

                                        if (StyxWoW.Me.CurrentTargetGuid != unit.Guid )
                                            Logger.Write(targetColor, "Current target is not BotPOI.  Switching to " + unit.SafeName() + "!");

                                        return unit;
                                    }
                                }

                                // no valid BotPoi, so let's check Targeting.FirstUnit which is Bots #1 choice
#if IGNORE_TARGETING_UNLESS_SEARCHING_FOR_NEW_TARGET

#elif BOT_FIRSTUNIT_GETS_PRIORITY
                                unit = Targeting.Instance.FirstUnit;
                                if (unit != null && unit.IsAlive )
                                {
                                    if (Blacklist.Contains(unit.Guid, BlacklistFlags.Combat))
                                    {
                                        Logger.Write(targetColor, "Targeting.FirstUnit " + unit.SafeName() + " is blacklisted!");
                                        if (unit == Me.CurrentTarget && (Me.CurrentTarget.Combat && Me.CurrentTarget.IsTargetingMeOrPet))
                                            return unit;

                                        return null;
                                    }

                                    if (StyxWoW.Me.CurrentTarget != unit)
                                        Logger.Write(targetColor, "Current target is not Bots first choice.  Switching to " + unit.SafeName() + "!");

                                    return unit;
                                }
#else
                                foreach (var unit in Targeting.Instance.TargetList)
                                {
                                    if (StyxWoW.Me.CurrentTargetGuid != unit.Guid && unit.IsAlive && !Blacklist.Contains(unit.Guid, BlacklistFlags.Combat))
                                    {
                                        Logger.Write(targetColor, "Bot has a higher priority target available.  Switching to " + unit.SafeName() + "!");
                                        return unit;
                                    }
                                }
#endif
                                if (Blacklist.Contains(StyxWoW.Me.CurrentTargetGuid, BlacklistFlags.Combat))
                                {
                                    if (Me.CurrentTarget.Combat && Me.CurrentTarget.IsTargetingMeOrPet)
                                    {
                                        Logger.Write(targetColor, "Current target " + StyxWoW.Me.CurrentTarget.SafeName() + " blacklisted and Bot has no other targets!  Fighting this one and hoping Bot wakes up if its Evade bugged!");
                                        return Me.CurrentTarget;
                                    }

                                    Logger.Write(targetColor, "CurrentTarget blacklisted and not in combat with " + Me.CurrentTarget.SafeName() + " so clearing target!");
                                    Me.ClearTarget();
                                    return null;
                                }

                                // valid unit? keep it then
                                if (Unit.ValidUnit(Me.CurrentTarget))
                                    return Me.CurrentTarget;

                                // at this point, stick with it if in Targetlist
                                if (Targeting.Instance.TargetList.Contains(Me.CurrentTarget))
                                {
                                    Logger.WriteDebug("EnsureTarget: target is in TargetList, continuing...");
                                    return Me.CurrentTarget;
                                }

                                // otherwise, let's get a new one
                                Logger.WriteDebug("EnsureTarget: invalid target, so forcing selection of a new one");
                                return null;
                            },

#endregion

#region Change targets if better target was found

                            new Decorator(
                                ret => ret != null && ((WoWUnit)ret).Guid != StyxWoW.Me.CurrentTargetGuid,
                                new Sequence(
                                    new DecoratorContinue(
                                        ret => StyxWoW.Me.CurrentPendingCursorSpell != null,
                                        new Action(ctx => Lua.DoString("SpellStopTargeting()"))
                                        ),
                                    new Action(ret => ((WoWUnit)ret).Target()),
                                    new WaitContinue( 2, ret => StyxWoW.Me.CurrentTarget != null && StyxWoW.Me.CurrentTarget == (WoWUnit)ret, new ActionAlwaysSucceed())
                                    )
                                ),

#endregion

#endregion

#region Target Invalid (none or dead) - Find a New one if possible

                            new Decorator(
                                ret => StyxWoW.Me.CurrentTarget == null || StyxWoW.Me.CurrentTarget.IsDead || !Unit.ValidUnit(Me.CurrentTarget),
                                new PrioritySelector(
                                    ctx =>
                                    {
                                        // If we have a RaF leader, then use its target.
                                        var rafLeader = RaFHelper.Leader;
                                        if (rafLeader != null && rafLeader.IsValid && !rafLeader.IsMe && rafLeader.Combat &&
                                            rafLeader.CurrentTarget != null && rafLeader.CurrentTarget.IsAlive && !Blacklist.Contains(rafLeader.CurrentTarget, BlacklistFlags.Combat))
                                        {
                                            Logger.Write(targetColor, "Current target invalid. Switching to Tanks target " + rafLeader.CurrentTarget.SafeName() + "!");
                                            return rafLeader.CurrentTarget;
                                        }

                                        // if we have BotPoi then try it
                                        if (BotPoi.Current.Type == PoiType.Kill)
                                        {
                                            var unit = BotPoi.Current.AsObject as WoWUnit;
                                            if (unit == null)
                                            {
                                                Logger.Write(targetColor, "Current Kill POI invalid. Clearing POI!");
                                                BotPoi.Clear("Singular detected null POI");
                                            }
                                            else if (!unit.IsAlive)
                                            {
                                                Logger.Write(targetColor, "Current Kill POI dead. Clearing POI " + unit.SafeName() + "!");
                                                BotPoi.Clear("Singular detected Unit is dead");
                                            }
                                            else if (Blacklist.Contains(unit, BlacklistFlags.Combat))
                                            {
                                                Logger.Write(targetColor, "Current Kill POI is blacklisted. Clearing POI " + unit.SafeName() + "!");
                                                BotPoi.Clear("Singular detected Unit is Blacklisted");
                                            }
                                            else 
                                            {
                                                Logger.Write(targetColor, "Current target invalid. Switching to POI " + unit.SafeName() + "!");
                                                return unit;
                                            }
                                        }

                                        // Look for agrroed mobs next
                                        var target = ObjectManager.GetObjectsOfType<WoWUnit>(false, false)
                                            .Where(
                                                p => !Blacklist.Contains(p, BlacklistFlags.Combat)
                                                && Unit.ValidUnit(p)
                                                // && p.DistanceSqr <= 40 * 40  // dont restrict check to 40 yds
                                                && (p.Aggro || p.PetAggro || (p.Combat && p.GotTarget && (p.IsTargetingMeOrPet || p.IsTargetingMyRaidMember))))
                                            .OrderBy(u => u.IsPlayer)
                                            .ThenBy(u => u.DistanceSqr)
                                            .FirstOrDefault();

                                        if (target != null)
                                        {
                                            // Return the closest one to us
                                            Logger.Write(targetColor, "Current target invalid. Switching to aggroed mob " + target.SafeName() + "!");
                                            return target;
                                        }

                                        // now anything in the target list or a Player
                                        target = Targeting.Instance.TargetList
                                            .Where(
                                                p => !Blacklist.Contains(p, BlacklistFlags.Combat)
                                                && p.IsAlive
                                                // && p.DistanceSqr <= 40 * 40 // don't restrict check to 40 yds
                                                )
                                            .OrderBy(u => u.IsPlayer)
                                            .ThenBy(u => u.DistanceSqr)
                                            .FirstOrDefault();

                                        if (target != null)
                                        {
                                            // Return the closest one to us
                                            Logger.Write(targetColor, "Current target invalid. Switching to TargetList mob " + target.SafeName() + "!");
                                            return target;
                                        }

    /*
                                        // Cache this query, since we'll be using it for 2 checks. No need to re-query it.
                                        var agroMob =
                                            ObjectManager.GetObjectsOfType<WoWUnit>(false, false)
                                                .Where(p => !Blacklist.Contains(p, BlacklistFlags.Combat) && p.IsHostile && !p.IsDead
                                                        && !p.Mounted && p.DistanceSqr <= 70 * 70 && p.IsPlayer && p.Combat && (p.IsTargetingMeOrPet || p.IsTargetingMyRaidMember))
                                                .OrderBy(u => u.DistanceSqr)
                                                .FirstOrDefault();

                                        if (agroMob != null)
                                        {
                                            if (!agroMob.IsPet || agroMob.SummonedByUnit == null)
                                            {
                                                Logger.Write(targetColor, "Current target invalid. Switching to player attacking us " + agroMob.SafeName() + "!");
                                            }
                                            else
                                            {
                                                Logger.Write(targetColor, "Current target invalid. Enemy player pet {0} attacking us, switching to player {1}!", agroMob.SafeName(), agroMob.SummonedByUnit.SafeName());
                                                agroMob = agroMob.SummonedByUnit;
                                            }

                                            return agroMob;
                                        }
    */
                                        // And there's nothing left, so just return null, kthx.
                                        // ... but show a message about botbase still calling our Combat behavior with nothing to kill
                                        if ( DateTime.Now >= _timeNextInvalidTargetMessage)
                                        {
                                            _timeNextInvalidTargetMessage = DateTime.Now + TimeSpan.FromSeconds(1);
                                            Logger.Write(targetColor, "Current target invalid.  No other targets available");
                                        }

                                        return null;
                                    },

                                    // Make sure the target is VALID. If not, then ignore this next part. (Resolves some silly issues!)
                                    new Decorator(
                                        ret => ret != null && ((WoWUnit)ret).Guid != StyxWoW.Me.CurrentTargetGuid,
                                        new Sequence(
                                            new DecoratorContinue(
                                                ret => StyxWoW.Me.CurrentPendingCursorSpell != null,
                                                new Sequence(
                                                    new Action( r => Logger.WriteDebug( "EnsureTarget: /cancel Pending Spell {0}", StyxWoW.Me.CurrentPendingCursorSpell.Name)),
                                                    new Action(ctx => Lua.DoString("SpellStopTargeting()"))
                                                    )
                                                ),
                                            new Action(ret => Logger.WriteDebug("EnsureTarget: set target to chosen target {0}", ((WoWUnit)ret).SafeName())),
                                            new Action(ret => ((WoWUnit)ret).Target()),
                                            new WaitContinue( 2, ret => StyxWoW.Me.CurrentTarget != null && StyxWoW.Me.CurrentTargetGuid == ((WoWUnit)ret).Guid, new ActionAlwaysSucceed())
                                            )
                                        ),

                                    new ActionAlwaysSucceed()
                                    )
                                )
                            )
#endregion

                        )
                    );
        }
    }
}
