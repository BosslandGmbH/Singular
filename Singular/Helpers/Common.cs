using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Logic.Combat;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.Helpers
{
    internal static class Common
    {
        /// <summary>
        ///  Creates a behavior to start auto attacking to current target.
        /// </summary>
        /// <remarks>
        ///  Created 23/05/2011
        /// </remarks>
        /// <param name="includePet"> This will also toggle pet auto attack. </param>
        /// <returns></returns>
        public static Composite CreateAutoAttack(bool includePet)
        {
            const int spellIdAutoShot = 75;

            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.IsAutoAttacking && StyxWoW.Me.AutoRepeatingSpellId != spellIdAutoShot,
                    new Action(ret => StyxWoW.Me.ToggleAttack())),
                new Decorator(
                    ret => includePet && StyxWoW.Me.GotAlivePet && (StyxWoW.Me.Pet.CurrentTarget == null || StyxWoW.Me.Pet.CurrentTarget != StyxWoW.Me.CurrentTarget),
                    new Action(
                        delegate
                        {
                            PetManager.CastPetAction("Attack");
                            return RunStatus.Failure;
                        }))
                );
        }

        /// <summary>
        ///  Creates a behavior to start shooting current target with the wand.
        /// </summary>
        /// <remarks>
        ///  Created 23/05/2011
        /// </remarks>
        /// <returns></returns>
        public static Composite CreateUseWand()
        {
            return CreateUseWand(ret => true);
        }

        /// <summary>
        ///  Creates a behavior to start shooting current target with the wand if extra conditions are met.
        /// </summary>
        /// <param name="extra"> Extra conditions to check to start shooting. </param>
        /// <returns></returns>
        public static Composite CreateUseWand(SimpleBooleanDelegate extra)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Item.HasWand && !StyxWoW.Me.IsWanding() && extra(ret),
                    new Action(ret => SpellManager.Cast("Shoot")))
                );
        }
    }
}
