using System.Linq;

using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Singular.Managers;
using Styx.Common;
using System.Drawing;
using System;
using Styx.Helpers;
using CommonBehaviors.Actions;


namespace Singular.ClassSpecific.Shaman
{
    public class Elemental
    {
        #region Common

        private static LocalPlayer Me => StyxWoW.Me;
	    private static ShamanSettings ShamanSettings => SingularSettings.Instance.Shaman();

	    private static uint MaelstormDeficit => Me.MaxMaelstrom - Me.CurrentMaelstrom;

	    // private static int NormalPullDistance { get { return Math.Max( 35, CharacterSettings.Instance.PullDistance); } }

        [Behavior(BehaviorType.Initialize, WoWClass.Shaman, WoWSpec.ShamanElemental, priority: 9999)]
        public static Composite CreateShamanElementalInitialize()
        {
            if (SpellManager.HasSpell("Enhanced Chain Lightning"))
            {
                Logger.Write(LogColor.Init, "Enhanced Chain Lightning: will now cast Earthquake on proc");
            }

            return null;
        }

        [Behavior(BehaviorType.PreCombatBuffs | BehaviorType.CombatBuffs, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Normal|WoWContext.Instances)]
        public static Composite CreateElementalPreCombatBuffsNormal()
        {
            return new PrioritySelector(
                Common.CreateShamanDpsShieldBehavior(),
                Totems.CreateRecallTotems()
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs | BehaviorType.CombatBuffs, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Battlegrounds )]
        public static Composite CreateElementalPreCombatBuffsPvp()
        {
            return new PrioritySelector(
                Common.CreateShamanDpsShieldBehavior(),
                Totems.CreateRecallTotems()
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanElemental)]
        public static Composite CreateElementalRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Common.CreateShamanDpsHealBehavior(),

                        Rest.CreateDefaultRestBehaviour("Healing Surge", "Ancestral Spirit"),

