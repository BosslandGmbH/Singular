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

using Singular.Settings;

using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public Composite CreateProtectionPaladinCombat()
        {
            NeedTankTargeting = true;
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateFaceUnit(),
                CreateSpellCast("Hammer of Wrath", false),
                CreateSpellCast("Avenger's Shield", false),
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                CreateMoveToAndFace(5f, ret => Me.CurrentTarget),
                CreateAutoAttack(true),
                // Same rotation for both.
                CreateSpellCast("Shield of the Righteous", ret => Me.CurrentHolyPower == 3),
                //Multi target
                new Decorator(
                    ret => NearbyUnfriendlyUnits.Count(a => a.Distance < 8) > 1,
                    new PrioritySelector(
                        CreateSpellCast("Hammer of the Righteous"),
                        CreateSpellCast("Crusader Strike"),
                        CreateSpellCast("Consecration"),
                        CreateSpellCast("Holy Wrath"),
                        CreateSpellCast("Judgement"))),
                new Decorator(
                    ret => NearbyUnfriendlyUnits.Count(a => a.Distance < 8) <= 1,
                    new PrioritySelector(
                        //Single target
                        CreateSpellCast("Crusader Strike"),
                        CreateSpellCast("Judgement"),
                        CreateSpellCast("Consecration"),
                        CreateSpellCast("Holy Wrath"))));
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateProtectionPaladinPull()
        {
            return
                new PrioritySelector(
                    CreateEnsureTarget(),
                    CreateAutoAttack(true),
                    CreateFaceUnit(ret => Me.CurrentTarget),
                    CreateSpellCast("Avenger's Shield", false),
                    CreateSpellCast("Judgement", false),
                    CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateProtectionPaladinCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellCast(
                        "Hand of Reckoning", ret => TankTargeting.Instance.NeedToTaunt.Count != 0,
                        ret => TankTargeting.Instance.NeedToTaunt.FirstOrDefault()),
                    CreateSpellBuffOnSelf("Avenging Wrath"),
                    CreateSpellBuffOnSelf(
                        "Lay on Hands",
                        ret => Me.HealthPercent <= SingularSettings.Instance.Paladin.LayOnHandsHealthProt && !Me.HasAura("Forbearance")),
                    CreateSpellBuffOnSelf(
                        "Guardian of Ancient Kings",
                        ret => Me.HealthPercent <= SingularSettings.Instance.Paladin.GoAKHealth),
                    CreateSpellBuffOnSelf(
                        "Ardent Defender",
                        ret => Me.HealthPercent <= SingularSettings.Instance.Paladin.ArdentDefenderHealth),
                    CreateSpellBuffOnSelf(
                        "Divine Protection",
                        ret => Me.HealthPercent <= SingularSettings.Instance.Paladin.DivineProtectionHealthProt)
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.PullBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateProtectionPaladinPullBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellBuffOnSelf("Divine Plea")
                    );
        }
    }
}