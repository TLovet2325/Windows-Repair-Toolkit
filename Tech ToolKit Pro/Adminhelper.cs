using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using System.Drawing;

namespace Tech_ToolKit_Pro
{
    // ════════════════════════════════════════════════════════════════
    //  ADMIN HELPER
    //  ─────────────────────────────────────────────────────────────
    //  Central class that handles all administrative-rights logic
    //  for Tech ToolKit Pro.
    //
    //  USAGE PATTERN in any form:
    //
    //    1. Check once on form load:
    //         if (!AdminHelper.IsAdmin)
    //             AdminHelper.ShowAdminBanner(this.panelContent);
    //
    //    2. Before running an admin command:
    //         if (!AdminHelper.EnsureAdmin("Disk Cleanup")) return;
    //         // ... run the command
    //
    //    3. Elevate a single external process silently:
    //         AdminHelper.RunElevated("sfc", "/scannow");
    //
    //    4. Restart the whole app elevated (last resort):
    //         AdminHelper.RestartAsAdmin();
    // ════════════════════════════════════════════════════════════════
    public static class AdminHelper
    {
        // ── Theme colours (match app theme) ──────────────────────────
        static readonly Color C_SURF = Color.FromArgb(22, 27, 34);
        static readonly Color C_AMBER = Color.FromArgb(255, 163, 72);
        static readonly Color C_RED = Color.FromArgb(248, 81, 73);
        static readonly Color C_GREEN = Color.FromArgb(63, 185, 119);
        static readonly Color C_TXT = Color.FromArgb(230, 237, 243);
        static readonly Color C_SUB = Color.FromArgb(139, 148, 158);
        static readonly Color C_BORDER = Color.FromArgb(48, 54, 61);

        // ════════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns true if the current process is running as Administrator.
        /// </summary>
        public static bool IsAdmin
        {
            get
            {
                try
                {
                    using (var id = WindowsIdentity.GetCurrent())
                    {
                        var principal = new WindowsPrincipal(id);
                        return principal.IsInRole(
                            WindowsBuiltInRole.Administrator);
                    }
                }
                catch { return false; }
            }
        }

        /// <summary>
        /// Returns the current user's username.
        /// </summary>
        public static string CurrentUser =>
            WindowsIdentity.GetCurrent()?.Name ?? "Unknown";

        // ════════════════════════════════════════════════════════════
        //  ENSURE ADMIN — show a dialog if not admin
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Call before running any feature that needs admin rights.
        /// If already admin → returns true immediately.
        /// If not admin → shows a styled dialog with 3 options:
        ///   [Restart as Admin]  [Run this once elevated]  [Cancel]
        /// Returns true only if the feature may proceed.
        /// </summary>
        public static bool EnsureAdmin(string featureName)
        {
            if (IsAdmin) return true;

            return ShowAdminDialog(featureName);
        }

        // ════════════════════════════════════════════════════════════
        //  ADMIN DIALOG
        // ════════════════════════════════════════════════════════════
        static bool ShowAdminDialog(string featureName)
        {
            bool result = false;

            var dlg = new Form
            {
                Text = "Administrator Required",
                Size = new Size(480, 300),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(13, 17, 23),
                ForeColor = C_TXT,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = new Font("Segoe UI", 9f)
            };

            // Shield icon + title
            var lblIcon = new Label
            {
                Text = "🛡",
                Font = new Font("Segoe UI", 28f),
                ForeColor = C_AMBER,
                AutoSize = true,
                Location = new Point(24, 20)
            };

            var lblHead = new Label
            {
                Text = "Administrator Rights Required",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_AMBER,
                AutoSize = true,
                Location = new Point(76, 24)
            };

            var lblBody = new Label
            {
                Text = string.Format(
                    "'{0}' requires Administrator rights to run.\n\n" +
                    "You are currently running as a Standard User.\n" +
                    "Choose an option below:",
                    featureName),
                Font = new Font("Segoe UI", 9f),
                ForeColor = C_SUB,
                AutoSize = false,
                Size = new Size(430, 60),
                Location = new Point(24, 76)
            };

            // Separator
            var sep = new Panel
            {
                Location = new Point(0, 148),
                Size = new Size(480, 1),
                BackColor = C_BORDER
            };

            // Buttons
            var btnRestart = MakeDlgBtn(
                "🔄  Restart App as Admin",
                "Relaunches Tech ToolKit Pro with full admin rights.",
                C_AMBER, new Point(24, 162));

            var btnRunOnce = MakeDlgBtn(
                "⚡  Run This Feature Only (Elevated)",
                "Runs just this command elevated — app stays standard.",
                C_GREEN, new Point(24, 210));

            var btnCancel = MakeDlgBtn(
                "✕  Cancel",
                "Go back without running this feature.",
                C_SUB, new Point(24, 258));
            btnCancel.Size = new Size(120, 28);

            btnRestart.Click += (s, e) =>
            {
                dlg.DialogResult = DialogResult.No; // signal restart
                dlg.Close();
            };
            btnRunOnce.Click += (s, e) =>
            {
                result = true;
                dlg.DialogResult = DialogResult.Yes;
                dlg.Close();
            };
            btnCancel.Click += (s, e) =>
            {
                dlg.DialogResult = DialogResult.Cancel;
                dlg.Close();
            };

            dlg.Controls.AddRange(new Control[]
            {
                lblIcon, lblHead, lblBody, sep,
                btnRestart, btnRunOnce, btnCancel
            });

            var dr = dlg.ShowDialog();

            if (dr == DialogResult.No)
            {
                RestartAsAdmin();
                return false;
            }

            return dr == DialogResult.Yes;
        }

