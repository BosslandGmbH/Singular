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
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk)]
        public static Composite CreateMonkPreCombatBuffs()
        {
            return new PrioritySelector(

                    PartyBuff.BuffGroup(
                        "Legacy of the Emperor",
                        ret => true,
                        "Legacy of the White Tiger"),

                    PartyBuff.BuffGroup(
                        "Legacy of the White Tiger",
                        ret => true,
                        "Legacy of the Emperor")
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Monk )]
        public static Composite CreateMonkRest()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Me.HealthPercent < 95 && AnySpheres(SphereType.Life),
                    CreateMoveToSphereBehavior( SphereType.Life)
                    ),
                new Decorator(
                    ret => Me.CurrentChi < Me.MaxChi && AnySpheres(SphereType.Chi),
                    CreateMoveToSphereBehavior( SphereType.Chi )
                    ),
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour(),
                // Can we res people?
                Spell.Resurrect("Resuscitate")
                );
        }
        
        public enum Talents
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
            DeadlyReach,
            ChargingOxWave,
            LegSweep,
            HealingElixirs,
            DampenHarm,
            DiffuseMagic,
            RushingJadeWind,
            InvokeXuenTheWhiteTiger,
            ChiTorpedo
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

                        // set context to whether currently busy with GCD or Cast
                        ctx => (bool)(SpellManager.GlobalCooldown || Me.IsCasting || Me.ChanneledSpell != null),

                        new Sequence(
                            // cast the spell
                            new Action(ret =>
                            {
                                Logger.Write(Color.Aquamarine, string.Format("*{0} on {1} at {2:F1} yds at {3:F1}%", name, onUnit(ret).SafeName(), onUnit(ret).Distance, onUnit(ret).HealthPercent));
                                SpellManager.Cast(name, onUnit(ret));
                            }),
                            // if spell was in progress before cast (we queued this one) then wait in progress one to finish
                            new WaitContinue( 
                                new TimeSpan(0, 0, 0, 0, (int) StyxWoW.WoWClient.Latency << 1),
                                ret => !((bool)ret) || !(SpellManager.GlobalCooldown || Me.IsCasting || Me.ChanneledSpell != null),
                                new ActionAlwaysSucceed()
                                ),
                            // wait for this cast to appear on the GCD or Spell Casting indicators
                            new WaitContinue(
                                new TimeSpan(0, 0, 0, 0, (int) StyxWoW.WoWClient.Latency << 1),
                                ret => SpellManager.GlobalCooldown || Me.IsCasting || Me.ChanneledSpell != null,
                                new ActionAlwaysSucceed()
                                )
                            )
                        )
                    )
                );
        }


        public static WoWObject FindSphere(SphereType typ, float range = 20f)
        {
            range *= range;
            return ObjectManager.ObjectList
                .Where(o => o.Type == WoWObjectType.AiGroup && o.Entry == (uint)typ && o.DistanceSqr < range)
                .OrderBy( o => o.DistanceSqr )
                .FirstOrDefault();
        }

        public static bool AnySpheres(SphereType typ, float range = 20f)
        {
            WoWObject sphere = FindSphere(typ, range);
            return sphere != null && sphere.Distance < 20;
        }

        public static WoWPoint FindSphereLocation(SphereType typ, float range = 20f)
        {
            WoWObject sphere = FindSphere(typ, range);
            return sphere != null ? sphere.Location : WoWPoint.Empty;
        }

        private static ulong lastSphere = 0;
        public static Composite CreateMoveToSphereBehavior(SphereType typ)
        {
            return new Decorator( 
                ret => !SingularSettings.Instance.DisableAllMovement,
                new Sequence(
                    new Action(ret =>
                    {
                        WoWObject sph = FindSphere(typ);
                        if (sph != null && sph.Guid != lastSphere)
                        {
                            lastSphere = sph.Guid;
                            Logger.WriteDebug("CreateMonkRest: Moving {0:F1} yds to {1} Sphere {2}", sph.Distance, typ.ToString(), lastSphere );
                        }
                    }),
                    Movement.CreateMoveToLocationBehavior(ret => FindSphereLocation(SphereType.Life), true, ret => 0f),
                    new ActionAlwaysSucceed()
                    )
                );
        }

    }
}