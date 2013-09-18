
using System.Collections.Generic;
using System.Linq;

using Singular.Settings;
using Singular.Helpers;

using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using System;
using System.Drawing;
using CommonBehaviors.Actions;

using Action = Styx.TreeSharp.Action;

namespace Singular.Managers
{
    /*
     * Targeting works like so, in order of being called
     * 
     * GetInitialObjectList - Return a list of initial objects for the targeting to use.
     * RemoveTargetsFilter - Remove anything that doesn't belong in the list.
     * IncludeTargetsFilter - If you want to include units regardless of the remove filter
     * WeighTargetsFilter - Weigh each target in the list.     
     *
     */

    internal class HealerManager : Targeting
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        private static readonly WaitTimer _tankReset = WaitTimer.ThirtySeconds;

        // private static ulong _tankGuid;

        static HealerManager()
        {
            // Make sure we have a singleton instance!
            Instance = new HealerManager();
        }

        public new static HealerManager Instance { get; private set; }

        public static bool NeedHealTargeting { get; set; }

        private List<WoWUnit> HealList { get { return ObjectList.ConvertAll(o => o.ToUnit()); } }

        protected override List<WoWObject> GetInitialObjectList()
        {
            // Targeting requires a list of WoWObjects - so it's not bound to any specific type of object. Just casting it down to WoWObject will work fine.
            return ObjectManager.ObjectList.Where(o => o is WoWPlayer).ToList();
        }

        protected override void DefaultIncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
        {
            bool foundMe = false;
            bool isHorde = StyxWoW.Me.IsHorde;

            foreach (WoWObject incomingUnit in incomingUnits)
            {
                try
                {
                    if (incomingUnit.IsMe)
                        foundMe = true;

                    if (incomingUnit.ToPlayer().IsHorde != isHorde || !incomingUnit.ToPlayer().IsFriendly)
                        continue;

                    outgoingUnits.Add(incomingUnit);
                    if (SingularSettings.Instance.IncludePetsAsHealTargets && incomingUnit is WoWPlayer && incomingUnit.ToPlayer().GotAlivePet)
                        outgoingUnits.Add(incomingUnit.ToPlayer().Pet);
                }
                catch (System.AccessViolationException)
                {
                }
                catch (Styx.InvalidObjectPointerException)
                {
                }
            }

            if (!foundMe)
            {
                outgoingUnits.Add(StyxWoW.Me);
                if (SingularSettings.Instance.IncludePetsAsHealTargets && StyxWoW.Me.GotAlivePet)
                    outgoingUnits.Add(StyxWoW.Me.Pet);
            }

            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
            {
                /*
                if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.IsFriendly && !StyxWoW.Me.CurrentTarget.IsPlayer)
                    outgoingUnits.Add(StyxWoW.Me.CurrentTarget);
                */
                if (StyxWoW.Me.FocusedUnit != null && StyxWoW.Me.FocusedUnit.IsFriendly && !StyxWoW.Me.FocusedUnit.IsPet && !StyxWoW.Me.FocusedUnit.IsPlayer)
                    outgoingUnits.Add(StyxWoW.Me.FocusedUnit);
            }
        }

