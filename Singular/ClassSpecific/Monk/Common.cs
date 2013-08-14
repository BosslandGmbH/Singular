using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Singular.Settings;
using Singular.Managers;

namespace Singular.ClassSpecific.Monk
{

    public enum SphereType
    {
        Chi = 3145,     // created by After Life
        Life = 3319,    // created by After Life
        Healing = 2866  // created by Healing Sphere spell
    }

    public class Common
    {
        private static MonkSettings MonkSettings { get { return SingularSettings.Instance.Monk(); } }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, (WoWSpec)int.MaxValue, WoWContext.All, 1)]
        public static Composite CreateMonkPreCombatBuffs()
        {
            return new PrioritySelector(
                // behaviors handling group buffing... handles special moments like
                // .. during the buff spam parade during battleground preparation, etc.
                // .. check our own buffs in PullBuffs and CombatBuffs if needed
                PartyBuff.BuffGroup("Legacy of the White Tiger"),
                PartyBuff.BuffGroup("Legacy of the Emperor")
                );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Monk, (WoWSpec) int.MaxValue, WoWContext.Normal | WoWContext.Battlegrounds )]
        public static Composite CreateMonkLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.BuffSelf("Dematerialize"),
                    Spell.BuffSelf("Nimble Brew", ret => Me.Stunned || Me.Fleeing || Me.HasAuraWithMechanic( WoWSpellMechanic.Horrified )),
                    Spell.BuffSelf("Dampen Harm", ret => Me.Stunned && Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any()),
                    Spell.BuffSelf("Tiger's Lust", ret => Me.Rooted && !Me.HasAuraWithEffect( WoWApplyAuraType.ModIncreaseSpeed))
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, (WoWSpec)int.MaxValue, WoWContext.All, 2)]
        public static Composite CreateMonkCombatBuffs()
        {
            return new PrioritySelector(
                
                Spell.BuffSelf( "Legacy of the White Tiger"),
                Spell.BuffSelf( "Legacy of the Emperor"),

                new Decorator(
                    req => !Unit.IsTrivial(Me.CurrentTarget),
                    new PrioritySelector(               
                        // check our individual buffs here
                        Spell.Buff("Disable", ret => Me.GotTarget && Me.CurrentTarget.IsPlayer && Me.CurrentTarget.ToPlayer().IsHostile && !Me.CurrentTarget.HasAuraWithEffect( WoWApplyAuraType.ModDecreaseSpeed)),

                        Spell.BuffSelf( "Ring of Peace", 
                            ret => Me.GotTarget 
                                && Me.CurrentTarget.SpellDistance() < 8
                                && (Me.CurrentTarget.IsPlayer || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() > 1))
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        [Behavior(BehaviorType.Rest, WoWClass.Monk, WoWSpec.MonkWindwalker)]
        public static Composite CreateMonkRest()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.HasAura("Drink") && !StyxWoW.Me.HasAura("Food"),
                    new PrioritySelector(
                        // pickup free heals from Life Spheres
                        new Decorator(
                            ret => Me.HealthPercent < 95 && AnySpheres(SphereType.Life, MonkSettings.SphereDistanceAtRest ),
                            CreateMoveToSphereBehavior( SphereType.Life, MonkSettings.SphereDistanceAtRest )
                            ),
                        // pickup free chi from Chi Spheres
                        new Decorator(
                            ret => Me.CurrentChi < Me.MaxChi && AnySpheres(SphereType.Chi, MonkSettings.SphereDistanceAtRest),
                            CreateMoveToSphereBehavior(SphereType.Chi, MonkSettings.SphereDistanceAtRest)
                            ),

                        // heal ourselves... confirm we have spell and enough energy already or waiting for energy regen will
                        // .. still be faster than eating
                        new Decorator(
                            ret => Me.HealthPercent >= MonkSettings.RestHealingSphereHealth
                                && SpellManager.HasSpell("Healing Sphere") 
                                && (Me.CurrentEnergy > 40 || Spell.EnergyRegenInactive() >= 10),
                            new Sequence(
                                // in Rest only, wait up to 4 seconds for Energy Regen and Spell Cooldownb 
                                new Wait(4, ret => Me.Combat || (Me.CurrentEnergy >= 40 && Spell.GetSpellCooldown("Healing Sphere") == TimeSpan.Zero), new ActionAlwaysSucceed()),
                                Common.CreateHealingSphereBehavior(Math.Max(80, SingularSettings.Instance.MinHealth)),
                                Helpers.Common.CreateWaitForLagDuration(ret => Me.Combat)
                                )
                            )
                        )
                    ),

                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour( null, "Resuscitate")
                );
        }
        
        /// <summary>
        /// a SpellManager.CanCast replacement to allow checking whether a spell can be cast 
        /// without checking if another is in progress, since Monks need to cast during
        /// a channeled cast already in progress
        /// </summary>
        /// <param name="name">name of the spell to cast</param>
        /// <param name="unit">unit spell is targeted at</param>
        /// <returns></returns>
        public static bool CanCastLikeMonk(string name, WoWUnit unit)
        {
            WoWSpell spell;
            if (!SpellManager.Spells.TryGetValue(name, out spell))
            {
                return false;
            }

            uint latency = StyxWoW.WoWClient.Latency * 2;
            TimeSpan cooldownLeft = spell.CooldownTimeLeft;
            if (cooldownLeft != TimeSpan.Zero && cooldownLeft.TotalMilliseconds >= latency)
                return false;

            if (spell.IsMeleeSpell)
            {
                if (!unit.IsWithinMeleeRange)
                {
                    Logger.WriteDebug("CanCastSpell: cannot cast wowSpell {0} @ {1:F1} yds", spell.Name, unit.Distance);
                    return false;
                }
            }
            else if (spell.IsSelfOnlySpell)
            {
                ;
            }
            else if (spell.HasRange)
            {
                if (unit == null)
                {
                    return false;
                }

                if (unit.Distance < spell.MinRange)
                {
                    Logger.WriteDebug("SpellCast: cannot cast wowSpell {0} @ {1:F1} yds - minimum range is {2:F1}", spell.Name, unit.Distance, spell.MinRange);
                    return false;
                }

                if (unit.Distance >= spell.MaxRange)
                {
                    Logger.WriteDebug("SpellCast: cannot cast wowSpell {0} @ {1:F1} yds - maximum range is {2:F1}", spell.Name, unit.Distance, spell.MaxRange);
                    return false;
                }
            }

            if (Me.CurrentPower < spell.PowerCost)
            {
                Logger.WriteDebug("CanCastSpell: wowSpell {0} requires {1} power but only {2} available", spell.Name, spell.PowerCost, Me.CurrentMana);
                return false;
            }

            if (Me.IsMoving && spell.CastTime > 0)
            {
                Logger.WriteDebug("CanCastSpell: wowSpell {0} is not instant ({1} ms cast time) and we are moving", spell.Name, spell.CastTime);
                return false;
            }

            return true;
        }


        /// <summary>
        ///   Creates a behavior to cast a spell by name, with special requirements, on a specific unit. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name="checkMovement"></param>
        /// <param name = "onUnit">The on unit.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite CastLikeMonk(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new PrioritySelector(
                new Decorator(ret => requirements != null && onUnit != null && requirements(ret) && onUnit(ret) != null && name != null && CanCastLikeMonk(name, onUnit(ret)),
                    new PrioritySelector(
                        new Sequence(
                            // cast the spell
                            new Action(ret =>
                            {
                                wasMonkSpellQueued = (Spell.GcdActive || Me.IsCasting || Me.ChanneledSpell != null);
                                Logger.Write(Color.Aquamarine, string.Format("*{0} on {1} at {2:F1} yds at {3:F1}%", name, onUnit(ret).SafeName(), onUnit(ret).Distance, onUnit(ret).HealthPercent));
                                SpellManager.Cast(name, onUnit(ret));
                            }),
                            // if spell was in progress before cast (we queued this one) then wait in progress one to finish
                            new WaitContinue( 
                                new TimeSpan(0, 0, 0, 0, (int) StyxWoW.WoWClient.Latency << 1),
                                ret => !wasMonkSpellQueued || !(Spell.GcdActive || Me.IsCasting || Me.ChanneledSpell != null),
                                new ActionAlwaysSucceed()
                                ),
                            // wait for this cast to appear on the GCD or Spell Casting indicators
                            new WaitContinue(
                                new TimeSpan(0, 0, 0, 0, (int) StyxWoW.WoWClient.Latency << 1),
                                ret => Spell.GcdActive || Me.IsCasting || Me.ChanneledSpell != null,
                                new ActionAlwaysSucceed()
                                )
                            )
                        )
                    )
                );
        }

        private static bool wasMonkSpellQueued = false;

        public static WoWObject FindClosestSphere(SphereType typ, float range)
        {
            range *= range;
            return ObjectManager.ObjectList
                .Where(o => o.Type == WoWObjectType.AiGroup && o.Entry == (uint)typ && o.DistanceSqr < range && !Blacklist.Contains(o.Guid, BlacklistFlags.Combat))
                .OrderBy( o => o.DistanceSqr )
                .FirstOrDefault();
        }

        public static bool AnySpheres(SphereType typ, float range)
        {
            WoWObject sphere = FindClosestSphere(typ, range);
            return sphere != null && sphere.Distance < 20;
        }

        public static WoWPoint FindSphereLocation(SphereType typ, float range)
        {
            WoWObject sphere = FindClosestSphere(typ, range);
            return sphere != null ? sphere.Location : WoWPoint.Empty;
        }

        private static ulong guidSphere = 0;
        private static WoWPoint locSphere = WoWPoint.Empty;
        private static DateTime timeAbortSphere = DateTime.Now;

        public static Composite CreateMoveToSphereBehavior(SphereType typ, float range)
        {
            return new Decorator( 
                ret => MonkSettings.MoveToSpheres && MovementManager.IsClassMovementAllowed,

                new PrioritySelector(

                    // check we haven't gotten out of range due to fall / pushback / port / etc
                    new Decorator( 
                        ret => guidSphere != 0 && Me.Location.Distance(locSphere) > range,
                        new Action(ret => { guidSphere = 0; locSphere = WoWPoint.Empty; })
                        ),

                    // validate the sphere we are moving to
                    new Action(ret =>
                    {
                        WoWObject sph = FindClosestSphere(typ, range);
                        if (sph == null)
                        {
                            guidSphere = 0; locSphere = WoWPoint.Empty;
                            return RunStatus.Failure;
                        }

                        if (sph.Guid == guidSphere)
                            return RunStatus.Failure;

                        guidSphere = sph.Guid;
                        locSphere = sph.Location;
                        timeAbortSphere = DateTime.Now + TimeSpan.FromSeconds(5);
                        Logger.WriteDebug("MoveToSphere: Moving {0:F1} yds to {1} Sphere {2} @ {3}", sph.Distance, typ, guidSphere, locSphere);
                        return RunStatus.Failure;
                    }),

                    new Decorator( 
                        ret => DateTime.Now > timeAbortSphere, 
                        new Action( ret => {
                            Logger.WriteDebug("MoveToSphere: blacklisting timed out {0} sphere {1} at {2}", typ, guidSphere, locSphere);
                            Blacklist.Add(guidSphere, BlacklistFlags.Combat, TimeSpan.FromMinutes(5));
                            })
                        ),

                    // move to the sphere if out of range
                    new Decorator(
                        ret => guidSphere != 0 && Me.Location.Distance(locSphere) > 1,
                        Movement.CreateMoveToLocationBehavior(ret => locSphere, true, ret => 0f)
                        ),

                    // pause briefly until its consumed
                    new Wait( 
                        1, 
                        ret => {  
                            WoWObject sph = FindClosestSphere(typ, range);
                            return sph == null || sph.Guid != guidSphere ;
                            },
                        new Action( r => { return RunStatus.Failure; } )
                        ),
                        
                    // still exist?  black list it then
                    new Decorator( 
                        ret => {  
                            WoWObject sph = FindClosestSphere(typ, range);
                            return sph != null && sph.Guid == guidSphere ;
                            },
                        new Action( ret => {
                            Logger.WriteDebug("MoveToSphere: blacklisting unconsumed {0} sphere {1} at {2}", typ, guidSphere, locSphere);
                            Blacklist.Add(guidSphere, BlacklistFlags.Combat, TimeSpan.FromMinutes(5));
                            })
                        )
                    )
                );
        }

        public static Sequence CreateHealingSphereBehavior( int sphereBelowHealth)
        {
            // healing sphere keeps spell on cursor for up to 3 casts... need to stop targeting after 1
            return new Sequence(
                Spell.CastOnGround("Healing Sphere",
                    ctx => Me.Location,
                    ret => Me.HealthPercent < sphereBelowHealth 
                        && (Me.PowerType != WoWPowerType.Mana)
                        && !Common.AnySpheres(SphereType.Healing, 1f),
                    false),
                new WaitContinue( TimeSpan.FromMilliseconds(500), ret => Spell.GetPendingCursorSpell != null, new ActionAlwaysSucceed()),
                new Action(ret => Lua.DoString("SpellStopTargeting()")),
                new WaitContinue( 
                    TimeSpan.FromMilliseconds(750), 
                    ret => Me.Combat || (Spell.GetSpellCooldown("Healing Sphere") == TimeSpan.Zero && !Common.AnySpheres(SphereType.Healing, 1f)), 
                    new ActionAlwaysSucceed()
                    )
                );
        }

        /// <summary>
        /// cast grapple weapon, dealing with issues of mobs immune to that spell
        /// </summary>
        /// <returns></returns>
        public static Composite CreateGrappleWeaponBehavior()
        {
            if (!MonkSettings.UseGrappleWeapon)
                return new ActionAlwaysFail();

            return new Throttle(15,
                Spell.Cast("Grapple Weapon", on =>
                {
                    if (Spell.IsSpellOnCooldown("Grapple Weapon"))
                        return null;

                    WoWUnit unit = Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(
                        u => u.SpellDistance() < 40
                            && !Me.CurrentTarget.Disarmed
                            && !Me.CurrentTarget.IsCrowdControlled()
                            && Me.IsSafelyFacing(u, 150)
                            );
                    return unit;
                })
                );
        }
    }

    public enum MonkTalents
    {
        Celerity = 1,
        TigersLust,
        Momumentum,
        ChiWave,
        ZenSphere,
        ChiBurst,
        PowerStrikes,
        Ascension,
        ChiBrew,
        RingOfPeace,
        ChargingOxWave,
        LegSweep,
        HealingElixirs,
        DampenHarm,
        DiffuseMagic,
        RushingJadeWind,
        InvokeXuenTheWhiteTiger,
        ChiTorpedo
    }

}