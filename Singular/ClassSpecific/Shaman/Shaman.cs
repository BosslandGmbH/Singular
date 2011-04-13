#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author: $
// $Date: $
// $HeadURL: $
// $LastChangedBy: $
// $LastChangedDate: $
// $LastChangedRevision:  $
// $Revision:  $

#endregion

using System.Collections.Generic;
using System.Linq;

using Singular;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using System;

namespace Singular
{
    partial class SingularRoutine
    {
        // note:  Singular TalentManager does -1 on WOW Tab and Index values
        public static bool HaveTalentFulmination
        {
            get{ return TalentManager.Talents.Where(t => t.Tab == 0 && t.Index == 12).Any(); }
        }

        // note:  Singular TalentManager does -1 on WOW Tab and Index values
        public static bool HaveTalentFocusedInsight
        {
            get { return TalentManager.Talents.Where(t => t.Tab == 2 && t.Index == 5).Any(); }
        }

        /// <summary>
        /// Tests whether currently in an RAF operating mode
        /// </summary>
        /// <param name="healTarget"></param>
        /// <returns></returns>
        public static bool IsRAF
        {
            get
            {
                return (StyxWoW.Me.IsInParty || StyxWoW.Me.IsInRaid) && RaFHelper.Leader != null && RaFHelper.Leader != StyxWoW.Me;
            }
        }

        /// <summary>
        /// WillChainHealHop()
        /// Tests whether casting Chain Lightning on 'healTarget' results in a minimum 
        /// of 2 hops (3 people healed.) 
        /// </summary>
        /// <param name="healTarget"></param>
        /// <returns></returns>
        public static bool WillChainHealHop(WoWUnit healTarget)
        {
            double threshhold = SingularSettings.Instance.Shaman.RAF_ChainHeal_Health;

            if (healTarget == null)
                return false;

            var t = (from o in ObjectManager.ObjectList
                     where o is WoWPlayer && healTarget.Location.Distance(o.Location) < 12
                     let p = o.ToPlayer()
                     where p != null
                           && p.IsHorde == StyxWoW.Me.IsHorde
                           && !p.IsPet
                           && p != healTarget
                           && p.IsAlive
                           && p.HealthPercent < threshhold
                     let c = (from oo in ObjectManager.ObjectList
                              where oo is WoWPlayer && p.Location.Distance(oo.Location) < 12
                              let pp = oo.ToPlayer()
                              where pp != null
                                    && pp.IsHorde == p.IsHorde
                                    && !pp.IsPet
                                    && pp.IsAlive
                                    && pp.HealthPercent < threshhold
                              select pp).Count()
                     orderby c descending, p.Distance ascending
                     select new { Player = p, Count = c }).FirstOrDefault();

            if (t == null || t.Count < 3)
                return false;
            return true;
        }

        /// <summary>
        /// WillChainLightningHop()
        /// Tests whether casting Chain Lightning on 'Current Target' results in a minimum 
        /// of 2 hops (3 people healed.) 
        /// </summary>
        /// <param name="healTarget"></param>
        /// <returns></returns>
        public static bool WillChainLightningHop()
        {
            if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.IsHorde != ((WoWPlayer)StyxWoW.Me.CurrentTarget).IsHorde )
            {
                return  (from o in ObjectManager.ObjectList
                         where o is WoWUnit
                            && o != StyxWoW.Me.CurrentTarget
                            && StyxWoW.Me.CurrentTarget.Location.Distance(o.Location) < 12
                         let u = o.ToUnit()
                         where u.Attackable 
                            && !u.IsPet
                            && u.CurrentHealth > 1
                            && ((!u.IsPlayer && u.IsHostile) || (u.IsPlayer && u.ToPlayer().IsHorde != StyxWoW.Me.IsHorde))
                         select u).Any();
            }

            return false;
        }

        public Composite CreateBestShockCast()
        {
            return new Decorator( ret => Me.GotTarget && Me.CurrentTarget.IsHostile,
                new PrioritySelector(
                    CreateSpellCast("Frost Shock", ret => StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAura("Frost Shock")),
                    CreateSpellCast("Flame Shock", ret => !HasMyAura("Flame Shock", Me.CurrentTarget, TimeSpan.FromSeconds(2), 1)),
                    CreateSpellCast("Earth Shock")
                    )
                );
        }
    }
}