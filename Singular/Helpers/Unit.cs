using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.DBC;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Singular.Dynamics;
using Singular.Managers;
using Singular.Settings;
using Singular.Utilities;

namespace Singular.Helpers
{
    internal static class Unit
    {
        public static int TrivialLevel { get; set; }
        public static int TrivialElite { get; set; }
        public static uint SeriousHealth { get; set; }


        [Behavior(BehaviorType.Initialize, priority: int.MaxValue)]
        public static Composite InitializeUnit()
        {
            TrivialLevel = StyxWoW.Me.Level - SingularSettings.Instance.TrivialLevelsBelow;
            TrivialElite = StyxWoW.Me.Level - SingularSettings.Instance.TrivialEliteBelow;

            Logger.WriteFile("  {0}: {1}", "TrivialLevel", Unit.TrivialLevel);
            Logger.WriteFile("  {0}: {1}", "TrivialElite", Unit.TrivialElite);
            Logger.WriteFile("  {0}: {1}", "NeedTankTargeting", TankManager.NeedTankTargeting);
            Logger.WriteFile("  {0}: {1}", "NeedHealTargeting", HealerManager.NeedHealTargeting);
            return null;
        }

        public static HashSet<uint> IgnoreMobs = new HashSet<uint>
            {
                52288, // Venomous Effusion (NPC near the snake boss in ZG. Its the green lines on the ground. We want to ignore them.)
                52302, // Venomous Effusion Stalker (Same as above. A dummy unit)
                52320, // Pool of Acid
                52525, // Bloodvenom

                52387, // Cave in stalker - Kilnara
            };

        /// <summary>
        /// checks if unit has a current target.  Differs from WoWUnit.GotTarget since it
        /// will only return true if targeting a WoWUnit
        /// </summary>
        /// <param name="unit">unit to check for a CurrentTarget</param>
        /// <returns>false: if CurrentTarget == null, otherwise true</returns>
        public static bool GotTarget(this WoWUnit unit)
        {
            return unit.CurrentTarget != null;
        }

        public static bool IsUndeadOrDemon(this WoWUnit unit)
        {
            return unit.CreatureType == WoWCreatureType.Undead
                    || unit.CreatureType == WoWCreatureType.Demon;
        }

        /// <summary>
        /// determines if unit is a melee toon based upon .Class.  for Shaman and Druids 
        /// will return based upon presence of aura 
        /// </summary>
        /// <param name="unit">unit to test for melee-ness</param>
        /// <returns>true: melee toon, false: probably not</returns>
        public static bool IsMelee(this WoWUnit unit)
        {
            if (unit.Class == WoWClass.DeathKnight
                || unit.Class == WoWClass.Paladin
                || unit.Class == WoWClass.Rogue
                || unit.Class == WoWClass.Warrior)
                return true;

            if (!unit.IsMe)
            {
                if (unit.Class == WoWClass.Hunter
                    || unit.Class == WoWClass.Mage
                    || unit.Class == WoWClass.Priest
                    || unit.Class == WoWClass.Warlock)
                    return false;

                if (unit.Class == WoWClass.Monk)    // treat all enemy Monks as melee
                    return true;

                if (unit.Class == WoWClass.Druid && unit.HasAnyShapeshift(ShapeshiftForm.Cat, ShapeshiftForm.Bear))
                    return true;

                if (unit.Class == WoWClass.Shaman && unit.GetAllAuras().Any(a => a.Name == "Unleashed Rage" && a.CreatorGuid == unit.Guid))
                    return true;

                return false;
            }

            switch (TalentManager.CurrentSpec)
            {
                case WoWSpec.DruidFeral:
                case WoWSpec.DruidGuardian:
                case WoWSpec.MonkBrewmaster:
                case WoWSpec.MonkWindwalker:
                case WoWSpec.ShamanEnhancement:
                    return true;
            }

            return false;
        }


        /// <summary>
        /// List of WoWPlayer in your Group. Deals with Party / Raid in a list independent manner and does not restrict distance
        /// </summary>
        public static IEnumerable<WoWUnit> GroupMembers
        {
            get
            {
                HashSet<WoWGuid> guids = new HashSet<WoWGuid>( StyxWoW.Me.GroupInfo.RaidMemberGuids);
                guids.Add(StyxWoW.Me.Guid);
                List<WoWUnit> list = ObjectManager.ObjectList
                    .Where(o => IsUnit(o) && guids.Contains(o.Guid))
                    .Select(o => o.ToUnit())
                    .ToList();
                return list;
            }
        }

        public static IEnumerable<WoWUnit> GroupMembersAndPets
        {
            get
            {
                HashSet<WoWGuid> guids = new HashSet<WoWGuid>( StyxWoW.Me.GroupInfo.RaidMemberGuids );
                List<WoWUnit> list = ObjectManager.ObjectList
                    .Where(o => IsUnit(o) && (guids.Contains(o.Guid) || (o.ToUnit().IsPet && guids.Contains(o.ToUnit().SummonedByUnitGuid))))
                    .Select( o => o.ToUnit())
                    .ToList();
                return list;
            }
        }

