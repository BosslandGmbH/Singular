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
using System;
using System.Drawing;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Unholy
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DeathKnightSettings DeathKnightSettings { get { return SingularSettings.Instance.DeathKnight(); } }

        static uint runic_power { get { return StyxWoW.Me.CurrentRunicPower; } }
        static int blood { get { return Common.BloodRuneSlotsActive; } }
        static int frost { get { return Common.FrostRuneSlotsActive; } }
        static int unholy { get { return Common.UnholyRuneSlotsActive; } }
        static int death { get { return Common.DeathRuneSlotsActive; } }


        #region Normal Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Normal)]
        public static Composite CreateDeathKnightUnholyNormalCombat()
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

                        // *** Cool downs ***
                        Spell.BuffSelf("Unholy Frenzy",
                            req => Me.CurrentTarget.IsWithinMeleeRange 
                                && !PartyBuff.WeHaveBloodlust
                                && Helpers.Common.UseLongCoolDownAbility),

                        Spell.Cast("Summon Gargoyle", req => DeathKnightSettings.UseSummonGargoyle && Helpers.Common.UseLongCoolDownAbility),

                        // SingularRoutine.DetectFightingNearMe(TalentManager.HasGlyph("Blood Boil") ? 15 : 10),
                        // SingularRoutine.DetectFightingNearTarget(TalentManager.HasGlyph("Blood Boil") ? 15 : 10),

                        // aoe
                        new Decorator(
                            req => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(12f).Count() >= DeathKnightSettings.DeathAndDecayCount,
                            new PrioritySelector(
                                // Spell.Cast("Gorefiend's Grasp"),
                                Spell.Cast("Remorseless Winter"),
                                CreateUnholyAoeBehavior(),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        // Single target rotation.

                        // Target < 45%, Soul Reaper
                        Spell.Cast("Soul Reaper", req => Me.CurrentTarget.HealthPercent < 45),

                        // Diseases
                        Common.CreateApplyDiseases(),

                        // Dark Transformation
                        Spell.Cast("Dark Transformation",
                            on  => Me.Pet,
                            req => Me.GotAlivePet
                                && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                                && Me.HasAura("Shadow Infusion", 5)),

                        // Scourge Strike/Death and Decay*(Unholy/Death Runes are Capped)
                        new Decorator(
                            req => Common.UnholyRuneSlotsActive >= 2,
                            new PrioritySelector(
                                Spell.CastOnGround("Death and Decay",
                                    on => Me.CurrentTarget,
                                    req => Spell.UseAOE,
                                    false),
                                Spell.Cast("Scourge Strike")
                                )
                            ),

                        // Death Coil (Sudden Doom, high RP)
                        Spell.Cast("Death Coil", req => Me.HasAura(Common.SuddenDoom) || Me.CurrentRunicPower >= 80),                        

                        // Festering Strike (BB and FF are up)
                        Spell.Cast("Festering Strike", req => Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2),
                        
                        // Scourge Strike
                        Spell.Cast("Scourge Strike"),
                        
                        // Festering Strike
                        Spell.Cast("Festering Strike"),

                        // post Single target
                        // attack at range if possible
                        Spell.Cast("Death Coil", req => Me.GotTarget() && !Me.CurrentTarget.IsWithinMeleeRange ),

                        // attack with other abilities if we don't know scourge strike yet
                        new Decorator(
                            req => !SpellManager.HasSpell("Scourge Strike"),
                            new PrioritySelector(
                                Spell.Buff("Icy Touch", true, on => Me.CurrentTarget, req => true, "Frost Fever"),
                                Spell.Buff("Plague Strike", true, on => Me.CurrentTarget, req => true, "Blood Plague"),
                                Spell.Cast("Death Strike", req => Me.HealthPercent < 90),
                                Spell.Cast("Death Coil"),
                                Spell.Cast("Icy Touch"),
                                Spell.Cast("Plague Strike")
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Composite CreateUnholyAoeBehavior()
        {
            return new PrioritySelector(
                // Spell.Cast("Gorefiend's Grasp", on => Me, req => Unit.NearbyUnfriendlyUnits.Count( u => u.Distance.Between(10,20) && u.IsTargetingMeOrPet ),
                Spell.Cast("Remorseless Winter"),

            // Diseases
                Common.CreateApplyDiseases(),

                Spell.Cast("Dark Transformation",
                    on => Me.Pet,
                    req => Me.GotAlivePet
                        && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                        && Me.HasAura("Shadow Infusion", 5)),

            // spread the disease around.
                new Throttle( TimeSpan.FromSeconds(1.5f),
                    Spell.Cast("Blood Boil",
                        req => Me.CurrentTarget.DistanceSqr <= 10 * 10
                            && !Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases
                        )
                    ),

                Spell.CastOnGround(
                    "Death and Decay",
                    on => Me.CurrentTarget,
                    req => Spell.UseAOE && Common.UnholyRuneSlotsActive >= 2,
                    false
                    ),

                Spell.Cast("Blood Boil",
                    req => Me.CurrentTarget.DistanceSqr <= 10 * 10 && Me.DeathRuneCount > 0 
                        || (Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2)
                        ),

                Spell.Cast("Soul Reaper", req => Me.CurrentTarget.HealthPercent < 35),

                Spell.Cast("Scourge Strike", req => Me.UnholyRuneCount == 2),

                Spell.Cast("Death Coil",
                    req => Me.HasAura(Common.SuddenDoom) 
                        || Me.RunicPowerPercent >= 80 
                        || !Me.GotAlivePet 
                        || !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                    )
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightUnholyPvPCombat()
        {
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

                        Common.CreateGetOverHereBehavior(),

                        Common.CreateDarkSuccorBehavior(),

                        Common.CreateSoulReaperHasteBuffBehavior(),

                        Common.CreateDarkSimulacrumBehavior(),

                        // *** Cool downs ***
                        Spell.BuffSelf("Unholy Frenzy",
                            ret => Me.CurrentTarget.IsWithinMeleeRange 
                                && !PartyBuff.WeHaveBloodlust 
                                && Helpers.Common.UseLongCoolDownAbility
                                ),

                        Spell.Cast("Summon Gargoyle", ret => DeathKnightSettings.UseSummonGargoyle && Helpers.Common.UseLongCoolDownAbility),


                        // *** Single target rotation. ***
                        // Execute
                        Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),

                        // Diseases
                        Spell.Cast("Outbreak",
                            ret => !Me.CurrentTarget.HasMyAura("Frost Fever") 
                                || !Me.CurrentTarget.HasAura("Blood Plague")
                                ),

                        Spell.Buff("Icy Touch", true, ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),

                        Spell.Buff("Plague Strike", true, on => Me.CurrentTarget, req => true, "Blood Plague"),

                        Spell.Cast("Dark Transformation",
                            on => Me.Pet,
                            ret => Me.GotAlivePet 
                                && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation") 
                                && Me.HasAura("Shadow Infusion", 5)
                            ),
                        Spell.CastOnGround(
                            "Death and Decay",
                            on => Me.CurrentTarget,
                            req => Spell.UseAOE && Common.UnholyRuneSlotsActive >= 2, 
                            false
                            ),
                        Spell.Cast("Scourge Strike", ret => Me.UnholyRuneCount == 2 || Me.DeathRuneCount > 0),
                        Spell.Cast("Festering Strike", ret => Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2),
                        Spell.Cast("Death Coil", ret => Me.HasAura(Common.SuddenDoom) || Me.CurrentRunicPower >= 80),
                        Spell.Cast("Scourge Strike"),
                        Spell.Cast("Festering Strike"),
                        Spell.Cast("Death Coil", ret => !Me.GotAlivePet || !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation"))
                        )
                    )
                );
        }

        #endregion

        #region Instance Rotations

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Instances)]
        public static Composite CreateDeathKnightUnholyInstanceCombat()
        {
            if (Me.Level < 100)
                return CreateDeathKnightUnholyInstanceCombatLowerLevels();

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

                        // # Executed every time the actor is available.
                        // 
                        new PrioritySelector(
                            // actions=auto_attack
                            // ... handled by Ensure
                            // actions+=/deaths_advance,if=movement.remains>2
                            // actions+=/antimagic_shell,damage=100000
                            // actions+=/blood_fury
                            Spell.BuffSelf("Blood Fury"),
                            // actions+=/berserking
                            Spell.BuffSelf("Berserking"),
                            // actions+=/arcane_torrent
                            Spell.BuffSelf("Arcane Torrent"),
                            // actions+=/potion,name=draenic_strength,if=buff.dark_transformation.up&target.time_to_die<=60
                            // actions+=/run_action_list,name=aoe,if=active_enemies>=2
                            // actions+=/run_action_list,name=single_target,if=active_enemies<2
                            // 
                            new ActionAlwaysFail()
                            ),

                        new Decorator(
                            req => (!dot.blood_plague_ticking||!dot.frost_fever_ticking)&&!dot.necrotic_plague_ticking,
                            new Sequence(
                                CreateSpreadDiseaseBehavior(),
                                new Action( r => Logger.WriteDebug( "- 1 - Spread Disease"))
                                )
                            ),

                        new Decorator(
                            req => Common.scenario.MobCount > 1,
                            new Sequence(
                                CreateAoeBehavior(),
                                new Action( r => Logger.WriteDebug( "- 2 - AOE Attack"))
                                )
                            ),

                        new Decorator(
                            req => Common.scenario.MobCount <= 1,
                            new Sequence(
                                CreateSingleTargetBehavior(),
                                new Action( r => Logger.WriteDebug( "- 3 - Single Target"))
                                )
                            ),

                        new ActionAlwaysFail()
                        )
                    )
                );
        }

        private static Composite CreateAoeBehavior()
        {
            return new PrioritySelector(
                // actions.aoe=unholy_blight
                Spell.BuffSelfAndWait("Unholy Blight"),
                // actions.aoe+=/run_action_list,name=spread,if=(!dot.blood_plague.ticking|!dot.frost_fever.ticking)&!dot.necrotic_plague.ticking                
                // actions.aoe+=/defile
                Spell.CastOnGround("Defile", on => Me.CurrentTarget, req => Spell.UseAOE ),
                // actions.aoe+=/breath_of_sindragosa,if=runic_power>75
                Spell.Buff("Breath of Sindragosa", req => runic_power > 75 && !Me.HasAura("Breath of Sindragosa")),
                // actions.aoe+=/run_action_list,name=bos_aoe,if=dot.breath_of_sindragosa.ticking
                new Decorator(
                    req => dot.breath_of_sindragosa_ticking,
                    CreateBosAoeBehavior()
                    ),
                // actions.aoe+=/blood_boil,if=blood=2|(frost=2&death=2)
                Spell.Cast("Blood Boil", req => Spell.UseAOE && (blood == 2 || (frost == 2 && death == 2))),
                // actions.aoe+=/summon_gargoyle
                Spell.Cast("Summon Gargoyle"),
                // actions.aoe+=/dark_transformation
                Spell.Buff( "Dark Transformation", on => Me.Pet ),
                // actions.aoe+=/blood_tap,if=level<=90&&buff.shadow_infusion.stack==5
                Spell.Cast("Blood Tap", req => Me.Level<=90&&buff.shadow_infusion_stack==5),
                // actions.aoe+=/defile
                Spell.CastOnGround("Defile", on => Me.CurrentTarget, req => Spell.UseAOE ),
                // actions.aoe+=/death_and_decay,if=unholy==1
                Spell.CastOnGround("Death and Decay", on => Me.CurrentTarget, req => Spell.UseAOE && unholy==1),
                // actions.aoe+=/soul_reaper,if=target.health.pct-3*(target.health.pct%target.time_to_die)<=45
                Spell.Cast("Soul Reaper", req => target.health_pct-3*(target.health_pct%target.time_to_die)<=45),
                // actions.aoe+=/scourge_strike,if=unholy==2
                Spell.Cast("Scourge Strike", req => unholy==2),
                // actions.aoe+=/blood_tap,if=buff.blood_charge.stack>10
                Spell.Cast("Blood Tap", req => buff.blood_charge_stack>10),
                // actions.aoe+=/death_coil,if=runic_power>90||buff.sudden_doom_react||(buff.dark_transformation_down&&unholy<=1)
                Spell.Cast("Death Coil", req => runic_power>90||buff.sudden_doom_react||(buff.dark_transformation_down&&unholy<=1)),
                // actions.aoe+=/blood_boil
                Spell.Cast("Blood Boil", req => Spell.UseAOE ),
                // actions.aoe+=/icy_touch
                Spell.Buff("Icy Touch"),
                // actions.aoe+=/scourge_strike,if=unholy==1
                Spell.Cast("Scourge Strike", req => unholy==1),
                // actions.aoe+=/death_coil
                Spell.Cast("Death Coil"),
                // actions.aoe+=/blood_tap
                Spell.Cast("Blood Tap"),
                // actions.aoe+=/plague_leech
                Spell.Cast("Plague Leech"),
                // actions.aoe+=/empower_rune_weapon
                Spell.Cast("Empower Rune Weapon"),
                // 

                new ActionAlwaysFail()
                );
        }
        private static Composite CreateSingleTargetBehavior()
        {
            return new PrioritySelector(
                // actions.single_target=plague_leech,if=(cooldown.outbreak.remains<1)&((blood<1&frost<1)|(blood<1&unholy<1)|(frost<1&unholy<1))
                Spell.Cast( "Plague Leech", req => (cooldown.outbreak_remains<1)&&((blood<1&&frost<1)||(blood<1&&unholy<1)||(frost<1&&unholy<1))),
                // actions.single_target+=/plague_leech,if=((blood<1&frost<1)|(blood<1&unholy<1)|(frost<1&unholy<1))&disease.min_remains<3
                Spell.Cast( "Plague Leech", req => ((blood<1&&frost<1)||(blood<1&&unholy<1)||(frost<1&&unholy<1))&&disease.min_remains<3),
                // actions.single_target+=/plague_leech,if=disease.min_remains<1
                Spell.Cast("Plague Leech", req => disease.min_remains < 1 ),
                // actions.single_target+=/outbreak,if=!talent.necrotic_plague.enabled&!disease.ticking
                Spell.Cast("Outbreak", req => !talent.necrotic_plague_enabled&&!disease.ticking),
                // actions.single_target+=/unholy_blight,if=!talent.necrotic_plague.enabled&disease.min_remains<3
                Spell.BuffSelfAndWait("Unholy Blight", req => !talent.necrotic_plague_enabled&&disease.min_remains<3),
                // actions.single_target+=/unholy_blight,if=talent.necrotic_plague_enabled&&dot.necrotic_plague_remains<1
                Spell.BuffSelfAndWait("Unholy Blight", req => talent.necrotic_plague_enabled&&dot.necrotic_plague_remains<1),
                // actions.single_target+=/death_coil,if=runic_power>90
                Spell.Cast("Death Coil", req => runic_power>90),
                // actions.single_target+=/soul_reaper,if=(target.health_pct-3*(target.health_pct%target.time_to_die))<=45
                Spell.Cast("Soul Reaper", req => (target.health_pct-3*(target.health_pct%target.time_to_die))<=45),
                // actions.single_target+=/breath_of_sindragosa,if=runic_power>75
                Spell.Buff("Breath of Sindragosa", req => runic_power > 75 && !Me.HasAura("Breath of Sindragosa")),
                // actions.single_target+=/run_action_list,name=bos_st,if=dot.breath_of_sindragosa.ticking
                new Decorator( 
                    req => dot.breath_of_sindragosa_ticking, 
                    CreateBosBehavior()
                    ),
                // actions.single_target+=/death_and_decay,if=cooldown.breath_of_sindragosa_remains<7&&runic_power<88&&talent.breath_of_sindragosa_enabled
                Spell.CastOnGround(
                    "Death and Decay", 
                    on => Me.CurrentTarget,
                    req => Spell.UseAOE && cooldown.breath_of_sindragosa_remains < 7 && runic_power < 88 && talent.breath_of_sindragosa_enabled
                    ),
                // actions.single_target+=/scourge_strike,if=cooldown.breath_of_sindragosa_remains<7&&runic_power<88&&talent.breath_of_sindragosa_enabled
                Spell.Cast("Scourge Strike", req => cooldown.breath_of_sindragosa_remains<7&&runic_power<88&&talent.breath_of_sindragosa_enabled),
                // actions.single_target+=/festering_strike,if=cooldown.breath_of_sindragosa_remains<7&&runic_power<76&&talent.breath_of_sindragosa_enabled
                Spell.Cast("Festering Strike", req => cooldown.breath_of_sindragosa_remains < 7 && runic_power < 76 && talent.breath_of_sindragosa_enabled),
                // actions.single_target+=/blood_tap,if=((target.health_pct-3*(target.health_pct%target.time_to_die))<=45)&&cooldown.soul_reaper_remains==0
                Spell.Cast("Blood Tap", req => ((target.health_pct - 3 * (target.health_pct % target.time_to_die)) <= 45) && cooldown.soul_reaper_remains == 0),
                // actions.single_target+=/death_and_decay,if=unholy==2
                Spell.CastOnGround("Death and Decay", on => Me.CurrentTarget, req => Spell.UseAOE && unholy == 2),
                // actions.single_target+=/defile,if=unholy==2
                // .. covered by prior cast
                // actions.single_target+=/plague_strike,if=!disease.ticking&&unholy==2
                Spell.Buff("Plague Strike", req => !disease.ticking && unholy == 2),
                // actions.single_target+=/scourge_strike,if=unholy==2
                Spell.Cast( "Scourge Strike", req => unholy==2),
                // actions.single_target+=/death_coil,if=runic_power>80
                Spell.Cast( "Death Coil", req => runic_power>80),
                // actions.single_target+=/festering_strike,if=blood==2&&frost==2&&(((Frost-death)>0)||((Blood-death)>0))
                Spell.Cast( "Festering Strike", req => blood==2&&frost==2&&(((frost-death)>0)||((blood-death)>0))),
                // actions.single_target+=/festering_strike,if=(blood==2||frost==2)&&(((Frost-death)>0)&&((Blood-death)>0))
                Spell.Cast( "Festering Strike", req => (blood==2||frost==2)&&(((frost-death)>0)&&((blood-death)>0))),
                // actions.single_target+=/defile,if=blood==2||frost==2
                Spell.CastOnGround("Defile", on => Me.CurrentTarget, req => Spell.UseAOE && (blood == 2 || frost == 2)),
                // actions.single_target+=/plague_strike,if=!disease.ticking&&(blood==2||frost==2)
                Spell.Buff( "Plague Strike", req => !disease.ticking&&(blood==2||frost==2)),
                // actions.single_target+=/scourge_strike,if=blood==2||frost==2
                Spell.Cast( "Scourge Strike", req => blood==2||frost==2),
                // actions.single_target+=/festering_strike,if=((Blood-death)>1)
                Spell.Cast( "Festering Strike", req => ((blood-death)>1)),
                // actions.single_target+=/blood_boil,if=((Blood-death)>1)
                Spell.Cast("Blood Boil", req => Spell.UseAOE && ((blood - death) > 1)),
                // actions.single_target+=/festering_strike,if=((Frost-death)>1)
                Spell.Cast( "Festering Strike", req => ((frost-death)>1)),
                // actions.single_target+=/blood_tap,if=((target.health_pct-3*(target.health_pct%target.time_to_die))<=45)&&cooldown.soul_reaper_remains==0
                Spell.Cast( "Blood Tap", req => ((target.health_pct-3*(target.health_pct%target.time_to_die))<=45)&&cooldown.soul_reaper_remains==0),
                // actions.single_target+=/summon_gargoyle
                Spell.Cast( "Summon Gargoyle"),
                // actions.single_target+=/death_and_decay
                Spell.CastOnGround("Death and Decay", on => Me.CurrentTarget, req => Spell.UseAOE ),
                // actions.single_target+=/defile
                // ... covered by prior cast
                // actions.single_target+=/blood_tap,if=cooldown.defile_remains==0
                Spell.Cast( "Blood Tap", req => cooldown.defile_remains==0),
                // actions.single_target+=/plague_strike,if=!disease.ticking
                Spell.Buff( "Plague Strike", req => !disease.ticking),
                // actions.single_target+=/dark_transformation
                Spell.Buff("Dark Transformation", on => Me.Pet),
                // actions.single_target+=/blood_tap,if=buff.blood_charge.stack>10&&(buff.sudden_doom_react||(buff.dark_transformation_down&&unholy<=1))
                Spell.Cast( "Blood Tap", req => buff.blood_charge_stack>10&&(buff.sudden_doom_react||(buff.dark_transformation_down&&unholy<=1))),
                // actions.single_target+=/death_coil,if=buff.sudden_doom_react||(buff.dark_transformation_down&&unholy<=1)
                Spell.Cast( "Death Coil", req => buff.sudden_doom_react||(buff.dark_transformation_down&&unholy<=1)),
                // actions.single_target+=/scourge_strike,if=!((target.health_pct-3*(target.health_pct%target.time_to_die))<=45)||(Unholy>=2)
                Spell.Cast( "Scourge Strike", req => !((target.health_pct-3*(target.health_pct%target.time_to_die))<=45)||(unholy>=2)),
                // actions.single_target+=/blood_tap
                Spell.Cast( "Blood Tap"),
                // actions.single_target+=/festering_strike,if=!((target.health_pct-3*(target.health_pct%target.time_to_die))<=45)||(((Frost-death)>0)&&((Blood-death)>0))
                Spell.Cast( "Festering Strike", req => !((target.health_pct-3*(target.health_pct%target.time_to_die))<=45)||(((frost-death)>0)&&((blood-death)>0))),
                // actions.single_target+=/death_coil
                Spell.Cast( "Death Coil"),
                // actions.single_target+=/plague_leech
                Spell.Cast( "Plague Leech"),
                // actions.single_target+=/scourge_strike,if=cooldown.empower_rune_weapon_remains==0
                Spell.Cast( "Scourge Strike", req => cooldown.empower_rune_weapon_remains==0),
                // actions.single_target+=/festering_strike,if=cooldown.empower_rune_weapon_remains==0
                Spell.Cast( "Festering Strike", req => cooldown.empower_rune_weapon_remains==0),
                // actions.single_target+=/blood_boil,if=cooldown.empower_rune_weapon_remains==0
                Spell.Cast("Blood Boil", req => Spell.UseAOE && cooldown.empower_rune_weapon_remains == 0),
                // actions.single_target+=/icy_touch,if=cooldown.empower_rune_weapon_remains==0
                Spell.Buff( "Icy Touch", req => cooldown.empower_rune_weapon_remains==0),
                // actions.single_target+=/empower_rune_weapon,if=blood<1&&unholy<1&&frost<1
                Spell.Cast( "Empower Rune Weapon", req => blood<1&&unholy<1&&frost<1),

                new ActionAlwaysFail()
                );
        }
        private static Composite CreateBosBehavior()
        {
            return new PrioritySelector(
                // 
                // actions.bos_st=death_and_decay,if=runic_power<88
                Spell.CastOnGround("Death and Decay", on => Me.CurrentTarget, req => Spell.UseAOE && runic_power < 88),
                // actions.bos_st+=/festering_strike,if=runic_power<77
                Spell.Cast("Festering Strike", req => runic_power<77),
                // actions.bos_st+=/scourge_strike,if=runic_power<88
                Spell.Cast("Scourge Strike", req => runic_power<88),
                // actions.bos_st+=/blood_tap,if=buff.blood_charge.stack>=5
                Spell.Cast("Blood Tap", req => buff.blood_charge_stack>=5),
                // actions.bos_st+=/plague_leech
                Spell.Cast("Plague Leech"),
                // actions.bos_st+=/empower_rune_weapon
                Spell.Cast("Empower Rune Weapon"),
                // actions.bos_st+=/death_coil,if=buff.sudden_doom_react
                Spell.Cast("Death Coil", req => buff.sudden_doom_react),

                new ActionAlwaysFail()
                );

        }

        private static Composite CreateBosAoeBehavior()
        {
            return new PrioritySelector(
                // actions.bos_aoe=death_and_decay,if=runic_power<88
                Spell.CastOnGround("Death and Decay", on => Me.CurrentTarget, req => Spell.UseAOE && runic_power < 88),
                // actions.bos_aoe+=/blood_boil,if=runic_power<88
                Spell.Cast("Blood Boil", req => Spell.UseAOE && runic_power < 88),
                // actions.bos_aoe+=/scourge_strike,if=runic_power<88&&unholy==1
                Spell.Cast("Scourge Strike", req => runic_power<88&&unholy==1),
                // actions.bos_aoe+=/icy_touch,if=runic_power<88
                Spell.Buff("Icy Touch", req => runic_power < 88),
                // actions.bos_aoe+=/blood_tap,if=buff.blood_charge.stack>=5
                Spell.Cast("Blood Tap", req => buff.blood_charge_stack>=5),
                // actions.bos_aoe+=/plague_leech
                Spell.Cast("Plague Leech"),
                // actions.bos_aoe+=/empower_rune_weapon
                Spell.BuffSelfAndWait("Empower Rune Weapon"),
                // actions.bos_aoe+=/death_coil,if=buff.sudden_doom_react
                Spell.Cast("Death Coil", req => buff.sudden_doom_react),

                new ActionAlwaysFail()
                );
        }

        private static Composite CreateSpreadDiseaseBehavior()
        {
            return new PrioritySelector(
                // actions.spread=blood_boil,cycle_targets=1,if=!disease.ticking&&!talent.necrotic_plague_enabled
                Spell.Cast(
                    "Blood Boil", 
                    on => Common.scenario.Mobs.FirstOrDefault(u => !disease.ticking_on(u) && u.SpellDistance() < 10),
                    req => Spell.UseAOE && !talent.necrotic_plague_enabled
                    ),
                // actions.spread+=/outbreak,if=!talent.necrotic_plague_enabled&&!disease.ticking
                Spell.Buff(
                    "Outbreak", 
                    on => Common.scenario.Mobs.FirstOrDefault(u => !disease.ticking_on(u) && u.SpellDistance() < 30), 
                    req => !talent.necrotic_plague_enabled
                    ),
                // actions.spread+=/outbreak,if=talent.necrotic_plague_enabled&&!dot.necrotic_plague.ticking
                Spell.Buff(
                    "Outbreak", 
                    on => Common.scenario.Mobs.FirstOrDefault(u => !dot.necrotic_plague_ticking_on(u) && u.SpellDistance() < 30), 
                    req => talent.necrotic_plague_enabled
                    ),
                // actions.spread+=/plague_strike,if=!talent.necrotic_plague_enabled&&!disease.ticking
                Spell.Buff(
                    "Plague Strike",
                    on => Common.scenario.Mobs.FirstOrDefault(u => !disease.ticking_on(u) && u.IsWithinMeleeRange),
                    req => !talent.necrotic_plague_enabled 
                    ),
                // actions.spread+=/plague_strike,if=talent.necrotic_plague_enabled&&!dot.necrotic_plague.ticking
                Spell.Buff(
                    "Plague Strike",
                    on => Common.scenario.Mobs.FirstOrDefault(u => !dot.necrotic_plague_ticking_on(u) && u.IsWithinMeleeRange),
                    req => talent.necrotic_plague_enabled 
                    ),

                new ActionAlwaysFail()
                );
        }

        private static Composite CreateDeathKnightUnholyInstanceCombatLowerLevels()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        // *** Cool downs ***
                        Spell.Cast(
                            "Summon Gargoyle",
                            ret => DeathKnightSettings.UseSummonGargoyle
                                && Helpers.Common.UseLongCoolDownAbility),

                        // Start AoE section
                        new Decorator(
                            ret => Spell.UseAOE
                                && Unit.UnfriendlyUnitsNearTarget(12f).Count() >= DeathKnightSettings.DeathAndDecayCount,
                            new PrioritySelector(
                // Diseases
                                Common.CreateApplyDiseases(),

                                // spread the disease around.
                                new Throttle(2,
                                    Spell.Cast("Blood Boil",
                                        ret => !Me.HasAura("Unholy Blight")
                                            && Me.CurrentTarget.DistanceSqr <= 10 * 10
                                            && Common.ShouldSpreadDiseases)
                                    ),

                                Spell.Cast("Dark Transformation",
                                    ret => Me.GotAlivePet
                                        && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                                        && Me.HasAura("Shadow Infusion", 5)),

                                Spell.CastOnGround("Death and Decay",
                                    loc => Me.CurrentTarget,
                                    req => Common.UnholyRuneSlotsActive >= 2,
                                    false),

                                Spell.Cast("Blood Boil",
                                    ret => Me.CurrentTarget.DistanceSqr <= 10 * 10
                                        && (Me.DeathRuneCount > 0 || (Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2))),

                                // Execute
                                Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),
                                Spell.Cast("Scourge Strike", ret => Me.UnholyRuneCount == 2),
                                Spell.Cast("Death Coil",
                                    req => Me.HasAura(Common.SuddenDoom)
                                        || Me.RunicPowerPercent >= 80
                                        || !Me.GotAlivePet
                                        || !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),

                                Spell.Cast("Remorseless Winter", ret => Common.HasTalent(DeathKnightTalents.RemorselessWinter)),

                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        // *** Single target rotation. ***

                        // Single target rotation.

                        // Target < 45%, Soul Reaper
                        Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 45),

                        // Defile Ground around target
                        Spell.CastOnGround("Defile", on => Me.CurrentTarget, req => Spell.UseAOE && Me.GotTarget() && !Me.CurrentTarget.IsMoving, false),

                        // Diseases
                        Common.CreateApplyDiseases(),

                        // Dark Transformation
                        Spell.Cast("Dark Transformation",
                            ret => Me.GotAlivePet
                                && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                                && Me.HasAura("Shadow Infusion", 5)),

                        // Death Coil if Dark Trans not active
                        Spell.Cast("Death Coil",
                            req => !Me.GotAlivePet
                                || !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                                || Me.RunicPowerPercent > 90
                            ),

                        // Scourge Strike on Death or Unholy Runes active
                        Spell.Cast("Scourge Strike",
                            req => Common.DeathRuneSlotsActive > 0
                                || Common.UnholyRuneSlotsActive > 0),

                        // Festering Strike on both Blood and Frost runes active
                        Spell.Cast("Festering Strike",
                            req => Common.BloodRuneSlotsActive > 0
                                && Common.FrostRuneSlotsActive > 0),

                        // Death Coil (Sudden Doom, high RP)
                        Spell.Cast("Death Coil",
                            ret => Me.HasAura(Common.SuddenDoom)
                                || !Me.CurrentTarget.IsWithinMeleeRange
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Diagnostics

        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.All, 99)]
        [Behavior(BehaviorType.Heal, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.All, 99)]
        public static Composite CreateDeathKnightUnholyDiagnostic()
        {
            return CreateCombatDiagnosticOutputBehavior( 
                Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull ? "PULL" : "COMBAT"
                );
        }

       private static Composite CreateCombatDiagnosticOutputBehavior(string sState = "")
        {
            if (!SingularSettings.Debug)
                return new Action(ret => { return RunStatus.Failure; });

            return new Decorator(
                req => !Spell.IsGlobalCooldown(),
                new ThrottlePasses( TimeSpan.FromMilliseconds(1000),
                    new Action(ret =>
                    {
                        string sMsg;
                        sMsg = string.Format(".... [{0}] h={1:F1}%, e={2:F1}%, runes(b{3} f{4} u{5} d{6}), moving={7}, how={8}",
                            sState,
                            Me.HealthPercent,
                            runic_power,
                            blood,
                            frost,
                            unholy,
                            death,
                            Me.IsMoving,
                            (int)Me.GetAuraTimeLeft("Horn of Winter", true).TotalSeconds
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            sMsg += string.Format(
                                ", {0}, {1:F1}%, dies {2}, {3:F1} yds, inmelee={4}, loss={5}, shdwinfus={6}, ff={7:F1}, bp={8:F1}, np={9:F1}, disticking={10}",
                                target.SafeName(),
                                target.HealthPercent,
                                target.TimeToDeath(),
                                target.Distance,
                                target.IsWithinMeleeRange.ToYN(),
                                target.InLineOfSpellSight,
                                Me.GetAuraStacks("Shadow Infusion"), 
                                dot.frost_fever_remains,
                                dot.blood_plague_remains,
                                dot.necrotic_plague_remains,
                                disease.ticking.ToYN()                                
                                );
                        }

                        if (!Me.GotAlivePet)
                            sMsg += ", no pet";
                        else
                            sMsg += string.Format(", peth={0:F1}%, drktrns={1}", 
                                Me.Pet.HealthPercent,
                                Me.Pet.ActiveAuras.ContainsKey("Dark Transformation").ToYN()
                                );

                        Logger.WriteDebug(Color.LightYellow, sMsg);
                        return RunStatus.Failure;
                    })
                    )
                );
        }

        #endregion
    }
}