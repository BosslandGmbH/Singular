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

namespace Singular.Helpers
{
    internal static class Safers
    {
        private static Color targetColor = Color.LightCoral;

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
                                        Logger.WriteDebug("Targeting first unit of TankTargeting");
                                        TankManager.Instance.FirstUnit.Target();
                                    }),
                                Helpers.Common.CreateWaitForLagDuration(),
                                new Action(ret => TankManager.TargetingTimer.Reset())
                                )
                            ),

#endregion

#region Switch from Current Target if a more important one exists!

                        new PrioritySelector(

#region Identify a New Target if we should attack something else!

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
                                    if (StyxWoW.Me.CurrentTarget.OwnedByRoot.IsPlayer)
                                    {
                                        Logger.Write(targetColor, "Current target owned by a player.  Switching to " + StyxWoW.Me.CurrentTarget.OwnedByRoot.SafeName() + "!");
                                        return StyxWoW.Me.CurrentTarget.OwnedByRoot;
                                    }
                                }

                                // check if current target is summoned by a player
                                if (StyxWoW.Me.CurrentTarget.SummonedByUnit != null)
                                {
                                    WoWUnit newTarget = StyxWoW.Me.CurrentTarget;
                                    do {
                                        newTarget = newTarget.SummonedByUnit;
                                    } while (newTarget.SummonedByUnit != null);

                                    if (newTarget.IsPlayer )
                                    {
                                        Logger.Write(targetColor, "Current target summoned by a player.  Switching to " + newTarget.SafeName() + "!");
                                        return newTarget;
                                    }
                                }

                                // If the current target is in combat or has aggro towards us, it should be a valid target.
                                if (!Blacklist.Contains(StyxWoW.Me.CurrentTargetGuid, BlacklistFlags.Combat) && (StyxWoW.Me.CurrentTarget.Combat || StyxWoW.Me.CurrentTarget.Aggro))
                                    return null;

                                // Check botpoi first and make sure our target is set to POI's object.
                                if (BotPoi.Current.Type == PoiType.Kill )
                                {
                                    var obj = BotPoi.Current.AsObject;

                                    if (obj != null && ((WoWUnit)obj).IsAlive )
                                    {
                                        if (Blacklist.Contains(obj.Guid, BlacklistFlags.Combat))
                                        {
                                            Logger.Write(targetColor, "BotPOI " + obj.SafeName() + " is blacklisted -- clearing POI!");
                                            BotPoi.Clear("Blacklisted mob");
                                        }
                                        else if (StyxWoW.Me.CurrentTargetGuid == obj.Guid )
                                        {
                                            Logger.WriteDebug(targetColor, "Current target is BotPOI, continuing...");
                                            return obj;
                                        }
                                        else
                                        {
                                            Logger.Write(targetColor, "Current target is not BotPOI.  Switching to " + obj.SafeName() + "!");
                                            return obj;
                                        }
                                    }
                                }

                                // Make sure we have the proper target from Targeting. 
                                // The Botbase should give us the best target in targeting.
#if BOT_FIRSTUNIT_GETS_PRIORITY
                                var firstUnit = Targeting.Instance.FirstUnit;
                                if (firstUnit != null && firstUnit.IsAlive )
                                {
                                    if (Blacklist.Contains(firstUnit.Guid, BlacklistFlags.Combat))
                                    {
                                        Logger.Write(targetColor, "Bot FirstUnit is blacklist -- ignoring!");
                                    }
                                    else if (StyxWoW.Me.CurrentTarget == firstUnit)
                                    {
                                        Logger.WriteDebug("Current target already Bots first choice, continuing...");
                                        return firstUnit;
                                    }
                                    else
                                    {
                                        Logger.Write(targetColor, "Current target is not Bots first choice.  Switching to " + firstUnit.SafeName() + "!");
                                        return firstUnit;
                                    }
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
                                    Logger.Write(targetColor, "Current target " + StyxWoW.Me.CurrentTarget.SafeName() + " blacklisted and Bot has no other targets!  Clearing target and hoping Bot wakes up!");
                                    StyxWoW.Me.ClearTarget();
                                    return null;
                                }

                                return null;
                            },

#endregion