        public static bool IsUnit(WoWObject o)
        {
            if (o != null)
            {
                try
                {
                    if (o.ToUnit() != null)
                        return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// List of WoWPartyMember in your Group. Deals with Party / Raid in a list independent manner and does not restrict distance
        /// </summary>
        public static IEnumerable<WoWPartyMember> GroupMemberInfos
        {
            get { return StyxWoW.Me.GroupInfo.RaidMembers.Union(StyxWoW.Me.GroupInfo.PartyMembers).Distinct(); }
        }

        public static IEnumerable<WoWUnit> NearbyGroupMembers
        {
            get
            {
                return GroupMembers.Where(p => p.DistanceSqr <= 40 * 40).ToList();
            }
        }

        public static IEnumerable<WoWUnit> NearbyGroupMembersAndPets
        {
            get
            {
                return GroupMembersAndPets.Where(p => p.DistanceSqr <= 40 * 40).ToList();
            }
        }

        /// <summary>
        ///   Gets the nearby friendly players that can be seen
        /// </summary>
        /// <value>The nearby friendly players.</value>
        public static IEnumerable<WoWPlayer> FriendlyPlayers(double range = 100.0)
        {
            if (range >= 100.0)
                return ObjectManager.GetObjectsOfType<WoWPlayer>(false, true).Where(p => p.IsFriendly).ToList();

            range *= range;
            return ObjectManager.GetObjectsOfType<WoWPlayer>(false, true).Where(p => p.IsFriendly && p.DistanceSqr < range).ToList();
        }

        /// <summary>
        ///   Gets the nearby friendly units that can be seen
        /// </summary>
        /// <value>The nearby friendly units.</value>
        public static IEnumerable<WoWUnit> FriendlyUnit(double range = 100.0)
        {
            if (range >= 100.0)
                return ObjectManager.GetObjectsOfType<WoWUnit>(false, true).Where(p => p.IsFriendly).ToList();

            range *= range;
            return ObjectManager.GetObjectsOfType<WoWUnit>(false, true).Where(p => p.IsFriendly && p.DistanceSqr < range).ToList();
        }

        /// <summary>
        ///   Gets the nearby friendly players within 40 yards.
        /// </summary>
        /// <value>The nearby friendly players.</value>
        public static IEnumerable<WoWPlayer> NearbyFriendlyPlayers
        {
            get
            {
                return FriendlyPlayers(40);
            }
        }

        /// <summary>
        ///   Gets the nearby unfriendly units within specified range.  if no range specified, 
        ///   includes all unfriendly units
        /// </summary>
        /// <value>The nearby unfriendly units.</value>
        public static IEnumerable<WoWUnit> UnfriendlyUnits(int maxSpellDist = -1, WoWUnit origin = null )
        {
            if (origin == null)
                origin = StyxWoW.Me;

            // need to use TargetList for this if in Dungeon
            bool useTargeting = (SingularRoutine.IsDungeonBuddyActive || (SingularRoutine.IsQuestBotActive && StyxWoW.Me.IsInInstance));
            if (useTargeting)
            {
                if ( maxSpellDist == -1)
                    return Targeting.Instance.TargetList.Where(u => u != null && ValidUnit(u));
                return Targeting.Instance.TargetList.Where(u => u != null && ValidUnit(u) && origin.SpellDistance(u) < maxSpellDist);
            }

            List<WoWUnit> list = new List<WoWUnit>();
            List<WoWObject> objectList = ObjectManager.ObjectList;
           
            for (int i = 0; i < objectList.Count; i++)
            {
                Type type = objectList[i].GetType();
                if (type == typeof(WoWUnit) || type == typeof(WoWPlayer))
                {
                    WoWUnit t = objectList[i] as WoWUnit;
                    if (t != null && ValidUnit(t) && (maxSpellDist == -1 || origin.SpellDistance(t) < maxSpellDist ))
                    {
                        list.Add(t);
                    }
                }
            }

            return list;
        }

        /// <summary>
        ///   Gets the nearby unfriendly units within 40 yards.
        /// </summary>
        /// <value>The nearby unfriendly units.</value>
        public static IEnumerable<WoWUnit> NearbyUnfriendlyUnits
        {
            get
            {
                return UnfriendlyUnits(40);
            }
        }

        public static IEnumerable<WoWUnit> UnitsInCombatWithMe(int maxSpellDist = -1)
        {
            return UnfriendlyUnits(maxSpellDist)
                .Where(
                    p => (p.Aggro || (p.Combat && p.TaggedByMe)) && p.CurrentTargetGuid == StyxWoW.Me.Guid
                        || (p == EventHandlers.AttackingEnemyPlayer && EventHandlers.TimeSinceAttackedByEnemyPlayer.TotalSeconds < 15)
                    );
        }

        public static IEnumerable<WoWUnit> NearbyUnitsInCombatWithMe
        {
            get { return NearbyUnfriendlyUnits.Where(p => p.Aggro || (p.Combat && p.CurrentTargetGuid == StyxWoW.Me.Guid)); }
        }

        public static IEnumerable<WoWUnit> UnitsInCombatWithMeOrMyStuff(int maxSpellDist = -1)
        {
            return UnfriendlyUnits(maxSpellDist)
                .Where(
                    p => p.Aggro
                        || (p.Combat
                            && (p.TaggedByMe
                                || (p.GotTarget() && p.IsTargetingMyStuff())
                                || (p == EventHandlers.AttackingEnemyPlayer && EventHandlers.TimeSinceAttackedByEnemyPlayer.TotalSeconds < 15)
                                )
                            )
                    );
        }

        public static IEnumerable<WoWUnit> NearbyUnitsInCombatWithMeOrMyStuff
        {
            get { return NearbyUnfriendlyUnits.Where(p => p.Aggro || (p.Combat && (p.TaggedByMe || (p.GotTarget() && p.IsTargetingMyStuff())))); }
        }

        public static IEnumerable<WoWUnit> UnitsInCombatWithUsOrOurStuff(int maxSpellDist = -1)
        {
            return UnfriendlyUnits(maxSpellDist)
                .Where(
                    p => p.Aggro 
                        || (p.Combat 
                            && (p.TaggedByMe 
                                || (p.GotTarget() && p.IsTargetingUs())
                                || (p == EventHandlers.AttackingEnemyPlayer && EventHandlers.TimeSinceAttackedByEnemyPlayer.TotalSeconds < 15)
                                )
                            )
                    ); 
        }

        public static IEnumerable<WoWUnit> NearbyUnitsInCombatWithUsOrOurStuff
        {
            get { return UnitsInCombatWithUsOrOurStuff(40); }
        }


        private static Color invalidColor = Color.LightCoral;

        public static bool ValidUnit(WoWUnit p, bool showReason = false)
        {
            if (p == null || !p.IsValid)
                return false;

            if (StyxWoW.Me.IsInInstance && IgnoreMobs.Contains(p.Entry))
            {
                if (showReason) Logger.Write(invalidColor, "invalid attack unit {0} is an Instance Ignore Mob", p.SafeName());
                return false;
            }

            // Ignore shit we can't select
            if (!p.CanSelect )
            {
                if (showReason) Logger.Write(invalidColor, "invalid attack unit {0} cannot be Selected", p.SafeName());
                return false;
            }

            // Ignore shit we can't attack
            if (!p.Attackable)
            {
                if (showReason) Logger.Write(invalidColor, "invalid attack unit {0} cannot be Attacked", p.SafeName());
                return false;
            }

            // Duh
            if (p.IsDead)
            {
                if (showReason) Logger.Write(invalidColor, "invalid attack unit {0} is already Dead", p.SafeName());
                return false;
            }

            // check for enemy players here as friendly only seems to work on npc's
            if (p.IsPlayer)
            {
                WoWPlayer pp = p.ToPlayer();
                if (pp.IsHorde == StyxWoW.Me.IsHorde && !pp.IsHostile)
                {
                    if (showReason)
                        Logger.Write(invalidColor, "invalid attack player {0} not a hostile enemy", p.SafeName());
                    return false;
                }

                if (!pp.CanWeAttack())
                {
                    if (showReason)
                        Logger.Write(invalidColor, "invalid attack player {0} cannot be Attacked currently", p.SafeName());
                    return false;
                }

                return true;
            }

            // Ignore evading NPCs 
            if (p.IsEvading())
            {
                if (showReason)
                    Logger.Write(invalidColor, "invalid unit, {0} game flagged as evading", p.SafeName());
                return false;
            }

            // Ignore friendlies!
            if (p.IsFriendly)
            {
                if (showReason) Logger.Write(invalidColor, "invalid attack unit {0} is Friendly", p.SafeName());
                return false;
            }

            // Dummies/bosses are valid by default. Period.
            if (p.IsTrainingDummy() || p.IsBoss())
                return true;

            // If it is a pet/minion/totem, lets find the root of ownership chain
            WoWPlayer pOwner = GetPlayerParent(p);

            // ignore if owner is player, alive, and not blacklisted then ignore (since killing owner kills it)
            // .. following .IsMe check to prevent treating quest mob summoned by us that we need to kill as invalid 
            if (pOwner != null && pOwner.IsAlive && !pOwner.IsMe)
            {
                if (!ValidUnit(pOwner))
                {
                    if (showReason)
                        Logger.Write(invalidColor, "invalid attack unit {0} - pets parent not an attackable Player", p.SafeName());
                    return false;
                }
                if (!StyxWoW.Me.IsPvPFlagged)
                {
                    if (showReason)
                        Logger.Write(invalidColor, "valid attackable player {0} but I am not PvP Flagged", p.SafeName());
                    return false;
                }
                if (Blacklist.Contains(pOwner, BlacklistFlags.Combat))
                {
                    if (showReason)
                        Logger.Write(invalidColor, "invalid attack unit {0} - Parent blacklisted for combat", p.SafeName());
                    return false;
                }

                return true;
            }

            // And ignore non-combat pets
            if (p.IsNonCombatPet)
            {
                if (showReason) Logger.Write(invalidColor, "{0} is a Noncombat Pet", p.SafeName());
                return false;
            }

            // And ignore critters (except for those ferocious ones or if set as BotPoi)
            if (IsIgnorableCritter(p))
            {
                if (showReason) Logger.Write(invalidColor, "{0} is a Critter", p.SafeName());
                return false;
            }

            /*
                        if (p.CreatedByUnitGuid != 0 || p.SummonedByUnitGuid != 0)
                            return false;
            */
            return true;
        }

        public static bool IsEvading(this WoWUnit u)
        {
            return (u.Flags & 0x10) != 0;
        }

        /// <summary>
        /// Checks if target is a Critter that can safely be ignored
        /// </summary>
        /// <param name="u"></param>
        /// WoWUnit to check
        /// <returns>true: can ignore safely, false: treat as attackable enemy</returns>
        public static bool IsIgnorableCritter(this WoWUnit u)
        {
            if (!u.IsCritter)
                return false;

            // good enemy if BotPoi
            if (Styx.CommonBot.POI.BotPoi.Current.Guid == u.Guid && Styx.CommonBot.POI.BotPoi.Current.Type == Styx.CommonBot.POI.PoiType.Kill)
                return false;

            // good enemy if Targeting
            if (Targeting.Instance.TargetList.Contains(u))
                return false;

            // good enemy if Threat towards us
            if (u.ThreatInfo.ThreatValue != 0 && u.IsTargetingMyRaidMember)
                return false;

            // Nah, just a harmless critter
            return true;
        }

        public static bool IsTrivial(this WoWUnit unit)
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return false;

            if (unit == null)
                return false;

            if (unit.Elite)
                return unit.Level <= TrivialElite;                
            
            return unit.Level <= TrivialLevel;
        }

        public static bool IsStressful(this WoWUnit unit)
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return true;

            if (unit == null)
                return false;

            if (unit.IsPlayer)
                return true;

            uint maxh = unit.MaxHealth;
            return maxh > StyxWoW.Me.MaxHealth * 2 || unit.Level > (StyxWoW.Me.Level + (unit.Elite ? -6 : 2));
        }

        public static bool IsStressfulFight(int minHealth, int minTimeToDeath, int minAttackers, int maxAttackRange)
        {
            if (!Unit.ValidUnit(StyxWoW.Me.CurrentTarget))
                return false;

            int mobCount = Unit.UnitsInCombatWithUsOrOurStuff(maxAttackRange).Count();
            if (mobCount > 0)
            {
                if (mobCount >= minAttackers)
                    return true;

                if (StyxWoW.Me.HealthPercent <= minHealth)
                {
                    if (mobCount > 1)
                        return true;
                    if (StyxWoW.Me.CurrentTarget.TimeToDeath(-1) > minTimeToDeath)
                        return true;
                    if (StyxWoW.Me.CurrentTarget.IsPlayer)
                        return true;
                    if (StyxWoW.Me.CurrentTarget.MaxHealth > (StyxWoW.Me.MaxHealth * 2) && StyxWoW.Me.CurrentTarget.CurrentHealth > StyxWoW.Me.CurrentHealth)
                        return true;
                    if (StyxWoW.Me.HealthPercent < minHealth / 2)
                        return true;
                }
            }

            return false;
        }


        public static WoWPlayer GetPlayerParent(WoWUnit unit)
        {
            // If it is a pet/minion/totem, lets find the root of ownership chain
            WoWUnit pOwner = unit;
            while (true)
            {
                if (pOwner.OwnedByUnit != null)
                    pOwner = pOwner.OwnedByRoot;
                else if (pOwner.CreatedByUnit != null)
                    pOwner = pOwner.CreatedByUnit;
                else if (pOwner.SummonedByUnit != null)
                    pOwner = pOwner.SummonedByUnit;
                else
                    break;
            }

            if (unit != pOwner && pOwner.IsPlayer)
                return pOwner as WoWPlayer;

            return null;
        }

        /// <summary>
        ///   Gets unfriendly units within *distance* yards of CurrentTarget.
        /// </summary>
        /// <param name="distance"> The distance to check from CurrentTarget</param>
        /// <returns>IEnumerable of WoWUnit in range including CurrentTarget</returns>
        public static IEnumerable<WoWUnit> UnfriendlyUnitsNearTarget(float distance)
        {
            return UnfriendlyUnitsNearTarget(StyxWoW.Me.CurrentTarget, distance);
        }

        /// <summary>
        /// Gets unfriendly units within *distance* yards of *unit*
        /// </summary>
        /// <param name="unit">WoWUnit to find targets in range</param>
        /// <param name="distance">range within WoWUnit of other units</param>
        /// <returns>IEnumerable of WoWUnit in range including *unit*</returns>
        public static IEnumerable<WoWUnit> UnfriendlyUnitsNearTarget(WoWUnit unit, float distance)
        {
            if (unit == null)
                return new List<WoWUnit>();

            var distFromTargetSqr = distance * distance;
            int distFromMe = 40 + (int) distance;

            var curTarLocation = unit.Location;
            return Unit.UnfriendlyUnits(distFromMe).Where(p => p.Location.DistanceSqr(curTarLocation) <= distFromTargetSqr).ToList();
        }

        /// <summary>
        ///  Checks the aura by the name on specified unit.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <returns></returns>
        public static bool HasAura(this WoWUnit unit, string aura)
        {
            return HasAura(unit, aura, 0);
        }

        public static bool HasAura(this WoWUnit unit, int spellId)
        {
            return HasAura(unit, spellId, 0);
        }

        /// <summary>
        ///  Checks the aura count by the name on specified unit.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <param name="stacks"> The stack count of the aura to return true. </param>
        /// <returns></returns>
        public static bool HasAura(this WoWUnit unit, string aura, int stacks)
        {
            return HasAura(unit, aura, stacks, null);
        }


        public static bool HasAura(this WoWUnit unit, int spellId, int stacks)
        {
            return HasAura(unit, spellId, stacks, null);
        }


        public static bool HasAllMyAuras(this WoWUnit unit, params string[] auras)
        {
            return !auras.Any( a => !unit.HasMyAura(a));
        }

        /// <summary>
        ///  Check the aura count thats created by yourself by the name on specified unit
        /// </summary>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <returns></returns>
        public static bool HasMyAura(this WoWUnit unit, string aura)
        {
            return HasMyAura(unit, aura, 0);
        }

        /// <summary>
        ///  Check the aura count thats created by yourself by the name on specified unit
        /// </summary>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="stacks"> The stack count of the aura to return true. </param>
        /// <returns></returns>
        public static bool HasMyAura(this WoWUnit unit, string aura, int stacks)
        {
            return HasAura(unit, aura, stacks, StyxWoW.Me);
        }

        private static bool HasAura(this WoWUnit unit, string aura, int stacks, WoWUnit creator)
        {
            if (unit == null)
                return false;
            return unit.GetAllAuras().Any(a => a.Name == aura && a.StackCount >= stacks && (creator == null || a.CreatorGuid == creator.Guid));
        }

        /// <summary>
        ///  Check the aura count thats created by yourself by the name on specified unit
        /// </summary>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <returns></returns>
        public static bool HasMyAura(this WoWUnit unit, int id)
        {
            return HasMyAura(unit, id, 0);
        }

        /// <summary>
        ///  Check the aura count thats created by yourself by the name on specified unit
        /// </summary>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="stacks"> The stack count of the aura to return true. </param>
        /// <returns></returns>
        public static bool HasMyAura(this WoWUnit unit, int id, int stacks)
        {
            return HasAura(unit, id, stacks, StyxWoW.Me);
        }

        private static bool HasAura(this WoWUnit unit, int id, int stacks, WoWUnit creator)
        {
            return unit.GetAllAuras().Any(a => a.SpellId == id && a.StackCount >= stacks && (creator == null || a.CreatorGuid == creator.Guid));
        }


		public static bool HasMyOrMyStuffsAura(this WoWUnit unit, string name)
		{
			return HasMyOrMyStuffsAura(unit, name, 0);
		}

		public static bool HasMyOrMyStuffsAura(this WoWUnit unit, int id)
		{
			var spell = WoWSpell.FromId(id);

			return spell != null && HasMyOrMyStuffsAura(unit, spell.Name, 0);
		}

		public static bool HasMyOrMyStuffsAura(this WoWUnit unit, string name, int stacks)
		{
			return unit.GetAllAuras().Any(a =>
			{
				if (a.Name != name)
					return false;

				if (a.StackCount < stacks)
					return false;

				if (!a.CreatorGuid.IsValid)
					return false;

				var creator = ObjectManager.GetObjectByGuid<WoWUnit>(a.CreatorGuid);

				if (creator == null)
					return false;

				if (creator.IsMe)
					return true;
				
				var ownedBy = creator.OwnedByRoot;

				if (ownedBy != null && ownedBy.IsMe)
				{
					return true;
				}

				return false;
			});
		}

        /// <summary>
        ///  Checks for the auras on a specified unit. Returns true if the unit has any aura in the auraNames list.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="auraNames"> Aura names to be checked. </param>
        /// <returns></returns>
        public static bool HasAnyAura(this WoWUnit unit, params string[] auraNames)
        {
            var auras = unit.GetAllAuras();
            var hashes = new HashSet<string>(auraNames);
            return auras.Any(a => hashes.Contains(a.Name));
        }


        public static bool HasAnyAura(this WoWUnit unit, params int[] ids)
        {
            var auras = unit.GetAllAuras();
            var hashes = new HashSet<int>(ids);
            return auras.Any(a => hashes.Contains(a.SpellId));
        }


        /// <summary>
        ///  Checks for my auras on a specified unit. Returns true if the unit has any aura in the auraNames list applied by player.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="auraNames"> Aura names to be checked. </param>
        /// <returns></returns>
        public static bool HasAnyOfMyAuras(this WoWUnit unit, params string[] auraNames)
        {
            var auras = unit.GetAllAuras();
            var hashes = new HashSet<string>(auraNames);
            return auras.Any(a => a.CreatorGuid == StyxWoW.Me.Guid && hashes.Contains(a.Name));
        }


        /// <summary>
        /// aura considered expired if spell of same name as aura is known and aura not present or has less than specified time remaining
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="aura">name of aura with spell of same name that applies</param>
        /// <returns>true if spell known and aura missing or less than 'secs' time left, otherwise false</returns>
        public static bool HasAuraExpired(this WoWUnit u, string aura, int secs = 3, bool myAura = true)
        {
            return u.HasAuraExpired(aura, aura, secs, myAura);
        }


        /// <summary>
        /// aura considered expired if spell of same name as aura is known and aura not present or has less than specified time remaining
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="aura">name of aura with spell of same name that applies</param>
        /// <returns>true if spell known and aura missing or less than 'secs' time left, otherwise false</returns>
        public static bool HasAuraExpired(this WoWUnit u, string aura, TimeSpan tm, bool myAura = true)
        {
            return u.HasAuraExpired(aura, aura, tm, myAura);
        }


        /// <summary>
        /// aura considered expired if spell is known and aura not present or has less than specified time remaining
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="spell">spell that applies aura</param>
        /// <param name="aura">aura</param>
        /// <returns>true if spell known and aura missing or less than 'secs' time left, otherwise false</returns>
        public static bool HasAuraExpired(this WoWUnit u, string spell, string aura, int secs = 3, bool myAura = true)
        {
            return HasAuraExpired( u, spell, aura, TimeSpan.FromSeconds(3), myAura);
        }


        /// <summary>
        /// aura considered expired if spell is known and aura not present or has less than specified time remaining
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="spell">spell that applies aura</param>
        /// <param name="aura">aura</param>
        /// <returns>true if spell known and aura missing or less than 'secs' time left, otherwise false</returns>
        public static bool HasAuraExpired(this WoWUnit u, string spell, string auraName, TimeSpan tm, bool myAura = true)
        {
            // need to compare millisecs even though seconds are provided.  otherwise see it as expired 999 ms early because
            // .. of loss of precision
            if (!SpellManager.HasSpell(spell))
                return false;

            WoWAura wantedAura = u.GetAllAuras().FirstOrDefault(a => a != null && a.Name.Equals(auraName, StringComparison.OrdinalIgnoreCase) && a.TimeLeft > TimeSpan.Zero && (!myAura || a.CreatorGuid == StyxWoW.Me.Guid));

            if (wantedAura == null)
                return true;

            // be aware: test previously was <= and vague recollection that was needed 
            // .. but no comment and need a way to consider passive ones found with timeleft of 0 as not expired if
            // .. if we pass 0 in as the timespan
            if (wantedAura.TimeLeft < tm)
                return true;

            return false;
        }


        /// <summary>
        /// aura considered expired if spell is known and aura not present or has less than specified time remaining
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="spell">spell that applies aura</param>
        /// <param name="aura">aura</param>
        /// <param name="tm">time to </param>
        /// <param name="myAura">true: restrict to only your characters auras</param>
        /// <returns>true if spell known and aura missing or less than 'secs' time left, otherwise false</returns>
        public static bool HasAuraExpired(this WoWUnit u, string spell, string auraName, int stackCount, TimeSpan tm, bool myAura = true)
        {
            // need to compare millisecs even though seconds are provided.  otherwise see it as expired 999 ms early because
            // .. of loss of precision
            if (!SpellManager.HasSpell(spell))
                return false;

            WoWAura wantedAura = u.GetAllAuras()
                .Where(a => a != null && string.Compare(a.Name, auraName, false) == 0 && a.TimeLeft > TimeSpan.Zero && (!myAura || a.CreatorGuid == StyxWoW.Me.Guid))
                .FirstOrDefault();

            if (wantedAura == null)
                return true;

            if (wantedAura.TimeLeft < tm)
                return true;

            if (Math.Max(1, wantedAura.StackCount) < stackCount)
                return true;

            return false;

        }


        /// <summary>
        /// aura considered expired if aura not present or less than specified time remaining.  
        /// differs from HasAuraExpired since it assumes you have learned the spell which applies it already
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="aura">aura</param>
        /// <returns>true aura missing or less than 'secs' time left, otherwise false</returns>
        public static bool HasKnownAuraExpired(this WoWUnit u, string aura, int secs = 3, bool myAura = true)
        {
            return u.GetAuraTimeLeft(aura, myAura).TotalSeconds < (double)secs;
        }

        public static bool HasKnownAuraExpired(this WoWUnit u, string aura, TimeSpan exp, bool myAura = true)
        {
            return u.GetAuraTimeLeft(aura, myAura) < exp;
        }


        /// <summary>
        ///  Checks for the auras on a specified unit. Returns true if the unit has any aura with any of the mechanics in the mechanics list.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="mechanics"> Mechanics to be checked. </param>
        /// <returns></returns>
        public static bool HasAuraWithMechanic(this WoWUnit unit, params WoWSpellMechanic[] mechanics)
        {
            if (unit == null)
                return false;

            var auras = unit.GetAllAuras();
            return auras.Any(a => mechanics.Contains(a.Spell.Mechanic));
        }

        /// <summary>
        ///  Checks for the auras on a specified unit. Returns true if the unit has any aura with any of the mechanics in the mechanics list.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="mechanics"> Mechanics to be checked. </param>
        /// <returns></returns>
        public static WoWAura GetAuraWithMechanic(this WoWUnit unit, params WoWSpellMechanic[] mechanics)
        {
            if (unit == null)
                return null;

            var auras = unit.GetAllAuras();
            WoWAura aura = auras.FirstOrDefault(a => mechanics.Contains(a.Spell.Mechanic));
            return aura;
        }

        /// <summary>
        ///  Checks for the auras on a specified unit. Returns true if the unit has any aura with any apply aura type in the list.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="mechanics"> Mechanics to be checked. </param>
        /// <returns></returns>
        public static bool HasAuraWithMechanic(this WoWUnit unit, params WoWApplyAuraType[] applyType)
        {
            if (unit == null)
                return false;

            var auras = unit.GetAllAuras();
            return auras.Any(a => a.Spell.SpellEffects.Any(se => applyType.Contains(se.AuraType)));
        }

        /// <summary>
        ///  Checks for the auras on a specified unit. Returns true if the unit has any aura with any apply aura type in the list.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="mechanics"> Mechanics to be checked. </param>
        /// <returns></returns>
        public static WoWAura GetAuraWithMechanic(this WoWUnit unit, params WoWApplyAuraType[] applyType)
        {
            if (unit == null)
                return null;

            var auras = unit.GetAllAuras();
            WoWAura aura = auras.FirstOrDefault(a => a.Spell.SpellEffects.Any(se => applyType.Contains(se.AuraType)));
            return aura;    // extra step for assign to local and return is to simplify debug and conditional brkpts
        }

        /// <summary>
        ///  Returns the timeleft of an aura by TimeSpan. Return TimeSpan.Zero if the aura doesn't exist.
        /// </summary>
        /// <param name="auraName"> The name of the aura in English. </param>
        /// <param name="onUnit"> The unit to check the aura for. </param>
        /// <param name="fromMyAura"> Check for only self or all buffs</param>
        /// <returns></returns>
        public static TimeSpan GetAuraTimeLeft(this WoWUnit onUnit, string auraName, bool fromMyAura = true)
        {
            if (onUnit == null)
                return TimeSpan.Zero;

            WoWAura wantedAura =
                onUnit.GetAllAuras().Where(a => a != null && a.Name == auraName && a.TimeLeft > TimeSpan.Zero && (!fromMyAura || a.CreatorGuid == StyxWoW.Me.Guid)).FirstOrDefault();

            return wantedAura != null ? wantedAura.TimeLeft : TimeSpan.Zero;
        }

        public static TimeSpan GetAuraStacksAndTimeLeft(this WoWUnit onUnit, string auraName, out uint stackCount, bool fromMyAura = true)
        {
            if (onUnit == null)
            {
                stackCount = 0;
                return TimeSpan.Zero;
            }

            WoWAura wantedAura =
                onUnit.GetAllAuras().Where(a => a != null && a.Name == auraName && a.TimeLeft > TimeSpan.Zero && (!fromMyAura || a.CreatorGuid == StyxWoW.Me.Guid)).FirstOrDefault();

            if (wantedAura == null)
            {
                stackCount = 0;
                return TimeSpan.Zero;
            }

            stackCount = Math.Max( 1, wantedAura.StackCount);
            return wantedAura.TimeLeft;
        }

        public static TimeSpan GetAuraTimeLeft(this WoWUnit onUnit, int auraID, bool fromMyAura = true)
        {
            if (onUnit == null)
                return TimeSpan.Zero;

            WoWAura wantedAura = onUnit.GetAllAuras()
                .Where(a => a.SpellId == auraID && a.TimeLeft > TimeSpan.Zero && (!fromMyAura || a.CreatorGuid == StyxWoW.Me.Guid)).FirstOrDefault();

            return wantedAura != null ? wantedAura.TimeLeft : TimeSpan.Zero;
        }

        public static uint GetAuraStacks(this WoWUnit onUnit, string auraName, bool fromMyAura = true)
        {
            if (onUnit == null)
                return 0;

            WoWAura wantedAura =
                onUnit.GetAllAuras().Where(a => a.Name == auraName && a.TimeLeft > TimeSpan.Zero && (!fromMyAura || a.CreatorGuid == StyxWoW.Me.Guid)).FirstOrDefault();

            if (wantedAura == null)
                return 0;

            return wantedAura.StackCount == 0 ? 1 : wantedAura.StackCount;
        }

        public static uint GetAuraStacks(this WoWUnit onUnit, int spellId, bool fromMyAura = true)
        {
            if (onUnit == null)
                return 0;

            WoWAura wantedAura =
                onUnit.GetAllAuras().Where(a => a.SpellId == spellId && a.TimeLeft > TimeSpan.Zero && (!fromMyAura || a.CreatorGuid == StyxWoW.Me.Guid)).FirstOrDefault();

            if (wantedAura == null)
                return 0;

            return wantedAura.StackCount == 0 ? 1 : wantedAura.StackCount;
        }

        public static void CancelAura(this WoWUnit unit, string aura)
        {
            WoWAura a = unit.GetAuraByName( aura );
            if (a != null && a.Cancellable)
            {
                a.TryCancelAura();
                Logger.Write( LogColor.Cancel, "/cancelaura: {0} #{1}", a.Name, a.SpellId);
            }
        }

        public static bool HasAnyShapeshift(this WoWUnit unit, params ShapeshiftForm[] forms)
        {
            ShapeshiftForm currentForm = StyxWoW.Me.Shapeshift;
            return forms.Any( f => f == currentForm);
        }


        public static bool HasShapeshiftAura(this WoWUnit unit, string auraName)
        {
            WoWAura aura = unit.GetAllAuras()
                .Where(a => a.ApplyAuraType == WoWApplyAuraType.ModShapeshift && a.Name == auraName)
                .FirstOrDefault();
            return aura != null;
        }

        public static bool IsNeutral(this WoWUnit unit)
        {
            return unit.GetReactionTowards(StyxWoW.Me) == WoWUnitReaction.Neutral;
        }

        /// <summary>
        /// Returns a list of resurrectable players in a 40 yard radius
        /// </summary>
        public static List<WoWPlayer> ResurrectablePlayers
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWPlayer>(false,false).Where(
                    p => !p.IsMe && p.IsDead && p.IsFriendly && p.IsInMyPartyOrRaid() && p.IsPlayer
                         && p.DistanceSqr < 40 * 40 && !Blacklist.Contains(p.Guid, BlacklistFlags.Combat)).ToList();
            }
        }

