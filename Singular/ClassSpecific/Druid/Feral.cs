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

#endregion

namespace Singular.ClassSpecific.Druid
{
    public class Feral
    {
        private static DruidSettings Settings
        {
            get { return SingularSettings.Instance.Druid; }
        }

        #region Common

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalPull()
        {
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                //face target
                Movement.CreateFaceTargetBehavior(),
                // LOS check
                Movement.CreateMoveToLosBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),
                //Dismount
                new Decorator(ret => StyxWoW.Me.Mounted,
                              Helpers.Common.CreateDismount("Pulling")),
                //Shoot flying targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Spell.Cast("Moonfire"),
                        Movement.CreateMoveToTargetBehavior(true, 27f)
                        )),
                Spell.Buff("Prowl"),
                Spell.Cast("Pounce"),
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
                //Ensure Target
                Safers.EnsureTarget(),
                //LOS check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),
                //Single target
                Spell.Cast("Faerie Fire",
                           ret =>
                           !StyxWoW.Me.CurrentTarget.HasAura("Weakened Armor") ||
                           (StyxWoW.Me.CurrentTarget.HasAura("Weakened Armor") &&
                            StyxWoW.Me.CurrentTarget.GetAuraByName("Weakened Armor").StackCount < 3)),
                Spell.Cast("Savage Roar", ret => !StyxWoW.Me.HasAura("Savage Roar")),
                //Only use this shit if we have got DoC
                new Decorator(
                    ret => SpellManager.HasSpell("Dream of Cenarius"),
                    new PrioritySelector(
                        Spell.Cast("Healing Touch",
                                   ret =>
                                   StyxWoW.Me.HasAura("Predatory Swiftness") && StyxWoW.Me.ComboPoints > 4 &&
                                   (!StyxWoW.Me.HasAura("Dream of Cenarius") ||
                                    (StyxWoW.Me.HasAura("Dream of Cenarius") &&
                                     StyxWoW.Me.GetAuraByName("Dream of Cenarius").StackCount < 2))),
                        Spell.Cast("Healing Touch",
                                   ret =>
                                   StyxWoW.Me.HasAura("Predatory Swiftness") &&
                                   StyxWoW.Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalSeconds <= 1 &&
                                   !StyxWoW.Me.HasAura("Dream of Cenarius")),
                        Spell.Cast("Healing Touch", ret => Common.prevSwift)
                        )),
                //TODO: Add Trinkets / Engeneering gloves here while Tiger's Fury is active
                Spell.Cast("Tiger's Fury",
                           ret =>
                           SpellManager.HasSpell("Tiger's Fury") &&
                           SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft.TotalSeconds < 1 && Common.energy <= 35 &&
                           !StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting")),
                Spell.Cast("Berserk",
                           ret =>
                           SpellManager.HasSpell("Berserk") &
                           SpellManager.Spells["Berserk"].CooldownTimeLeft.TotalSeconds < 1 &&
                           (StyxWoW.Me.HasAura("Tiger's Fury") ||
                            (CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) < 15 &&
                             ((SpellManager.HasSpell("Tiger's Fury") &&
                               SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft.TotalSeconds > 6) ||
                              !SpellManager.HasSpell("Tiger's Fury"))))),
                Spell.Cast("Nature's Vigil",
                           ret =>
                           SpellManager.HasSpell("Nature's Vigil") &&
                           SpellManager.Spells["Nature's Vigil"].CooldownTimeLeft.TotalSeconds < 1 &&
                           StyxWoW.Me.HasAura("Berserk")),
                Spell.Cast("Incarnation",
                           ret =>
                           SpellManager.HasSpell("Incarnation") &&
                           SpellManager.Spells["Incarnation"].CooldownTimeLeft.TotalSeconds < 1 &&
                           StyxWoW.Me.HasAura("Berserk")),
                //TODO: Usage of Racials!
                Spell.Cast("Ferocious Bite",
                           ret =>
                           StyxWoW.Me.ComboPoints >= 1 && StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds <= 2 &&
                           StyxWoW.Me.CurrentTarget.HealthPercent <= 25),
                Spell.Cast(106832,
                           ret =>
                           StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Thrash", true).TotalSeconds < 3 &&
                           !StyxWoW.Me.HasAura("Dream of Cenarius")),
                Spell.Cast("Savage Roar",
                           ret =>
                           StyxWoW.Me.GetAuraTimeLeft(127538, true).TotalSeconds <= 1 ||
                           (StyxWoW.Me.GetAuraTimeLeft(127538, true).TotalSeconds <= 3 &&
                            StyxWoW.Me.ComboPoints > 0) && StyxWoW.Me.CurrentTarget.HealthPercent < 25),
                //Only use this shit if we have got DoC
                new Decorator(
                    ret => SpellManager.HasSpell("Dream of Cenarius"),
                    Spell.Cast("Nature's Swiftness",
                               ret =>
                               !StyxWoW.Me.HasAura("Dream of Cenarius") && !StyxWoW.Me.HasAura("Predatory Swiftness") &&
                               StyxWoW.Me.ComboPoints >= 5 && StyxWoW.Me.CurrentTarget.HealthPercent <= 25)
                    ),
                new Decorator(
                    ret =>
                    (SpellManager.HasSpell("Dream of Cenarius") && StyxWoW.Me.ComboPoints >= 5 &&
                     StyxWoW.Me.CurrentTarget.HealthPercent <= 25 &&
                     StyxWoW.Me.HasAura("Dream of Cenarius")) ||
                    (!SpellManager.HasSpell("Dream of Cenarius") && StyxWoW.Me.HasAura("Berserk") &&
                     StyxWoW.Me.CurrentTarget.HealthPercent <= 25) ||
                    CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) <= 40,
                    Item.UseItem(76089)
                    ),
                Spell.Cast("Rip",
                           ret =>
                           StyxWoW.Me.ComboPoints >= 5 && StyxWoW.Me.HasAura("Virmen's Bite") &&
                           StyxWoW.Me.HasAura("Dream of Cenarius") && Common.RipMultiplier < Common.tick_multiplier &&
                           StyxWoW.Me.CurrentTarget.HealthPercent <= 25 &&
                           CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) > 30),
                Spell.Cast("Ferocious Bite",
                           ret =>
                           StyxWoW.Me.ComboPoints >= 5 && StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                           StyxWoW.Me.CurrentTarget.HealthPercent <= 25),
                Spell.Cast("Rip",
                           ret =>
                           StyxWoW.Me.ComboPoints >= 5 && CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) >= 6 &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 2.0 &&
                           StyxWoW.Me.HasAura("Dream of Cenarius")),
                Spell.Cast("Rip",
                           ret =>
                           StyxWoW.Me.ComboPoints >= 5 && CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) >= 6 &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 6.0 &&
                           StyxWoW.Me.HasAura("Dream of Cenarius") &&
                           Common.RipMultiplier <= Common.tick_multiplier & StyxWoW.Me.CurrentTarget.HealthPercent > 25),
                Spell.Cast("Savage Roar",
                           ret =>
                           StyxWoW.Me.GetAuraTimeLeft(127538, true).TotalSeconds <= 1 ||
                           (StyxWoW.Me.GetAuraTimeLeft(127538, true).TotalSeconds <= 3 &&
                            StyxWoW.Me.ComboPoints > 0)),
                //Only use this shit if we have got DoC
                new Decorator(
                    ret => SpellManager.HasSpell("Dream of Cenarius"),
                    Spell.Cast("Nature's Swiftness",
                               ret =>
                               !StyxWoW.Me.HasAura("Dream of Cenarius") && !StyxWoW.Me.HasAura("Predatory Swiftness") &&
                               StyxWoW.Me.ComboPoints >= 5 &&
                               StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 3 &&
                               (StyxWoW.Me.HasAura("Berserk") ||
                                (SpellManager.HasSpell("Tiger's Fury") &&
                                 StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds <=
                                 SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft.TotalSeconds) ||
                                !SpellManager.HasSpell("Tiger's Fury")) &&
                               StyxWoW.Me.CurrentTarget.HealthPercent > 25)
                    ),
                Spell.Cast("Rip",
                           ret =>
                           StyxWoW.Me.ComboPoints >= 5 && CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) >= 6 &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 2.0 &&
                           (StyxWoW.Me.HasAura("Berserk") ||
                            (SpellManager.HasSpell("Tiger's Fury") &&
                             StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds <=
                             SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft.TotalSeconds)
                            || !SpellManager.HasSpell("Tiger's Fury"))),
                Spell.Cast(106832,
                           ret =>
                           StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Thrash", true).TotalSeconds < 3),
                Spell.Cast("Ravage",
                           ret =>
                           (StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                            (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind)) &&
                           Common.ExtendedRip < 3 && StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds <= 4),
                new Decorator(
                    ret =>
                    !(StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                      (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind)),
                    new PrioritySelector(
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                   TalentManager.HasGlyph("Shred") &&
                                   (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951)),
                            Spell.Cast("Shred",
                                       ret =>
                                       Common.ExtendedRip < 3 && StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                                       StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds <= 4)
                            ),
                        new Decorator(
                            ret => !(StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                     TalentManager.HasGlyph("Shred") &&
                                     (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951))),
                            Spell.Cast("Mangle",
                                       ret =>
                                       Common.ExtendedRip < 3 && StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                                       StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds <= 4)
                            ))
                    ),
                Spell.Cast("Ferocious Bite",
                           ret =>
                           (CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) <= 4 && StyxWoW.Me.ComboPoints >= 5) ||
                           CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) <= 1),
                Spell.Cast("Savage Roar",
                           ret =>
                           StyxWoW.Me.GetAuraTimeLeft(127538, true).TotalSeconds <= 6 &&
                           StyxWoW.Me.ComboPoints >= 5 &&
                           (((StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds +
                              (8 - (Common.ExtendedRip * 2))) > 6 &&
                             (SpellManager.HasSpell("Soul of the Forest") || StyxWoW.Me.HasAura("Berserk"))) ||
                            (StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds +
                             (8 - (Common.ExtendedRip * 2))) > 10) && StyxWoW.Me.CurrentTarget.HasMyAura("Rip")),
                Spell.Cast("Ferocious Bite",
                           ret =>
                           StyxWoW.Me.ComboPoints >= 5 &&
                           (StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds +
                            (8 - (Common.ExtendedRip * 2))) > 6 && StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                           (SpellManager.HasSpell("Soul of the Forest") || StyxWoW.Me.HasAura("Berserk"))),
                Spell.Cast("Ferocious Bite",
                           ret =>
                           StyxWoW.Me.ComboPoints >= 5 &&
                           (StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds +
                            (8 - (Common.ExtendedRip * 2))) > 10 && StyxWoW.Me.CurrentTarget.HasMyAura("Rip")),
                Spell.Cast("Rake",
                           ret =>
                           CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) >= 8.5 &&
                           StyxWoW.Me.HasAura("Dream of Cenarius") && (Common.RipMultiplier < Common.tick_multiplier)),
                Spell.Cast("Rake",
                           ret =>
                           CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) >= 8.5 &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 3.0 &&
                           (StyxWoW.Me.HasAura("Berserk") ||
                            (SpellManager.HasSpell("Tiger's Fury") &&
                             (SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft.TotalSeconds + 0.8) >=
                             StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds) ||
                            !SpellManager.HasSpell("Tiger's Fury"))),
                Spell.Cast("Ravage",
                           ret =>
                           (StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                            (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind)) &&
                           StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting")),
                new Decorator(
                    ret =>
                    !(StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                      (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind)),
                    new PrioritySelector(
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                   TalentManager.HasGlyph("Shred") &&
                                   (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951)),
                            Spell.Cast("Shred",
                                       ret =>
                                       StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting"))
                            ),
                        new Decorator(
                            ret => !(StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                     TalentManager.HasGlyph("Shred") &&
                                     (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951))),
                            Spell.Cast("Mangle",
                                       ret =>
                                       StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting"))
                            ))
                    ),
                new Decorator(
                    ret =>
                    (StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                     (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind)),
                    new PrioritySelector(
                        Spell.Cast("Ravage",
                                   ret =>
                                   StyxWoW.Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalSeconds > 1 &&
                                   !(Common.energy +
                                     (Common.energyregen *
                                      (StyxWoW.Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalSeconds - 1)) <
                                     (4 - StyxWoW.Me.ComboPoints) * 20)),
                        Spell.Cast("Ravage",
                                   ret =>
                                   ((StyxWoW.Me.ComboPoints < 5 &&
                                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 3.0) ||
                                    (StyxWoW.Me.ComboPoints == 0 &&
                                     StyxWoW.Me.GetAuraTimeLeft(127538, true).TotalSeconds < 2))),
                        Spell.Cast("Ravage", ret => CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) <= 8.5)
                        )
                    ),
                new Decorator(
                    ret =>
                    !(StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                      (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind)),
                    new PrioritySelector(
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                   TalentManager.HasGlyph("Shred") &&
                                   (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951)),
                            Spell.Cast("Shred",
                                       ret =>
                                       StyxWoW.Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalSeconds > 1 &&
                                       !(Common.energy +
                                         (Common.energyregen *
                                          (StyxWoW.Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalSeconds - 1)) <
                                         (4 - StyxWoW.Me.ComboPoints) * 20))
                            ),
                        new Decorator(
                            ret => !(StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                     TalentManager.HasGlyph("Shred") &&
                                     (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951))),
                            Spell.Cast("Mangle",
                                       ret =>
                                       StyxWoW.Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalSeconds > 1 &&
                                       !(Common.energy +
                                         (Common.energyregen *
                                          (StyxWoW.Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalSeconds - 1)) <
                                         (4 - StyxWoW.Me.ComboPoints) * 20))
                            ))
                    ),
                new Decorator(
                    ret =>
                    !(StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                      (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind)),
                    new PrioritySelector(
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                   TalentManager.HasGlyph("Shred") &&
                                   (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951)),
                            Spell.Cast("Shred",
                                       ret =>
                                       ((StyxWoW.Me.ComboPoints < 5 &&
                                         StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 3.0) ||
                                        (StyxWoW.Me.ComboPoints == 0 &&
                                         StyxWoW.Me.GetAuraTimeLeft(127538, true).TotalSeconds < 2)))
                            ),
                        new Decorator(
                            ret => !(StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                     TalentManager.HasGlyph("Shred") &&
                                     (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951))),
                            Spell.Cast("Mangle",
                                       ret =>
                                       ((StyxWoW.Me.ComboPoints < 5 &&
                                         StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 3.0) ||
                                        (StyxWoW.Me.ComboPoints == 0 &&
                                         StyxWoW.Me.GetAuraTimeLeft(127538, true).TotalSeconds < 2)))
                            ))
                    ),
                new Decorator(
                    ret =>
                    !(StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                      (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind)),
                    new PrioritySelector(
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                   TalentManager.HasGlyph("Shred") &&
                                   (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951)),
                            Spell.Cast("Shred",
                                       ret =>
                                       (CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) <= 8.5))
                            ),
                        new Decorator(
                            ret => !(StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                     TalentManager.HasGlyph("Shred") &&
                                     (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951))),
                            Spell.Cast("Mangle",
                                       ret =>
                                       ((CalculateTimeToDeath(StyxWoW.Me.CurrentTarget) <= 8.5)))
                            ))
                    ),
                Spell.Cast(106832,
                           ret =>
                           StyxWoW.Me.ComboPoints >= 5 &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Thrash", true).TotalSeconds < 6 &&
                           (StyxWoW.Me.HasAura("Tiger's Fury") || StyxWoW.Me.HasAura("Berserk"))),
                Spell.Cast(106832,
                           ret =>
                           StyxWoW.Me.ComboPoints >= 5 &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Thrash", true).TotalSeconds < 6 &&
                           SpellManager.HasSpell("Tiger's Fury") &&
                           SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft.TotalSeconds < 3.0),
                Spell.Cast(106832,
                           ret =>
                           StyxWoW.Me.ComboPoints >= 5 &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Thrash", true).TotalSeconds < 6 &&
                           Common.energytime_to_max <= 1.0),
                new Decorator(
                    ret =>
                    !(StyxWoW.Me.ComboPoints >= 5 && !StyxWoW.Me.CurrentTarget.HasMyAura("Thrash") ||
                      StyxWoW.Me.CurrentTarget.HasMyAura("Thrash") &&
                      StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Thrash", true).TotalSeconds < 6) &&
                    (StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                     (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind)),
                    new PrioritySelector(
                        Spell.Cast("Ravage",
                                   ret => StyxWoW.Me.HasAura("Tiger's Fury") || StyxWoW.Me.HasAura("Berserk")),
                        Spell.Cast("Ravage",
                                   ret =>
                                   SpellManager.HasSpell("Tiger's Fury") &&
                                   SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft.TotalSeconds < 3.0),
                        Spell.Cast("Ravage", ret => Common.energytime_to_max <= 1.0)
                        )
                    ),
                new Decorator(
                    ret =>
                    !(StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                      (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind))
                    &&
                    !(StyxWoW.Me.ComboPoints >= 5 &&
                      StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Thrash", true).TotalSeconds < 6),
                    new PrioritySelector(
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                   TalentManager.HasGlyph("Shred") &&
                                   (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951)),
                            Spell.Cast("Shred",
                                       ret =>
                                       (StyxWoW.Me.HasAura("Tiger's Fury") || StyxWoW.Me.HasAura("Berserk")))
                            ),
                        new Decorator(
                            ret => !(StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                     TalentManager.HasGlyph("Shred") &&
                                     (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951))),
                            Spell.Cast("Mangle",
                                       ret =>
                                       (StyxWoW.Me.HasAura("Tiger's Fury") || StyxWoW.Me.HasAura("Berserk")))
                            ))
                    ),
                new Decorator(
                    ret =>
                    !(StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                      (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind))
                    &&
                    !(StyxWoW.Me.ComboPoints >= 5 &&
                      StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Thrash", true).TotalSeconds < 6),
                    new PrioritySelector(
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                   TalentManager.HasGlyph("Shred") &&
                                   (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951)),
                            Spell.Cast("Shred",
                                       ret =>
                                       (SpellManager.HasSpell("Tiger's Fury") &&
                                        SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft.TotalSeconds < 3.0))
                            ),
                        new Decorator(
                            ret => !(StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                     TalentManager.HasGlyph("Shred") &&
                                     (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951))),
                            Spell.Cast("Mangle",
                                       ret =>
                                       (SpellManager.HasSpell("Tiger's Fury") &&
                                        SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft.TotalSeconds < 3.0))
                            ))
                    ),
                new Decorator(
                    ret =>
                    !(StyxWoW.Me.HasAura("Incarnation: King of the Jungle") ||
                      (StyxWoW.Me.HasAura("Prowl") && StyxWoW.Me.CurrentTarget.MeIsBehind))
                    &&
                    !(StyxWoW.Me.ComboPoints >= 5 &&
                      StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Thrash", true).TotalSeconds < 6),
                    new PrioritySelector(
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                   TalentManager.HasGlyph("Shred") &&
                                   (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951)),
                            Spell.Cast("Shred",
                                       ret =>
                                       (Common.energytime_to_max <= 1.0))
                            ),
                        new Decorator(
                            ret => !(StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.IsShredBoss() ||
                                     TalentManager.HasGlyph("Shred") &&
                                     (StyxWoW.Me.HasAura(5217) || StyxWoW.Me.HasAura(106951))),
                            Spell.Cast("Mangle",
                                       ret =>
                                       (Common.energytime_to_max <= 1.0))
                            ))
                    ),
                Spell.CastOnGround("Force of Nature", u => StyxWoW.Me.CurrentTarget.Location,
                                   ret =>
                                   StyxWoW.Me.CurrentTarget != null && SpellManager.HasSpell("Force of Nature")),
                Movement.CreateMoveToMeleeBehavior(true)
                )
                ;
        }

        #region misc calculations

        private static uint _firstLife;
        private static uint _firstLifeMax;
        private static int _firstTime;
        private static uint _currentLife;
        private static int _currentTime;
        private static ulong _guid;

        public static long CalculateTimeToDeath(WoWUnit target)
        {
            if (StyxWoW.Me.CurrentTarget.IsTrainingDummy())
            {
                return 111;
            }

            if (target.CurrentHealth == 0 || target.IsDead || !target.IsValid || !target.IsAlive)
            {
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) is dead!", target.Name, target.Guid, target.Entry);
                return 0;
            }
            //Fill variables on new target or on target switch, this will loose all calculations from last target
            if (_guid != target.Guid || (_guid == target.Guid && target.CurrentHealth == _firstLifeMax))
            {
                _guid = target.Guid;
                _firstLife = target.CurrentHealth;
                _firstLifeMax = target.MaxHealth;
                _firstTime = ConvDate2Timestam(DateTime.Now);
                //Lets do a little trick and calculate with seconds / u know Timestamp from unix? we'll do so too
            }
            _currentLife = target.CurrentHealth;
            _currentTime = ConvDate2Timestam(DateTime.Now);
            int timeDiff = _currentTime - _firstTime;
            uint hpDiff = _firstLife - _currentLife;
            if (hpDiff > 0)
            {
                /*
                * Rule of three (Dreisatz):
                * If in a given timespan a certain value of damage is done, what timespan is needed to do 100% damage?
                * The longer the timespan the more precise the prediction
                * time_diff/hp_diff = x/first_life_max
                * x = time_diff*first_life_max/hp_diff
                */
                long fullTime = timeDiff * _firstLifeMax / hpDiff;
                long pastFirstTime = (_firstLifeMax - _firstLife) * timeDiff / hpDiff;
                long calcTime = _firstTime - pastFirstTime + fullTime - _currentTime;
                if (calcTime < 1) calcTime = 1;
                //calc_time is a int value for time to die (seconds) so there's no need to do SecondsToTime(calc_time)
                long timeToDie = calcTime;
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) dies in {3}, you are dpsing with {4} dps", target.Name, target.Guid, target.Entry, timeToDie, dps);
                return timeToDie;
            }
            if (hpDiff <= 0)
            {
                //unit was healed,resetting the initial values
                _guid = target.Guid;
                _firstLife = target.CurrentHealth;
                _firstLifeMax = target.MaxHealth;
                _firstTime = ConvDate2Timestam(DateTime.Now);
                //Lets do a little trick and calculate with seconds / u know Timestamp from unix? we'll do so too
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) was healed, resetting data.", target.Name, target.Guid, target.Entry);
                return -1;
            }
            if (_currentLife == _firstLifeMax)
            {
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) is at full health.", target.Name, target.Guid, target.Entry);
                return -1;
            }
            //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) no damage done, nothing to calculate.", target.Name, target.Guid, target.Entry);
            return -1;
        }

        private static int ConvDate2Timestam(DateTime time)
        {
            var date1 = new DateTime(1970, 1, 1); // Refernzdatum (festgelegt)
            DateTime date2 = time; // jetztiges Datum / Uhrzeit
            var ts = new TimeSpan(date2.Ticks - date1.Ticks); // das Delta ermitteln
            // Das Delta als gesammtzahl der sekunden ist der Timestamp
            return (Convert.ToInt32(ts.TotalSeconds));
        }

        #endregion
    }
}