using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Singular.Lists;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;
using Styx.Logic.Combat;

using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Shaman
{
    class Enhancement
    {
        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.EnhancementShaman)]
        [Behavior(BehaviorType.PullBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateEnhancementShamanPullBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Lightning Shield"),
                Spell.Cast("Windfury Weapon", ret => !Item.HasWeapoinImbue(WoWInventorySlot.MainHand, "Windfury")),
                Spell.Cast("Flametongue Weapon", ret => !Item.HasWeapoinImbue(WoWInventorySlot.OffHand, "Flametongue"))
                );
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.EnhancementShaman)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateEnhancementShaman()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateFaceTargetBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Spell.WaitForCast(true),
                CreateEnhancementShamanPullBuffs(),
                Common.CreateAutoAttack(false),
                Totems.CreateSetTotems(),

                // Only call if we're missing more than 2 totems. 

                Spell.Cast("Call of the Elements", ret => Totems.TotemsInRangeOf(StyxWoW.Me.CurrentTarget) < 3),

                Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                //Self Heals, can be turned off
                Spell.Cast("Feral Spirit", ret => SingularSettings.Instance.Shaman.CastOn != CastOn.Never &&
                    !SingularSettings.Instance.Shaman.EnhancementHeal &&
                    StyxWoW.Me.HealthPercent <= 50),
                Spell.StopAndCast("Healing Surge", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent <= 50 && SingularSettings.Instance.Shaman.EnhancementHeal),

               //Aoe
                Spell.Cast("Chain Lightning",
                    ret => !StyxWoW.Me.CurrentTarget.IsNeutral &&
                        Clusters.GetClusterCount(StyxWoW.Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Chained, 10f) >= 2 &&
                        StyxWoW.Me.Auras["Maelstrom Weapon"].StackCount > 4),
                Spell.Cast("Fire Nova",
                    ret => Clusters.GetClusterCount(StyxWoW.Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 10f) >= 2 &&
                           StyxWoW.Me.CurrentTarget.HasMyAura("Flame Shock")),


                // Ensure Searing is nearby
                Spell.Cast("Searing Totem", ret => StyxWoW.Me.Totems.Count(t => t.WoWTotem == WoWTotem.Searing && t.Unit.Distance < 13) == 0),

                Spell.Cast("Stormstrike"),
                Spell.Cast("Lava Lash"),
                Spell.Cast("Lightning Bolt", ret => StyxWoW.Me.Auras["Maelstrom Weapon"].StackCount > 4),

                // Clip the last tick of FS if we can.
                Spell.Buff("Flame Shock", ret => StyxWoW.Me.HasAura("Unleash Flame") || !Styx.Logic.Combat.SpellManager.HasSpell("Unleash Elements")),

                Spell.Cast("Unleash Elements"),

                Spell.Cast("Earth Shock"),

                //User selects when to cast
                Spell.Cast("Feral Spirit", ret =>
                    SingularSettings.Instance.Shaman.CastOn == CastOn.All ||
                    SingularSettings.Instance.Shaman.CastOn == CastOn.Bosses && BossList.BossIds.Contains(StyxWoW.Me.CurrentTarget.Entry) ||
                    SingularSettings.Instance.Shaman.CastOn == CastOn.Players && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.IsHostile),

                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.CurrentTarget != StyxWoW.Me,
                    Movement.CreateMoveBehindTargetBehavior(1f)),
                    Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }
    }
}
