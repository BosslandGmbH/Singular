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

namespace Singular.ClassSpecific.Priest
{
    public enum PriestTalent
    {
        VoidTendrils = 1,
        Psyfiend,
        DominateMind,
        BodyAndSoul,
        AngelicFeather,
        Phantasm,
        FromDarknessComesLight,
        Mindbender,
        PowerWordSolace,
        DesperatePrayer,
        SpectralGuide,
        AngelicBulwark,
        TwistOfFate,
        PowerInfusion,
        DivineInsight,
        Cascade,
        DivineStar,
        Halo
    }

    public class Common
    {
        [Behavior(BehaviorType.PreCombatBuffs,WoWClass.Priest)]
        public static Composite CreatePriestPreCombatBuffs()
        {
            return new PrioritySelector(
                        
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.BuffSelf("Shadowform"),
                        //Spell.BuffSelf("Vampiric Embrace"), // VE is now a CD, not a normal buff
                        // Spell.BuffSelf("Power Word: Fortitude", ret => Unit.NearbyFriendlyPlayers.Any(u => !u.IsDead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && CanCastFortitudeOn(u))),
                        PartyBuff.BuffGroup("Power Word: Fortitude"),
                        //Spell.BuffSelf("Shadow Protection", ret => SingularSettings.Instance.Priest.UseShadowProtection && Unit.NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && !Unit.HasAura(u, "Shadow Protection", 0))), // we no longer have Shadow resist
                        Spell.BuffSelf("Inner Fire", ret => SingularSettings.Instance.Priest.UseInnerFire),
                        Spell.BuffSelf("Inner Will", ret => !SingularSettings.Instance.Priest.UseInnerFire),
                        Spell.BuffSelf("Fear Ward", ret => SingularSettings.Instance.Priest.UseFearWard),

                        CreatePriestMovementBuff("PreCombat")
                        )
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

        public static Decorator CreatePriestMovementBuff(string mode, bool checkMoving = true)
        {
            return new Decorator(
                ret => SingularSettings.Instance.Priest.UseSpeedBuff
                    && !MovementManager.IsMovementDisabled 
                    && StyxWoW.Me.IsAlive 
                    && (!checkMoving || StyxWoW.Me.IsMoving)
                    && !StyxWoW.Me.Mounted
                    && !StyxWoW.Me.IsOnTransport
                    && !StyxWoW.Me.OnTaxi
                    && (SpellManager.HasSpell("Angelic Feather") || TalentManager.IsSelected((int) PriestTalent.BodyAndSoul))
                    && !StyxWoW.Me.HasAnyAura("Angelic Feather", "Body and Soul")
                    && (BotPoi.Current == null || BotPoi.Current.Type == PoiType.None || BotPoi.Current.Location.Distance(StyxWoW.Me.Location) > 10)
                    && !StyxWoW.Me.IsAboveTheGround(),

                new PrioritySelector(
                    Spell.WaitForCast(),
                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(

                            Spell.BuffSelf( "Power Word: Shield", 
                                ret => TalentManager.IsSelected((int) PriestTalent.BodyAndSoul)
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
                );
        }
    }
}
