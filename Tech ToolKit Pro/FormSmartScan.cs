using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    public partial class FormSmartScan : Form
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
        static readonly Color C_PURPLE = Color.FromArgb(188, 140, 255);
        static readonly Color C_TEAL = Color.FromArgb(56, 189, 193);
        static readonly Color C_TXT = Color.FromArgb(230, 237, 243);
        static readonly Color C_SUB = Color.FromArgb(139, 148, 158);

        // ════════════════════════════════════════════════════════════
        //  SCAN ENGINE DEFINITION
        // ════════════════════════════════════════════════════════════
        class ScanEngine
        {
            public string Title { get; set; }
            public string Subtitle { get; set; }
            public string Icon { get; set; }
            public string Desc { get; set; }
            public Color Accent { get; set; }
            public string Exe { get; set; }
            public string Args { get; set; }
            public bool NeedsAdmin { get; set; } = true;
            public bool Enabled { get; set; } = true;
            // UI refs
            public CheckBox ChkBox { get; set; }
            public Label StatLbl { get; set; }
            public ScanProgressBar PBar { get; set; }
        }

        List<ScanEngine> engines;

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, enginesPanel, overallPanel, outputPanel, bottomBar;
        Label lblTitle, lblStatus;
        Label lblOverallPct, lblOverallStatus, lblElapsed;
        ScanProgressBar overallBar;
        RichTextBox rtbOutput;
        Button btnSmartScan, btnCancel, btnClear;
        System.Windows.Forms.Timer elapsedTimer;
        DateTime scanStart;
        bool scanning = false;
        int currIdx = 0;
        int issues = 0;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormSmartScan()
        {
            InitEngines();
            BuildUI();
            AppendOutput("✔  Smart Scan ready. Select engines above and click Start.", C_GREEN);
            AppendOutput("ℹ  Smart Scan runs multiple security and health checks in sequence.", C_SUB);
            AppendOutput("ℹ  Administrator rights are required for most engines.", C_SUB);
            InitializeComponent();
            AdminHelper.ShowAdminBanner(this,
                "⚠  MRT scans require Administrator rights. " +
                "Click 'Restart as Admin' to enable scanning.");
        }

        // ════════════════════════════════════════════════════════════
        //  DEFINE ENGINES
        // ════════════════════════════════════════════════════════════
        void InitEngines()
        {
            engines = new List<ScanEngine>
            {
                new ScanEngine {
                    Title      = "Windows Defender",
                    Subtitle   = "MpCmdRun -Scan -ScanType 1",
                    Icon       = "🛡",
                    Desc       = "Quick virus & malware scan",
                    Accent     = C_BLUE,
                    Exe        = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "Windows Defender", "MpCmdRun.exe"),
                    Args       = "-Scan -ScanType 1",
                    NeedsAdmin = true
                },
                new ScanEngine {
                    Title      = "SFC — System Files",
                    Subtitle   = "sfc /scannow",
                    Icon       = "🔍",
                    Desc       = "Scan & repair protected system files",
                    Accent     = C_GREEN,
                    Exe        = "sfc",
                    Args       = "/scannow",
                    NeedsAdmin = true
                },
                new ScanEngine {
                    Title      = "DISM — Component Store",
                    Subtitle   = "DISM /CheckHealth",
                    Icon       = "🔧",
                    Desc       = "Check Windows image component store",
                    Accent     = C_PURPLE,
                    Exe        = "DISM",
                    Args       = "/Online /Cleanup-Image /CheckHealth",
                    NeedsAdmin = true
                },
                new ScanEngine {
                    Title      = "Disk Health",
                    Subtitle   = "chkdsk C: /scan",
                    Icon       = "💾",
                    Desc       = "Online disk scan for file system errors",
                    Accent     = C_AMBER,
                    Exe        = "chkdsk",
                    Args       = "C: /scan",
                    NeedsAdmin = true
                },
                new ScanEngine {
                    Title      = "Network Stack",
                    Subtitle   = "netsh winsock show catalog",
                    Icon       = "🌐",
                    Desc       = "Check WinSock and network stack integrity",
                    Accent     = C_TEAL,
                    Exe        = "netsh",
                    Args       = "winsock show catalog",
                    NeedsAdmin = false
                },
                new ScanEngine {
                    Title      = "BCD Boot Config",
                    Subtitle   = "bcdedit /enum",
                    Icon       = "🚀",
                    Desc       = "Verify boot configuration data integrity",
                    Accent     = C_RED,
                    Exe        = "bcdedit",
                    Args       = "/enum",
                    NeedsAdmin = true
                }
            };
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Smart Scan";
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
                    new Rectangle(0, 0, 4, 52), C_PURPLE, C_BLUE,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 52);
            };
            lblTitle = new Label
            {
                Text = "⚡  SMART SCAN  —  MULTI-ENGINE",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            var lblSub = new Label
            {
                Text = "Defender · SFC · DISM · Disk · Network · Boot  —  all in one pass",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 34)
            };
            topBar.Controls.AddRange(new Control[] { lblTitle, lblSub });

            // ── Engine cards ──────────────────────────────────────────
            enginesPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 136,
                BackColor = C_BG,
                Padding = new Padding(10, 8, 10, 0)
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            for (int i = 0; i < 6; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.6f));

            for (int i = 0; i < engines.Count; i++)
                grid.Controls.Add(BuildEngineCard(engines[i]), i, 0);

            enginesPanel.Controls.Add(grid);

            // ── Overall progress ──────────────────────────────────────
            overallPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = C_SURF,
                Padding = new Padding(16, 8, 16, 8)
            };
            overallPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, overallPanel.Width, 0);
                    e.Graphics.DrawLine(p, 0, overallPanel.Height - 1,
                        overallPanel.Width, overallPanel.Height - 1);
                }
            };

            var lblOvHdr = new Label
            {
                Text = "OVERALL PROGRESS",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 8)
            };

            lblOverallPct = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = C_TXT,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            overallBar = new ScanProgressBar(C_PURPLE, C_BLUE)
            {
                Location = new Point(16, 26),
                Size = new Size(100, 14),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblOverallStatus = new Label
            {
                Text = "Select engines and click Smart Scan.",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 48)
            };

            lblElapsed = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            overallPanel.Controls.AddRange(new Control[]
                { lblOvHdr, lblOverallPct, overallBar,
                  lblOverallStatus, lblElapsed });
            overallPanel.Resize += (s, e) =>
            {
                overallBar.Size = new Size(overallPanel.Width - 80, 14);
                lblOverallPct.Location = new Point(overallPanel.Width - 52, 24);
                lblElapsed.Location = new Point(overallPanel.Width - 120, 48);
            };

            // ── Output console ────────────────────────────────────────
            outputPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(16, 8, 16, 8)
            };

            var lblOut = new Label
            {
                Text = "SCAN OUTPUT",
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

            outputPanel.Controls.AddRange(new Control[] { lblOut, rtbOutput });
            outputPanel.Resize += (s, e) =>
                rtbOutput.Size = new Size(outputPanel.Width - 32, outputPanel.Height - 28);

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

            btnSmartScan = MakeBtn("⚡  Start Smart Scan", C_PURPLE, new Size(185, 34));
            btnCancel = MakeBtn("✕  Cancel", C_SUB, new Size(100, 34));
            btnClear = MakeBtn("🗑  Clear Output", C_SUB, new Size(135, 34));

            btnCancel.Enabled = false;

            btnSmartScan.Click += BtnSmartScan_Click;
            btnCancel.Click += BtnCancel_Click;
            btnClear.Click += (s, e) => rtbOutput.Clear();

            bottomBar.Controls.AddRange(new Control[]
                { lblStatus, btnSmartScan, btnCancel, btnClear });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnSmartScan.Location = new Point(16, y);
                btnCancel.Location = new Point(213, y);
                btnClear.Location = new Point(325, y);
                lblStatus.Location = new Point(480,
                    (bottomBar.Height - lblStatus.Height) / 2);
            };

            Controls.Add(outputPanel);
            Controls.Add(overallPanel);
            Controls.Add(enginesPanel);
            Controls.Add(topBar);
            Controls.Add(bottomBar);

            elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            elapsedTimer.Tick += (s, e) =>
            {
                var ts = DateTime.Now - scanStart;
                lblElapsed.Text = string.Format("{0:D2}:{1:D2}:{2:D2}",
                    (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            };
        }

        // ════════════════════════════════════════════════════════════
        //  ENGINE CARD BUILDER
        // ════════════════════════════════════════════════════════════
        Panel BuildEngineCard(ScanEngine eng)
        {
            var card = new Panel
            {
                BackColor = C_SURF,
                Margin = new Padding(4),
                Dock = DockStyle.Fill
            };
            card.Paint += (s, e) =>
            {
                using (var p = new Pen(
                    Color.FromArgb(45, eng.Accent.R, eng.Accent.G, eng.Accent.B), 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                using (var br = new SolidBrush(eng.Accent))
                    e.Graphics.FillRectangle(br, 0, 0, card.Width, 3);
            };

            var lblIcon = new Label
            {
                Text = eng.Icon,
                Font = new Font("Segoe UI", 18f),
                ForeColor = eng.Accent,
                AutoSize = true,
                Location = new Point(8, 6)
            };

            eng.ChkBox = new CheckBox
            {
                Text = eng.Title,
                Font = new Font("Segoe UI Semibold", 7.5f),
                ForeColor = eng.Accent,
                AutoSize = false,
                Size = new Size(card.Width - 40, 32),
                Location = new Point(38, 8),
                Checked = true,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblDesc = new Label
            {
                Text = eng.Desc,
                Font = new Font("Segoe UI", 7f),
                ForeColor = C_SUB,
                AutoSize = false,
                Location = new Point(8, 52),
                Size = new Size(card.Width - 16, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            eng.PBar = new ScanProgressBar(eng.Accent,
                Color.FromArgb(180, eng.Accent.R, eng.Accent.G, eng.Accent.B))
            {
                Location = new Point(8, 84),
                Size = new Size(card.Width - 16, 7),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            eng.StatLbl = new Label
            {
                Text = "Waiting",
                Font = new Font("Segoe UI", 7f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(8, 96)
            };

            card.Controls.AddRange(new Control[]
                { lblIcon, eng.ChkBox, lblDesc, eng.PBar, eng.StatLbl });

            card.Resize += (s, e) =>
            {
                eng.ChkBox.Width = card.Width - 44;
                lblDesc.Width = card.Width - 16;
                eng.PBar.Width = card.Width - 16;
            };

            return card;
        }

        // ════════════════════════════════════════════════════════════
        //  START SMART SCAN
        // ════════════════════════════════════════════════════════════
        void BtnSmartScan_Click(object sender, EventArgs e)
        {
            var selected = new List<int>();
            for (int i = 0; i < engines.Count; i++)
                if (engines[i].ChkBox.Checked) selected.Add(i);

            if (selected.Count == 0)
            {
                MessageBox.Show("Select at least one scan engine.",
                    "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                string.Format(
                    "Run Smart Scan with {0} engine(s)?\n\n" +
                    "They will run sequentially.\n" +
                    "Administrator rights required.\nContinue?",
                    selected.Count),
                "Start Smart Scan",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            scanning = true;
            issues = 0;
            currIdx = 0;
            scanStart = DateTime.Now;

            btnSmartScan.Enabled = false;
            btnCancel.Enabled = true;

            // Reset all engine cards
            foreach (var eng in engines)
            {
                eng.PBar.Animate = false;
                eng.PBar.Value = 0;
                eng.PBar.Invalidate();
                eng.StatLbl.Text = eng.ChkBox.Checked ? "Waiting..." : "Skipped";
                eng.StatLbl.ForeColor = eng.ChkBox.Checked ? C_SUB : C_BORDER;
            }

            overallBar.Value = 0;
            lblOverallPct.Text = "0%";
            overallBar.Animate = true;
            overallBar.Invalidate();

            rtbOutput.Clear();
            AppendOutput("═══ SMART SCAN STARTED ═══", C_PURPLE);
            AppendOutput(string.Format("Time: {0}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")), C_SUB);
            AppendOutput(string.Format("Engines: {0}", selected.Count), C_SUB);
            AppendOutput("", C_TXT);

            elapsedTimer.Start();
            SetStatus("Running Smart Scan...");

            RunNextEngine(selected, 0);
        }

        void RunNextEngine(List<int> selectedIdx, int pos)
        {
            if (!scanning || pos >= selectedIdx.Count)
            {
                SmartScanFinished(selectedIdx.Count);
                return;
            }

            int engIdx = selectedIdx[pos];
            var eng = engines[engIdx];

            // Update overall
            int overallPct = (int)((double)pos / selectedIdx.Count * 100);
            overallBar.Value = overallPct;
            lblOverallPct.Text = overallPct + "%";
            lblOverallStatus.Text = string.Format("Running: {0}  ({1}/{2})",
                eng.Title, pos + 1, selectedIdx.Count);

            // Update card
            eng.PBar.Animate = true;
            eng.PBar.Invalidate();
            eng.StatLbl.Text = "Running...";
            eng.StatLbl.ForeColor = C_AMBER;

            AppendOutput(string.Format("─── [{0}/{1}] {2} {3} ───",
                pos + 1, selectedIdx.Count, eng.Icon, eng.Title), eng.Accent);
            AppendOutput(string.Format("    Command: {0} {1}", eng.Exe, eng.Args), C_SUB);

            SetStatus(string.Format("Running {0}...", eng.Title));

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                int exitCode = 0;
                string output = "";

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = eng.Exe,
                        Arguments = eng.Args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    var proc = new Process { StartInfo = psi };
                    proc.Start();

                    output = proc.StandardOutput.ReadToEnd();
                    string err = proc.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(err)) output += "\n" + err;

                    proc.WaitForExit(300000); // 5 min timeout
                    exitCode = proc.ExitCode;
                }
                catch
                {
                    // Fallback: run with shell (for admin required)
                    try
                    {
                        var proc = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = eng.Exe,
                                Arguments = eng.Args,
                                UseShellExecute = true,
                                Verb = "runas",
                                WindowStyle = ProcessWindowStyle.Hidden,
                                CreateNoWindow = true
                            }
                        };
                        proc.Start();
                        proc.WaitForExit(300000);
                        exitCode = proc.ExitCode;
                        output = "(Output captured via elevated shell)";
                    }
                    catch (Exception ex2)
                    {
                        exitCode = -1;
                        output = ex2.Message;
                    }
                }

                Invoke(new Action(() =>
                    EngineFinished(engIdx, pos, selectedIdx, exitCode, output)));
            });
        }

        void EngineFinished(int engIdx, int pos,
            List<int> selectedIdx, int exitCode, string output)
        {
            var eng = engines[engIdx];
            eng.PBar.Animate = false;

            bool ok = (exitCode == 0 || exitCode == 1 || exitCode == 3010);

            // Check for issues in output
            string lo = (output ?? "").ToLower();
            bool hasIssue = lo.Contains("found") || lo.Contains("corrupt")
                         || lo.Contains("repair") || lo.Contains("error")
                         || lo.Contains("failed") || lo.Contains("threat");
            if (hasIssue) issues++;

            eng.PBar.Value = 100;
            eng.PBar.SetColors(ok ? (hasIssue ? C_AMBER : C_GREEN) : C_RED,
                ok ? (hasIssue ? C_RED : C_GREEN) : C_AMBER);
            eng.PBar.Invalidate();

            eng.StatLbl.Text = ok
                ? (hasIssue ? "⚠  Issues found" : "✔  Clean")
                : string.Format("✖  Code {0}", exitCode);
            eng.StatLbl.ForeColor = ok
                ? (hasIssue ? C_AMBER : C_GREEN)
                : C_RED;

            // Output lines
            if (!string.IsNullOrWhiteSpace(output))
            {
                foreach (string line in output.Split('\n'))
                {
                    string l = line.Trim();
                    if (string.IsNullOrEmpty(l)) continue;
                    Color c = l.ToLower().Contains("error") ? C_AMBER
                            : l.ToLower().Contains("found") ? C_RED
                            : l.ToLower().Contains("clean") ? C_GREEN
                            : l.ToLower().Contains("success") ? C_GREEN
                            : C_TXT;
                    AppendOutput("    " + l, c);
                }
            }

            AppendOutput(string.Format("    Result: {0}  (exit {1})",
                ok ? (hasIssue ? "Issues detected" : "OK") : "Failed",
                exitCode),
                ok ? (hasIssue ? C_AMBER : C_GREEN) : C_RED);
            AppendOutput("", C_TXT);

            // Continue
            RunNextEngine(selectedIdx, pos + 1);
        }

        void SmartScanFinished(int total)
        {
            scanning = false;
            elapsedTimer.Stop();

            var ts = DateTime.Now - scanStart;
            string elapsed = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)ts.TotalHours, ts.Minutes, ts.Seconds);

            overallBar.Value = 100;
            lblOverallPct.Text = "100%";
            overallBar.Animate = false;
            overallBar.SetColors(issues > 0 ? C_AMBER : C_GREEN,
                issues > 0 ? C_RED : C_BLUE);
            overallBar.Invalidate();

            lblOverallStatus.Text = issues > 0
                ? string.Format("⚠  Scan complete — {0} engine(s) found issues", issues)
                : "✔  Scan complete — All engines clean";
            lblOverallStatus.ForeColor = issues > 0 ? C_AMBER : C_GREEN;

            AppendOutput("═══ SMART SCAN COMPLETE ═══", C_PURPLE);
            AppendOutput(string.Format("Duration : {0}", elapsed), C_SUB);
            AppendOutput(string.Format("Engines  : {0}", total), C_SUB);
            AppendOutput(string.Format("Issues   : {0}", issues),
                issues > 0 ? C_AMBER : C_GREEN);

            btnSmartScan.Enabled = true;
            btnCancel.Enabled = false;

            SetStatus(issues > 0
                ? string.Format("⚠  Smart Scan done in {0} — {1} issue(s) found.", elapsed, issues)
                : string.Format("✔  Smart Scan done in {0}. System looks clean.", elapsed));

            if (issues > 0)
                MessageBox.Show(
                    string.Format(
                        "{0} engine(s) detected potential issues.\n\n" +
                        "Review the output for details.\n" +
                        "Consider running SFC or DISM individually\n" +
                        "from the Disk Maintenance section.",
                        issues),
                    "Issues Detected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void BtnCancel_Click(object sender, EventArgs e)
        {
            scanning = false;
            elapsedTimer.Stop();
            btnSmartScan.Enabled = true;
            btnCancel.Enabled = false;

            foreach (var eng in engines)
            {
                eng.PBar.Animate = false;
                if (eng.StatLbl.Text == "Running...")
                {
                    eng.StatLbl.Text = "Cancelled";
                    eng.StatLbl.ForeColor = C_AMBER;
                }
            }

            overallBar.Animate = false;
            lblOverallStatus.Text = "Scan cancelled by user.";
            lblOverallStatus.ForeColor = C_AMBER;
            AppendOutput("⚠  Smart Scan cancelled by user.", C_AMBER);
            SetStatus("Smart Scan cancelled.");
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
            scanning = false;
            if (elapsedTimer != null) elapsedTimer.Stop();
            base.OnFormClosed(e);
        }
    }
}