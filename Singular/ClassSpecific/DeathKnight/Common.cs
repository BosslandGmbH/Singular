﻿using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using System.Drawing;
using System.Collections.Generic;

namespace Singular.ClassSpecific.DeathKnight
{
    public static class Common
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static DeathKnightSettings Settings => SingularSettings.Instance.DeathKnight();
	    public static long RunicPowerDeficit => Me.MaxRunicPower - Me.CurrentRunicPower;
	    public static bool HasTalent(DeathKnightTalents talent) => TalentManager.IsSelected((int)talent);

		#region Pull

		// All DKs should be throwing death grip when not in intances. It just speeds things up, and makes a mess for PVP :)
		[Behavior(BehaviorType.Pull, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightCommonPull()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateDeathGripBehavior()
                    )
                )
			);
        }

		#endregion

		#region CombatBuffs

		[Behavior(BehaviorType.CombatBuffs, WoWClass.DeathKnight)]
		public static Composite CreateDeathKnightBloodCombatBuffs()
		{
			return new Decorator(
				req => !Me.GotTarget() || !Me.CurrentTarget.IsTrivial(),
				new PrioritySelector(
					Helpers.Common.CreateCombatRezBehavior("Raise Ally", on => ((WoWUnit)on).SpellDistance() < 40 && ((WoWUnit)on).InLineOfSpellSight)
					)
				);
		}

		#endregion

		#region PreCombatBuffs

		[Behavior(BehaviorType.PreCombatBuffs, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightPreCombatBuffs()
        {
            return new PrioritySelector(
                // limit PoF to once every ten seconds in case there is some
                // .. oddness here
                new Throttle(10, Spell.BuffSelf("Path of Frost", req => Settings.UsePathOfFrost))
                );
        }

        #endregion

        #region Death Grip

        public static Composite CreateDeathGripBehavior()
        {
            return new Sequence(
                Spell.Cast("Death Grip",
                    req => !MovementManager.IsMovementDisabled
                        && (SingularRoutine.CurrentWoWContext != WoWContext.Instances || !Me.GroupInfo.IsInParty || Me.IsTank)
                        && Me.CurrentTarget.DistanceSqr > 10 * 10
                        && (((Me.CurrentTarget.IsPlayer || Me.CurrentTarget.TaggedByMe) && !Me.CurrentTarget.IsMovingTowards() ) || (!Me.CurrentTarget.TaggedByOther && Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull))
                    ),
                new DecoratorContinue( req => Me.IsMoving, new Action(req => StopMoving.Now())),
                new WaitContinue( 1, until => !Me.GotTarget() || Me.CurrentTarget.IsWithinMeleeRange, new ActionAlwaysSucceed())
                );
        }

		#endregion

		public static Composite CreateAntiMagicShellBehavior()
        {
            return Spell.Cast(
                "Anti-Magic Shell",
                on =>
                {
                    bool isMindFreezeOnCD = Spell.IsSpellOnCooldown("Mind Freeze");
                    return Unit.UnitsInCombatWithUsOrOurStuff(40)
                        .FirstOrDefault(u =>
                            (u.IsCasting)
                            && u.CurrentTargetGuid == StyxWoW.Me.Guid
                            && (!u.CanInterruptCurrentSpellCast || isMindFreezeOnCD || !Spell.CanCastHack("Mind Freeze", u))
                    );
                });
        }

        /// <summary>
        /// invoke on CurrentTarget if not tagged. use ranged instant casts if possible.  this
        /// is a blend of abilities across all specializations
        /// </summary>
        /// <returns></returns>
        public static Composite CreateDeathKnightPullMore()
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return new ActionAlwaysFail();

            return new Throttle(
                2,
                new Decorator(
                    req => Me.GotTarget()
                        && !Me.CurrentTarget.IsPlayer
                        && !Me.CurrentTarget.IsTagged
                        && !Me.CurrentTarget.IsWithinMeleeRange,
                    new PrioritySelector(
                        new Sequence(
                            ctx => Me.CurrentTarget,
                            Spell.Cast("Death Grip", on => (on as WoWUnit)),
                            new DecoratorContinue( req => Me.IsMoving, new Action(req => StopMoving.Now())),
                            new WaitContinue( TimeSpan.FromMilliseconds(500), until => !Me.IsMoving, new ActionAlwaysSucceed()),
                            new WaitContinue( 1, until => (until as WoWUnit).IsWithinMeleeRange, new ActionAlwaysSucceed())
                            ),
                        Spell.Cast("Dark Command", req => Me.Specialization == WoWSpec.DeathKnightBlood )
                        )
                    )
                );
        }
	}

	public enum DeathKnightTalents
	{
		Bloodworms = 1,
		Hearthbreaker,
		Blooddrinker,

		ShatteringStrike = Bloodworms,
		IcyTalons = Hearthbreaker,
		MurderousEfficiency = Blooddrinker,

		AllWillServer = Bloodworms,
		BurstingSores = Hearthbreaker,
		EbonFever = Blooddrinker,

		RapidDecomposition = 4,
		Soulgorge,
		SpectralDeflection,

		FreezingFog = RapidDecomposition,
		FrozenPulse = Soulgorge,
		HornOfWinter = SpectralDeflection,

		Epidemic = RapidDecomposition,
		PestilentPustules = Soulgorge,
		BlightedRuneWeapon = SpectralDeflection,

		Ossuary = 7,
		BloodTap,
		AntiMagicBarrier,

		Icecap = Ossuary,
		HungeringRuneWeapon = BloodTap,
		Avalanche = AntiMagicBarrier,

		UnholyFrenzy = Ossuary,
		Castigator = BloodTap,
		ClawingShadows = AntiMagicBarrier,

		MarkOfBlood = 10,
		RedThirst,
		Tombstone,

		AbominationsMight = MarkOfBlood,
		BlindingSleet = RedThirst,
		WinterIsComing = Tombstone,

		SludgeBelcher = MarkOfBlood,
		Asphyxiate = RedThirst,
		DebilitatingInfestation = Tombstone,

		TighteningGrasp = 13,
		TrembleBeforeMe,
		MarchOfTheDamned,

		VolatileShielding = TighteningGrasp,
		Permafrost = TrembleBeforeMe,
		WhiteWalker = MarchOfTheDamned,

		SpellEater = TighteningGrasp,
		CorpseShield = TrembleBeforeMe,
		LingeringApparition = MarchOfTheDamned,

		WillOfTheNecropolis = 16,
		RuneTap,
		FoulBulwark,

		Frostscythe = WillOfTheNecropolis,
		RunicAttenuation = RuneTap,
		GatheringStorm = FoulBulwark,

		ShadowInfusion = WillOfTheNecropolis,
		Necrosis = RuneTap,
		InfectedClaws = FoulBulwark,

		Bonestorm = 19,
		BloodMirror,
		Purgatory,

		Obliteration = Bonestorm,
		BreathOfSindragosa = BloodMirror,
		GlacialAdvance = Purgatory,

		DarkArbiter = Bonestorm,
		Defile = BloodMirror,
		SoulReaper = Purgatory
	}
}