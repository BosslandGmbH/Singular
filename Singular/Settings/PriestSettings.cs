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
    internal class PriestSettings : Styx.Helpers.Settings
    {
        public PriestSettings()
            : base(SingularSettings.SettingsPath + "_Priest.xml")
        {
        }

		[Setting]
		[DefaultValue(30)]
		[Category("Discipline")]
		[DisplayName("Penance Health")]
		[Description("Penance will be used at this value")]
		public int Penance { get; set; }

		[Setting]
		[DefaultValue(40)]
		[Category("Discipline")]
		[DisplayName("Flash Heal Health")]
		[Description("Flash Heal will be used at this value")]
		public int FlashHeal { get; set; }

		[Setting]
		[DefaultValue(50)]
		[Category("Discipline")]
		[DisplayName("Greater Heal Health")]
		[Description("Greater Heal will be used at this value")]
		public int GreaterHeal { get; set; }

		[Setting]
		[DefaultValue(70)]
		[Category("Discipline")]
		[DisplayName("Heal Health")]
		[Description("Heal will be used at this value")]
		public int Heal { get; set; }

		[Setting]
		[DefaultValue(70)]
		[Category("Discipline")]
		[DisplayName("Renew Health")]
		[Description("Renew will be used at this value")]
		public int Renew { get; set; }

		[Setting]
		[DefaultValue(30)]
		[Category("Discipline")]
		[DisplayName("Pain Suppression Health")]
		[Description("Pain Suppression will be used at this value")]
		public int PainSuppression { get; set; }

		[Setting]
		[DefaultValue(70)]
		[Category("Discipline")]
		[DisplayName("Binding Heal Self Health")]
		[Description("Binding Heal will be used when your health is below this value")]
		public int BindingHealMe { get; set; }

		[Setting]
		[DefaultValue(70)]
		[Category("Discipline")]
		[DisplayName("Binding Heal Other Health")]
		[Description("Binding Heal will be used when someone elses health is below this value")]
		public int BindingHealThem { get; set; }

		[Setting]
		[DefaultValue(50)]
		[Category("Discipline")]
		[DisplayName("Prayer of Healing Health")]
		[Description("Prayer of Healing will be used at this value")]
		public int PrayerOfHealing { get; set; }

		[Setting]
		[DefaultValue(3)]
		[Category("Discipline")]
		[DisplayName("Prayer of Healing Count")]
		[Description("Prayer of Healing will be used when count of players whom health is below PoH Health setting mets this value")]
		public int PrayerOfHealingCount { get; set; }
    }
}