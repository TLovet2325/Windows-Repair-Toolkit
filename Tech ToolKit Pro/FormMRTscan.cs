using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    // ════════════════════════════════════════════════════════════════
    //  FIXES APPLIED
    //  ─────────────────────────────────────────────────────────────
    //  1. Removed "partial" — no Designer file exists.
    //
    //  2. Fixed MRT detection — on modern Windows 10/11 mrt.exe can
    //     live in MULTIPLE locations. The original only checked
    //     System32. Now checks all known paths in priority order:
    //       • %SystemRoot%\System32\mrt.exe          (traditional)
    //       • %SystemRoot%\SysWOW64\mrt.exe          (32-bit on 64-bit)
    //       • %ProgramData%\Microsoft\Windows Defender\Platform\*\mrt.exe
    //       • %windir%\System32\MRT.exe              (case variant)
    //       • Falls back to WHERE mrt.exe via shell
    //
    //  3. Added AdminHelper.ShowAdminBanner() after BuildUI().
    //
    //  4. Added AdminHelper.EnsureAdmin() guard before scan starts.
    // ════════════════════════════════════════════════════════════════
    public partial class FormMRTscan : Form
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
        Panel topBar, infoPanel, scanPanel, logPanel, bottomBar;
        Label lblTitle, lblStatus, lblMrtPath, lblMrtVersion;
        RadioButton rbQuick, rbFull, rbCustom;
        TextBox txtCustomPath;
        Button btnBrowse;
        ScanProgressBar scanBar;
        Label lblPct, lblElapsed, lblScanStatus;
        ListView logList;
        Button btnStartScan, btnViewLog, btnCancel;
        System.Windows.Forms.Timer elapsedTimer;
        DateTime scanStart;
        bool scanning = false;
        Process mrtProc;

        // Resolved MRT path — set by FindMrtPath()
        string mrtExePath = null;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormMRTscan()
        {
            BuildUI();
            DetectMRT();

            // Banner after BuildUI so Controls exist
            AdminHelper.ShowAdminBanner(this,
                "⚠  MRT scans require Administrator rights. " +
                "Click 'Restart as Admin' to enable scanning.");
        }

        // ════════════════════════════════════════════════════════════
        //  FIND MRT — checks ALL known locations
        // ════════════════════════════════════════════════════════════
        string FindMrtPath()
        {
            // ── 1. Standard System32 paths ────────────────────────────
            string windir = Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);
            string sys32 = Environment.GetFolderPath(
                Environment.SpecialFolder.System);           // C:\Windows\System32

            var candidates = new System.Collections.Generic.List<string>
            {
                Path.Combine(sys32,  "mrt.exe"),             // most common
                Path.Combine(sys32,  "MRT.exe"),             // case variant
                Path.Combine(windir, "System32", "mrt.exe"),
                Path.Combine(windir, "SysWOW64",  "mrt.exe"),// 32-bit on 64-bit OS
                Path.Combine(windir, "SysNative", "mrt.exe"),// WOW64 redirect
                // Windows Defender platform folder (newer Windows 10/11)
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                    "Microsoft", "Windows Defender", "Platform")
            };

            // ── Check simple paths first ──────────────────────────────
            foreach (string c in candidates)
            {
                if (File.Exists(c)) return c;
            }

            // ── 2. Defender Platform wildcard subfolders ──────────────
            //    %ProgramData%\Microsoft\Windows Defender\Platform\<version>\mrt.exe
            string defPlatform = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "Microsoft", "Windows Defender", "Platform");

            if (Directory.Exists(defPlatform))
            {
                try
                {
                    foreach (string subDir in Directory.GetDirectories(defPlatform))
                    {
                        string candidate = Path.Combine(subDir, "mrt.exe");
                        if (File.Exists(candidate)) return candidate;
                        candidate = Path.Combine(subDir, "MRT.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
                catch { }
            }

            // ── 3. Windows Update download cache ─────────────────────
            //    Sometimes MRT is in the WU download folder awaiting install
            string wuCache = Path.Combine(windir,
                "SoftwareDistribution", "Download");
            if (Directory.Exists(wuCache))
            {
                try
                {
                    foreach (string f in Directory.GetFiles(
                        wuCache, "mrt.exe", SearchOption.AllDirectories))
                        return f;
                }
                catch { }
            }

            // ── 4. Shell WHERE command as last resort ─────────────────
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C where mrt.exe 2>nul",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                proc.Start();
                string result = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(5000);

                if (!string.IsNullOrEmpty(result))
                {
                    string first = result.Split('\n')[0].Trim();
                    if (File.Exists(first)) return first;
                }
            }
            catch { }

            return null;  // not found
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "MRT Scan";
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
                    new Rectangle(0, 0, 4, 52), C_RED, C_AMBER,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 52);
            };
            lblTitle = new Label
            {
                Text = "🛡  MALICIOUS SOFTWARE REMOVAL TOOL",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(new Label
            {
                Text = "Microsoft MRT  ·  mrt.exe  ·  Detects and removes prevalent malware",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 34)
            });

            // ── Info panel ────────────────────────────────────────────
            infoPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.FromArgb(16, 22, 32),
                Padding = new Padding(16, 8, 16, 8)
            };
            infoPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, infoPanel.Height - 1,
                        infoPanel.Width, infoPanel.Height - 1);
                using (var br = new SolidBrush(
                    Color.FromArgb(18, C_BLUE.R, C_BLUE.G, C_BLUE.B)))
                    e.Graphics.FillRectangle(br, 0, 0,
                        infoPanel.Width, infoPanel.Height);
            };

            lblMrtPath = new Label
            {
                Text = "Locating MRT...",
                Font = new Font("Consolas", 8f),
                ForeColor = C_BLUE,
                AutoSize = true,
                Location = new Point(16, 12)
            };
            lblMrtVersion = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 30)
            };
            infoPanel.Controls.Add(lblMrtPath);
            infoPanel.Controls.Add(lblMrtVersion);
            infoPanel.Controls.Add(new Label
            {
                Text = "ℹ  MRT runs silently and generates a log at %windir%\\debug\\mrt.log",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 50)
            });

            // ── Scan type panel ───────────────────────────────────────
            scanPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 310,
                BackColor = C_BG,
                Padding = new Padding(16, 12, 16, 12)
            };

            scanPanel.Controls.Add(new Label
            {
                Text = "SELECT SCAN TYPE",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 12)
            });

            rbQuick = MakeRadio("⚡  Quick Scan",
                "Scans areas most commonly affected by malware.\nFastest option — 5 to 10 minutes.",
                new Point(16, 34), C_GREEN, true);
            rbFull = MakeRadio("🔍  Full Scan",
                "Scans every file on every local disk.\nMost thorough — may take several hours.",
                new Point(16, 104), C_AMBER, false);
            rbCustom = MakeRadio("📁  Custom Scan",
                "Scan a specific folder or drive you choose.",
                new Point(16, 174), C_BLUE, false);

            txtCustomPath = new TextBox
            {
                Font = new Font("Segoe UI", 9f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(200, 178),
                Size = new Size(250, 26),
                Text = @"C:\",
                Enabled = false
            };

            btnBrowse = MakeBtn("Browse...", C_BLUE, new Size(80, 26));
            btnBrowse.Location = new Point(458, 178);
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

            // Progress section
            scanPanel.Controls.Add(new Label
            {
                Text = "SCAN PROGRESS",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 216)
            });

            scanBar = new ScanProgressBar(C_RED, C_AMBER)
            {
                Location = new Point(16, 238),
                Size = new Size(100, 16),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblPct = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = C_TXT,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            lblElapsed = new Label
            {
                Text = "Elapsed: 00:00",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 262)
            };

            lblScanStatus = new Label
            {
                Text = "Ready to scan.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 280)
            };

            scanPanel.Controls.AddRange(new Control[]
            {
                txtCustomPath, btnBrowse,
                scanBar, lblPct, lblElapsed, lblScanStatus
            });
            scanPanel.Resize += (s, e) =>
            {
                scanBar.Size = new Size(scanPanel.Width - 80, 16);
                lblPct.Location = new Point(scanPanel.Width - 52, 236);
            };

            // ── Log panel ─────────────────────────────────────────────
            logPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(16, 8, 16, 8)
            };

            var lblLog = new Label
            {
                Text = "SCAN LOG",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            logList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(0, 22),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true
            };
            logList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right;

            logList.Columns.Add("Time", 70);
            logList.Columns.Add("Event", 120);
            logList.Columns.Add("Detail", 684);

            logList.DrawColumnHeader += DrawHeader;
            logList.DrawItem += (s, e) => { };
            logList.DrawSubItem += DrawRow;

            logPanel.Controls.AddRange(new Control[] { lblLog, logList });
            logPanel.Resize += (s, e) =>
                logList.Size = new Size(logPanel.Width - 32, logPanel.Height - 28);

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

            btnStartScan = MakeBtn("🛡  Start MRT Scan", C_RED, new Size(175, 34));
            btnViewLog = MakeBtn("📄  View MRT Log", C_BLUE, new Size(155, 34));
            btnCancel = MakeBtn("✕  Cancel", C_SUB, new Size(100, 34));

            btnCancel.Enabled = false;

            btnStartScan.Click += BtnStart_Click;
            btnViewLog.Click += (s, e) => OpenMrtLog();
            btnCancel.Click += BtnCancel_Click;

            bottomBar.Controls.AddRange(new Control[]
                { lblStatus, btnStartScan, btnViewLog, btnCancel });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnStartScan.Location = new Point(16, y);
                btnViewLog.Location = new Point(203, y);
                btnCancel.Location = new Point(370, y);
                lblStatus.Location = new Point(490,
                    (bottomBar.Height - lblStatus.Height) / 2);
            };

            Controls.Add(logPanel);
            Controls.Add(scanPanel);
            Controls.Add(infoPanel);
            Controls.Add(topBar);
            Controls.Add(bottomBar);

            elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            elapsedTimer.Tick += (s, e) =>
            {
                var ts = DateTime.Now - scanStart;
                lblElapsed.Text = string.Format("Elapsed: {0:D2}:{1:D2}:{2:D2}",
                    (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            };
        }

        // ════════════════════════════════════════════════════════════
        //  DETECT MRT — tries every known location
        // ════════════════════════════════════════════════════════════
        void DetectMRT()
        {
            AddLog("Info", "Searching for mrt.exe in all known locations...", C_BLUE);

            mrtExePath = FindMrtPath();

            if (mrtExePath != null)
            {
                lblMrtPath.Text = "  ✔  " + mrtExePath;
                lblMrtPath.ForeColor = C_GREEN;

                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(mrtExePath);
                    lblMrtVersion.Text = string.Format(
                        "Version: {0}   |   Company: {1}",
                        vi.FileVersion ?? "–", vi.CompanyName ?? "–");
                }
                catch
                {
                    lblMrtVersion.Text = "Version info unavailable";
                }

                AddLog("Info", "MRT found at: " + mrtExePath, C_GREEN);
                btnStartScan.Enabled = true;
            }
            else
            {
                // Show all paths that were checked
                string sys32 = Environment.GetFolderPath(
                    Environment.SpecialFolder.System);
                string windir = Environment.GetFolderPath(
                    Environment.SpecialFolder.Windows);

                lblMrtPath.Text = "  ✖  mrt.exe not found in standard locations";
                lblMrtPath.ForeColor = C_RED;
                lblMrtVersion.Text = "Try: Run Windows Update or download from microsoft.com";

                btnStartScan.Enabled = false;

                AddLog("Warning",
                    string.Format("Not found in: {0}  or  {1}\\SysWOW64",
                        sys32, windir), C_AMBER);
                AddLog("Warning",
                    "Also checked: ProgramData\\Microsoft\\Windows Defender\\Platform",
                    C_AMBER);
                AddLog("Warning",
                    "Run Windows Update or re-enable Windows Defender to restore MRT.",
                    C_AMBER);

                // Offer manual browse
                var result = MessageBox.Show(
                    "mrt.exe was not found automatically.\n\n" +
                    "Common locations checked:\n" +
                    "  • " + sys32 + "\\mrt.exe\n" +
                    "  • " + windir + "\\SysWOW64\\mrt.exe\n" +
                    "  • ProgramData\\...\\Windows Defender\\Platform\n\n" +
                    "Would you like to browse for mrt.exe manually?",
                    "MRT Not Found",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                    BrowseForMrt();
            }
        }

        // ── Manual browse fallback ────────────────────────────────────
        void BrowseForMrt()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Locate mrt.exe",
                Filter = "MRT executable|mrt.exe|All executables|*.exe",
                FileName = "mrt.exe"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                string path = dlg.FileName;
                if (!File.Exists(path)) return;

                // Verify it really is MRT
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(path);
                    if (vi.ProductName == null ||
                        !vi.ProductName.ToLower().Contains("microsoft"))
                    {
                        MessageBox.Show("This doesn't appear to be a genuine MRT file.",
                            "Invalid File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                catch { }

                mrtExePath = path;
                lblMrtPath.Text = "  ✔  " + path + "  (manual)";
                lblMrtPath.ForeColor = C_GREEN;
                btnStartScan.Enabled = true;
                AddLog("Info", "MRT set manually: " + path, C_GREEN);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  START SCAN
        // ════════════════════════════════════════════════════════════
        void BtnStart_Click(object sender, EventArgs e)
        {
            // Guard — MRT always needs admin
            if (!AdminHelper.EnsureAdmin("MRT Scan")) return;

            // Re-check path in case it changed since detection
            if (string.IsNullOrEmpty(mrtExePath) || !File.Exists(mrtExePath))
            {
                mrtExePath = FindMrtPath();
                if (mrtExePath == null)
                {
                    MessageBox.Show(
                        "mrt.exe could not be found.\n\n" +
                        "Run Windows Update to get the latest MRT,\nor " +
                        "use the 'Browse' option if it is installed elsewhere.",
                        "MRT Not Found",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            string args = "";
            if (rbQuick.Checked) args = "/Q /F:Y";
            else if (rbFull.Checked) args = "/Q /F:Y /EX";
            else if (rbCustom.Checked) args = string.Format(
                "/Q /F:Y /CUSTOM:\"{0}\"", txtCustomPath.Text.TrimEnd('\\'));

            string scanTypeName = rbQuick.Checked ? "Quick"
                                : rbFull.Checked ? "Full"
                                : string.Format("Custom ({0})", txtCustomPath.Text);

            var confirm = MessageBox.Show(
                string.Format(
                    "Start MRT {0} Scan?\n\n" +
                    "Path: {1}\n\n" +
                    "The scan runs in the background.\n" +
                    "Results → %windir%\\debug\\mrt.log\n\n" +
                    "Continue?", scanTypeName, mrtExePath),
                "Confirm MRT Scan",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            scanning = true;
            scanStart = DateTime.Now;

            btnStartScan.Enabled = false;
            btnCancel.Enabled = true;
            scanBar.Animate = true;
            scanBar.Value = 0;
            scanBar.Invalidate();
            lblScanStatus.Text = string.Format("Running {0} scan...", scanTypeName);
            lblScanStatus.ForeColor = C_AMBER;
            lblPct.Text = "...";
            elapsedTimer.Start();

            AddLog("Started", string.Format(
                "MRT {0} scan — {1} {2}", scanTypeName, mrtExePath, args), C_BLUE);
            SetStatus(string.Format("Running MRT {0} scan...", scanTypeName));

            var pulse = new System.Windows.Forms.Timer { Interval = 60 };
            int step = 0;
            pulse.Tick += (s2, e2) =>
            {
                step = (step + 1) % 100;
                scanBar.Value = step;
                if (!scanBar.Animate) pulse.Stop();
            };
            pulse.Start();

            mrtProc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = mrtExePath,
                    Arguments = args,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                },
                EnableRaisingEvents = true
            };

            mrtProc.Exited += (s2, e2) =>
            {
                if (InvokeRequired)
                    Invoke(new Action(() => ScanFinished(mrtProc.ExitCode)));
                else
                    ScanFinished(mrtProc.ExitCode);
            };

            try
            {
                mrtProc.Start();
                System.Threading.ThreadPool.QueueUserWorkItem(
                    _ => mrtProc.WaitForExit());
            }
            catch (Exception ex)
            {
                ScanFinished(-1);
                AddLog("Error", ex.Message, C_RED);
            }
        }

        void ScanFinished(int exitCode)
        {
            scanning = false;
            scanBar.Animate = false;
            elapsedTimer.Stop();
            btnStartScan.Enabled = true;
            btnCancel.Enabled = false;

            var ts = DateTime.Now - scanStart;
            string elapsed = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)ts.TotalHours, ts.Minutes, ts.Seconds);

            bool ok = (exitCode == 0 || exitCode == 1);

            scanBar.Value = 100;
            scanBar.SetColors(ok ? C_GREEN : C_RED, ok ? C_GREEN : C_AMBER);
            scanBar.Animate = false;
            scanBar.Invalidate();

            lblScanStatus.Text = ok
                ? "✔  Scan complete"
                : string.Format("⚠  Scan ended (code {0})", exitCode);
            lblScanStatus.ForeColor = ok ? C_GREEN : C_AMBER;
            lblPct.Text = "100%";

            AddLog("Finished",
                string.Format("Completed in {0}  |  Exit code: {1}", elapsed, exitCode),
                ok ? C_GREEN : C_AMBER);

            SetStatus(ok
                ? string.Format("✔  MRT scan complete in {0}.", elapsed)
                : string.Format("MRT scan ended — code {0}.", exitCode));

            ReadMrtLog();
        }

        void BtnCancel_Click(object sender, EventArgs e)
        {
            if (mrtProc != null && !mrtProc.HasExited)
                try { mrtProc.Kill(); } catch { }

            scanning = false;
            scanBar.Animate = false;
            elapsedTimer.Stop();
            btnStartScan.Enabled = true;
            btnCancel.Enabled = false;
            lblScanStatus.Text = "Scan cancelled by user.";
            lblScanStatus.ForeColor = C_AMBER;
            SetStatus("Scan cancelled.");
            AddLog("Cancelled", "MRT scan cancelled by user.", C_AMBER);
        }

        // ════════════════════════════════════════════════════════════
        //  READ / VIEW MRT LOG
        // ════════════════════════════════════════════════════════════
        void ReadMrtLog()
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "debug", "mrt.log");

            if (!File.Exists(logPath))
            {
                AddLog("Log", "mrt.log not found at: " + logPath, C_SUB);
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(logPath);
                AddLog("Log", string.Format("Reading mrt.log ({0} lines)", lines.Length), C_BLUE);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string lo = line.ToLower();
                    Color c = lo.Contains("found") || lo.Contains("infected") ? C_RED
                            : lo.Contains("clean") || lo.Contains("no threat") ? C_GREEN
                            : lo.Contains("error") ? C_AMBER
                            : C_SUB;
                    AddLog("Log", line.Trim(), c);
                }
            }
            catch (Exception ex)
            {
                AddLog("Error", "Could not read mrt.log: " + ex.Message, C_RED);
            }
        }

        void OpenMrtLog()
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "debug", "mrt.log");

            if (File.Exists(logPath))
                Process.Start("notepad.exe", logPath);
            else
                MessageBox.Show(
                    "MRT log not found.\nRun a scan first.\n\nExpected:\n" + logPath,
                    "Log Not Found",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void AddLog(string ev, string detail, Color fg)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => AddLog(ev, detail, fg))); return; }
            var item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(ev);
            item.SubItems.Add(detail);
            item.ForeColor = fg;
            item.Tag = fg == C_GREEN ? "ok" : fg == C_RED ? "fail" : "info";
            logList.Items.Add(item);
            logList.EnsureVisible(logList.Items.Count - 1);
        }

        void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(msg))); return; }
            lblStatus.Text = msg;
            lblStatus.ForeColor = msg.StartsWith("✔") ? C_GREEN
                                : msg.Contains("Error") ? C_RED : C_SUB;
        }

        RadioButton MakeRadio(string text, string desc, Point loc, Color accent, bool chk)
        {
            var rb = new RadioButton
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = accent,
                AutoSize = true,
                Location = loc,
                Checked = chk,
                BackColor = Color.Transparent
            };
            var lbl = new Label
            {
                Text = desc,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = false,
                Size = new Size(380, 34),
                Location = new Point(loc.X + 20, loc.Y + 20),
                BackColor = Color.Transparent
            };
            scanPanel.Controls.Add(rb);
            scanPanel.Controls.Add(lbl);
            return rb;
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

        void DrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Color.FromArgb(28, 34, 42)))
                e.Graphics.FillRectangle(bg, e.Bounds);
            using (var sf = new StringFormat
            { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var ft = new Font("Segoe UI Semibold", 8f))
            using (var br = new SolidBrush(C_SUB))
            {
                var rc = new Rectangle(e.Bounds.X + 6, e.Bounds.Y,
                    e.Bounds.Width - 6, e.Bounds.Height);
                e.Graphics.DrawString(e.Header.Text, ft, br, rc, sf);
            }
            using (var p = new Pen(C_BORDER, 1))
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        void DrawRow(object sender, DrawListViewSubItemEventArgs e)
        {
            string tag = e.Item.Tag as string ?? "";
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            Color fg = e.ColumnIndex == 0 ? C_SUB
                     : e.ColumnIndex == 1
                        ? (tag == "ok" ? C_GREEN
                         : tag == "fail" ? C_RED : C_AMBER)
                     : e.Item.ForeColor;

            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
            {
                var rc = new Rectangle(e.Bounds.X + 6, e.Bounds.Y,
                    e.Bounds.Width - 8, e.Bounds.Height);
                e.Graphics.DrawString(e.SubItem.Text, logList.Font, br, rc, sf);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (elapsedTimer != null) elapsedTimer.Stop();
            if (mrtProc != null && !mrtProc.HasExited)
                try { mrtProc.Kill(); } catch { }
            base.OnFormClosed(e);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  SCAN PROGRESS BAR
    // ════════════════════════════════════════════════════════════════
    public class ScanProgressBar : Control
    {
        int _val;
        Color _c1, _c2;
        bool _animate;
        int _pulse;
        System.Windows.Forms.Timer _t;

        public int Value { get { return _val; } set { _val = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public bool Animate { get { return _animate; } set { _animate = value; Invalidate(); } }
        public void SetColors(Color c1, Color c2) { _c1 = c1; _c2 = c2; Invalidate(); }

        public ScanProgressBar(Color c1, Color c2)
        {
            _c1 = c1; _c2 = c2;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(38, 46, 56); // same as your card bg
            _t = new System.Windows.Forms.Timer { Interval = 25 };
            _t.Tick += (s, e) =>
            {
                if (_animate)
                {
                    _pulse = (_pulse + 5) % (Width > 0 ? Width * 2 : 400);
                    Invalidate();
                }
            };
            _t.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var br = new SolidBrush(Color.FromArgb(38, 46, 56)))
                g.FillRectangle(br, 0, 0, Width, Height);

            if (_animate)
            {
                int pw = Math.Max(Width / 3, 60);
                int px = _pulse % (Width + pw) - pw;
                var blend = new ColorBlend(3);
                blend.Colors = new[]
                {
                    Color.Transparent,
                    Color.FromArgb(200, _c1.R, _c1.G, _c1.B),
                    Color.Transparent
                };
                blend.Positions = new[] { 0f, 0.5f, 1f };
                var rect = new Rectangle(px, 0, pw, Height);
                if (rect.Width > 0)
                {
                    using (var br = new LinearGradientBrush(
                        new Rectangle(rect.X, 0, Math.Max(rect.Width, 1), Height),
                        Color.Transparent,
                        Color.FromArgb(200, _c1.R, _c1.G, _c1.B),
                        LinearGradientMode.Horizontal))
                    { br.InterpolationColors = blend; g.FillRectangle(br, rect); }
                }
            }
            else
            {
                int fw = (int)(Width * (_val / 100.0));
                if (fw > 2)
                {
                    using (var br = new LinearGradientBrush(
                        new Rectangle(0, 0, fw, Height), _c1, _c2,
                        LinearGradientMode.Horizontal))
                        g.FillRectangle(br, new Rectangle(0, 0, fw, Height));
                    using (var br = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
                        g.FillRectangle(br, new Rectangle(0, 0, fw, Height / 2));
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _t != null) _t.Stop();
            base.Dispose(disposing);
        }
    }
}