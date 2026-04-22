using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Compression;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;


namespace Tech_ToolKit_Pro
{
    public partial class Form1 : Form
    {
        private Form _activeChild = null;

        public Form1()
        {
            InitializeComponent();
            menuTransition.Tick += menuTransition_Tick;
            securityTransition.Tick += securityTransition_Tick;
            networkTransition.Tick += networkTransition_Tick;
            diagnosticTransition.Tick += diagnosticTransition_Tick;
            updateTransition.Tick += updateTransition_Tick;
            menuContainer.AutoSize = false;
            securityContainer.AutoSize = false;
            networkContainer.AutoSize = false;
            diagnosticContainer.AutoSize = false;
            updateContainer.AutoSize = false;
            menuContainer.Height = 38;
            securityContainer.Height = 38;
            networkContainer.Height = 38;
            diagnosticContainer.Height = 38;
            updateContainer.Height = 38;
        }

        // ════════════════════════════════════════════════════════════
        //  LOAD FORM — the only method you need to show any sub-form
        // ════════════════════════════════════════════════════════════
        private void LoadForm(Form form)
        {
            if (_activeChild != null && !_activeChild.IsDisposed)
            {
                _activeChild.Close();
                _activeChild.Dispose();
            }
            _activeChild = form;
            form.TopLevel = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.Dock = DockStyle.Fill;
            form.Parent = contentPanel;
            contentPanel.Controls.Clear();
            contentPanel.Controls.Add(form);
            form.BringToFront();
            form.Show();
        }

        bool MaintenanceExpand = false;
        bool SecurityExpand = false;
        bool NetworkExpand = false;
        bool DiagnosticExpand = false;
        bool UpdateExpand = false;
        bool SidebarExpand = false;

        private void menuTransition_Tick(object sender, EventArgs e)
        {
            if (!MaintenanceExpand) { menuContainer.Height += 10; if (menuContainer.Height >= 250) { menuContainer.Height = 250; menuTransition.Stop(); MaintenanceExpand = true; } }
            else { menuContainer.Height -= 10; if (menuContainer.Height <= 38) { menuContainer.Height = 38; menuTransition.Stop(); MaintenanceExpand = false; } }
        }

        private void securityTransition_Tick(object sender, EventArgs e)
        {
            if (!SecurityExpand) { securityContainer.Height += 10; if (securityContainer.Height >= 143) { securityContainer.Height = 143; securityTransition.Stop(); SecurityExpand = true; } }
            else { securityContainer.Height -= 10; if (securityContainer.Height <= 38) { securityContainer.Height = 38; securityTransition.Stop(); SecurityExpand = false; } }
        }

        private void networkTransition_Tick(object sender, EventArgs e)
        {
            if (!NetworkExpand) { networkContainer.Height += 10; if (networkContainer.Height >= 73) { networkContainer.Height = 73; networkTransition.Stop(); NetworkExpand = true; } }
            else { networkContainer.Height -= 10; if (networkContainer.Height <= 38) { networkContainer.Height = 38; networkTransition.Stop(); NetworkExpand = false; } }
        }

        private void diagnosticTransition_Tick(object sender, EventArgs e)
        {
            if (!DiagnosticExpand) { diagnosticContainer.Height += 10; if (diagnosticContainer.Height >= 108) { diagnosticContainer.Height = 108; diagnosticTransition.Stop(); DiagnosticExpand = true; } }
            else { diagnosticContainer.Height -= 10; if (diagnosticContainer.Height <= 38) { diagnosticContainer.Height = 38; diagnosticTransition.Stop(); DiagnosticExpand = false; } }
        }

        private void updateTransition_Tick(object sender, EventArgs e)
        {
            if (!UpdateExpand) { updateContainer.Height += 10; if (updateContainer.Height >= 114) { updateContainer.Height = 114; updateTransition.Stop(); UpdateExpand = true; } }
            else { updateContainer.Height -= 10; if (updateContainer.Height <= 38) { updateContainer.Height = 38; updateTransition.Stop(); UpdateExpand = false; } }
        }

