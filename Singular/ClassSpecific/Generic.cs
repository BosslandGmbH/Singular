using System.Linq;

using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using System;


namespace Singular.ClassSpecific
{
    public static class Generic
    {
        // [Behavior(BehaviorType.PreCombatBuffs, priority:999)]
        // [IgnoreBehaviorCount(BehaviorType.Combat), IgnoreBehaviorCount(BehaviorType.Rest)]
        public static Composite CreateFlasksBehaviour()
        {
            return new Decorator(
                ret => SingularSettings.Instance.UseAlchemyFlasks && !Unit.HasAnyAura(StyxWoW.Me, "Enhanced Agility", "Enhanced Intellect", "Enhanced Strength"),
                new PrioritySelector(
                    Item.UseItem(75525),
                    Item.UseItem(58149),
                    Item.UseItem(47499)));
        }

        // [Behavior(BehaviorType.Combat, priority: 999)]
        public static Composite CreateUseTrinketsBehaviour()
        {
            // Saving Settings via GUI will now force reinitialize so we can build the behaviors
            // basead upon the settings rather than continually checking the settings in the Btree
            // 
            // 

            if (SingularSettings.Instance.Trinket1Usage == TrinketUsage.Never && SingularSettings.Instance.Trinket2Usage == TrinketUsage.Never)
            {
                return new Action(ret => { return RunStatus.Failure; });
            }

            PrioritySelector ps = new PrioritySelector();

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.OnCooldown))
            {
                ps.AddChild(Item.UseEquippedTrinket(TrinketUsage.OnCooldown));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.OnCooldownInCombat))
            {
                ps.AddChild(Item.UseEquippedTrinket(TrinketUsage.OnCooldownInCombat));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.LowHealth))
            {
                ps.AddChild( new Decorator( ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.PotionHealth,
                                            Item.UseEquippedTrinket( TrinketUsage.LowHealth)));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.LowPower))
            {
                ps.AddChild( new Decorator( ret => StyxWoW.Me.PowerPercent < SingularSettings.Instance.PotionMana,
                                            Item.UseEquippedTrinket(TrinketUsage.LowPower)));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.CrowdControlled ))
            {
                ps.AddChild( new Decorator( ret => Unit.IsCrowdControlled( StyxWoW.Me),
                                            Item.UseEquippedTrinket( TrinketUsage.CrowdControlled )));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.CrowdControlledSilenced ))
            {
                ps.AddChild( new Decorator( ret => StyxWoW.Me.Silenced && Unit.IsCrowdControlled( StyxWoW.Me),
                                            Item.UseEquippedTrinket(TrinketUsage.CrowdControlledSilenced)));
            }

            return ps;
        }

        // [Behavior(BehaviorType.Combat, priority: 998)]
        public static Composite CreateRacialBehaviour()
        {
            return new Throttle( TimeSpan.FromMilliseconds(250),
                new Decorator(
                    ret => SingularSettings.Instance.UseRacials,
                    new PrioritySelector(
                        new Decorator(
                            ret => SpellManager.CanCast("Stoneform") && StyxWoW.Me.GetAllAuras().Any(a => a.Spell.Mechanic == WoWSpellMechanic.Bleeding ||
                                a.Spell.DispelType == WoWDispelType.Disease ||
                                a.Spell.DispelType == WoWDispelType.Poison),
                            Spell.Cast("Stoneform")),
                        new Decorator(
                            ret => SpellManager.CanCast("Escape Artist") && Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Rooted, WoWSpellMechanic.Snared),
                            Spell.BuffSelf("Escape Artist")),
                        new Decorator(
                            ret => SpellManager.CanCast("Gift of the Naaru") && StyxWoW.Me.HealthPercent < SingularSettings.Instance.GiftNaaruHP,
                            Spell.Cast("Gift of the Naaru")),
                        Spell.BuffSelf("Shadowmeld", ret => NeedShadowmeld()),
                        Spell.BuffSelf("Lifeblood", ret => !PartyBuff.WeHaveBloodlust && !StyxWoW.Me.HasAnyAura("Lifeblood", "Berserking")),
                        Spell.BuffSelf("Berserking", ret => !PartyBuff.WeHaveBloodlust && !StyxWoW.Me.HasAura("Lifeblood")),
                        Spell.BuffSelf("Blood Fury")
                        )
                    )
                );
        }

        private static bool NeedShadowmeld()
        {
            if ( !SingularSettings.Instance.ShadowmeldThreatDrop || StyxWoW.Me.Race != WoWRace.NightElf )
                return false;

            if ( !SpellManager.CanCast("Shadowmeld") )
                return false;

            if (StyxWoW.Me.IsInGroup())
            {
                if (Group.MeIsTank)
                    return false;

                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                {
                    if (!Unit.NearbyUnfriendlyUnits.Any(unit => unit.CurrentTargetGuid == StyxWoW.Me.Guid && (unit.Class == WoWClass.Hunter || unit.Class == WoWClass.Mage || unit.Class == WoWClass.Priest || unit.Class == WoWClass.Warlock)))
                    {
                        return true;    // since likely a ranged target
                    }
                }
                else if (!Unit.NearbyUnfriendlyUnits.Any(unit => unit.CurrentTargetGuid == StyxWoW.Me.Guid))
                {
                    return false;
                }

                if (Group.AnyTankNearby)
                    return true;
            }

            // need to add logic to wait for pats, or for PVP losing ranged targets may be enough
            return false;
        }

        // [Behavior(BehaviorType.Combat, priority: 997)]
        public static Composite CreatePotionAndHealthstoneBehavior()
        {
            return Item.CreateUsePotionAndHealthstone(SingularSettings.Instance.PotionHealth, SingularSettings.Instance.PotionMana);
        }
    }


    public static class NoContextAvailable
    {
        public static Composite CreateDoNothingBehavior()
        {
            return new Throttle( 15,
                new Action( r => Logger.Write( "No Context Available - do nothing while we wait"))
                );
        }
    }
}
