using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular.ClassSpecific.Shaman
{
    class Elemental
    {
        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Behavior(BehaviorType.PullBuffs)]
        //[Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateElementalPullBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Lightning Shield"),
                Spell.Cast("Flametongue Weapon", ret => !Item.HasWeapoinImbue(WoWInventorySlot.MainHand, "Flametongue"))
                //new LogMessage("Flametongue done!"),
                );
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateShamanBuffs()
        {
            return new PrioritySelector(
                Spell.Cast("Ghost Wolf", ret => !CharacterSettings.Instance.UseMount && StyxWoW.Me.Shapeshift == ShapeshiftForm.Normal),
                new Decorator(ret => Totems.NeedToRecallTotems,
                    new Action(ret => Totems.RecallTotems()))
                );
        }

        private static WoWSpell LavaBurst { get { return SpellManager.Spells["Lava Burst"]; } }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        [Priority(50)]
        public static Composite CreateElementalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                CreateElementalPullBuffs(),
                // Only call if we're missing more than 2 totems. 
                Totems.CreateSetTotems(3),
                Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.Cast("Thunderstorm", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 10) > 2),
                Spell.Cast("Thunderstorm", ret => StyxWoW.Me.ManaPercent < 40),

                // Ensure Searing is nearby
                Spell.Cast(
                    "Searing Totem", ret => StyxWoW.Me,
                    ret => StyxWoW.Me.Totems.Count(
                        t =>
                        t.Unit != null && t.WoWTotem == WoWTotem.Searing &&
                        t.Unit.Location.DistanceSqr(StyxWoW.Me.CurrentTarget.Location) < 35 * 35) == 0 &&
                           !StyxWoW.Me.Totems.Any(t => t.WoWTotem == WoWTotem.FireElemental)),
                // Pop the ele on bosses
                Spell.Cast("Fire Elemental Totem", ret => StyxWoW.Me, ret => StyxWoW.Me.CurrentTarget.IsBoss() && !StyxWoW.Me.Totems.Any(t => t.WoWTotem == WoWTotem.FireElemental)),

                // Don't pop ES if FS is going to fall off soon. Just hold on to it.
                Spell.Cast(
                    "Earth Shock",
                    ret => StyxWoW.Me.HasAura("Lightning Shield", 9) && StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 6),

                // Clip the last tick of FS if we can.
                Spell.Cast("Flame Shock", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds < 3),


                // Not sure why EM doesn't want to be cast. I'll have to debug this further.
                Spell.BuffSelf("Elemental Mastery"),

                // Pretty much no matter what it is, just use on CD
                Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                new Decorator(
                    ret => SingularSettings.Instance.Shaman.IncludeAoeRotation && Unit.UnfriendlyUnitsNearTarget.Count() > 2,
                    new PrioritySelector(
                // Spread shocks. Make sure we only spread it to ones we're facing (LazyRaider support with movement turned off)
                        Spell.Cast(
                            "Flame Shock",
                            ret => Unit.UnfriendlyUnitsNearTarget.First(u => !u.HasMyAura("Flame Shock") && StyxWoW.Me.IsSafelyFacing(u)),
                            ret => Unit.UnfriendlyUnitsNearTarget.Count(u => !u.HasMyAura("Flame Shock")) != 0),
                // Bomb them with novas
                        Spell.Cast("Fire Nova", ret => Unit.UnfriendlyUnitsNearTarget.Any(u => u.HasMyAura("Flame Shock"))),
                // CL for the fun of it. :)
                        Spell.Cast(
                            "Chain Lightning", ret => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget, ClusterType.Chained, 12),
                            ret => Unit.UnfriendlyUnitsNearTarget.Count() > 2)
                        )),

                //Movement.CreateEnsureMovementStoppedBehavior(),
                Spell.Cast("Lava Burst"),

                // Ignore this, its useless and a DPS loss. Its ont he GCD and gains nothing from our SP, crit, or any other modifiers. 
                //Spell.Cast("Rocket Barrage"),

                // So... ignore movement if we have the glyph (hence the negated HasGlyph, if we don't have it, we want to chekc movement, otherwise, ignore it.)
                Spell.Cast("Lightning Bolt", ret => !TalentManager.HasGlyph("Unleashed Lightning"), ret => StyxWoW.Me.CurrentTarget, ret => true),
                Spell.Cast("Unleash Elements", ret => Item.HasWeapoinImbue(WoWInventorySlot.MainHand, "Flametongue") && StyxWoW.Me.IsMoving),

                Movement.CreateMoveToTargetBehavior(true, 38f)
                );
        }
    }
}
