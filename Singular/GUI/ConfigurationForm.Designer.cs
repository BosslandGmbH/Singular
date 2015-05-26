namespace Singular.GUI
{
    partial class ConfigurationForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabGeneral = new System.Windows.Forms.TabPage();
            this.pgGeneral = new System.Windows.Forms.PropertyGrid();
            this.tabClass = new System.Windows.Forms.TabPage();
            this.pgClass = new System.Windows.Forms.PropertyGrid();
            this.tabGroupHeal = new System.Windows.Forms.TabPage();
            this.pgHeal = new System.Windows.Forms.PropertyGrid();
            this.grpHealHeader = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.cboHealContext = new System.Windows.Forms.ComboBox();
            this.tabHotkeys = new System.Windows.Forms.TabPage();
            this.pgHotkeys = new System.Windows.Forms.PropertyGrid();
            this.tabDebug = new System.Windows.Forms.TabPage();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.chkBFVendor = new System.Windows.Forms.CheckBox();
            this.chkBFFlightPath = new System.Windows.Forms.CheckBox();
            this.chkBFLoot = new System.Windows.Forms.CheckBox();
            this.chkBFRest = new System.Windows.Forms.CheckBox();
            this.chkBFRoam = new System.Windows.Forms.CheckBox();
            this.chkBFCombat = new System.Windows.Forms.CheckBox();
            this.chkBFDeath = new System.Windows.Forms.CheckBox();
            this.chkBFPull = new System.Windows.Forms.CheckBox();
            this.grpCapability = new System.Windows.Forms.GroupBox();
            this.chkCFInterrupt = new System.Windows.Forms.CheckBox();
            this.chkCFTaunt = new System.Windows.Forms.CheckBox();
            this.chkCFMultiMobPull = new System.Windows.Forms.CheckBox();
            this.chkCFDefDispel = new System.Windows.Forms.CheckBox();
            this.chkCFOffDispel = new System.Windows.Forms.CheckBox();
            this.chkCFKiting = new System.Windows.Forms.CheckBox();
            this.chkCFSpecialAttacks = new System.Windows.Forms.CheckBox();
            this.chkCFPetUse = new System.Windows.Forms.CheckBox();
            this.chkCFPetSummon = new System.Windows.Forms.CheckBox();
            this.chkCFTargeting = new System.Windows.Forms.CheckBox();
            this.chkCFAOE = new System.Windows.Forms.CheckBox();
            this.chkCFGapCloser = new System.Windows.Forms.CheckBox();
            this.chkCFFacing = new System.Windows.Forms.CheckBox();
            this.chkCFMoveBehind = new System.Windows.Forms.CheckBox();
            this.chkCFMovement = new System.Windows.Forms.CheckBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.lblTankToStayNear = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.chkTraceBuffs = new System.Windows.Forms.CheckBox();
            this.cboDebugOutput = new System.Windows.Forms.ComboBox();
            this.cboForceUseOf = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.chkDebugCasting = new System.Windows.Forms.CheckBox();
            this.chkDebugTraceHeal = new System.Windows.Forms.CheckBox();
            this.chkDebugTrace = new System.Windows.Forms.CheckBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.lblPoi = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.lblTargets = new System.Windows.Forms.Label();
            this.grpAuxTargeting = new System.Windows.Forms.GroupBox();
            this.lblAuxTargets = new System.Windows.Forms.Label();
            this.timerTargeting = new System.Windows.Forms.Timer(this.components);
            this.grpFooter = new System.Windows.Forms.GroupBox();
            this.btnDump = new System.Windows.Forms.Button();
            this.btnLogMark = new System.Windows.Forms.Button();
            this.btnSaveAndClose = new System.Windows.Forms.Button();
            this.lblVersion = new System.Windows.Forms.Label();
            this.lblBuildTime = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.chkShowBehaviorFlags = new System.Windows.Forms.CheckBox();
            this.tabControl1.SuspendLayout();
            this.tabGeneral.SuspendLayout();
            this.tabClass.SuspendLayout();
            this.tabGroupHeal.SuspendLayout();
            this.grpHealHeader.SuspendLayout();
            this.tabHotkeys.SuspendLayout();
            this.tabDebug.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.grpCapability.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.grpAuxTargeting.SuspendLayout();
            this.grpFooter.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabGeneral);
            this.tabControl1.Controls.Add(this.tabClass);
            this.tabControl1.Controls.Add(this.tabGroupHeal);
            this.tabControl1.Controls.Add(this.tabHotkeys);
            this.tabControl1.Controls.Add(this.tabDebug);
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Multiline = true;
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(347, 420);
            this.tabControl1.TabIndex = 4;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            this.tabControl1.Selected += new System.Windows.Forms.TabControlEventHandler(this.tabControl1_Selected);
            this.tabControl1.VisibleChanged += new System.EventHandler(this.tabControl1_VisibleChanged);
            // 
            // tabGeneral
            // 
            this.tabGeneral.Controls.Add(this.pgGeneral);
            this.tabGeneral.Location = new System.Drawing.Point(4, 22);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Padding = new System.Windows.Forms.Padding(3);
            this.tabGeneral.Size = new System.Drawing.Size(339, 394);
            this.tabGeneral.TabIndex = 0;
            this.tabGeneral.Text = "General";
            this.tabGeneral.UseVisualStyleBackColor = true;
            // 
            // pgGeneral
            // 
            this.pgGeneral.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgGeneral.Location = new System.Drawing.Point(3, 3);
            this.pgGeneral.Name = "pgGeneral";
            this.pgGeneral.Size = new System.Drawing.Size(333, 388);
            this.pgGeneral.TabIndex = 0;
            // 
            // tabClass
            // 
            this.tabClass.Controls.Add(this.pgClass);
            this.tabClass.Location = new System.Drawing.Point(4, 22);
            this.tabClass.Name = "tabClass";
            this.tabClass.Padding = new System.Windows.Forms.Padding(3);
            this.tabClass.Size = new System.Drawing.Size(339, 394);
            this.tabClass.TabIndex = 1;
            this.tabClass.Text = "Class Specific";
            this.tabClass.UseVisualStyleBackColor = true;
            // 
            // pgClass
            // 
            this.pgClass.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgClass.Location = new System.Drawing.Point(3, 3);
            this.pgClass.Name = "pgClass";
            this.pgClass.Size = new System.Drawing.Size(333, 388);
            this.pgClass.TabIndex = 0;
            // 
            // tabGroupHeal
            // 
            this.tabGroupHeal.Controls.Add(this.pgHeal);
            this.tabGroupHeal.Controls.Add(this.grpHealHeader);
            this.tabGroupHeal.Location = new System.Drawing.Point(4, 22);
            this.tabGroupHeal.Name = "tabGroupHeal";
            this.tabGroupHeal.Size = new System.Drawing.Size(339, 394);
            this.tabGroupHeal.TabIndex = 3;
            this.tabGroupHeal.Text = "Group Healing";
            this.tabGroupHeal.UseVisualStyleBackColor = true;
            // 
            // pgHeal
            // 
            this.pgHeal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgHeal.Location = new System.Drawing.Point(0, 39);
            this.pgHeal.Name = "pgHeal";
            this.pgHeal.PropertySort = System.Windows.Forms.PropertySort.NoSort;
            this.pgHeal.Size = new System.Drawing.Size(339, 355);
            this.pgHeal.TabIndex = 5;
            // 
            // grpHealHeader
            // 
            this.grpHealHeader.Controls.Add(this.label3);
            this.grpHealHeader.Controls.Add(this.cboHealContext);
            this.grpHealHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.grpHealHeader.ForeColor = System.Drawing.SystemColors.Control;
            this.grpHealHeader.Location = new System.Drawing.Point(0, 0);
            this.grpHealHeader.Margin = new System.Windows.Forms.Padding(0);
            this.grpHealHeader.Name = "grpHealHeader";
            this.grpHealHeader.Padding = new System.Windows.Forms.Padding(0);
            this.grpHealHeader.Size = new System.Drawing.Size(339, 39);
            this.grpHealHeader.TabIndex = 1;
            this.grpHealHeader.TabStop = false;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label3.Location = new System.Drawing.Point(7, 14);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(101, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Healing Context:";
            // 
            // cboHealContext
            // 
            this.cboHealContext.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboHealContext.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboHealContext.ForeColor = System.Drawing.SystemColors.ControlText;
            this.cboHealContext.FormattingEnabled = true;
            this.cboHealContext.Location = new System.Drawing.Point(114, 10);
            this.cboHealContext.Name = "cboHealContext";
            this.cboHealContext.Size = new System.Drawing.Size(208, 21);
            this.cboHealContext.TabIndex = 3;
            this.toolTip1.SetToolTip(this.cboHealContext, "Choose the Spec + Context you want to configure");
            this.cboHealContext.SelectedIndexChanged += new System.EventHandler(this.cboHealContext_SelectedIndexChanged);
            // 
            // tabHotkeys
            // 
            this.tabHotkeys.Controls.Add(this.pgHotkeys);
            this.tabHotkeys.Location = new System.Drawing.Point(4, 22);
            this.tabHotkeys.Name = "tabHotkeys";
            this.tabHotkeys.Padding = new System.Windows.Forms.Padding(3);
            this.tabHotkeys.Size = new System.Drawing.Size(339, 394);
            this.tabHotkeys.TabIndex = 4;
            this.tabHotkeys.Text = "Hotkeys";
            this.tabHotkeys.UseVisualStyleBackColor = true;
            // 
            // pgHotkeys
            // 
            this.pgHotkeys.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgHotkeys.Location = new System.Drawing.Point(3, 3);
            this.pgHotkeys.Name = "pgHotkeys";
            this.pgHotkeys.PropertySort = System.Windows.Forms.PropertySort.Categorized;
            this.pgHotkeys.Size = new System.Drawing.Size(333, 388);
            this.pgHotkeys.TabIndex = 1;
            // 
            // tabDebug
            // 
            this.tabDebug.Controls.Add(this.groupBox4);
            this.tabDebug.Controls.Add(this.grpCapability);
            this.tabDebug.Controls.Add(this.groupBox1);
            this.tabDebug.Controls.Add(this.groupBox3);
            this.tabDebug.Controls.Add(this.groupBox5);
            this.tabDebug.Controls.Add(this.groupBox2);
            this.tabDebug.Controls.Add(this.grpAuxTargeting);
            this.tabDebug.Location = new System.Drawing.Point(4, 22);
            this.tabDebug.Name = "tabDebug";
            this.tabDebug.Padding = new System.Windows.Forms.Padding(3);
            this.tabDebug.Size = new System.Drawing.Size(339, 394);
            this.tabDebug.TabIndex = 2;
            this.tabDebug.Text = " Debug";
            this.tabDebug.UseVisualStyleBackColor = true;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.chkBFVendor);
            this.groupBox4.Controls.Add(this.chkBFFlightPath);
            this.groupBox4.Controls.Add(this.chkBFLoot);
            this.groupBox4.Controls.Add(this.chkBFRest);
            this.groupBox4.Controls.Add(this.chkBFRoam);
            this.groupBox4.Controls.Add(this.chkBFCombat);
            this.groupBox4.Controls.Add(this.chkBFDeath);
            this.groupBox4.Controls.Add(this.chkBFPull);
            this.groupBox4.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.groupBox4.Location = new System.Drawing.Point(6, 392);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(313, 75);
            this.groupBox4.TabIndex = 7;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Behavior Flags";
            // 
            // chkBFVendor
            // 
            this.chkBFVendor.AutoSize = true;
            this.chkBFVendor.Enabled = false;
            this.chkBFVendor.Location = new System.Drawing.Point(107, 51);
            this.chkBFVendor.Name = "chkBFVendor";
            this.chkBFVendor.Size = new System.Drawing.Size(60, 17);
            this.chkBFVendor.TabIndex = 2;
            this.chkBFVendor.TabStop = false;
            this.chkBFVendor.Text = "Vendor";
            this.chkBFVendor.UseVisualStyleBackColor = true;
            // 
            // chkBFFlightPath
            // 
            this.chkBFFlightPath.AutoSize = true;
            this.chkBFFlightPath.Enabled = false;
            this.chkBFFlightPath.Location = new System.Drawing.Point(206, 35);
            this.chkBFFlightPath.Name = "chkBFFlightPath";
            this.chkBFFlightPath.Size = new System.Drawing.Size(76, 17);
            this.chkBFFlightPath.TabIndex = 1;
            this.chkBFFlightPath.TabStop = false;
            this.chkBFFlightPath.Text = "Flight Path";
            this.chkBFFlightPath.UseVisualStyleBackColor = true;
            // 
            // chkBFLoot
            // 
            this.chkBFLoot.AutoSize = true;
            this.chkBFLoot.Enabled = false;
            this.chkBFLoot.Location = new System.Drawing.Point(107, 35);
            this.chkBFLoot.Name = "chkBFLoot";
            this.chkBFLoot.Size = new System.Drawing.Size(47, 17);
            this.chkBFLoot.TabIndex = 2;
            this.chkBFLoot.TabStop = false;
            this.chkBFLoot.Text = "Loot";
            this.chkBFLoot.UseVisualStyleBackColor = true;
            // 
            // chkBFRest
            // 
            this.chkBFRest.AutoSize = true;
            this.chkBFRest.Enabled = false;
            this.chkBFRest.Location = new System.Drawing.Point(11, 51);
            this.chkBFRest.Name = "chkBFRest";
            this.chkBFRest.Size = new System.Drawing.Size(48, 17);
            this.chkBFRest.TabIndex = 3;
            this.chkBFRest.TabStop = false;
            this.chkBFRest.Text = "Rest";
            this.chkBFRest.UseVisualStyleBackColor = false;
            // 
            // chkBFRoam
            // 
            this.chkBFRoam.AutoSize = true;
            this.chkBFRoam.Enabled = false;
            this.chkBFRoam.Location = new System.Drawing.Point(206, 19);
            this.chkBFRoam.Name = "chkBFRoam";
            this.chkBFRoam.Size = new System.Drawing.Size(54, 17);
            this.chkBFRoam.TabIndex = 1;
            this.chkBFRoam.TabStop = false;
            this.chkBFRoam.Text = "Roam";
            this.chkBFRoam.UseVisualStyleBackColor = true;
            // 
            // chkBFCombat
            // 
            this.chkBFCombat.AutoSize = true;
            this.chkBFCombat.Enabled = false;
            this.chkBFCombat.Location = new System.Drawing.Point(11, 35);
            this.chkBFCombat.Name = "chkBFCombat";
            this.chkBFCombat.Size = new System.Drawing.Size(62, 17);
            this.chkBFCombat.TabIndex = 3;
            this.chkBFCombat.TabStop = false;
            this.chkBFCombat.Text = "Combat";
            this.chkBFCombat.UseVisualStyleBackColor = false;
            // 
            // chkBFDeath
            // 
            this.chkBFDeath.AutoSize = true;
            this.chkBFDeath.Enabled = false;
            this.chkBFDeath.Location = new System.Drawing.Point(107, 19);
            this.chkBFDeath.Name = "chkBFDeath";
            this.chkBFDeath.Size = new System.Drawing.Size(55, 17);
            this.chkBFDeath.TabIndex = 2;
            this.chkBFDeath.TabStop = false;
            this.chkBFDeath.Text = "Death";
            this.chkBFDeath.UseVisualStyleBackColor = true;
            // 
            // chkBFPull
            // 
            this.chkBFPull.AutoSize = true;
            this.chkBFPull.Enabled = false;
            this.chkBFPull.Location = new System.Drawing.Point(11, 19);
            this.chkBFPull.Name = "chkBFPull";
            this.chkBFPull.Size = new System.Drawing.Size(43, 17);
            this.chkBFPull.TabIndex = 3;
            this.chkBFPull.TabStop = false;
            this.chkBFPull.Text = "Pull";
            this.chkBFPull.UseVisualStyleBackColor = false;
            // 
            // grpCapability
            // 
            this.grpCapability.Controls.Add(this.chkCFInterrupt);
            this.grpCapability.Controls.Add(this.chkCFTaunt);
            this.grpCapability.Controls.Add(this.chkCFMultiMobPull);
            this.grpCapability.Controls.Add(this.chkCFDefDispel);
            this.grpCapability.Controls.Add(this.chkCFOffDispel);
            this.grpCapability.Controls.Add(this.chkCFKiting);
            this.grpCapability.Controls.Add(this.chkCFSpecialAttacks);
            this.grpCapability.Controls.Add(this.chkCFPetUse);
            this.grpCapability.Controls.Add(this.chkCFPetSummon);
            this.grpCapability.Controls.Add(this.chkCFTargeting);
            this.grpCapability.Controls.Add(this.chkCFAOE);
            this.grpCapability.Controls.Add(this.chkCFGapCloser);
            this.grpCapability.Controls.Add(this.chkCFFacing);
            this.grpCapability.Controls.Add(this.chkCFMoveBehind);
            this.grpCapability.Controls.Add(this.chkCFMovement);
            this.grpCapability.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.grpCapability.Location = new System.Drawing.Point(7, 473);
            this.grpCapability.Name = "grpCapability";
            this.grpCapability.Size = new System.Drawing.Size(313, 112);
            this.grpCapability.TabIndex = 4;
            this.grpCapability.TabStop = false;
            this.grpCapability.Text = "Capability Flags";
            // 
            // chkCFInterrupt
            // 
            this.chkCFInterrupt.AutoSize = true;
            this.chkCFInterrupt.Enabled = false;
            this.chkCFInterrupt.Location = new System.Drawing.Point(206, 84);
            this.chkCFInterrupt.Name = "chkCFInterrupt";
            this.chkCFInterrupt.Size = new System.Drawing.Size(65, 17);
            this.chkCFInterrupt.TabIndex = 1;
            this.chkCFInterrupt.TabStop = false;
            this.chkCFInterrupt.Text = "Interrupt";
            this.chkCFInterrupt.UseVisualStyleBackColor = true;
            // 
            // chkCFTaunt
            // 
            this.chkCFTaunt.AutoSize = true;
            this.chkCFTaunt.Enabled = false;
            this.chkCFTaunt.Location = new System.Drawing.Point(107, 84);
            this.chkCFTaunt.Name = "chkCFTaunt";
            this.chkCFTaunt.Size = new System.Drawing.Size(54, 17);
            this.chkCFTaunt.TabIndex = 2;
            this.chkCFTaunt.TabStop = false;
            this.chkCFTaunt.Text = "Taunt";
            this.chkCFTaunt.UseVisualStyleBackColor = true;
            // 
            // chkCFMultiMobPull
            // 
            this.chkCFMultiMobPull.AutoSize = true;
            this.chkCFMultiMobPull.Enabled = false;
            this.chkCFMultiMobPull.Location = new System.Drawing.Point(11, 84);
            this.chkCFMultiMobPull.Name = "chkCFMultiMobPull";
            this.chkCFMultiMobPull.Size = new System.Drawing.Size(68, 17);
            this.chkCFMultiMobPull.TabIndex = 3;
            this.chkCFMultiMobPull.TabStop = false;
            this.chkCFMultiMobPull.Text = "Multi Pull";
            this.chkCFMultiMobPull.UseVisualStyleBackColor = false;
            // 
            // chkCFDefDispel
            // 
            this.chkCFDefDispel.AutoSize = true;
            this.chkCFDefDispel.Enabled = false;
            this.chkCFDefDispel.Location = new System.Drawing.Point(206, 68);
            this.chkCFDefDispel.Name = "chkCFDefDispel";
            this.chkCFDefDispel.Size = new System.Drawing.Size(75, 17);
            this.chkCFDefDispel.TabIndex = 0;
            this.chkCFDefDispel.TabStop = false;
            this.chkCFDefDispel.Text = "Def Dispel";
            this.chkCFDefDispel.UseVisualStyleBackColor = true;
            // 
            // chkCFOffDispel
            // 
            this.chkCFOffDispel.AutoSize = true;
            this.chkCFOffDispel.Enabled = false;
            this.chkCFOffDispel.Location = new System.Drawing.Point(206, 52);
            this.chkCFOffDispel.Name = "chkCFOffDispel";
            this.chkCFOffDispel.Size = new System.Drawing.Size(72, 17);
            this.chkCFOffDispel.TabIndex = 0;
            this.chkCFOffDispel.TabStop = false;
            this.chkCFOffDispel.Text = "Off Dispel";
            this.chkCFOffDispel.UseVisualStyleBackColor = true;
            // 
            // chkCFKiting
            // 
            this.chkCFKiting.AutoSize = true;
            this.chkCFKiting.Enabled = false;
            this.chkCFKiting.Location = new System.Drawing.Point(206, 36);
            this.chkCFKiting.Name = "chkCFKiting";
            this.chkCFKiting.Size = new System.Drawing.Size(52, 17);
            this.chkCFKiting.TabIndex = 0;
            this.chkCFKiting.TabStop = false;
            this.chkCFKiting.Text = "Kiting";
            this.chkCFKiting.UseVisualStyleBackColor = true;
            // 
            // chkCFSpecialAttacks
            // 
            this.chkCFSpecialAttacks.AutoSize = true;
            this.chkCFSpecialAttacks.Enabled = false;
            this.chkCFSpecialAttacks.Location = new System.Drawing.Point(206, 20);
            this.chkCFSpecialAttacks.Name = "chkCFSpecialAttacks";
            this.chkCFSpecialAttacks.Size = new System.Drawing.Size(100, 17);
            this.chkCFSpecialAttacks.TabIndex = 0;
            this.chkCFSpecialAttacks.TabStop = false;
            this.chkCFSpecialAttacks.Text = "Special Attacks";
            this.chkCFSpecialAttacks.UseVisualStyleBackColor = true;
            // 
            // chkCFPetUse
            // 
            this.chkCFPetUse.AutoSize = true;
            this.chkCFPetUse.Enabled = false;
            this.chkCFPetUse.Location = new System.Drawing.Point(107, 68);
            this.chkCFPetUse.Name = "chkCFPetUse";
            this.chkCFPetUse.Size = new System.Drawing.Size(64, 17);
            this.chkCFPetUse.TabIndex = 0;
            this.chkCFPetUse.TabStop = false;
            this.chkCFPetUse.Text = "Pet Use";
            this.chkCFPetUse.UseVisualStyleBackColor = true;
            // 
            // chkCFPetSummon
            // 
            this.chkCFPetSummon.AutoSize = true;
            this.chkCFPetSummon.Enabled = false;
            this.chkCFPetSummon.Location = new System.Drawing.Point(107, 52);
            this.chkCFPetSummon.Name = "chkCFPetSummon";
            this.chkCFPetSummon.Size = new System.Drawing.Size(86, 17);
            this.chkCFPetSummon.TabIndex = 0;
            this.chkCFPetSummon.TabStop = false;
            this.chkCFPetSummon.Text = "Pet Summon";
            this.chkCFPetSummon.UseVisualStyleBackColor = true;
            // 
            // chkCFTargeting
            // 
            this.chkCFTargeting.AutoSize = true;
            this.chkCFTargeting.Enabled = false;
            this.chkCFTargeting.Location = new System.Drawing.Point(107, 36);
            this.chkCFTargeting.Name = "chkCFTargeting";
            this.chkCFTargeting.Size = new System.Drawing.Size(71, 17);
            this.chkCFTargeting.TabIndex = 0;
            this.chkCFTargeting.TabStop = false;
            this.chkCFTargeting.Text = "Targeting";
            this.chkCFTargeting.UseVisualStyleBackColor = true;
            // 
            // chkCFAOE
            // 
            this.chkCFAOE.AutoSize = true;
            this.chkCFAOE.Enabled = false;
            this.chkCFAOE.Location = new System.Drawing.Point(107, 20);
            this.chkCFAOE.Name = "chkCFAOE";
            this.chkCFAOE.Size = new System.Drawing.Size(48, 17);
            this.chkCFAOE.TabIndex = 0;
            this.chkCFAOE.TabStop = false;
            this.chkCFAOE.Text = "AOE";
            this.chkCFAOE.UseVisualStyleBackColor = true;
            // 
            // chkCFGapCloser
            // 
            this.chkCFGapCloser.AutoSize = true;
            this.chkCFGapCloser.Enabled = false;
            this.chkCFGapCloser.Location = new System.Drawing.Point(11, 68);
            this.chkCFGapCloser.Name = "chkCFGapCloser";
            this.chkCFGapCloser.Size = new System.Drawing.Size(78, 17);
            this.chkCFGapCloser.TabIndex = 0;
            this.chkCFGapCloser.TabStop = false;
            this.chkCFGapCloser.Text = "Gap Closer";
            this.chkCFGapCloser.UseVisualStyleBackColor = false;
            // 
            // chkCFFacing
            // 
            this.chkCFFacing.AutoSize = true;
            this.chkCFFacing.Enabled = false;
            this.chkCFFacing.Location = new System.Drawing.Point(11, 52);
            this.chkCFFacing.Name = "chkCFFacing";
            this.chkCFFacing.Size = new System.Drawing.Size(58, 17);
            this.chkCFFacing.TabIndex = 0;
            this.chkCFFacing.TabStop = false;
            this.chkCFFacing.Text = "Facing";
            this.chkCFFacing.UseVisualStyleBackColor = true;
            // 
            // chkCFMoveBehind
            // 
            this.chkCFMoveBehind.AutoSize = true;
            this.chkCFMoveBehind.Enabled = false;
            this.chkCFMoveBehind.Location = new System.Drawing.Point(11, 36);
            this.chkCFMoveBehind.Name = "chkCFMoveBehind";
            this.chkCFMoveBehind.Size = new System.Drawing.Size(89, 17);
            this.chkCFMoveBehind.TabIndex = 0;
            this.chkCFMoveBehind.TabStop = false;
            this.chkCFMoveBehind.Text = "Move Behind";
            this.chkCFMoveBehind.UseVisualStyleBackColor = true;
            // 
            // chkCFMovement
            // 
            this.chkCFMovement.AutoSize = true;
            this.chkCFMovement.Enabled = false;
            this.chkCFMovement.Location = new System.Drawing.Point(11, 20);
            this.chkCFMovement.Name = "chkCFMovement";
            this.chkCFMovement.Size = new System.Drawing.Size(76, 17);
            this.chkCFMovement.TabIndex = 0;
            this.chkCFMovement.TabStop = false;
            this.chkCFMovement.Text = "Movement";
            this.chkCFMovement.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.lblTankToStayNear);
            this.groupBox1.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.groupBox1.Location = new System.Drawing.Point(7, 133);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(313, 38);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Tank to Stay Near";
            // 
            // lblTankToStayNear
            // 
            this.lblTankToStayNear.AutoSize = true;
            this.lblTankToStayNear.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTankToStayNear.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblTankToStayNear.Location = new System.Drawing.Point(6, 16);
            this.lblTankToStayNear.Name = "lblTankToStayNear";
            this.lblTankToStayNear.Size = new System.Drawing.Size(35, 14);
            this.lblTankToStayNear.TabIndex = 0;
            this.lblTankToStayNear.Text = "None";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.chkTraceBuffs);
            this.groupBox3.Controls.Add(this.cboDebugOutput);
            this.groupBox3.Controls.Add(this.cboForceUseOf);
            this.groupBox3.Controls.Add(this.label6);
            this.groupBox3.Controls.Add(this.label5);
            this.groupBox3.Controls.Add(this.chkShowBehaviorFlags);
            this.groupBox3.Controls.Add(this.chkDebugCasting);
            this.groupBox3.Controls.Add(this.chkDebugTraceHeal);
            this.groupBox3.Controls.Add(this.chkDebugTrace);
            this.groupBox3.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.groupBox3.Location = new System.Drawing.Point(7, 277);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(314, 109);
            this.groupBox3.TabIndex = 3;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Miscellaneous Debug Settings";
            // 
            // chkTraceBuffs
            // 
            this.chkTraceBuffs.AutoSize = true;
            this.chkTraceBuffs.ForeColor = System.Drawing.SystemColors.ControlText;
            this.chkTraceBuffs.Location = new System.Drawing.Point(175, 52);
            this.chkTraceBuffs.Name = "chkTraceBuffs";
            this.chkTraceBuffs.Size = new System.Drawing.Size(81, 17);
            this.chkTraceBuffs.TabIndex = 5;
            this.chkTraceBuffs.Text = "Trace Buffs";
            this.toolTip1.SetToolTip(this.chkTraceBuffs, "Enable Singular Behavior tracing -- EXTREMELY VERBOSE!!!");
            this.chkTraceBuffs.UseVisualStyleBackColor = true;
            // 
            // cboDebugOutput
            // 
            this.cboDebugOutput.ForeColor = System.Drawing.SystemColors.ControlText;
            this.cboDebugOutput.FormattingEnabled = true;
            this.cboDebugOutput.Location = new System.Drawing.Point(6, 16);
            this.cboDebugOutput.Name = "cboDebugOutput";
            this.cboDebugOutput.Size = new System.Drawing.Size(68, 21);
            this.cboDebugOutput.TabIndex = 0;
            // 
            // cboForceUseOf
            // 
            this.cboForceUseOf.ForeColor = System.Drawing.SystemColors.ControlText;
            this.cboForceUseOf.FormattingEnabled = true;
            this.cboForceUseOf.Location = new System.Drawing.Point(151, 82);
            this.cboForceUseOf.Name = "cboForceUseOf";
            this.cboForceUseOf.Size = new System.Drawing.Size(154, 21);
            this.cboForceUseOf.TabIndex = 3;
            this.toolTip1.SetToolTip(this.cboForceUseOf, "*not saved* - Select behaviors to use on Taining Dummy");
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label6.Location = new System.Drawing.Point(80, 20);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(74, 13);
            this.label6.TabIndex = 1;
            this.label6.Text = "Debug Output";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label5.Location = new System.Drawing.Point(3, 85);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(148, 13);
            this.label5.TabIndex = 7;
            this.label5.Text = "Behaviors on Training Dummy";
            // 
            // chkDebugCasting
            // 
            this.chkDebugCasting.AutoSize = true;
            this.chkDebugCasting.ForeColor = System.Drawing.SystemColors.ControlText;
            this.chkDebugCasting.Location = new System.Drawing.Point(6, 44);
            this.chkDebugCasting.Name = "chkDebugCasting";
            this.chkDebugCasting.Size = new System.Drawing.Size(132, 17);
            this.chkDebugCasting.TabIndex = 2;
            this.chkDebugCasting.Text = "Debug Casting Engine";
            this.toolTip1.SetToolTip(this.chkDebugCasting, "Enable additional debug output for spell casting (more verbose)");
            this.chkDebugCasting.UseVisualStyleBackColor = true;
            this.chkDebugCasting.CheckedChanged += new System.EventHandler(this.chkDebugLogging_CheckedChanged);
            // 
            // chkDebugTraceHeal
            // 
            this.chkDebugTraceHeal.AutoSize = true;
            this.chkDebugTraceHeal.ForeColor = System.Drawing.SystemColors.ControlText;
            this.chkDebugTraceHeal.Location = new System.Drawing.Point(175, 33);
            this.chkDebugTraceHeal.Name = "chkDebugTraceHeal";
            this.chkDebugTraceHeal.Size = new System.Drawing.Size(93, 17);
            this.chkDebugTraceHeal.TabIndex = 4;
            this.chkDebugTraceHeal.Text = "Trace Healing";
            this.chkDebugTraceHeal.UseVisualStyleBackColor = true;
            // 
            // chkDebugTrace
            // 
            this.chkDebugTrace.AutoSize = true;
            this.chkDebugTrace.ForeColor = System.Drawing.SystemColors.ControlText;
            this.chkDebugTrace.Location = new System.Drawing.Point(175, 14);
            this.chkDebugTrace.Name = "chkDebugTrace";
            this.chkDebugTrace.Size = new System.Drawing.Size(124, 17);
            this.chkDebugTrace.TabIndex = 4;
            this.chkDebugTrace.Text = "Trace Behavior Calls";
            this.toolTip1.SetToolTip(this.chkDebugTrace, "Enable Singular Behavior tracing -- EXTREMELY VERBOSE!!!");
            this.chkDebugTrace.UseVisualStyleBackColor = true;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.lblPoi);
            this.groupBox5.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.groupBox5.Location = new System.Drawing.Point(7, 6);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(313, 38);
            this.groupBox5.TabIndex = 0;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "BotPoi (Point of Interest)";
            this.groupBox5.Enter += new System.EventHandler(this.groupBox2_Enter);
            // 
            // lblPoi
            // 
            this.lblPoi.AutoSize = true;
            this.lblPoi.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPoi.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblPoi.Location = new System.Drawing.Point(6, 16);
            this.lblPoi.Name = "lblPoi";
            this.lblPoi.Size = new System.Drawing.Size(35, 14);
            this.lblPoi.TabIndex = 0;
            this.lblPoi.Text = "None";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.lblTargets);
            this.groupBox2.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.groupBox2.Location = new System.Drawing.Point(8, 50);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(313, 77);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Target List";
            this.groupBox2.Enter += new System.EventHandler(this.groupBox2_Enter);
            // 
            // lblTargets
            // 
            this.lblTargets.AutoSize = true;
            this.lblTargets.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTargets.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblTargets.Location = new System.Drawing.Point(6, 16);
            this.lblTargets.Name = "lblTargets";
            this.lblTargets.Size = new System.Drawing.Size(147, 14);
            this.lblTargets.TabIndex = 0;
            this.lblTargets.Text = "Target  99% @ 10 yds";
            // 
            // grpAuxTargeting
            // 
            this.grpAuxTargeting.Controls.Add(this.lblAuxTargets);
            this.grpAuxTargeting.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.grpAuxTargeting.Location = new System.Drawing.Point(8, 178);
            this.grpAuxTargeting.Name = "grpAuxTargeting";
            this.grpAuxTargeting.Size = new System.Drawing.Size(313, 93);
            this.grpAuxTargeting.TabIndex = 3;
            this.grpAuxTargeting.TabStop = false;
            this.grpAuxTargeting.Text = "Other Targeting";
            // 
            // lblAuxTargets
            // 
            this.lblAuxTargets.AutoSize = true;
            this.lblAuxTargets.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblAuxTargets.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblAuxTargets.Location = new System.Drawing.Point(6, 16);
            this.lblAuxTargets.Name = "lblAuxTargets";
            this.lblAuxTargets.Size = new System.Drawing.Size(133, 14);
            this.lblAuxTargets.TabIndex = 0;
            this.lblAuxTargets.Text = "Other Target @ ...";
            // 
            // timerTargeting
            // 
            this.timerTargeting.Interval = 333;
            this.timerTargeting.Tick += new System.EventHandler(this.timerTargeting_Tick);
            // 
            // grpFooter
            // 
            this.grpFooter.Controls.Add(this.btnDump);
            this.grpFooter.Controls.Add(this.btnLogMark);
            this.grpFooter.Controls.Add(this.btnSaveAndClose);
            this.grpFooter.Controls.Add(this.lblVersion);
            this.grpFooter.Controls.Add(this.lblBuildTime);
            this.grpFooter.Controls.Add(this.label1);
            this.grpFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.grpFooter.ForeColor = System.Drawing.SystemColors.Control;
            this.grpFooter.Location = new System.Drawing.Point(0, 423);
            this.grpFooter.Margin = new System.Windows.Forms.Padding(0);
            this.grpFooter.Name = "grpFooter";
            this.grpFooter.Padding = new System.Windows.Forms.Padding(0);
            this.grpFooter.Size = new System.Drawing.Size(347, 71);
            this.grpFooter.TabIndex = 0;
            this.grpFooter.TabStop = false;
            // 
            // btnDump
            // 
            this.btnDump.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.btnDump.Location = new System.Drawing.Point(237, 11);
            this.btnDump.Name = "btnDump";
            this.btnDump.Size = new System.Drawing.Size(23, 19);
            this.btnDump.TabIndex = 8;
            this.btnDump.Text = "Q";
            this.btnDump.UseVisualStyleBackColor = true;
            this.btnDump.Click += new System.EventHandler(this.btnDump_Click);
            // 
            // btnLogMark
            // 
            this.btnLogMark.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnLogMark.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnLogMark.Location = new System.Drawing.Point(135, 35);
            this.btnLogMark.Name = "btnLogMark";
            this.btnLogMark.Size = new System.Drawing.Size(96, 26);
            this.btnLogMark.TabIndex = 6;
            this.btnLogMark.Text = "LOGMARK!";
            this.toolTip1.SetToolTip(this.btnLogMark, "Add a LogMark to log file to simplify indicating where a problem occurred");
            this.btnLogMark.UseVisualStyleBackColor = true;
            this.btnLogMark.Click += new System.EventHandler(this.btnLogMark_Click);
            // 
            // btnSaveAndClose
            // 
            this.btnSaveAndClose.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSaveAndClose.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnSaveAndClose.Location = new System.Drawing.Point(237, 35);
            this.btnSaveAndClose.Name = "btnSaveAndClose";
            this.btnSaveAndClose.Size = new System.Drawing.Size(96, 26);
            this.btnSaveAndClose.TabIndex = 7;
            this.btnSaveAndClose.Text = "Save && Close";
            this.btnSaveAndClose.UseVisualStyleBackColor = true;
            this.btnSaveAndClose.Click += new System.EventHandler(this.btnSaveAndClose_Click);
            // 
            // lblVersion
            // 
            this.lblVersion.AutoSize = true;
            this.lblVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblVersion.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblVersion.Location = new System.Drawing.Point(8, 38);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(75, 13);
            this.lblVersion.TabIndex = 6;
            this.lblVersion.Text = "v0.1.0.0000";
            // 
            // lblBuildTime
            // 
            this.lblBuildTime.AutoSize = true;
            this.lblBuildTime.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblBuildTime.Location = new System.Drawing.Point(8, 51);
            this.lblBuildTime.Name = "lblBuildTime";
            this.lblBuildTime.Size = new System.Drawing.Size(124, 13);
            this.lblBuildTime.TabIndex = 5;
            this.lblBuildTime.Text = "####/##/## ##:##:##";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Impact", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label1.Location = new System.Drawing.Point(6, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(79, 25);
            this.label1.TabIndex = 4;
            this.label1.Text = "Singular";
            // 
            // chkShowBehaviorFlags
            // 
            this.chkShowBehaviorFlags.AutoSize = true;
            this.chkShowBehaviorFlags.ForeColor = System.Drawing.SystemColors.ControlText;
            this.chkShowBehaviorFlags.Location = new System.Drawing.Point(6, 65);
            this.chkShowBehaviorFlags.Name = "chkShowBehaviorFlags";
            this.chkShowBehaviorFlags.Size = new System.Drawing.Size(162, 17);
            this.chkShowBehaviorFlags.TabIndex = 2;
            this.chkShowBehaviorFlags.Text = "Display Behav Flag Changes";
            this.chkShowBehaviorFlags.UseVisualStyleBackColor = true;
            this.chkShowBehaviorFlags.CheckedChanged += new System.EventHandler(this.chkShowBehaviorFlags_CheckedChanged);
            // 
            // ConfigurationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(347, 494);
            this.Controls.Add(this.grpFooter);
            this.Controls.Add(this.tabControl1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConfigurationForm";
            this.ShowIcon = false;
            this.Text = "Singular Configuration";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ConfigurationForm_FormClosing);
            this.Load += new System.EventHandler(this.ConfigurationForm_Load);
            this.VisibleChanged += new System.EventHandler(this.ConfigurationForm_VisibleChanged);
            this.tabControl1.ResumeLayout(false);
            this.tabGeneral.ResumeLayout(false);
            this.tabClass.ResumeLayout(false);
            this.tabGroupHeal.ResumeLayout(false);
            this.grpHealHeader.ResumeLayout(false);
            this.grpHealHeader.PerformLayout();
            this.tabHotkeys.ResumeLayout(false);
            this.tabDebug.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.grpCapability.ResumeLayout(false);
            this.grpCapability.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.grpAuxTargeting.ResumeLayout(false);
            this.grpAuxTargeting.PerformLayout();
            this.grpFooter.ResumeLayout(false);
            this.grpFooter.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabGeneral;
        private System.Windows.Forms.PropertyGrid pgGeneral;
        private System.Windows.Forms.TabPage tabClass;
        private System.Windows.Forms.PropertyGrid pgClass;
        private System.Windows.Forms.TabPage tabDebug;
        private System.Windows.Forms.GroupBox grpAuxTargeting;
        private System.Windows.Forms.Label lblAuxTargets;
        private System.Windows.Forms.Timer timerTargeting;
        private System.Windows.Forms.TabPage tabGroupHeal;
        private System.Windows.Forms.TabPage tabHotkeys;
        private System.Windows.Forms.PropertyGrid pgHotkeys;
        private System.Windows.Forms.GroupBox grpHealHeader;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cboHealContext;
        private System.Windows.Forms.GroupBox grpFooter;
        private System.Windows.Forms.Button btnSaveAndClose;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label lblBuildTime;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PropertyGrid pgHeal;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label lblTargets;
        private System.Windows.Forms.Button btnLogMark;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Label lblPoi;
        private System.Windows.Forms.CheckBox chkDebugTrace;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.ComboBox cboForceUseOf;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label lblTankToStayNear;
        private System.Windows.Forms.CheckBox chkDebugCasting;
        private System.Windows.Forms.ComboBox cboDebugOutput;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button btnDump;
        private System.Windows.Forms.CheckBox chkTraceBuffs;
        private System.Windows.Forms.CheckBox chkDebugTraceHeal;
        private System.Windows.Forms.GroupBox grpCapability;
        private System.Windows.Forms.CheckBox chkCFMovement;
        private System.Windows.Forms.CheckBox chkCFDefDispel;
        private System.Windows.Forms.CheckBox chkCFOffDispel;
        private System.Windows.Forms.CheckBox chkCFKiting;
        private System.Windows.Forms.CheckBox chkCFSpecialAttacks;
        private System.Windows.Forms.CheckBox chkCFPetUse;
        private System.Windows.Forms.CheckBox chkCFPetSummon;
        private System.Windows.Forms.CheckBox chkCFTargeting;
        private System.Windows.Forms.CheckBox chkCFAOE;
        private System.Windows.Forms.CheckBox chkCFGapCloser;
        private System.Windows.Forms.CheckBox chkCFFacing;
        private System.Windows.Forms.CheckBox chkCFMoveBehind;
        private System.Windows.Forms.CheckBox chkCFInterrupt;
        private System.Windows.Forms.CheckBox chkCFTaunt;
        private System.Windows.Forms.CheckBox chkCFMultiMobPull;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.CheckBox chkBFVendor;
        private System.Windows.Forms.CheckBox chkBFFlightPath;
        private System.Windows.Forms.CheckBox chkBFLoot;
        private System.Windows.Forms.CheckBox chkBFRest;
        private System.Windows.Forms.CheckBox chkBFRoam;
        private System.Windows.Forms.CheckBox chkBFCombat;
        private System.Windows.Forms.CheckBox chkBFDeath;
        private System.Windows.Forms.CheckBox chkBFPull;
        private System.Windows.Forms.CheckBox chkShowBehaviorFlags;
    }
}