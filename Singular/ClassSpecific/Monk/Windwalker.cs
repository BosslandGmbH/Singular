using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

using Singular.Settings;
using Styx.Common;
using Styx.WoWInternals;
using Styx.Common.Helpers;

namespace Singular.ClassSpecific.Monk
{
    public class Windwalker
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static MonkSettings MonkSettings => SingularSettings.Instance.Monk();

	    private static uint EnergyDeficit => Me.MaxEnergy - Me.CurrentEnergy;

		[Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkWindwalker)]
		public static Composite CreateWindwalkerMonkCombatBuffs()
		{
			return new PrioritySelector(
				Spell.BuffSelf("Energizing Elixir", req => Me.CurrentChi <= 1 && Me.CurrentEnergy < 25)
				);

		}

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkWindwalker)]
        public static Composite CreateWindwalkerMonkCombat()
        {
            return new PrioritySelector(
				Common.CreateAttackFlyingOrUnreachableMobs(),

				// keep FoF only as long as we can hit something alive in melee range
				new Decorator(
					ret => StyxWoW.Me.HasMyAura("Fists of Fury"),
					new PrioritySelector(
						// no enemies in melee, cancel cast so we can move
						new Decorator(
							req => !Unit.UnfriendlyUnits().Any(u => u.IsWithinMeleeRange),
							new Sequence(
								new Action(ret =>
								{
									Logger.Write(LogColor.Cancel, "/cancel Fists of Fury: no enemies in melee range");
									SpellManager.StopCasting();
									return RunStatus.Success;
								}),
								new Wait(TimeSpan.FromMilliseconds(500), until => !Me.HasMyAura("Fists of Fury"), new ActionAlwaysSucceed())
								)
							),
						// enemies in melee, but not facing currently
						new Decorator(
							req => !Unit.UnfriendlyUnits().Any(u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)),
							new PrioritySelector(
								ctx => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange
									? Me.CurrentTarget
									: Unit.UnfriendlyUnits().FirstOrDefault(u => u.IsWithinMeleeRange),
								Movement.CreateFaceTargetBehavior(on => (WoWUnit)on, 60f, true)
								)
							),

						Spell.WaitForCastOrChannel()
						)
					),

				Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCast(),

                // close distance if at range
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
						Common.CreateMonkCloseDistanceBehavior( ),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Spell.Cast("Healing Elixir", ret => Me.HealthPercent <= 70 && Spell.GetCharges("Healing Elixir") > 1),
						Spell.BuffSelf("Serenity", req => Me.CurrentTarget.IsStressful()),
						Spell.Cast("Touch of Death", req => Me.CurrentTarget.TimeToDeath() > 8),
						Spell.Cast("Storm, Earth, and Fire", req => MonkSettings.UseSef && !Me.HasActiveAura("Storm, Earth, and Fire") && Me.CurrentTarget.IsStressful()),
						Spell.Cast("Touch of Karma", req => Me.CurrentTarget.IsStressful() && Me.CurrentTarget.IsTargetingMeOrPet),

                        // Multiple Target
						new Decorator(ret => Unit.UnfriendlyUnits(8).Count() >= 2,
							new PrioritySelector(
								Spell.BuffSelf("Serenity"),
								Spell.Cast("Strike of the Windlord", ret => MonkSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None),
								Spell.Cast("Whirling Dragon Punch"),
								Spell.Cast("Fists of Fury"),
								Spell.Cast("Rushing Jade Wind", req => Unit.UnfriendlyUnits(8).Count() <= 7),
								new Decorator(ret => Unit.UnfriendlyUnits(8).Count() >= 4,
									new PrioritySelector(
										Spell.Cast("Spinning Crane Kick"),
										Spell.Cast("Chi Burst")
										)),
								Spell.Cast("Chi Burst"),
								Spell.Cast("Leg Sweep", ret => MonkSettings.StunMobsWhileSolo && Me.CurrentTarget.IsWithinMeleeRange),
								Spell.Cast("Spinning Crane Kick"),
								Spell.Cast("Blackout Kick", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => !u.HasMyAura("Mark of the Crane")), req => Spell.LastSpellCast != "Blackout Kick"),
								Spell.Cast("Blackout Kick", req => Spell.LastSpellCast != "Blackout Kick"),
								Spell.Cast("Tiger Palm", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => !u.HasMyAura("Mark of the Crane")), req => Spell.LastSpellCast != "Tiger Palm"),
								Spell.Cast("Tiger Palm", req => Spell.LastSpellCast != "Tiger Palm")
								)),

                        // Single Target
						Spell.Cast("Strike of the Windlord", ret => !MonkSettings.UseArtifactOnlyInAoE && MonkSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None),
                        Spell.Cast("Fists of Fury"),
						Spell.Cast("Whirling Dragon Punch"),
						Spell.Cast("Leg Sweep", ret => MonkSettings.StunMobsWhileSolo && Me.CurrentTarget.IsWithinMeleeRange && Me.CurrentTarget.IsCasting && Spell.IsSpellOnCooldown("Spear Hand Strike")),
						Spell.Cast("Tiger Palm", req => Me.CurrentChi < 4 && EnergyDeficit < 10 && Spell.LastSpellCast != "Tiger Palm"),
						Spell.Cast("Rising Sun Kick"),
						Spell.Cast("Chi Wave"),
						Spell.Cast("Blackout Kick", req => Spell.LastSpellCast != "Blackout Kick"),
						Spell.Cast("Tiger Palm", req => Spell.LastSpellCast != "Tiger Palm")
                        )
                    ),

				Common.CreateCloseDistanceBehavior()
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, WoWSpec.MonkWindwalker, WoWContext.All)]
        public static Composite CreateMonkPreCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(ret => !Helpers.Rest.IsEatingOrDrinking,
					PartyBuff.BuffGroup("Legacy of the White Tiger"))
                );
        }
    }
}