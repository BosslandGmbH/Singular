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
using System.Drawing;

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
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }

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
                new Decorator(ctx => Me.HasAura("Resurrection Sickness") || Me.IsCasting,
                    new Sequence(
                    new Action(ctx => RunStatus.Success))),

                new Decorator(ctx => SpellManager.HasSpell("Soul Link") && !Me.HasAura("Soul Link") && Me.GotAlivePet,
                    Spell.BuffSelf("Soul Link")),    

                new Decorator(ctx => SingularSettings.Instance.DisablePetUsage && Me.GotAlivePet,
                    new Action(ctx => Lua.DoString("PetDismiss()"))),

                // following will break questing which has some /use item spells that this would cancel
                //new Decorator(
                //    ctx => Me.CastingSpell != null && Me.CastingSpell.Name.Contains("Summon") && Me.GotAlivePet,
                //    new Action(ctx => SpellManager.StopCasting())),

                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Sequence(
                            Spell.BuffSelf("Life Tap", ret => Me.ManaPercent < 80 && Me.HealthPercent > 60 && !Me.HasAnyAura("Drink", "Food")),
                            Helpers.Common.CreateWaitForLagDuration()
                            ),
                        Rest.CreateDefaultRestBehaviour(),

                        Common.CreatWarlockHealthFunnelBehavior( 85 )
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
                Spell.WaitForCastOrChannel(),
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
                || (WarlockSettings.UseSoulstone == Soulstone.Auto && SingularRoutine.CurrentWoWContext != WoWContext.Instances && MovementManager.IsClassMovementAllowed );
            return cast;
        }

        #endregion

        #region CombatBuffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warlock)]
        public static Composite CreateWarlockCombatBuffs()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),

                // important not to use WaitForChannel() here so channelled casts 
                // .. can be checked in Combat behavior
                new Decorator( ret => !Me.IsCasting && !Me.IsChanneling && !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // need combat healing?  check here since mix of buffs and abilities
                        // heal / shield self as needed
                        Spell.BuffSelf("Dark Regeneration", ret => Me.HealthPercent < 40),
                        new Decorator(
                            ret => StyxWoW.Me.HealthPercent < 40 || Me.HasAura("Dark Regeneration"),
                            new PrioritySelector(
                                ctx => Item.FindFirstUsableItemBySpell("Healthstone", "Healing Potion", "Life Spirit"),
                                new Decorator(
                                    ret => ret != null,
                                    new Sequence(
                                        new Action(ret => Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                        new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                        Helpers.Common.CreateWaitForLagDuration())
                                    )
                                )
                            ),

                        new Decorator(
                            ret => Me.HasAura("Demonic Rebirth") && !Me.GotAlivePet,
                            new Sequence(
                                new Action( r => Logger.Write( Color.White, "^Demonic Rebirth active!")),
                                CreateWarlockSummonPet()
                                )
                            ),

                        new Decorator( 
                            ret => Me.IsInGroup() && WarlockSettings.UseSoulstone != Soulstone.None && WarlockSettings.UseSoulstone != Soulstone.Self,
                            CreateWarlockRessurectBehavior(ctx => Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead) ?? Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead))
                            ),


                        // remove our banish if they are our CurrentTarget 
                        Spell.Cast("Banish", ret => Me.CurrentTarget.HasMyAura( "Banish")),
                            
                        // banish someone if they are not current target, attacking us, and 12 yds or more away
                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits
                                .Where(
                                    u => (u.CreatureType == WoWCreatureType.Demon || u.CreatureType == WoWCreatureType.Elemental )
                                        && Me.CurrentTargetGuid != u.Guid
                                        && (u.Aggro || u.PetAggro || (u.Combat && u.IsTargetingMeOrPet))
                                        && !u.IsCrowdControlled()
                                        && u.Distance.Between(10, 30) && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && (!Me.GotTarget || u.Location.Distance(Me.CurrentTarget.Location) > 10))
                                .OrderByDescending(u => u.Distance)
                                .FirstOrDefault(),
                            Spell.Cast("Banish", onUnit => (WoWUnit)onUnit)
                            ),


                        new PrioritySelector(
                            // find an add within 8 yds (not our current target)
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => (u.Combat || Battlegrounds.IsInsideBattleground) && !u.IsStunned() && u.CurrentTargetGuid == Me.Guid && Me.CurrentTargetGuid != u.Guid && u.Distance < 8f),

                            Spell.CastOnGround( "Shadowfury", on => ((WoWUnit)on).Location, ret => ret != null, true),

                            // treat as a heal, but we cast on what would be our fear target -- allow even when fear use disabled
                            Spell.Buff("Mortal Coil", on => (WoWUnit) on ?? Me.CurrentTarget, ret => Me.HealthPercent < 50 ),

                            // fear current target if my health is dangerously low and his not as much
                            Spell.Buff("Howl of Terror", on => Me.CurrentTarget, ret => WarlockSettings.UseFear &&
                                4 <= Unit.NearbyUnfriendlyUnits.Count(u => (u.Combat || Battlegrounds.IsInsideBattleground) && !u.IsStunned() && u.CurrentTargetGuid == Me.Guid && Me.CurrentTargetGuid != u.Guid && u.Distance < 8f)),

                            // fear add if multiple mobs and our health low
                            Spell.Buff("Fear", 
                                on => (WoWUnit) on, 
                                req => WarlockSettings.UseFear 
                                    && Me.HealthPercent < 50
                                    && !((WoWUnit) req).IsUndead ),

                            // fear current target if my health is dangerously low and his not as much
                            Spell.Buff("Fear", 
                                on => Me.CurrentTarget, 
                                ret => WarlockSettings.UseFear 
                                    && Me.HealthPercent < Me.CurrentTarget.HealthPercent 
                                    && Me.HealthPercent < 35
                                    && !Me.CurrentTarget.IsUndead )
                            ),

                        Spell.BuffSelf("Dark Soul: Misery", ret => Me.Specialization == WoWSpec.WarlockAffliction && (Me.CurrentTarget.IsBoss || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingUs()) >= 3)),
                        Spell.BuffSelf("Dark Soul: Instability", ret => Me.Specialization == WoWSpec.WarlockDestruction && (Me.CurrentTarget.IsBoss || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingUs()) >= 3)),
                        Spell.BuffSelf("Dark Soul: Knowledge", ret => Me.Specialization == WoWSpec.WarlockDemonology && Me.GetCurrentPower(WoWPowerType.DemonicFury) > 800 && (Me.CurrentTarget.IsBoss || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingUs()) >= 3)),

                        Spell.Cast("Summon Doomguard", ret => Me.CurrentTarget.IsBoss && PartyBuff.WeHaveBloodlust),
                        Spell.BuffSelf("Grimoire of Service", ret => Me.CurrentTarget.IsBoss || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingUs()) >= 3),

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

                        Spell.BuffSelf("Dark Bargain", ret => Me.HealthPercent < 30),
                        Spell.BuffSelf("Sacrificial Pact", ret => Me.HealthPercent < 60 && GetCurrentPet() != WarlockPet.None && Me.Pet.HealthPercent > 50),

                        // emergency heal needed? soulburn then fail to use drain life immediately
                        new Sequence(
                            CreateCastSoulburn( ret => Me.HealthPercent < 20),
                            Spell.Cast("Drain Life")
                            ),

                        Spell.Cast("Drain Life", ret => Me.HealthPercent < 50 || Me.HasAura("Dark Regeneration")),

                        new Decorator(
                            ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 3
                                || Me.CurrentTarget.IsBoss 
                                || (Me.GotTarget && Me.CurrentTarget.IsPlayer && Unit.ValidUnit(Me.CurrentTarget)),
                            new PrioritySelector(
                                Spell.BuffSelf("Dark Soul: Misery"),
                                Spell.BuffSelf("Grimoire of Service"),
                                Spell.BuffSelf("Unending Resolve")
                                )
                            ),

                        Common.CreatWarlockHealthFunnelBehavior( 40, 99),

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
                            Spell.Cast( n => "Summon" + GetBestPet().ToString().CamelToSpaced(), 
                                chkMov => true,
                                onUnit => Me, 
                                req => true,
                                cncl => false),

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
                    Spell.Cast(spellName,
                    chkMov => true,
                    ctx => onUnit(ctx),
                    req => requirements(req),
                    cncl => false)
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
            WarlockPet bestPet = SingularSettings.Instance.Warlock().Pet;
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

                string spellName = "Summon" + bestPet.ToString().CamelToSpaced();
                SpellFindResults sfr;
                if (!SpellManager.FindSpell(spellName, out sfr))
                {
                    if (SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                        bestPet = WarlockPet.Voidwalker;
                    else if (Me.Level >= 30)
                        bestPet = WarlockPet.Felhunter;
                    else
                        bestPet = WarlockPet.Imp;
                }
            }

            return bestPet;
        }

        /// <summary>
        /// Pet.CreatureFamily.Id values for pets while the
        /// Grimoire of Supremecy talent is selected.  
        /// </summary>
        public enum WarlockGrimoireOfSupremecyPets
        {
            FelImp = 100,
            Wrathguard = 104,
            Voidlord = 101,
            Observer = 103,
            Shivarra = 102
        }
       
        /// <summary>
        /// return standard pet id associated with active pet. 
        /// note: we map Grimoire of Supremecy pets so rest of 
        /// Singular can treat in talent independent fashion
        /// </summary>
        /// <returns></returns>
        public static WarlockPet GetCurrentPet()
        {
            if (!Me.GotAlivePet)
                return WarlockPet.None;

            switch ((WarlockGrimoireOfSupremecyPets) Me.Pet.CreatureFamilyInfo.Id)
            {
                case WarlockGrimoireOfSupremecyPets.FelImp:
                    return WarlockPet.Imp;
                case WarlockGrimoireOfSupremecyPets.Wrathguard:
                    return WarlockPet.Felguard;
                case WarlockGrimoireOfSupremecyPets.Voidlord:
                    return WarlockPet.Voidwalker;
                case WarlockGrimoireOfSupremecyPets.Observer:
                    return WarlockPet.Felhunter;
                case WarlockGrimoireOfSupremecyPets.Shivarra:
                    return WarlockPet.Succubus;
            }

            return (WarlockPet)Me.Pet.CreatureFamilyInfo.Id;
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
                    Spell.WaitForCastOrChannel(),
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


        public static Composite CreatWarlockHealthFunnelBehavior(int petMinHealth, int petMaxHealth = 99)
        {
            return new Decorator(
                ret => GetCurrentPet() != WarlockPet.None
                    && Me.Pet.HealthPercent < petMinHealth 
                    && Me.Pet.Distance < 45
                    && Me.Pet.InLineOfSpellSight
                    && !HasTalent(WarlockTalent.SoulLink),
                new Sequence(
                    new PrioritySelector(
                        CreateCastSoulburn(ret => Me.Combat && Me.Specialization == WoWSpec.WarlockAffliction ),
                        new ActionAlwaysSucceed()
                        ),
                    // new Action(ret => Logger.Write("^Health Funnel since Pet @ {0:F1}%", Me.Pet.HealthPercent)),


                    // for Affliction make it a quick heal

                    // isn't this a dupe of the code above?

                    //new DecoratorContinue(
                    //    ret => SpellManager.HasSpell("Soulburn: Health Funnel") && Me.CurrentSoulShards > 0,
                    //    new PrioritySelector(
                    //        CreateCastSoulburn( ret => true ),
                    //        new ActionAlwaysSucceed()
                    //        )
                    //    ),

                        new Sequence(
                            new Action(ctx => WoWMovement.MoveStop()),
                            Helpers.Common.CreateWaitForLagDuration(),
                            new Action(ret => Logger.Write("Casting Health Funnel on Pet @ {0:F1}%", Me.Pet.HealthPercent)),
                    Spell.Cast(ret => "Health Funnel", ret => false, on => Me.Pet, req => true, req => !Me.GotAlivePet || Me.Pet.HealthPercent >= petMaxHealth )),
                    Helpers.Common.CreateWaitForLagDuration(),
                    new WaitContinue(TimeSpan.FromMilliseconds(500), ret => !Me.IsCasting && Me.GotAlivePet && Me.Pet.HealthPercent < petMaxHealth && Me.HealthPercent > 50, new ActionAlwaysSucceed())
                    )
                );
        }

        public static IEnumerable<WoWUnit> TargetsInCombat
        {
            get
            {
                return Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && u.IsTargetingUs() && !u.IsCrowdControlled() && StyxWoW.Me.IsSafelyFacing(u));
            }
        }

    }
}