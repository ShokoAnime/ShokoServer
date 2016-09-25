namespace JMMServer
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
            this.SuspendLayout();
            // 
            // txtMigrateInProgress
            // 
            this.txtMigrateInProgress.AutoSize = true;
            this.txtMigrateInProgress.Location = new System.Drawing.Point(40, 32);
            this.txtMigrateInProgress.Name = "txtMigrateInProgress";
            this.txtMigrateInProgress.Size = new System.Drawing.Size(366, 13);
            this.txtMigrateInProgress.TabIndex = 0;
            this.txtMigrateInProgress.Text = "Migration is in progress, this window will automatically close once completed.";
            // 
            // txtMigrateInProgress1
            // 
            this.txtMigrateInProgress1.AutoSize = true;
            this.txtMigrateInProgress1.Location = new System.Drawing.Point(40, 58);
            this.txtMigrateInProgress1.Name = "txtMigrateInProgress1";
            this.txtMigrateInProgress1.Size = new System.Drawing.Size(259, 13);
            this.txtMigrateInProgress1.TabIndex = 1;
            this.txtMigrateInProgress1.Text = "Afterwards JMM Server will restarts itself if successful.";
            // 
            // MigrationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(449, 100);
            this.Controls.Add(this.txtMigrateInProgress1);
            this.Controls.Add(this.txtMigrateInProgress);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MigrationForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Migration in progess";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label txtMigrateInProgress;
        private System.Windows.Forms.Label txtMigrateInProgress1;
    }
}