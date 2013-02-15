
using System;
using System.ComponentModel;
using System.IO;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    public enum FeralForm
    {
        None,
        Bear,
        Cat
    }

    internal class DruidSettings : Styx.Helpers.Settings
    {
        public DruidSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "Druid.xml"))
        {
        }
        // Pvp By IloveAnimals

        #region pvp
        /*
        [Setting]
        [DefaultValue(3)]
        [Category("Feral PvP")]
        [DisplayName("PvP Add Switch")]
        [Description("Switch to bear when the amount of attackers is equal or greater than this value")]
        public int PvPAddSwitch { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Feral PvP")]
        [DisplayName("PvP Health Switch")]
        [Description("Switch to bear when health drops below this value")]
        public int PvPHealthSwitch { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("Berserk Save")]
        [Description("Only use Berserk when there are 2 or more attackers")]
        public bool PvPBerserksafe { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("Shift out of snares")]
        [Description("Cancels Catform to remove snares")]
        public bool PvPSnared { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral PvP")]
        [DisplayName("Remove root")]
        [Description("Uses Dash/Stampeding Roar to remove root")]
        public bool PvPRooted { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("Cyclone adds")]
        [Description("Use Cyclone on adds")]
        public bool PvPccAdd { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Feral PvP")]
        [DisplayName("Rejuv Health")]
        [Description("Rejuv will be used when your health drops below this value")]
        public int PvPReju { get; set; }

        [Setting]
        [DefaultValue(20)]
        [Category("Feral PvP")]
        [DisplayName("HealingTouch Health")]
        [Description("Healing Touch will be used when your health drops below this value")]
        public int PvPHealingTouch { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Feral PvP")]
        [DisplayName("Predator's Swiftness heal")]
        [Description("Predator's Swiftness will be used when your health drops below this value")]
        public int PvPProcc { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("In combat healing")]
        public bool PvPpHealBool { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("Use Nature's Grasp")]
        [Description("Use Nature's Grasp when there are 2+ attackers")]
        public bool PvPGrasp { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("Use CC on fleeing target")]
        public bool PvPRoot { get; set; }
        */

        /* Logic for this needs work
        [Setting]
        [DefaultValue(true)]
        [Category("Feral PvP")]
        [DisplayName("Use Stealth when roaming")]
        //[Description("Use Stealth in PvP")]
        public bool PvPStealth { get; set; }
        */

        #endregion
  
        #region Common

        [Setting]
        [DefaultValue(40)]
        [Category("Common")]
        [DisplayName("Innervate Mana")]
        [Description("Innervate will be used when your mana drops below this value")]
        public int InnervateMana { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Enable Symbiosis")]
        [Description("True: will cast Symbiosis upon group member enabling special abilities")]
        public bool UseSymbiosis { get; set; }

/*
                        [Setting]
                        [DefaultValue(false)]
                        [Category("Common")]
                        [DisplayName("Disable Healing for Balance and Feral")]
                        public bool NoHealBalanceAndFeral { get; set; }

                        [Setting]
                        [DefaultValue(20)]
                        [Category("Common")]
                        [DisplayName("Healing Touch Health (Balance and Feral)")]
                        [Description("Healing Touch will be used at this value.")]
                        public int NonRestoHealingTouch { get; set; }

                        [Setting]
                        [DefaultValue(40)]
                        [Category("Common")]
                        [DisplayName("Rejuvenation Health (Balance and Feral)")]
                        [Description("Rejuvenation will be used at this value")]
                        public int NonRestoRejuvenation { get; set; }
                */
        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Combat Rez")]
        [Description("If set to true, it will rez while in combat.")]
        public bool UseRebirth { get; set; }

        [Setting]
        [DefaultValue(35)]
        [Category("Common")]
        [DisplayName("Renewal Health")]
        [Description("Renewal will be used at this value. ")]
        public int RenewalHealth { get; set; }

        #endregion

        #region Balance

        [Setting]
        [DefaultValue(false)]
        [Category("Balance")]
        [DisplayName("Starfall")]
        [Description("Use Starfall.")]
        public bool UseStarfall { get; set; }

        #endregion

        #region Resto

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("Tranquility Health")]
        [Description("Tranquility will be used at this value")]
        public int TranquilityHealth { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Restoration")]
        [DisplayName("Tranquility Count")]
        [Description("Tranquility will be used when count of party members whom health is below Tranquility health mets this value ")]
        public int TranquilityCount { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("Restoration")]
        [DisplayName("Swiftmend Health")]
        [Description("Swiftmend will be used at this value")]
        public int Swiftmend { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Restoration")]
        [DisplayName("Wild Growth Health")]
        [Description("Wild Growth will be used at this value")]
        public int WildGrowthHealth { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Restoration")]
        [DisplayName("Wild Growth Count")]
        [Description("Wild Growth will be used when count of party members whom health is below Wild Growth health mets this value ")]
        public int WildGrowthCount { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("Regrowth Health")]
        [Description("Regrowth will be used at this value")]
        public int Regrowth { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("Healing Touch Health")]
        [Description("Healing Touch will be used at this value")]
        public int HealingTouch { get; set; }

        [Setting]
        [DefaultValue(75)]
        [Category("Restoration")]
        [DisplayName("Nourish Health")]
        [Description("Nourish will be used at this value")]
        public int Nourish { get; set; }

        [Setting]
        [DefaultValue(90)]
        [Category("Restoration")]
        [DisplayName("Rejuvenation Health")]
        [Description("Rejuvenation will be used at this value")]
        public int Rejuvenation { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Restoration")]
        [DisplayName("Tree of Life Health")]
        [Description("Tree of Life will be used at this value")]
        public int TreeOfLifeHealth { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Restoration")]
        [DisplayName("Tree of Life Count")]
        [Description("Tree of Life will be used when count of party members whom health is below Tree of Life health mets this value ")]
        public int TreeOfLifeCount { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("Barkskin Health")]
        [Description("Barkskin will be used at this value")]
        public int Barkskin { get; set; }

        #endregion


        #region Guardian
        [Setting]
        [DefaultValue(70)]
        [Category("Guardian")]
        [DisplayName("Frenzied Regeneration Health")]
        [Description("FR will be used at this value. Set this to 100 to enable on cooldown usage. (Recommended: 30 if glyphed. 15 if not.)")]
        public int TankFrenziedRegenerationHealth { get; set; }

        [Setting]
        [DefaultValue(100)]
        [Category("Guardian")]
        [DisplayName("Savage Defense Health")]
        [Description("Savage Defense will be used at this value. Set this to 100 to enable on cooldown usage.")]
        public int TankSavageDefense { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Guardian")]
        [DisplayName("Might of Ursoc Health")]
        [Description("Might of Ursoc will be used at this value. Set this to 100 to enable on cooldown usage.")]
        public int TankMightOfUrsoc { get; set; }

        [Setting]
        [DefaultValue(55)]
        [Category("Guardian")]
        [DisplayName("Survival Instincts Health")]
        [Description("SI will be used at this value. Set this to 100 to enable on cooldown usage. (Recommended: 55)")]
        public int TankSurvivalInstinctsHealth { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Guardian")]
        [DisplayName("Barkskin Health")]
        [Description("Barkskin will be used at this value. Set this to 100 to enable on cooldown usage.")]
        public int TankFeralBarkskin { get; set; }

        #endregion

        #region Feral
/*
        [Setting]
        [DefaultValue(50)]
        [Category("Feral")]
        [DisplayName("Barkskin Health")]
        [Description("Barkskin will be used at this value. Set this to 100 to enable on cooldown usage.")]
        public int FeralBarkskin { get; set; }


        [Setting]
        [DefaultValue(55)]
        [Category("Feral")]
        [DisplayName("Survival Instincts Health")]
        [Description("SI will be used at this value. Set this to 100 to enable on cooldown usage. (Recommended: 55)")]
        public int SurvivalInstinctsHealth { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Feral PvP")]
        [DisplayName("Frenzied Regeneration Health")]
        [Description("FR will be used at this value. Set this to 100 to enable on cooldown usage. (Recommended: 30 if glyphed. 15 if not.)")]
        public int FrenziedRegenerationHealth { get; set; }

        [Setting]
        [DefaultValue(0)]
        [Category("Form Selection")]
        [DisplayName("Form Selection")]
        [Description("Form Selection!")]
        public int Shapeform { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Feral")]
        [DisplayName("Predator's Swiftness heal")]
        [Description("Healing with Predator's Swiftness will be used at this value")]
        public int NonRestoprocc { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Cat - Disable Healing for Balance and Feral")]
        public bool RaidCatProwl { get; set; }

        [Setting]
        [DefaultValue(15)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Cat - Predator's Swiftness (Balance and Feral)")]
        public int RaidCatProccHeal { get; set; }


        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Interrupt")]
        [Description("Automatically interrupt spells while in an instance if this value is set to true.")]
        public bool Interrupt { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Cat - Warn if not behind boss")]
        public bool CatRaidWarning { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Cat - Use dash as gap closer")]
        public bool CatRaidDash { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Cat - Use Raid button ")]
        //[Description("If set to true, it will cast Berserk only if we got adds.")]
        public bool CatRaidButtons { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Cat - Use Stampeding Roar")]
        [Description("If set to true, it will cast Stampeding Roar to close gap to target.")]
        public bool CatRaidStampeding { get; set; }
        


        [Setting]
        [DefaultValue(true)]
        [Category("Feral PvP")]
        [DisplayName("Cat - Stealth Pull")]
        [Description("Always try to pull while in stealth. If disabled it pulls with FFF instead.")]
        public bool CatNormalPullStealth { get; set; }


        [Setting]
        [DefaultValue(4)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Cat - Adds to AOE")]
        [Description("Number of adds needed to start Aoe rotation.")]
        public int CatRaidAoe { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Cat - Auto Berserk")]
        [Description("If set to true, it will cast Berserk automatically to do max dps.")]
        public bool CatRaidBerserk { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Cat - Auto Tiger's Fury")]
        [Description("If set to true, it will cast Tiger's Fury automatically to do max dps.")]
        public bool CatRaidTigers { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Cat - Rebuff infight")]
        [Description("If set to true, it will rebuff Mark of the Wild infight.")]
        public bool CatRaidRebuff { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Cat - Feral Charge")]
        [Description("Use Feral Charge to close gaps. It should handle bosses where charge is not" +
                     "possible || best solution automatically.")]
        public bool CatRaidUseFeralCharge { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Bear - Feral Charge")]
        [Description("Use Feral Charge to close gaps. It should handle bosses where charge is not" +
                     "possible || best solution automatically.")]
        public bool BearRaidUseFeralCharge { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Bear - Adds to AOE")]
        [Description("Number of adds needed to start Aoe rotation.")]
        public int BearRaidAoe { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Auto Berserk")]
        [Description("If set to true, it will cast Berserk automatically to do max threat.")]
        public bool BearRaidBerserk { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Bear - Berserk Burst")]
        [Description("If set to true, it will SPAM MANGLE FOR GODS SAKE while Berserk is active.")]
        public bool BearRaidBerserkFun { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral Raid / Instance")]
        [DisplayName("Bear - Auto defensive cooldowns")]
        [Description("If set to true, it will cast defensive cooldowns automatically.")]
        public bool BearRaidCooldown { get; set; }
*/
        // End of IloveDruids

        #endregion
    }
}