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
using Rest = Singular.Helpers.Rest;
using System.Drawing;
using System.Collections.Generic;

namespace Singular.ClassSpecific.DemonHunter
{
    public static class Common
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static DemonHunterSettings Settings => SingularSettings.Instance.DemonHunter();
	    public static bool HasTalent(DemonHunterTalents talent) => TalentManager.IsSelected((int)talent);


        #region Rest

        [Behavior(BehaviorType.Rest, WoWClass.DemonHunter)]
        public static Composite CreateDemonHunterRest()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Settings.OOCUseSoulFragments && !Rest.IsEatingOrDrinking && Me.HealthPercent <= Settings.OOCSoulFragmentHealthPercent,
                        CreateCollectFragmentsBehavior(Settings.OOCSoulFragmentRange)
                    ),

                    Rest.CreateDefaultRestBehaviour()
                );
        }
        #endregion


        #region Pull
        [Behavior(BehaviorType.Pull, WoWClass.DemonHunter)]
        public static Composite CreateDemonHunterPull()
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

                            CreateThrowGlaiveBehavior(),
                            Spell.Cast("Fel Rush", ret => Settings.EngageWithFelRush && Me.CurrentTarget.Distance <= 15),
                            Spell.HandleOffGCD(Spell.CastOnGround("Infernal Strike", on => (WoWUnit)on, ret => Settings.PullInfernalStrike && Me.CurrentTarget.Distance < 30))
                            )
                        )
                );
        }
        #endregion

        #region Throw Glaive
        public static Composite CreateThrowGlaiveBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown(),
                Spell.Cast("Throw Glaive", req => Me.CurrentTarget.DistanceSqr > 10 * 10)
            );
        }
		#endregion

        public static Composite CreateDemonHunterPullMore()
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
                        Spell.Cast("Throw Glaive", on => (on as WoWUnit)),
                        Spell.Cast("Torment", req => Me.Specialization == WoWSpec.DemonHunterVengeance)
                        )
                    )
                );
        }

        #region Fragment Composites

        private static IEnumerable<WoWAreaTrigger> FindFragments(float range)
        {
            range *= range;
            return ObjectManager.GetObjectsOfType<WoWAreaTrigger>()
                .Where(
                    o =>
                        !Blacklist.Contains(o, BlacklistFlags.Loot) && o.Caster == Me && o.DistanceSqr <= range &&
                        (o.Entry == 11266 || o.Entry == 10665 || o.Entry == 12929 || o.Entry == 8352)).OrderBy(o => o.DistanceSqr);
        }

        public static Composite CreateCollectFragmentsBehavior(float range)
        {
            return new Decorator(
                ret => !MovementManager.IsMovementDisabled && !Me.IsRooted(),
                new PrioritySelector(
                    // Set the closest fragment as the context.
                    ctx => FindFragments(range).FirstOrDefault(),
                    new Decorator(
                        // Check if we could find a fragment. Context will be null when there were no fragments.
                        ret => ret != null,
                        new PrioritySelector(
                            // Move to the fragment. This returns Failure when we are already in given range (3f)
                            Movement.CreateMoveToLocationBehavior(to => ((WoWAreaTrigger) to).Location, false, distance => 3f),
                            new Decorator(
                                // Now check if fragment was not vacuumed to us automatically. We are already in 3 yards
                                // range of the fragment and fragment is still valid at this point
                                ret => ((WoWAreaTrigger) ret).IsValid,
                                new Action(
                                    ret =>
                                        Blacklist.Add(((WoWAreaTrigger) ret).Guid, BlacklistFlags.Loot,
                                            TimeSpan.FromMinutes(1), "Reached to fragment but it was not vacuumed")))
                            ))));
        }
        #endregion

    }

    public enum DemonHunterTalents
    {
        FelMastery = 1,
        ChaosCleave,
        BlindFury,

        AbyssalStrike = FelMastery,
        AgonizingFlames = ChaosCleave,
        RazorSpikes = BlindFury,


        Prepared = 4,
        DemonBlades,
        DemonicAppetite,

        FeastOfSouls = Prepared,
        Fallout  = AgonizingFlames,
        BurningAlive = DemonicAppetite,


        Felblade = 7,
        FirstBlood,
        Bloodlet,

        FlameCrash = FirstBlood,
        Gluttony = Bloodlet,


        Netherwalk = 10,
        DesperateInstincts,
        SoulRending,

        FeedTheDemon = Netherwalk,
        Fracture = DesperateInstincts,


        Momentum = 13,
        FelEruption,
        Nemesis,

        ConcentratedSigils = Momentum,
        QuickenedSigils = Nemesis,


        MasterOfTheGlaive = 16,
        UnleashedPower,
        DemonReborn,

        FelDevastation = MasterOfTheGlaive,
        BladeTurning = UnleashedPower,
        SpiritBomb = DemonReborn,


        ChaosBlades = 19,
        FelBarrage,
        Demonic,

        LastResort = ChaosBlades,
        NetherBond = FelBarrage,
        SoulBarrier = Demonic
    }
}