#region Change targets if better target was found

                            new Decorator(
                                ret => ret != null && ((WoWUnit)ret).Guid != StyxWoW.Me.CurrentTargetGuid,
                                new Sequence(
                                    new Action(ret => ((WoWUnit)ret).Target()),
                                    new WaitContinue(
                                        2,
                                        ret => StyxWoW.Me.CurrentTarget != null && StyxWoW.Me.CurrentTarget == (WoWUnit)ret,
                                        new ActionAlwaysSucceed())
                                    )
                                )

#endregion

                            ),

#endregion

#region Target Invalid (none or dead) - Find a New one if possible

                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget == null || StyxWoW.Me.CurrentTarget.IsDead,
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

                                    // Check bot poi.
                                    if (BotPoi.Current.Type == PoiType.Kill)
                                    {
                                        var unit = BotPoi.Current.AsObject as WoWUnit;

                                        if (unit != null && unit.IsAlive && !unit.IsMe && !Blacklist.Contains(unit, BlacklistFlags.Combat))
                                        {
                                            Logger.Write(targetColor, "Current target invalid. Switching to POI " + unit.SafeName() + "!");
                                            return unit;
                                        }

                                        if (unit == null )
                                        {
                                            Logger.Write(targetColor, "Current Kill POI invalid. Clearing POI!");
                                            BotPoi.Clear();
                                        }
                                        else if (!unit.IsAlive)
                                        {
                                            Logger.Write(targetColor, "Current Kill POI dead. Clearing POI " + unit.SafeName() + "!");
                                            BotPoi.Clear();
                                        }
                                    }

                                    // Does the target list have anything in it? And is the unit in combat?
                                    // Make sure we only check target combat, if we're NOT in a BG. (Inside BGs, all targets are valid!!)
                                    var firstUnit = Targeting.Instance.FirstUnit;
                                    if (firstUnit != null && firstUnit.IsAlive && !firstUnit.IsMe && firstUnit.Combat &&
                                        !Blacklist.Contains(firstUnit, BlacklistFlags.Combat))
                                    {
                                        Logger.Write(targetColor, "Current target invalid. Switching to Bot First Unit " + firstUnit.SafeName() + "!");
                                        return firstUnit;
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
                                    // Cache this query, since we'll be using it for 2 checks. No need to re-query it.
                                    var agroMob = ObjectManager.GetObjectsOfType<WoWUnit>(false, false)
                                        .Where(
                                            p => !Blacklist.Contains(p, BlacklistFlags.Combat) 
                                            && Unit.ValidUnit(p)
                                            && p.DistanceSqr <= 40 * 40
                                            && (p.Aggro || p.PetAggro || (p.Combat && p.GotTarget && (p.IsTargetingMeOrPet || p.IsTargetingMyRaidMember))))
                                        .OrderBy(u => u.IsPlayer )
                                        .ThenBy(u => u.DistanceSqr)
                                        .FirstOrDefault();

                                    if (agroMob != null)
                                    {
                                        // Return the closest one to us
                                        Logger.Write(targetColor, "Current target invalid. Switching to aggroed mob " + agroMob.SafeName() + "!");
                                        return agroMob;
                                    }

                                    // And there's nothing left, so just return null, kthx.
                                    Logger.Write(targetColor, "Current target invalid.  No other targets available");
                                    return null;
                                },
                                // Make sure the target is VALID. If not, then ignore this next part. (Resolves some silly issues!)
                                new Decorator(
                                    ret => ret != null,
                                    new Sequence(
                                        // pending spells like mage blizard cause targeting to fail.
                                        new DecoratorContinue(ctx => StyxWoW.Me.CurrentPendingCursorSpell != null,
                                            new Action(ctx => Lua.DoString("SpellStopTargeting()"))),
                                        new Action(ret => ((WoWUnit)ret).Target()),
                                        new WaitContinue(
                                            2,
                                            ret => StyxWoW.Me.CurrentTarget != null &&
                                                   StyxWoW.Me.CurrentTarget == (WoWUnit)ret,
                                            new ActionAlwaysSucceed())
                                        )
                                    ),
                                new ActionAlwaysSucceed()
                                )
                            )

#endregion

                        )
                    );
        }
    }
}