        protected override void DefaultRemoveTargetsFilter(List<WoWObject> units)
        {
            bool isHorde = StyxWoW.Me.IsHorde;
            int maxHealRangeSqr;
            
            if ( MovementManager.IsMovementDisabled)
                maxHealRangeSqr = 40 * 40;
            else
                maxHealRangeSqr = SingularSettings.Instance.MaxHealTargetRange * SingularSettings.Instance.MaxHealTargetRange;

            for (int i = units.Count - 1; i >= 0; i--)
            {
                WoWUnit unit = units[i].ToUnit();
                try
                {
                    if (unit == null || !unit.IsValid || unit.IsDead || !unit.IsFriendly || unit.HealthPercent <= 0)
                    {
                        units.RemoveAt(i);
                        continue;
                    }

                    WoWPlayer p = null;
                    if (unit is WoWPlayer)
                        p = unit.ToPlayer();
                    else if (unit.IsPet && unit.OwnedByRoot != null && unit.OwnedByRoot.IsPlayer)
                        p = unit.OwnedByRoot.ToPlayer();

                    if (p != null)
                    {
                        // Make sure we ignore dead/ghost players. If we need res logic, they need to be in the class-specific area.
                        if (p.IsGhost)
                        {
                            units.RemoveAt(i);
                            continue;
                        }

                        // They're not in our party/raid. So ignore them. We can't heal them anyway.
                        if (!p.IsInMyPartyOrRaid)
                        {
                            units.RemoveAt(i);
                            continue;
                        }
                        /*
                                            if (!p.Combat && p.HealthPercent >= SingularSettings.Instance.IgnoreHealTargetsAboveHealth)
                                            {
                                                units.RemoveAt(i);
                                                continue;
                                            }
                         */
                    }

                    // If we have movement turned off, ignore people who aren't in range.
                    // Almost all healing is 40 yards, so we'll use that. If in Battlegrounds use a slightly larger value to expane our 
                    // healing range, but not too large that we are running all over the bg zone 
                    // note: reordered following tests so only one floating point distance comparison done due to evalution of DisableAllMovement
                    if (unit.DistanceSqr > maxHealRangeSqr)
                    {
                        units.RemoveAt(i);
                        continue;
                    }
                }
                catch (System.AccessViolationException)
                {
                    units.RemoveAt(i);
                    continue;
                }
                catch (Styx.InvalidObjectPointerException)
                {
                    units.RemoveAt(i);
                    continue;
                }
            }
        }

        protected override void DefaultTargetWeight(List<TargetPriority> units)
        {
            var tanks = GetMainTankGuids();
            var inBg = Battlegrounds.IsInsideBattleground;
            var amHolyPally = StyxWoW.Me.Specialization == WoWSpec.PaladinHoly;

            foreach (TargetPriority prio in units)
            {
                WoWUnit u = prio.Object.ToUnit();
                if (u == null || !u.IsValid)
                {
                    prio.Score = -9999f;
                    continue;
                }

                // The more health they have, the lower the score.
                // This should give -500 for units at 100%
                // And -50 for units at 10%
                try
                {
                    prio.Score = u.IsAlive ? 500f : -500f;
                    prio.Score -= u.HealthPercent * 5;

                    // If they're out of range, give them a bit lower score.
                    if (u.DistanceSqr > 40 * 40)
                    {
                        prio.Score -= 50f;
                    }

                    // If they're out of LOS, again, lower score!
                    if (!u.InLineOfSpellSight)
                    {
                        prio.Score -= 100f;
                    }

                    // Give tanks more weight. If the tank dies, we all die. KEEP HIM UP.
                    if (tanks.Contains(u.Guid) && u.HealthPercent != 100 && 
                        // Ignore giving more weight to the tank if we have Beacon of Light on it.
                        (!amHolyPally || !u.Auras.Any(a => a.Key == "Beacon of Light" && a.Value.CreatorGuid == StyxWoW.Me.Guid)))
                    {
                        prio.Score += 100f;
                    }

                    // Give flag carriers more weight in battlegrounds. We need to keep them alive!
                    if (inBg && u.IsPlayer && u.Auras.Keys.Any(a => a.ToLowerInvariant().Contains("flag")))
                    {
                        prio.Score += 100f;
                    }
                }
                catch (System.AccessViolationException)
                {
                    prio.Score = -9999f;
                    continue;
                }
                catch (Styx.InvalidObjectPointerException)
                {
                    prio.Score = -9999f;
                    continue;
                }
            }
        }

        public override void Pulse()
        {
            if (NeedHealTargeting)
                base.Pulse();
        }

        public static HashSet<ulong> GetMainTankGuids()
        {
            var infos = StyxWoW.Me.GroupInfo.RaidMembers;

            return new HashSet<ulong>(
                from pi in infos
                where (pi.Role & WoWPartyMember.GroupRole.Tank) != 0
                select pi.Guid);
        }

        /// <summary>
        /// finds the lowest health target in HealerManager.  HealerManager updates the list over multiple pulses, resulting in 
        /// the .FirstUnit entry often being at higher health than later entries.  This method dynamically searches the current
        /// list and returns the lowest at this moment.
        /// </summary>
        /// <returns></returns>
        public static WoWUnit FindLowestHealthTarget()
        {
#if LOWEST_IS_FIRSTUNIT
            return HealerManager.Instance.FirstUnit;
#else
            double minHealth = 999;
            WoWUnit minUnit = null;

            // iterate the list so we make a single pass through it
            foreach (WoWUnit unit in HealerManager.Instance.TargetList)
            {
                try
                {
                    if (unit.HealthPercent < minHealth)
                    {
                        minHealth = unit.HealthPercent;
                        minUnit = unit;
                    }
                }
                catch
                {
                    // simply eat the exception here
                }
            }

            return minUnit;
#endif
        }


