using System.Linq;

using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Rest = Singular.Helpers.Rest;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;


namespace Singular.ClassSpecific
{
    public static class Generic
    {
        [Behavior(BehaviorType.Rest, priority:999)]
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

        [Behavior(BehaviorType.Combat, priority: 999)]
        //[IgnoreBehaviorCount(BehaviorType.Combat), IgnoreBehaviorCount(BehaviorType.Rest)]
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

            if ( SingularSettings.IsTrinketUsageWanted(TrinketUsage.OnCooldown) || SingularSettings.IsTrinketUsageWanted(TrinketUsage.OnCooldownInCombat))
            {
                ps.AddChild( Item.UseEquippedTrinket(TrinketUsage.OnCooldown));
            }

            if ( SingularSettings.IsTrinketUsageWanted(TrinketUsage.LowHealth))
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

        [Behavior(BehaviorType.Combat, priority: 998)]
        //[IgnoreBehaviorCount(BehaviorType.Combat), IgnoreBehaviorCount(BehaviorType.Rest)]
        public static Composite CreateRacialBehaviour()
        {
            return new Decorator(
                ret => SingularSettings.Instance.UseRacials,
                new PrioritySelector(
                    new Decorator(
                        ret => SpellManager.CanCast("Stoneform") && StyxWoW.Me.GetAllAuras().Any(a => a.Spell.Mechanic == WoWSpellMechanic.Bleeding ||
                            a.Spell.DispelType == WoWDispelType.Disease ||
                            a.Spell.DispelType == WoWDispelType.Poison),
                        Spell.Cast("Stoneform")),
                    new Decorator(
                        ret => SpellManager.CanCast("Escape Artist") && Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Rooted, WoWSpellMechanic.Snared),
                        Spell.Cast("Escape Artist")),
                    new Decorator(
                        ret => SpellManager.CanCast("Every Man for Himself") && Unit.IsCrowdControlled(StyxWoW.Me),
                        Spell.Cast("Every Man for Himself")),
                    new Decorator(
                        ret => SpellManager.CanCast("Gift of the Naaru") && StyxWoW.Me.HealthPercent < SingularSettings.Instance.GiftNaaruHP,
                        Spell.Cast("Gift of the Naaru")),
                    new Decorator(
                        ret => SingularSettings.Instance.ShadowmeldThreatDrop && SpellManager.CanCast("Shadowmeld") && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid) &&
                            !Unit.GroupMemberInfos.Any(pm => pm.Guid == StyxWoW.Me.Guid && pm.Role == WoWPartyMember.GroupRole.Tank) &&
                            ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Any(unit => unit.CurrentTargetGuid == StyxWoW.Me.Guid),
                        Spell.Cast("Shadowmeld"))
                    ));
        }
    }
}
