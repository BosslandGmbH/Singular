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
    public static class Affliction
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }

        private static int _mobCount;

        private static int DotCountNeeded;
        private static int MaxDotCount;

        [Behavior(BehaviorType.Initialize, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.All)]
        public static Composite CreateWarlockAfflictionInit()
        {
            MaxDotCount = 0;
            if (SpellManager.HasSpell("Agony"))
                ++MaxDotCount;
            if (SpellManager.HasSpell("Corruption"))
                ++MaxDotCount;
            if (SpellManager.HasSpell("Unstable Affliction"))
                ++MaxDotCount;

            return null;
        }


        [Behavior(BehaviorType.Pull, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.All)]
        public static Composite CreateWarlockAfflictionNormalPull()
        { 
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Action(ret =>
                        {
                            _mobCount = Common.TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateWarlockDiagnosticOutputBehavior("Pull"),
                        SingularRoutine.CurrentWoWContext == WoWContext.Instances 
                            ? CreateApplyDotsBehaviorInstance(onUnit => Me.CurrentTarget, ret => true)
                            : CreateApplyDotsBehaviorNormal(onUnit => Me.CurrentTarget)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Warlock, WoWSpec.WarlockAffliction, priority: 999)]
        public static Composite CreateWarlockAfflictionHeal()
        {
            return new PrioritySelector(
                CreateWarlockDiagnosticOutputBehavior("Combat")
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.Normal)]
        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.Instances)]
        public static Composite CreateWarlockAfflictionNormalCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                // Movement.CreateEnsureMovementStoppedBehavior(35f),

                new Action(r => { if ( Me.GotTarget()) Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; } ),

                // cancel an early drain soul if DoTs are falling off
                CancelChanneledCastBehavior(),

                Spell.WaitForCastOrChannel(),

                new Decorator(ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),

                        new Action(ret =>
                        {
                            _mobCount = Common.TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateAoeBehavior(),

                        Common.CastCataclysm(),

                        CreateApplyDotsBehaviorNormal(on => Me.CurrentTarget),

                        Spell.Cast("Drain Life", req => Me.HealthPercent < WarlockSettings.DrainLifeCastPct),
                        Spell.Cast("Drain Soul", mov => true, on => Me.CurrentTarget, req => true, cancel => false),
                        Spell.Cast("Shadow Bolt", req => !SpellManager.HasSpell("Drain Soul")),
                        Spell.Cast("Drain Life", mov => true, on => Me.CurrentTarget, req => !SpellManager.HasSpell("Drain Soul"))
                
                        )
                    )
                );

        }

        private static Composite CancelChanneledCastBehavior()
        {
            return new PrioritySelector(

                ctx => Me.ChanneledSpell == null ? null : Me.ChanneledSpell.Name,

                new Decorator(
                    ret => {
                        // true: evaluate if we need to cancel, false: let it continue
                        if (ret != null && Me.GotTarget())
                            return ((string)ret) == "Drain Soul" || ((string)ret) == "Drain Life";
                        return false;
                        },
                    new PrioritySelector(
                        new Decorator(
                            ret =>
                            {
                                if (Spell.IsGlobalCooldown())
                                    return false;

                                return ShouldWeCancelChanneledCast();
                            },
                            new Sequence(
                                new Action(ret => SpellManager.StopCasting()),
                                new WaitContinue(TimeSpan.FromMilliseconds(500), ret => Me.ChanneledSpell == null, new ActionAlwaysSucceed())
                                )
                            )
                        )
                    )
                );
        }

        public static bool ShouldWeCancelChanneledCast()
        {
            string spellName = Me.ChanneledSpell == null ? null : Me.ChanneledSpell.Name;

            if (spellName == null)
                return false;

            if (spellName == "Drain Life" && Me.HealthPercent >= WarlockSettings.DrainLifeCancelPct && SpellManager.HasSpell("Drain Soul"))
            {
                Logger.Write(LogColor.Cancel, "/cancel {0}: health has reached {1:F1}%", spellName, Me.HealthPercent);
                return true;
            }

            int dotsNeeded = GetDotsMissing(Me.CurrentTarget);
            if (dotsNeeded > (MaxDotCount - 1) && Me.CurrentTarget.IsPlayer)
            {
                Logger.Write(LogColor.Cancel, "/cancel {0}: player {1} needs DoTs {2}/{3}", spellName, Me.CurrentTarget.SafeName(), dotsNeeded, MaxDotCount);
                return true;
            }
            long ttd = Me.CurrentTarget.TimeToDeath(0);
            if (dotsNeeded > (MaxDotCount - 1) && ttd > 6)
            {
                Logger.Write(LogColor.Cancel, "/cancel {0}: {1} needs all DoTs {2}/{3}", spellName, Me.CurrentTarget.SafeName(), dotsNeeded, MaxDotCount);
                return true;
            }
            if (dotsNeeded == MaxDotCount && ttd > 2)
            {
                Logger.Write(LogColor.Cancel, "/cancel {0}: {1} needs more DoTs {2}/{3}", spellName, Me.CurrentTarget.SafeName(), dotsNeeded, MaxDotCount);
                return true;
            }
            return false;
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.Battlegrounds )]
        public static Composite CreateWarlockAfflictionPvpCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),

                        new Action(ret =>
                        {
                            _mobCount = Common.TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

#if NOT_NOW
                        new Throttle( 4, 
                            Spell.Cast( "Curse of Enfeeblement", 
                                on => Unit.NearbyUnfriendlyUnits
                                        .Where( u => !u.IsCrowdControlled() && u.IsTargetingMeOrPet 
                                            && ((u.PowerType != WoWPowerType.Mana && !u.HasAura("Weakened Blows")) || (u.PowerType == WoWPowerType.Mana && !u.HasAura("Slow Casting"))))
                                        .OrderBy( u => u.Distance )
                                        .FirstOrDefault()
                                )
                            ),
#endif
                        // make sure Primary Target is loaded up with DoTs
                        CreateApplyDotsBehaviorPvp(on => Me.CurrentTarget, ret => true),

                        // Glyph of Siphon Life? then spam Corruption around....
                        new Decorator(
                            ret => TalentManager.HasGlyph( "Siphon Life"),
                            Spell.Buff( "Corruption", 2, ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HasAuraExpired("Corruption", 2)), req => true)
                            ),

                        // now go around the room with instant DoTs
                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => !u.HasAllMyAuras( "Corruption", "Agony")),
                            CreateApplyDotsBehaviorPvp( on => (WoWUnit) on, ret => true)
                            ),

                        // fear target if targeting me and not cc'd (prio by distance)
                        new Throttle(2, 8,
                            new PrioritySelector(
                                ctx => Unit.NearbyUnfriendlyUnits
                                        .Where(u => !u.IsCrowdControlled() && u.CurrentTargetGuid == Me.Guid)
                                        .OrderBy(u => u.Distance)
                                        .FirstOrDefault(),

                                Spell.Buff("Howl of Terror", on => (WoWUnit)on, req => Spell.IsSpellOnCooldown("Fear") || 1 < Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet && Me.SpellDistance(u) < 10f)),
                                Spell.Buff("Mortal Coil", on => (WoWUnit)on, req => Me.HealthPercent < 70),
                                Spell.Buff("Fear", on => Common.GetBestFearTarget())
                                )
                            ),

                        // now try to spread some affliction
                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => !u.HasAllMyAuras("Unstable Affliction")),
                            CreateApplyDotsBehaviorPvp(on => (WoWUnit)on, ret => true)
                            ),

                        Spell.Cast("Drain Soul"),

                        // only a lowbie should hit this
                        Spell.Cast("Drain Life", ret => !SpellManager.HasSpell("Drain Soul"))
                        )
                    )
                );

        }


        public static Composite CreateAoeBehavior()
        {
            return new Decorator(
                ret => Spell.UseAOE,
                new PrioritySelector(

                    new Decorator(
                        ret => _mobCount >= 4 && SpellManager.HasSpell("Seed of Corruption"),
                        new PrioritySelector(
                            // if current target doesn't have CotE, then Soulburn+CotE
                            new Decorator(
                                req => !Me.CurrentTarget.HasAura("Curse of the Elements"),
                                new Sequence(
                                    Common.CreateCastSoulburn(req => true),
                                    Spell.Buff("Curse of the Elements")
                                    )
                                ),
                            // roll SoC on targets in combat that we are facing
                            new PrioritySelector(
                                ctx => Common.TargetsInCombat.FirstOrDefault(m => !m.HasAura("Seed of Corruption")),
                                new Sequence(
                                    new PrioritySelector(
                                        Common.CreateCastSoulburn(req => req != null),
                                        new ActionAlwaysSucceed()
                                        ),
                                    Spell.Cast("Seed of Corruption", on => (WoWUnit)on)
                                    )
                                )
                            )
                        ),

                    Common.CastCataclysm(),

                    new Decorator(
                        ret => _mobCount >= 2,
                        new PrioritySelector(
                            CreateApplyDotsBehaviorNormal(ctx => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Corruption", 0))),
                            CreateApplyDotsBehaviorNormal(ctx => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Agony", 2)))
                            // CreateApplyDotsBehaviorInstance(ctx => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Unstable Affliction", 2)), soulBurn => true)
                            )
                        )
                    )
                );
        }

        public static Composite CreateApplyDotsBehaviorNormal(UnitSelectionDelegate onUnit)
        {
            System.Diagnostics.Debug.Assert(onUnit != null);

            return new PrioritySelector(

                ctx => onUnit(ctx),

                new Decorator(
                    req => Unit.ValidUnit((WoWUnit) req),

                    new PrioritySelector(
                        CreateCastSoulSwap(on => (WoWUnit) on),

                        new Decorator(
                            req => GetSoulSwapDotsNeeded((WoWUnit)req) > 0,
                            new PrioritySelector(
                                Spell.Buff("Corruption", 3, on => (WoWUnit) on, ret => true),
                                Spell.Buff("Agony", 3, on => (WoWUnit) on, ret => true),
                                Spell.Buff("Unstable Affliction", 3, on => (WoWUnit) on, req => true),
                                new Action(r => {
                                    Logger.WriteDebug("ApplyDots: no DoTs needed on {0}", ((WoWUnit)r).SafeName());
                                    return RunStatus.Failure;
                                    })
                                )
                            ),

                        // 
                        Spell.Buff(
                            "Haunt", 
                            1, 
                            on => (on as WoWUnit),
                            req => Me.CurrentSoulShards > 2
                                && !(req as WoWUnit).HasAuraExpired("Corruption", 3)
                                && !(req as WoWUnit).HasAuraExpired("Agony", 3)
                                && !(req as WoWUnit).HasAuraExpired("Unstable Affliction", 3)
                                && (((req as WoWUnit).IsPlayer) || ((req as WoWUnit).Guid == Me.CurrentTargetGuid && (req as WoWUnit).TimeToDeath(99) > 8))                                
                            )
                        )
                    )
                );
        }

        public static Composite CreateApplyDotsBehaviorInstance(UnitSelectionDelegate onUnit, SimpleBooleanDelegate soulBurn)
        {
            System.Diagnostics.Debug.Assert(onUnit != null);

            return new PrioritySelector(

                new Decorator(
                    ret => !Me.HasAura("Soulburn"),
                    new PrioritySelector(
                // target below 20% we have a higher prior on Haunt (but skip if soulburn already up...)
                        Spell.Buff("Haunt",
                            2,
                            onUnit,
                            req => Me.CurrentSoulShards > 0
                                && Me.CurrentTarget.HealthPercent < 20
                                && !Me.HasAura("Soulburn")
                            ),

                        // otherwise, save 2 shards for Soulburn and instant pet rez if needed (unless Misery buff up)
                        Spell.Buff("Haunt", 2, onUnit, req => Me.CurrentSoulShards > 2 || Me.HasAura("Dark Soul: Misery"))
                        )
                    ),

                CreateCastSoulSwap(onUnit),

                new Action(ret =>
                {
                    DotCountNeeded = 0;
                    if (onUnit != null && onUnit(ret) != null)
                    {
                        // if mob dying very soon, skip DoTs
                        if (onUnit(ret).TimeToDeath(99) < 4)
                            DotCountNeeded = 4;
                        else
                        {
                            if (!onUnit(ret).HasAuraExpired("Agony", 3))
                                ++DotCountNeeded;
                            if (!onUnit(ret).HasAuraExpired("Corruption", 3))
                                ++DotCountNeeded;
                            if (!onUnit(ret).HasAuraExpired("Unstable Affliction", 3))
                                ++DotCountNeeded;
                            if (!onUnit(ret).HasAuraExpired("Haunt", 3))
                                ++DotCountNeeded;
                        }
                    }
                    // Logger.WriteDebug("CreateApplyDotsBehavior: DotCount={0}", DotCountNeeded );
                    return RunStatus.Failure;
                }),
                new Decorator(
                    req => DotCountNeeded < 4,
                    new PrioritySelector(
                        Spell.Buff("Agony", 3, onUnit, ret => true),
                        Spell.Buff("Corruption", 3, onUnit, ret => true),
                        Spell.Buff("Unstable Affliction", 3, onUnit, req => true)
                        )
                    )
                );
        }

        public static Composite CreateApplyDotsBehaviorPvp(UnitSelectionDelegate onUnit, SimpleBooleanDelegate soulBurn)
        {
            return new PrioritySelector(

                    // soulburn + soulswap sequence if requested
                    new Sequence(
                       Common.CreateCastSoulburn(
                            ret => soulBurn(ret)
                                && Me.CurrentSoulShards > 0
                                && onUnit != null && onUnit(ret) != null
                                && onUnit(ret).CurrentHealth > 1
                                && SpellManager.HasSpell("Soul Swap")
                                && onUnit(ret).HasAuraExpired("Unstable Affliction")
                                && !Me.HasAura("Soulburn") 
                                ),

                        CreateCastSoulSwap(onUnit)
                        ),

                    Spell.Buff("Corruption", 3, ctx => onUnit(ctx), req => true),
                    Spell.Buff("Agony", 3, ctx => onUnit(ctx), req => true),
                    Spell.Buff("Unstable Affliction", 3, ctx => onUnit(ctx), req => true),

                    // target has all my DoTs but not Haunt -- make sure Soulburn isn't active
                   Spell.Buff("Haunt", 
                        2,
                        ctx => onUnit(ctx), 
                        req => Me.CurrentSoulShards > 0
                            && onUnit(req).HasAllMyAuras("Agony", "Corruption", "Unstable Affliction")
                            && !Me.HasAura("Soulburn")
                        )
                    );
        }


        delegate bool NeedSoulSwapDelegate(WoWUnit unit);

        public static Composite CreateCastSoulSwap(UnitSelectionDelegate onUnit)
        {
            const string SOUL_SWAP = "Soul Swap";
            if (!SpellManager.HasSpell(SOUL_SWAP))
                return new ActionAlwaysFail();

            NeedSoulSwapDelegate needSoulSwap = NeedSoulSwapNormal;

            return new Decorator(
                req => Me.CurrentSoulShards >= 2,
                new Sequence(
                    ctx => onUnit(ctx),
                    new Decorator(
                        req => needSoulSwap((WoWUnit)req),
                        new ActionAlwaysSucceed()
                        ),
                    Common.CreateCastSoulburn(req => true),
                    new Action(ret =>
                    {
                        Logger.Write(LogColor.SpellNonHeal, string.Format("*Soul Swap on {0} @ {1:F1}% at {2:F1} yds", ((WoWUnit)ret).SafeName(), ((WoWUnit)ret).HealthPercent, ((WoWUnit)ret).SpellDistance()));
                        if (!Spell.CastPrimative("Soul Swap", onUnit(ret)))
                            return RunStatus.Failure;
                        return RunStatus.Success;
                    })
                    )
                );
        }

        public static Composite CreateCastSoulburnHaunt(UnitSelectionDelegate onUnit)
        {
            if (!Common.HasTalent(WarlockTalents.SoulburnHaunt))
                return new ActionAlwaysFail();

            return new Decorator(
                req => Me.CurrentSoulShards >= 2,
                new Sequence(
                    ctx => onUnit(ctx),
                    new Decorator(
                        req => NeedSoulburnHauntNormal(req as WoWUnit),
                        new ActionAlwaysSucceed()
                        ),
                    Common.CreateCastSoulburn(req => true),
                    new Action(ret =>
                    {
                        Logger.Write(LogColor.SpellNonHeal, string.Format("*Haunt on {0} @ {1:F1}% at {2:F1} yds", ((WoWUnit)ret).SafeName(), ((WoWUnit)ret).HealthPercent, ((WoWUnit)ret).SpellDistance()));
                        if (!Spell.CastPrimative("Haunt", onUnit(ret)))
                            return RunStatus.Failure;
                        return RunStatus.Success;
                    })
                    )
                );
        }

        private static bool NeedSoulSwapNormal(WoWUnit unit)
        {
            if (!unit.IsAlive)
                return false;

            if (GetSoulSwapDotsNeeded(unit) < 2)
                return false;

            if (unit.SpellDistance() > 40)
                return false;

            if (!unit.InLineOfSpellSight)
                return false;

            return true;
        }

        private static bool NeedSoulburnHauntNormal(WoWUnit unit)
        {
            if (!unit.IsAlive)
                return false;

            if (unit.SpellDistance() > 40)
                return false;

            if (!unit.InLineOfSpellSight)
                return false;

            return true;
        }

        private static int GetSoulSwapDotsNeeded(WoWUnit unit)
        {
            int dotCount = 0;
            if (unit.HasAuraExpired("Agony"))
                dotCount++;
            if (unit.HasAuraExpired("Corruption"))
                dotCount++;
            if (unit.HasAuraExpired("Unstable Affliction"))
                dotCount++;
            return dotCount;
        }

        private static int GetDotsMissing(WoWUnit unit)
        {
            int dotCount = 0;
            if (unit.HasAuraExpired("Agony", 0))
                dotCount++;
            if (unit.HasAuraExpired("Corruption", 0))
                dotCount++;
            if (unit.HasAuraExpired("Unstable Affliction", 0))
                dotCount++;
            return dotCount;
        }

        private static WoWUnit GetBestAoeTarget()
        {
            WoWUnit unit = null;

            if (SpellManager.HasSpell("Seed of Corruption"))
                unit = Clusters.GetBestUnitForCluster(Common.TargetsInCombat.Where(m => !m.HasAura("Seed of Corruption")), ClusterType.Radius, 15f);

            if (SpellManager.HasSpell("Agony"))
                unit = Common.TargetsInCombat.FirstOrDefault(t => !t.HasMyAura("Agony"));

            return unit;
        }

        private static Composite CreateWarlockDiagnosticOutputBehavior(string sState = null)
        {
            if (!SingularSettings.Debug)
                return new Action(ret => { return RunStatus.Failure; });

            return new ThrottlePasses(1,
                new Action(ret =>
                {
                    string sMsg;
                    sMsg = string.Format(".... [{0}] h={1:F1}%, m={2:F1}%, moving={3}, pet={4:F0}% @ {5:F0} yds, soulburn={6}",
                        sState,
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving.ToYN(),
                        Me.GotAlivePet ? Me.Pet.HealthPercent : 0,
                        Me.GotAlivePet ? Me.Pet.Distance : 0,
                        (long)Me.GetAuraTimeLeft("Soulburn", true).TotalMilliseconds

                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        sMsg += string.Format(
                            ", {0}, {1:F1}%, dies={2} secs, {3:F1} yds, loss={4}, face={5}, agony={6}, corr={7}, ua={8}, haunt={9}, seed={10}, aoe={11}",
                            target.SafeName(),
                            target.HealthPercent,
                            target.TimeToDeath(),
                            target.Distance,
                            target.InLineOfSpellSight.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            (long)target.GetAuraTimeLeft("Agony", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Corruption", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Unstable Affliction", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Haunt", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Seed of Corruption", true).TotalMilliseconds,
                            _mobCount
                            );
                    }

                    Logger.WriteDebug(Color.LightYellow, sMsg);
                    return RunStatus.Failure;
                })
                );
        }
    }
}