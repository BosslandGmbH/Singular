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
using Styx.Common.Helpers;

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
        public static bool HasTalent(MonkTalents tal) { return TalentManager.IsSelected((int)tal); }


        [Behavior(BehaviorType.Initialize, WoWClass.Monk)]
        public static Composite CreateMonkInitialize()
        {
            return null;
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Monk, (WoWSpec)int.MaxValue, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateMonkLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.BuffSelf("Nimble Brew", ret => Me.Stunned || Me.Fleeing || Me.HasAuraWithMechanic( WoWSpellMechanic.Horrified )),
                    Spell.BuffSelf("Dampen Harm", ret => Me.Stunned && Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any()),
                    Spell.BuffSelf("Tiger's Lust", ret => Me.Rooted && !Me.HasAuraWithEffect( WoWApplyAuraType.ModIncreaseSpeed)),
                    Spell.BuffSelf("Life Cocoon", req => Me.Stunned && TalentManager.HasGlyph("Life Cocoon") && Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any())
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, (WoWSpec)int.MaxValue, WoWContext.All, 2)]
        public static Composite CreateMonkCombatBuffs()
        {
            UnitSelectionDelegate onunitRop;

            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                onunitRop = on => Unit.UnfriendlyUnits(8).Any(u => u.CurrentTargetGuid == Me.Guid && u.IsPlayer) ? Me : Group.Healers.FirstOrDefault( h => h.SpellDistance() < 40 && Unit.UnfriendlyUnits((int) h.Distance2D + 8).Any( u => u.CurrentTargetGuid == h.Guid && u.SpellDistance(h) < 8));
            else // Instances and Normal - just protect self
                onunitRop = on => Unit.UnfriendlyUnits(8).Count(u => u.CurrentTargetGuid == Me.Guid) > 1 ? Me : null;

            return new PrioritySelector(
                
                PartyBuff.BuffGroup( "Legacy of the White Tiger", req => Me.Specialization == WoWSpec.MonkBrewmaster || Me.Specialization == WoWSpec.MonkWindwalker),
                PartyBuff.BuffGroup("Legacy of the Emperor", req => Me.Specialization == WoWSpec.MonkMistweaver),

                new Decorator(
                    req => !Unit.IsTrivial(Me.CurrentTarget),
                    new PrioritySelector(               
                        // check our individual buffs here
                        Spell.Buff("Disable", ret => Me.GotTarget() && Me.CurrentTarget.IsPlayer && Me.CurrentTarget.ToPlayer().IsHostile && !Me.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModDecreaseSpeed)),
                        Spell.Buff("Ring of Peace", onunitRop)
                        )
                    ),

                CreateChiBurstBehavior()
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        [Behavior(BehaviorType.Rest, WoWClass.Monk, WoWSpec.MonkWindwalker)]
        public static Composite CreateMonkRest()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.HasAnyAura("Drink", "Food", "Refreshment"),
                    new PrioritySelector(
                        // pickup free heals from Life Spheres
                        new Decorator(
                            ret => Me.HealthPercent < 95 && AnySpheres(SphereType.Life, SingularSettings.Instance.SphereDistanceAtRest ),
                            CreateMoveToSphereBehavior(SphereType.Life, SingularSettings.Instance.SphereDistanceAtRest)
                            ),
                        // pickup free chi from Chi Spheres
                        new Decorator(
                            ret => Me.CurrentChi < Me.MaxChi && AnySpheres(SphereType.Chi, SingularSettings.Instance.SphereDistanceAtRest),
                            CreateMoveToSphereBehavior(SphereType.Chi, SingularSettings.Instance.SphereDistanceAtRest)
                            )
                        )
                    ),

                Common.CreateMonkDpsHealBehavior(),

                // Rest up! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour( "Surging Mist", "Resuscitate")
                );
        }

        public static Composite CreateMonkDpsHealBehavior()
        {
            Composite offheal;
            if (!SingularSettings.Instance.DpsOffHealAllowed)
                offheal = new ActionAlwaysFail();
            else
            {
                offheal = new Decorator(
                    ret => HealerManager.ActingAsOffHealer,
                    CreateMonkOffHealBehavior()
                    );
            }

            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    new Decorator(
                        ret => !Me.Combat
                            && !Me.IsMoving
                            && Me.HealthPercent <= 85  // not redundant... this eliminates unnecessary GetPredicted... checks
                            && SpellManager.HasSpell("Surging Mist")
                            && Me.PredictedHealthPercent(includeMyHeals: true) < 85,
                        new PrioritySelector(
                            new Sequence(
                                ctx => (float)Me.HealthPercent,
                                new Action(r => Logger.WriteDebug("Surging Mist: {0:F1}% Predict:{1:F1}% and moving:{2}, cancast:{3}", (float)r, Me.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving, Spell.CanCastHack("Surging Mist", Me, skipWowCheck: false))),
                                Spell.Cast(
                                    "Surging Mist",
                                    mov => true,
                                    on => Me,
                                    req => true,
                                    cancel => Me.HealthPercent > 85
                                    ),
                                new WaitContinue(TimeSpan.FromMilliseconds(500), until => !Me.IsCasting && Me.HealthPercent > (1.1 * ((float)until)), new ActionAlwaysSucceed()),
                                new Action(r => Logger.WriteDebug("Surging Mist: After Heal Attempted: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true)))
                                ),
                            Spell.Buff( "Expel Harm", on => Me, req => Me.HealthPercent < MonkSettings.ExpelHarmHealth)
                            )
                        ),

                    new Decorator(
                        ret => Me.Combat,

                        new PrioritySelector(

                            // add buff / shield here
                            Spell.HandleOffGCD(
                                new Throttle(
                                    3, 
                                    Spell.Cast("Tigereye Brew", ctx => Me, ret => Me.HealthPercent < MonkSettings.TigereyeBrewHealth && Me.HasAura("Tigereye Brew", 1))
                                    )
                                ),
                            
                            // save myself if possible
                            new Decorator(
                                ret => (!Me.IsInGroup() || Battlegrounds.IsInsideBattleground)
                                    && !Me.IsMoving 
                                    && Me.HealthPercent < MonkSettings.SurgingMist
                                    && Me.PredictedHealthPercent(includeMyHeals: true) < MonkSettings.SurgingMist,
                                new PrioritySelector(
                                    new Sequence(
                                        ctx => (float)Me.HealthPercent,
                                        new Action(r => Logger.WriteDebug("Surging Mist: {0:F1}% Predict:{1:F1}% and moving:{2}, cancast:{3}", (float)r, Me.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving, Spell.CanCastHack("Surging Mist", Me, skipWowCheck: false))),
                                        Spell.Cast(
                                            "Surging Mist",
                                            mov => true,
                                            on => Me,
                                            req => true,
                                            cancel => Me.HealthPercent > 85
                                            ),
                                        new WaitContinue(TimeSpan.FromMilliseconds(500), until => !Me.IsCasting && Me.HealthPercent > (1.1 * ((float)until)), new ActionAlwaysSucceed()),
                                        new Action(r => Logger.WriteDebug("Surging Mist: After Heal Attempted: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true)))
                                        ),
                                    new Action(r => Logger.WriteDebug("Surging Mist: After Heal Skipped: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true)))
                                    )
                                )
                            )
                        ),

                    offheal
                    )
                );


        }

        private static WoWUnit _moveToHealUnit = null;

        public static Composite CreateMonkOffHealBehavior()
        {
            HealerManager.NeedHealTargeting = SingularSettings.Instance.DpsOffHealAllowed;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();
            int cancelHeal = (int)Math.Max(SingularSettings.Instance.IgnoreHealTargetsAboveHealth, MonkSettings.OffHealSettings.SurgingMist);

            bool moveInRange = (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds);

            Logger.WriteDebugInBehaviorCreate("Monk Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);
/*
            int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
                behavs.AddBehavior(dispelPriority, "Cleanse Spirit", null, Dispelling.CreateDispelBehavior());
*/
            #region Save the Group


            #endregion

            #region AoE Heals

                behavs.AddBehavior(
                    Mistweaver.HealthToPriority(MonkSettings.MistHealSettings.ChiWave) + 400,
                    String.Format("Chi Wave on {0} targets @ {1}%", MonkSettings.OffHealSettings.CountChiWaveTalent, MonkSettings.OffHealSettings.ChiWaveTalent),
                    "Chi Wave",
                    CreateClusterHeal("Chi Wave", ClusterType.Cone, MonkSettings.OffHealSettings.ChiWaveTalent, MonkSettings.OffHealSettings.CountChiWaveTalent, 40)
                    );

                behavs.AddBehavior(
                    Mistweaver.HealthToPriority(MonkSettings.MistHealSettings.ChiBurstTalent) + 400,
                    String.Format("Chi Burst on {0} targets @ {1}%", MonkSettings.OffHealSettings.CountChiBurstTalent, MonkSettings.OffHealSettings.ChiBurstTalent),
                    "Chi Burst",
                    CreateClusterHeal("Chi Burst", ClusterType.Cone, MonkSettings.OffHealSettings.ChiBurstTalent, MonkSettings.OffHealSettings.CountChiBurstTalent, 40)
                    );


            #endregion

            #region Single Target Heals

            behavs.AddBehavior(Mistweaver.HealthToPriority(MonkSettings.OffHealSettings.SurgingMist),
                string.Format("Surging Mist @ {0}%", MonkSettings.OffHealSettings.SurgingMist),
                "Surging Mist",
                Spell.Cast("Surging Mist",
                    mov => true,
                    on => (WoWUnit)on,
                    req => ((WoWUnit)req).PredictedHealthPercent(includeMyHeals: true) < MonkSettings.OffHealSettings.SurgingMist,
                    cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                    )
                );

            #endregion

            behavs.OrderBehaviors();

            if (Singular.Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Heal )
                behavs.ListBehaviors();

            return new PrioritySelector(
                ctx => HealerManager.FindLowestHealthTarget(), // HealerManager.Instance.FirstUnit,

                new Decorator(
                    ret => ret != null && (Me.Combat || ((WoWUnit)ret).Combat || ((WoWUnit)ret).PredictedHealthPercent() <= 99),

                    new PrioritySelector(
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                behavs.GenerateBehaviorTree(),

                                new Decorator(
                                    ret => moveInRange,
                                    new Sequence(
                                        new Action(r => _moveToHealUnit = (WoWUnit)r),
                                        new PrioritySelector(
                                            Movement.CreateMoveToLosBehavior(on => _moveToHealUnit),
                                            Movement.CreateMoveToUnitBehavior(on => _moveToHealUnit, 30f, 25f)
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static Composite CreateClusterHeal( string spell, ClusterType ct, int health, int range, int minCount)
        {
            return new Decorator(
                req => (req as WoWUnit).HealthPercent < health,
                new PrioritySelector(

                    ctx => Clusters.GetBestUnitForCluster(HealerManager.Instance.TargetList.Where(u => u.HealthPercent <= health), ct, range),

                    new Decorator(
                        req =>
                        {
                            if (req == null)
                                return false;

                            if (!Spell.CanCastHack(spell, (WoWUnit)req))
                                return false;

                            if (!((WoWUnit)req).InLineOfSpellSight)
                                return false;

                            int count = Clusters.GetClusterCount(
                                (WoWUnit)req,
                                HealerManager.Instance.TargetList.Where(u => u.HealthPercent <= health),
                                ct,
                                range
                                );

                            if (count < minCount)
                                return false;

                            Logger.Write( LogColor.Hilite, "^Casting {0} on {1} in attempt to hit {2} members below {3}%", spell, ((WoWUnit)req).SafeName(), count, health);
                            return true;
                        },

                        new Sequence(
                            new DecoratorContinue(
                                req => !Me.IsSafelyFacing((WoWUnit)req, 5f),
                                new PrioritySelector(
                                    new Sequence(
                                        new Action(r => Logger.WriteDiagnostic("{0}: trying to face {1}", spell, ((WoWUnit)r).SafeName())),
                                        new Action(r => ((WoWUnit)r).Face()),
                                        new Wait(TimeSpan.FromMilliseconds(1500), until => Me.IsSafelyFacing((WoWUnit)until, 5f), new ActionAlwaysSucceed()),
                                        new Action(r => Logger.Write("{0}:  succeeded at facing {1}", spell, ((WoWUnit)r).SafeName()))
                                        ),
                                    new Action(r =>
                                    {
                                        Logger.Write("{0}:  failed at facing {1}", spell, ((WoWUnit)r).SafeName());
                                        return RunStatus.Failure;
                                    })
                                    )
                                ),

                            Spell.Cast(spell, mov => true, on => (WoWUnit)on, req => true, cancel => false)
                            )
                        )
                    )
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

            uint latency = SingularRoutine.Latency * 2;
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

            if (Me.IsMoving && spell.CastTime > 0)
            {
                Logger.WriteDebug("CanCastSpell: wowSpell {0} is not instant ({1} ms cast time) and we are moving", spell.Name, spell.CastTime);
                return false;
            }

			if (!spell.CanCast)
			{
				Logger.WriteDebug("CanCastSpell: wowSpell {0} cannot be cast according to WoW", spell.Name);
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
                                Spell.CastPrimative(name, onUnit(ret));
                            }),
                            // if spell was in progress before cast (we queued this one) then wait in progress one to finish
                            new WaitContinue( 
                                new TimeSpan(0, 0, 0, 0, (int) SingularRoutine.Latency << 1),
                                ret => !wasMonkSpellQueued || !(Spell.GcdActive || Me.IsCasting || Me.ChanneledSpell != null),
                                new ActionAlwaysSucceed()
                                ),
                            // wait for this cast to appear on the GCD or Spell Casting indicators
                            new WaitContinue(
                                new TimeSpan(0, 0, 0, 0, (int) SingularRoutine.Latency << 1),
                                ret => Spell.GcdActive || Me.IsCasting || Me.ChanneledSpell != null,
                                new ActionAlwaysSucceed()
                                )
                            )
                        )
                    )
                );
        }
         
        private static bool wasMonkSpellQueued = false;

        // delay casting instant ranged abilities if we just cast Roll/FSK
        public readonly static WaitTimer RollTimer = new WaitTimer(TimeSpan.FromMilliseconds(1500));


        public static Composite CreateMonkCloseDistanceBehavior( SimpleIntDelegate minDist = null, UnitSelectionDelegate onUnit = null, SimpleBooleanDelegate canReq = null)
        {
            /*
            new Decorator(
                unit => (unit as WoWUnit).SpellDistance() > 10
                    && Me.IsSafelyFacing(unit as WoWUnit, 5f),
                Spell.Cast("Roll")
                )
            */

            bool hasFSKGlpyh = TalentManager.HasGlyph("Flying Serpent Kick");
            bool hasTigersLust = HasTalent(MonkTalents.TigersLust);

            if (minDist == null)
                minDist = min => Me.Combat ? 10 : 12;

            if (onUnit == null)
                onUnit = on => Me.CurrentTarget;

            if (canReq == null)
                canReq = req => true;

            return new Throttle( 1,
                new PrioritySelector(
                    ctx => onUnit(ctx),
                    new Decorator(
                        req => {
                            if (!MovementManager.IsClassMovementAllowed)
                                return false;

                            if (!canReq(req))
                                return false;

                            float dist = Me.SpellDistance(req as WoWUnit);
                            if ( dist <= minDist(req))
                                return false;

                            if ((req as WoWUnit).IsAboveTheGround())
                                return false;

                            float facingPrecision = (req as WoWUnit).SpellDistance() < 15 ? 6f : 4f;
                            if (!Me.IsSafelyFacing(req as WoWUnit, facingPrecision))
                                return false;

                            bool isObstructed = Movement.MeshTraceline(Me.Location, (req as WoWUnit).Location);
                            if (isObstructed == true)
                                return false;

                            return true;
                            },
                        new PrioritySelector(
                            Spell.BuffSelf(
                                "Tiger's Lust", 
                                req => hasTigersLust 
                                    && !Me.HasAuraWithEffect(WoWApplyAuraType.ModIncreaseSpeed)
                                    && Me.HasAuraWithEffect(WoWApplyAuraType.ModRoot, WoWApplyAuraType.ModDecreaseSpeed)
                                ),

                            new Sequence( 
                                Spell.Cast(
                                    "Flying Serpent Kick", 
                                    on => (WoWUnit) on,
                                    ret => TalentManager.CurrentSpec == WoWSpec.MonkWindwalker
                                        && !Me.Auras.ContainsKey("Flying Serpent Kick")
                                        && ((ret as WoWUnit).SpellDistance() > 25 || Spell.IsSpellOnCooldown("Roll"))
                                    ),
                                /* wait until in progress */
                                new PrioritySelector(
                                    new Wait(
                                        TimeSpan.FromMilliseconds(750),
                                        until => Me.Auras.ContainsKey("Flying Serpent Kick"),
                                        new Action( r => Logger.WriteDebug("CloseDistance: Flying Serpent Kick detected towards {0} @ {1:F1} yds in progress", (r as WoWUnit).SafeName(), (r as WoWUnit).SpellDistance()))
                                        ),
                                    new Action( r => {
                                        Logger.WriteDebug("CloseDistance: failure - did not see Flying Serpent Kick aura appear - lag?");
                                        return RunStatus.Failure;
                                        })
                                    ),

                                /* cancel when in range */
                                new Wait( 
                                    TimeSpan.FromMilliseconds(2500),
                                    until => {
                                        if (!Me.Auras.ContainsKey("Flying Serpent Kick"))
                                        {
                                            Logger.WriteDebug("CloseDistance: Flying Serpent Kick completed on {0} @ {1:F1} yds and {2} behind me", (until as WoWUnit).SafeName(), (until as WoWUnit).SpellDistance(), (until as WoWUnit).IsBehind(Me) ? "IS" : "is NOT");
                                            return true;
                                        }

                                        if (!hasFSKGlpyh)
                                        {
                                            SpellFindResults sfr;
                                            SpellManager.FindSpell("Flying Serpent Kick", out sfr);

                                            if (((until as WoWUnit).IsWithinMeleeRange || (until as WoWUnit).SpellDistance() < 8f))
                                            {
                                                Logger.Write(LogColor.Cancel, "/cancel Flying Serpent Kick in melee range of {0} @ {1:F1} yds", (until as WoWUnit).SafeName(), (until as WoWUnit).SpellDistance());
                                                //Spell.CastPrimative("Flying Serprent Kick");
                                                // Lua.DoString("CastSpellByID(" + sfr.Original.Id + ")");
                                                return true;
                                            }
                                            else if ((until as WoWUnit).IsBehind(Me))
                                            {
                                                Logger.Write(LogColor.Cancel, "/cancel Flying Serpent Kick flew past {0} @ {1:F1} yds", (until as WoWUnit).SafeName(), (until as WoWUnit).SpellDistance());
                                                //Spell.CastPrimative("Flying Serprent Kick");
                                                //Lua.DoString("CastSpellByID(" + sfr.Original.Id + ")");
                                                return true;
                                            }
                                        }

                                        return false;
                                        },
                                    new PrioritySelector(
                                        new Decorator(
                                            req => !Me.Auras.ContainsKey("Flying Serpent Kick"),
                                            new ActionAlwaysSucceed()
                                            ),
                                        new Sequence(
                                            new Action( r => {
                                                if (hasFSKGlpyh)
                                                {
                                                    Logger.WriteDebug("CloseDistance: FSK is glyphed, should not be here - notify developer!");
                                                }
                                                else
                                                {
                                                    Logger.WriteDebug("CloseDistance: casting Flying Serpent Kick to cancel");
                                                    Spell.CastPrimative(101545);
                                                }
                                            }),
                                            /* wait until cancel takes effect */
                                            new PrioritySelector(
                                                new Wait(
                                                    TimeSpan.FromMilliseconds(450),
                                                    until => !Me.Auras.ContainsKey("Flying Serpent Kick"),
                                                    new Action( r => Logger.WriteDebug("CloseDistance: Flying Serpent Kick complete, landed {0:F1} yds from {1}", (r as WoWUnit).SpellDistance(), (r as WoWUnit).SafeName()))
                                                    ),
                                                new Action( r => {
                                                    Logger.WriteDebug("CloseDistance: error - Flying Serpent Kick was not removed - lag?");
                                                    })
                                                )
                                            )
                                        )
                                    )
                                ),

                            Spell.BuffSelf("Tiger's Lust", req => hasTigersLust ),

                            new Sequence(
                                Spell.Cast("Roll", on => (WoWUnit)on, req => !MonkSettings.DisableRoll && MovementManager.IsClassMovementAllowed),
                                new PrioritySelector(
                                    new Wait(
                                        TimeSpan.FromMilliseconds(500), 
                                        until => Me.Auras.ContainsKey("Roll"),
                                        new Action(r => Logger.WriteDebug("CloseDistance: Roll in progress"))
                                        ),
                                    new Action( r => {
                                        Logger.WriteDebug("CloseDistance: failure - did not detect Roll in progress aura- lag?");
                                        return RunStatus.Failure;
                                        })
                                    ),
                                new Wait(
                                    TimeSpan.FromMilliseconds(950), 
                                    until => !Me.Auras.ContainsKey("Roll"),
                                    new Action(r => Logger.WriteDebug("CloseDistance: Roll has ended"))
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static WoWObject FindClosestSphere(SphereType typ, float range)
        {
            range *= range;
            return ObjectManager.ObjectList
                .Where(o => o.Type == WoWObjectType.AreaTrigger && o.Entry == (uint)typ && o.DistanceSqr < range && !Blacklist.Contains(o.Guid, BlacklistFlags.Combat))
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

        private static WoWGuid guidSphere = WoWGuid.Empty;
        private static WoWPoint locSphere = WoWPoint.Empty;
        private static DateTime timeAbortSphere = DateTime.Now;

        public static Composite CreateMoveToSphereBehavior(SphereType typ, float range)
        {
            return new Decorator(
                ret => SingularSettings.Instance.MoveToSpheres && !MovementManager.IsMovementDisabled,

                new PrioritySelector(

                    // check we haven't gotten out of range due to fall / pushback / port / etc
                    new Decorator( 
                        ret => guidSphere.IsValid&& Me.Location.Distance(locSphere) > range,
                        new Action(ret => { guidSphere = WoWGuid.Empty; locSphere = WoWPoint.Empty; })
                        ),

                    // validate the sphere we are moving to
                    new Action(ret =>
                    {
                        WoWObject sph = FindClosestSphere(typ, range);
                        if (sph == null)
                        {
                            guidSphere = WoWGuid.Empty; locSphere = WoWPoint.Empty;
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
                        ret => guidSphere.IsValid && Me.Location.Distance(locSphere) > 1,
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

/*
        public static Sequence CreateHealingSphereBehavior( int sphereBelowHealth)
        {
            // healing sphere keeps spell on cursor for up to 3 casts... need to stop targeting after 1
            return new Sequence(
                Spell.CastOnGround("Healing Sphere",
                    on => Me,
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
*/

        public static Composite CreateChiBurstBehavior()
        {
            if ( !HasTalent(MonkTalents.ChiBurst))
                return new ActionAlwaysFail();

            if (TalentManager.CurrentSpec == WoWSpec.MonkMistweaver && SingularRoutine.CurrentWoWContext != WoWContext.Normal)
            {
                return new Decorator(
                    req => !Spell.IsSpellOnCooldown("Chi Burst") && !Me.CurrentTarget.IsBoss()
                        && 3 <= Clusters.GetPathToPointCluster(Me.Location.RayCast(Me.RenderFacing, 40f), HealerManager.Instance.TargetList.Where(m => Me.IsSafelyFacing(m, 25)), 5f).Count(),
                    Spell.Cast("Chi Burst",
                        mov => true,
                        ctx => Me,
                        ret => Me.HealthPercent < MonkSettings.ChiWavePct,
                        cancel => false
                        )
                    );
            }

            return new Decorator(
                req => !Spell.IsSpellOnCooldown("Chi Burst") && !Me.CurrentTarget.IsBoss() 
                    && 3 <= Clusters.GetPathToPointCluster( Me.Location.RayCast(Me.RenderFacing, 40f), Unit.NearbyUnfriendlyUnits.Where( m => Me.IsSafelyFacing(m,25)), 5f).Count(),
                Spell.Cast("Chi Burst",
                    mov => true,
                    ctx => Me,
                    ret => Me.HealthPercent < MonkSettings.ChiWavePct,
                    cancel => false
                    )
                );
            }


        /// <summary>
        /// selects best target, favoring healing multiple group members followed by damaging multiple targets
        /// </summary>
        /// <returns></returns>
        private static WoWUnit BestChiBurstTarget()
        {
            WoWUnit target = null;

            if (Me.IsInGroup())
                target = Clusters.GetBestUnitForCluster(
                    Unit.NearbyGroupMembers.Where(m => m.IsAlive && m.HealthPercent < 80),
                    ClusterType.PathToUnit,
                    40f);

            if (target == null || target.IsMe)
                target = Clusters.GetBestUnitForCluster(
                    Unit.NearbyUnitsInCombatWithMeOrMyStuff,
                    ClusterType.PathToUnit,
                    40f);

            if (target == null)
                target = Me;

            return target;
        }

        public static WoWUnit BestExpelHarmTarget()
        {
            if (HealerManager.NeedHealTargeting)
                return HealerManager.Instance.TargetList.FirstOrDefault( t => t.SpellDistance() < 40 && t.InLineOfSpellSight) ?? Me;

            return Unit.NearbyGroupMembers.FirstOrDefault(t => t.SpellDistance() < 40 && t.InLineOfSpellSight) ?? Me;
        }

        public static Composite CastTouchOfDeath()
        {
            return Spell.Cast("Touch of Death", ret => Me.HasAura("Death Note") || Me.CurrentTarget.HealthPercent < 10);
        }
    }

    public enum MonkTalents
    {
#if PRE_WOD
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
#else

        Celerity = 1,
        TigersLust,
        Momentum,

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
        ChiTorpedo,

        SoulDance,
        BreathOfTheSerpent = SoulDance,
        HurricaneStrike = SoulDance,
        ChiExplosion,
        Serenity,
        PoolOfMists = Serenity

#endif
    }

}