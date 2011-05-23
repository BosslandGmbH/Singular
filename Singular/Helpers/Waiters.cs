
using System.Linq;
using Styx;
using Styx.Logic.Combat;
using TreeSharp;
using CommonBehaviors.Actions;
namespace Singular.Helpers
{
    internal static class Waiters
    {
        /// <summary>
        ///   Creates a composite that will return a success, so long as you are currently casting. (Use this to prevent the CC from
        ///   going down to lower branches in the tree, while casting.)
        /// </summary>
        /// <remarks>
        ///   Created 13/5/2011.
        /// </remarks>
        /// <returns>.</returns>
        public static Composite WaitForCast()
        {
            return WaitForCast(false);
        }

        /// <summary>
        ///   Creates a composite that will return a success, so long as you are currently casting. (Use this to prevent the CC from
        ///   going down to lower branches in the tree, while casting.)
        /// </summary>
        /// <remarks>
        ///   Created 13/5/2011.
        /// </remarks>
        /// <param name = "faceDuring">Whether or not to face during casting</param>
        /// <returns></returns>
        public static Composite WaitForCast(bool faceDuring)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => StyxWoW.Me.IsCasting && !StyxWoW.Me.IsWanding(),
                    new PrioritySelector(
                        // This is here to avoid double casting spells with dots/debuffs (like Immolate)
                        // Note: This needs testing.
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget != null && 
                                   StyxWoW.Me.CurrentTarget.Auras.Any(a=> 
                                       a.Value.SpellId == StyxWoW.Me.CastingSpellId &&
                                       a.Value.CreatorGuid == StyxWoW.Me.Guid),
                            new Action(ret => SpellManager.StopCasting())),
                        new Decorator(
                            ret => faceDuring,
                            Movement.CreateFaceTargetBehavior()),
                        new ActionAlwaysSucceed()
                        )));
        }
    }
}
