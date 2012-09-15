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
    public class Destruction
    {
        #region Common

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warlock, WoWSpec.WarlockDestruction, priority:1)]
        public static Composite CreateWarlockDestructionPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(false),
                Pet.CreateSummonPet("Imp"),
                Spell.Buff("Dark Intent",
                    ret => StyxWoW.Me.PartyMembers.OrderByDescending(p => p.MaxHealth).FirstOrDefault(),
                    ret => !StyxWoW.Me.HasAura("Dark Intent"))
                );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.All)]
        public static Composite CreateWarlockDestructionNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Spell.Cast("Soul Fire"),
                Spell.Buff("Immolate", true),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.All)]
        public static Composite CreateWarlockDestructionNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.Cast("Conflagrate"),
                Spell.Cast("Chaos Bolt",ret => BackdraftStacks < 3),
                Spell.Buff("Immolate",true),
                Spell.Cast("Incinerate"),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion
        static double BackdraftStacks
        {
            get
            {
                var c = MyAura(StyxWoW.Me, "Backdraft");
                return c == null ? 0 : c.StackCount;
            }
        }



        private static WoWAura MyAura(WoWUnit Who, String What)
        {
            return Who.GetAllAuras().FirstOrDefault(p => p.CreatorGuid == StyxWoW.Me.Guid && p.Name == What);
        }
    }
}
