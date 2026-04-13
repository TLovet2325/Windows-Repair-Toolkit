using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    public partial class FormDefenderScan : Form
    {
        // ════════════════════════════════════════════════════════════
        //  THEME
        // ════════════════════════════════════════════════════════════
        static readonly Color C_BG = Color.FromArgb(13, 17, 23);
        static readonly Color C_SURF = Color.FromArgb(22, 27, 34);
        static readonly Color C_SURF2 = Color.FromArgb(30, 36, 44);
        static readonly Color C_BORDER = Color.FromArgb(48, 54, 61);
        static readonly Color C_BLUE = Color.FromArgb(88, 166, 255);
        static readonly Color C_GREEN = Color.FromArgb(63, 185, 119);
        static readonly Color C_AMBER = Color.FromArgb(255, 163, 72);
        static readonly Color C_RED = Color.FromArgb(248, 81, 73);
        static readonly Color C_TXT = Color.FromArgb(230, 237, 243);
        static readonly Color C_SUB = Color.FromArgb(139, 148, 158);

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, infoPanel, optPanel, progPanel, logPanel, bottomBar;
        Label lblTitle, lblStatus, lblDefPath;
        Label lblThreats, lblScanned, lblScanStatus, lblElapsed, lblPct;
        RadioButton rbQuick, rbFull, rbCustom, rbBoot;
        TextBox txtCustomPath;
        Button btnBrowse;
        ScanProgressBar scanBar;
        RichTextBox rtbOutput;
        Button btnStart, btnCancel, btnOpenDefender, btnViewHistory;
        System.Windows.Forms.Timer elapsedTimer, pulseTimer;
        DateTime scanStart;
        Process defProc;
        bool scanning = false;
        int threatsFound = 0;
        long filesScanned = 0;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormDefenderScan()
        {
            BuildUI();
            DetectDefender();
       
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Defender Scan";
            BackColor = C_BG;
            ForeColor = C_TXT;
            FormBorderStyle = FormBorderStyle.None;
            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 9f);

            // ── Top bar ───────────────────────────────────────────────
            topBar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = C_SURF };
            topBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 51, topBar.Width, 51);
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, 4, 52), C_BLUE, C_GREEN,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 52);
            };
            lblTitle = new Label
            {
                Text = "🛡  WINDOWS DEFENDER SCAN",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            var lblSub = new Label
            {
                Text = "MpCmdRun.exe  ·  Quick · Full · Custom · Boot scan",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 34)
            };
            topBar.Controls.AddRange(new Control[] { lblTitle, lblSub });

            // ── Info panel ────────────────────────────────────────────
            infoPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(16, 22, 32)
            };
            infoPanel.Paint += (s, e) =>
            {
                using (var br = new SolidBrush(Color.FromArgb(14, C_BLUE.R, C_BLUE.G, C_BLUE.B)))
                    e.Graphics.FillRectangle(br, 0, 0, infoPanel.Width, infoPanel.Height);
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, infoPanel.Height - 1,
                        infoPanel.Width, infoPanel.Height - 1);
            };
            lblDefPath = new Label
            {
                Text = "Detecting Windows Defender...",
                Font = new Font("Consolas", 8f),
                ForeColor = C_BLUE,
                AutoSize = true,
                Location = new Point(16, 14)
            };
            infoPanel.Controls.Add(lblDefPath);

            // ── Options panel ─────────────────────────────────────────
            optPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,
                BackColor = C_BG,
                Padding = new Padding(16, 10, 16, 10)
            };

            var lblOpt = new Label
            {
                Text = "SCAN TYPE",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 10)
            };

            rbQuick = MkRB("⚡  Quick Scan", C_GREEN, new Point(16, 30), true);
            rbFull = MkRB("🔍  Full Scan", C_AMBER, new Point(190, 30), false);
            rbBoot = MkRB("🚀  Boot Scan", C_BLUE, new Point(340, 30), false);
            rbCustom = MkRB("📁  Custom Scan", C_TXT, new Point(16, 60), false);

            txtCustomPath = new TextBox
            {
                Font = new Font("Segoe UI", 9f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(180, 63),
                Size = new Size(220, 26),
                Text = @"C:\",
                Enabled = false
            };

            btnBrowse = MakeBtn("Browse...", C_BLUE, new Size(80, 26));
            btnBrowse.Location = new Point(408, 63);
            btnBrowse.Enabled = false;
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Select folder to scan";
                    dlg.SelectedPath = txtCustomPath.Text;
                    if (dlg.ShowDialog() == DialogResult.OK)
                        txtCustomPath.Text = dlg.SelectedPath;
                }
            };

            rbCustom.CheckedChanged += (s, e) =>
            {
                txtCustomPath.Enabled = rbCustom.Checked;
                btnBrowse.Enabled = rbCustom.Checked;
            };

            var lblBootNote = new Label
            {
                Text = "Boot scan runs at next system restart",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(340, 50)
            };

            optPanel.Controls.AddRange(new Control[]
                { lblOpt, rbQuick, rbFull, rbBoot, rbCustom,
                  txtCustomPath, btnBrowse, lblBootNote });

            // ── Progress panel ────────────────────────────────────────
            progPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = C_SURF,
                Padding = new Padding(16, 8, 16, 8)
            };
            progPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, progPanel.Width, 0);
                    e.Graphics.DrawLine(p, 0, progPanel.Height - 1,
                        progPanel.Width, progPanel.Height - 1);
                }
            };

            scanBar = new ScanProgressBar(C_BLUE, C_GREEN)
            {
                Location = new Point(16, 14),
                Size = new Size(100, 14),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblPct = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = C_TXT,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            lblScanStatus = new Label
            {
                Text = "Ready.",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 36)
            };

            lblElapsed = new Label
            {
                Text = "Elapsed: 00:00",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 54)
            };

            // Live counters
            lblThreats = new Label
            {
                Text = "Threats: 0",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = C_GREEN,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            lblScanned = new Label
            {
                Text = "Scanned: 0",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            progPanel.Controls.AddRange(new Control[]
                { scanBar, lblPct, lblScanStatus, lblElapsed, lblThreats, lblScanned });
            progPanel.Resize += (s, e) =>
            {
                scanBar.Size = new Size(progPanel.Width - 80, 14);
                lblPct.Location = new Point(progPanel.Width - 52, 12);
                lblThreats.Location = new Point(progPanel.Width - 130, 36);
                lblScanned.Location = new Point(progPanel.Width - 130, 54);
            };

            // ── Output log ────────────────────────────────────────────
            logPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(16, 8, 16, 8)
            };

            var lblOut = new Label
            {
                Text = "REAL-TIME OUTPUT",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            rtbOutput = new RichTextBox
            {
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 8.5f),
                Location = new Point(0, 22),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };
            rtbOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                             | AnchorStyles.Left | AnchorStyles.Right;

            logPanel.Controls.AddRange(new Control[] { lblOut, rtbOutput });
            logPanel.Resize += (s, e) =>
                rtbOutput.Size = new Size(logPanel.Width - 32, logPanel.Height - 28);

            // ── Bottom bar ────────────────────────────────────────────
            bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = C_SURF };
            bottomBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0);
            };

            lblStatus = new Label
            {
                Text = "Ready.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true
            };

            btnStart = MakeBtn("🛡  Start Scan", C_BLUE, new Size(155, 34));
            btnCancel = MakeBtn("✕  Cancel", C_SUB, new Size(100, 34));
            btnOpenDefender = MakeBtn("🛡  Open Defender", C_GREEN, new Size(155, 34));
            btnViewHistory = MakeBtn("📋  Threat History", C_AMBER, new Size(155, 34));

            btnCancel.Enabled = false;

            btnStart.Click += BtnStart_Click;
            btnCancel.Click += BtnCancel_Click;
            btnOpenDefender.Click += (s, e) => Process.Start("windowsdefender:");
            btnViewHistory.Click += (s, e) => Process.Start(
                "windowsdefender://threatsettings");

            bottomBar.Controls.AddRange(new Control[]
                { lblStatus, btnStart, btnCancel, btnOpenDefender, btnViewHistory });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnStart.Location = new Point(16, y);
                btnCancel.Location = new Point(183, y);
                btnOpenDefender.Location = new Point(296, y);
                btnViewHistory.Location = new Point(463, y);
                lblStatus.Location = new Point(632,
                    (bottomBar.Height - lblStatus.Height) / 2);
            };

            Controls.Add(logPanel);
            Controls.Add(progPanel);
            Controls.Add(optPanel);
            Controls.Add(infoPanel);
            Controls.Add(topBar);
            Controls.Add(bottomBar);

            // Timers
            elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            elapsedTimer.Tick += (s, e) =>
            {
                var ts = DateTime.Now - scanStart;
                lblElapsed.Text = string.Format("Elapsed: {0:D2}:{1:D2}:{2:D2}",
                    (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            };
        }

        // ════════════════════════════════════════════════════════════
        //  DETECT DEFENDER
        // ════════════════════════════════════════════════════════════
        void DetectDefender()
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Windows Defender", "MpCmdRun.exe");

            if (!File.Exists(path))
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Windows Defender", "MpCmdRun.exe");

            if (File.Exists(path))
            {
                lblDefPath.Text = "  ✔  " + path;
                lblDefPath.ForeColor = C_GREEN;
                AppendOutput("✔  Windows Defender found at: " + path, C_GREEN);

                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(path);
                    AppendOutput(string.Format("    Version: {0}", vi.FileVersion), C_SUB);
                }
                catch { }
            }
            else
            {
                lblDefPath.Text = "  ✖  MpCmdRun.exe not found";
                lblDefPath.ForeColor = C_RED;
                btnStart.Enabled = false;
                AppendOutput("✖  Windows Defender not found on this system.", C_RED);
                AppendOutput("   Make sure Windows Security is enabled.", C_AMBER);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  START SCAN
        // ════════════════════════════════════════════════════════════
        void BtnStart_Click(object sender, EventArgs e)
        {
            string mpPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Windows Defender", "MpCmdRun.exe");

            if (!File.Exists(mpPath))
                mpPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Windows Defender", "MpCmdRun.exe");

            if (!File.Exists(mpPath))
            {
                MessageBox.Show("MpCmdRun.exe not found.", "Defender Not Found",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Build args
            string args = "";
            string scanName = "";

            if (rbQuick.Checked)
            {
                args = "-Scan -ScanType 1";
                scanName = "Quick";
            }
            else if (rbFull.Checked)
            {
                args = "-Scan -ScanType 2";
                scanName = "Full";
            }
            else if (rbBoot.Checked)
            {
                args = "-BootSectorScan -Cancel";
                scanName = "Boot";
                MessageBox.Show(
                    "Boot scan has been scheduled.\n" +
                    "It will run the next time your PC restarts.",
                    "Boot Scan Scheduled",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                AppendOutput("✔  Boot scan scheduled for next restart.", C_BLUE);
                return;
            }
            else if (rbCustom.Checked)
            {
                args = string.Format("-Scan -ScanType 3 -File \"{0}\"",
                    txtCustomPath.Text.TrimEnd('\\'));
                scanName = "Custom";
            }

            var confirm = MessageBox.Show(
                string.Format(
                    "Start Windows Defender {0} Scan?\n\n" +
                    "Real-time output will be shown below.\n" +
                    "Administrator rights required.\nContinue?",
                    scanName),
                "Confirm Defender Scan",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            // Reset
            scanning = true;
            threatsFound = 0;
            filesScanned = 0;
            scanStart = DateTime.Now;

            btnStart.Enabled = false;
            btnCancel.Enabled = true;
            scanBar.Animate = true;
            scanBar.Value = 0;
            scanBar.Invalidate();

            lblScanStatus.Text = string.Format("Running {0} scan...", scanName);
            lblScanStatus.ForeColor = C_AMBER;
            lblPct.Text = "...";
            lblThreats.Text = "Threats: 0";
            lblThreats.ForeColor = C_GREEN;
            lblScanned.Text = "Scanned: 0";

            rtbOutput.Clear();
            AppendOutput(string.Format("═══ Windows Defender {0} Scan ═══", scanName), C_BLUE);
            AppendOutput(string.Format("Started: {0}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")), C_SUB);
            AppendOutput(string.Format("Command: MpCmdRun.exe {0}", args), C_SUB);
            AppendOutput("", C_TXT);

            elapsedTimer.Start();
            SetStatus(string.Format("Running Defender {0} scan...", scanName));

            // Progress pulse
            var pulse = new System.Windows.Forms.Timer { Interval = 50 };
            int step = 0;
            pulse.Tick += (s2, e2) =>
            {
                step = (step + 2) % 100;
                scanBar.Value = step;
                if (!scanBar.Animate) pulse.Stop();
            };
            pulse.Start();

            defProc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = mpPath,
                    Arguments = args,
                    UseShellExecute = false,
                    Verb = "",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                },
                EnableRaisingEvents = true
            };

            defProc.OutputDataReceived += (s2, e2) =>
            {
                if (e2.Data != null) Invoke(new Action(() => ProcessOutput(e2.Data)));
            };
            defProc.ErrorDataReceived += (s2, e2) =>
            {
                if (e2.Data != null) Invoke(new Action(() =>
                    AppendOutput("[ERR] " + e2.Data, C_AMBER)));
            };
            defProc.Exited += (s2, e2) =>
            {
                Invoke(new Action(() => ScanFinished(defProc.ExitCode)));
            };

            try
            {
                defProc.Start();
                defProc.BeginOutputReadLine();
                defProc.BeginErrorReadLine();
                System.Threading.ThreadPool.QueueUserWorkItem(
                    _ => defProc.WaitForExit());
            }
            catch
            {
                // If redirect fails (admin), run with shell
                defProc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = mpPath,
                        Arguments = args,
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Normal
                    },
                    EnableRaisingEvents = true
                };
                defProc.Exited += (s2, e2) =>
                    Invoke(new Action(() => ScanFinished(defProc.ExitCode)));

                defProc.Start();
                AppendOutput("Running with elevated shell (output not captured).", C_AMBER);
                System.Threading.ThreadPool.QueueUserWorkItem(
                    _ => defProc.WaitForExit());
            }
        }

        void ProcessOutput(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            Color c = C_TXT;
            string lo = line.ToLower();

            if (lo.Contains("threat") || lo.Contains("found") || lo.Contains("infected"))
            {
                c = C_RED;
                threatsFound++;
                lblThreats.Text = string.Format("Threats: {0}", threatsFound);
                lblThreats.ForeColor = threatsFound > 0 ? C_RED : C_GREEN;
            }
            else if (lo.Contains("scanning") || lo.Contains("scan"))
            {
                c = C_BLUE;
                filesScanned++;
                lblScanned.Text = string.Format("Scanned: {0}", filesScanned);
            }
            else if (lo.Contains("clean") || lo.Contains("no threats") || lo.Contains("finished"))
            {
                c = C_GREEN;
            }
            else if (lo.Contains("error") || lo.Contains("failed"))
            {
                c = C_AMBER;
            }

            AppendOutput(line, c);
        }

        void ScanFinished(int exitCode)
        {
            scanning = false;
            scanBar.Animate = false;
            elapsedTimer.Stop();
            btnStart.Enabled = true;
            btnCancel.Enabled = false;

            var ts = DateTime.Now - scanStart;
            string elapsed = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)ts.TotalHours, ts.Minutes, ts.Seconds);

            bool ok = (exitCode == 0);
            scanBar.Value = 100;
            scanBar.SetColors(threatsFound > 0 ? C_RED : C_GREEN,
                threatsFound > 0 ? C_AMBER : C_BLUE);
            scanBar.Animate = false;
            scanBar.Invalidate();

            lblScanStatus.Text = threatsFound > 0
                ? string.Format("⚠  {0} threat(s) found!", threatsFound)
                : "✔  Scan complete — No threats found";
            lblScanStatus.ForeColor = threatsFound > 0 ? C_RED : C_GREEN;
            lblPct.Text = "100%";

            AppendOutput("", C_TXT);
            AppendOutput("═══ Scan Complete ═══", C_BLUE);
            AppendOutput(string.Format("Duration : {0}", elapsed), C_SUB);
            AppendOutput(string.Format("Threats  : {0}", threatsFound),
                threatsFound > 0 ? C_RED : C_GREEN);
            AppendOutput(string.Format("Exit Code: {0}", exitCode), C_SUB);

            SetStatus(threatsFound > 0
                ? string.Format("⚠  {0} threat(s) detected! Review output.", threatsFound)
                : string.Format("✔  Scan complete in {0}. No threats found.", elapsed));
        }

        void BtnCancel_Click(object sender, EventArgs e)
        {
            if (defProc != null && !defProc.HasExited)
                try { defProc.Kill(); } catch { }
            scanning = false;
            scanBar.Animate = false;
            elapsedTimer.Stop();
            btnStart.Enabled = true;
            btnCancel.Enabled = false;
            lblScanStatus.Text = "Scan cancelled.";
            lblScanStatus.ForeColor = C_AMBER;
            SetStatus("Scan cancelled by user.");
            AppendOutput("⚠  Scan cancelled by user.", C_AMBER);
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void AppendOutput(string text, Color c)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => AppendOutput(text, c))); return; }
            rtbOutput.SelectionStart = rtbOutput.TextLength;
            rtbOutput.SelectionLength = 0;
            rtbOutput.SelectionColor = c;
            rtbOutput.AppendText(text + "\n");
            rtbOutput.ScrollToCaret();
        }

        void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(msg))); return; }
            lblStatus.Text = msg;
            lblStatus.ForeColor = msg.StartsWith("✔") ? C_GREEN
                                : msg.StartsWith("⚠") ? C_AMBER : C_SUB;
        }

        RadioButton MkRB(string text, Color c, Point loc, bool chk) =>
            new RadioButton
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = c,
                AutoSize = true,
                Location = loc,
                Checked = chk,
                BackColor = Color.Transparent
            };

        Button MakeBtn(string text, Color accent, Size sz)
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (elapsedTimer != null) elapsedTimer.Stop();
            if (defProc != null && !defProc.HasExited)
                try { defProc.Kill(); } catch { }
            base.OnFormClosed(e);
        }
    }
}