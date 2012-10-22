#region

using System;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

#endregion

namespace Singular.ClassSpecific.Druid
{
    public class Feral
    {
        private static DruidSettings DruidSettings
        {
            get { return SingularSettings.Instance.Druid; }
        }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        #region Common

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateFaceTargetBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Helpers.Common.CreateAutoAttack(false),
                new Decorator(ret => Me.Mounted,
                              Helpers.Common.CreateDismount("Pulling")),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !SpellManager.GlobalCooldown, 
                    new PrioritySelector(

                        //Shoot flying targets
                        new Decorator(
                            ret => Me.CurrentTarget.IsFlying,
                            new PrioritySelector(
                                Spell.Buff("Faerie Fire", ret => Me.CurrentTarget.Distance < 35),
                                Spell.Cast("Moonfire", ret => Me.CurrentTarget.Distance < 40),
                                Movement.CreateMoveToTargetBehavior(true, 27f)
                                )),

                        Spell.Buff("Prowl", ret => !Me.Combat ),
                        Spell.Cast("Pounce", ret => Me.HasAura("Prowl") && Me.CurrentTarget.IsWithinMeleeRange ),
                        Spell.Buff("Rake", ret => Me.CurrentTarget.IsWithinMeleeRange ),
                        Spell.Cast("Mangle", ret => Me.CurrentTarget.IsWithinMeleeRange )
                        )
                    ),

                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalPreCombatBuffs()
        {
            return new PrioritySelector();
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Cat Form"));
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        TimeToDeathExtension.CreateWriteDebugTimeToDeath(),

                        //Single target
                        Spell.Cast("Faerie Fire", ret =>!Me.CurrentTarget.HasAura("Weakened Armor", 3)),

#if NOT_YET
                        new Decorator(
                            ret => SpellManager.HasSpell("Dream of Cenarius"),
                            new PrioritySelector(
                                Spell.Cast("Healing Touch",
                                           ret => Me.HasAura("Predatory Swiftness") 
                                               && Me.ComboPoints >= 5 
                                               && !Me.HasAura("Dream of Cenarius", 2)),
                                Spell.Cast("Healing Touch",
                                           ret => Me.HasAura("Predatory Swiftness") 
                                               && Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalSeconds <= 1 
                                               && !Me.HasAura("Dream of Cenarius")),
                                Spell.BuffSelf("Nature's Swiftness"),
                                Spell.Cast("Healing Touch", ret => Me.HasAura("Nature's Swiftness"))
                                )
                            ),
#endif

                        Spell.Cast("Savage Roar",
                            ret => Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds <= 3
                                && (Me.ComboPoints > 0 || TalentManager.HasGlyph("Stampeding Roar"))),

                        Spell.BuffSelf("Tiger's Fury",
                                   ret => Spell.GetSpellCooldown("Tiger's Fury").TotalSeconds < 1 
                                       && Common.energy <= 35 
                                       && !Me.ActiveAuras.ContainsKey("Clearcasting")),

                        Spell.BuffSelf("Berserk",
                                   ret => Spell.GetSpellCooldown("Berserk").TotalSeconds < 1
                                       && (Me.HasAura("Tiger's Fury") || (Me.CurrentTarget.TimeToDeath() < 15 && Spell.GetSpellCooldown("Tiger's Fury").TotalSeconds > 6))),

                        Spell.Cast("Nature's Vigil", ret => Me.HasAura("Berserk")),

                        Spell.Cast("Incarnation", ret => Me.HasAura("Berserk")),

                        // bite if rip good for awhile or target dying soon
                        Spell.Cast("Ferocious Bite", 
                            ret => Me.ComboPoints >= 5
                                && Me.CurrentTarget.IsWithinMeleeRange
                                && Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds > 6 || Me.CurrentTarget.TimeToDeath() < 6),

                        // bite if rip expiring soon and we can refresh
                        Spell.Cast("Ferocious Bite", 
                            ret => Me.ComboPoints > 0
                                && Me.CurrentTarget.IsWithinMeleeRange
                                && Me.CurrentTarget.HealthPercent <= 25 
                                && (Me.CurrentTarget.HasAura("Rip") 
                                && Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 4 )),

                        Spell.Cast("Rip",
                            ret => Me.ComboPoints >= 5
                                && Me.CurrentTarget.IsWithinMeleeRange
                                && Me.CurrentTarget.TimeToDeath() >= 6 
                                && Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 6.0 
                                && Me.CurrentTarget.HealthPercent > 25),

                        Spell.Cast("Thrash",
                            ret => Me.ActiveAuras.ContainsKey("Clearcasting")
                                && Me.CurrentTarget.IsWithinMeleeRange
                                && Me.CurrentTarget.GetAuraTimeLeft("Thrash", true).TotalSeconds < 3),

#if DPS_POTION_CHECK
                        new Decorator(
                            ret =>
                            (SpellManager.HasSpell("Dream of Cenarius") && Me.ComboPoints >= 5 &&
                             Me.CurrentTarget.HealthPercent <= 25 &&
                             Me.HasAura("Dream of Cenarius")) ||
                            (!SpellManager.HasSpell("Dream of Cenarius") && Me.HasAura("Berserk") &&
                             Me.CurrentTarget.HealthPercent <= 25) ||
                            CalculateTimeToDeath(Me.CurrentTarget) <= 40,
                            Item.UseItem(76089) // Virmen's Bite
                            ),
#endif
                        Spell.Cast("Ravage",
                            ret => Me.CurrentTarget.HealthPercent > 80 && Me.CurrentTarget.IsWithinMeleeRange),

                        Spell.Buff("Rake",
                            ret => Me.CurrentTarget.TimeToDeath() > 6 && Me.CurrentTarget.IsWithinMeleeRange),

                        Spell.Cast("Shred", 
                            ret =>  Me.CurrentTarget.IsWithinMeleeRange
                                && (Me.CurrentTarget.MeIsSafelyBehind || (TalentManager.HasGlyph("Shred") && (Me.HasAnyAura("Tiger's Fury", "Berserk"))))),

                        Spell.Cast("Mangle", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        Spell.CastOnGround("Force of Nature", 
                            u => StyxWoW.Me.CurrentTarget.Location,
                            ret => StyxWoW.Me.CurrentTarget != null 
                                && StyxWoW.Me.CurrentTarget.Distance < 40
                                && SpellManager.HasSpell("Force of Nature"))
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

    }
}