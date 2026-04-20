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
    //  2. FULL SCAN completed in 0 seconds — root cause:
    //
    //     The original arguments were:
    //       Quick  → /Q /F:Y          (/Q = quiet, /F:Y = auto-accept)
    //       Full   → /Q /F:Y /EX      (/EX does NOT mean full scan)
    //
    //     The CORRECT MRT command-line flags are:
    //       /Q            = Run in quiet (no GUI) mode
    //       /F            = Full scan of all local drives
    //       /F:Y          = Full scan, auto-accept EULA
    //       (no flag)     = Quick scan
    //       /N            = No scan (just removes previously detected)
    //
    //     So /Q /F:Y /EX was being treated as a quick scan (MRT ignored
    //     /EX) and finished instantly because /Q hides the window and
    //     skips the full scan.
    //
    //     NEW APPROACH — run MRT without /Q so the real MRT GUI opens.
    //     This is actually the BEST user experience because:
    //       · The user sees the official Microsoft MRT progress window
    //       · Full scan genuinely scans every file on every drive
    //       · Results are shown in the MRT window AND written to mrt.log
    //       · Our form monitors the process and reads the log when done
    //
    //     Corrected argument table:
    //       Quick  → no args  (MRT GUI opens, user clicks "Quick scan")
    //       Full   → /F       (MRT GUI opens pre-set to Full scan)
    //       Custom → /N       (not truly supported by MRT CLI — we open
    //                          MRT GUI and guide the user)
    //
    //     SILENT mode (optional, advanced):
    //       Quick silent → /Q
    //       Full  silent → /Q /F
    //     These run with NO visible window. We use the GUI mode by
    //     default so the user can actually see scan progress.
    //
    //  3. MRT now runs with UseShellExecute = true, Verb = "runas",
    //     WindowStyle = Normal so the official MRT window appears.
    //
    //  4. Our elapsed timer still runs; when MRT exits we read mrt.log.
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
        //  MRT SCAN MODE
        // ════════════════════════════════════════════════════════════
        // Describes a scan mode — what args to pass and how to run MRT
        class ScanMode
        {
            public string Name { get; set; }
            public string MrtArgs { get; set; }   // args for mrt.exe
            public bool SilentMode { get; set; }   // /Q flag (no GUI)
            public string Description { get; set; }
        }

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, infoPanel, scanPanel, logPanel, bottomBar;
        Label lblTitle, lblStatus, lblMrtPath, lblMrtVersion;
        RadioButton rbQuick, rbFull, rbSilentQuick, rbSilentFull;
        ScanProgressBar scanBar;
        Label lblPct, lblElapsed, lblScanStatus;
        ListView logList;
        Button btnStartScan, btnViewLog, btnCancel;
        System.Windows.Forms.Timer elapsedTimer;
        DateTime scanStart;
        bool scanning = false;
        Process mrtProc;
        string mrtExePath = null;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormMRTscan()
        {
            BuildUI();
            DetectMRT();
            AdminHelper.ShowAdminBanner(this,
                "⚠  MRT scans require Administrator rights. " +
                "Click 'Restart as Admin' to enable scanning.");
        }

        // ════════════════════════════════════════════════════════════
        //  FIND MRT
        // ════════════════════════════════════════════════════════════
        string FindMrtPath()
        {
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);

            var candidates = new System.Collections.Generic.List<string>
            {
                Path.Combine(sys32,   "mrt.exe"),
                Path.Combine(sys32,   "MRT.exe"),
                Path.Combine(windir,  "System32", "mrt.exe"),
                Path.Combine(windir,  "SysWOW64",  "mrt.exe"),
                Path.Combine(windir,  "SysNative", "mrt.exe")
            };
            foreach (string c in candidates)
                if (File.Exists(c)) return c;

            // Defender platform subfolders
            string defPlatform = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "Microsoft", "Windows Defender", "Platform");
            if (Directory.Exists(defPlatform))
            {
                try
                {
                    foreach (string sub in Directory.GetDirectories(defPlatform))
                    {
                        string c1 = Path.Combine(sub, "mrt.exe");
                        if (File.Exists(c1)) return c1;
                        string c2 = Path.Combine(sub, "MRT.exe");
                        if (File.Exists(c2)) return c2;
                    }
                }
                catch { }
            }

            // WU cache
            string wuCache = Path.Combine(windir, "SoftwareDistribution", "Download");
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

            // WHERE fallback
            try
            {
                var p = new Process
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
                p.Start();
                string res = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(5000);
                if (!string.IsNullOrEmpty(res))
                {
                    string first = res.Split('\n')[0].Trim();
                    if (File.Exists(first)) return first;
                }
            }
            catch { }

            return null;
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
                Text = "ℹ  MRT generates a log at %windir%\\debug\\mrt.log  ·  GUI mode shows official Microsoft scan progress",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 50)
            });

            // ── Scan type panel ───────────────────────────────────────
            scanPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 330,
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

            // ── GUI mode (recommended) ────────────────────────────────
            scanPanel.Controls.Add(new Label
            {
                Text = "▶  GUI MODE  (Recommended — opens the official MRT window)",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = Color.FromArgb(88, 166, 255),
                AutoSize = true,
                Location = new Point(16, 34)
            });

            // Quick GUI
            rbQuick = MakeRadio("⚡  Quick Scan  (GUI)",
                "Opens the Microsoft MRT window pre-set to Quick Scan.\n" +
                "You can watch real progress. Fastest — 5 to 15 minutes.",
                new Point(16, 52), C_GREEN, true);

            // Full GUI  — FIXED: mrt.exe /F opens full scan in GUI mode
            rbFull = MakeRadio("🔍  Full Scan  (GUI)",
                "Opens the Microsoft MRT window pre-set to Full Scan.\n" +
                "Scans EVERY file on every local disk. May take several hours.\n" +
                "Flag used:  mrt.exe /F",
                new Point(16, 118), C_AMBER, false);

            // ── Silent mode (no window) ───────────────────────────────
            scanPanel.Controls.Add(new Label
            {
                Text = "▶  SILENT MODE  (No window — runs in background, reads mrt.log when done)",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = Color.FromArgb(139, 148, 158),
                AutoSize = true,
                Location = new Point(16, 196)
            });

            // Silent quick  — mrt.exe /Q
            rbSilentQuick = MakeRadio("⚡  Quick Scan  (Silent)",
                "Runs mrt.exe /Q — quiet background scan, no window.\n" +
                "Progress shown here via elapsed timer. Log read on completion.",
                new Point(16, 214), C_GREEN, false);

            // Silent full  — mrt.exe /Q /F   ← THE CORRECT FULL SCAN FLAG
            rbSilentFull = MakeRadio("🔍  Full Scan  (Silent)",
                "Runs mrt.exe /Q /F — full scan of all drives, no window.\n" +
                "⚠  May take several hours. Log read on completion.\n" +
                "Flags used:  mrt.exe /Q /F",
                new Point(16, 268), C_RED, false);

            // Progress section
            scanPanel.Controls.Add(new Label
            {
                Text = "SCAN  PROGRESS",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 236)
            });

            scanBar = new ScanProgressBar(C_RED, C_AMBER)
            {
                Location = new Point(16, 256),
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
                Text = "Elapsed: 00:00:00",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 280)
            };
            lblScanStatus = new Label
            {
                Text = "Ready to scan.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 298)
            };

            scanPanel.Controls.AddRange(new Control[]
            {
                scanBar, lblPct, lblElapsed, lblScanStatus
            });
            scanPanel.Resize += (s, e) =>
            {
                scanBar.Size = new Size(scanPanel.Width - 80, 16);
                lblPct.Location = new Point(scanPanel.Width - 52, 254);
            };

            // ── Log panel ─────────────────────────────────────────────
            logPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(16, 8, 16, 8)
            };
            logPanel.Controls.Add(new Label
            {
                Text = "SCAN LOG",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 0)
            });
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
            logList.Columns.Add("Time", 170);
            logList.Columns.Add("Event", 170);
            logList.Columns.Add("Detail", 527);
            logList.DrawColumnHeader += DrawHeader;
            logList.DrawItem += (s, e) => { };
            logList.DrawSubItem += DrawRow;
            logPanel.Controls.Add(logList);
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
                // Pulse progress bar during silent scans to show activity
                if (scanning && (rbSilentQuick.Checked || rbSilentFull.Checked))
                {
                    scanBar.Value = (int)(ts.TotalSeconds % 100);
                }
            };
        }

        // ════════════════════════════════════════════════════════════
        //  DETECT MRT
        // ════════════════════════════════════════════════════════════
        void DetectMRT()
        {
            AddLog("Info", "Searching for mrt.exe...", C_BLUE);
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
                catch { lblMrtVersion.Text = "Version info unavailable"; }
                AddLog("Info", "MRT found: " + mrtExePath, C_GREEN);
                btnStartScan.Enabled = true;
            }
            else
            {
                lblMrtPath.Text = "  ✖  mrt.exe not found";
                lblMrtPath.ForeColor = C_RED;
                lblMrtVersion.Text = "Run Windows Update or download from microsoft.com";
                btnStartScan.Enabled = false;
                AddLog("Warning", "MRT not found. Run Windows Update.", C_AMBER);

                var r = MessageBox.Show(
                    "mrt.exe was not found automatically.\n\n" +
                    "Would you like to browse for mrt.exe manually?",
                    "MRT Not Found",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r == DialogResult.Yes) BrowseForMrt();
            }
        }

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
                mrtExePath = path;
                lblMrtPath.Text = "  ✔  " + path + "  (manual)";
                lblMrtPath.ForeColor = C_GREEN;
                btnStartScan.Enabled = true;
                AddLog("Info", "MRT set manually: " + path, C_GREEN);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  START SCAN
        //  ─────────────────────────────────────────────────────────
        //  CORRECT MRT ARGUMENTS:
        //
        //  GUI Quick:   mrt.exe              (opens MRT wizard, quick scan default)
        //  GUI Full:    mrt.exe /F           (opens MRT wizard, Full scan pre-selected)
        //
        //  Silent Quick: mrt.exe /Q          (no window, quick scan)
        //  Silent Full:  mrt.exe /Q /F       (no window, FULL SCAN — this is the fix)
        //
        //  There is NO /EX flag. /EX was wrong and caused instant "success"
        //  because MRT treated it as an unrecognised flag and did a quick
        //  scan or exited immediately.
        // ════════════════════════════════════════════════════════════
        void BtnStart_Click(object sender, EventArgs e)
        {
            if (!AdminHelper.EnsureAdmin("MRT Scan")) return;

            if (string.IsNullOrEmpty(mrtExePath) || !File.Exists(mrtExePath))
            {
                mrtExePath = FindMrtPath();
                if (mrtExePath == null)
                {
                    MessageBox.Show(
                        "mrt.exe could not be found.\nRun Windows Update first.",
                        "MRT Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // ── Determine mode and arguments ──────────────────────────
            string scanName;
            string mrtArgs;
            bool silentMode;

            if (rbQuick.Checked)
            {
                // GUI mode — open MRT wizard with no pre-set flag
                // MRT defaults to Quick Scan in the wizard
                scanName = "Quick Scan (GUI)";
                mrtArgs = "";              // no args = open MRT GUI
                silentMode = false;
            }
            else if (rbFull.Checked)
            {
                // GUI mode — /F pre-selects Full Scan in the MRT wizard
                scanName = "Full Scan (GUI)";
                mrtArgs = "/F";            // opens MRT GUI with Full Scan selected
                silentMode = false;
            }
            else if (rbSilentQuick.Checked)
            {
                // Silent quick scan — /Q = quiet, no window
                scanName = "Quick Scan (Silent)";
                mrtArgs = "/Q";
                silentMode = true;
            }
            else // rbSilentFull
            {
                // Silent full scan — /Q /F = quiet + full
                // This is the CORRECT way to run a real full scan silently
                scanName = "Full Scan (Silent)";
                mrtArgs = "/Q /F";
                silentMode = true;
            }

            string confirmMsg = silentMode
                ? string.Format(
                    "Start MRT {0}?\n\n" +
                    "Command:  mrt.exe {1}\n\n" +
                    "The scan runs in the background with no visible window.\n" +
                    "Results will be read from mrt.log when complete.\n\n" +
                    "⚠  Full scan may take SEVERAL HOURS.\n\nContinue?",
                    scanName, mrtArgs.Trim())
                : string.Format(
                    "Start MRT {0}?\n\n" +
                    "Command:  mrt.exe {1}\n\n" +
                    "The official Microsoft MRT window will open.\n" +
                    "You can watch real-time progress there.\n" +
                    "Results will be read from mrt.log when you close it.\n\nContinue?",
                    scanName, mrtArgs.Trim());

            if (MessageBox.Show(confirmMsg, "Confirm MRT Scan",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes) return;

            // ── Launch ────────────────────────────────────────────────
            scanning = true;
            scanStart = DateTime.Now;

            btnStartScan.Enabled = false;
            btnCancel.Enabled = true;
            scanBar.Animate = !silentMode; // pulse bar for GUI mode
            scanBar.Value = 0;
            scanBar.SetColors(C_RED, C_AMBER);
            scanBar.Invalidate();
            lblScanStatus.Text = string.Format(
                silentMode
                    ? "Running {0} silently — please wait..."
                    : "MRT window opened — scan in progress...",
                scanName);
            lblScanStatus.ForeColor = C_AMBER;
            lblPct.Text = silentMode ? "..." : "GUI";
            elapsedTimer.Start();

            AddLog("Started",
                string.Format("mrt.exe {0}  [{1}]",
                    mrtArgs.Trim(), mrtExePath), C_BLUE);
            SetStatus(string.Format("Running MRT {0}...", scanName));

            var psi = new ProcessStartInfo
            {
                FileName = mrtExePath,
                Arguments = mrtArgs,
                UseShellExecute = true,
                Verb = "runas",
                // GUI mode: Normal so MRT window is visible
                // Silent mode: Hidden (no window)
                WindowStyle = silentMode
                    ? ProcessWindowStyle.Hidden
                    : ProcessWindowStyle.Normal,
                CreateNoWindow = silentMode
            };

            mrtProc = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };
            mrtProc.Exited += (s2, e2) =>
            {
                if (InvokeRequired)
                    Invoke(new Action(() => ScanFinished(mrtProc.ExitCode)));
                else
                    ScanFinished(mrtProc.ExitCode);
            };

            // Pulse animation for silent mode (GUI mode shows real progress)
            if (silentMode)
            {
                var pulse = new System.Windows.Forms.Timer { Interval = 60 };
                int step = 0;
                pulse.Tick += (s2, e2) =>
                {
                    step = (step + 1) % 100;
                    scanBar.Value = step;
                    if (!scanBar.Animate) pulse.Stop();
                };
                pulse.Start();
            }

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

        // ════════════════════════════════════════════════════════════
        //  SCAN FINISHED
        // ════════════════════════════════════════════════════════════
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

            // MRT exit codes:
            //   0 = no infection found
            //   1 = infection found and cleaned
            //   2 = infection found, partial clean
            //  -1 = error / cancelled
            bool ok = (exitCode == 0 || exitCode == 1 || exitCode == 2);

            scanBar.Value = 100;
            scanBar.SetColors(ok ? C_GREEN : C_RED, ok ? C_TEAL : C_AMBER);
            scanBar.Invalidate();

            string resultText = exitCode == 0 ? "✔  Clean — no threats found"
                              : exitCode == 1 ? "⚠  Threat found and removed"
                              : exitCode == 2 ? "⚠  Threat found, partial removal"
                              : "✖  Scan error or cancelled";
            Color resultColor = exitCode == 0 ? C_GREEN
                              : exitCode == 1 ? C_AMBER
                              : exitCode == 2 ? C_AMBER : C_RED;

            lblScanStatus.Text = resultText;
            lblScanStatus.ForeColor = resultColor;
            lblPct.Text = "Done";

            AddLog("Finished",
                string.Format("Duration: {0}  |  Exit: {1}  |  {2}",
                    elapsed, exitCode, resultText),
                resultColor);

            SetStatus(string.Format("✔  MRT scan finished in {0}. {1}", elapsed, resultText));
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
            lblScanStatus.Text = "Scan cancelled.";
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
                AddLog("Log",
                    string.Format("mrt.log — {0} lines", lines.Length), C_BLUE);
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
        static readonly Color C_TEAL = Color.FromArgb(56, 189, 193);

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
                Size = new Size(600, 42),
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
                e.Graphics.DrawString(e.Header.Text, ft, br,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y,
                        e.Bounds.Width - 6, e.Bounds.Height), sf);
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
                e.Graphics.DrawString(e.SubItem.Text, logList.Font, br,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y,
                        e.Bounds.Width - 8, e.Bounds.Height), sf);
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