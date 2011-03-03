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

using System.ComponentModel;

using Styx;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class PaladinSettings : Styx.Helpers.Settings
    {
        public PaladinSettings()
            : base(SingularSettings.SettingsPath + "_Paladin.xml")
        {
        }

		[Setting]
		[DefaultValue(40)]
		[Category("Protection")]
		[DisplayName("Lay on Hands Health")]
		[Description("Lay on Hands will be used at this value")]
		public int LayOnHandsHealthProt { get; set; }

		[Setting]
		[DefaultValue(40)]
		[Category("Protection")]
		[DisplayName("Guardian of Ancient Kings Health")]
		[Description("Guardian of Ancient Kings will be used at this value")]
		public int GoAKHealth { get; set; }

		[Setting]
		[DefaultValue(40)]
		[Category("Protection")]
		[DisplayName("Ardent Defender Health")]
		[Description("Ardent Defender will be used at this value")]
		public int ArdentDefenderHealth { get; set; }

		[Setting]
		[DefaultValue(80)]
		[Category("Protection")]
		[DisplayName("Divine Protection Health")]
		[Description("Divine Protection will be used at this value")]
		public int DivineProtectionHealthProt { get; set; }

		[Setting]
		[DefaultValue(70)]
		[Category("Retribution")]
		[DisplayName("Divine Protection Health")]
		[Description("Divine Protection will be used at this value")]
		public int DivineProtectionHealthRet { get; set; }

		[Setting]
		[DefaultValue(30)]
		[Category("Retribution")]
		[DisplayName("Lay on Hands Health")]
		[Description("Lay on Hands will be used at this value")]
		public int LayOnHandsHealthRet { get; set; }

		[Setting]
		[DefaultValue(3)]
		[Category("Retribution")]
		[DisplayName("Consecration Count")]
		[Description("Consecration will be used when you have more then that many mobs attacking you")]
		public int ConsecrationCount { get; set; }
    }
}