using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Singular.Settings;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.Common;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using System.Drawing;


namespace Singular.ClassSpecific.Warlock
{
    // wowraids.org 
    public class Destruction
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }

        private static int _mobCount;
        private static bool _InstantRoF;

        public static int active_enemies { get { return Common.scenario.Mobs.Count(); } }
        public static double burning_ember { get { return Me.GetPowerInfo(WoWPowerType.BurningEmbers).Current / 10; } }
        public static class set_bonus 
        {
            public static bool tier17_2pc { get { return StyxWoW.Me.HasAura(165455); } }
        }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.All)]
        public static Composite CreateWarlockDestructionNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateWarlockDiagnosticOutputBehavior( "Pull" ),

                        // grinding or questing, if target meets these cast Flame Shock if possible
                        // 1. mob is less than 12 yds, so no benefit from delay in Lightning Bolt missile arrival
                        // 2. area has another player competing for mobs (we want to tag the mob quickly)
                        new Decorator(
                            ret =>{
                                if (StyxWoW.Me.CurrentTarget.IsHostile && StyxWoW.Me.CurrentTarget.Distance < 12)
                                {
                                    Logger.WriteDiagnostic("NormalPull: fast pull since hostile target is {0:F1} yds away", StyxWoW.Me.CurrentTarget.Distance);
                                    return true;
                                }
                                WoWPlayer nearby = ObjectManager.GetObjectsOfType<WoWPlayer>(true, false).FirstOrDefault(p => !p.IsMe && p.SpellDistance(Me.CurrentTarget) <= 40);
                                if (nearby != null)
                                {
                                    Logger.WriteDiagnostic("NormalPull: fast pull since player {0} targeting my target from @ {1:F1} yds", nearby.SafeName(), nearby.SpellDistance(Me.CurrentTarget));
                                    return true;
                                }
                                return false;
                                },

                            new PrioritySelector(
                                // instant spells
                                Spell.Cast("Conflagrate"),
                                Spell.CastOnGround(
                                    "Rain of Fire", 
                                    on => Me.CurrentTarget, 
                                    req => Spell.UseAOE 
                                        && _InstantRoF 
                                        && !Unit.UnfriendlyUnitsNearTarget(8).Any(u => u.Guid != Me.CurrentTargetGuid && (!u.Aggro || u.IsCrowdControlled())), 
                                    false
                                    ),
                                new Action( r => {
                                    Logger.WriteDiagnostic("NormalPull: no instant cast spells were available -- using default Pull logic");
                                    return RunStatus.Failure;
                                    })
                                )
                            ),


                        Spell.Buff("Immolate", 3, on => Me.CurrentTarget, ret => true),
                        Spell.Cast("Incinerate")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.All, priority: 999)]
        public static Composite CreateAfflictionHeal()
        {
            return new PrioritySelector(
                CreateWarlockDiagnosticOutputBehavior("Combat")
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.Normal)]
        public static Composite CreateWarlockDestructionNormalCombat()
        {
            _InstantRoF = Me.HasAura("Aftermath");

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        new Action(ret =>
                        {
                            _mobCount = TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateAoeBehavior(),

                        // Noxxic
                        Spell.Cast("Shadowburn", ret => Me.CurrentTarget.HealthPercent < 20),
                        Spell.Buff("Immolate", 4, on => Me.CurrentTarget, ret => true),
                        Spell.Cast("Conflagrate", req => Spell.GetCharges("Conflagrate") >= 2),

                        Common.CastCataclysm(),

                        Spell.Cast("Chaos Bolt", ret => Me.CurrentTarget.HealthPercent >= 20 && BackdraftStacks < 3),

                        Spell.CastOnGround("Rain of Fire", on => Me.CurrentTarget, req => Spell.UseAOE && _InstantRoF && !Me.CurrentTarget.IsMoving && !Me.CurrentTarget.HasMyAura("Rain of Fire") && !Unit.UnfriendlyUnitsNearTarget(8).Any(u => !u.Aggro || u.IsCrowdControlled()), false),

                        Spell.Cast("Incinerate"),

                        Spell.Cast("Fel Flame", ret => Me.IsMoving && Me.CurrentTarget.GetAuraTimeLeft("Immolate").TotalMilliseconds.Between(300, 3000)),

                        Spell.Cast("Shadow Bolt")
                        )
                    )
                );

        }


        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.Battlegrounds )]
        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.Instances)]
        public static Composite CreateWarlockDestructionInstanceCombat()
        {
            _InstantRoF = Me.HasAura("Aftermath");

            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances && Me.Level >= 100)
                return CreateWarlockDestructionInstanceSimcCombat();

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        new Action(ret =>
                        {
                            _mobCount = TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateAoeBehavior(),

                        new Decorator(
                            req =>
                            {
                                if (Me.HasAnyAura("Dark Soul: Instability", "Toxic Power", "Expanded Mind"))
                                    return true;
                                return false;
                            },
                            new PrioritySelector(
                                Spell.Cast("Shadowburn", req => Me.CurrentTarget.HealthPercent < 20),
                                Spell.Cast("Chaos Bolt", req => Me.CurrentTarget.HealthPercent < 20)
                                )
                            ),

                // Noxxic
                        new Decorator(
                            req => true, // WarlockSettings.DestructionSpellPriority == Singular.Settings.WarlockSettings.SpellPriority.Noxxic,
                            new PrioritySelector(
                                Spell.Cast("Shadowburn", ret => Me.CurrentTarget.HealthPercent < 20),
                                Spell.Buff("Immolate", 3, on => Me.CurrentTarget, ret => true),
                                Spell.Cast("Conflagrate"),
                                Spell.CastOnGround("Rain of Fire", on => Me.CurrentTarget, req => Spell.UseAOE && _InstantRoF && !Me.CurrentTarget.IsMoving && !Me.CurrentTarget.HasMyAura("Rain of Fire") && !Unit.UnfriendlyUnitsNearTarget(8).Any(u => !u.Aggro || u.IsCrowdControlled()), false),

                                Common.CastCataclysm(),

                                Spell.Cast("Chaos Bolt", ret => Me.CurrentTarget.HealthPercent >= 20 && BackdraftStacks < 3),
                                Spell.Cast("Incinerate"),

                                Spell.Cast("Fel Flame", ret => Me.IsMoving && Me.CurrentTarget.GetAuraTimeLeft("Immolate").TotalMilliseconds.Between(300, 3000))
                                )
                            ),

                        // Icy Veins
                        new Decorator(
                            req => false, // WarlockSettings.DestructionSpellPriority == Singular.Settings.WarlockSettings.SpellPriority.IcyVeins,
                            new PrioritySelector(
                                Spell.Cast("Shadowburn", ret =>
                                {
                                    if (Me.CurrentTarget.HealthPercent < 20)
                                    {
                                        if (CurrentBurningEmbers >= 35)
                                            return true;
                                        if (Me.HasAnyAura("Dark Soul: Instability", "Toxic Power", "Expanded Mind"))
                                            return true;
                                        if (Me.CurrentTarget.TimeToDeath(99) < 3)
                                            return true;
                                        if (Me.ManaPercent < 5)
                                            return true;
                                    }
                                    return false;
                                }),

                                Spell.Buff("Immolate", 3, on => Me.CurrentTarget, ret => true),
                                Spell.Cast("Conflagrate", req => Spell.GetCharges("Conflagrate") >= 2),
                                Spell.CastOnGround("Rain of Fire", on => Me.CurrentTarget, req => Spell.UseAOE && _InstantRoF && !Me.CurrentTarget.IsMoving && !Me.CurrentTarget.HasMyAura("Rain of Fire") && !Unit.UnfriendlyUnitsNearTarget(8).Any(u => !u.Aggro || u.IsCrowdControlled()), false),

                                Common.CastCataclysm(),

                                Spell.Cast("Chaos Bolt", ret =>
                                {
                                    if (BackdraftStacks < 3)
                                    {
                                        if (CurrentBurningEmbers >= 35)
                                            return true;
                                        if (Me.HasAura("Dark Soul: Instability"))
                                            return true;
                                    }
                                    return false;
                                }),

                                Spell.Cast("Conflagrate", req => Spell.GetCharges("Conflagrate") == 1),

                                Spell.Cast("Incinerate"),

                                Spell.Cast("Fel Flame", ret => Me.IsMoving && Me.CurrentTarget.GetAuraTimeLeft("Immolate").TotalMilliseconds.Between(300, 3000))
                                )
                            ),

                        Spell.Cast("Shadow Bolt")
                        )
                    )
                );

        }


        public static Composite CreateWarlockDestructionInstanceSimcCombat()
        {
            Logger.WriteDebug("SimC Combat Routine");

            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromLongRange(),
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

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        new PrioritySelector(
                            // actions=potion,name=draenic_intellect,if=buff.bloodlust.react&buff.dark_soul_remains>10|target.time_to_die<=25|buff.dark_soul_remains>10
                            // actions+=/berserking
                            Spell.BuffSelf("Blood Fury", req => true, gcd: HasGcd.No),
                            // actions+=/blood_fury
                            Spell.BuffSelf("Berserking", req => true, gcd: HasGcd.No),
                            // actions+=/arcane_torrent
                            Spell.BuffSelf("Arcane Torrent", req => true, gcd: HasGcd.No),
                            // actions+=/mannoroths_fury
                            Spell.BuffSelf("Mannoroth's Fury", req => true, gcd: HasGcd.No),
                            // actions+=/dark_soul,if=!talent.archimondes_darkness_enabled|(talent.archimondes_darkness_enabled&(charges=2|trinket.proc.intellect.react|trinket_stacking_proc.intellect.react>6|target.health.pct<=10))
                            Spell.BuffSelf("Dark Soul", req => true, gcd: HasGcd.No ),
                            // actions+=/service_pet,if=talent.grimoire_of_service_enabled
                            // actions+=/summon_doomguard,if=!talent.demonic_servitude_enabled&active_enemies<5
                            // actions+=/summon_infernal,if=!talent.demonic_servitude_enabled&active_enemies>=5
                            // actions+=/run_action_list,name=single_target,if=active_enemies<6
                            // actions+=/run_action_list,name=aoe,if=active_enemies>=6

                            new ActionAlwaysFail()
                            ),

                        new Decorator(
                            req => Common.scenario.MobCount < 6,
                            new Sequence(
                                CreateSingleTargetSimcBehavior(),
                                new Action(r => Logger.WriteDebug("- 1 - Single Target"))
                                )
                            ),

                        new Decorator(
                            req => Common.scenario.MobCount >= 6,
                            new Sequence(
                                CreateAoeSimcBehavior(),
                                new Action(r => Logger.WriteDebug("- 2 - AOE Attack"))
                                )
                            ),

                        new ActionAlwaysFail()
                        )
                    )
                );

        }


        public static Composite CreateSingleTargetSimcBehavior()
        {
            return new PrioritySelector(
                // actions.single_target=havoc,target=2
                // actions.single_target+=/shadowburn,if=talent.charred_remains_enabled&(burning_ember>=2.5|buff.dark_soul_up|target.time_to_die<10)
                Spell.Cast( "Shadowburn", req => talent.charred_remains_enabled&&(burning_ember>=2.5||buff.dark_soul_up||target.time_to_die<10)),
                // actions.single_target+=/kiljaedens_cunning,if=(talent.cataclysm_enabled&&!cooldown.cataclysm_remains)
                Spell.BuffSelf( "Kil'jaeden's Cunning", req => (talent.cataclysm_enabled && cooldown.cataclysm_remains == 0), gcd: HasGcd.No),
                // actions.single_target+=/kiljaedens_cunning,moving=1,if=!talent.cataclysm_enabled
                Spell.BuffSelf("Kil'jaeden's Cunning", req => Me.IsMoving && !talent.cataclysm_enabled, gcd: HasGcd.No),
                // actions.single_target+=/cataclysm,if=active_enemies>1
                Common.Cataclysm(2),
                // actions.single_target+=/fire_and_brimstone,if=buff.fire_and_brimstone_down&&dot.immolate_remains<=action.immolate_cast_time&&(cooldown.cataclysm_remains>action.immolate_cast_time||!talent.cataclysm_enabled)&&active_enemies>4
                Spell.Cast("Fire and Brimstone", req => buff.fire_and_brimstone_down && dot.immolate_remains <= action.immolate_cast_time && (cooldown.cataclysm_remains > action.immolate_cast_time || !talent.cataclysm_enabled) && active_enemies > 4),
                // actions.single_target+=/immolate,cycle_targets=1,if=remains<=cast_time&&(cooldown.cataclysm_remains>cast_time||!talent.cataclysm_enabled)
                Spell.Buff(
                    "Immolate", 
                    on => Common.scenario.Mobs
                        .Where(u => u.GetAuraTimeLeft("Immolate").TotalSeconds < action.immolate_cast_time && (cooldown.cataclysm_remains > action.immolate_cast_time || !talent.cataclysm_enabled))
                        .OrderByDescending(u => u.CurrentHealth)
                        .FirstOrDefault()
                    ),
                // actions.single_target+=/cancel_buff,name=fire_and_brimstone,if=buff.fire_and_brimstone_up&&dot.immolate_remains>(dot.immolate_duration*0.3)
                new Action( r => 
                {
                    if (buff.fire_and_brimstone_up&&dot.immolate_remains>(dot.immolate_duration*0.3))
                    {
                        Logger.Write(LogColor.Cancel, "/cancel Fire and Brimstone");
                        Me.CancelAura("Fire and Brimstone");
                        return RunStatus.Success;
                    }
                    return RunStatus.Failure;
                }),
                // actions.single_target+=/shadowburn,if=buff.havoc_remains
                Spell.Cast("Shadowburn", req => buff.havoc_remains > 0),
                // actions.single_target+=/chaos_bolt,if=buff.havoc_remains>cast_time&&buff.havoc_stack>=3
                Spell.Cast("Chaos Bolt", req => buff.havoc_remains > action.chaos_bolt_cast_time && buff.havoc_stack >= 3),
                // actions.single_target+=/conflagrate,if=charges=2
                Spell.Cast("Conflagrate", req => action.conflagrate_charges == 2),
                // actions.single_target+=/cataclysm
                Common.Cataclysm(1),
                // actions.single_target+=/rain_of_fire,if=remains<=tick_time&&(active_enemies>4||(buff.mannoroths_fury_up&&active_enemies>2))
                Spell.CastOnGround("Rain of Fire", on => Me.CurrentTarget, req => buff.rain_of_fire_remains <= action.rain_of_fire_tick_time && (active_enemies > 4 || (buff.mannoroths_fury_up && active_enemies > 2))),
                // actions.single_target+=/chaos_bolt,if=talent.charred_remains_enabled&&active_enemies>1&&target.health_pct>20
                Spell.Cast("Chaos Bolt", req => talent.charred_remains_enabled && active_enemies > 1 && target.health_pct > 20),
                // actions.single_target+=/chaos_bolt,if=talent.charred_remains_enabled&&buff.backdraft_stack<3&&burning_ember>=2.5
                Spell.Cast("Chaos Bolt", req => talent.charred_remains_enabled && buff.backdraft_stack < 3 && burning_ember >= 2.5),
                // actions.single_target+=/chaos_bolt,if=buff.backdraft_stack<3&&(burning_ember>=3.5||buff.dark_soul_up||(burning_ember>=3&&buff.ember_master_react)||target.time_to_die<20)
                Spell.Cast("Chaos Bolt", req => buff.backdraft_stack < 3 && (burning_ember >= 3.5 || buff.dark_soul_up || (burning_ember >= 3 && buff.ember_master_react) || target.time_to_die < 20)),
                // actions.single_target+=/chaos_bolt,if=buff.backdraft_stack<3&&set_bonus.tier17_2pc=1&&burning_ember>=2.5
                Spell.Cast("Chaos Bolt", req => buff.backdraft_stack < 3 && set_bonus.tier17_2pc && burning_ember >= 2.5),
                // actions.single_target+=/chaos_bolt,if=buff.backdraft_stack<3&&buff.archmages_greater_incandescence_int_react&&buff.archmages_greater_incandescence_int_remains>cast_time
                // actions.single_target+=/chaos_bolt,if=buff.backdraft_stack<3&&trinket.proc.intellect_react&&trinket.proc.intellect_remains>cast_time
                // actions.single_target+=/chaos_bolt,if=buff.backdraft_stack<3&&trinket_stacking_proc.intellect_react>7&&trinket_stacking_proc.intellect_remains>=cast_time
                // actions.single_target+=/chaos_bolt,if=buff.backdraft_stack<3&&trinket.proc.crit_react&&trinket.proc.crit_remains>cast_time
                // actions.single_target+=/chaos_bolt,if=buff.backdraft_stack<3&&trinket_stacking_proc.multistrike_react>=8&&trinket_stacking_proc.multistrike_remains>=cast_time
                // actions.single_target+=/chaos_bolt,if=buff.backdraft_stack<3&&trinket.proc.multistrike_react&&trinket.proc.multistrike_remains>cast_time
                // actions.single_target+=/chaos_bolt,if=buff.backdraft_stack<3&&trinket.proc.versatility_react&&trinket.proc.versatility_remains>cast_time
                // actions.single_target+=/chaos_bolt,if=buff.backdraft_stack<3&&trinket.proc.mastery_react&&trinket.proc.mastery_remains>cast_time
                // actions.single_target+=/fire_and_brimstone,if=buff.fire_and_brimstone_down&&dot.immolate_remains<=(dot.immolate_duration*0.3)&&active_enemies>4
                Spell.Cast("Fire and Brimstone", req => buff.fire_and_brimstone_down && dot.immolate_remains <= (dot.immolate_duration * 0.3) && active_enemies > 4),
                // actions.single_target+=/immolate,cycle_targets=1,if=remains<=(duration*0.3)
                Spell.Buff(
                    "Immolate",
                    on => Common.scenario.Mobs
                        .Where(u => u.GetAuraTimeLeft("Immolate").TotalSeconds < dot.immolate_duration * 0.3 && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && u.SpellDistance() < 40)
                        .OrderByDescending(u => u.CurrentHealth)
                        .FirstOrDefault()
                    ),
                // actions.single_target+=/conflagrate
                Spell.Cast( "Conflagrate"),
                // actions.single_target+=/incinerate
                Spell.Cast( "Incinerate")
                );
        }


 
        public static Composite CreateAoeSimcBehavior()
        {
            return new PrioritySelector(
                // actions.aoe=rain_of_fire,if=remains<=tick_time
                Spell.CastOnGround("Rain of Fire", on => Me.CurrentTarget, req => buff.rain_of_fire_remains <= action.rain_of_fire_tick_time),
                // actions.aoe+=/havoc,target=2
                Spell.Buff( 
                    "Havoc", 
                    on => Common.scenario.Mobs
                        .FirstOrDefault( 
                            u => u.Guid != Me.CurrentTargetGuid 
                                && u.InLineOfSpellSight 
                                && u.SpellDistance() < 40 
                                && Me.IsSafelyFacing(u)
                            )
                    ),
                // actions.aoe+=/shadowburn,if=buff.havoc_remains
                Spell.Cast( "Shadowburn", req => buff.havoc_remains > 0),
                // actions.aoe+=/chaos_bolt,if=buff.havoc_remains>cast_time&&buff.havoc_stack>=3
                Spell.Cast("Chaos Bolt", req => buff.havoc_remains > action.chaos_bolt_cast_time && buff.havoc_stack >= 3),
                // actions.aoe+=/kiljaedens_cunning,if=(talent.cataclysm_enabled&&!cooldown.cataclysm_remains)
                Spell.Cast("Kil'jaeden's Cunning", req => (talent.cataclysm_enabled && cooldown.cataclysm_remains == 0)),
                // actions.aoe+=/kiljaedens_cunning,moving=1,if=!talent.cataclysm_enabled
                Spell.Cast("Kil'jaeden's Cunning", req => !talent.cataclysm_enabled),
                // actions.aoe+=/cataclysm
                Common.Cataclysm(1),
                // actions.aoe+=/fire_and_brimstone,if=buff.fire_and_brimstone_down
                Spell.Cast("Fire and Brimstone", req => buff.fire_and_brimstone_down),
                // actions.aoe+=/immolate,if=buff.fire_and_brimstone_up&&!dot.immolate_ticking
                Spell.Buff("Immolate", req => buff.fire_and_brimstone_up && !dot.immolate_ticking),
                // actions.aoe+=/conflagrate,if=buff.fire_and_brimstone_up&&charges=2
                Spell.Cast("Conflagrate", req => buff.fire_and_brimstone_up && action.conflagrate_charges == 2),
                // actions.aoe+=/immolate,if=buff.fire_and_brimstone_up&&dot.immolate_remains<=(dot.immolate_duration*0.3)
                Spell.Buff("Immolate", req => buff.fire_and_brimstone_up && dot.immolate_remains <= (dot.immolate_duration * 0.3)),
                // actions.aoe+=/chaos_bolt,if=talent.charred_remains_enabled&&buff.fire_and_brimstone_up&&burning_ember>=2.5
                Spell.Cast("Chaos Bolt", req => talent.charred_remains_enabled && buff.fire_and_brimstone_up && burning_ember >= 2.5),
                // actions.aoe+=/incinerate
                Spell.Cast("Incinerate")
                );
        }




        public static Composite CreateAoeBehavior()
        {
            return new Decorator( 
                ret => Spell.UseAOE && _mobCount > 1,
                new PrioritySelector(

                    new Decorator(
                        ret => _mobCount < 4,
                        Spell.Buff("Immolate", true, on => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(u => u.HasAuraExpired("Immolate", 3) && Spell.CanCastHack("Immolate", u) && Me.IsSafelyFacing(u, 150)), req => true)
                        ),

                    new PrioritySelector(
                        ctx => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(u => u.Guid != Me.CurrentTargetGuid && !u.HasMyAura("Havoc ")),
                        Spell.Buff("Havoc", on => ((WoWUnit)on) ?? Unit.NearbyUnitsInCombatWithMeOrMyStuff.Where(u => u.Guid != Me.CurrentTargetGuid).OrderByDescending( u => u.CurrentHealth).FirstOrDefault())
                        ),

                    Common.CastCataclysm(),

                    new Decorator(
                        ret => _mobCount >= 4,
                        new PrioritySelector(
                            new PrioritySelector(
                                ctx => Clusters.GetBestUnitForCluster( Unit.NearbyUnfriendlyUnits.Where(u => Me.IsSafelyFacing(u)), ClusterType.Radius, 8f),
                                Spell.CastOnGround( "Rain of Fire", 
                                    on => (WoWUnit)on, 
                                    req => req != null 
                                        && _InstantRoF
                                        && !Me.HasAura( "Rain of Fire")
                                        && 3 <= Unit.UnfriendlyUnitsNearTarget((WoWUnit)req, 8).Count()
                                        && !Unit.UnfriendlyUnitsNearTarget((WoWUnit)req, 8).Any(u => !u.Aggro || u.IsCrowdControlled())
                                    )
                                ),

                                Spell.HandleOffGCD(Spell.Buff("Fire and Brimstone", on => Me.CurrentTarget, req => Unit.NearbyUnfriendlyUnits.Count(u => Me.CurrentTarget.Location.Distance(u.Location) <= 10f) >= 4))
                            )
                        )
                    )
                );
        }


        #endregion

        public static double CurrentBurningEmbers
        {
            get
            {
                return Me.GetPowerInfo(WoWPowerType.BurningEmbers).Current;
            }
        }

        static double ImmolateTime(WoWUnit u = null)
        {
            return (u ?? Me.CurrentTarget).GetAuraTimeLeft("Immolate", true).TotalSeconds;
        }

        static IEnumerable<WoWUnit> TargetsInCombat
        {
            get
            {
                return Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && u.IsTargetingMyStuff() && !u.IsCrowdControlled() && StyxWoW.Me.IsSafelyFacing(u));
            }
        }

        static int BackdraftStacks
        {
            get
            {
                return (int) Me.GetAuraStacks("Backdraft");
            }
        }

        private static Composite CreateWarlockDiagnosticOutputBehavior(string s)
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    string msg;
                    msg = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, embers={3}, backdraft={4}, conflag={5}, aoe={6}",
                        s,
                        Me.HealthPercent,
                        Me.ManaPercent,
                        CurrentBurningEmbers,
                        BackdraftStacks,
                        Spell.GetCharges("Conflagrate"),
                        _mobCount
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        msg += string.Format(
                            ", {0}, {1:F1}%, dies={2} secs, {3:F1} yds, loss={4}, face={5}, immolate={6}, rainfire={7}",
                            target.SafeName(),
                            target.HealthPercent,
                            target.TimeToDeath(),
                            target.Distance,
                            target.InLineOfSpellSight.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            (long)target.GetAuraTimeLeft("Immolate", true).TotalMilliseconds,
                            target.HasMyAura("Rain of Fire").ToYN()
                            );
                    }

                    Logger.WriteDebug(Color.LightYellow, msg);
                    return RunStatus.Failure;
                })
            );
        }

    }
}