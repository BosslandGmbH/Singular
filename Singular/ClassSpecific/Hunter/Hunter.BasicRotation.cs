using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using TreeSharp;

namespace Singular
{
	partial class SingularRoutine
	{
        public Composite CreateBeastMasterCombat()
        {
            return new PrioritySelector(

                // Always keep it up on our target!
                CreateSpellBuff("Hunter's Mark"),

                CreateSpellBuff("Serpent Sting"),
                // Ignore these two when our pet is raging
                CreateSpellCast("Focus Fire", ret => !Me.Pet.HasAura("Bestial Wrath")),
                CreateSpellCast("Kill Shot", ret => !Me.Pet.HasAura("Bestial Wrath")),
                // Basically, cast it whenever its up.
                CreateSpellCast("Kill Command"),
                // Only really cast this when we need a sting refresh.
                CreateSpellCast("Cobra Shot", ret => Me.CurrentTarget.HasAura("Serpent Sting") && Me.CurrentTarget.Auras["Serpent Sting"].TimeLeft.TotalSeconds < 3),
                // Focus dump on arcane shot, unless our pet has bestial wrath, then we use it for better DPS
                CreateSpellCast("Arcane Shot")
                );
        }

        public Composite CreateSurvivalCombat()
        {
            return new PrioritySelector(

                // Always keep it up on our target!
                CreateSpellBuff("Hunter's Mark"),

                new Decorator(
                    ret => !Me.HasAura("Lock and Load"),
                    new PrioritySelector(
                        // The extra here 'flips' the explosive usage.
                        CreateSpellCast("Kill Command", ret => Me.FocusPercent == 100),
                        CreateSpellCast("Explosive Shot", ret => LastSpellCast != "Explosive Shot"),
                        CreateSpellCast("Steady Shot", ret => LastSpellCast != "Steady Shot"))),

                // Refresh when it wears off.
                CreateSpellBuff("Serpent Sting"),
                // Whenever it's not on CD
                CreateSpellCast("Explosive Shot"),
                // Whenever its not on CD
                CreateSpellCast("Black Arrow"),
                // Main DPS filler
                CreateSpellCast("Steady Shot")

                );
        }
	}
}
