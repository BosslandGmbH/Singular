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

using System;
using System.Linq;

using Singular.ClassSpecific.Shaman;
using Singular.Settings;

using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Spec(TalentSpec.Lowbie)]
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
                    ret => TotemManager.TotemsInRange == 0 && SpellManager.HasSpell("Call of the Elements"),
                    new Sequence(
                        new Action(ret => TotemManager.SetupTotemBar()),
                        new Action(ret => TotemManager.CallTotems()))),
            CreateSpellCast("Lightning Bolt")
            );
        }

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
				CreateSpellCast("Wind Shear", ret => Me.CurrentTarget.IsCasting),
				
                //For low levels we are avoiding this.
                new Decorator(
                    ret => TotemManager.TotemsInRange == 0 && SpellManager.HasSpell("Call of the Elements"),
                    new Sequence(
                        new Action(ret => TotemManager.SetupTotemBar()),
                        new Action(ret => TotemManager.CallTotems()))),

                // add check for reinforcements ( elem totems )

                // clip the debuff if less than 2 secs remaining (don't chance a LvB with no FS dot)
                //  .. note:  flame shock doesn't stack, so be sure to pass 0 for stack test
                CreateSpellBuff("Flame Shock", ret => !HasMyAura("Flame Shock", Me.CurrentTarget, TimeSpan.FromSeconds(2), 0)),         

                // unleash elements buff is lower dps than additional procs by lightning bolt
                // .. it is best used in movement fights as an additional instant 
                // CreateSpellCast("Unleash Elements"),    // if we have this, make sure its before Flame Shock

                CreateSpellCast("Elemental Mastery"),  
                CreateSpellCast("Lava Burst"),     

                // atleast (6 lightning shield stacks or no Fulmination talent) and FS has > 5 secs left before casting
                //  .. note:  flame shock doesn't stack, so be sure to pass 0 for stack test
                CreateSpellCast("Earth Shock", ret => 
                    (HasAuraStacks("Lightning Shield", 7) || !HaveTalentFulmination) && HasMyAura("Flame Shock", Me.CurrentTarget, TimeSpan.FromSeconds(5), 0)),

                CreateSpellCast("Chain Lightning", ret => WillChainLightningHop()),

                CreateSpellCast("Lightning Bolt")
                );
        }
		
        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.ElementalShaman)]
        [Spec(TalentSpec.Lowbie)]
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
        [Spec(TalentSpec.Lowbie)]
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