        public static bool IsCrowdControlled(this WoWUnit unit)
        {
            Dictionary<string, WoWAura>.ValueCollection auras = unit.Auras.Values;

#if AURAS_HAVE_MECHANICS
            return auras.Any(
                a => a.Spell.Mechanic == WoWSpellMechanic.Banished ||
                     a.Spell.Mechanic == WoWSpellMechanic.Charmed ||
                     a.Spell.Mechanic == WoWSpellMechanic.Horrified ||
                     a.Spell.Mechanic == WoWSpellMechanic.Incapacitated ||
                     a.Spell.Mechanic == WoWSpellMechanic.Polymorphed ||
                     a.Spell.Mechanic == WoWSpellMechanic.Sapped ||
                     a.Spell.Mechanic == WoWSpellMechanic.Shackled ||
                     a.Spell.Mechanic == WoWSpellMechanic.Asleep ||
                     a.Spell.Mechanic == WoWSpellMechanic.Frozen ||
                     a.Spell.Mechanic == WoWSpellMechanic.Invulnerable ||
                     a.Spell.Mechanic == WoWSpellMechanic.Invulnerable2 ||
                     a.Spell.Mechanic == WoWSpellMechanic.Turned ||

                     // Really want to ignore hexed mobs.
                     a.Spell.Name == "Hex"

                     );
#else
            return unit.Stunned
                || unit.Rooted
                || unit.Fleeing 
                || unit.HasAuraWithEffect(
                        WoWApplyAuraType.ModConfuse, 
                        WoWApplyAuraType.ModCharm, 
                        WoWApplyAuraType.ModFear, 
                        // WoWApplyAuraType.ModDecreaseSpeed, 
                        WoWApplyAuraType.ModPacify, 
                        WoWApplyAuraType.ModPacifySilence, 
                        WoWApplyAuraType.ModPossess, 
                        WoWApplyAuraType.ModRoot, 
                        WoWApplyAuraType.ModStun 
                        );
#endif
        }

