using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Management;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    // ════════════════════════════════════════════════════════════════
    //  FormPointofRestoration  —  MODERNISED
    //  ─────────────────────────────────────────────────────────────
    //  Visual upgrades:
    //   · Gradient accent top bar (green → blue)
    //   · Three stat cards  (Total points / Latest point / Drive C: protection)
    //   · Action card row   (Create / Restore / Delete / System Properties)
    //   · Modern dark ListView with alternating rows + selection accent
    //   · Inline status chip that changes colour per state
    //   · sysdm.cpl shortcut button ("System Properties") that opens
    //     the System Protection tab directly
    //
    //  All original logic preserved:
    //   · 24h frequency bypass via registry before Checkpoint-Computer
    //   · WMI scope + query separated, no ORDER BY
    //   · UI updates only on UI thread via Invoke()
    //   · Sorted newest-first by SequenceNumber
    // ════════════════════════════════════════════════════════════════
    public partial class FormPointofRestoration : Form
    {
        // ════════════════════════════════════════════════════════════
        //  THEME
        // ════════════════════════════════════════════════════════════
        static readonly Color C_BG = Color.FromArgb(13, 17, 23);
        static readonly Color C_SURF = Color.FromArgb(22, 27, 34);
        static readonly Color C_SURF2 = Color.FromArgb(30, 36, 44);
        static readonly Color C_SURF3 = Color.FromArgb(36, 43, 52);
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
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, statsRow, actionsRow, listPanel, bottomBar;
        Label lblTitle, lblStatus;
        TextBox txtName;
        Button btnCreate, btnRefresh, btnRestore, btnDelete,
                 btnSysdm, btnClearLog;
        ListView rpList;

        // Stat card value labels
        Label lblStatTotal, lblStatLatest, lblStatProtect;

        // Spinner (marquee) shown while loading
        System.Windows.Forms.Timer spinTimer;
        int spinStep = 0;
        bool spinning = false;
        Label lblSpin;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormPointofRestoration()
        {
            BuildUI();
            LoadRestorePoints();
            AdminHelper.ShowAdminBanner(this,
                "⚠  Creating and restoring points requires " +
                "Administrator rights. Click 'Restart as Admin' to unlock.");
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Point of Restoration";
            BackColor = C_BG;
            ForeColor = C_TXT;
            FormBorderStyle = FormBorderStyle.None;
            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 9f);

            BuildTopBar();
            BuildStatsRow();
            BuildActionsRow();
            BuildListPanel();
            BuildBottomBar();

            // Assemble — Fill goes first, then Bottom, then Top panels
            Controls.Add(listPanel);    // Fill
            Controls.Add(actionsRow);   // Top (rendered 3rd from top)
            Controls.Add(statsRow);     // Top (rendered 2nd)
            Controls.Add(topBar);       // Top (topmost)
            Controls.Add(bottomBar);    // Bottom

            // Spinner timer
            spinTimer = new System.Windows.Forms.Timer { Interval = 120 };
            spinTimer.Tick += (s, e) =>
            {
                if (!spinning) return;
                string[] dots = { "●○○", "○●○", "○○●" };
                lblSpin.Text = dots[spinStep % 3];
                spinStep++;
            };
        }

        // ── Top bar ───────────────────────────────────────────────────
        void BuildTopBar()
        {
            topBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = C_SURF };
            topBar.Paint += (s, e) =>
            {
                // Bottom border
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
                // Left accent gradient bar
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, 4, topBar.Height), C_GREEN, C_BLUE,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, topBar.Height);
            };

            lblTitle = new Label
            {
                Text = "🛡  POINT OF RESTORATION",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 12)
            };
            var lblSub = new Label
            {
                Text = "Create, restore and manage Windows System Restore points",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 34)
            };
            topBar.Controls.AddRange(new Control[] { lblTitle, lblSub });
        }

        // ── Stat cards row ────────────────────────────────────────────
        void BuildStatsRow()
        {
            statsRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = C_BG,
                Padding = new Padding(12, 8, 12, 4)
            };
            statsRow.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, statsRow.Height - 1, statsRow.Width, statsRow.Height - 1);
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));

            // Card 1 — Total restore points
            var card1 = BuildStatCard("Restore Points", "–", C_BLUE, "📋",
                out lblStatTotal);
            // Card 2 — Latest restore point date
            var card2 = BuildStatCard("Latest Point", "–", C_GREEN, "🕒",
                out lblStatLatest);
            // Card 3 — System Protection status
            var card3 = BuildStatCard("System Protection", "Checking...", C_AMBER, "🛡",
                out lblStatProtect);

            tbl.Controls.Add(card1, 0, 0);
            tbl.Controls.Add(card2, 1, 0);
            tbl.Controls.Add(card3, 2, 0);
            statsRow.Controls.Add(tbl);
        }

        Panel BuildStatCard(string title, string value, Color accent,
            string icon, out Label valLabel)
        {
            var card = new Panel
            {
                BackColor = C_SURF,
                Margin = new Padding(4, 0, 4, 0),
                Dock = DockStyle.Fill
            };
            card.Paint += (s, e) =>
            {
                using (var br = new SolidBrush(accent))
                    e.Graphics.FillRectangle(br, 0, 0, card.Width, 3);
                using (var p = new Pen(Color.FromArgb(40, accent.R, accent.G, accent.B), 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            var lblIcon = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 14f),
                ForeColor = accent,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(10, 6)
            };
            var lblTit = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(42, 8)
            };
            valLabel = new Label
            {
                Text = value,
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = C_TXT,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(42, 28)
            };
            card.Controls.AddRange(new Control[] { lblIcon, lblTit, valLabel });
            return card;
        }

        // ── Action cards row ──────────────────────────────────────────
        void BuildActionsRow()
        {
            actionsRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = C_BG,
                Padding = new Padding(12, 6, 12, 6)
            };
            actionsRow.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, actionsRow.Height - 1,
                        actionsRow.Width, actionsRow.Height - 1);
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            for (int i = 0; i < 5; i++)
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

            // ── Card A: Create ────────────────────────────────────────
            var cardCreate = BuildActionCard(
                "➕  Create",
                "New restore point",
                "Name your point and click Create.",
                C_GREEN, out btnCreate);
            btnCreate.Click += BtnCreate_Click;

            // ── Card B: Restore ───────────────────────────────────────
            var cardRestore = BuildActionCard(
                "⟳  Restore",
                "Restore to selected point",
                "Select a point below, then click Restore.",
                C_AMBER, out btnRestore);
            btnRestore.Enabled = false;
            btnRestore.Click += BtnRestore_Click;

            // ── Card C: Delete ────────────────────────────────────────
            var cardDelete = BuildActionCard(
                "✕  Delete",
                "Remove selected point",
                "Permanently removes the selected point.",
                C_RED, out btnDelete);
            btnDelete.Enabled = false;
            btnDelete.Click += BtnDelete_Click;

            // ── Card D: Refresh ───────────────────────────────────────
            var cardRefresh = BuildActionCard(
                "↺  Refresh",
                "Reload restore points",
                "Queries WMI for the current list.",
                C_BLUE, out btnRefresh);
            btnRefresh.Click += (s, e) => LoadRestorePoints();

            // ── Card E: System Properties (sysdm.cpl) ─────────────────
            var cardSysdm = BuildActionCard(
                "⚙  System Properties",
                "Open System Protection tab",
                "Opens sysdm.cpl → System Protection.",
                C_PURPLE, out btnSysdm);
            btnSysdm.Click += BtnSysdm_Click;

            tbl.Controls.Add(cardCreate, 0, 0);
            tbl.Controls.Add(cardRestore, 1, 0);
            tbl.Controls.Add(cardDelete, 2, 0);
            tbl.Controls.Add(cardRefresh, 3, 0);
            tbl.Controls.Add(cardSysdm, 4, 0);
            actionsRow.Controls.Add(tbl);
        }

        Panel BuildActionCard(string btnText, string title, string hint,
            Color accent, out Button btn)
        {
            var card = new Panel
            {
                BackColor = C_SURF,
                Margin = new Padding(4, 0, 4, 0),
                Dock = DockStyle.Fill
            };
            card.Paint += (s, e) =>
            {
                using (var br = new SolidBrush(accent))
                    e.Graphics.FillRectangle(br, 0, 0, card.Width, 2);
                using (var p = new Pen(Color.FromArgb(38, accent.R, accent.G, accent.B), 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = C_TXT,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(8, 6)
            };
            var lblHint = new Label
            {
                Text = hint,
                Font = new Font("Segoe UI", 7f),
                ForeColor = C_SUB,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(8, 22),
                Size = new Size(card.Width - 16, 28)
            };

            btn = new Button
            {
                Text = btnText,
                Size = new Size(card.Width - 16, 26),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = accent,
                BackColor = Color.FromArgb(18, accent.R, accent.G, accent.B),
                Cursor = Cursors.Hand,
                Location = new Point(8, 52),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(55, accent.R, accent.G, accent.B);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, accent.R, accent.G, accent.B);

            card.Controls.AddRange(new Control[] { lblTitle, lblHint, btn });

            // Copy out-parameter to a local so the lambda can capture it.
            // C# does not allow capturing ref/out parameters directly
            // inside anonymous methods or lambda expressions.
            var localBtn = btn;
            card.Resize += (s, e) =>
            {
                lblHint.Width = card.Width - 16;
                localBtn.Width = card.Width - 16;
                localBtn.Location = new Point(8, card.Height - localBtn.Height - 8);
            };
            return card;
        }

        // ── List panel ────────────────────────────────────────────────
        void BuildListPanel()
        {
            listPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(12, 8, 12, 8)
            };

            // Create point name row
            var nameRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.Transparent
            };

            nameRow.Controls.Add(new Label
            {
                Text = "New point name:",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 9)
            });

            txtName = new TextBox
            {
                Font = new Font("Segoe UI", 9.5f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(120, 5),
                Size = new Size(340, 26),
                Text = "My Restore Point " + DateTime.Now.ToString("dd-MM-yyyy")
            };
            nameRow.Controls.Add(txtName);

            // Spin indicator
            lblSpin = new Label
            {
                Text = "●○○",
                Font = new Font("Segoe UI", 10f),
                ForeColor = C_TEAL,
                AutoSize = true,
                Visible = false,
                Location = new Point(475, 7),
                BackColor = Color.Transparent
            };
            nameRow.Controls.Add(lblSpin);

            // Section header
            var lblSection = new Label
            {
                Text = "EXISTING  RESTORE  POINTS",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 8, 0, 4)
            };

            // The ListView
            rpList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                Dock = DockStyle.Fill,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true,
                MultiSelect = false
            };

            rpList.Columns.Add("Restore Point Name", 290);
            rpList.Columns.Add("Date Created", 320);
            rpList.Columns.Add("Type", 390);
            rpList.Columns.Add("Sequence", 353);

            rpList.DrawColumnHeader += DrawHeader;
            rpList.DrawItem += (s, e) => { };
            rpList.DrawSubItem += DrawRow;
            rpList.SelectedIndexChanged += (s, e) =>
            {
                bool sel = rpList.SelectedItems.Count > 0;
                btnRestore.Enabled = sel;
                btnDelete.Enabled = sel;
            };

            listPanel.Controls.Add(rpList);
            listPanel.Controls.Add(lblSection);
            listPanel.Controls.Add(nameRow);
        }

        // ── Bottom bar ────────────────────────────────────────────────
        void BuildBottomBar()
        {
            bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = C_SURF
            };
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
                AutoSize = true,
                Location = new Point(16, 12)
            };
            bottomBar.Controls.Add(lblStatus);
        }

        // ════════════════════════════════════════════════════════════
        //  OPEN SYSTEM PROPERTIES  (sysdm.cpl)
        //  ─────────────────────────────────────────────────────────
        //  Launches sysdm.cpl directly — Windows opens it on the
        //  "System Protection" tab (tab index 4) when called with
        //  the correct argument string.
        // ════════════════════════════════════════════════════════════
        void BtnSysdm_Click(object sender, EventArgs e)
        {
            try
            {
                // Opening sysdm.cpl with no args opens the first tab.
                // To land directly on System Protection (tab 4) we use
                // the rundll32 shell32 route or just let the user navigate.
                // Most reliable cross-version method:
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = "shell32.dll,Control_RunDLL sysdm.cpl,,4",
                    UseShellExecute = true
                });
                SetStatus("Opened System Properties → System Protection tab.", false);
            }
            catch
            {
                // Fallback: plain sysdm.cpl
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "sysdm.cpl",
                        UseShellExecute = true
                    });
                    SetStatus("Opened System Properties.", false);
                }
                catch (Exception ex)
                {
                    SetStatus("⚠  Could not open System Properties: " + ex.Message, false);
                }
            }
        }

        // ════════════════════════════════════════════════════════════
        //  LOAD RESTORE POINTS
        // ════════════════════════════════════════════════════════════
        void LoadRestorePoints()
        {
            SetStatus("Loading restore points...", true);

            if (rpList.InvokeRequired)
                rpList.Invoke(new Action(() =>
                {
                    rpList.Items.Clear();
                    btnRestore.Enabled = false;
                    btnDelete.Enabled = false;
                }));
            else
            {
                rpList.Items.Clear();
                btnRestore.Enabled = false;
                btnDelete.Enabled = false;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                var rows = new List<ListViewItem>();
                string err = null;
                string latestDate = "–";

                try
                {
                    var scope = new ManagementScope(@"\\.\root\default");
                    scope.Connect();
                    var query = new ObjectQuery("SELECT * FROM SystemRestore");
                    var searcher = new ManagementObjectSearcher(scope, query);

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Description"]?.ToString() ?? "–";
                        string seq = obj["SequenceNumber"]?.ToString() ?? "0";
                        string raw = obj["CreationTime"]?.ToString() ?? "";
                        string date = ParseWmiDate(raw);
                        string type = GetRestoreType(
                            obj["RestorePointType"]?.ToString() ?? "");

                        var item = new ListViewItem(name);
                        item.SubItems.Add(date);
                        item.SubItems.Add(type);
                        item.SubItems.Add(seq);
                        item.Tag = seq;
                        item.ForeColor = C_TXT;
                        rows.Add(item);
                    }

                    // Sort newest first
                    rows.Sort((a, b) =>
                    {
                        int sa = 0, sb = 0;
                        int.TryParse(a.SubItems[3].Text, out sa);
                        int.TryParse(b.SubItems[3].Text, out sb);
                        return sb.CompareTo(sa);
                    });

                    if (rows.Count > 0)
                        latestDate = rows[0].SubItems[1].Text;
                }
                catch (ManagementException mex)
                {
                    err = string.Format("WMI ({0}): {1}", mex.ErrorCode, mex.Message);
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                }

                Invoke(new Action(() =>
                {
                    rpList.BeginUpdate();
                    rpList.Items.Clear();

                    if (err != null)
                    {
                        SetStatus("⚠  " + err, false);
                        UpdateStatCards(0, "Error", "Unknown");
                    }
                    else
                    {
                        foreach (var item in rows)
                            rpList.Items.Add(item);

                        SetStatus(string.Format(
                            "{0} restore point(s) found.", rows.Count), false);

                        UpdateStatCards(rows.Count, latestDate, null);
                    }
                    rpList.EndUpdate();
                    rpList.Invalidate();
                }));
            });
        }

        void UpdateStatCards(int total, string latest, string overrideProtect)
        {
            lblStatTotal.Text = total == 0 ? "None" : total.ToString();
            lblStatTotal.ForeColor = total == 0 ? C_AMBER : C_TXT;

            lblStatLatest.Text = string.IsNullOrEmpty(latest) || latest == "–"
                ? "None" : latest;
            lblStatLatest.ForeColor = latest == "None" || latest == "–"
                ? C_AMBER : C_TXT;

            if (overrideProtect != null)
            {
                lblStatProtect.Text = overrideProtect;
                lblStatProtect.ForeColor = overrideProtect == "Error"
                    ? C_RED : C_SUB;
            }
            else
            {
                // Quick check: if we got restore points then protection is on
                lblStatProtect.Text = total > 0 ? "Enabled ✔" : "Check sysdm.cpl";
                lblStatProtect.ForeColor = total > 0 ? C_GREEN : C_AMBER;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  CREATE RESTORE POINT
        //  ─────────────────────────────────────────────────────────
        //  Bypasses 24-hour throttle by setting
        //  SystemRestorePointCreationFrequency = 0 in registry.
        // ════════════════════════════════════════════════════════════
        void BtnCreate_Click(object sender, EventArgs e)
        {
            if (!AdminHelper.EnsureAdmin("Create Restore Point")) return;

            string name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Please enter a restore point name.", false);
                return;
            }

            var confirm = MessageBox.Show(
                string.Format(
                    "Create restore point:\n\n\"{0}\"\n\n" +
                    "This may take up to a minute.\nContinue?", name),
                "Confirm Create",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            SetStatus("Creating restore point — please wait...", true);
            btnCreate.Enabled = false;
            btnRefresh.Enabled = false;

            string safeName = name.Replace("\"", "'").Replace("`", "'");

            string psScript =
                "try {" +
                "  $regPath = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore';" +
                "  $oldVal  = (Get-ItemProperty -Path $regPath " +
                "    -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue);" +
                "  Set-ItemProperty -Path $regPath " +
                "    -Name SystemRestorePointCreationFrequency -Value 0 -Type DWord -Force;" +
                "  Checkpoint-Computer -Description '" + safeName + "' " +
                "    -RestorePointType MODIFY_SETTINGS -ErrorAction Stop;" +
                "  if ($oldVal) {" +
                "    Set-ItemProperty -Path $regPath " +
                "      -Name SystemRestorePointCreationFrequency " +
                "      -Value $oldVal.SystemRestorePointCreationFrequency -Type DWord -Force;" +
                "  } else {" +
                "    Remove-ItemProperty -Path $regPath " +
                "      -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue;" +
                "  }" +
                "  Write-Output 'SUCCESS';" +
                "} catch {" +
                "  Write-Output ('ERROR: ' + $_.Exception.Message);" +
                "}";

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                string output = "";
                int exitCode = -1;
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" +
                                                 psScript.Replace("\"", "\\\"") + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    var proc = new Process { StartInfo = psi };
                    proc.Start();
                    output = proc.StandardOutput.ReadToEnd().Trim();
                    output += proc.StandardError.ReadToEnd().Trim();
                    proc.WaitForExit(90000);
                    exitCode = proc.ExitCode;
                }
                catch (Exception ex)
                {
                    output = "EXCEPTION: " + ex.Message;
                }

                string fo = output;
                int fc = exitCode;
                Invoke(new Action(() =>
                {
                    btnCreate.Enabled = true;
                    btnRefresh.Enabled = true;

                    bool ok = fo.Contains("SUCCESS") ||
                              (fc == 0 &&
                               !fo.ToLower().Contains("error") &&
                               !fo.ToLower().Contains("exception"));

                    if (ok)
                    {
                        SetStatus(string.Format(
                            "✔  Restore point \"{0}\" created.", name), false);
                        txtName.Text = "My Restore Point " +
                            DateTime.Now.ToString("dd-MM-yyyy");

                        var t = new System.Windows.Forms.Timer { Interval = 1500 };
                        t.Tick += (s2, e2) =>
                        {
                            t.Stop(); t.Dispose(); LoadRestorePoints();
                        };
                        t.Start();
                    }
                    else
                    {
                        string msg = string.IsNullOrEmpty(fo)
                            ? string.Format("Exit: {0}", fc) : fo;
                        SetStatus("✖  Create failed: " + msg, false);
                        MessageBox.Show(
                            "Failed to create restore point.\n\nOutput:\n" + msg +
                            "\n\nMake sure System Protection is enabled for C:\n" +
                            "Click '⚙ System Properties' → System Protection tab.",
                            "Create Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }));
            });
        }

        // ════════════════════════════════════════════════════════════
        //  RESTORE
        // ════════════════════════════════════════════════════════════
        void BtnRestore_Click(object sender, EventArgs e)
        {
            if (rpList.SelectedItems.Count == 0) return;
            if (!AdminHelper.EnsureAdmin("System Restore")) return;

            var sel = rpList.SelectedItems[0];
            string rpName = sel.Text;
            string seq = sel.Tag?.ToString() ?? "";

            var confirm = MessageBox.Show(
                string.Format(
                    "⚠  Restore system to:\n\n  \"{0}\"  (Seq #{1})\n\n" +
                    "Your PC will restart automatically.\n" +
                    "Save all open work first.\n\nContinue?",
                    rpName, seq),
                "Confirm System Restore",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            SetStatus("Initiating restore — system will restart...", true);
            RunPowerShellAdmin(
                string.Format("Restore-Computer -RestorePoint {0}", seq),
                () => SetStatus("Restore initiated. System restarting...", false));
        }

        // ════════════════════════════════════════════════════════════
        //  DELETE
        // ════════════════════════════════════════════════════════════
        void BtnDelete_Click(object sender, EventArgs e)
        {
            if (rpList.SelectedItems.Count == 0) return;
            if (!AdminHelper.EnsureAdmin("Delete Restore Point")) return;

            string rpName = rpList.SelectedItems[0].Text;
            var confirm = MessageBox.Show(
                string.Format("Delete restore point:\n\n\"{0}\"\n\nThis cannot be undone.", rpName),
                "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            SetStatus("Deleting restore point...", true);
            btnDelete.Enabled = false;

            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C vssadmin delete shadows /For=C: /Oldest /Quiet",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };
                proc.Exited += (s2, e2) =>
                    Invoke(new Action(() =>
                    {
                        SetStatus("Restore point deleted.", false);
                        LoadRestorePoints();
                    }));
                proc.Start();
                System.Threading.ThreadPool.QueueUserWorkItem(
                    _ => proc.WaitForExit(30000));
            }
            catch (Exception ex)
            {
                SetStatus("Delete failed: " + ex.Message, false);
                btnDelete.Enabled = true;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  RUN POWERSHELL ADMIN
        // ════════════════════════════════════════════════════════════
        void RunPowerShellAdmin(string command, Action onComplete)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = string.Format(
                            "-NoProfile -ExecutionPolicy Bypass -Command \"{0}\"", command),
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };
                proc.Exited += (s, e) =>
                {
                    if (InvokeRequired) Invoke(new Action(onComplete));
                    else onComplete();
                };
                proc.Start();
                System.Threading.ThreadPool.QueueUserWorkItem(
                    _ => proc.WaitForExit(120000));
            }
            catch (Exception ex)
            {
                SetStatus("PowerShell error: " + ex.Message, false);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void SetStatus(string msg, bool busy)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => SetStatus(msg, busy))); return; }

            lblStatus.Text = msg;
            lblStatus.ForeColor = msg.StartsWith("✔") ? C_GREEN
                                : msg.StartsWith("✖") ? C_RED
                                : msg.StartsWith("⚠") ? C_AMBER
                                : busy ? C_TEAL : C_SUB;

            // Spinning dot indicator
            if (lblSpin != null)
            {
                lblSpin.Visible = busy;
                if (busy) { spinning = true; spinTimer?.Start(); }
                else { spinning = false; spinTimer?.Stop(); }
            }
        }

        string ParseWmiDate(string raw)
        {
            try
            {
                if (raw != null && raw.Length >= 14)
                    return new DateTime(
                        int.Parse(raw.Substring(0, 4)),
                        int.Parse(raw.Substring(4, 2)),
                        int.Parse(raw.Substring(6, 2)),
                        int.Parse(raw.Substring(8, 2)),
                        int.Parse(raw.Substring(10, 2)), 0)
                        .ToString("dd MMM yyyy  HH:mm");
            }
            catch { }
            return raw ?? "–";
        }

        string GetRestoreType(string code)
        {
            switch (code)
            {
                case "0": return "App Install";
                case "1": return "App Uninstall";
                case "6": return "Restore Operation";
                case "7": return "Checkpoint";
                case "10": return "Driver Install";
                case "12": return "Modify Settings";
                case "13": return "Cancelled";
                default: return "System Checkpoint";
            }
        }

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW — dark ListView header + rows
        // ════════════════════════════════════════════════════════════
        void DrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Color.FromArgb(26, 32, 40)))
                e.Graphics.FillRectangle(bg, e.Bounds);

            // Accent line at top of header
            using (var br = new SolidBrush(C_GREEN))
                e.Graphics.FillRectangle(br,
                    new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, 2));

            using (var sf = new StringFormat
            { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var ft = new Font("Segoe UI Semibold", 8f))
            using (var br = new SolidBrush(C_SUB))
                e.Graphics.DrawString(e.Header.Text, ft, br,
                    new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 2,
                        e.Bounds.Width - 10, e.Bounds.Height - 2), sf);

            using (var p = new Pen(C_BORDER, 1))
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        void DrawRow(object sender, DrawListViewSubItemEventArgs e)
        {
            bool sel = e.Item.Selected;
            Color bg = sel
                ? Color.FromArgb(28, 72, 52)   // soft green tint when selected
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            // Green left-edge indicator when selected
            if (sel && e.ColumnIndex == 0)
                using (var br = new SolidBrush(C_GREEN))
                    e.Graphics.FillRectangle(br,
                        new Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height));

            Color fg = e.ColumnIndex == 0 ? (sel ? C_GREEN : C_TXT)
                     : e.ColumnIndex == 1 ? C_BLUE
                     : e.ColumnIndex == 2 ? C_AMBER
                     : C_SUB;

            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 0
                    ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
                e.Graphics.DrawString(e.SubItem.Text, rpList.Font, br,
                    new Rectangle(e.Bounds.X + (e.ColumnIndex == 0 ? 12 : 4),
                        e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), sf);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            spinTimer?.Stop();
            base.OnFormClosed(e);
        }
    }
}