        private void sidebarTransition_Tick(object sender, EventArgs e)
        {
            if (SidebarExpand)
            {
                Sidebar.Width -= 10;
                if (Sidebar.Width <= 42)
                {
                    Sidebar.Width = 42; SidebarExpand = false; sidebarTransition.Stop();
                    pndashboard.Width = menuContainer.Width = networkContainer.Width =
                    updateContainer.Width = diagnosticContainer.Width = securityContainer.Width = pnrecoveryfiles.Width = pnWinBackUp.Width = Sidebar.Width;
                }
            }
            else
            {
                Sidebar.Width += 10;
                if (Sidebar.Width >= 198)
                {
                    Sidebar.Width = 198; SidebarExpand = true; sidebarTransition.Stop();
                    pndashboard.Width = menuContainer.Width = networkContainer.Width =
                    updateContainer.Width = diagnosticContainer.Width = securityContainer.Width = pnrecoveryfiles.Width = pnWinBackUp.Width = Sidebar.Width;
                }
            }
        }

        // ── Sidebar toggles ──────────────────────────────────────────
        private void button3_Click(object sender, EventArgs e) => menuTransition.Start();
        private void button14_Click(object sender, EventArgs e) => securityTransition.Start();
        private void button5_Click(object sender, EventArgs e) => networkTransition.Start();
        private void button25_Click(object sender, EventArgs e) => diagnosticTransition.Start();
        private void button46_Click(object sender, EventArgs e) => updateTransition.Start();
        private void btnHam_Click(object sender, EventArgs e) => sidebarTransition.Start();

        // ── Startup ──────────────────────────────────────────────────
        private void Form1_Load(object sender, EventArgs e)
        {
            // Enable double buffering on contentPanel to reduce flickering during UI updates/transitions
            typeof(Panel).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, contentPanel, new object[] { true });

            LoadForm(new FormDashboard()); // Dashboard always shows first

        }

        // ── Content buttons — one line each ──────────────────────────
        private void button1_Click(object sender, EventArgs e) => LoadForm(new FormDashboard());
        private void button2_Click(object sender, EventArgs e) => LoadForm(new FormPointofRestoration());
        private void button4_Click(object sender, EventArgs e) => LoadForm(new FormTempCleanUp());
        private void button6_Click(object sender, EventArgs e) => LoadForm(new FormUltraCleanUp());
        private void button7_Click(object sender, EventArgs e) => LoadForm(new FormDiskMaintenance());
        private void button10_Click(object sender, EventArgs e) { } // reserved
        private void button12_Click(object sender, EventArgs e) => LoadForm(new FormTaskList());
        private void button13_Click(object sender, EventArgs e) => LoadForm(new FormUninstallApps());
        private void button15_Click(object sender, EventArgs e) => LoadForm(new FormMRTscan());
        private void button16_Click(object sender, EventArgs e) => LoadForm(new FormDefenderScan());
        private void button17_Click(object sender, EventArgs e) => LoadForm(new FormSmartScan());
        private void button26_Click(object sender, EventArgs e) => LoadForm(new FormDiskSmart());
        private void button27_Click(object sender, EventArgs e) => LoadForm(new FormSystemReport());
        private void button36_Click(object sender, EventArgs e) => LoadForm(new FormFlushDNS());
        private void button51_Click(object sender, EventArgs e) => LoadForm(new FormShowUpdate());
        private void UpApps_Click(object sender, EventArgs e) => LoadForm(new FormAppsUptates());
        private void button8_Click(object sender, EventArgs e) => LoadForm(new FormRecoveryFiles());
        private void button9_Click(object sender, EventArgs e) => LoadForm(new FormWinBackUp());

        private void button10_Click_1(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private bool isCustomMaximized = false;
        private Size normalSize = new Size(1072, 616);

        private void button11_Click(object sender, EventArgs e)
        {
            if (!isCustomMaximized)
            {
                // Get screen working area (without taskbar)
                Rectangle screen = Screen.PrimaryScreen.WorkingArea;

                // Resize to 85% of screen (clean modern look)
                this.Size = new Size(
                    (int)(screen.Width * 0.85),
                    (int)(screen.Height * 0.85)
                );

                // Center the form
                this.Location = new Point(
                    (screen.Width - this.Width) / 2,
                    (screen.Height - this.Height) / 2
                );

                isCustomMaximized = true;
            }
            else
            {
                // Restore normal size
                this.Size = normalSize;
                this.StartPosition = FormStartPosition.CenterScreen;

                isCustomMaximized = false;
            }
        }
        private void button18_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        public const int WM_INCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 0x2;
        [DllImport("User32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("User32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int IParam);
        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_INCLBUTTONDOWN, HTCAPTION, 0);
            }
        }
    }
}