        // ════════════════════════════════════════════════════════════
        //  ADMIN BANNER — add a thin warning strip to any panel
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Inserts a thin amber "Not running as admin" banner
        /// at the top of the given parent panel.
        /// Call this in Form_Load for forms that need admin rights.
        /// </summary>
        public static Panel ShowAdminBanner(Control parent,
            string message = null)
        {
            if (IsAdmin) return null; // already admin — no banner needed

            var banner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(30, 255, 163, 72)
            };
            banner.Paint += (s, e) =>
            {
                using (var p = new Pen(C_AMBER, 1))
                    e.Graphics.DrawLine(p, 0, banner.Height - 1,
                        banner.Width, banner.Height - 1);
                using (var br = new SolidBrush(C_AMBER))
                    e.Graphics.FillRectangle(br, 0, 0, 3, banner.Height);
            };

            string msg = message ??
                "⚠  Some features on this page require Administrator rights. " +
                "Click 'Restart as Admin' to unlock all features.";

            var lbl = new Label
            {
                Text = msg,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = C_AMBER,
                AutoSize = true,
                Location = new Point(12, 8)
            };

            var btnElevate = new Button
            {
                Text = "🛡  Restart as Admin",
                Font = new Font("Segoe UI Semibold", 7.5f),
                ForeColor = C_AMBER,
                BackColor = Color.FromArgb(40, 255, 163, 72),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(150, 22),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnElevate.FlatAppearance.BorderColor = C_AMBER;
            btnElevate.FlatAppearance.BorderSize = 1;
            btnElevate.Click += (s, e) => RestartAsAdmin();

            banner.Controls.AddRange(new Control[] { lbl, btnElevate });
            banner.Resize += (s, e) =>
                btnElevate.Location = new Point(banner.Width - 164, 4);

            // Insert at top of parent's control list
            parent.Controls.Add(banner);
            parent.Controls.SetChildIndex(banner, 0);
            return banner;
        }

