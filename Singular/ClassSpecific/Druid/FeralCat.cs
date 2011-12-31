using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using CommonBehaviors.Actions;


namespace Singular.ClassSpecific.Druid
{
    public class FeralCat
    {
        private static DruidSettings Settings { get { return SingularSettings.Instance.Druid; } }

        private const int FERAL_T13_ITEM_SET_ID = 1058;

        private static int NumTier13Pieces
        {
            get
            {
                int
                count = StyxWoW.Me.Inventory.Equipped.Hands.ItemInfo.ItemSetId == FERAL_T13_ITEM_SET_ID ? 1 : 0;
                count += StyxWoW.Me.Inventory.Equipped.Legs.ItemInfo.ItemSetId == FERAL_T13_ITEM_SET_ID ? 1 : 0;
                count += StyxWoW.Me.Inventory.Equipped.Chest.ItemInfo.ItemSetId == FERAL_T13_ITEM_SET_ID ? 1 : 0;
                count += StyxWoW.Me.Inventory.Equipped.Shoulder.ItemInfo.ItemSetId == FERAL_T13_ITEM_SET_ID ? 1 : 0;
                count += StyxWoW.Me.Inventory.Equipped.Head.ItemInfo.ItemSetId == FERAL_T13_ITEM_SET_ID ? 1 : 0;
                return count;
            }
        }

        private static bool HasTeir13Bonus
        {
            get
            {
                return NumTier13Pieces >= 2;
            }
        }

        public static Composite CreateFeralCatManualForms()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                // If we're in caster form, and not casting anything (tranq), then fucking switch to cat.
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Normal,
                    Spell.BuffSelf("Cat Form")),
                new Decorator(
                    ret => !Settings.ManualForms && StyxWoW.Me.Shapeshift != ShapeshiftForm.Cat,
                    Spell.BuffSelf("Cat Form")),
                //// If the user has manual forms enabled. Automatically switch to cat combat if they switch forms.
                new Decorator(
                    ret => Settings.ManualForms && StyxWoW.Me.Shapeshift == ShapeshiftForm.Bear,
                    new PrioritySelector(
                        FeralBearTank.CreateBearTankActualCombat(),
                        new ActionAlwaysSucceed())));
        }

        [Spec(TalentSpec.FeralDruid)]
        [Spec(TalentSpec.FeralTankDruid)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Druid)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateFeralCatCombat()
        {
            return new PrioritySelector(
                CreateFeralCatManualForms(),
                CreateFeralCatActualCombat()
                );
        }

        public static Composite CreateFeralCatActualCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                new Decorator(ret => !StyxWoW.Me.IsInRaid && !StyxWoW.Me.IsInParty && SingularSettings.Instance.Druid.FeralHeal,
                    Resto.CreateRestoDruidHealOnlyBehavior(true)),

                //based on Ej
                //http://elitistjerks.com/f73/t127445-feral_cat_cataclysm_4_3_dragon_soul/#Rotation

                Spell.Cast(
                    "Feral Charge (Cat)",
                    ret => Settings.UseFeralChargeCat && StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 23), // these params often fail

                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Movement.CreateMoveBehindTargetBehavior(),

                //Keep up FF if its not present and target is boss 
                Spell.Cast("Faerie Fire (Feral)", ret => !StyxWoW.Me.CurrentTarget.HasSunders()),

                //Keep up bleed debuff 
                Spell.Cast("Mangle (Cat)", ret => !StyxWoW.Me.CurrentTarget.HasBleedDebuff()),

                //Tiger's Fury on CD (ignoring the gear section for now)
                Spell.BuffSelf("Tiger's Fury"),

                //AoE
                Spell.Cast("Swipe", ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 7f) >=
                       SingularSettings.Instance.Druid.SwipeCount),

                //Refresh Rip with FB depending on set bonus
                Spell.Cast("Ferocious Bite",
                    ret => StyxWoW.Me.ComboPoints > 3 &&
                           StyxWoW.Me.CurrentTarget.HealthPercent <= (HasTeir13Bonus ? 60 : 25) &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).Seconds <= 4 &&
                           StyxWoW.Me.CurrentTarget.HasMyAura("Rip")),

                //5cp rip & rake, we would rather refresh with bite if we have set bonus
                Spell.Cast("Rip",
                    ret => (StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.IsPlayer) && StyxWoW.Me.ComboPoints == 5 &&
                            (StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).Seconds <= 4)),

                Spell.Cast("Rake", ret => StyxWoW.Me.ComboPoints < 3 && StyxWoW.Me.CurrentTarget.HealthPercent > 20 && StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).Seconds < 2), // && (StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.IsPlayer)

                Spell.BuffSelf("Savage Roar"),

                //If we have both dot and buff up then a FB is a dps increase
                Spell.Cast("Ferocious Bite", ret =>
                    StyxWoW.Me.ComboPoints > 3 &&
                    StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).Seconds > 5 &&
                    StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).Seconds > 5),

                Spell.Cast("Ravage", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Stampede")),

                Spell.Cast("Shred", ret => StyxWoW.Me.ComboPoints < 1 && StyxWoW.Me.IsBehind(StyxWoW.Me.CurrentTarget)),

                Spell.Cast("Ferocious Bite", ret => StyxWoW.Me.ComboPoints > 3),

                Spell.Cast("Mangle (Cat)"),

                Movement.CreateMoveToMeleeBehavior(true)

                );
        }
    }
}