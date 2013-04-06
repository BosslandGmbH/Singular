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
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.pgGeneral = new System.Windows.Forms.PropertyGrid();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.pgClass = new System.Windows.Forms.PropertyGrid();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.pgHeal = new System.Windows.Forms.PropertyGrid();
            this.grpHealHeader = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.cboHealContext = new System.Windows.Forms.ComboBox();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.pgHotkeys = new System.Windows.Forms.PropertyGrid();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.chkUseInstanceBehaviorsWhenSolo = new System.Windows.Forms.CheckBox();
            this.ShowPlayerNames = new System.Windows.Forms.CheckBox();
            this.button1 = new System.Windows.Forms.Button();
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
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.grpHealHeader.SuspendLayout();
            this.tabPage5.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.grpFooter.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Multiline = true;
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(340, 463);
            this.tabControl1.TabIndex = 4;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.pgGeneral);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(332, 437);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "General";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // pgGeneral
            // 
            this.pgGeneral.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgGeneral.Location = new System.Drawing.Point(3, 3);
            this.pgGeneral.Name = "pgGeneral";
            this.pgGeneral.Size = new System.Drawing.Size(326, 431);
            this.pgGeneral.TabIndex = 0;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.pgClass);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(332, 352);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Class Specific";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // pgClass
            // 
            this.pgClass.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgClass.Location = new System.Drawing.Point(3, 3);
            this.pgClass.Name = "pgClass";
            this.pgClass.Size = new System.Drawing.Size(326, 346);
            this.pgClass.TabIndex = 0;
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.pgHeal);
            this.tabPage4.Controls.Add(this.grpHealHeader);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Size = new System.Drawing.Size(332, 352);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Group Healing";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // pgHeal
            // 
            this.pgHeal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgHeal.Location = new System.Drawing.Point(0, 39);
            this.pgHeal.Name = "pgHeal";
            this.pgHeal.PropertySort = System.Windows.Forms.PropertySort.NoSort;
            this.pgHeal.Size = new System.Drawing.Size(332, 313);
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
            this.cboHealContext.SelectedIndexChanged += new System.EventHandler(this.cboHealContext_SelectedIndexChanged);
            // 
            // tabPage5
            // 
            this.tabPage5.Controls.Add(this.pgHotkeys);
            this.tabPage5.Location = new System.Drawing.Point(4, 22);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage5.Size = new System.Drawing.Size(332, 352);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "Hotkeys";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // pgHotkeys
            // 
            this.pgHotkeys.Location = new System.Drawing.Point(3, 3);
            this.pgHotkeys.Name = "pgHotkeys";
            this.pgHotkeys.PropertySort = System.Windows.Forms.PropertySort.NoSort;
            this.pgHotkeys.Size = new System.Drawing.Size(323, 276);
            this.pgHotkeys.TabIndex = 1;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.chkUseInstanceBehaviorsWhenSolo);
            this.tabPage3.Controls.Add(this.ShowPlayerNames);
            this.tabPage3.Controls.Add(this.button1);
            this.tabPage3.Controls.Add(this.groupBox2);
            this.tabPage3.Controls.Add(this.groupBox1);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(332, 352);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Debugging";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // chkUseInstanceBehaviorsWhenSolo
            // 
            this.chkUseInstanceBehaviorsWhenSolo.AutoSize = true;
            this.chkUseInstanceBehaviorsWhenSolo.Location = new System.Drawing.Point(80, 255);
            this.chkUseInstanceBehaviorsWhenSolo.Name = "chkUseInstanceBehaviorsWhenSolo";
            this.chkUseInstanceBehaviorsWhenSolo.Size = new System.Drawing.Size(193, 17);
            this.chkUseInstanceBehaviorsWhenSolo.TabIndex = 2;
            this.chkUseInstanceBehaviorsWhenSolo.Text = "Use Instance Behaviors While Solo";
            this.chkUseInstanceBehaviorsWhenSolo.UseVisualStyleBackColor = true;
            this.chkUseInstanceBehaviorsWhenSolo.CheckedChanged += new System.EventHandler(this.chkUseInstanceBehaviorsWhenSolo_CheckedChanged);
            // 
            // ShowPlayerNames
            // 
            this.ShowPlayerNames.AutoSize = true;
            this.ShowPlayerNames.Location = new System.Drawing.Point(80, 232);
            this.ShowPlayerNames.Name = "ShowPlayerNames";
            this.ShowPlayerNames.Size = new System.Drawing.Size(188, 17);
            this.ShowPlayerNames.TabIndex = 2;
            this.ShowPlayerNames.Text = "Show Player Names in Log Output";
            this.ShowPlayerNames.UseVisualStyleBackColor = true;
            this.ShowPlayerNames.CheckedChanged += new System.EventHandler(this.ShowPlayerNames_CheckedChanged);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(109, 314);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(116, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "Apoc\'s Debug Button";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.lblTargets);
            this.groupBox2.Location = new System.Drawing.Point(8, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(313, 104);
            this.groupBox2.TabIndex = 0;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Target List";
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
            this.groupBox1.Location = new System.Drawing.Point(8, 122);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(313, 104);
            this.groupBox1.TabIndex = 0;
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
            this.grpFooter.Location = new System.Drawing.Point(0, 466);
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
            // ConfigurationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(340, 537);
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
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage4.ResumeLayout(false);
            this.grpHealHeader.ResumeLayout(false);
            this.grpHealHeader.PerformLayout();
            this.tabPage5.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.grpFooter.ResumeLayout(false);
            this.grpFooter.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.PropertyGrid pgGeneral;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.PropertyGrid pgClass;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label lblHealTargets;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.CheckBox ShowPlayerNames;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.TabPage tabPage5;
        private System.Windows.Forms.PropertyGrid pgHotkeys;
        private System.Windows.Forms.CheckBox chkUseInstanceBehaviorsWhenSolo;
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
    }
}