        // ════════════════════════════════════════════════════════════
        //  RESTART AS ADMIN
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Restarts the current application with "runas" (UAC prompt).
        /// If the user accepts → new elevated process starts, current exits.
        /// If the user cancels the UAC → nothing happens.
        /// </summary>
        public static void RestartAsAdmin()
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    }
                };
                proc.Start();
                Application.Exit();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User cancelled UAC prompt — do nothing
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not restart as Administrator:\n\n" + ex.Message,
                    "Elevation Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  RUN ELEVATED — single external process
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Starts an external process elevated (runas) and waits for exit.
        /// Returns the exit code, or -1 on failure.
        /// Set waitForExit = false to fire-and-forget.
        /// Set showWindow  = true  to show a console window.
        /// </summary>
        public static int RunElevated(string exe, string args,
            bool waitForExit = true,
            bool showWindow = false)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = args,
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = showWindow
                            ? ProcessWindowStyle.Normal
                            : ProcessWindowStyle.Hidden,
                        CreateNoWindow = !showWindow
                    }
                };

                proc.Start();

                if (waitForExit)
                {
                    proc.WaitForExit();
                    return proc.ExitCode;
                }

                return 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User cancelled UAC prompt
                return -2;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(
                        "Failed to run elevated:\n{0} {1}\n\n{2}",
                        exe, args, ex.Message),
                    "Elevation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return -1;
            }
        }

        /// <summary>
        /// Same as RunElevated but fires a callback on the UI thread
        /// when the process finishes (non-blocking).
        /// </summary>
        public static void RunElevatedAsync(string exe, string args,
            Control uiContext,
            Action<int> onComplete,
            bool showWindow = false)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = args,
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = showWindow
                            ? ProcessWindowStyle.Normal
                            : ProcessWindowStyle.Hidden,
                        CreateNoWindow = !showWindow
                    },
                    EnableRaisingEvents = true
                };

                proc.Exited += (s, e) =>
                {
                    int code = proc.ExitCode;
                    if (uiContext != null && uiContext.InvokeRequired)
                        uiContext.Invoke(new Action(() => onComplete?.Invoke(code)));
                    else
                        onComplete?.Invoke(code);
                };

                proc.Start();
                System.Threading.ThreadPool.QueueUserWorkItem(
                    _ => proc.WaitForExit());
            }
            catch (System.ComponentModel.Win32Exception)
            {
                onComplete?.Invoke(-2); // user cancelled UAC
            }
            catch
            {
                onComplete?.Invoke(-1);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  RUN COMMAND — hidden, capture output
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Runs a command and captures stdout+stderr.
        /// Does NOT elevate — use RunElevated for admin commands.
        /// Returns output string and exit code via out params.
        /// </summary>
        public static string RunCommand(string exe, string args,
            out int exitCode,
            int timeoutMs = 30000)
        {
            exitCode = -1;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = new Process { StartInfo = psi })
                {
                    proc.Start();
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(timeoutMs);
                    exitCode = proc.ExitCode;

                    string combined = stdout;
                    if (!string.IsNullOrEmpty(stderr))
                        combined += (combined.Length > 0 ? "\n" : "") + stderr;
                    return combined.Trim();
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  ADMIN LABEL HELPER — add a shield badge to a button
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Prefixes a button's text with a shield icon if not admin,
        /// and appends "(Admin)" as a reminder.
        /// Call this for every button that triggers an admin feature.
        /// </summary>
        public static void MarkAdminButton(Button btn,
            string baseText, bool needsAdmin = true)
        {
            if (!needsAdmin || IsAdmin)
            {
                btn.Text = baseText;
                return;
            }

            btn.Text = "🛡  " + baseText;
            btn.ForeColor = Color.FromArgb(255, 163, 72); // amber
            // Tooltip
            var tt = new ToolTip();
            tt.SetToolTip(btn,
                "This feature requires Administrator rights.\n" +
                "You will be prompted to confirm.");
        }

        // ════════════════════════════════════════════════════════════
        //  STATUS BAR ADMIN INDICATOR
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns a formatted admin-status string for use in status bars.
        /// e.g. "🛡 Running as Administrator" or "⚠ Standard User"
        /// </summary>
        public static string AdminStatusText =>
            IsAdmin
                ? "🛡  Running as Administrator"
                : "⚠  Standard User — some features require elevation";

        public static Color AdminStatusColor =>
            IsAdmin ? Color.FromArgb(63, 185, 119)   // green
                    : Color.FromArgb(255, 163, 72);   // amber

        // ════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════
        static Button MakeDlgBtn(string text, string tooltip,
            Color accent, Point loc)
        {
            var b = new Button
            {
                Text = text,
                Location = loc,
                Size = new Size(430, 34),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = accent,
                BackColor = Color.FromArgb(20, accent.R, accent.G, accent.B),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(50, accent.R, accent.G, accent.B);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, accent.R, accent.G, accent.B);

            if (!string.IsNullOrEmpty(tooltip))
                new ToolTip().SetToolTip(b, tooltip);

            return b;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ADMIN GUARD — attribute-style wrapper for any method
    //
    //  Usage:
    //    using (var guard = new AdminGuard("CHKDSK")) {
    //        if (!guard.Ok) return;
    //        // ... admin code here
    //    }
    // ════════════════════════════════════════════════════════════════
    public sealed class AdminGuard : IDisposable
    {
        public bool Ok { get; private set; }

        public AdminGuard(string featureName)
        {
            Ok = AdminHelper.EnsureAdmin(featureName);
        }

        public void Dispose() { }
    }
}