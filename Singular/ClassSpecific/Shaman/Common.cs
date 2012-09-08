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

using System.Collections.Generic;
using System.Linq;

using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using CommonBehaviors.Actions;

namespace Singular.ClassSpecific.Shaman
{
    public static class Common
    {
        public static Composite CreateShamanRacialsCombat()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Blood Fury",
                    ret => SingularSettings.Instance.UseRacials &&
                        StyxWoW.Me.Race == WoWRace.Orc &&
                        !StyxWoW.Me.HasAnyAura("Elemental Mastery", "Bloodlust", "Heroism", "Time Warp", "Ancient Hysteria")),
                Spell.BuffSelf("Berserking",
                    ret => SingularSettings.Instance.UseRacials &&
                        StyxWoW.Me.Race == WoWRace.Troll &&
                        !StyxWoW.Me.HasAnyAura("Elemental Mastery", "Bloodlust", "Heroism", "Time Warp", "Ancient Hysteria")),
                Spell.BuffSelf("Lifeblood",
                    ret => SingularSettings.Instance.UseRacials &&
                        !StyxWoW.Me.HasAnyAura("Lifeblood", "Blood Fury", "Berserking", "Bloodlust", "Heroism", "Time Warp", "Ancient Hysteria")));
        }
    }
}