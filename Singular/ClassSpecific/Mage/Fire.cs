#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author$
// $Date$
// $HeadURL$
// $LastChangedBy$
// $LastChangedDate$
// $LastChangedRevision$
// $Revision$

#endregion

using System.Linq;
using Styx.Combat.CombatRoutine;

using TreeSharp;
using Styx;
using CommonBehaviors.Actions;
using Styx.Logic.Pathing;
using Styx.Helpers;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateFireMageCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                //Move away from frozen targets
                new Decorator(
                    ret => Me.CurrentTarget.HasAura("Frost Nova") && Me.CurrentTarget.DistanceSqr < 5 * 5,
                    new Action(
                        ret =>
                        {
                            Logger.Write("Getting away from frozen target");
                            WoWPoint moveTo = WoWMathHelper.CalculatePointFrom(Me.Location, Me.CurrentTarget.Location, 10f);

                            if (Navigator.CanNavigateFully(Me.Location, moveTo))
                            {
                                Navigator.MoveTo(moveTo);
                            }
                        })),
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateMoveToAndFace(39f, ret => Me.CurrentTarget),
                CreateSpellBuffOnSelf("Ice Block", ret => Me.HealthPercent < 10 && !Me.ActiveAuras.ContainsKey("Hypothermia")),
                new Decorator(ret => Me.ActiveAuras.ContainsKey("Ice Block"),
                   new ActionIdle()),
                CreateSpellBuff("Frost Nova", ret => NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8)),
                CreateSpellCast("Evocation", ret => Me.ManaPercent < 20),
                new Decorator(ret => HaveManaGem() && Me.ManaPercent <= 30,
                   new Action(ctx => UseManaGem())),
                CreateSpellBuffOnSelf("Mana Shield", ret => !Me.Auras.ContainsKey("Mana Shield") && Me.HealthPercent <= 75),
                CreateMagePolymorphOnAddBehavior(),
                CreateSpellCast("Counterspell", ret => Me.CurrentTarget.IsCasting),
                CreateSpellCast("Mirror Image", ret => Me.CurrentTarget.HealthPercent > 20),
                CreateSpellCast("Time Warp", ret => Me.CurrentTarget.HealthPercent > 20),
                new Decorator(
                    ret => Me.CurrentTarget.HealthPercent > 50,
                    new Sequence(
                        new Action(ctx => Me.CurrentTarget.Face()),
                        new Action(ctx => StyxWoW.SleepForLagDuration()),
                        new PrioritySelector(CreateSpellCast("Flame Orb"))
                        )),
                CreateSpellCast("Scorch", ret => (!Me.CurrentTarget.HasAura("Critical Mass") || Me.CurrentTarget.Auras["Critical Mass"].TimeLeft.TotalSeconds < 3) && TalentManager.GetCount(2, 20) != 0 && LastSpellCast != "Scorch"),
                CreateSpellCast("Pyroblast", ret => Me.ActiveAuras.ContainsKey("Hot Streak") && Me.ActiveAuras["Hot Streak"].TimeLeft.TotalSeconds > 1),
                CreateSpellCast("Fire Blast", ret => Me.ActiveAuras.ContainsKey("Impact")),
                CreateSpellBuff("Living Bomb", ret => !Me.CurrentTarget.HasAura("Living Bomb")),
                CreateSpellCast("Combustion", ret=> Me.CurrentTarget.ActiveAuras.ContainsKey("Living Bomb") && Me.CurrentTarget.ActiveAuras.ContainsKey("Ignite") && Me.CurrentTarget.ActiveAuras.ContainsKey("Pyroblast!")),
                CreateSpellCast("Fireball"),
                CreateFireRangedWeapon()
                );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateFireMagePull()
        {
            return
                new PrioritySelector(
                    // Make sure we're in range, and facing the damned target. (LOS check as well)
                    CreateMoveToAndFace(39f, ret => Me.CurrentTarget),
                    CreateSpellCast("Fireball")
                    );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateFireMagePreCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellBuffOnSelf(
                        "Arcane Brilliance",
                        ret => (!Me.HasAura("Arcane Brilliance") &&
                                !Me.HasAura("Fel Intelligence"))),
                    CreateSpellBuffOnSelf(
                        "Molten Armor",
                        ret => (!Me.HasAura("Molten Armor"))
                        )
                    );
        }
    }
}