        public static WoWUnit GetBestCoverageTarget(string spell, int health, int range, int radius, int minCount, SimpleBooleanDelegate requirements = null, IEnumerable<WoWUnit> mainTarget = null)
        {
            if (!Me.IsInGroup() || !Me.Combat)
                return null;

            if (!Spell.CanCastHack(spell, Me, skipWowCheck: true))
            {
                if (!SingularSettings.Instance.DebugSpellCanCast)
                    Logger.WriteDebug("GetBestCoverageTarget: CanCastHack says NO to [{0}]", spell);
                return null;
            }

            if (requirements == null)
                requirements = req => true;

            // build temp list of targets that could use heal and are in range + radius
            List<WoWUnit> coveredTargets = HealerManager.Instance.TargetList
                .Where(u => u.IsAlive && u.SpellDistance() < (range + radius) && u.HealthPercent < health && requirements(u))
                .ToList();


            // create a iEnumerable of the possible heal targets wtihin range
            IEnumerable<WoWUnit> listOf;
            if (range == 0)
                listOf = new List<WoWUnit>() { Me };
            else if (mainTarget == null)
                listOf = HealerManager.Instance.TargetList.Where(p => p.IsAlive && p.SpellDistance() <= range);
            else
                listOf = mainTarget;

            // now search list finding target with greatest number of heal targets in radius
            var t = listOf
                .Select(p => new
                {
                    Player = p,
                    Count = coveredTargets
                        .Where(pp => pp.IsAlive && pp.SpellDistance(p) < radius)
                        .Count()
                })
                .OrderByDescending(v => v.Count)
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (t != null)
            {
                if (t.Count >= minCount)
                {
                    Logger.WriteDebug("GetBestCoverageTarget('{0}'): found {1} with {2} nearby under {3}%", spell, t.Player.SafeName(), t.Count, health);
                    return t.Player;
                }

                if (SingularSettings.Instance.DebugSpellCanCast)
                {
                    Logger.WriteDebug("GetBestCoverageTarget('{0}'): not enough found - {1} with {2} nearby under {3}%", spell, t.Player.SafeName(), t.Count, health);
                }
            }

            return null;
        }

