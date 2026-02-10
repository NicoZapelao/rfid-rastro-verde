using System;
using System.Runtime.InteropServices;

namespace RfidRastroVerde
{
    partial class Form1
    {
        /// <summary>
        /// Variável de designer necessária.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpar os recursos que estão sendo usados.
        /// </summary>
        /// <param name="disposing">true se for necessário descartar os recursos gerenciados; caso contrário, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código gerado pelo Windows Form Designer

        /// <summary>
        /// Método necessário para suporte ao Designer - não modifique 
        /// o conteúdo deste método com o editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnOpen = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.gridTags = new System.Windows.Forms.DataGridView();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.txtTrayScan = new System.Windows.Forms.TextBox();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.gridTags)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOpen
            // 
            this.btnOpen.BackColor = System.Drawing.Color.Lime;
            this.btnOpen.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnOpen.Location = new System.Drawing.Point(32, 105);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(280, 125);
            this.btnOpen.TabIndex = 0;
            this.btnOpen.Text = "Conectar Leitores";
            this.btnOpen.UseVisualStyleBackColor = false;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            // 
            // btnStop
            // 
            this.btnStop.BackColor = System.Drawing.Color.Red;
            this.btnStop.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnStop.Location = new System.Drawing.Point(32, 249);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(280, 51);
            this.btnStop.TabIndex = 4;
            this.btnStop.Text = "Encerrar Sessão";
            this.btnStop.UseVisualStyleBackColor = false;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // txtLog
            // 
            this.txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLog.Location = new System.Drawing.Point(0, 0);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(1061, 110);
            this.txtLog.TabIndex = 5;
            this.txtLog.WordWrap = false;
            // 
            // gridTags
            // 
            this.gridTags.AllowUserToAddRows = false;
            this.gridTags.AllowUserToDeleteRows = false;
            this.gridTags.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridTags.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridTags.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridTags.Location = new System.Drawing.Point(0, 0);
            this.gridTags.MultiSelect = false;
            this.gridTags.Name = "gridTags";
            this.gridTags.ReadOnly = true;
            this.gridTags.RowHeadersVisible = false;
            this.gridTags.RowHeadersWidth = 51;
            this.gridTags.RowTemplate.Height = 24;
            this.gridTags.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridTags.Size = new System.Drawing.Size(1061, 533);
            this.gridTags.TabIndex = 6;
            this.gridTags.CellContentDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridTags_CellDoubleClick);
            // 
            // btnExport
            // 
            this.btnExport.BackColor = System.Drawing.SystemColors.ControlDark;
            this.btnExport.Location = new System.Drawing.Point(32, 612);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(280, 46);
            this.btnExport.TabIndex = 7;
            this.btnExport.Text = "Exportar XML/JSON";
            this.btnExport.UseVisualStyleBackColor = false;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // btnClear
            // 
            this.btnClear.BackColor = System.Drawing.SystemColors.ControlDark;
            this.btnClear.Location = new System.Drawing.Point(32, 557);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(280, 49);
            this.btnClear.TabIndex = 8;
            this.btnClear.Text = "Limpar";
            this.btnClear.UseVisualStyleBackColor = false;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // txtTrayScan
            // 
            this.txtTrayScan.ImeMode = System.Windows.Forms.ImeMode.Off;
            this.txtTrayScan.Location = new System.Drawing.Point(32, 68);
            this.txtTrayScan.Name = "txtTrayScan";
            this.txtTrayScan.ShortcutsEnabled = false;
            this.txtTrayScan.Size = new System.Drawing.Size(280, 22);
            this.txtTrayScan.TabIndex = 9;
            // 
            // splitMain
            // 
            this.splitMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitMain.Location = new System.Drawing.Point(356, 29);
            this.splitMain.Name = "splitMain";
            this.splitMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.gridTags);
            this.splitMain.Panel1MinSize = 200;
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.txtLog);
            this.splitMain.Panel2MinSize = 80;
            this.splitMain.Size = new System.Drawing.Size(1061, 647);
            this.splitMain.SplitterDistance = 533;
            this.splitMain.TabIndex = 12;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(33, 41);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(91, 16);
            this.label1.TabIndex = 13;
            this.label1.Text = "Bandeja Ativa";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1463, 711);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.splitMain);
            this.Controls.Add(this.btnClear);
            this.Controls.Add(this.btnExport);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnOpen);
            this.Controls.Add(this.txtTrayScan);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.gridTags)).EndInit();
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            this.splitMain.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.DataGridView gridTags;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.TextBox txtTrayScan;
        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.Label label1;
    }
}

