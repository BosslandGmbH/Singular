using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.CommonBot.POI;
using Styx.CommonBot;
using System;
using CommonBehaviors.Actions;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.Priest
{
    public class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PriestSettings PriestSettings { get { return SingularSettings.Instance.Priest(); } }
        public static bool HasTalent( PriestTalents tal ) { return TalentManager.IsSelected((int)tal); }


        [Behavior(BehaviorType.Heal, WoWClass.Priest, context:WoWContext.Battlegrounds, priority:2)]
        public static Composite CreatePriestHealPreface()
        {
            return new PrioritySelector(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

            #region Avoidance 

                    new Decorator(
                        ret => Unit.NearbyUnitsInCombatWithMe.Any(u => u.SpellDistance() < 8)
                            && (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || (SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.HealthPercent < 50)),
                        CreatePriestAvoidanceBehavior()
                        )

            #endregion 

                    )
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs,WoWClass.Priest)]
        public static Composite CreatePriestPreCombatBuffs()
        {
            return new PrioritySelector(
                        
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        PartyBuff.BuffGroup("Power Word: Fortitude"),
                        //Spell.BuffSelf("Shadow Protection", ret => SingularSettings.Instance.Priest().UseShadowProtection && Unit.NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && !Unit.HasAura(u, "Shadow Protection", 0))), // we no longer have Shadow resist
                        Spell.BuffSelf("Inner Fire", ret => SingularSettings.Instance.Priest().UseInnerFire),
                        Spell.BuffSelf("Inner Will", ret => !SingularSettings.Instance.Priest().UseInnerFire),
                        Spell.BuffSelf("Fear Ward", ret => SingularSettings.Instance.Priest().UseFearWard),

                        Spell.BuffSelf("Shadowform"),

                        CreatePriestMovementBuff("PreCombat")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Priest)]
        public static Composite CreatePriestLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.Cast("Guardian Spirit", on => Me, ret => Me.Stunned && Me.HealthPercent < 20),
                    Spell.Cast("Pain Suppression", on => Me, ret => Me.Stunned),
                    Spell.Cast("Dispersion", on => Me, ret => Me.HealthPercent < 60)
                    )
                );
        }

        private static bool CanCastFortitudeOn(WoWUnit unit)
        {
            //return !unit.HasAura("Blood Pact") &&
            return !unit.HasAura("Power Word: Fortitude") &&
                   !unit.HasAura("Qiraji Fortitude") &&
                   !unit.HasAura("Commanding Shout");
        }

        public static Decorator CreatePriestMovementBuff()
        {
            return new Decorator(
                ret => MovementManager.IsClassMovementAllowed
                    && StyxWoW.Me.IsAlive
                    && !StyxWoW.Me.Mounted
                    && !StyxWoW.Me.IsOnTransport
                    && !StyxWoW.Me.OnTaxi
                    && (SpellManager.HasSpell("Angelic Feather") || TalentManager.IsSelected((int)PriestTalents.BodyAndSoul))
                    && !StyxWoW.Me.HasAnyAura("Angelic Feather", "Body and Soul")
                    && !StyxWoW.Me.IsAboveTheGround(),

                new PrioritySelector(
                    Spell.WaitForCast(),
                    new Throttle(3,
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                Spell.BuffSelf("Power Word: Shield",
                                    ret => TalentManager.IsSelected((int)PriestTalents.BodyAndSoul)
                                        && !StyxWoW.Me.HasAnyAura("Body and Soul", "Weakened Soul")),

                                new Decorator(
                                    ret => SpellManager.HasSpell("Angelic Feather")
                                        && !StyxWoW.Me.HasAura("Angelic Feather"),
                                    new Sequence(
                // new Action( ret => Logger.Write( "Speed Buff for {0}", mode ) ),
                                        Spell.CastOnGround("Angelic Feather",
                                            ctx => StyxWoW.Me.Location,
                                            ret => true,
                                            false),
                                        Helpers.Common.CreateWaitForLagDuration(orUntil => StyxWoW.Me.CurrentPendingCursorSpell != null),
                                        new Action(ret => Lua.DoString("SpellStopTargeting()"))
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static Decorator CreatePriestMovementBuff(string mode, bool checkMoving = true)
        {
            return new Decorator(
                ret => MovementManager.IsClassMovementAllowed 
                    && StyxWoW.Me.IsAlive 
                    && (!checkMoving || StyxWoW.Me.IsMoving)
                    && !StyxWoW.Me.Mounted
                    && !StyxWoW.Me.IsOnTransport
                    && !StyxWoW.Me.OnTaxi
                    && (SpellManager.HasSpell("Angelic Feather") || TalentManager.IsSelected((int) PriestTalents.BodyAndSoul))
                    && !StyxWoW.Me.HasAnyAura("Angelic Feather", "Body and Soul")
                    && (BotPoi.Current == null || BotPoi.Current.Type == PoiType.None || BotPoi.Current.Location.Distance(StyxWoW.Me.Location) > 10)
                    && !StyxWoW.Me.IsAboveTheGround(),

                new PrioritySelector(
                    Spell.WaitForCast(),
                    new Throttle( 3, 
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                Spell.BuffSelf( "Power Word: Shield", 
                                    ret => TalentManager.IsSelected((int) PriestTalents.BodyAndSoul)
                                        && !StyxWoW.Me.HasAnyAura("Body and Soul", "Weakened Soul")),

                                new Decorator(
                                    ret => SpellManager.HasSpell("Angelic Feather")
                                        && !StyxWoW.Me.HasAura("Angelic Feather"),
                                    new Sequence(
                                        // new Action( ret => Logger.Write( "Speed Buff for {0}", mode ) ),
                                        Spell.CastOnGround("Angelic Feather",
                                            ctx => StyxWoW.Me.Location,
                                            ret => true,
                                            false),
                                        Helpers.Common.CreateWaitForLagDuration( orUntil => StyxWoW.Me.CurrentPendingCursorSpell != null ),
                                        new Action(ret => Lua.DoString("SpellStopTargeting()"))
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        #region Avoidance and Disengage

        /// <summary>
        /// creates a Priest specific avoidance behavior based upon settings.  will check for safe landing
        /// zones before using WildCharge or rocket jump.  will additionally do a running away or jump turn
        /// attack while moving away from attacking mob if behaviors provided
        /// </summary>
        /// <param name="nonfacingAttack">behavior while running away (back to target - instants only)</param>
        /// <param name="jumpturnAttack">behavior while facing target during jump turn (instants only)</param>
        /// <returns></returns>
        public static Composite CreatePriestAvoidanceBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => MovementManager.IsClassMovementAllowed,
                    Disengage.CreateDisengageBehavior("Rocket Jump", Disengage.Direction.Frontwards, 20, CreateSlowMeleeBehavior())
                    ),
                new Decorator(
                    ret => MovementManager.IsClassMovementAllowed 
                        && PriestSettings.AllowKiting
                        && (Common.HasTalent(PriestTalents.AngelicFeather) || Common.HasTalent(PriestTalents.BodyAndSoul) || Common.HasTalent(PriestTalents.VoidTendrils )),
                    Kite.BeginKitingBehavior(35)
                    )
                );
        }

        public static Composite CreateSlowMeleeBehavior()
        {
            return new PrioritySelector(
                ctx => SafeArea.NearestEnemyMobAttackingMe,
                new Decorator(
                    ret => ret != null,
                    new Throttle(2,
                        new PrioritySelector(
                            Spell.Buff("Void Tendrils", onUnit => (WoWUnit)onUnit, req => true),
                            Spell.Buff("Psychic Horror", onUnit => (WoWUnit)onUnit, req => true),
                            Spell.CastOnGround("Psyfiend",
                                loc => ((WoWUnit)loc).Distance <= 20 ? ((WoWUnit)loc).Location : WoWMovement.CalculatePointFrom(((WoWUnit)loc).Location, (float)((WoWUnit)loc).Distance - 20),
                                req => ((WoWUnit)req) != null,
                                false
                                )
                            )
                        )
                    )
                );
        }

        #endregion

    }

    public enum PriestTalents
    {
        VoidTendrils = 1,
        Psyfiend,
        DominateMind,
        BodyAndSoul,
        AngelicFeather,
        Phantasm,
        FromDarknessComesLight,
        Mindbender,
        SolaceAndInsanity,
        DesperatePrayer,
        SpectralGuise,
        AngelicBulwark,
        TwistOfFate,
        PowerInfusion,
        DivineInsight,
        Cascade,
        DivineStar,
        Halo
    }

}
