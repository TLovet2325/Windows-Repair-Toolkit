using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Tech_ToolKit_Pro;

namespace WindowsBackupUtility
{
    public partial class FormWinBackUp : Form
    {
        // Controls
        private TextBox txtSource;
        private TextBox txtDestination;
        private Button btnSelectSource;
        private Button btnSelectDestination;
        private Button btnStartBackup;
        private RadioButton rdbFull;
        private RadioButton rdbIncremental;
        private RadioButton rdbDifferential;

        public FormWinBackUp()
        {
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            this.Text = "Windows Backup";
            this.BackColor = Theme.Bg;
            this.ForeColor = Theme.Txt;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9f);

            // Title Label
            Label lblTitle = new Label
            {
                Text = "💾  WINDOWS BACKUP",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = Theme.Txt,
                AutoSize = true,
                Location = new Point(20, 15)
            };

            // Source Panel
            Label lblSource = new Label { Text = "Source Folder:", ForeColor = Theme.Sub, Location = new Point(20, 60), AutoSize = true };
            txtSource = new TextBox { Location = new Point(20, 80), Width = 400, BackColor = Theme.Surf, ForeColor = Theme.Txt, BorderStyle = BorderStyle.FixedSingle, ReadOnly = true };
            btnSelectSource = MakeBtn("Browse...", Theme.Blue, new Size(80, 24));
            btnSelectSource.Location = new Point(430, 78);
            btnSelectSource.Click += btnSelectSource_Click;

            // Destination Panel
            Label lblDest = new Label { Text = "Destination Folder:", ForeColor = Theme.Sub, Location = new Point(20, 120), AutoSize = true };
            txtDestination = new TextBox { Location = new Point(20, 140), Width = 400, BackColor = Theme.Surf, ForeColor = Theme.Txt, BorderStyle = BorderStyle.FixedSingle, ReadOnly = true };
            btnSelectDestination = MakeBtn("Browse...", Theme.Green, new Size(80, 24));
            btnSelectDestination.Location = new Point(430, 138);
            btnSelectDestination.Click += btnSelectDestination_Click;

            // Backup Type
            Label lblType = new Label { Text = "Backup Type:", ForeColor = Theme.Sub, Location = new Point(20, 180), AutoSize = true };
            rdbFull = new RadioButton { Text = "Full Backup", Location = new Point(20, 200), AutoSize = true, Checked = true, ForeColor = Theme.Txt };
            rdbIncremental = new RadioButton { Text = "Incremental", Location = new Point(130, 200), AutoSize = true, ForeColor = Theme.Txt };
            rdbDifferential = new RadioButton { Text = "Differential", Location = new Point(240, 200), AutoSize = true, ForeColor = Theme.Txt };

            // Start Button
            btnStartBackup = MakeBtn("▶  Start Backup", Theme.Amber, new Size(180, 34));
            btnStartBackup.Location = new Point(20, 250);
            btnStartBackup.Click += btnStartBackup_Click;

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblSource);
            this.Controls.Add(txtSource);
            this.Controls.Add(btnSelectSource);
            this.Controls.Add(lblDest);
            this.Controls.Add(txtDestination);
            this.Controls.Add(btnSelectDestination);
            this.Controls.Add(lblType);
            this.Controls.Add(rdbFull);
            this.Controls.Add(rdbIncremental);
            this.Controls.Add(rdbDifferential);
            this.Controls.Add(btnStartBackup);
        }

        private Button MakeBtn(string text, Color accent, Size sz)
        {
            var b = new Button
            {
                Text = text,
                Size = sz,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = accent,
                BackColor = Color.FromArgb(20, accent.R, accent.G, accent.B),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(60, accent.R, accent.G, accent.B);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, accent.R, accent.G, accent.B);
            return b;
        }

        private string SourcePath = "";
        private string DestinationPath = "";
        private enum BackupType { Full, Incremental, Differential }

        private void btnSelectSource_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    SourcePath = fbd.SelectedPath;
                    txtSource.Text = SourcePath;
                }
            }
        }

        private void btnSelectDestination_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    DestinationPath = fbd.SelectedPath;
                    txtDestination.Text = DestinationPath;
                }
            }
        }

        private void btnStartBackup_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(DestinationPath))
            {
                MessageBox.Show("Please select both source and destination folders.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            BackupType type = BackupType.Full;

            if (rdbIncremental.Checked)
                type = BackupType.Incremental;
            else if (rdbDifferential.Checked)
                type = BackupType.Differential;

            btnStartBackup.Enabled = false;
            btnStartBackup.Text = "Backing up...";

            // Do simple async backup to avoid freezing UI
            System.Threading.Tasks.Task.Run(() =>
            {
                PerformBackup(SourcePath, DestinationPath, type);
                this.Invoke(new Action(() => {
                    btnStartBackup.Enabled = true;
                    btnStartBackup.Text = "▶  Start Backup";
                    MessageBox.Show("Backup Completed Successfully!", "Backup Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
            });
        }

        private void PerformBackup(string sourceDir, string destDir, BackupType type)
        {
            string logPath = Path.Combine(destDir, $"BackupLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            using (StreamWriter log = new StreamWriter(logPath))
            {
                log.WriteLine($"Backup Type: {type}");
                log.WriteLine($"Start Time: {DateTime.Now}");
                log.WriteLine($"Source: {sourceDir}");
                log.WriteLine($"Destination: {destDir}");
                log.WriteLine(new string('-', 60));

                var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                DateTime? lastBackupTime = GetLastBackupTime(destDir);

                foreach (string file in files)
                {
                    try
                    {
                        string relativePath = file.Substring(sourceDir.Length + 1);
                        string destFile = Path.Combine(destDir, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                        bool shouldCopy = false;

                        switch (type)
                        {
                            case BackupType.Full:
                                shouldCopy = true;
                                break;
                            case BackupType.Incremental:
                            case BackupType.Differential:
                                shouldCopy = lastBackupTime == null || File.GetLastWriteTime(file) > lastBackupTime;
                                break;
                        }

                        if (shouldCopy)
                        {
                            File.Copy(file, destFile, true);
                            log.WriteLine($"Copied: {relativePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.WriteLine($"Error copying {file}: {ex.Message}");
                    }
                }

                log.WriteLine($"End Time: {DateTime.Now}");
            }
        }

        private DateTime? GetLastBackupTime(string destDir)
        {
            var logs = Directory.GetFiles(destDir, "BackupLog_*.txt");
            if (logs.Length == 0) return null;
            return logs.Select(f => File.GetCreationTime(f)).Max();
        }
    }
}
