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
            this.chkUseInstanceBehaviorsWhenSolo = new System.Windows.Forms.CheckBox();
            this.ShowPlayerNames = new System.Windows.Forms.CheckBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.chkDebugSpellCanCast = new System.Windows.Forms.CheckBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.lblTargets = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.lblHealTargets = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.grpFooter = new System.Windows.Forms.GroupBox();
            this.btnLogMark = new System.Windows.Forms.Button();
            this.btnSaveAndClose = new System.Windows.Forms.Button();
            this.lblVersion = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.lblPoi = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.tabGeneral.SuspendLayout();
            this.tabClass.SuspendLayout();
            this.tabGroupHeal.SuspendLayout();
            this.grpHealHeader.SuspendLayout();
            this.tabHotkeys.SuspendLayout();
            this.tabDebug.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.grpFooter.SuspendLayout();
            this.groupBox5.SuspendLayout();
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
            this.tabControl1.Size = new System.Drawing.Size(340, 368);
            this.tabControl1.TabIndex = 4;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            this.tabControl1.VisibleChanged += new System.EventHandler(this.tabControl1_VisibleChanged);
            // 
            // tabGeneral
            // 
            this.tabGeneral.Controls.Add(this.pgGeneral);
            this.tabGeneral.Location = new System.Drawing.Point(4, 22);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Padding = new System.Windows.Forms.Padding(3);
            this.tabGeneral.Size = new System.Drawing.Size(332, 342);
            this.tabGeneral.TabIndex = 0;
            this.tabGeneral.Text = "General";
            this.tabGeneral.UseVisualStyleBackColor = true;
            // 
            // pgGeneral
            // 
            this.pgGeneral.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgGeneral.Location = new System.Drawing.Point(3, 3);
            this.pgGeneral.Name = "pgGeneral";
            this.pgGeneral.Size = new System.Drawing.Size(326, 336);
            this.pgGeneral.TabIndex = 0;
            // 
            // tabClass
            // 
            this.tabClass.Controls.Add(this.pgClass);
            this.tabClass.Location = new System.Drawing.Point(4, 22);
            this.tabClass.Name = "tabClass";
            this.tabClass.Padding = new System.Windows.Forms.Padding(3);
            this.tabClass.Size = new System.Drawing.Size(332, 342);
            this.tabClass.TabIndex = 1;
            this.tabClass.Text = "Class Specific";
            this.tabClass.UseVisualStyleBackColor = true;
            // 
            // pgClass
            // 
            this.pgClass.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgClass.Location = new System.Drawing.Point(3, 3);
            this.pgClass.Name = "pgClass";
            this.pgClass.Size = new System.Drawing.Size(326, 336);
            this.pgClass.TabIndex = 0;
            // 
            // tabGroupHeal
            // 
            this.tabGroupHeal.Controls.Add(this.pgHeal);
            this.tabGroupHeal.Controls.Add(this.grpHealHeader);
            this.tabGroupHeal.Location = new System.Drawing.Point(4, 22);
            this.tabGroupHeal.Name = "tabGroupHeal";
            this.tabGroupHeal.Size = new System.Drawing.Size(332, 342);
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
            this.pgHeal.Size = new System.Drawing.Size(332, 303);
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
            this.grpHealHeader.Size = new System.Drawing.Size(332, 39);
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
            this.tabHotkeys.Size = new System.Drawing.Size(332, 342);
            this.tabHotkeys.TabIndex = 4;
            this.tabHotkeys.Text = "Hotkeys";
            this.tabHotkeys.UseVisualStyleBackColor = true;
            // 
            // pgHotkeys
            // 
            this.pgHotkeys.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgHotkeys.Location = new System.Drawing.Point(3, 3);
            this.pgHotkeys.Name = "pgHotkeys";
            this.pgHotkeys.PropertySort = System.Windows.Forms.PropertySort.NoSort;
            this.pgHotkeys.Size = new System.Drawing.Size(326, 336);
            this.pgHotkeys.TabIndex = 1;
            // 
            // tabDebug
            // 
            this.tabDebug.Controls.Add(this.groupBox4);
            this.tabDebug.Controls.Add(this.groupBox3);
            this.tabDebug.Controls.Add(this.groupBox5);
            this.tabDebug.Controls.Add(this.groupBox2);
            this.tabDebug.Controls.Add(this.groupBox1);
            this.tabDebug.Location = new System.Drawing.Point(4, 22);
            this.tabDebug.Name = "tabDebug";
            this.tabDebug.Padding = new System.Windows.Forms.Padding(3);
            this.tabDebug.Size = new System.Drawing.Size(332, 342);
            this.tabDebug.TabIndex = 2;
            this.tabDebug.Text = "Debugging";
            this.tabDebug.UseVisualStyleBackColor = true;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.chkUseInstanceBehaviorsWhenSolo);
            this.groupBox4.Controls.Add(this.ShowPlayerNames);
            this.groupBox4.Location = new System.Drawing.Point(165, 262);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(156, 74);
            this.groupBox4.TabIndex = 9;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Temporary Settings";
            // 
            // chkUseInstanceBehaviorsWhenSolo
            // 
            this.chkUseInstanceBehaviorsWhenSolo.AutoSize = true;
            this.chkUseInstanceBehaviorsWhenSolo.Location = new System.Drawing.Point(6, 43);
            this.chkUseInstanceBehaviorsWhenSolo.Name = "chkUseInstanceBehaviorsWhenSolo";
            this.chkUseInstanceBehaviorsWhenSolo.Size = new System.Drawing.Size(147, 17);
            this.chkUseInstanceBehaviorsWhenSolo.TabIndex = 9;
            this.chkUseInstanceBehaviorsWhenSolo.Text = "Force Instance Behaviors";
            this.chkUseInstanceBehaviorsWhenSolo.UseVisualStyleBackColor = true;
            // 
            // ShowPlayerNames
            // 
            this.ShowPlayerNames.AutoSize = true;
            this.ShowPlayerNames.Location = new System.Drawing.Point(6, 20);
            this.ShowPlayerNames.Name = "ShowPlayerNames";
            this.ShowPlayerNames.Size = new System.Drawing.Size(121, 17);
            this.ShowPlayerNames.TabIndex = 8;
            this.ShowPlayerNames.Text = "Show Player Names";
            this.ShowPlayerNames.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.chkDebugSpellCanCast);
            this.groupBox3.Location = new System.Drawing.Point(8, 262);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(151, 74);
            this.groupBox3.TabIndex = 8;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Debug Flags";
            // 
            // chkDebugSpellCanCast
            // 
            this.chkDebugSpellCanCast.AutoSize = true;
            this.chkDebugSpellCanCast.Location = new System.Drawing.Point(6, 20);
            this.chkDebugSpellCanCast.Name = "chkDebugSpellCanCast";
            this.chkDebugSpellCanCast.Size = new System.Drawing.Size(127, 17);
            this.chkDebugSpellCanCast.TabIndex = 3;
            this.chkDebugSpellCanCast.Text = "Debug Spell.CanCast";
            this.chkDebugSpellCanCast.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.lblTargets);
            this.groupBox2.Location = new System.Drawing.Point(8, 50);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(313, 87);
            this.groupBox2.TabIndex = 0;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Target List";
            this.groupBox2.Enter += new System.EventHandler(this.groupBox2_Enter);
            // 
            // lblTargets
            // 
            this.lblTargets.AutoSize = true;
            this.lblTargets.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTargets.Location = new System.Drawing.Point(6, 16);
            this.lblTargets.Name = "lblTargets";
            this.lblTargets.Size = new System.Drawing.Size(0, 14);
            this.lblTargets.TabIndex = 0;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.lblHealTargets);
            this.groupBox1.Location = new System.Drawing.Point(8, 143);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(313, 104);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Heal Targeting";
            // 
            // lblHealTargets
            // 
            this.lblHealTargets.AutoSize = true;
            this.lblHealTargets.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblHealTargets.Location = new System.Drawing.Point(6, 16);
            this.lblHealTargets.Name = "lblHealTargets";
            this.lblHealTargets.Size = new System.Drawing.Size(0, 14);
            this.lblHealTargets.TabIndex = 0;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 250;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // grpFooter
            // 
            this.grpFooter.Controls.Add(this.btnLogMark);
            this.grpFooter.Controls.Add(this.btnSaveAndClose);
            this.grpFooter.Controls.Add(this.lblVersion);
            this.grpFooter.Controls.Add(this.label2);
            this.grpFooter.Controls.Add(this.label1);
            this.grpFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.grpFooter.ForeColor = System.Drawing.SystemColors.Control;
            this.grpFooter.Location = new System.Drawing.Point(0, 371);
            this.grpFooter.Margin = new System.Windows.Forms.Padding(0);
            this.grpFooter.Name = "grpFooter";
            this.grpFooter.Padding = new System.Windows.Forms.Padding(0);
            this.grpFooter.Size = new System.Drawing.Size(340, 71);
            this.grpFooter.TabIndex = 5;
            this.grpFooter.TabStop = false;
            // 
            // btnLogMark
            // 
            this.btnLogMark.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnLogMark.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnLogMark.Location = new System.Drawing.Point(132, 33);
            this.btnLogMark.Name = "btnLogMark";
            this.btnLogMark.Size = new System.Drawing.Size(96, 23);
            this.btnLogMark.TabIndex = 7;
            this.btnLogMark.Text = "LOGMARK!";
            this.toolTip1.SetToolTip(this.btnLogMark, "Add a LogMark to log file to simplify indicating where a problem occurred");
            this.btnLogMark.UseVisualStyleBackColor = true;
            this.btnLogMark.Click += new System.EventHandler(this.btnLogMark_Click);
            // 
            // btnSaveAndClose
            // 
            this.btnSaveAndClose.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSaveAndClose.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnSaveAndClose.Location = new System.Drawing.Point(234, 33);
            this.btnSaveAndClose.Name = "btnSaveAndClose";
            this.btnSaveAndClose.Size = new System.Drawing.Size(96, 23);
            this.btnSaveAndClose.TabIndex = 7;
            this.btnSaveAndClose.Text = "Save && Close";
            this.btnSaveAndClose.UseVisualStyleBackColor = true;
            this.btnSaveAndClose.Click += new System.EventHandler(this.btnSaveAndClose_Click);
            // 
            // lblVersion
            // 
            this.lblVersion.AutoSize = true;
            this.lblVersion.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lblVersion.Location = new System.Drawing.Point(8, 51);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(46, 13);
            this.lblVersion.TabIndex = 6;
            this.lblVersion.Text = "v0.1.0.0";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label2.Location = new System.Drawing.Point(8, 38);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(92, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Community Driven";
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
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.lblPoi);
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
            this.lblPoi.Location = new System.Drawing.Point(6, 16);
            this.lblPoi.Name = "lblPoi";
            this.lblPoi.Size = new System.Drawing.Size(0, 14);
            this.lblPoi.TabIndex = 0;
            // 
            // ConfigurationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(340, 442);
            this.Controls.Add(this.grpFooter);
            this.Controls.Add(this.tabControl1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConfigurationForm";
            this.ShowIcon = false;
            this.Text = "Singular Configuration";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ConfigurationForm_FormClosing);
            this.Load += new System.EventHandler(this.ConfigurationForm_Load);
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
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.grpFooter.ResumeLayout(false);
            this.grpFooter.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabGeneral;
        private System.Windows.Forms.PropertyGrid pgGeneral;
        private System.Windows.Forms.TabPage tabClass;
        private System.Windows.Forms.PropertyGrid pgClass;
        private System.Windows.Forms.TabPage tabDebug;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label lblHealTargets;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.TabPage tabGroupHeal;
        private System.Windows.Forms.TabPage tabHotkeys;
        private System.Windows.Forms.PropertyGrid pgHotkeys;
        private System.Windows.Forms.GroupBox grpHealHeader;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cboHealContext;
        private System.Windows.Forms.GroupBox grpFooter;
        private System.Windows.Forms.Button btnSaveAndClose;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PropertyGrid pgHeal;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label lblTargets;
        private System.Windows.Forms.Button btnLogMark;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.CheckBox chkDebugSpellCanCast;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.CheckBox chkUseInstanceBehaviorsWhenSolo;
        private System.Windows.Forms.CheckBox ShowPlayerNames;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Label lblPoi;
    }
}