        // this one optimized for single applytype lookup
        public static bool HasAuraWithEffect(this WoWUnit unit, WoWApplyAuraType applyType)
        {
            return unit.Auras.Values.Any(a => a.Spell != null && a.Spell.SpellEffects.Any(se => applyType == se.AuraType));
        }

        public static bool HasAuraWithEffect(this WoWUnit unit, params WoWApplyAuraType[] applyType)
        {
            var hashes = new HashSet<WoWApplyAuraType>(applyType);
            return unit.Auras.Values.Any( a => a.Spell != null && a.Spell.SpellEffects.Any(se => hashes.Contains(se.AuraType)));
        }

        /// <summary>
        /// IsBoss() checks usually appear in a sequence testing same target repeatedly.  
        /// Cache the values for a fast return in that circumstanc
        /// </summary>
        private static WoWGuid _lastIsBossGuid;
        private static bool _lastIsBossResult = false;
   
        /// <summary>
        /// checks if unit is in list of bosses. cache of prior check kept for faster return in 
        /// instance behaviors which repeatedly test this as part of criteria for different
        /// cooldown casts
        /// </summary>
        /// <param name="unit">unit to test if they are a known boss</param>
        /// <returns>true: if boss</returns>
        public static bool IsBoss(this WoWUnit unit)
        {
            WoWGuid guid = unit == null ? WoWGuid.Empty : unit.Guid;
            if ( guid == _lastIsBossGuid )
                return _lastIsBossResult;

            _lastIsBossGuid = guid;
#if SINGULAR_BOSS_DETECT
            _lastIsBossGuid = guid;
            _lastIsBossResult = unit != null && (Lists.BossList.CurrentMapBosses.Contains(unit.Name) || Lists.BossList.BossIds.Contains(unit.Entry));
#else
            _lastIsBossResult = unit.IsBoss;
#endif
            if (!_lastIsBossResult)
                _lastIsBossResult = IsTrainingDummy(unit);
            return _lastIsBossResult;
        }

