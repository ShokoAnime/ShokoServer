namespace Shoko.Server
{
    partial class MigrationForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MigrationForm));
            this.txtMigrateInProgress = new System.Windows.Forms.Label();
            this.txtMigrateInProgress1 = new System.Windows.Forms.Label();
            this.gbMigration = new System.Windows.Forms.GroupBox();
            this.gbMigration.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtMigrateInProgress
            // 
            this.txtMigrateInProgress.AutoSize = true;
            this.txtMigrateInProgress.Location = new System.Drawing.Point(167, 117);
            this.txtMigrateInProgress.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.txtMigrateInProgress.Name = "txtMigrateInProgress";
            this.txtMigrateInProgress.Size = new System.Drawing.Size(542, 20);
            this.txtMigrateInProgress.TabIndex = 0;
            this.txtMigrateInProgress.Text = "Migration is in progress, this window will automatically close once completed.";
            // 
            // txtMigrateInProgress1
            // 
            this.txtMigrateInProgress1.AutoSize = true;
            this.txtMigrateInProgress1.Location = new System.Drawing.Point(242, 157);
            this.txtMigrateInProgress1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.txtMigrateInProgress1.Name = "txtMigrateInProgress1";
            this.txtMigrateInProgress1.Size = new System.Drawing.Size(392, 20);
            this.txtMigrateInProgress1.TabIndex = 1;
            this.txtMigrateInProgress1.Text = "Afterwards Shoko Server will restart itself if successful.";
            // 
            // gbMigration
            // 
            this.gbMigration.Controls.Add(this.txtMigrateInProgress);
            this.gbMigration.Controls.Add(this.txtMigrateInProgress1);
            this.gbMigration.Location = new System.Drawing.Point(13, 13);
            this.gbMigration.Name = "gbMigration";
            this.gbMigration.Size = new System.Drawing.Size(900, 324);
            this.gbMigration.TabIndex = 2;
            this.gbMigration.TabStop = false;
            // 
            // MigrationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(925, 349);
            this.Controls.Add(this.gbMigration);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MigrationForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Migration in progess";
            this.gbMigration.ResumeLayout(false);
            this.gbMigration.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label txtMigrateInProgress;
        private System.Windows.Forms.Label txtMigrateInProgress1;
        private System.Windows.Forms.GroupBox gbMigration;
    }
}