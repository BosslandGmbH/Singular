using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using CommonBehaviors.Actions;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.CommonBot.POI;

namespace Singular.ClassSpecific.Warlock
{
    public enum WarlockTalent
    {
        None = 0,
        DarkRegeneration,
        SoulLeech,
        HarvestLife,
        HowlOfTerror,
        MortalCoil,
        Shadowfury,
        SoulLink,
        SacrificialPact,
        DarkBargain,
        BloodFear,
        BurningRush,
        UnboundWill,
        GrimoireOfSupremacy,
        GrimoireOfService,
        GrimoireOfSacrifice,
        ArchimondesVengeance,
        KiljadensCunning,
        MannorothsFury
    }

    public class Common
    {
        #region Local Helpers

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock; } }

        private static bool HaveHealthStone { get { return StyxWoW.Me.BagItems.Any(i => i.Entry == 5512); } }

        #endregion

        #region Talents

        public static bool HasTalent(WarlockTalent tal)
        {
            return TalentManager.IsSelected((int)tal);
        }

        #endregion

        #region Rest

        [Behavior(BehaviorType.Rest, WoWClass.Warlock)]
        public static Composite CreateWarlockRest()
        {
            return new PrioritySelector(
                new Decorator(ctx => SingularSettings.Instance.DisablePetUsage && Me.GotAlivePet,
                    new Action(ctx => Lua.DoString("PetDismiss()"))),
                // following will break questing which has some /use item spells that this would cancel
                //new Decorator(
                //    ctx => Me.CastingSpell != null && Me.CastingSpell.Name.Contains("Summon") && Me.GotAlivePet,
                //    new Action(ctx => SpellManager.StopCasting())),
                Spell.WaitForCast(false),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Sequence(
                            Spell.BuffSelf("Life Tap", ret => Me.ManaPercent < 80 && Me.HealthPercent > 60 && !Me.HasAnyAura("Drink", "Food")),
                            Helpers.Common.CreateWaitForLagDuration()
                            ),
                        Rest.CreateDefaultRestBehaviour(),

                        new Decorator( 
                            ret => GetCurrentPet() != WarlockPet.None 
                                && Me.Pet.HealthPercent < 85
                                && Me.Pet.Distance < 45
                                && Me.Pet.InLineOfSpellSight,
                            Spell.Heal(ret => "Health Funnel", ret => false, on => Me.Pet, req => true, req => !Me.GotAlivePet || Me.Pet.HealthPercent > 95)
                            )
                        )
                    )
                );
        }

        #endregion

        #region PreCombatBuffs

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warlock)]
        public static Composite CreateWarlockPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Throttle( 4, Spell.Cast("Create Healthstone", ret => !HaveHealthStone)),
                        Spell.BuffSelf("Soulstone", ret => NeedToSoulstoneMyself()),
                        CreateWarlockSummonPet(),
                        PartyBuff.BuffGroup("Dark Intent"),
                        Spell.BuffSelf( "Grimoire of Sacrifice", ret => GetCurrentPet() != WarlockPet.None )
                        )
                    )
                );
        }

        private static bool NeedToSoulstoneMyself()
        {
            bool cast = WarlockSettings.UseSoulstone == Soulstone.Self 
                || (WarlockSettings.UseSoulstone == Soulstone.Auto && SingularRoutine.CurrentWoWContext != WoWContext.Instances && !SingularSettings.Instance.DisableAllMovement );
            return cast;
        }

        #endregion

        #region CombatBuffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warlock)]
        public static Composite CreateWarlockCombatBuffs()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Spell.WaitForCast(true),
                new Decorator( ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector( 
                        Item.CreateUsePotionAndHealthstone( 50, 0),

                        new Decorator( 
                            ret => Me.IsInGroup() && WarlockSettings.UseSoulstone != Soulstone.None && WarlockSettings.UseSoulstone != Soulstone.Self,
                            CreateWarlockRessurectBehavior(ctx => Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead) ?? Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead))
                            ),

                        // fear anything nott my target within 8yds 
                        Spell.Buff("Fear", 
                            on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => (u.Combat || Battlegrounds.IsInsideBattleground) && u.CurrentTargetGuid == Me.Guid && Me.CurrentTargetGuid != u.Guid && u.Distance < 8f),
                            req => WarlockSettings.UseFear),

                        // fear my target if my health is dangerously low and his isn't
                        Spell.Buff("Fear", 
                            ret => WarlockSettings.UseFear 
                                && Me.HealthPercent < Me.CurrentTarget.HealthPercent && Me.HealthPercent < 35),

                        Spell.BuffSelf("Dark Soul: Misery", ret => Me.Specialization == WoWSpec.WarlockAffliction && Me.CurrentTarget.IsBoss || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingUs()) >= 3),
                        Spell.BuffSelf("Dark Soul: Instability", ret => Me.Specialization == WoWSpec.WarlockDestruction && Me.CurrentTarget.IsBoss || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingUs()) >= 3),
                        Spell.BuffSelf("Summon Doomguard", ret => Me.CurrentTarget.IsBoss && PartyBuff.WeHaveBloodlust),
                        Spell.BuffSelf("Grimoire of Service", ret => Me.CurrentTarget.IsBoss),

                        // lower threat if tanks nearby to pickup
                        Spell.BuffSelf("Soulshatter",
                            ret => SingularRoutine.CurrentWoWContext == WoWContext.Instances 
                                && Group.Tanks.Any(t => t.IsAlive && t.Distance < 50)
                                && Unit.NearbyUnfriendlyUnits.Any(u => u.CurrentTargetGuid == Me.Guid)),

                        // lower threat if voidwalker nearby to pickup
                        Spell.BuffSelf("Soulshatter",
                            ret => SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds 
                                && !Group.Tanks.Any(t => t.IsAlive && t.Distance < 50)
                                && GetCurrentPet() == WarlockPet.Voidwalker 
                                && Unit.NearbyUnfriendlyUnits.Any(u => u.CurrentTargetGuid == Me.Guid)),

                        Spell.BuffSelf( "Dark Regeneration", ret => Me.HealthPercent < 30 ),

                        new Decorator(
                            ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 4
                                || (Me.GotTarget && Me.CurrentTarget.IsPlayer && Unit.ValidUnit(Me.CurrentTarget)),
                            new PrioritySelector(
                                Spell.BuffSelf("Dark Soul: Misery"),
                                Spell.BuffSelf("Summon Doomguard"),
                                Spell.BuffSelf("Grimoire of Service"),
                                Spell.BuffSelf("Unending Resolve"),
                                Spell.Cast("Sacrificial Pact", ret => GetCurrentPet() != WarlockPet.None && Me.Pet.HealthPercent > 60),
                                Spell.Cast("Dark Bargain")
                                )
                            ),

                        new Decorator( 
                            ret => GetCurrentPet() != WarlockPet.None 
                                && Me.Pet.HealthPercent < 40
                                && Me.Pet.Distance < 45
                                && Me.Pet.InLineOfSpellSight ,
                            new Sequence(
                                new PrioritySelector(
                                    CreateCastSoulburn( ret => true),
                                    new ActionAlwaysSucceed()
                                    ),
                                new Action( ret => Logger.Write( "^Health Funnel since Pet @ {0:F1}%", Me.Pet.HealthPercent )),
                                Spell.Heal(ret => "Health Funnel", ret => false, on => Me.Pet, req => true, req => false)
                                )
                            ),

                        Spell.BuffSelf("Life Tap",
                            ret => Me.ManaPercent < SingularSettings.Instance.PotionMana
                                && Me.HealthPercent > SingularSettings.Instance.PotionHealth),

                        PartyBuff.BuffGroup("Dark Intent"),

                        new Throttle( 2,
                            new PrioritySelector(
                                Spell.Buff( "Curse of the Elements", true,
                                    ret => !Me.CurrentTarget.HasMyAura("Curse of Enfeeblement")
                                        && !Me.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModDamageTaken)),

                                Spell.Buff("Curse of Enfeeblement", true,
                                    ret => !Me.CurrentTarget.HasMyAura("Curse of the Elements")
                                        && !Me.CurrentTarget.HasDemoralizing())
                                )
                            ),

                        // mana restoration - match against survival cooldowns
                        new Decorator( 
                            ret => Me.ManaPercent < 60,
                            new PrioritySelector(
                                Spell.BuffSelf("Life Tap", ret => Me.HealthPercent > 50 && Me.HasAnyAura("Glyph of Healthstone")),
                                Spell.BuffSelf("Life Tap", ret => Me.HealthPercent > 50 && Me.HasAnyAura("Unending Resolve")),
                                Spell.BuffSelf("Life Tap", ret => Me.HealthPercent > 50 && Me.HasAnyAura("Dark Regeneration")),
                                Spell.BuffSelf("Life Tap", ret => Me.HasAnyAura("Sacrificial Pact")),
                                Spell.BuffSelf("Life Tap", ret => Me.HasAnyAura("Dark Bargain")),
                                Spell.BuffSelf("Life Tap", ret => Me.ManaPercent < 30 && Me.HealthPercent > 60)
                                )
                            )
                        )
                    )
                );
        }

        #endregion

        /// <summary>
        /// summons warlock pet user configured.  handles cases where pet is gone and
        /// auto-summons (when landing after flying, after some movies/videos, instance changes, etc.)
        /// </summary>
        /// <returns></returns>
        private static Composite CreateWarlockSummonPet()
        {
            return new Decorator(
                ret => !SingularSettings.Instance.DisablePetUsage
                    && !Me.HasAura( "Grimoire of Sacrifice")        // don't summon pet if this buff active
                    && GetBestPet() != GetCurrentPet()
                    && PetManager.PetTimer.IsFinished
                    && SpellManager.CanCast( "Summon Imp"), 

                new Sequence(
                    // wait for possible auto-spawn if supposed to have a pet and none present
                    new DecoratorContinue(
                        ret => GetCurrentPet() == WarlockPet.None && GetBestPet() != WarlockPet.None,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("Summon Pet:  waiting briefly for live pet to appear", GetBestPet().ToString())),
                            new WaitContinue( 
                                TimeSpan.FromMilliseconds( 1000),
                                ret => GetCurrentPet() != WarlockPet.None, 
                                new Sequence(
                                    new Action( ret => Logger.WriteDebug("Summon Pet:  live {0} detected", GetCurrentPet().ToString())),
                                    new Action( r => { return RunStatus.Failure; } )
                                    )
                                )
                            )
                        ),

                    // dismiss pet if wrong one is alive
                    new DecoratorContinue( 
                        ret => GetCurrentPet() != GetBestPet() && GetCurrentPet() != WarlockPet.None,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("Summon Pet:  dismissing {0}", GetCurrentPet().ToString())),
                            new Action(ctx => Lua.DoString("PetDismiss()")),
                            new WaitContinue( 
                                TimeSpan.FromMilliseconds(1000),
                                ret => GetCurrentPet() == WarlockPet.None,
                                new Action( ret => {
                                    Logger.WriteDebug("Summon Pet:  dismiss complete", GetCurrentPet().ToString());
                                    return RunStatus.Success; 
                                    })
                                )
                            )
                        ),

                    // summon pet best pet (unless best is none)
                    new DecoratorContinue(
                        ret => GetBestPet() != WarlockPet.None && GetBestPet() != GetCurrentPet(),
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("Summon Pet:  about to summon{0}", GetBestPet().ToString().CamelToSpaced())),

                            // Heal() used intentionally here (has spell completion logic not present in Cast())
                            Spell.Heal( n => "Summon" + GetBestPet().ToString().CamelToSpaced(), 
                                chkMov => true,
                                onUnit => Me, 
                                req => true,
                                cncl => false,
                                false ),

                            // make sure we see pet alive before continuing
                            new Wait( 1, ret => GetCurrentPet() != WarlockPet.None, new ActionAlwaysSucceed() )
                            )
                        )
                    )
                );
        }

        public static Composite CreateCastSoulburn(SimpleBooleanDelegate requirements)
        {
            return new Sequence(
                Spell.BuffSelf("Soulburn", ret => Me.CurrentSoulShards > 0 && requirements(ret)),
                new Wait(TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Soulburn"), new Action(ret => { return RunStatus.Success; }))
                );
        }

        /// <summary>
        /// Cast spells that have a Buff and a cast time. This avoids double
        /// casting and doesn't require additional housekeeping as in PreventDoubleCasting logic
        /// </summary>
        /// <param name="spellName">spell name</param>
        /// <param name="onUnit">target</param>
        /// <param name="requirements">true to cast, false if not</param>
        /// <returns></returns>
        public static Composite BuffWithCastTime(string spellName, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            // throttle makes sure only 1 successful attempt each 500 ms
            return new Throttle(TimeSpan.FromMilliseconds(500),
                // Spell.Heal used because it has spell completion logic not in Spell.Cast
                new Sequence(
                    Spell.Heal(spellName,
                    chkMov => true,
                    ctx => onUnit(ctx),
                    req => requirements(req),
                    cncl => false,
                    false)
                    )
                );

        }


        #region Pet Support

        /// <summary>
        /// determines the best WarlockPet value to use.  Attempts to use 
        /// user setting first, but if choice not available yet will choose Imp 
        /// for instances and Voidwalker for everything else.  
        /// </summary>
        /// <returns>WarlockPet to use</returns>
        public static WarlockPet GetBestPet()
        {
            WarlockPet bestPet = SingularSettings.Instance.Warlock.Pet;
            if (bestPet != WarlockPet.None)
            {
                if (Me.Specialization == WoWSpec.None)
                    return WarlockPet.Imp;

                if (bestPet == WarlockPet.Auto)
                {
                    if (Me.Specialization == WoWSpec.WarlockDemonology)
                        bestPet = WarlockPet.Felguard;
                    else if (Me.Specialization == WoWSpec.WarlockDestruction && Me.Level == Me.MaxLevel)
                        bestPet = WarlockPet.Felhunter;
                    else if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                        bestPet = WarlockPet.Succubus;
                    else if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                        bestPet = WarlockPet.Felhunter;
                    else
                        bestPet = WarlockPet.Voidwalker;
                }

                SpellFindResults sfr;
                if (!SpellManager.FindSpell((int)bestPet, out sfr))
                {
                    // default: use Imp in instances to be sure no Taunt
                    bestPet = SingularRoutine.CurrentWoWContext == WoWContext.Instances ? WarlockPet.Imp : WarlockPet.Voidwalker;
                }
            }

            return bestPet;
        }

        // following enum used only for mapping Grimoire spellids to standard names
        private enum GrimoireOfServicePet
        {
            FelImp = 112866,      // Imp
            Voidlord = 112867,    // Voidwalker
            Shivarra = 112868,    // Succubus
            Observer = 112869,    // Felhunter
            Wrathguard = 112870     // Felguard
        }

        private static Dictionary<string, WarlockPet> demonMap = new Dictionary<string, WarlockPet>()
        {
            { "Fel Imp", WarlockPet.Imp },
            { "Voidlord", WarlockPet.Voidwalker },
            { "Shivarra", WarlockPet.Succubus },
            { "Observer", WarlockPet.Felhunter },
            { "Wrathguard", WarlockPet.Felguard }
        };

        // for some reason, Me.Pet.CreatedBySpellId is often 0 while pet exists
        // .. Name however is always populated if Pet exists
        public static WarlockPet GetCurrentPet()
        {
            if (!Me.GotAlivePet)
                return WarlockPet.None;

            if (HasTalent(WarlockTalent.GrimoireOfSupremacy))
                return demonMap[Me.Pet.CreatureFamilyInfo.Name];

            WarlockPet Pet = WarlockPet.None;
            Enum.TryParse<WarlockPet>(Me.Pet.CreatureFamilyInfo.Name, out Pet);
            return Pet;
        }

        #endregion

        public static Composite CreateWarlockRessurectBehavior(UnitSelectionDelegate onUnit)
        {
            if (!UseSoulstoneForBattleRez())
                return new PrioritySelector();

            if (onUnit == null)
            {
                Logger.WriteDebug("CreateWarlockRessurectBehavior: error - onUnit == null");
                return new PrioritySelector();
            }

            return new Decorator(
                ret => onUnit(ret) != null && onUnit(ret).IsDead && SpellManager.CanCast( "Soulstone", onUnit(ret), true, true),
                new PrioritySelector(
                    Spell.WaitForCast(true),
                    Movement.CreateMoveToRangeAndStopBehavior(ret => (WoWUnit)ret, range => 40f),
                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        Spell.Cast("Soulstone", ret => (WoWUnit)ret)
                        )
                    )
                );
        }

        private static bool UseSoulstoneForBattleRez()
        {
            bool cast = WarlockSettings.UseSoulstone == Soulstone.Ressurect
                || (WarlockSettings.UseSoulstone == Soulstone.Auto && SingularRoutine.CurrentWoWContext == WoWContext.Instances);
            return cast;
        }


    }
}