        private const int BannerOfTheAlliance = 61573;
        private const int BannerOfTheHorde = 61574;

        public static bool IsTrainingDummy(this WoWUnit unit)
        {
            // return Lists.BossList.TrainingDummies.Contains(unit.Entry);
            
            int bannerId = StyxWoW.Me.IsHorde ? BannerOfTheAlliance : BannerOfTheHorde;
            return unit != null && unit.Level > 1 
                && ((unit.CurrentHealth == 1 && unit.MaxHealth < unit.Level) || unit.HasAura(bannerId) || unit.Name.Contains("Training Dummy"));
        }

        /// <summary>
        /// checks if unit is targeting you, your minions, a group member, or group pets
        /// </summary>
        /// <param name="u">unit</param>
        /// <returns>true if targeting your guys, false if not</returns>
        public static bool IsTargetingMyStuff(this WoWUnit u)
        {
            return u.IsTargetingMeOrPet 
                || u.IsTargetingAnyMinion 
                || (u.GotTarget() && u.CurrentTarget.IsCompanion());
        }

        public static bool IsCompanion(this WoWUnit u)
        {
            return u.CreatedByUnitGuid == StyxWoW.Me.Guid;
        }

        /// <summary>
        /// checks if unit is targeting you, your minions, a group member, or group pets
        /// </summary>
        /// <param name="u">unit</param>
        /// <returns>true if targeting your guys, false if not</returns>
        public static bool IsTargetingUs(this WoWUnit u)
        {
            return u.IsTargetingMyStuff() || Unit.GroupMemberInfos.Any(m => m.Guid == u.CurrentTargetGuid);
        }


        public static bool IsInMyPartyOrRaid(this WoWUnit u)
        {
            return StyxWoW.Me.GroupInfo.RaidMemberGuids.Any(m => m == u.Guid);
        }

        public static bool IsSensitiveDamage(this WoWUnit u,  int range = 0)
        {
            if (u.Guid == StyxWoW.Me.CurrentTargetGuid)
                return false;

            if (!u.Combat && !u.IsPlayer && u.IsNeutral())
                return true;

            if (range > 0 && u.SpellDistance() > range)
                return false;

            return u.IsCrowdControlled();
        }

        public static bool IsShredBoss(this WoWUnit unit)
        {
            return Lists.BossList.CanShredBoss.Contains(unit.Entry);
        }

        public static bool HasAuraWithEffect(this WoWUnit unit, WoWApplyAuraType auraType, int miscValue, int basePointsMin, int basePointsMax)
        {
            var auras = unit.GetAllAuras();
            return (from a in auras
                    where a.Spell != null
                    let spell = a.Spell
                    from e in spell.GetSpellEffects()
                    // First check: Ensure the effect is... well... valid
                    where e != null &&
                        // Ensure the aura type is correct.
                    e.AuraType == auraType &&
                        // Check for a misc value. (Resistance types, etc)
                    (miscValue == -1 || e.MiscValueA == miscValue) &&
                        // Check for the base points value. (Usually %s for most debuffs)
                    e.BasePoints >= basePointsMin && e.BasePoints <= basePointsMax
                    select a).Any();
        }
        public static bool HasSunders(this WoWUnit unit)
        {
            // Remember; this is negative values [debuff]. So min is -12, max is -4. Duh.
            return unit.HasAuraWithEffect(WoWApplyAuraType.ModResistancePct, 1, -12, -4);

            //var auras = unit.GetAllAuras();
            //var tmp = (from a in auras
            //           where a.Spell != null
            //           from e in a.Spell.SpellEffects
            //           // Sunder, Faerie Fire, and another have -4% armor per-stack.
            //           // Expose Armor, and others, have a flat -12%
            //           // Ensure we check MiscValueA for 1, as thats the resistance index for physical (aka; armor)
            //           where
            //               e != null && e.AuraType == WoWApplyAuraType.ModResistancePct && e.MiscValueA == 1 &&
            //               (e.BasePoints == -4 || e.BasePoints == -12)
            //           select a).Any();

            //return tmp;
        }
        public static bool HasDemoralizing(this WoWUnit unit)
        {
            // don't try if the unit is out of range.
            if (unit.DistanceSqr > 25)
                return true;

            // Plain and simple, any effect with -damage is good. Ensure at least -1. Since 0 may be a buggy spell entry or something.
            var tmp = unit.HasAuraWithEffect(WoWApplyAuraType.ModDamagePercentDone, -1, int.MinValue, -1);

            return tmp;

            //var auras = unit.GetAllAuras();
            //var tmp = (from a in auras
            //           where a.Spell != null && a.Spell.SpellEffect1 != null
            //           let effect = a.Spell.SpellEffect1
            //           // Basically, all spells are -10% damage done that are demoralizing shout/roar/etc.
            //           // The aura type is damage % done. Just chekc for anything < 0. (There may be some I'm forgetting that aren't -10%, but stacks of like 2% or something
            //           where effect.AuraType == WoWApplyAuraType.ModDamagePercentDone && effect.BasePoints < 0
            //           select a).Any();
            //if (!tmp)
            //    Logger.Write(unit.Name + " does not have demoralizing!");
            //return tmp;
        }

