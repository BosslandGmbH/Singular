using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using CommonBehaviors.Actions;


namespace Singular.ClassSpecific.Druid
{
    public class Feral
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

        [Spec(TalentSpec.FeralDruid)]
        [Spec(TalentSpec.FeralTankDruid)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Druid)]
        [Context(WoWContext.All)]
        public static Composite CreateFeralCombat()
        {
            return new PrioritySelector(
                new Decorator(
                    // We shall tank if we are assigned as dungeon/raid tank or the current tank is dead
                    ret => Group.Tank != null && (Group.Tank.IsMe || !Group.Tank.IsAlive),
                    CreateBearTankCombat()),
                CreateFeralCatCombat()
                );
        }

        #region Cat

        public static Composite CreateFeralCatCombat()
        {
            return new PrioritySelector(
                CreateFeralCatManualForms(),
                CreateFeralCatActualCombat());
        }

        public static Composite CreateFeralCatManualForms()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                // If we're in caster form, and not casting anything (tranq), then fucking switch to cat.
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Normal,
                    Spell.BuffSelf("Cat Form")),
                // We don't want to get stuck in Aquatic form. We won't be able to cast shit
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Aqua,
                    Spell.BuffSelf("Cat Form")),
                new Decorator(
                    ret => !Settings.ManualForms && StyxWoW.Me.Shapeshift != ShapeshiftForm.Cat,
                    Spell.BuffSelf("Cat Form")),
                //// If the user has manual forms enabled. Automatically switch to cat combat if they switch forms.
                new Decorator(
                    ret => Settings.ManualForms && StyxWoW.Me.Shapeshift == ShapeshiftForm.Bear,
                    new PrioritySelector(
                        CreateBearTankActualCombat(),
                        new ActionAlwaysSucceed())));
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
                    ret => StyxWoW.Me.ComboPoints == 5 &&
                            (StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).Seconds <= 4)),

                Spell.Cast("Rake", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).Seconds < 2),

                Spell.BuffSelf("Savage Roar"),

                //If we have both dot and buff up then a FB is a dps increase
                Spell.Cast("Ferocious Bite", ret =>
                    StyxWoW.Me.ComboPoints > 3 &&
                    StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).Seconds > 5 &&
                    StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).Seconds > 5),

                Spell.Cast("Ravage", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Stampede")),

                // shred does less damage than mangle.
                Spell.Cast("Shred", ret => !SpellManager.HasSpell("Mangle (Cat)") && StyxWoW.Me.ComboPoints < 1 && StyxWoW.Me.IsBehind(StyxWoW.Me.CurrentTarget)),

                Spell.Cast("Ferocious Bite", ret => StyxWoW.Me.ComboPoints > 3),

                Spell.Cast("Mangle (Cat)"),

                Movement.CreateMoveToMeleeBehavior(true)

                );
        }

        #endregion

        #region Bear

        public static Composite CreateBearTankCombat()
        {
            return new PrioritySelector(
                CreateBearTankManualForms(),
                CreateBearTankActualCombat());
        }

        private static Composite CreateBearTankManualForms()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                // If we're in caster form, and not casting anything (tranq), then fucking switch to bear.
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Normal,
                    Spell.BuffSelf("Bear Form")),
                new Decorator(
                    ret => !Settings.ManualForms && StyxWoW.Me.Shapeshift != ShapeshiftForm.Bear,
                    Spell.BuffSelf("Bear Form")),
                // If the user has manual forms enabled. Automatically switch to cat combat if they switch forms.
                new Decorator(
                    ret => Settings.ManualForms && StyxWoW.Me.Shapeshift == ShapeshiftForm.Cat,
                    new PrioritySelector(
                        Feral.CreateFeralCatActualCombat(),
                        new ActionAlwaysSucceed()))
                );
        }

        public static Composite CreateBearTankActualCombat()
        {
            TankManager.NeedTankTargeting = true;
            return new PrioritySelector(
                ctx => TankManager.Instance.FirstUnit ?? StyxWoW.Me.CurrentTarget,
                //((WoWUnit)ret)

                Spell.WaitForCast(),
                // If we're in caster form, and not casting anything (tranq), then fucking switch to bear.
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Normal,
                    Spell.BuffSelf("Bear Form")),
                // We don't want to get stuck in Aquatic form. We won't be able to cast shit
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Aqua,
                    Spell.BuffSelf("Bear Form")),
                new Decorator(
                    ret => !Settings.ManualForms && StyxWoW.Me.Shapeshift != ShapeshiftForm.Bear,
                    Spell.BuffSelf("Bear Form")),
                // If the user has manual forms enabled. Automatically switch to cat combat if they switch forms.
                new Decorator(
                    ret => Settings.ManualForms && StyxWoW.Me.Shapeshift == ShapeshiftForm.Cat,
                    new PrioritySelector(
                        Feral.CreateFeralCatActualCombat(),
                        new ActionAlwaysSucceed())),
                Safers.EnsureTarget(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => Settings.UseFeralChargeBear && ((WoWUnit)ret).Distance > 8f && ((WoWUnit)ret).Distance < 25f,
                    Spell.Cast("Feral Charge (Bear)", ret => ((WoWUnit)ret))),
                // Defensive CDs are hard to 'roll' from this type of logic, so we'll simply use them more as 'oh shit' buttons, than anything.
                // Barkskin should be kept on CD, regardless of what we're tanking
                Spell.BuffSelf("Barkskin", ret => StyxWoW.Me.HealthPercent < Settings.FeralBarkskin),
                // Since Enrage no longer makes us take additional damage, just keep it on CD. Its a rage boost, and coupled with King of the Jungle, a DPS boost for more threat.
                Spell.BuffSelf("Enrage"),
                // Only pop SI if we're taking a bunch of damage.
                Spell.BuffSelf("Survival Instincts", ret => StyxWoW.Me.HealthPercent < Settings.SurvivalInstinctsHealth),
                // We only want to pop FR < 30%. Users should not be able to change this value, as FR automatically pushes us to 30% hp.
                Spell.BuffSelf("Frenzied Regeneration", ret => StyxWoW.Me.HealthPercent < Settings.FrenziedRegenerationHealth),
                // Make sure we deal with interrupts...
                //Spell.Cast(80964 /*"Skull Bash (Bear)"*/, ret => (WoWUnit)ret, ret => ((WoWUnit)ret).IsCasting),
                Helpers.Common.CreateInterruptSpellCast(ret => ((WoWUnit)ret)),
                new Decorator(
                    ret => Targeting.GetAggroOnMeWithin(StyxWoW.Me.Location, 15f) > 2,
                    new PrioritySelector(
                        Spell.Cast("Berserk"),
                        Spell.Cast("Maul"),
                        Spell.Cast("Thrash"),
                        Spell.Cast("Swipe (Bear)"),
                        Spell.Cast("Mangle (Bear)")
                        )),
                // If we have 3+ units not targeting us, and are within 10yds, then pop our AOE taunt. (These are ones we have 'no' threat on, or don't hold solid threat on)
                Spell.Cast(
                    "Challenging Roar", ret => TankManager.Instance.NeedToTaunt.First(),
                    ret => SingularSettings.Instance.EnableTaunting && TankManager.Instance.NeedToTaunt.Count(u => u.Distance <= 10) >= 3),
                // If there's a unit that needs taunting, do it.
                Spell.Cast(
                    "Growl", ret => TankManager.Instance.NeedToTaunt.First(),
                    ret => SingularSettings.Instance.EnableTaunting && TankManager.Instance.NeedToTaunt.FirstOrDefault() != null),
                Spell.Cast("Pulverize", ret => ((WoWUnit)ret).HasAura("Lacerate", 3) && !StyxWoW.Me.HasAura("Pulverize")),

                Spell.Cast("Demoralizing Roar", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 10 && !u.HasDemoralizing())),

                Spell.Cast("Faerie Fire (Feral)", ret => !((WoWUnit)ret).HasSunders()),
                Spell.Cast("Mangle (Bear)"),
                // Maul is our rage dump... don't pop it unless we have to, or we still have > 2 targets.
                Spell.Cast(
                    "Maul",
                    ret =>
                    StyxWoW.Me.RagePercent > 60 || (Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 6) >= 2 && TalentManager.HasGlyph("Maul"))),
                Spell.Cast("Thrash", ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < 8 && u.IsCrowdControlled())),
                Spell.Cast("Lacerate"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
    }
}