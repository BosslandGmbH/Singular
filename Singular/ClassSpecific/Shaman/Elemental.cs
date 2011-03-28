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

using Singular.ClassSpecific.Shaman;
using Singular.Settings;

using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        public Composite CreateElementalShamanCombat()
        {
            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateMoveToAndFace(39, ret => Me.CurrentTarget),
                CreateWaitForCast(true),
				
				//Healing Basic
				CreateSpellCast("Healing Surge", ret => (Me.HealthPercent <= SingularSettings.Instance.Shaman.Elemental_HealingSurge_Health)),
				//Interupt spell casters
				CreateSpellCast("Wind Shear", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != null),
				
                new Decorator(
                    ret => TotemManager.TotemsInRange == 0,
                    new Sequence(
                        new Action(ret => TotemManager.SetupTotemBar()),
                        new Action(ret => TotemManager.CallTotems()))),

                CreateSpellCast("Elemental Mastery"),
                CreateSpellBuff("Flame Shock"),
                CreateSpellCast("Unleash Elements"),
                CreateSpellCast("Lava Burst"),
                CreateSpellCast("Earth Shock", ret => HasAuraStacks("Lightning Shield", 6)),
				CreateSpellBuffOnSelf("Lightning Shield"),
                CreateSpellCast("Lightning Bolt")
                );
        }
		
		[Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateElementalShamanPull()
		{
            return new PrioritySelector(
			CreateEnsureTarget(),
            CreateMoveToAndFace(40, ret => Me.CurrentTarget),
            CreateWaitForCast(true),
			//Totems
                new Decorator(
                    ret => TotemManager.TotemsInRange == 0,
                    new Sequence(
                        new Action(ret => TotemManager.SetupTotemBar()),
                        new Action(ret => TotemManager.CallTotems()))),
			CreateSpellCast("Lightning Bolt")
            );
        }
		
        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        public Composite CreateElementalShamanBuffs()
        {
            return new PrioritySelector(
                CreateSpellBuffOnSelf("Lightning Shield"),
                new Decorator(
                    ret => !Me.HasAura("Flametongue Weapon (Passive)") && SpellManager.CanCast("Flametongue Weapon"),
                    new Action(ret => CastWithLog("Flametongue Weapon", Me)))
                );
        }


        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Spec(TalentSpec.RestorationShaman)]
        [Spec(TalentSpec.EnhancementShaman)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Rest)]
        public Composite CreateShamanRest()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => TotemManager.NeedToRecallTotems,
                    new Action(ret => TotemManager.RecallTotems())),

                CreateRestoShamanHealOnlyBehavior(true),
                CreateDefaultRestComposite(SingularSettings.Instance.DefaultRestHealth, SingularSettings.Instance.DefaultRestMana)

                );
        }
    }
}