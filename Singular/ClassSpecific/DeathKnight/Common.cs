using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using System.Drawing;
using System.Collections.Generic;

namespace Singular.ClassSpecific.DeathKnight
{
    public static class Common
    {
        internal const uint Ghoul = 26125;
        internal const int SuddenDoom = 81340;
        internal const int KillingMachine = 51124;
        internal const int FreezingFog = 59052;

        public static bool glyphEmpowerment { get; set; }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DeathKnightSettings Settings { get { return SingularSettings.Instance.DeathKnight(); } }

        public static bool HasTalent(DeathKnightTalents tal)
        {
            return TalentManager.IsSelected((int)tal);
        }

        [Behavior(BehaviorType.Initialize, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightInitialize()
        {
            scenario = new CombatScenario(44, 1.5f);
            talent.necrotic_plague_enabled = Common.HasTalent(DeathKnightTalents.NecroticPlague);
            talent.breath_of_sindragosa_enabled = Common.HasTalent(DeathKnightTalents.BreathOfSindragosa);
            talent.defile_enabled = Common.HasTalent(DeathKnightTalents.Defile);
            talent.unholy_blight_enabled = Common.HasTalent(DeathKnightTalents.UnholyBlight);

            BloodBoilRange = 10;
            if (TalentManager.HasGlyph("Blood Boil"))
            {
                BloodBoilRange = 15;
                Logger.Write(LogColor.Init, "Glyph of Blood Boil: range of Blood Boil extended to {0}", BloodBoilRange);
            }

            glyphEmpowerment = TalentManager.HasGlyph("Empowerment") && (Me.Specialization == WoWSpec.DeathKnightFrost || Me.Specialization == WoWSpec.DeathKnightUnholy);
            if (glyphEmpowerment)
            {
                Logger.Write(LogColor.Init, "Glyph of Empowerment: Empower Rune Weapon heals for 30%");
            }

            BloodBoilRangeSqr = BloodBoilRange * BloodBoilRange;
            return null;
        }


        internal static int ActiveRuneCount
        {
            get
            {
                return Me.BloodRuneCount + Me.FrostRuneCount + Me.UnholyRuneCount +
                       Me.DeathRuneCount;
            }
        }

        internal static bool GhoulMinionIsActive
        {
            get { return Me.Minions.Any(u => u.Entry == Ghoul); }
        }

        internal static void DestroyGhoulMinion()
        {
            Lua.DoString("DestroyTotem(1)");
        }

        internal static bool NeedsFrostFever(this WoWUnit u)
        {
            if (talent.necrotic_plague_enabled)
                return u.NeedsNecroticPlague();
            return u.HasAuraExpired("Icy Touch", "Frost Fever");
        }
        internal static bool NeedsBloodPlague(this WoWUnit u)
        {
            if (talent.necrotic_plague_enabled)
                return u.NeedsNecroticPlague();
            return u.HasAuraExpired("Plague Strike", "Blood Plague");
        }

        internal static bool NeedsNecroticPlague(this WoWUnit u)
        {
            return !talent.necrotic_plague_enabled ? false : u.HasKnownAuraExpired("Necrotic Plague");
        }

        internal static bool ShouldSpreadDiseases
        {
            get
            {
                return disease.ticking_on(Me.CurrentTarget) 
                    && Unit.UnfriendlyUnits(Common.BloodBoilRange).Any(u => !disease.ticking_on(u));
            }
        }

        internal static int BloodRuneSlotsActive { get { return Me.GetRuneCount(0) + Me.GetRuneCount(1); } }
        internal static int FrostRuneSlotsActive { get { return Me.GetRuneCount(2) + Me.GetRuneCount(3); } }
        internal static int UnholyRuneSlotsActive { get { return Me.GetRuneCount(4) + Me.GetRuneCount(5); } }
        internal static int DeathRuneSlotsActive { get { return Me.GetRuneCount(RuneType.Death); } }


        internal static CombatScenario scenario { get; set; }


        /// <summary>
        /// check that we are in the last tick of Frost Fever or Blood Plague on current target and have a fully depleted rune
        /// </summary>
        internal static bool CanCastPlagueLeech
        {
            get
            {
                // check talent only to avoid some unnecessary LUA if not needed
                if (!HasTalent(DeathKnightTalents.PlagueLeech) || !Me.GotTarget())
                    return false;

                WoWAura auraFrostFever = Me.GetAllAuras().Where(a => a.Name == "Frost Fever" && a.TimeLeft.TotalMilliseconds > 250).FirstOrDefault();
                WoWAura auraBloodPlague = Me.GetAllAuras().Where(a => a.Name == "Blood Plague" && a.TimeLeft.TotalMilliseconds > 250).FirstOrDefault();
                if (auraFrostFever == null || auraBloodPlague == null)
                    return false;

                bool depletedBlood, depletedFrost, depletedUnholy;

                // Check Runes per http://wow.joystiq.com/2013/06/25/lichborne-patch-5-4-patch-note-analysis-for-death-knights/#continued
                if (TalentManager.CurrentSpec == WoWSpec.DeathKnightUnholy)
                {
                    depletedBlood = BloodRuneSlotsActive == 0;
                    depletedFrost = FrostRuneSlotsActive == 0;
                    return (depletedFrost && depletedBlood);
                }

                depletedFrost = FrostRuneSlotsActive == 0;
                depletedUnholy = UnholyRuneSlotsActive == 0;

                return (depletedFrost && depletedUnholy);
            }
        }

        public static int BloodBoilRange;
        public static int BloodBoilRangeSqr;

        #region Pull

        // All DKs should be throwing death grip when not in intances. It just speeds things up, and makes a mess for PVP :)
        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, (WoWSpec)int.MaxValue, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightNormalAndPvPPull()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        CreateDarkSuccorBehavior(),
                        Common.CreateGetOverHereBehavior(),
                        Spell.Cast("Outbreak"),
                        Spell.Cast("Howling Blast"),
                        Spell.Buff("Icy Touch")
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        // Non-blood DKs shouldn't be using Death Grip in instances. Only tanks should!
        // You also shouldn't be a blood DK if you're DPSing. Thats just silly. (Like taking a prot war as DPS... you just don't do it)
        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Instances)]
        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Instances)]
        public static Composite CreateDeathKnightFrostAndUnholyInstancePull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),
                new Decorator( 
                    req => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(
                            Movement.WaitForFacing(),
                            Movement.WaitForLineOfSpellSight(),

                            Spell.Cast("Howling Blast"),
                            Spell.Buff("Icy Touch")
                            )
                    ),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region PreCombatBuffs

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightPreCombatBuffs()
        {
            return new PrioritySelector(
                CreateDeathKnightPresenceBehavior(),

                // limit PoF to once every ten seconds in case there is some
                // .. oddness here
                new Throttle(10, Spell.BuffSelf("Path of Frost", req => Settings.UsePathOfFrost)),

                // Bone Shield has 1 min cd and 5 min duration, so cast out of combat if possible
                Spell.BuffSelf( "Bone Shield", req => Me.IsInInstance || Battlegrounds.IsInsideBattleground),

                // Attack Power Buff
                Spell.BuffSelf("Horn of Winter", req => !Me.HasPartyBuff(PartyBuffType.AttackPower))
                );
        }

        #endregion

        #region Pull Buffs

        [Behavior(BehaviorType.PullBuffs, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightPullBuffs()
        {
            return new PrioritySelector(
                CreateDeathKnightPresenceBehavior(),

                // Attack Power Buff
                Spell.BuffSelf("Horn of Winter", req => !Me.HasPartyBuff(PartyBuffType.AttackPower))
                );
        }

        #endregion

        #region Loss of Control 

        [Behavior(BehaviorType.LossOfControl, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightLossOfControlBehavior()
        {
            return new Decorator(
                req => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    new PrioritySelector(
                        new Decorator(req => Settings.AutoClearAuraWithDarkSimulacrum && Me.HasMyAura("Ice Block"), new Action(r => Me.CancelAura("Ice Block"))),

                        new Sequence(
                            Spell.BuffSelf("Hand of Protection", req => Me.HasAura("Touch of Karma") || Me.IsCrowdControlled()),
                            new Wait( 1, until => Me.HasAura("Hand of Protection") && Me.HealthPercent > 10, new ActionAlwaysFail())
                            ),
                        new Decorator( req => Settings.AutoClearAuraWithDarkSimulacrum && Me.HasMyAura("Hand of Protection") && Me.HealthPercent > 10, new Action( r => Me.CancelAura("Hand of Protection"))),

                        new Sequence(
                            Spell.BuffSelf("Divine Shield", req => Me.HasAura("Touch of Karma") || Me.IsCrowdControlled()),
                            new Wait( 1, until => Me.HasAura("Divine Shield") && Me.HealthPercent > 10, new ActionAlwaysSucceed())
                            ),
                        new Decorator( req => Settings.AutoClearAuraWithDarkSimulacrum && Me.HasMyAura("Divine Shield") && Me.HealthPercent > 10, new Action( r => Me.CancelAura("Divine Shield")))
                        ),

                    Spell.BuffSelf("Icebound Fortitude", req => Me.HealthPercent < Settings.IceboundFortitudePercent),

                    Spell.BuffSelf("Lichborne", req => Me.HasAuraWithEffect( WoWApplyAuraType.ModFear) || Me.Fleeing )

                    )
                );
        }

        #endregion 

        #region Heal

        [Behavior(BehaviorType.Heal, WoWClass.DeathKnight)]
        public static Composite CreateDeathKnightHeals()
        {
            return new Decorator(
                req => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    Spell.Cast("Death Coil", on => Me, req => Me.HasAura("Lichborne") && Me.HealthPercent < Settings.LichbornePercent),

                    Spell.BuffSelf("Death Pact", req => Me.HealthPercent < Settings.DeathPactPercent ),

                    Spell.Cast("Death Siphon",
                        req => Common.HasTalent( DeathKnightTalents.DeathSiphon) 
                            && Me.GotTarget() && Me.CurrentTarget.InLineOfSpellSight && Me.IsSafelyFacing(Me.CurrentTarget)
                            && Me.HealthPercent < Settings.DeathSiphonPercent),

                    Spell.BuffSelf("Conversion",
                        req => Common.HasTalent( DeathKnightTalents.Conversion) 
                            && Me.HealthPercent < Settings.ConversionPercent 
                            && Me.RunicPowerPercent >= Settings.MinimumConversionRunicPowerPrecent),

                    new Decorator(
                        req => TalentManager.CurrentSpec == WoWSpec.DeathKnightBlood,
                        Spell.HandleOffGCD(
                            new Sequence(
                                Spell.BuffSelf("Rune Tap", req => Me.HealthPercent < Settings.RuneTapPercent),
                                new Wait( TimeSpan.FromMilliseconds(500), until => Me.HasAura("Rune Tap"), new ActionAlwaysSucceed())
                                )
                            )
                        ),

                    // **** Defensive Cooldowns *** 

                    // following for DPS only -- let Blood fall through in instances
                    Spell.Cast("Death Strike",
                        req => (TalentManager.CurrentSpec != WoWSpec.DeathKnightBlood || SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                            && Me.GotTarget() && Me.CurrentTarget.InLineOfSpellSight && Me.IsSafelyFacing(Me.CurrentTarget)
                            && Me.HealthPercent < Settings.DeathStrikeEmergencyPercent),

                    // use it to heal with deathcoils.
                    Spell.BuffSelf("Lichborne",
                        req => Me.HealthPercent < Settings.LichbornePercent 
                            && Me.CurrentRunicPower >= 60
                            && (!Settings.LichborneExclusive || !Me.HasAnyAura( "Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Icebound Fortitude"))),

                    // *** Dark Simulacrum saved abilities ***
                    new Decorator(
                        req => Spell.CanCastHack("Ice Block") && Me.HasAuraWithEffect(WoWApplyAuraType.PeriodicDamage, WoWApplyAuraType.PeriodicDamagePercent),
                        new Sequence(
                            Spell.BuffSelf("Ice Block"),
                            new Action(r => Logger.Write(Color.DodgerBlue, "^Ice Block"))
                            )
                        ),

                    Spell.BuffSelf("Hand of Freedom", req => Me.IsRooted() || Me.IsSlowed(30)),

                    // *** Defensive Cooldowns ***
                    // Anti-magic shell - no cost and doesnt trigger GCD 
                        Spell.BuffSelf(
                            "Anti-Magic Shell",
                            req => Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == Me.Guid)
                            ),

                    // we want to make sure our primary target is within melee range so we don't run outside of anti-magic zone.
                        Spell.CastOnGround(
                            "Anti-Magic Zone",
                            on => Me,
                            req => Common.HasTalent(DeathKnightTalents.AntiMagicZone)
                                && !Me.HasAura("Anti-Magic Shell")
                                && Unit.NearbyUnfriendlyUnits
                                    .Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == Me.Guid)
                                && Targeting.Instance.FirstUnit != null
                                && Targeting.Instance.FirstUnit.IsWithinMeleeRange
                            ),

                        Spell.BuffSelf("Icebound Fortitude", req => Me.HealthPercent < Settings.IceboundFortitudePercent),

                        Spell.BuffSelf("Lichborne", req => Me.IsCrowdControlled()),

                        Spell.BuffSelf("Desecrated Ground", req => Common.HasTalent(DeathKnightTalents.DesecratedGround) && Me.IsCrowdControlled()),

                        Helpers.Common.CreateCombatRezBehavior("Raise Ally", req => ((WoWUnit)req).SpellDistance() < 40 && ((WoWUnit)req).InLineOfSpellSight)

                    )
                );
        }

        #endregion

        #region CombatBuffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Normal)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Normal)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightCombatBuffs()
        {
            return new Decorator(
                req => !Me.GotTarget() || !Me.CurrentTarget.IsTrivial(),
                new PrioritySelector(

                        // *** Offensive Cooldowns ***
                        
                        Spell.BuffSelf(
                            "Remorseless Winter", 
                            req => 
                            {
                                if (Spell.IsSpellOnCooldown("Remorseless Winter"))
                                    return false;

                                if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                                {
                                    if (!Me.GotTarget())
                                        return false;
                                    if (!Me.CurrentTarget.IsPlayer || Me.CurrentTarget.SpellDistance() > 8)
                                        return false;
                                    Logger.Write(LogColor.Hilite, "^Remorseless Winter: slowing {0} @ {1:F1} yds", Me.CurrentTarget.SafeName(), Me.CurrentTarget.SpellDistance());
                                    return true;
                                }

                                WoWUnit unit = Unit.UnfriendlyUnits(8).FirstOrDefault( u => u.IsPlayer && !u.IsMelee());
                                if (unit == null)
                                    return false;

                                Logger.Write(LogColor.Hilite, "^Remorseless Winter: slowing non-melee {0} @ {1:F1} yds", unit.SafeName(), unit.SpellDistance());
                                return true;
                            }),

                        // I need to use Empower Rune Weapon to use Death Strike
                        Spell.BuffSelf("Empower Rune Weapon",
                            req =>
                            {
                                if (StyxWoW.Me.HealthPercent <= Settings.EmpowerRuneWeaponPercent)
                                {
                                    if (glyphEmpowerment)
                                        return true;

                                    if (Me.GotTarget()
                                        && Me.CurrentTarget.IsWithinMeleeRange
                                        && Me.IsSafelyFacing(Me.CurrentTarget)
                                        && Me.CurrentTarget.InLineOfSpellSight
                                        && !Spell.CanCastHack("Death Strike"))
                                        return true;
                                }
                                return false;
                            }),

                        // I'm unholy and I don't have a pet or I am blood/frost and I am using pet as dps bonus
                        Spell.BuffSelf("Raise Dead",
                            req => TalentManager.CurrentSpec == WoWSpec.DeathKnightUnholy
                                && SingularRoutine.IsAllowed(Styx.CommonBot.Routines.CapabilityFlags.PetSummoning)
                                && !Me.GotAlivePet
                            ),

                        // never use army of the dead in instances if not blood specced unless you have the army of the dead glyph to take away the taunting
                        Spell.BuffSelf("Army of the Dead",
                            req => Settings.UseArmyOfTheDead
                                && Helpers.Common.UseLongCoolDownAbility
                                && (SingularRoutine.CurrentWoWContext != WoWContext.Instances || TalentManager.HasGlyph("Army of the Dead"))),

                        Spell.BuffSelf("Empower Rune Weapon",
                                req => Helpers.Common.UseLongCoolDownAbility && Me.RunicPowerPercent < 70 && ActiveRuneCount == 0),

                        Spell.BuffSelf("Death's Advance",
                            req => Common.HasTalent(DeathKnightTalents.DeathsAdvance)
                                && Me.GotTarget()
                                && (!Spell.CanCastHack("Death Grip") || SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                                && Me.CurrentTarget.DistanceSqr > 10 * 10),

                        Spell.BuffSelf("Blood Tap", req => Common.NeedBloodTap()),

                        Spell.Cast("Plague Leech", req => CanCastPlagueLeech),

                        // Attack Power Buff
                        Spell.BuffSelf("Horn of Winter", req => !Me.HasPartyBuff(PartyBuffType.AttackPower))
                    )
                );
        }

        #endregion

        #region Presence

        /// <summary>
        /// presence doesn't change change, so only need to call in PreCombatBuffs and PullBuffs. 
        /// </summary>
        /// <returns></returns>
        public static Composite CreateDeathKnightPresenceBehavior()
        {
            return Spell.BuffSelf(sp => SelectedPresence.ToString() + " Presence", req => SelectedPresence != DeathKnightPresence.None);
        }

        /// <summary>
        /// returns the users selected Presence after validating.  return is guarranteed
        /// to be valid for casting if != .None
        /// </summary>
        public static DeathKnightPresence SelectedPresence
        {
            get
            {
                var Presence = Settings.Presence;
                if ( Presence == DeathKnightPresence.None)
                    return Presence;

                if (Presence == DeathKnightPresence.Auto)
                {
                    switch (TalentManager.CurrentSpec)
                    {
                        case WoWSpec.DeathKnightBlood:
                            Presence =  DeathKnightPresence.Blood;
                            break;
                        default:
                        case WoWSpec.DeathKnightFrost:
                            Presence = DeathKnightPresence.Frost;
                            break;
                        case WoWSpec.DeathKnightUnholy:
                            Presence = DeathKnightPresence.Unholy ;
                            break;
                    }
                }

                if (!SpellManager.HasSpell(Presence.ToString() + " Presence"))
                {
                    Presence = DeathKnightPresence.Frost;
                    if (!SpellManager.HasSpell(Presence.ToString() + " Presence"))
                    {
                        Presence = DeathKnightPresence.None;
                    }
                }

                return Presence;
            }
        }

        #endregion 

        #region Death Grip

        public static Composite CreateGetOverHereBehavior()
        {
            return new Throttle( 1,
                new PrioritySelector(
                    CreateDeathGripBehavior(),
                    new Decorator(
                        req => {
							if (!Me.GotTarget())
								return false;
							if (!(Me.Combat || Me.CurrentTarget.Combat))
								return false;
							return (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsMovingAway());
						},
                        CreateChainsOfIceBehavior()
                        )
                    )
                );
        }

        public static Composite CreateDeathGripBehavior()
        {
            return new Sequence(
                Spell.Cast("Death Grip", 
                    req => !MovementManager.IsMovementDisabled 
                        && !Me.CurrentTarget.IsBoss() 
                        && Me.CurrentTarget.DistanceSqr > 10 * 10 
                        && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.TaggedByMe || (!Me.CurrentTarget.TaggedByOther && Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull && SingularRoutine.CurrentWoWContext != WoWContext.Instances))
                    ),
                new DecoratorContinue( req => Me.IsMoving, new Action(req => StopMoving.Now())),
                new WaitContinue( 1, until => !Me.GotTarget() || Me.CurrentTarget.IsWithinMeleeRange, new ActionAlwaysSucceed())
                );
        }

        public static Composite CreateChainsOfIceBehavior()
        {
            return Spell.Buff("Chains of Ice", req => Unit.CurrentTargetIsMovingAwayFromMe && !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost));
        }

        public static Composite CreateDarkSuccorBehavior()
        {
            // health below determined %
            // user wants to cast on cooldown without regard to health
            // we have aura AND (target is about to die OR aura expires in less than 3 secs)
            return new Decorator(
                req => Me.GetAuraTimeLeft("Dark Succor").TotalMilliseconds > 250
                    && (Me.HealthPercent < 80 || Me.GetAuraTimeLeft("Dark Succor").TotalMilliseconds < 3000 || (Me.GotTarget() && Me.CurrentTarget.TimeToDeath(99) < 6) 
                    && Me.CurrentTarget.InLineOfSpellSight 
                    && Me.IsSafelyFacing( Me.CurrentTarget)
                    && Spell.CanCastHack("Death Strike", Me.CurrentTarget)),
                new Sequence(
                    new Action( r => Logger.WriteDebug( Color.White, "Dark Succor ({0} ms left) influenced Death Strike coming....", (int) Me.GetAuraTimeLeft("Dark Succor").TotalMilliseconds  )),
                    Spell.Cast("Death Strike")
                    )
                );
        }

        private static bool IsValidDarkSimulacrumTarget(WoWUnit u)
        {
            return u.PowerType == WoWPowerType.Mana && Me.IsSafelyFacing(u, 150) && u.InLineOfSpellSight;
        }

        public static WoWSpell GetDarkSimulacrumStolenSpell()
        {
            SpellFindResults sfr;
            if (!SpellManager.FindSpell("Dark Simulacrum", out sfr))
                return null;
            return sfr.Override;
        }

        public static bool HasDarkSimulacrumSpell(string spellName)
        {
            WoWSpell stolenSpell = GetDarkSimulacrumStolenSpell();
            if (stolenSpell == null)
                return false;
            return stolenSpell.Name.Equals(spellName, StringComparison.InvariantCultureIgnoreCase);
        }

        public static Composite CreateDarkSimulacrumBehavior()
        {
            if (Settings.TargetWithDarkSimulacrum == DarkSimulacrumTarget.None)
                return new ActionAlwaysFail();

            UnitSelectionDelegate onUnit;
            if (Settings.TargetWithDarkSimulacrum == DarkSimulacrumTarget.All || WoWContext.Normal == SingularRoutine.CurrentWoWContext)
                onUnit = on =>
                {
                    if (Me.GotTarget() && IsValidDarkSimulacrumTarget(Me.CurrentTarget))
                        return Me.CurrentTarget;
                    return Unit.NearbyUnitsInCombatWithUsOrOurStuff.FirstOrDefault(u => IsValidDarkSimulacrumTarget(u));
                };
            else // Healers
                onUnit = on => Unit.NearbyUnitsInCombatWithUsOrOurStuff
                    .FirstOrDefault(
                        u => u.IsPlayer
                            && (u.ToPlayer().Specialization == WoWSpec.DruidRestoration
                                || u.ToPlayer().Specialization == WoWSpec.MonkMistweaver
                                || u.ToPlayer().Specialization == WoWSpec.PaladinHoly
                                || u.ToPlayer().Specialization == WoWSpec.PriestDiscipline
                                || u.ToPlayer().Specialization == WoWSpec.PriestHoly
                                || u.ToPlayer().Specialization == WoWSpec.ShamanRestoration)
                            && IsValidDarkSimulacrumTarget(u)
                        );

            return new PrioritySelector(

                Spell.Cast("Dark Simulacrum", onUnit, req => !Me.HasMyAura("Dark Simulacrum")),

                new Throttle(45,
                    new Decorator(
                        req => Me.HasMyAura("Dark Simulacrum"),
                        new Action(r =>
                        {
                            WoWSpell stolenSpell = GetDarkSimulacrumStolenSpell();
                            if (stolenSpell == null)
                                return RunStatus.Failure;
                            string strType = "";
                            if (stolenSpell.IsHeal())
                                strType = "heal ";
                            else if (stolenSpell.IsDamageRedux())
                                strType = "buff ";

                            Logger.Write(Color.DodgerBlue, "^Dark Simulacrum: we gained {0}[{1}] #{2}", strType, stolenSpell.Name, stolenSpell.Id);
                            return RunStatus.Success;
                        })
                        )
                    ),
                new PrioritySelector(
                    ctx =>
                    {
                        /*
                        if (!Me.HasMyAura("Dark Simulacrum"))
                            return null;
                        */
                        SpellFindResults sfr;
                        if (!SpellManager.FindSpell("Dark Simulacrum", out sfr))
                            return null;
                        if (sfr.Override == null)
                            return null;

                        // suppress cast for certain ones we will specifically reference in combat buffs, etc
                        if (dontImmediatelyCastThese.Contains(sfr.Override.Id))
                            return null;

                        // if a heal, then target self or friendly as appropriate
                        bool isHeal = sfr.Override.IsHeal();
                        if (isHeal)
                        {
                            if (Me.HealthPercent < 60)
                                return Me;

                            return Unit.GroupMembers
                                .Where(u => u.HealthPercent < 90 && u.Distance < sfr.Override.MaxRange && u.InLineOfSpellSight)
                                .OrderBy(u => (int)u.HealthPercent)
                                .FirstOrDefault();
                        }

                        if (sfr.Override.IsDamageRedux())
                            return Me;

                        // otherwise, cast immediately on enemy
                        return Me.CurrentTarget;
                    },

                    Spell.Cast("Dark Simulacrum", on => (WoWUnit)on)
                    )
                );
        }

        private static HashSet<int> dontImmediatelyCastThese = new HashSet<int>()
        {
            1022,   // Hand of Protection
            45438,  // Ice Block
            642,    // Divine Shield
            1044,   // Hand of Freedom
        };

        public static Composite CreateSoulReaperHasteBuffBehavior()
        {
            return new PrioritySelector(
                ctx =>
                {
                    WoWUnit unit = ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
                        .Where(u => IsSoulReaperHasteTarget(u))
                        .OrderBy(u => u.CurrentHealth)
                        .FirstOrDefault();
                    return unit;
                },

                Spell.Cast("Soul Reaper", on => (WoWUnit)on, req => req != null && !Me.HasAura("Soul Reaper"))
                );
        }

        /// <summary>
        /// check if target likely to die within 6 seconds and we have a reasonable
        /// shot at getting Soul Reaper haste buff
        /// </summary>
        /// <param name="u"></param>
        /// <returns></returns>
        private static bool IsSoulReaperHasteTarget(WoWUnit u)
        {
            if ( u.IsTotem )
            {
                if (u.SummonedByUnit == null || !u.SummonedByUnit.IsPlayer || u.SummonedByUnit.Class != WoWClass.Shaman || !Unit.ValidUnit(u.SummonedByUnit))
                {
                    return false;
                }
            }
            else if (u.Guid == Me.CurrentTargetGuid)
            {
                if (u.TimeToDeath(99) > 3)
                {
                    return false;
                }
            }
            else if (u.IsTrivial())
            {

            }
            else if (u.IsStressful())
            {
                return false;
            }
            else if (u.HealthPercent > 5)
            {
                return false;
            }

            bool inAttackablePosition = u.IsWithinMeleeRange && Me.IsSafelyFacing(u, 150) && u.InLineOfSpellSight;
            return inAttackablePosition;
        }

        public static Composite CreateApplyDiseases()
        {
            // throttle to avoid/reduce following an Outbreak with a Plague Strike for example
            return new Throttle(
                new PrioritySelector(
                // abilities that don't require Runes first
                    Spell.BuffSelf(
                        "Unholy Blight",
                        req => Spell.CanCastHack("Unholy Blight")
                            && Unit.NearbyUnfriendlyUnits.Any(u => (u.IsPlayer || u.IsBoss()) && u.Distance < (u.MeleeDistance() + 5) && u.NeedsBloodPlague())),

                    Spell.Cast("Outbreak", req => Me.CurrentTarget.NeedsFrostFever() || Me.CurrentTarget.NeedsBloodPlague()),

                    // now Rune based abilities
                    Spell.Buff(
                        "Plague Strike",
                        req => TalentManager.CurrentSpec == WoWSpec.DeathKnightUnholy
                            && (Me.CurrentTarget.NeedsFrostFever() || Me.CurrentTarget.NeedsBloodPlague())
                        ),

                    new Decorator(
                        req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.CurrentTarget.NeedsFrostFever(),
                        new PrioritySelector(
                            Spell.Cast("Howling Blast", req => Spell.UseAOE && TalentManager.CurrentSpec == WoWSpec.DeathKnightFrost),
                            Spell.Buff("Icy Touch", req => !Spell.UseAOE || TalentManager.CurrentSpec != WoWSpec.DeathKnightFrost)
                            )
                        ),

                    Spell.Buff("Plague Strike", req => Me.CurrentTarget.NeedsBloodPlague())
                    )
                );
        }

        #endregion

        /// <summary>
        /// invoke on CurrentTarget if not tagged. use ranged instant casts if possible.  this  
        /// is a blend of abilities across all specializations
        /// </summary>
        /// <returns></returns>
        public static Composite CreateDeathKnightPullMore()
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return new ActionAlwaysFail();

            return new Throttle(
                2,
                new Decorator(
                    req => Me.GotTarget()
                        && !Me.CurrentTarget.IsPlayer
                        && !Me.CurrentTarget.IsTagged
                        && !Me.CurrentTarget.IsWithinMeleeRange,
                    new PrioritySelector(
                        new Sequence(
                            ctx => Me.CurrentTarget,
                            Spell.Cast("Death Grip", on => (on as WoWUnit)),
                            new DecoratorContinue( req => Me.IsMoving, new Action(req => StopMoving.Now())),
                            new WaitContinue( TimeSpan.FromMilliseconds(500), until => !Me.IsMoving, new ActionAlwaysSucceed()),
                            new WaitContinue( 1, until => (until as WoWUnit).IsWithinMeleeRange, new ActionAlwaysSucceed())
                            ),
                        Spell.Cast("Outbreak"),
                        Spell.Buff("Icy Touch"),
                        Spell.Cast("Death Siphon"),
                        Spell.Cast("Dark Command", req => Me.Specialization == WoWSpec.DeathKnightBlood ),
                        Spell.Cast("Death Coil")
                        )
                    )
                );
        }

        public static bool NeedBloodTap()
        {
            const int BLOOD_CHARGE = 114851;
            return StyxWoW.Me.HasAura(BLOOD_CHARGE, 5) && Common.DeathRuneSlotsActive < 2 && (Common.BloodRuneSlotsActive == 0 || Common.FrostRuneSlotsActive == 0 || Common.UnholyRuneSlotsActive == 0);
        }

    }

    public enum DeathKnightTalents
    {
#if PRE_WOD
        RollingBlood = 1,
        PlagueLeech,
        UnholyBlight,
        LichBorne,
        AntiMagicZone,
        Purgatory,
        DeathsAdvance,
        Chilblains,
        Asphyxiate,
        DeathPact,
        DeathSiphon,
        Conversion,
        BloodTap,
        RunicEmpowerment,
        RunicCorruption,
        GorefiendsGrasp,
        RemorselessWinter,
        DesecratedGround
#else

        Plaguebearer = 1,
        PlagueLeech,
        UnholyBlight,

        Lichborne,
        AntiMagicZone,
        Purgatory,

        DeathsAdvance,
        Chilblains,
        Asphyxiate,

        BloodTap,
        RunicEmpowerment,
        RunicCorruption,

        DeathPact,
        DeathSiphon,
        Conversion,

        GorefiendsGrasp,
        RemorselessWinter,
        DesecratedGround,

        NecroticPlague,
        Defile,
        BreathOfSindragosa

#endif
    }

    #region Locals - SimC Synonyms

    public static class target
    {
        public static double health_pct { get { return StyxWoW.Me.CurrentTarget.HealthPercent; } }
        public static long time_to_die { get { return StyxWoW.Me.CurrentTarget.TimeToDeath(); } }
    }

    class buff
    {
        public static uint blood_charge_stack { get { return StyxWoW.Me.GetAuraStacks("Blood Charge"); } }
        public static uint shadow_infusion_stack { get { return StyxWoW.Me.GetAuraStacks("Shadow Infusion"); } }
        public static bool dark_transformation_up { get { return !StyxWoW.Me.GotAlivePet ? false : StyxWoW.Me.Pet.ActiveAuras.ContainsKey("Dark Transformation"); } }
        public static bool dark_transformation_down { get { return !dark_transformation_up; } }
        public static bool antimagic_shell_up { get { return antimagic_shell_remains > 0; } }
        public static double antimagic_shell_remains { get { return StyxWoW.Me.GetAuraTimeLeft("Anti-Magic Shell").TotalSeconds; } }
        public static bool sudden_doom_react { get { return StyxWoW.Me.HasAura(Common.SuddenDoom); } }
        public static bool killing_machine_react { get { return StyxWoW.Me.HasAura(Common.KillingMachine); } }
        public static bool rime_react { get { return StyxWoW.Me.HasAura(Common.FreezingFog); } }

    }


    public static class cooldown
    {
        public static double empower_rune_weapon_remains { get { return Spell.GetSpellCooldown("Empower Rune Weapon").TotalSeconds; } }
        public static double breath_of_sindragosa_remains { get { return Spell.GetSpellCooldown("Breath of Sindragosa").TotalSeconds; } }
        public static double defile_remains { get { return Spell.GetSpellCooldown("Defile").TotalSeconds; } }
        public static double outbreak_remains { get { return Spell.GetSpellCooldown("Outbreak").TotalSeconds; } }
        public static double soul_reaper_remains { get { return Spell.GetSpellCooldown("Soul Reaper").TotalSeconds; } }
        public static double antimagic_shell_remains { get { return Spell.GetSpellCooldown("Anti-Magic Shell").TotalSeconds; } }
        public static double pillar_of_frost_remains { get { return Spell.GetSpellCooldown("Pillar of Frost").TotalSeconds; } }
        public static double unholy_blight_remains { get { return Spell.GetSpellCooldown("Unholy Blight").TotalSeconds; } }
    }

    public static class obliterate
    {
        public static double ready_in
        {
            get
            {
                return 0;
            }
        }
    }
    public static class talent
    {
        public static bool necrotic_plague_enabled { get; set; }
        public static bool breath_of_sindragosa_enabled { get; set; }
        public static bool defile_enabled { get; set; }
        public static bool unholy_blight_enabled { get; set; }

        public static bool runic_empowerment_enabled { get; set; }

        public static bool blood_tap_enabled { get; set; }
    }

    public static class disease
    {
        static string[] listbase = { "Blood Plague", "Frost Fever" };
        static string[] listwithnp = { "Necrotic Plague" };
        static string[] diseaselist { get { return talent.necrotic_plague_enabled ? listwithnp : listbase; } }

        public static bool ticking
        {
            get
            {
                return ticking_on(StyxWoW.Me.CurrentTarget);
            }
        }

        public static double min_remains
        {
            get 
            {
                return min_remains_on(StyxWoW.Me.CurrentTarget);
            }
        }

        public static bool ticking_on(WoWUnit unit)
        {
            return unit.HasAllMyAuras(diseaselist);
        }

        public static double min_remains_on(WoWUnit unit)
        {
            double min = double.MaxValue;
            foreach (var s in diseaselist)
            {
                double rmn = unit.GetAuraTimeLeft(s).TotalSeconds;
                if (rmn < min)
                    min = rmn;
            }

            if (min == double.MaxValue)
                min = 0;

            return min;
        }

        public static double max_remains
        {
            get
            {
                return max_remains_on(StyxWoW.Me.CurrentTarget);
            }
        }
        public static double max_remains_on(WoWUnit unit)
        {
            double max = double.MinValue;
            foreach (var s in diseaselist)
            {
                double rmn = unit.GetAuraTimeLeft(s).TotalSeconds;
                if (rmn > max)
                    max = rmn;
            }

            if (max == double.MinValue)
                max = 0;

            return max;
        }

        public static bool min_ticking { get { return ticking; } }

        public static bool max_ticking
        {
            get
            {
                return max_ticking_on(StyxWoW.Me.CurrentTarget);
            }
        }

        private static bool max_ticking_on(WoWUnit unit)
        {
            return unit.HasAnyOfMyAuras(diseaselist);
        }
    }

    public static class dot
    {
        public static bool necrotic_plague_ticking
        {
            get
            {
                return necrotic_plague_remains > 0;
            }
        }
        public static double necrotic_plague_remains
        {
            get
            {
                return StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Necrotic Plague").TotalSeconds;
            }
        }

        public static bool necrotic_plague_ticking_on(WoWUnit unit)
        {
            return necrotic_plague_remains_on(unit) > 0;
        }
        public static double necrotic_plague_remains_on(WoWUnit unit)
        {
            return unit.GetAuraTimeLeft("Necrotic Plague").TotalSeconds;
        }

        public static bool frost_fever_ticking
        {
            get
            {
                return frost_fever_remains > 0;
            }
        }
        public static double frost_fever_remains
        {
            get
            {
                return StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Frost Fever").TotalSeconds;
            }
        }
        public static bool blood_plague_ticking
        {
            get
            {
                return blood_plague_remains > 0;
            }
        }
        public static double blood_plague_remains
        {
            get
            {
                return StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Blood Plague").TotalSeconds;
            }
        }

        public static bool breath_of_sindragosa_ticking
        {
            get
            {
                return breath_of_sindragosa_remains > 0;
            }
        }
        public static double breath_of_sindragosa_remains
        {
            get
            {
                return StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Breath of Sindragosa").TotalSeconds;
            }
        }
    }

    #endregion

}