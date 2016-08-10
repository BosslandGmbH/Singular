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

namespace Singular.ClassSpecific.DemonHunter
{
    public static class Common
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static DemonHunterSettings Settings => SingularSettings.Instance.DemonHunter();
	    public static bool HasTalent(DemonHunterTalents talent) => TalentManager.IsSelected((int)talent);


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
                            Spell.Cast("Fel Rush", ret => Me.CurrentTarget.Distance <= 15 && Settings.EngageWithFelRush)
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