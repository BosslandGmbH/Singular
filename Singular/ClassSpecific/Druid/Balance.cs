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

using Singular.Composites;

using Styx;
using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        private string _oldDps = "Wrath";

        private int oldEclipse;
        private int StarfallRange { get { return TalentManager.HasGlyph("Focus") ? 20 : 40; } }

        private int CurrentEclipse { get { return BitConverter.ToInt32(BitConverter.GetBytes(Me.CurrentEclipse), 0); } }

        private string BoomkinDpsSpell
        {
            get
            {
                if (Me.HasAura("Eclipse (Solar)"))
                {
                    _oldDps = "Wrath";
                }
                    // This doesn't seem to register for whatever reason.
                else if (Me.HasAura("Eclipse (Lunar)")) //Eclipse (Lunar) => 48518
                {
                    _oldDps = "Starfire";
                }
                //else
                //{
                //    if (oldEclipse == 0)
                //        oldEclipse = CurrentEclipse;

                //    // If our current eclipse is higher than our old one, we want to be casting Starfire.
                //    // Vice versa for Wrath.
                //    if (CurrentEclipse > oldEclipse)
                //        _oldDps = "Starfire";
                //    else if (CurrentEclipse < oldEclipse)
                //        _oldDps = "Wrath";

                //    // If we haven't changed at all... try flipping the DPS spell we wanna use.
                //    //else if (CurrentEclipse == oldEclipse)
                //    //{
                //    //    _oldDps = _oldDps == "Wrath" ? "Starfire" : "Wrath";
                //    //}

                //    oldEclipse = CurrentEclipse;
                //}

                return _oldDps;
            }
        }

        [Class(WoWClass.Druid)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Spec(TalentSpec.BalanceDruid)]
        public Composite CreateBalanceDruidCombat()
        {
            WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(
                new ActionLogMessage(false, () => "Eclipse Percent: " + CurrentEclipse),
                CreateEnsureTarget(),
                CreateMoveToAndFace(35, ret => Me.CurrentTarget),
                // Ensure we do /petattack if we have treants up.
                CreateAutoAttack(true),
                CreateSpellCast("Starfall"),
                CreateSpellCastOnLocation("Force of Nature", ret => Me.CurrentTarget.Location),
                CreateSpellCast("Solar Beam", ret => Me.CurrentTarget.IsCasting),
                CreateSpellCast("Starsurge"),
                CreateSpellCast(
                    "Moonfire",
                    ret =>
                    GetAuraTimeLeft("Moonfire", Me.CurrentTarget, true).TotalSeconds < 3 &&
                    GetAuraTimeLeft("Sunfire", Me.CurrentTarget, true).Seconds < 3),
                CreateSpellCast("Insect Swarm", ret => GetAuraTimeLeft("Insect Swarm", Me.CurrentTarget, true).TotalSeconds < 3),
                //CreateSpellCast("Wrath", ret => Me.HasAura("Eclipse (Solar)")),
                //CreateSpellCast("Starfire", ret => Me.HasAura("Eclipse (Lunar)")),

                CreateSpellCast("Wrath", ret => BoomkinDpsSpell == "Wrath"),
                CreateSpellCast("Starfire", ret => BoomkinDpsSpell == "Starfire")
                );
        }
    }
}