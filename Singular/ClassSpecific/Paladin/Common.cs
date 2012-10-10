using System.Collections.Generic;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using System;
using Styx.WoWInternals;


namespace Singular.ClassSpecific.Paladin
{

    enum PaladinBlessings
    {
        Auto, Kings, Might
    }

    public class Common
    {
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Paladin)]
        public static Composite CreatePaladinPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreatePaladinBlessBehavior(),
                    new Decorator(
                        ret => TalentManager.CurrentSpec == WoWSpec.PaladinHoly,
                        new PrioritySelector(
                            
                            Spell.BuffSelf("Seal of Insight"),
                            Spell.BuffSelf("Seal of Righteousness", ret => !SpellManager.HasSpell("Seal of Insight"))
                            )),
                    new Decorator(
                        ret => TalentManager.CurrentSpec != WoWSpec.PaladinHoly,
                        new PrioritySelector(
                            Spell.BuffSelf("Righteous Fury", ret => TalentManager.CurrentSpec == WoWSpec.PaladinProtection && StyxWoW.Me.GroupInfo.IsInParty)
                            /*
                            Spell.BuffSelf("Seal of Truth"),
                            Spell.BuffSelf("Seal of Righteousness", ret => !SpellManager.HasSpell("Seal of Truth"))
                             */
                            ))

                    );
        }


        /// <summary>
        /// cast Blessing of Kings or Blessing of Might based upon configuration setting.
        /// 
        /// </summary>
        /// <returns></returns>
        private static Composite CreatePaladinBlessBehavior()
        {
            return
                new PrioritySelector(

                        PartyBuff.BuffGroup( 
                            "Blessing of Kings", 
                            ret => SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Auto || SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Kings,
                            "Blessing of Might"),

                        PartyBuff.BuffGroup(
                            "Blessing of Might",
                            ret => SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Auto || SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Might, 
                            "Blessing of Kings")
                    );
        }

    }
}
