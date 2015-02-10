using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Frost
    {
        private const int KillingMachine = Common.KillingMachine;

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DeathKnightSettings DeathKnightSettings { get { return SingularSettings.Instance.DeathKnight(); } }

        static uint runic_power { get { return StyxWoW.Me.CurrentRunicPower; } }
        static int blood { get { return Common.BloodRuneSlotsActive; } }
        static int frost { get { return Common.FrostRuneSlotsActive; } }
        static int unholy { get { return Common.UnholyRuneSlotsActive; } }
        static int death { get { return Common.DeathRuneSlotsActive; } }

        #region Normal Rotations

        private static List<WoWUnit> _nearbyUnfriendlyUnits;

        private static bool IsDualWielding
        {
            get { return Me.Inventory.Equipped.MainHand != null && Me.Inventory.Equipped.OffHand != null; }
        }

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Normal)]
        public static Composite CreateDeathKnightFrostNormalCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateDeathKnightPullMore(),

                        Common.CreateGetOverHereBehavior(),

                        Common.CreateDarkSuccorBehavior(),

                        Common.CreateSoulReaperHasteBuffBehavior(),

                        Common.CreateDarkSimulacrumBehavior(),

                        // Cooldowns
                        Spell.BuffSelf("Pillar of Frost", req => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange),

                        // Start AoE section
                        new PrioritySelector(
                            ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(12f).ToList(),
                            new Decorator(
                                ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count() >= DeathKnightSettings.DeathAndDecayCount,
                                new PrioritySelector(
                                    // Spell.Cast("Gorefiend's Grasp"),
                                    Spell.Cast("Remorseless Winter"),
                                    CreateFrostAoeBehavior(),
                                    Movement.CreateMoveToMeleeBehavior(true)
                                    )
                                )
                            ),

                        // *** Dual Weld Single Target Priority
                        new Decorator(
                            ctx => IsDualWielding,
                            CreateFrostSingleTargetDW()
                            ),

                        // *** 2 Hand Single Target Priority
                        new Decorator(
                            ctx => !IsDualWielding,
                            CreateFrostSingleTarget2H()
                            ),

                        // *** 3 Lowbie Cast what we have Priority
                        new Decorator(
                            ret => !SpellManager.HasSpell("Obliterate"),
                            new PrioritySelector(
                                Spell.Buff(
                                    sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", 
                                    true, 
                                    on => Me.CurrentTarget, 
                                    req => true, 
                                    "Frost Fever"
                                    ),
                                Spell.Buff(
                                    "Plague Strike", 
                                    true, 
                                    on => Me.CurrentTarget, 
                                    req => true, 
                                    "Blood Plague"
                                    ),
                                Spell.Cast("Death Strike", ret => Me.HealthPercent < 90),
                                Spell.Cast("Frost Strike"),
                                Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch"),
                                Spell.Cast("Plague Strike")
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightFrostPvPCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateDarkSuccorBehavior(),

                        Common.CreateGetOverHereBehavior(),

                        Common.CreateSoulReaperHasteBuffBehavior(),

                        Common.CreateDarkSimulacrumBehavior(),

                        Common.CreateSoulReaperHasteBuffBehavior(),

                        // Cooldowns
                        Spell.BuffSelf("Pillar of Frost", req => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange),
                
                        // Start AoE section
                        new PrioritySelector(
                            ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(12f).ToList(),
                            new Decorator(
                                ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count() >= DeathKnightSettings.DeathAndDecayCount,
                                new PrioritySelector(
                                    Spell.Cast("Gorefiend's Grasp"),
                                    Spell.Cast("Remorseless Winter")
                                    )
                                )
                            ),

                        // *** Dual Weld Single Target Priority
                        new Decorator(ctx => IsDualWielding,
                                      new PrioritySelector(
                                          // Execute
                                          Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),

                                          // Diseases
                                          Common.CreateApplyDiseases(),

                                          // Killing Machine
                                          Spell.Cast("Frost Strike",
                                                     ret =>
                                                     !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                                     Me.HasAura(KillingMachine)),
                                          Spell.Cast("Obliterate",
                                                     ret =>
                                                     Me.HasAura(KillingMachine) && Common.UnholyRuneSlotsActive == 2),

                                          // RP Capped
                                          Spell.Cast("Frost Strike",
                                                     ret =>
                                                     !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                                     NeedToDumpRunicPower ),
                                          // Rime Proc
                                          Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.HasAura("Freezing Fog")),
                                          // both Unholy Runes are off cooldown
                                          Spell.Cast("Obliterate", ret => Me.UnholyRuneCount == 2),
                                          Spell.Cast("Frost Strike"),
                                          Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch")
                                          )),
                        // *** 2 Hand Single Target Priority
                        new Decorator(ctx => !IsDualWielding,
                                      new PrioritySelector(
                                          // Execute
                                          Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),

                                          // Diseases
                                          Common.CreateApplyDiseases(),

                                          // Killing Machine
                                          Spell.Cast("Obliterate", ret => Me.HasAura(KillingMachine)),

                                          // RP Capped
                                          Spell.Cast("Frost Strike",
                                                     ret =>
                                                     !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                                     NeedToDumpRunicPower ),
                                          // Rime Proc
                                          Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.HasAura("Freezing Fog")),

                                          Spell.Cast("Obliterate"),
                                          Spell.Cast("Frost Strike")
                                          ))
                        )
                    ),

                Movement.CreateMoveToMeleeTightBehavior(true)
                );
        }

        #endregion

        #region Instance Rotations

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Instances)]
        public static Composite CreateDeathKnightFrostInstanceCombat()
        {
            if (Me.Level >= 100)
                return CreateDeathKnightInstanceCombatSimc();

            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Movement.CreateMoveToMeleeTightBehavior(true),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        // Cooldowns
                        Spell.BuffSelf("Pillar of Frost", req => Me.GotTarget() && Me.CurrentTarget.IsWithinMeleeRange ),

                        // Start AoE section
                        new PrioritySelector(
                            ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(12f).ToList(),
                            new Decorator(
                                ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count() >= DeathKnightSettings.DeathAndDecayCount,
                                new PrioritySelector(
                                    // Spell.Cast("Gorefiend's Grasp", ret => Group.Tanks.FirstOrDefault()),
                                    CreateFrostAoeBehavior(),
                                    Movement.CreateMoveToMeleeBehavior(true)
                                    )
                                )
                            ),

                        // *** Dual Weld Single Target Priority
                        new Decorator(
                            ctx => IsDualWielding,
                            CreateFrostSingleTargetDW()
                            ),

                        // *** 2 Hand Single Target Priority
                        new Decorator(
                            ctx => !IsDualWielding,
                            CreateFrostSingleTarget2H()
                            )

                        )
                    )

                );
        }


        private static Composite CreateFrostSingleTarget2H()
        {
            return new PrioritySelector(

                // Soul Reaper when target below 35%
                Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),

                // Defile Ground around target
                Spell.CastOnGround("Defile", on => Me.CurrentTarget, req => Spell.UseAOE && Me.GotTarget() && !Me.CurrentTarget.IsMoving, false),

                // Obliterate when Killing Machine is procced and both diseases are on the target
                Spell.Cast("Obliterate", req => Me.HasAura(KillingMachine) && Me.CurrentTarget.HasAura("Frost Fever") && Me.CurrentTarget.HasAura("Blood Plague")),

                // Diseases
                Common.CreateApplyDiseases(),

                // Obliterate When any runes are capped
                Spell.Cast("Obliterate", req => Common.BloodRuneSlotsActive >= 2 || Common.FrostRuneSlotsActive >= 2 || Common.UnholyRuneSlotsActive >= 2),

                // Frost Strike if RP capped
                Spell.Cast("Frost Strike", ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.CurrentRunicPower >= 89 ),

                // Howling Blast if Rime procced
                Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", mov => false, on => Me.CurrentTarget, req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.HasAura("Freezing Fog")),

                // Frost Strike
                Spell.Cast("Frost Strike"),

                // Obliterate
                Spell.Cast("Obliterate"),

                // Plague Leech
                Spell.Cast("Plague Leech", req => Common.CanCastPlagueLeech)
                );

        }

        private static Composite CreateFrostSingleTargetDW()
        {           
            return new PrioritySelector(

                // Blood Tap if you have 11 or more stacks of blood charge
                // in CombatBuffs

                // Frost Strike if Killing Machine is procced, or if RP is 89 or higher
                Spell.Cast("Frost Strike",
                    ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)
                        && (Me.CurrentRunicPower >= 89 || Me.HasAura(KillingMachine))),

                // Howling Blastwith both frost or both death off cooldown
                Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", mov => false, on => Me.CurrentTarget, req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && (Common.FrostRuneSlotsActive == 2 || Me.DeathRuneCount >= 2)),

                // Soul Reaper when target below 35%
                Spell.Cast("Soul Reaper", req => Me.CurrentTarget.HealthPercent < 35),

                // Plague Strike if one Unholy Rune is off cooldown and blood plague is down/nearly down
                Spell.Cast("Plague Strike", req => Common.UnholyRuneSlotsActive == 1 && Me.CurrentTarget.HasAuraExpired("Blood Plague")),

                // Howling Blast if Rime procced
                Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", mov => false, on => Me.CurrentTarget, req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.HasAura("Freezing Fog")),

                // Frost Strikeif RP is 77 or higher
                Spell.Cast("Frost Strike", req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.CurrentRunicPower >= 77),

                // Obliterate when 1 or more Unholy Runes are off cooldown and killing machine is down
                Spell.Cast("Obliterate", req => !Me.HasAura(KillingMachine) && Common.UnholyRuneSlotsActive >= 1),

                // Howling Blast
                Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", mov => false, on => Me.CurrentTarget, req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),

                // Blood Tap
                Spell.BuffSelf("Blood Tap", req => Common.NeedBloodTap()),

                // Frost Strikeif RP is 40 or higher
                Spell.Cast("Frost Strike", req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.CurrentRunicPower >= 40),

                // Plague Leech
                Spell.Cast("Plague Leech", req => Common.CanCastPlagueLeech),

                // Empower Rune Weapon
                Spell.BuffSelf("Empower Rune Weapon")
                );
        }

        #endregion


        // elitist jerks aoe priority (with addition of spreading diseases, which is mentioned but not specified)
        // .. note: only checking for blood plague in a few cases as frost fever 
        // .. should take care of itself with Howling Blast
        private static Composite CreateFrostAoeBehavior()
        {
            return new PrioritySelector(
                Spell.Cast("Remorseless Winter"),
                Spell.Cast("Soul Reaper", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 35 && u.IsWithinMeleeRange && Me.IsSafelyFacing(u))),

                // aoe aware disease apply - only checking current target because of ability to spread
                Spell.Cast("Unholy Blight", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < 10 && u.HasAuraExpired("Blood Plague"))),
                Spell.Cast("Howling Blast", ret => Me.CurrentTarget.HasAuraExpired("Frost Fever")),
                Spell.Cast("Outbreak", ret => Me.CurrentTarget.HasAuraExpired("Blood Plague")),   // only care about blood plague for this one
                Spell.Cast("Plague Strike", ret => Me.CurrentTarget.HasAuraExpired("Blood Plague")),

                // spread disease
                new Throttle( 2, 
                    Spell.Cast("Blood Boil",
                        ret => Unit.UnfriendlyUnitsNearTarget(10).Any(u => u.HasAuraExpired("Blood Plague"))
                            && Unit.UnfriendlyUnitsNearTarget(10).Any(u => !u.HasAuraExpired("Blood Plague")))
                    ),

                // damage
                Spell.CastOnGround("Death and Decay", on => Me.CurrentTarget, req => Spell.UseAOE && Me.UnholyRuneCount >= 1, false),
                Spell.Cast("Howling Blast"),
                Spell.Cast("Plague Strike", req => Me.UnholyRuneCount >= 2),
                Spell.Cast("Frost Strike", ret => NeedToDumpRunicPower),
                Spell.Cast("Obliterate", ret => !IsDualWielding && Me.HasAura(KillingMachine)),
                Spell.Cast("Plague Leech", req => Common.DeathRuneSlotsActive == 0 && (Common.BloodRuneSlotsActive == 0 || Common.FrostRuneSlotsActive == 0 || Common.UnholyRuneSlotsActive == 0))
                );
        }

        private static bool NeedToDumpRunicPower
        {
            get
            {
                return Me.CurrentRunicPower >= 76;
            }
        }

        /// <summary>
        /// instance priority as defined by SimulationCraft default profile for 1H and 2H
        /// </summary>
        /// <returns></returns>
        private static Composite CreateDeathKnightInstanceCombatSimc()
        {

            Generic.SuppressGenericRacialBehavior = true;

            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        new Action(r =>
                        {
                            Common.scenario.Update(StyxWoW.Me.CurrentTarget);
                            return RunStatus.Failure;
                        }),

                        Helpers.Common.CreateInterruptBehavior(),

                        // *** Dual Weld Single Target Priority
                        new Decorator(
                            ctx => IsDualWielding,
                            CreateFrostSingleTarget1HSimc()
                            ),

                        // *** 2 Hand Single Target Priority
                        new Decorator(
                            ctx => !IsDualWielding,
                            CreateFrostSingleTarget2HSimc()
                            )

                        )
                    )

                );
        }

        private static Composite CreateFrostSingleTarget1HSimc()
        {
            return new PrioritySelector(
                // # Executed every time the actor is available.
                // 
                // actions=auto_attack
                //  ... handled by Ensure
                // actions+=/deaths_advance,if=movement_remains>2
                // actions+=/antimagic_shell,damage=100000
                // actions+=/pillar_of_frost
                // actions+=/potion,name=draenic_strength,if=target.time_to_die<=30|(target.time_to_die<=60&&buff.pillar_of_frost_up)
                // actions+=/empower_rune_weapon,if=target.time_to_die<=60&&buff.potion_up
                // actions+=/blood_fury
                Spell.BuffSelfAndWait("Blood Fury"),
                // actions+=/berserking
                Spell.BuffSelfAndWait("Berserking"),
                // actions+=/arcane_torrent
                Spell.BuffSelfAndWait("Arcane Torrent"),
                // actions+=/run_action_list,name=aoe,if=active_enemies>=3
                new Decorator(
                    req => Common.scenario.MobCount >= 3,
                    new PrioritySelector(
                        // actions.aoe=unholy_blight
                        Spell.BuffSelf("Unholy Blight"),
                        // actions.aoe+=/blood_boil,if=dot.blood_plague.ticking&&(!talent.unholy_blight_enabled|cooldown.unholy_blight_remains<49),line_cd=28
                        new Throttle(
                            28,
                            Spell.Cast("Blood Boil", req => dot.blood_plague_ticking&&(!talent.unholy_blight_enabled||cooldown.unholy_blight_remains<49))
                            ),
                        // actions.aoe+=/defile
                        Spell.CastOnGround("Defile", on => Me.CurrentTarget, req => true),
                        // actions.aoe+=/breath_of_sindragosa,if=runic_power>75
                        Spell.Buff("Breath of Sindragosa", req => runic_power > 75 && !Me.HasAura("Breath of Sindragosa")),
                        // actions.aoe+=/run_action_list,name=bos_aoe,if=dot.breath_of_sindragosa.ticking
                        new Decorator(
                            req => dot.breath_of_sindragosa_ticking,
                            CreateFrostSingleTarget1H_bos_aoe_Simc()
                            ),
                        // actions.aoe+=/howling_blast
                        Spell.Cast("Howling Blast"),
                        // actions.aoe+=/blood_tap,if=buff.blood_charge_stack>10
                        Spell.Cast("Blood Tap", req => buff.blood_charge_stack>10),
                        // actions.aoe+=/frost_strike,if=runic_power>88
                        Spell.Cast("Frost Strike", req => runic_power>88),
                        // actions.aoe+=/death_and_decay,if=unholy=1
                        Spell.CastOnGround("Death and Decay", on => Me.CurrentTarget, req => unholy==1),
                        // actions.aoe+=/plague_strike,if=unholy=2
                        Spell.Buff("Plague Strike", req => unholy == 2),
                        // actions.aoe+=/blood_tap
                        Spell.Cast("Blood Tap"),
                        // actions.aoe+=/frost_strike,if=!talent.breath_of_sindragosa_enabled|cooldown.breath_of_sindragosa_remains>=10
                        Spell.Cast("Frost Strike", req => !talent.breath_of_sindragosa_enabled|cooldown.breath_of_sindragosa_remains>=10),
                        // actions.aoe+=/plague_leech
                        Spell.Buff("Plague Leech"),
                        // actions.aoe+=/plague_strike,if=unholy=1
                        Spell.Buff("Plague Strike", req => unholy==1),
                        // actions.aoe+=/empower_rune_weapon
                        Spell.BuffSelf("Empower Rune Weapon")
                        )
                    ),

                // actions+=/run_action_list,name=single_target,if=active_enemies<3
                new Decorator(
                    req => Common.scenario.MobCount < 3,
                    new PrioritySelector(
                        // 
                        // actions.single_target=blood_tap,if=buff.blood_charge_stack>10&&(runic_power>76|(runic_power>=20&&buff.killing_machine_react))
                        Spell.Cast("Blood Tap", req => buff.blood_charge_stack>10&&(runic_power>76|(runic_power>=20&&buff.killing_machine_react))),
                        // actions.single_target+=/soul_reaper,if=target.health_pct-3*(target.health_pct%target.time_to_die)<=35
                        Spell.Cast("Soul Reaper", req => target.health_pct-3*(target.health_pct%target.time_to_die)<=35),
                        // actions.single_target+=/blood_tap,if=(target.health_pct-3*(target.health_pct%target.time_to_die)<=35&&cooldown.soul_reaper_remains=0)
                        Spell.Cast("Blood Tap", req => (target.health_pct-3*(target.health_pct%target.time_to_die)<=35&&cooldown.soul_reaper_remains==0)),
                        // actions.single_target+=/breath_of_sindragosa,if=runic_power>75
                        Spell.Buff("Breath of Sindragosa", req => runic_power > 75 && !Me.HasAura("Breath of Sindragosa")),
                        // actions.single_target+=/run_action_list,name=bos_st,if=dot.breath_of_sindragosa.ticking
                        new Decorator(
                            req => dot.breath_of_sindragosa_ticking,
                            CreateFrostSingleTarget1H_bos_st_Simc()
                            ),
                        // actions.single_target+=/defile
                        Spell.CastOnGround("Defile", on => Me.CurrentTarget, req => true),
                        // actions.single_target+=/blood_tap,if=talent.defile_enabled&&cooldown.defile_remains==0
                        Spell.Cast("Blood Tap", req => talent.defile_enabled&&cooldown.defile_remains==0),
                        // actions.single_target+=/howling_blast,if=talent.breath_of_sindragosa_enabled&&cooldown.breath_of_sindragosa_remains<7&&runic_power<88
                        Spell.Cast("Howling Blast", req => talent.breath_of_sindragosa_enabled&&cooldown.breath_of_sindragosa_remains<7&&runic_power<88),
                        // actions.single_target+=/obliterate,if=talent.breath_of_sindragosa_enabled&&cooldown.breath_of_sindragosa_remains<3&&runic_power<76
                        Spell.Buff("Breath of Sindragosa", req => talent.breath_of_sindragosa_enabled&&cooldown.breath_of_sindragosa_remains<3&&runic_power<76),
                        // actions.single_target+=/frost_strike,if=buff.killing_machine_react||runic_power>88
                        Spell.Cast("Frost Strike", req => buff.killing_machine_react||runic_power>88),
                        // actions.single_target+=/frost_strike,if=cooldown.antimagic_shell_remains<1&&runic_power>=50&&!buff.antimagic_shell_up
                        Spell.Cast("Anti-magic Shell", req => cooldown.antimagic_shell_remains<1&&runic_power>=50&&!buff.antimagic_shell_up),
                        // actions.single_target+=/howling_blast,if=death>1||frost>1
                        Spell.Cast("Howling Blast", req => death>1||frost>1),
                        // actions.single_target+=/unholy_blight,if=!disease_ticking
                        Spell.Cast("Unholy Blight", req => !disease.ticking),
                        // actions.single_target+=/howling_blast,if=!talent.necrotic_plague_enabled&&!dot.frost_fever_ticking
                        Spell.Cast("Howling Blast", req => !talent.necrotic_plague_enabled&&!dot.frost_fever_ticking),
                        // actions.single_target+=/howling_blast,if=talent.necrotic_plague_enabled&&!dot.necrotic_plague_ticking
                        Spell.Cast("Howling Blast", req => talent.necrotic_plague_enabled&&!dot.necrotic_plague_ticking),
                        // actions.single_target+=/plague_strike,if=!talent.necrotic_plague_enabled&&!dot.blood_plague_ticking&&unholy>0
                        Spell.Buff("Plague Strike", req => !talent.necrotic_plague_enabled && !dot.blood_plague_ticking && unholy > 0),
                        // actions.single_target+=/howling_blast,if=buff.rime_react
                        Spell.Cast("Howling Blast", req => buff.rime_react),
                        // actions.single_target+=/frost_strike,if=set_bonus.tier17_2pc=1&&(runic_power>=50&&(cooldown.pillar_of_frost_remains<5))
                        Spell.Cast("Frost Strike", req => set_bonus.tier17_2pc==1&&(runic_power>=50&&(cooldown.pillar_of_frost_remains<5))),
                        // actions.single_target+=/frost_strike,if=runic_power>76
                        Spell.Cast("Frost Strike", req => runic_power>76),
                        // actions.single_target+=/obliterate,if=unholy>0&&!buff.killing_machine_react
                        Spell.Cast("Obliterate", req => unholy>0&&!buff.killing_machine_react),
                        // actions.single_target+=/howling_blast,if=!(target.health_pct-3*(target.health_pct%target.time_to_die)<=35&&cooldown.soul_reaper_remains<3)||death+frost>=2
                        Spell.Cast("Howling Blast", req => !(target.health_pct-3*(target.health_pct%target.time_to_die)<=35&&cooldown.soul_reaper_remains<3)||death+frost>=2),
                        // actions.single_target+=/blood_tap
                        Spell.Cast("Blood Tap"),
                        // actions.single_target+=/plague_leech
                        Spell.Cast("Plague Leech"),
                        // actions.single_target+=/empower_rune_weapon
                        Spell.BuffSelf("Empower Rune Weapon"),
                        // 
                        new ActionAlwaysFail()
                        )
                    )
                );
        }

        private static Composite CreateFrostSingleTarget1H_bos_aoe_Simc()
        {
            return new PrioritySelector(
                // actions.bos_aoe=howling_blast
                Spell.Cast("Howling Blast"),
                // actions.bos_aoe+=/blood_tap,if=buff.blood_charge_stack>10
                Spell.Cast("Blood Tap", req => buff.blood_charge_stack>10),
                // actions.bos_aoe+=/death_and_decay,if=unholy==1
                Spell.Cast("Death and Decay", req => unholy==1),
                // actions.bos_aoe+=/plague_strike,if=unholy==2
                Spell.Buff("Plague Strike", req => unholy == 2),
                // actions.bos_aoe+=/blood_tap
                Spell.Cast("Blood Tap"),
                // actions.bos_aoe+=/plague_leech
                Spell.Cast("Plague Leech"),
                // actions.bos_aoe+=/plague_strike,if=unholy==1
                Spell.Buff("Plague Strike", req => unholy == 1),
                // actions.bos_aoe+=/empower_rune_weapon
                Spell.BuffSelf("Empower Rune Weapon"),
                // 
                new ActionAlwaysFail()
                );
        }

        private static Composite CreateFrostSingleTarget1H_bos_st_Simc()
        {
            return new PrioritySelector(
                // actions.bos_st=obliterate,if=buff.killing_machine_react
                Spell.Cast("Obliterate", req => buff.killing_machine_react),
                // actions.bos_st+=/blood_tap,if=buff.killing_machine_react&&&buff.blood_charge_stack>=5
                Spell.Cast("Blood Tap", req => buff.killing_machine_react&&buff.blood_charge_stack>=5),
                // actions.bos_st+=/plague_leech,if=buff.killing_machine_react
                Spell.Buff("Plague Leech", req => buff.killing_machine_react),
                // actions.bos_st+=/howling_blast,if=runic_power<88
                Spell.Cast("Howling Blast", req => runic_power<88),
                // actions.bos_st+=/obliterate,if=unholy>0&&&runic_power<76
                Spell.Cast("Obliterate", req => unholy>0&&runic_power<76),
                // actions.bos_st+=/blood_tap,if=buff.blood_charge_stack>=5
                Spell.Cast("Blood Tap", req => buff.blood_charge_stack>=5),
                // actions.bos_st+=/plague_leech
                Spell.Buff("Plague Leech"),
                // actions.bos_st+=/empower_rune_weapon
                Spell.BuffSelf("Empower Rune Weapon"),

                new ActionAlwaysFail()
                );
        }

        private static Composite CreateFrostSingleTarget2HSimc()
        {
            return new PrioritySelector(

                // # Executed every time the actor is available.
                // 
                // actions==auto_attack
                // actions+=/deaths_advance,if=movement_remains>2
                // actions+=/antimagic_shell,damage==100000
                // actions+=/pillar_of_frost
                // actions+=/potion,name==draenic_strength,if=target.time_to_die<==30||(target.time_to_die<==60&&buff.pillar_of_frost.up)
                // actions+=/empower_rune_weapon,if=target.time_to_die<==60&&buff.potion.up
                // actions+=/blood_fury
                // actions+=/berserking
                // actions+=/arcane_torrent
                // actions+=/run_action_list,name==aoe,if=active_enemies>=4
                // actions+=/run_action_list,name==single_target,if=active_enemies<4
                
                new Decorator(
                    req => Common.scenario.MobCount >= 4,
                    new PrioritySelector(
                        // 
                        // actions.aoe==unholy_blight
                        Spell.BuffSelf("Unholy Blight"),
                        // actions.aoe+=/blood_boil,if=dot.blood_plague_ticking&&(!talent.unholy_blight_enabled||cooldown.unholy_blight_remains<49),line_cd==28
                        new Throttle(
                            28,
                            Spell.Cast("Blood Boil", req => dot.blood_plague_ticking&&(!talent.unholy_blight_enabled||cooldown.unholy_blight_remains<49))
                            ),
                        // actions.aoe+=/defile
                        Spell.CastOnGround("Defile", on => Me.CurrentTarget, req => true),
                        // actions.aoe+=/breath_of_sindragosa,if=runic_power>75
                        Spell.Cast("Breath of Sindragosa", req => runic_power>75),
                        // actions.aoe+=/run_action_list,name==bos_aoe,if=dot.breath_of_sindragosa_ticking
                        new Decorator(
                            req => dot.breath_of_sindragosa_ticking,
                            CreateFrost2H_bos_aoe()
                            ),
                        // actions.aoe+=/howling_blast
                        Spell.Cast("Howling Blast"),
                        // actions.aoe+=/blood_tap,if=buff.blood_charge_stack>10
                        Spell.Cast("Blood Tap", req => buff.blood_charge_stack>10),
                        // actions.aoe+=/frost_strike,if=runic_power>88
                        Spell.Cast("Frost Strike", req => runic_power>88),
                        // actions.aoe+=/death_and_decay,if=unholy==1
                        Spell.CastOnGround("Death and Decay", on => Me.CurrentTarget, req => unholy==1),
                        // actions.aoe+=/plague_strike,if=unholy==2
                        Spell.Buff("Plague Strike", req => unholy==2),
                        // actions.aoe+=/blood_tap
                        Spell.Cast("Blood Tap"),
                        // actions.aoe+=/frost_strike,if=!talent.breath_of_sindragosa_enabled||cooldown.breath_of_sindragosa_remains>=10
                        Spell.Cast("Frost Strike", req => !talent.breath_of_sindragosa_enabled||cooldown.breath_of_sindragosa_remains>=10),
                        // actions.aoe+=/plague_leech
                        Spell.Cast("Plague Leech"),
                        // actions.aoe+=/plague_strike,if=unholy==1
                        Spell.Buff("Plague Strike", req => unholy==1),
                        // actions.aoe+=/empower_rune_weapon
                        Spell.BuffSelf("Empower Rune Weapon")
                        )
                    ),
                new Decorator(
                    req => Common.scenario.MobCount < 4,
                    new PrioritySelector(
                        // actions.single_target==plague_leech,if=disease.min_remains<1
                        Spell.Cast("Plague Leech", req => disease.min_remains<1),
                        // actions.single_target+=/soul_reaper,if=target.health_pct-3*(target.health_pct%target.time_to_die)<=35
                        Spell.Cast("Soul Reaper", req => target.health_pct-3*(target.health_pct%target.time_to_die)<=35),
                        // actions.single_target+=/blood_tap,if=(target.health_pct-3*(target.health_pct%target.time_to_die)<=35&&cooldown.soul_reaper_remains==0)
                        Spell.Cast("Blood Tap", req => (target.health_pct-3*(target.health_pct%target.time_to_die)<=35&&cooldown.soul_reaper_remains==0)),
                        // actions.single_target+=/defile
                        Spell.CastOnGround("Defile", on => Me.CurrentTarget, req => true),
                        // actions.single_target+=/blood_tap,if=talent.defile_enabled&&cooldown.defile_remains==0
                        Spell.Cast("Blood Tap", req => talent.defile_enabled&&cooldown.defile_remains==0),
                        // actions.single_target+=/howling_blast,if=buff.rime_react&&disease.min_remains>5&&buff.killing_machine_react
                        Spell.Cast("Howling Blast", req => buff.rime_react&&disease.min_remains>5&&buff.killing_machine_react),
                        // actions.single_target+=/obliterate,if=buff.killing_machine_react
                        Spell.Cast("Obliterate", req => buff.killing_machine_react),
                        // actions.single_target+=/blood_tap,if=buff.killing_machine_react
                        Spell.Cast("Blood Tap", req => buff.killing_machine_react),
                        // actions.single_target+=/howling_blast,if=!talent.necrotic_plague_enabled&&!dot.frost_fever_ticking&&buff.rime_react
                        Spell.Cast("Howling Blast", req => !talent.necrotic_plague_enabled&&!dot.frost_fever_ticking&&buff.rime_react),
                        // actions.single_target+=/outbreak,if=!disease.max_ticking
                        Spell.Cast("Outbreak", req => !disease.max_ticking),
                        // actions.single_target+=/unholy_blight,if=!disease.min_ticking
                        Spell.Cast("Unholy Blight", req => !disease.min_ticking),
                        // actions.single_target+=/breath_of_sindragosa,if=runic_power>75
                        Spell.Cast("Breath of Sindragosa", req => runic_power>75),
                        // actions.single_target+=/run_action_list,name==bos_st,if=dot.breath_of_sindragosa_ticking
                        new Decorator(
                            req => dot.breath_of_sindragosa_ticking,
                            CreateFrostSingleTarget2H_bos_st()
                            ),
                        // actions.single_target+=/obliterate,if=talent.breath_of_sindragosa_enabled&&cooldown.breath_of_sindragosa_remains<7&&runic_power<76
                        Spell.Cast("Obliterate", req => talent.breath_of_sindragosa_enabled&&cooldown.breath_of_sindragosa_remains<7&&runic_power<76),
                        // actions.single_target+=/howling_blast,if=talent.breath_of_sindragosa_enabled&&cooldown.breath_of_sindragosa_remains<3&&runic_power<88
                        Spell.Cast("Howling Blast", req => talent.breath_of_sindragosa_enabled&&cooldown.breath_of_sindragosa_remains<3&&runic_power<88),
                        // actions.single_target+=/howling_blast,if=!talent.necrotic_plague_enabled&&!dot.frost_fever_ticking
                        Spell.Cast("Howling Blast", req => !talent.necrotic_plague_enabled&&!dot.frost_fever_ticking),
                        // actions.single_target+=/howling_blast,if=talent.necrotic_plague_enabled&&!dot.necrotic_plague_ticking
                        Spell.Cast("Howling Blast", req => talent.necrotic_plague_enabled&&!dot.necrotic_plague_ticking),
                        // actions.single_target+=/plague_strike,if=!talent.necrotic_plague_enabled&&!dot.blood_plague_ticking
                        Spell.Buff("Plague Strike", req => !talent.necrotic_plague_enabled&&!dot.blood_plague_ticking),
                        // actions.single_target+=/blood_tap,if=buff.blood_charge_stack>10&&runic_power>76
                        Spell.Cast("Blood Tap", req => buff.blood_charge_stack>10&&runic_power>76),
                        // actions.single_target+=/frost_strike,if=runic_power>76
                        Spell.Cast("Frost Strike", req => runic_power>76),
                        // actions.single_target+=/howling_blast,if=buff.rime_react&&disease.min_remains>5&&(blood_frac>=1.8||unholy_frac>=1.8||frost_frac>=1.8)
                        // Spell.Cast("Howling Blast", req => buff.rime_react&&disease.min_remains>5&&(blood_frac>=1.8||unholy_frac>=1.8||frost_frac>=1.8)),
                        // actions.single_target+=/obliterate,if=blood_frac>=1.8||unholy_frac>=1.8||frost_frac>=1.8
                        // Spell.Cast("Obliterate", req => blood_frac>=1.8||unholy_frac>=1.8||frost_frac>=1.8),
                        // actions.single_target+=/plague_leech,if=disease.min_remains<3&&((blood_frac<=0.95&&unholy_frac<=0.95)||(frost_frac<=0.95&&unholy_frac<=0.95)||(frost_frac<=0.95&&blood_frac<=0.95))
                        // Spell.Cast("Plague Leech", req => disease.min_remains<3&&((blood_frac<=0.95&&unholy_frac<=0.95)||(frost_frac<=0.95&&unholy_frac<=0.95)||(frost_frac<=0.95&&blood_frac<=0.95))),
                        // actions.single_target+=/frost_strike,if=talent.runic_empowerment_enabled&&(frost==0||unholy==0||blood==0)&&(!buff.killing_machine_react||!obliterate.ready_in<=1)
                        //Spell.Cast("Frost Strike", req => talent.runic_empowerment_enabled&&(frost==0||unholy==0||blood==0)&&(!buff.killing_machine_react||!obliterate.ready_in<=1)),
                        // actions.single_target+=/frost_strike,if=talent.blood_tap_enabled&&buff.blood_charge_stack<=10&&(!buff.killing_machine_react||!obliterate.ready_in<=1)
                        //Spell.Cast("Frost Strike", req => talent.blood_tap_enabled&&buff.blood_charge_stack<=10&&(!buff.killing_machine_react||!obliterate.ready_in<=1)),
                        // actions.single_target+=/howling_blast,if=buff.rime_react&&disease.min_remains>5
                        Spell.Cast("Howling Blast", req => buff.rime_react&&disease.min_remains>5),
                        // actions.single_target+=/obliterate,if=blood_frac>=1.5||unholy_frac>=1.6||frost_frac>=1.6||buff.bloodlust.up||cooldown.plague_leech_remains<=4
                        // Spell.Cast("Obliterate", req => blood_frac>=1.5||unholy_frac>=1.6||frost_frac>=1.6||buff.bloodlust.up||cooldown.plague_leech_remains<=4),
                        // actions.single_target+=/blood_tap,if=(buff.blood_charge_stack>10&&runic_power>=20)||(blood_frac>=1.4||unholy_frac>=1.6||frost_frac>=1.6)
                        // Spell.Cast("Blood Tap", req => (buff.blood_charge_stack>10&&runic_power>=20)||(blood_frac>=1.4||unholy_frac>=1.6||frost_frac>=1.6)),
                        // actions.single_target+=/frost_strike,if=!buff.killing_machine_react
                        Spell.Cast("Frost Strike", req => !buff.killing_machine_react),
                        // actions.single_target+=/plague_leech,if=(blood_frac<=0.95&&unholy_frac<=0.95)||(frost_frac<=0.95&&unholy_frac<=0.95)||(frost_frac<=0.95&&blood_frac<=0.95)
                        // Spell.Cast("Plague Leech", req => (blood_frac<=0.95&&unholy_frac<=0.95)||(frost_frac<=0.95&&unholy_frac<=0.95)||(frost_frac<=0.95&&blood_frac<=0.95)),
                        // actions.single_target+=/empower_rune_weapon
                        Spell.Cast("Empower Rune Weapon"),

                        new ActionAlwaysFail()
                        )
                    )
                );


                // 

        }

        private static Composite CreateFrostSingleTarget2H_bos_st()
        {
            return new PrioritySelector(
                // actions.bos_st==obliterate,if=buff.killing_machine_react
                Spell.Cast("Obliterate", req =>buff.killing_machine_react),
                // actions.bos_st+=/blood_tap,if=buff.killing_machine_react&&buff.blood_charge_stack>=5
                Spell.Cast("Blood Tap", req =>buff.killing_machine_react&&buff.blood_charge_stack>=5),
                // actions.bos_st+=/plague_leech,if=buff.killing_machine_react
                Spell.Cast("Plague Leech", req =>buff.killing_machine_react),
                // actions.bos_st+=/blood_tap,if=buff.blood_charge_stack>=5
                Spell.Cast("Blood Tap", req =>buff.blood_charge_stack>=5),
                // actions.bos_st+=/plague_leech
                Spell.Cast("Plague Leech"),
                // actions.bos_st+=/obliterate,if=runic_power<76
                Spell.Cast("Obliterate", req =>runic_power<76),
                // actions.bos_st+=/howling_blast,if=((death==1&&frost==0&&unholy==0)||death==0&&frost==1&&unholy==0)&&runic_power<88
                Spell.Cast("Howling Blast", req =>((death==1&&frost==0&&unholy==0)||death==0&&frost==1&&unholy==0)&&runic_power<88),

                new ActionAlwaysFail()
                );
        }

        private static Composite CreateFrost2H_bos_aoe()
        {
            return new PrioritySelector(               
                // actions.bos_aoe==howling_blast
                Spell.Cast("Howling Blast"),
                // actions.bos_aoe+=/blood_tap,if=buff.blood_charge_stack>10
                Spell.Cast("Blood Tap", req =>buff.blood_charge_stack>10),
                // actions.bos_aoe+=/death_and_decay,if=unholy==1
                Spell.CastOnGround("Death and Decay", on => Me.CurrentTarget, req =>unholy==1),
                // actions.bos_aoe+=/plague_strike,if=unholy==2
                Spell.Cast("Plague Strike", req =>unholy==2),
                // actions.bos_aoe+=/blood_tap
                Spell.Cast("Blood Tap"),
                // actions.bos_aoe+=/plague_leech
                Spell.Cast("Plague Leech"),
                // actions.bos_aoe+=/plague_strike,if=unholy==1
                Spell.Cast("Plague Strike", req =>unholy==1),
                // actions.bos_aoe+=/empower_rune_weapon
                Spell.Cast("Empower Rune Weapon"),
                // 
                new ActionAlwaysFail()
                );
        }

        public static class set_bonus
        {
            public static int tier17_2pc { get { return StyxWoW.Me.HasAura("Item - Death Knight T17 Frost 2P Bonus") ? 1 : 0; } }
        }

    }
}