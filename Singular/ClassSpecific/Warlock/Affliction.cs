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


namespace Singular.ClassSpecific.Warlock
{
    public class Affliction
    {
        #region Common

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warlock, WoWSpec.WarlockAffliction, priority:1)]
        public static Composite CreateWarlockAfflictionPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(false),
                Pet.CreateSummonPet("Felhunter"),
                new Decorator(
                    ret => !SpellManager.HasSpell("Summon Felhunter"),
                    Pet.CreateSummonPet("Voidwalker")),
                Spell.Buff("Dark Intent", 
                    ret => StyxWoW.Me.PartyMembers.OrderByDescending(p => p.MaxHealth).FirstOrDefault(), 
                    ret => !StyxWoW.Me.HasAura("Dark Intent"))
                );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.All)]
        public static Composite CreateWarlockAfflictionNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Spell.BuffSelf("Soulburn"),
                Spell.Cast("Soul Swap"),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        static string[] _Doublecast = { "Agony", "Corruption", "Unstable Afflictio" };
        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.All)]
        public static Composite CreateWarlockAfflictionNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Spell.PreventDoubleCast(_Doublecast),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),



                //Double cast protection
                //new Decorator(ret => StyxWoW.Me.CurrentTarget.HasMyAura("Unstable Affliction") && StyxWoW.Me.CastingSpell != null && StyxWoW.Me.CastingSpell.Name == "Unstable Affliction",
                 //   new Styx.TreeSharp.Action(r => SpellManager.StopCasting())),

                new Decorator(ret=> StyxWoW.Me.CurrentTarget.HealthPercent > 20.0f, 
                    new PrioritySelector(
                Spell.Cast("Agony", ret => AgonyTime < 3),
                Spell.Cast("Corruption", ret => CorruptionTime < 3),
                Spell.Cast("Unstable Affliction", ret => UnstableAfflictionTime < 3),
                Spell.Cast("Haunt", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Haunt")),
                Spell.Cast("Malefic Grasp")
                )) ,
                 new Decorator(ret=> StyxWoW.Me.CurrentTarget.HealthPercent <= 20.0f, 
                     new PrioritySelector(
                         Spell.BuffSelf("Soulburn",ret => (AgonyTime < 3 || CorruptionTime < 3 || UnstableAfflictionTime < 3) && !StyxWoW.Me.HasAura("Soulburn")),
                         Spell.Cast("Soul Swap", ret => StyxWoW.Me.HasAura("Soulburn")),
                         Spell.Buff("Haunt"),
                Spell.Cast("Drain Soul"))),


                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        static double AgonyTime
        {
            get
            {
                var c = MyAura(StyxWoW.Me.CurrentTarget, "Agony");
                return c == null ? 0 : c.TimeLeft.TotalSeconds;
            }
        }
        static double CorruptionTime
        {
            get
            {
                var c = MyAura(StyxWoW.Me.CurrentTarget, "Corruption");
                return c == null ? 0 : c.TimeLeft.TotalSeconds;
            }
        }
        static double UnstableAfflictionTime
        {
            get
            {
                var c = MyAura(StyxWoW.Me.CurrentTarget, "Unstable Affliction");
                return c == null ? 0 : c.TimeLeft.TotalSeconds;
            }
        }


        
        private static WoWAura MyAura(WoWUnit Who, String What)
        {
            return Who.GetAllAuras().FirstOrDefault(p => p.CreatorGuid == StyxWoW.Me.Guid && p.Name == What);
        }



    }
}