        public static bool HasBleedDebuff(this WoWUnit unit)
        {
            return unit.HasAuraWithEffect(WoWApplyAuraType.ModMechanicDamageTakenPercent, 15, 0, int.MaxValue);
        }

        /// <summary>A temporary fix until the next build of HB.</summary>
        static SpellEffect[] GetSpellEffects(this WoWSpell spell)
        {
            SpellEffect[] effects = new SpellEffect[3];
            effects[0] = spell.GetSpellEffect(0);
            effects[1] = spell.GetSpellEffect(1);
            effects[2] = spell.GetSpellEffect(2);
            return effects;
        }

        public static bool IsInGroup(this LocalPlayer me)
        {
            return me.GroupInfo.IsInParty || me.GroupInfo.IsInRaid;
        }

        //private static string _lastGetPredictedError;
        public static float GetVerifiedGetPredictedHealthPercent(this WoWUnit unit, bool includeMyHeals = false)
        {
            float hbhp = unit.GetPredictedHealthPercent(includeMyHeals);
#if false
            Styx.Patchables.IncomingHeal[] heals = unit.IncomingHealsArray().ToArray();
            uint myhealth = unit.CurrentHealth;

            uint myincoming = TotalIncomingHeals(heals, includeMyHeals);
            float mypredict = (100f * (myhealth + myincoming)) / unit.MaxHealth;

            if (Math.Abs(mypredict - hbhp) < 2f)
                return hbhp;

            string msg = string.Format("Predict Error=WoWUnit.GetPredictedHealthPercent({0}) returned {1:F1}% for {2} with MyPredict={3:F1}% and HealthPercent={4:F1}%", includeMyHeals, hbhp, unit.SafeName(), mypredict, myhealth);
            if (msg != _lastGetPredictedError)
            {
                _lastGetPredictedError = msg;
                Logger.WriteDebug(System.Drawing.Color.Pink, msg);
            }

            hbhp = Math.Min(hbhp, mypredict);
#endif
            return hbhp;
        }

        private static uint TotalIncomingHeals( Styx.Patchables.IncomingHeal[] heals, bool includeMyHeals = false)
        {
            uint aggcheck = heals
                .Where(heal => includeMyHeals || heal.OwnerGuid != StyxWoW.Me.Guid)
                .Aggregate(0u, (current, heal) => current + heal.HealAmount);
#if false
            uint myincoming = 0;
            foreach (var heal in heals)
            {
                if (includeMyHeals || heal.OwnerGuid != StyxWoW.Me.Guid)
                    myincoming += heal.HealAmount;
            }

            uint sumcheck = (uint) heals
                .Where(heal => includeMyHeals || heal.OwnerGuid != StyxWoW.Me.Guid)
                .Sum( heal => (long) heal.HealAmount);                

            if ( myincoming != aggcheck || aggcheck != sumcheck)
            {
                Logger.WriteDiagnostic(Color.HotPink, "Accuracy Error= my={0}  agg={1}  sum={2}", myincoming, aggcheck, sumcheck);
            }
#endif
            return aggcheck;
        }


        public static IncomingHeal[] LocalIncomingHeals(this WoWUnit unit)
        {
            // Reversing note: CGUnit_C::GetPredictedHeals
            const int PredictedHealsCount = 0x1374;
            const int PredictedHealsArray = 0x1378;

            Debug.Assert(unit != null);
            uint health = unit.CurrentHealth;
            var incomingHealsCnt = StyxWoW.Memory.Read<int>(unit.BaseAddress + PredictedHealsCount);
            if (incomingHealsCnt == 0)
                return new IncomingHeal[0];

            var incomingHealsListPtr = StyxWoW.Memory.Read<IntPtr>(unit.BaseAddress + PredictedHealsArray);

            var heals = StyxWoW.Memory.ReadArray<IncomingHeal>(incomingHealsListPtr, incomingHealsCnt);
            return heals;
        }


        public static uint LocalGetPredictedHealthDebug(this WoWUnit unit, bool includeMyHeals = false)
        {
            // Reversing note: CGUnit_C::GetPredictedHeals
            const int PredictedHealsCount = 0x1494;
            const int PredictedHealsArray = 0x1498;
            uint maxHealth = unit.MaxHealth;

            Debug.Assert(unit != null);
            uint health = unit.CurrentHealth;
            var incomingHealsCnt = StyxWoW.Memory.Read<int>(unit.BaseAddress + PredictedHealsCount);
            if (incomingHealsCnt == 0)
            {
                Logger.WriteDiagnostic( "  0 incoming heals");
                return health;
            }

            var incomingHealsListPtr = StyxWoW.Memory.Read<IntPtr>(unit.BaseAddress + PredictedHealsArray);
            var heals = StyxWoW.Memory.ReadArray<IncomingHeal>(incomingHealsListPtr, incomingHealsCnt);

            StringBuilder sb = new StringBuilder();
            sb.Append( "\n");

            uint inchealth = 0;
            foreach ( var heal in heals)
            {
                if (includeMyHeals && heal.OwnerGuid == StyxWoW.Me.Guid)
                    continue;
                WoWUnit owner = ObjectManager.GetObjectByGuid<WoWUnit>(heal.OwnerGuid);
                WoWSpell spell = WoWSpell.FromId(heal.spellId);
                uint HealPct = (heal.HealAmount * 100) / maxHealth;

                sb.Append(
                    string.Format("  {0} {1}% {2} {3} {4} {5}\r\n", 
                        heal.IsHealOverTime.ToYN(),
                        HealPct.ToString().PadLeft(3),
                        heal.HealAmount.ToString().PadLeft(6),
                        spell.Name.PadLeft(15).Substring(0, 15),
                        spell.Id,
                        owner.Name
                        )
                    );

                inchealth += heal.HealAmount;
            }

            sb.Append( "   Total Incoming Heals = ");
            sb.Append( (inchealth * 100 / maxHealth).ToString().PadLeft(3));
            sb.Append( "% ");
            sb.Append( inchealth.ToString().PadLeft(6));
            sb.Append("  Predicted Health Pct = ");
            sb.Append( (((float) health + inchealth) * 100 / unit.MaxHealth).ToString("F1"));
            sb.Append( "%\r\n");

            Logger.WriteDiagnostic( sb.ToString());

            return health + inchealth;
        }

        public static float LocalGetPredictedHealthPercentDebug(this WoWUnit unit, bool includeMyHeals = false)
        {
             return (float)unit.LocalGetPredictedHealth(includeMyHeals) * 100 / unit.MaxHealth;
        }

        public static uint LocalGetPredictedHealth(this WoWUnit unit, bool includeMyHeals = false)
        {
            // Reversing note: CGUnit_C::GetPredictedHeals
            const int PredictedHealsCount = 0x1374;
            const int PredictedHealsArray = 0x1378;

            Debug.Assert(unit != null);
            uint health = unit.CurrentHealth;
            var incomingHealsCnt = StyxWoW.Memory.Read<int>(unit.BaseAddress + PredictedHealsCount);
            if (incomingHealsCnt == 0)
                return health;

            var incomingHealsListPtr = StyxWoW.Memory.Read<IntPtr>(unit.BaseAddress + PredictedHealsArray);

            var heals = StyxWoW.Memory.ReadArray<IncomingHeal>(incomingHealsListPtr, incomingHealsCnt);
            return heals.Where(heal => includeMyHeals || heal.OwnerGuid != StyxWoW.Me.Guid)
                .Aggregate(health, (current, heal) => current + heal.HealAmount);
        }

        public static float LocalGetPredictedHealthPercent(this WoWUnit unit, bool includeMyHeals = false)
        {
            return (float)unit.LocalGetPredictedHealth(includeMyHeals) * 100 / unit.MaxHealth;
        }