        /// <summary>
        /// find best Tank target that is missing Heal Over Time passed
        /// </summary>
        /// <param name="hotName">spell name of HoT</param>
        /// <returns>reference to target that needs the HoT</returns>
        public static WoWUnit GetBestTankTargetForHOT( string hotName, float health = 100f)
        {
            WoWUnit hotTarget = null;
            hotTarget = Group.Tanks.Where(u => u.IsAlive && u.Combat && u.HealthPercent < health && u.DistanceSqr < 40 * 40 && !u.HasMyAura(hotName) && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();
            if (hotTarget != null)
                Logger.WriteDebug("GetBestTankTargetForHOT('{0}'): found tank {1} @ {2:F1}%, hasmyaura={3} with {4} ms left", hotName, hotTarget.SafeName(), hotTarget.HealthPercent, hotTarget.HasMyAura(hotName), (int)hotTarget.GetAuraTimeLeft("Riptide").TotalMilliseconds);
            return hotTarget;
        }

        public static WoWUnit TankToMoveTowards
        {
            get
            {
                if (!SingularSettings.Instance.StayNearTank)
                    return null;

                if (RaFHelper.Leader != null && RaFHelper.Leader.IsValid && RaFHelper.Leader.IsAlive && (RaFHelper.Leader.Combat || RaFHelper.Leader.Distance < SingularSettings.Instance.MaxHealTargetRange))
                    return RaFHelper.Leader;

                return Group.Tanks.Where(t => t.IsAlive && (t.Combat || t.Distance < SingularSettings.Instance.MaxHealTargetRange)).OrderBy(t => t.Distance).FirstOrDefault();
            }
        }

        private static int moveNearTank { get; set; }
        private static int stopNearTank { get; set; }

        public static Composite CreateStayNearTankBehavior()
        {
            if (!SingularSettings.Instance.StayNearTank )
                return new ActionAlwaysFail();

            if (SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                return new ActionAlwaysFail();

            moveNearTank = Math.Max( 10, SingularSettings.Instance.StayNearTankRange);
            stopNearTank = moveNearTank - 5;

            return new PrioritySelector(
                ctx => HealerManager.TankToMoveTowards,
                // no healing needed, then move within heal range of tank
                new Decorator(
                    ret => ((WoWUnit)ret) != null,
                    new Sequence(
                        new PrioritySelector(
                            Movement.CreateMoveToLosBehavior(unit => ((WoWUnit)unit)),
                            Movement.CreateMoveToUnitBehavior(unit => ((WoWUnit)unit), moveNearTank, stopNearTank),
                            Movement.CreateEnsureMovementStoppedBehavior( stopNearTank, unit => (WoWUnit)unit, "in range of tank")
                            ),
                        new ActionAlwaysFail()
                        )
                    )
                );
        }

        public static Composite CreateAttackEnsureTarget()
        {
            if (SingularSettings.DisableAllTargeting || SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                return new ActionAlwaysFail();

            return new PrioritySelector(
                new Decorator(
                    req => Me.GotTarget && !Me.CurrentTarget.IsPlayer,
                    new PrioritySelector(
                        ctx => Unit.HighestHealthMobAttackingTank(),
                        new Decorator(
                            req => req != null && Me.CurrentTargetGuid != ((WoWUnit)req).Guid && (Me.CurrentTarget.HealthPercent + 10) < ((WoWUnit)req).HealthPercent,
                            new Sequence(
                                new Action(on =>
                                {
                                    Logger.Write(Color.LightCoral, "switch to highest health mob {0} @ {1:F1}%", ((WoWUnit)on).SafeName(), ((WoWUnit)on).HealthPercent);
                                    ((WoWUnit)on).Target();
                                }),
                                new Wait(1, req => Me.CurrentTargetGuid == ((WoWUnit)req).Guid, new ActionAlwaysFail())
                                )
                            )
                        )
                    ),
                new Decorator(
                    req => !Me.GotTarget,
                    new Sequence(
                        ctx => Unit.HighestHealthMobAttackingTank(),
                        new Action(on =>
                        {
                            Logger.Write(Color.LightCoral, "target highest health mob {0} @ {1:F1}%", ((WoWUnit)on).SafeName(), ((WoWUnit)on).HealthPercent);
                            ((WoWUnit)on).Target();
                        }),
                        new Wait(1, req => Me.CurrentTargetGuid == ((WoWUnit)req).Guid, new ActionAlwaysFail())
                        )
                    )
                );
        }


    }

    class PrioritizedBehaviorList
    {
        class PrioritizedBehavior
        {
            public int Priority { get; set; }
            public string Name { get; set; }
            public Composite behavior { get; set; }

            public PrioritizedBehavior(int p, string s, Composite bt)
            {
                Priority = p;
                Name = s;
                behavior = bt;
            }
        }

        List<PrioritizedBehavior> blist = new List<PrioritizedBehavior>();

        public void AddBehavior(int pri, string behavName, string spellName, Composite bt)
        {
            if (pri == 0)
                Logger.WriteDebug("Skipping Behavior [{0}] configured for Priority {1}", behavName, pri);
            else if (!String.IsNullOrEmpty(spellName) && !SpellManager.HasSpell(spellName))
                Logger.WriteDebug("Skipping Behavior [{0}] since spell '{1}' is not known by this character", behavName, spellName);
            else
                blist.Add(new PrioritizedBehavior(pri, behavName, bt));
        }

        public void OrderBehaviors()
        {
            blist = blist.OrderByDescending(b => b.Priority).ToList();
        }

        public Composite GenerateBehaviorTree()
        {
            if ( !SingularSettings.Debug )
                return new PrioritySelector(blist.Select(b => b.behavior).ToArray());

            PrioritySelector pri = new PrioritySelector();
            foreach (PrioritizedBehavior pb in blist)
            {
                pri.AddChild(new CallTrace(pb.Name, pb.behavior));
            }

            return pri;
        }

        public void ListBehaviors()
        {
            if (Dynamics.CompositeBuilder.SilentBehaviorCreation)
                return;

            foreach (PrioritizedBehavior hs in blist)
            {
                Logger.WriteDebug(Color.GreenYellow, "   Priority {0} for Behavior [{1}]", hs.Priority.ToString().AlignRight(4), hs.Name);
            }
        }
    }

}