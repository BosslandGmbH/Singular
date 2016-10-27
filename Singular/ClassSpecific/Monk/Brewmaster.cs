using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Common.Helpers;
using Styx.TreeSharp;
using System.Collections.Generic;
using System.Numerics;
using Styx.CommonBot;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using CommonBehaviors.Actions;
using Styx.Common;

namespace Singular.ClassSpecific.Monk
{
    public class Brewmaster
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static MonkSettings MonkSettings => SingularSettings.Instance.Monk();


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        public static Composite CreateMonkPreCombatBuffs()
        {
            return new PrioritySelector(
                PartyBuff.BuffGroup("Legacy of the White Tiger")
                );
        }

		[Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        public static Composite CreateBrewmasterMonkCombatBuffs()
        {
            return new PrioritySelector(
				Spell.BuffSelf("Fortifying Brew", req => Me.HealthPercent <= MonkSettings.FortifyingBrewPct),
                Spell.BuffSelf("Ironskin Brew", req => MonkSettings.UseIronskinBrew && Spell.GetCharges("Ironskin Brew") > MonkSettings.IronskinBrewCharges),
                Spell.BuffSelf("Purifying Brew", req => Me.HasAura((int)MonkSettings.Stagger)),
				Spell.BuffSelf("Expel Harm", req => Common.SphereCount(SphereType.Ox, 30) >= 3),
				Spell.BuffSelf("Black Ox Brew", req => Spell.GetCharges("Ironskin Brew") <= 0)
                );
        }

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        public static Composite CreateBrewmasterMonkCombat()
        {
			TankManager.NeedTankTargeting = (SingularRoutine.CurrentWoWContext == WoWContext.Instances);

            return new PrioritySelector(
				Common.CreateAttackFlyingOrUnreachableMobs(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        new Decorator(
							ret => SingularRoutine.CurrentWoWContext == WoWContext.Instances && Unit.NearbyUnfriendlyUnits.Count(u => !u.IsBoss) > 2,
                            new PrioritySelector(
								CreateSummonBlackOxStatueBehavior(on => Me.CurrentTarget),
								Spell.Cast("Provoke", on => FindStatue(), ret => TankManager.Instance.NeedToTaunt.Count >= 2)
                                                )
                                            ),

                        // taunt if needed
                        Spell.Cast("Provoke", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(), ret => SingularSettings.Instance.EnableTaunting),

                            new PrioritySelector(
							ctx => TankManager.Instance.TargetList.FirstOrDefault(u => u.IsWithinMeleeRange) ?? Me.CurrentTarget,
                            Spell.CastOnGround("Exploding Keg", on => (WoWUnit)on, ret => MonkSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None && Me.HealthPercent <= MonkSettings.ArtifactHealthPercent),
							Spell.Cast("Keg Smash", on => (WoWUnit)on),
							Spell.Cast("Tiger Palm", on => (WoWUnit)on, req => Common.HasTalent(MonkTalents.EyeOfTheTiger) && Me.GetAuraTimeLeft("Eye of the Tiger").TotalSeconds <= 1.8),
							new Decorator(ret => Unit.UnfriendlyUnits(8).Count() >= 2,
                            new PrioritySelector(
									Spell.Cast("Blackout Strike", on => (WoWUnit)on, req => Common.HasTalent(MonkTalents.BlackoutCombo)),
									Spell.Cast("Chi Burst", on => (WoWUnit)on),
									Spell.Cast("Breath of Fire", on => (WoWUnit)on),
									Spell.Cast("Rushing Jade Wind", on => (WoWUnit)on),
									Spell.Cast("Tiger Palm", on => (WoWUnit)on, req => Me.CurrentEnergy >= 65),
									Spell.Cast("Blackout Strike", on => (WoWUnit)on)
									)),

							Spell.Cast("Tiger Palm", on => (WoWUnit)on, req => Me.CurrentEnergy >= 65),
							Spell.Cast("Blackout Strike", on => (WoWUnit)on),
							Spell.Cast("Rushing Jade Wind", on => (WoWUnit)on),
							Spell.Cast("Chi Wave", on => (WoWUnit)on),
							Spell.Cast("Leg Sweep", on => (WoWUnit)on, ret => ((WoWUnit)ret).IsWithinMeleeRange),
							Spell.Cast("Breath of Fire", on => (WoWUnit)on, req => Unit.UnfriendlyUnits(8).Any()),

							Common.CreateCloseDistanceBehavior()
                        )
                    )
                                            )
                );
        }

        private static WoWUnit _statue;

        private static Composite CreateSummonBlackOxStatueBehavior( UnitSelectionDelegate on )
        {
            if (!SpellManager.HasSpell("Summon Black Ox Statue"))
                return new ActionAlwaysFail();

            return new Throttle(
                8,
                new Decorator(
                    req => !Spell.IsSpellOnCooldown("Summon Black Ox Statue"),

                    new PrioritySelector(
                        ctx => _statue = FindStatue(),

                        new Decorator(

                            req => _statue == null || (Me.GotTarget() && _statue.SpellDistance(on(req)) > 30),

                            new Throttle(
                                10,
                                new PrioritySelector(

                                    ctx => on(ctx),

                                    Spell.CastOnGround(
                                        "Summon Black Ox Statue",
                                        loc =>
                                        {
                                            WoWUnit unit = on(loc);
                                            Vector3 locStatue = WoWMovement.CalculatePointFrom(unit.Location, -5);
                                            if (locStatue.Distance(Me.Location) > 30)
                                            {
                                                float needFacing = Styx.Helpers.WoWMathHelper.CalculateNeededFacing(Me.Location, locStatue );
                                                locStatue = locStatue.RayCast(needFacing, 30f);
                                            }
                                            return locStatue;
                                        },
                                        req => req != null,
                                        false,
                                        desc => string.Format("{0:F1} yds from {1}", 5, (desc as WoWUnit).SafeName())
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static WoWUnit FindStatue()
        {
            const uint BLACK_OX_STATUE = 61146;
            WoWGuid guidMe = Me.Guid;
            return ObjectManager.GetObjectsOfType<WoWUnit>()
                .FirstOrDefault(u => u.Entry == BLACK_OX_STATUE && u.CreatedByUnitGuid == guidMe);
        }

    }
}