		[StructLayout(LayoutKind.Sequential)]
		internal struct IncomingHeal
		{
			public WoWGuid OwnerGuid;
			public int spellId;
			private int _dword_C;
			public uint HealAmount;
			private byte _isHealOverTime; // includes chaneled spells.
			private byte _byte_15; // unknown value
			private byte _byte_16; // unused
			private byte _byte_17; // unused

			public bool IsHealOverTime { get { return _isHealOverTime == 1; } }
		}

        /// <summary>
        /// unit is located behind or along the side of target
        /// </summary>
        /// <param name="unit">units position to check</param>
        /// <param name="target">target to determine position against</param>
        /// <returns>true if on side or behind, otherwise false</returns>
        public static bool IsBehindOrSide( this WoWUnit unit, WoWUnit target)
        {
            return unit != null && target != null && !target.IsSafelyFacing(unit, 160);
        }

        /// <summary>
        /// basic check if mob is running away from you.  true for any mob moving
        /// that has their back to you.  be aware will return true for one you
        /// approach from the rear even if they are backing up towards you 
        /// </summary>
        /// <param name="unit">unit</param>
        /// <returns>true: mob moving and you are safely behind it</returns>
        public static bool IsMovingAway(this WoWUnit unit)
        {
            return unit.IsMoving && !unit.IsWithinMeleeRange && StyxWoW.Me.IsSafelyBehind(unit);
        }

        public static bool IsMovingTowards(this WoWUnit unit)
        {
            if ( !unit.IsMoving || unit.IsWithinMeleeRange )
                return false;

            float checkDist = (float) unit.Distance - Spell.MeleeDistance(unit);
            if (checkDist < 0)
                return false;

            // WoWPoint loc = WoWPoint.RayCast(StyxWoW.Me.CurrentTarget.Location, StyxWoW.Me.CurrentTarget.RenderFacing, checkDist);
            // return StyxWoW.Me.IsFacing( loc );

            return unit.IsSafelyFacing(StyxWoW.Me, 10);
        }


        private static bool lastMovingAwayAnswer = false;
        private static WoWGuid guidLastMovingAwayCheck;
        private static double distLastMovingAwayCheck = 0f;
        private static readonly WaitTimer MovingAwayTimer = new WaitTimer(TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// tracks if current target has moved away from.  works for blink and other quick
        /// movements which have nothing to do with direction enemy is facing
        /// </summary>
        public static bool CurrentTargetIsMovingAwayFromMe
        {
            get
            {
                if (!StyxWoW.Me.CurrentTargetGuid.IsValid || guidLastMovingAwayCheck != StyxWoW.Me.CurrentTargetGuid)
                {
                    lastMovingAwayAnswer = false;
                    if (!StyxWoW.Me.GotTarget())
                        guidLastMovingAwayCheck = WoWGuid.Empty;
                    else
                    {
                        guidLastMovingAwayCheck = StyxWoW.Me.CurrentTargetGuid;
                        distLastMovingAwayCheck = StyxWoW.Me.CurrentTarget.Distance;
                        MovingAwayTimer.Reset();
                    }
                }
                else if ( MovingAwayTimer.IsFinished )
                {
                    double currentDistance = StyxWoW.Me.CurrentTarget.Distance;
                    double changeInDistance = currentDistance - distLastMovingAwayCheck;
                    lastMovingAwayAnswer = changeInDistance > 0;
                    distLastMovingAwayCheck = currentDistance;
                    MovingAwayTimer.Reset();
                }

                return lastMovingAwayAnswer ;
            }
        }

        public static IEnumerable<WoWUnit> MobsAttackingTank()
        {
            return Unit.NearbyUnfriendlyUnits.Where(u => Group.Tanks.Any( t => t.IsAlive && t.Guid == u.CurrentTargetGuid));
        }

        public static WoWUnit LowestHealthMobAttackingTank()
        {
            return MobsAttackingTank().OrderBy(u => u.HealthPercent).FirstOrDefault();
        }

        public static WoWUnit HighestHealthMobAttackingTank()
        {
            return MobsAttackingTank().OrderByDescending(u => u.HealthPercent).FirstOrDefault();
        }

        /// <summary>
        /// Calls the UnitCanAttack LUA to check if current target is attackable. This is
        /// necessary because the WoWUnit.Attackable property returns 'true' when targeting
        /// any enemy player including in Sanctuary, not PVP flagged, etc where a player
        /// is not attackable
        /// </summary>
        public static bool CanWeAttack(this WoWUnit unit, bool restoreFocus = true)
        {
            if (unit == null)
                return false;

            bool canAttack = false;

            if (unit.Guid == StyxWoW.Me.CurrentTargetGuid)
                canAttack = Lua.GetReturnVal<bool>("return UnitCanAttack(\"player\",\"target\")", 0);
            else
            {
                // do not perform test in PVP or Instance contexts
                if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                    return true;

                // skip test if not a player 
                if (!unit.IsPlayer)
                    return true;

                WoWUnit focusSave = StyxWoW.Me.FocusedUnit;
                StyxWoW.Me.SetFocus(unit);
                canAttack = Lua.GetReturnVal<bool>("return UnitCanAttack(\"player\",\"focus\")", 0);
                if (restoreFocus)
                {
                    if (focusSave == null || !focusSave.IsValid)
                        StyxWoW.Me.SetFocus(WoWGuid.Empty);
                    else
                        StyxWoW.Me.SetFocus(focusSave);
                }
            }

            return canAttack;
        }

        public static bool IsAvoidMob(this WoWUnit unit)
        {
            return ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.AvoidMobs != null && ProfileManager.CurrentProfile.AvoidMobs.Contains(unit.Entry);
        }


        public static bool IsFlyingOrUnreachableMob(this WoWUnit unit)
        {
            if (!StyxWoW.Me.GotTarget())
                return false;

            // return immediately if already in melee range
            if (StyxWoW.Me.CurrentTarget.IsWithinMeleeRange)
                return false;

            // ignore players above ground
            if (StyxWoW.Me.CurrentTarget.IsPlayer)
                return false;

            // check if target appears to be higher than melee range off ground
            float heightOffGround = StyxWoW.Me.CurrentTarget.HeightOffTheGround();
            float meleeDist = StyxWoW.Me.CurrentTarget.MeleeDistance();
            if (heightOffGround > meleeDist)
            {
                Logger.Write(LogColor.Hilite, "Ranged Attack: {0} {1:F3} yds above ground using Ranged attack since reach is {2:F3} yds....", StyxWoW.Me.CurrentTarget.SafeName(), heightOffGround, meleeDist);
                return true;
            }

            // additional check for off ground
            double heightCheck = StyxWoW.Me.CurrentTarget.MeleeDistance();
            if (StyxWoW.Me.CurrentTarget.Distance2DSqr < heightCheck * heightCheck && Math.Abs(StyxWoW.Me.Z - StyxWoW.Me.CurrentTarget.Z) >= heightCheck)
            {
                Logger.Write(LogColor.Hilite, "Ranged Attack: {0} appears to be off the ground! using Ranged attack....", StyxWoW.Me.CurrentTarget.SafeName());
                return true;
            }

            if ((DateTime.UtcNow - Singular.Utilities.EventHandlers.LastNoPathFailure).TotalSeconds < 3f)
            {
                Logger.Write( LogColor.Hilite, "Ranged Attack: No Path Available error just happened, so using Ranged attack ....", StyxWoW.Me.CurrentTarget.SafeName());
                return true;
            }

            WoWPoint dest = StyxWoW.Me.CurrentTarget.Location;
            if (!StyxWoW.Me.CurrentTarget.IsWithinMeleeRange && !Styx.Pathing.Navigator.CanNavigateFully(StyxWoW.Me.Location, dest))
            {
                Logger.Write(LogColor.Hilite, "Ranged Attack: {0} is not Fully Pathable! using ranged attack....", StyxWoW.Me.CurrentTarget.SafeName());
                return true;
            }

            return false;
        }
    }

    public enum CombatArea
    {
        Radius = 1,
        Facing
    }

    public class CombatScenario
    {
        
        /// <summary>
        /// spell distance from Me to check
        /// </summary>
        public int Range { get; set; }

        /// <summary>
        /// area to consider mobs for AOE attack
        /// </summary>
        public CombatArea Area { get; set; }

        /// <summary>
        /// spell distance from Me to check
        /// </summary>
        public float BaseGcd { get; set; }

        /// <summary>
        /// count of mobs attacking within range.  will be forced to
        /// 1 if world pvp is recent or avoidaoe is true
        /// </summary>
        public int MobCount { get; set; }

        /// <summary>
        /// count of mobs crowd controlled within range.
        /// </summary>
        public int CcCount { get; set; }

        /// <summary>
        /// count of mobs crowd controlled within range.
        /// </summary>
        public int PlayerCount { get; set; }

        /// <summary>
        /// flag indicating determined best approach is to suppress AOE abilities
        /// multi-target combat can ensure (typically through DoTs)
        /// </summary>
        public bool AvoidAOE { get; set; }

