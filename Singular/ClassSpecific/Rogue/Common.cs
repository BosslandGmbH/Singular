using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Singular.Settings;
using System;

namespace Singular.ClassSpecific.Rogue
{
    public class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static RogueSettings RogueSettings { get { return SingularSettings.Instance.Rogue; } }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Rogue)]
        public static Composite CreateRoguePreCombatBuffs()
        {
            return new PrioritySelector(
                CreateApplyPoisons(),
                Spell.BuffSelf("Recuperate", ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.HealthPercent < 80)
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Rogue)]
        public static Composite CreateRogueRest()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Stealth", ret => StyxWoW.Me.HasAura("Food")),
                new Decorator(
                    ret => StyxWoW.Me.HasAura("Vanish") && StyxWoW.Me.CurrentMap.IsContinent,
                    new ActionAlwaysSucceed()),
                Rest.CreateDefaultRestBehaviour()
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Rogue, (WoWSpec) int.MaxValue, WoWContext.All, 1)]
        public static Composite CreateRogueCombatHeal()
        {
            return new Decorator(
                ret => !Spell.IsCastingOrChannelling() & !Spell.IsGlobalCooldown() && !Me.HasAura("Vanish") && !Me.IsStealthed,
                new PrioritySelector(
                    Movement.CreateFaceTargetBehavior(),
                    new Decorator(
                        ret => SingularSettings.Instance.UseBandages
                            && StyxWoW.Me.HealthPercent < 15
                            && !Unit.NearbyUnfriendlyUnits.Any(u => u.Combat && u.Guid != StyxWoW.Me.CurrentTargetGuid && u.CurrentTargetGuid == StyxWoW.Me.Guid)
                            && Item.HasBandage(),
                        new Sequence(
                            new PrioritySelector(
                                Spell.Cast("Gouge"),
                                Spell.Cast("Blind"),
                                new Decorator( 
                                    ret => !Unit.NearbyUnfriendlyUnits.Any(u => !u.IsCrowdControlled()),
                                    new ActionAlwaysSucceed()
                                    )
                                ),
                            Helpers.Common.CreateWaitForLagDuration(),
                            new WaitContinue(TimeSpan.FromMilliseconds(250), ret => Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                            new WaitContinue(TimeSpan.FromMilliseconds(1500), ret => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                            Item.CreateUseBandageBehavior()
                            )
                        ),

                    Spell.BuffSelf("Recuperate", 
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.Rogue.RecuperateHealth  
                            && StyxWoW.Me.RawComboPoints > 0)
                    )
                );

        }

        public static Composite CreateApplyPoisons()
        {
            return new PrioritySelector(
                new Decorator(r => Poisons.NeedLethalPosion() > 0, Spell.BuffSelf(Poisons.NeedLethalPosion())),
                new Decorator(r => Poisons.NeedNonLethalPosion() > 0, Spell.BuffSelf(Poisons.NeedNonLethalPosion()))
                );
        }

        public static Composite CreateRogueBlindOnAddBehavior()
        {
            return new PrioritySelector(
                    ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u =>
                            u.IsTargetingMeOrPet && u != StyxWoW.Me.CurrentTarget),
                    new Decorator(
                        ret => ret != null && !StyxWoW.Me.HasAura("Blade Flurry"),
                        Spell.Buff("Blind", ret => (WoWUnit)ret, ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro) > 1)));
        }

        public static WoWUnit BestTricksTarget
        {
            get
            {
                if (!StyxWoW.Me.GroupInfo.IsInParty && !StyxWoW.Me.GroupInfo.IsInRaid)
                    return null;

                // If the player has a focus target set, use it instead. TODO: Add Me.FocusedUnit to the HB API.
                if (StyxWoW.Me.FocusedUnitGuid != 0)
                    return StyxWoW.Me.FocusedUnit;

                if (StyxWoW.Me.IsInInstance)
                {
                    if (RaFHelper.Leader != null && !RaFHelper.Leader.IsMe)
                    {
                        // Leader first, always. Otherwise, pick a rogue/DK/War pref. Fall back to others just in case.
                        return RaFHelper.Leader;
                    }

                    if (StyxWoW.Me.GroupInfo.IsInParty)
                    {
                        var bestTank = Group.Tanks.OrderBy(t => t.DistanceSqr).FirstOrDefault(t => t.IsAlive);

                        if (bestTank != null)
                            return bestTank;
                    }

                    var bestPlayer = Group.GetPlayerByClassPrio(100f, false,
                        WoWClass.Rogue, WoWClass.DeathKnight, WoWClass.Warrior,WoWClass.Hunter, WoWClass.Mage, WoWClass.Warlock, WoWClass.Shaman, WoWClass.Druid,
                        WoWClass.Paladin, WoWClass.Priest);
                    return bestPlayer;
                }

                return null;
            }
        }
    }
}
