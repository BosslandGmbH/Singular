#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author: apoc $
// $Date: 2011-02-17 10:50:06 +0200 (Per, 17 Şub 2011) $
// $HeadURL: http://svn.apocdev.com/singular/trunk/Singular/ClassSpecific/Priest/Discipline.cs $
// $LastChangedBy: apoc $
// $LastChangedDate: 2011-02-17 10:50:06 +0200 (Per, 17 Şub 2011) $
// $LastChangedRevision: 72 $
// $Revision: 72 $

#endregion

using System.Collections.Generic;
using System.Linq;

using Singular.Settings;

using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using System;

namespace Singular
{
	partial class SingularRoutine
	{
		[Class(WoWClass.Druid)]
		[Spec(TalentSpec.RestorationDruid)]
		[Behavior(BehaviorType.Rest)]
		[Context(WoWContext.All)]
		public Composite CreateRestoDruidHealRest()
		{
			return new PrioritySelector(
				// Rest up damnit! Do this first, so we make sure we're fully rested.
				CreateDefaultRestComposite(SingularSettings.Instance.DefaultRestHealth, SingularSettings.Instance.DefaultRestMana),
				// Make sure we're healing OOC too!
				CreateRestoDruidHealOnlyBehavior(),
				// Can we res people?
				new Decorator(
					ret => ResurrectablePlayers.Count != 0,
					CreateSpellCast("Resurrection", ret => true, ret => ResurrectablePlayers.FirstOrDefault()))
				);
		}

		private Composite CreateRestoDruidHealOnlyBehavior()
		{
			return new
				Decorator(
				ret => HealTargeting.Instance.FirstUnit != null,
				new PrioritySelector(
					ctx => HealTargeting.Instance.FirstUnit,
					CreateWaitForCast(),
				// Ensure we're in range of the unit to heal, and it's in LOS.
					CreateRangeAndFace(35f, ret => (WoWUnit)ret),
					//Cast Lifebloom on tank if
					//1- Tank doesn't have lifebloom
					//2- Tank has less then 3 stacks of lifebloom
					//3- Tank has 3 stacks of lifebloom but it will expire in 3 seconds
					CreateSpellCast("Lifebloom", 
						ret => Me.Combat && RaFHelper.Leader != null && (WoWUnit)ret == RaFHelper.Leader &&
							   ((WoWUnit)ret).HealthPercent > 60 && 
							   (!((WoWUnit)ret).HasAura("Lifebloom") || ((WoWUnit)ret).Auras["Lifebloom"].StackCount < 3 ||
							   ((WoWUnit)ret).Auras["Lifebloom"].TimeLeft <= TimeSpan.FromSeconds(3)),
						ret => (WoWUnit)ret),
					//Cast rebirth if the tank is dead. Check for Unburdened Rebirth glyph or Maple seed reagent
					CreateSpellCast("Rebirth",
						ret => Me.Combat && RaFHelper.Leader != null && (WoWUnit)ret == RaFHelper.Leader &&
							   ((WoWUnit)ret).Dead && (TalentManager.HasGlyph("Unburdened Rebirth") || Me.BagItems.Any(i => i.Entry == 17034)),
						ret => (WoWUnit)ret),
					CreateSpellCast("Tranquility",
						ret => Me.Combat && Me.IsInParty && NearbyFriendlyPlayers.Count(p =>
								p.IsAlive && p.HealthPercent <= SingularSettings.Instance.Druid.TranquilityHealth && p.Distance <= 30) >= SingularSettings.Instance.Druid.TranquilityCount),
					//Use Innervate on party members if we have Glyph of Innervate
					CreateSpellBuff("Innervate",
						ret => TalentManager.HasGlyph("Innervate") && Me.Combat && (WoWUnit)ret != Me && Me.ManaPercent <= SingularSettings.Instance.Druid.InnervateMana && 
							   ((WoWUnit)ret).PowerType == Styx.WoWPowerType.Mana && ((WoWUnit)ret).ManaPercent <= SingularSettings.Instance.Druid.InnervateMana,
						ret => (WoWUnit)ret),
					CreateSpellCast("Swiftmend",
						ret => Me.Combat && ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Druid.Swiftmend && 
							   (((WoWUnit)ret).HasAura("Rejuvenation") || ((WoWUnit)ret).HasAura("Regrowth")),
						ret => (WoWUnit)ret),
					CreateSpellCast("Wild Growth",
						ret => Me.IsInParty && NearbyFriendlyPlayers.Count(
									p => p.IsAlive && p.HealthPercent <= SingularSettings.Instance.Druid.WildGrowthHealth &&
										 p.Location.Distance(((WoWUnit)ret).Location) <= 30) >= SingularSettings.Instance.Druid.WildGrowthCount,
						ret => (WoWUnit)ret),
					CreateSpellBuff("Regrowth",
						ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Druid.Regrowth,
						ret => (WoWUnit)ret),
					CreateSpellCast("Healing Touch",
						ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Druid.HealingTouch,
						ret => (WoWUnit)ret),
					CreateSpellCast("Nourish",
						ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Druid.Nourish && 
							   (((WoWUnit)ret).HasAura("Rejuvenation") || ((WoWUnit)ret).HasAura("Regrowth") ||
							   ((WoWUnit)ret).HasAura("Lifebloom") || ((WoWUnit)ret).HasAura("Wild Growth")),
						ret => (WoWUnit)ret),
					CreateSpellCast("Rejuvenation",
						ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Druid.Rejuvenation,
						ret => (WoWUnit)ret)
					));
		}

		[Class(WoWClass.Druid)]
		[Spec(TalentSpec.RestorationDruid)]
		[Behavior(BehaviorType.Combat)]
		[Context(WoWContext.All)]
		public Composite CreateRestoDruidCombat()
		{
			return
				new PrioritySelector(
					// Firstly, deal with healing people!
					CreateRestoDruidHealOnlyBehavior(),
					new Decorator(
						ret => !Me.IsInParty,
						new PrioritySelector(
							CreateSpellBuff("Moonfire"),
							CreateSpellCast("Starfire", ret => Me.HasAura("Fury of Stormrage")),
							CreateSpellCast("Wrath")))
				);
		}

		[Class(WoWClass.Druid)]
		[Spec(TalentSpec.RestorationDruid)]
		[Behavior(BehaviorType.Pull)]
		[Context(WoWContext.All)]
		public Composite CreateRestoDruidPull()
		{
			return
				new PrioritySelector(
					new Decorator(
						ret => !Me.IsInParty,
						CreateSpellCast("Wrath"))
				);
		}

		[Class(WoWClass.Druid)]
		[Spec(TalentSpec.RestorationDruid)]
		[Behavior(BehaviorType.CombatBuffs)]
		[Context(WoWContext.All)]
		public Composite CreateRestoDruidCombatBuffs()
		{
			return 
				new PrioritySelector(
					CreateSpellBuffOnSelf("Tree of Life",
						ret => Me.IsInParty && NearbyFriendlyPlayers.Count(
								p => p.IsAlive && p.HealthPercent <= SingularSettings.Instance.Druid.TreeOfLifeHealth) >= SingularSettings.Instance.Druid.TreeOfLifeCount),
					CreateSpellBuffOnSelf("Innervate",
						ret => Me.ManaPercent <= SingularSettings.Instance.Druid.InnervateMana),
					CreateSpellBuffOnSelf("Barkskin",
						ret => Me.HealthPercent <= SingularSettings.Instance.Druid.Barkskin)
				);
		}
	}
}