                        Common.CreateShamanMovementBuff()
                        )
                    )
                );
        }

        /// <summary>
        /// perform diagnostic output logging at highest priority behavior that occurs while in Combat
        /// </summary>
        /// <returns></returns>
        [Behavior(BehaviorType.Heal | BehaviorType.Pull, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.All, 999)]
        public static Composite CreateElementalLogDiagnostic()
        {
            return CreateElementalDiagnosticOutputBehavior();
        }


        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanElemental)]
        public static Composite CreateElementalHeal()
        {
            return Common.CreateShamanDpsHealBehavior( );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Shaman, WoWSpec.ShamanElemental)]
        public static Composite CreateElementalNormalPull()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Common.CreateShamanDpsShieldBehavior(),

                        Totems.CreateTotemsBehavior(),
                        Spell.BuffSelf("Totem Mastery", req => !Me.HasAura("Ember Totem")), // Only one buff should need to be checked.

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

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
                                WoWPlayer nearby = ObjectManager.GetObjectsOfType<WoWPlayer>(true, false).FirstOrDefault(p => !p.IsMe && p.DistanceSqr <= 40 * 40);
                                if (nearby != null)
                                {
                                    Logger.WriteDiagnostic("NormalPull: fast pull since player {0} nearby @ {1:F1} yds", nearby.SafeName(), nearby.Distance);
                                    return true;
                                }
                                return false;
                                },
                            new PrioritySelector(
                                // have a big attack loaded up, so don't waste it
                                Spell.Cast("Earth Shock", ret => StyxWoW.Me.HasAura("Lightning Shield", 5)),
                                Spell.Buff("Flame Shock", true, req => SpellManager.HasSpell("Lava Burst")),
                                Spell.Cast("Earth Shock", ret => !SpellManager.HasSpell("Flame Shock"))
                                )
                            ),

                        // have a big attack loaded up, so don't waste it
                        Spell.Cast("Earth Shock", ret => StyxWoW.Me.HasAura("Lightning Shield", 5)),

                        // otherwise, start with Lightning Bolt so we can follow with an instant
                        // to maximize damage at initial aggro
                        Spell.Cast("Lightning Bolt"),

                        // we are moving so throw an instant of some type
                        Spell.Buff("Flame Shock", true, req => SpellManager.HasSpell("Lava Burst")),
                        Spell.Buff("Lava Burst", true, req => Me.GotTarget() && Me.CurrentTarget.HasMyAura("Flame Shock")),
                        Spell.Cast("Earth Shock")
                        )
                    )

                // Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanElemental)]
        public static Composite CreateElementalNormalCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),

                        Totems.CreateTotemsBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Dispelling.CreatePurgeEnemyBehavior("Purge"),

                        Common.CreateShamanDpsShieldBehavior(),

                        Spell.BuffSelf("Thunderstorm", ret => Unit.NearbyUnfriendlyUnits.Count( u => u.Distance < 10f ) >= 3),

                        Common.CastElementalBlast(),

                        Spell.CastOnGround("Lightning Surge Totem",
                            on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f).Location,
                            ret => ShamanSettings.UseLightningSurgeTotem
                                && SingularRoutine.CurrentWoWContext == WoWContext.Normal
                                && Unit.UnfriendlyUnits(8).Count() >= ShamanSettings.LightningSurgeTotemCount),
                        Spell.BuffSelf("Earth Elemental", ret => ShamanSettings.EarthElementalHealthPercent <= Me.HealthPercent),
                        Spell.BuffSelf("Totem Mastery", req => !Me.HasAura("Ember Totem")), // Only one buff should need to be checked.
                        Spell.Buff("Flame Shock", true, req => !Me.CurrentTarget.HasMyAura("Flame Shock") && Me.CurrentTarget.TimeToDeath(int.MaxValue) > 6),
                        Spell.Cast("Fire Elemental"),
                        Spell.Cast("Earth Shock", req => MaelstormDeficit <= 0),
                        Spell.Cast("Ascendance"),
						Spell.BuffSelf("Elemental Mastery"),
                        Spell.Cast("Lava Burst"),
						Spell.CastOnGround("Earthquake Totem",
							on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f).Location,
							req => Spell.UseAOE
								&& Me.CurrentTarget.Distance < 34
								&& (Me.ManaPercent > ShamanSettings.EarthquakeMaelPercent || Me.GetAuraTimeLeft("Lucidity") > TimeSpan.Zero)
								&& Unit.UnfriendlyUnitsNearTarget(10f).Count() >= ShamanSettings.EarthquakeCount - 1),
						Spell.Cast("Earth Shock", req => Me.CurrentMaelstrom >= 90),

                        // Artifact Weapon
                        new Decorator(
                            ret => ShamanSettings.UseArtifactOnlyInAoE && Unit.UnfriendlyUnitsNearTarget(10f).Count() > 1, // Focused toward Chain Lightning
                            new PrioritySelector(
                                Spell.Cast("Stormkeeper",
                                    ret =>
                                        ShamanSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown || !Common.HasTalent(ShamanTalents.Ascendance)
                                        || (ShamanSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.AtHighestDPSOpportunity && Common.HasTalent(ShamanTalents.Ascendance) && Spell.GetSpellCooldown("Ascendance") > TimeSpan.FromSeconds(15))
                                )
                            )
                        ),
                        Spell.Cast("Stormkeeper",
                            ret =>
                                ShamanSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.OnCooldown || !Common.HasTalent(ShamanTalents.Ascendance)
                                || (ShamanSettings.UseDPSArtifactWeaponWhen == UseDPSArtifactWeaponWhen.AtHighestDPSOpportunity && Common.HasTalent(ShamanTalents.Ascendance) && Spell.GetSpellCooldown("Ascendance") > TimeSpan.FromSeconds(15))
                        ),

                        Spell.Cast("Chain Lightning", ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() > 1 && !Unit.UnfriendlyUnitsNearTarget(10f).Any(u => u.IsCrowdControlled())),
                        Spell.Cast("Lightning Bolt")
                        )
                    )

                // Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                // Movement.CreateMoveToRangeAndStopBehavior(ret => Me.CurrentTarget, ret => NormalPullDistance)
                );
        }

        #endregion

        #region Diagnostics

        private static Composite CreateElementalDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    uint stks = 0;
                    string shield;

                    WoWAura aura = Me.GetAuraByName("Lightning Shield");
                    if (aura != null)
                    {
                        stks = aura.StackCount;
                        if (!Me.HasAura("Lightning Shield", (int)stks))
                            Logger.WriteDebug(Color.MediumVioletRed, "Inconsistancy Error:  have {0} stacks but Me.HasAura('Lightning Shield', {0}) was False!!!!", stks, stks);
                    }
                    else
                    {
                        aura = Me.GetAuraByName("Water Shield");
                        if (aura == null )
                        {
                            aura = Me.GetAuraByName("Earth Shield");
                            if (aura != null)
                                stks = aura.StackCount;
                        }
                    }

                    shield = aura == null ? "(null)" : aura.Name;

                    string line = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, shield={3}, stks={4}, moving={5}",
                        CompositeBuilder.CurrentBehaviorType.ToString(),
                        Me.HealthPercent,
                        Me.ManaPercent,
                        shield,
                        stks,
                        Me.IsMoving.ToYN()
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target == null)
                        line += ", target=(null)";
                    else
                        line += string.Format(", target={0} @ {1:F1} yds, th={2:F1}%, face={3} loss={4}, fs={5}",
                            target.SafeName(),
                            target.Distance,
                            target.HealthPercent,
                            Me.IsSafelyFacing(target).ToYN(),
                            target.InLineOfSpellSight.ToYN(),
                            (long)target.GetAuraTimeLeft("Flame Shock").TotalMilliseconds
                            );


                    if (Totems.Exist(WoWTotemType.Fire))
                        line += ", fire=" + Totems.GetTotem(WoWTotemType.Fire).Name;

                    if (Totems.Exist(WoWTotemType.Earth))
                        line += ", earth=" + Totems.GetTotem(WoWTotemType.Earth).Name;

                    if (Totems.Exist(WoWTotemType.Water))
                        line += ", water=" + Totems.GetTotem(WoWTotemType.Water).Name;

                    if (Totems.Exist(WoWTotemType.Air  ))
                        line += ", air=" + Totems.GetTotem(WoWTotemType.Air).Name;

                    Logger.WriteDebug(Color.Yellow, line);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }
}