        /// <summary>
        /// flag indicating determined best approach is to suppress AOE abilities
        /// multi-target combat can ensure (typically through DoTs)
        /// </summary>
        public bool WorldPvpRecently { get; set; }


        /// <summary>
        /// list of Mobs within AOE range
        /// </summary>
        public List<WoWUnit> Mobs { get; set; }

        /// <summary>
        /// maximum number of milliseconds that a damage record should be retained.
        /// this is applicable only to tanks
        /// </summary>
        public float MaxAgeForDamage { get; set; }
        public long AllDamage { get; set; }
        public float RecentAgeForDamage { get; set; }
        public long RecentDamage { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float GcdTime { get; set; }

        private CombatScenario()
        {
        }

        public CombatScenario( int range, float basegcd, CombatArea area = CombatArea.Radius, float maxdmgtime = 0f)
        {
            Range = range;
            BaseGcd = basegcd;
            Area = area;
            MaxAgeForDamage = maxdmgtime;
            Mobs = new List<WoWUnit>();                
        }

        public void Update(WoWUnit origin )
        {
            GcdTime = BaseGcd * StyxWoW.Me.SpellHasteModifier;

            if (StyxWoW.Me.GotTarget())
                StyxWoW.Me.TimeToDeath();

            bool worldPvp = EventHandlers.TimeSinceAttackedByEnemyPlayer.TotalSeconds < 15;
            if (worldPvp )
            {
                WorldPvpRecently = worldPvp;
                AvoidAOE = true;
                CcCount = 0;
                PlayerCount = 1;
                Mobs = !Unit.ValidUnit(StyxWoW.Me.CurrentTarget)
                    ? new List<WoWUnit>()
                    : new List<WoWUnit>(new[] { StyxWoW.Me.CurrentTarget });
                MobCount = Mobs.Any() ? 1 : 0;
            }
            else
            {
                WorldPvpRecently = false;
                AvoidAOE = false;
                MobCount = 0;
                CcCount = 0;
                PlayerCount = 0;

                Mobs = Unit.UnfriendlyUnits(Range, origin)
                    .Where(u =>
                    {
                        if (u == null || !u.IsValid)
                            return false;

                        if (u.IsCrowdControlled())
                            CcCount++;
                        else 
                        {
                            if (u.IsPlayer)
                                PlayerCount++;
                            else if (!u.Combat && u != StyxWoW.Me.CurrentTarget)
                                AvoidAOE = true;
                            else if (u == StyxWoW.Me.CurrentTarget || u.Aggro || u.TaggedByMe || u.IsTargetingUs())
                            {
                                if (Area == CombatArea.Radius || StyxWoW.Me.IsSafelyFacing(u, 150f))
                                {
                                    if (u.InLineOfSpellSight)
                                    {
                                        MobCount++;
                                    }
                                }
                                return true;
                            }
                        }

                        return false;
                    })
                    .ToList();

                if (!Spell.UseAOE || CcCount > 0 || PlayerCount > 0)
                    AvoidAOE = true;

                if (AvoidAOE)
                    MobCount = MobCount > 1 ? 1 : MobCount;
            }

            if (MaxAgeForDamage > 0)
            {
                long alld, recentd;
                EventHandlers.GetRecentDamage(MaxAgeForDamage, out alld, RecentAgeForDamage, out recentd);
                AllDamage = alld;
                RecentDamage = recentd;
            }

            System.Diagnostics.Debug.Assert(Mobs != null);
        }
    }


    // following class should probably be in Unit, but made a separate 
    // .. extension class to separate the private property names.
    // --
    // credit to Handnavi.  the following is a wrapping of his code
    public static class TimeToDeathExtension
    {
        public static WoWGuid guid { get; set; }  // guid of mob

        private static uint _firstLife;         // life of mob when first seen
        private static uint _firstLifeMax;      // max life of mob when first seen
        private static int _firstTime;          // time mob was first seen
        private static uint _currentLife;       // life of mob now
        private static int _currentTime;        // time now

        /// <summary>
        /// seconds until the target dies.  first call initializes values. subsequent
        /// return estimate or indeterminateValue if death can't be calculated
        /// </summary>
        /// <param name="target">unit to monitor</param>
        /// <param name="indeterminateValue">return value if death cannot be calculated ( -1 or int.MaxValue are common)</param>
        /// <returns>number of seconds </returns>
        public static long TimeToDeath(this WoWUnit target, long indeterminateValue = -1)
        {
            if (target == null || !target.IsValid || !target.IsAlive)
            {
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) is dead!", target.SafeName(), target.Guid, target.Entry);
                return 0;
            }

            if (StyxWoW.Me.CurrentTarget.IsTrainingDummy())
            {
                return 111;     // pick a magic number since training dummies dont die
            }

            //Fill variables on new target or on target switch, this will loose all calculations from last target
            if (guid != target.Guid || (guid == target.Guid && target.CurrentHealth == _firstLifeMax))
            {
                guid = target.Guid;
                _firstLife = target.CurrentHealth;
                _firstLifeMax = target.MaxHealth;
                _firstTime = ConvDate2Timestam(DateTime.UtcNow);
                //Lets do a little trick and calculate with seconds / u know Timestamp from unix? we'll do so too
            }
            _currentLife = target.CurrentHealth;
            _currentTime = ConvDate2Timestam(DateTime.UtcNow);
            int timeDiff = _currentTime - _firstTime;
            uint hpDiff = _firstLife - _currentLife;
            if (hpDiff > 0)
            {
                /*
                * Rule of three (Dreisatz):
                * If in a given timespan a certain value of damage is done, what timespan is needed to do 100% damage?
                * The longer the timespan the more precise the prediction
                * time_diff/hp_diff = x/first_life_max
                * x = time_diff*first_life_max/hp_diff
                * 
                * For those that forgot, http://mathforum.org/library/drmath/view/60822.html
                */
                long fullTime = timeDiff * _firstLifeMax / hpDiff;
                long pastFirstTime = (_firstLifeMax - _firstLife) * timeDiff / hpDiff;
                long calcTime = _firstTime - pastFirstTime + fullTime - _currentTime;
                if (calcTime < 1) calcTime = 1;
                //calc_time is a int value for time to die (seconds) so there's no need to do SecondsToTime(calc_time)
                long timeToDie = calcTime;
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) dies in {3}, you are dpsing with {4} dps", target.SafeName(), target.Guid, target.Entry, timeToDie, dps);
                return timeToDie;
            }
            if (hpDiff <= 0)
            {
                //unit was healed,resetting the initial values
                guid = target.Guid;
                _firstLife = target.CurrentHealth;
                _firstLifeMax = target.MaxHealth;
                _firstTime = ConvDate2Timestam(DateTime.UtcNow);
                //Lets do a little trick and calculate with seconds / u know Timestamp from unix? we'll do so too
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) was healed, resetting data.", target.SafeName(), target.Guid, target.Entry);
                return indeterminateValue;
            }
            if (_currentLife == _firstLifeMax)
            {
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) is at full health.", target.SafeName(), target.Guid, target.Entry);
                return indeterminateValue;
            }
            //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) no damage done, nothing to calculate.", target.SafeName(), target.Guid, target.Entry);
            return indeterminateValue;
        }


        private static readonly DateTime timeOrigin = new DateTime(2012, 1, 1); // Refernzdatum (festgelegt)

        private static int ConvDate2Timestam(DateTime time)
        {
#if PREV
                DateTime baseLine = new DateTime(1970, 1, 1); // Refernzdatum (festgelegt)
                DateTime date2 = time; // jetztiges Datum / Uhrzeit
                var ts = new TimeSpan(date2.Ticks - baseLine.Ticks); // das Delta ermitteln
                // Das Delta als gesammtzahl der sekunden ist der Timestamp
                return (Convert.ToInt32(ts.TotalSeconds));
#else
            return (int)(time - timeOrigin).TotalSeconds;
#endif
        }

        /// <summary>
        /// creates behavior to write timetodeath values to debug log.  only
        /// evaluated if Singular Debug setting is enabled
        /// </summary>
        /// <returns></returns>
        public static Composite CreateWriteDebugTimeToDeath()
        {
            return new Action(
                ret =>
                {
                    if (SingularSettings.Debug && StyxWoW.Me.GotTarget())
                    {
                        long timeNow = StyxWoW.Me.CurrentTarget.TimeToDeath();
                        if (timeNow != lastReportedTime || guid != StyxWoW.Me.CurrentTargetGuid )
                        {
                            lastReportedTime = timeNow;
                            Logger.WriteFile( "TimeToDeath: {0} (GUID: {1}, Entry: {2}) dies in {3}", 
                                StyxWoW.Me.CurrentTarget.SafeName(),
                                StyxWoW.Me.CurrentTarget.Guid,
                                StyxWoW.Me.CurrentTarget.Entry, 
                                lastReportedTime);
                        }
                    }

                    return RunStatus.Failure;
                });

        }

        private static long lastReportedTime = -111;

    }

}
