using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        protected Composite CreateHunterBackPedal()
        {
            return
                new Decorator(
                    ret => Me.CurrentTarget.Distance <= 7 && Me.CurrentTarget.IsAlive &&
                           (Me.CurrentTarget.CurrentTarget == null || Me.CurrentTarget.CurrentTarget != Me),
                    new Action(ret =>
                    {
                        WoWPoint moveTo = WoWMathHelper.CalculatePointFrom(Me.Location, Me.CurrentTarget.Location, 10f);

                        if (Navigator.CanNavigateFully(Me.Location, moveTo))
                            Navigator.MoveTo(moveTo);
                    }));
        }

        public Composite CreateHunterBuffs()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Me.CastingSpellId != 0 && Me.CastingSpell.Name == "Revive " + WantedPet && Me.GotAlivePet,
                    new Action(ret => SpellManager.StopCasting())),

                CreateWaitForCast(),

                new Decorator(
                    ret => !Me.GotAlivePet,
                    new Action(ret => PetManager.CallPet(WantedPet)))
                );
        }
    }
}
