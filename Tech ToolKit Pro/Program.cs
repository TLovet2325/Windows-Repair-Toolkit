using System;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ── Global Exception Handling ────────────────────────────
            Application.ThreadException += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ThreadException] {args.Exception.Message}");
                MessageBox.Show($"An unexpected error occurred:\n\n{args.Exception.Message}", 
                    "Tech ToolKit Pro - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                if (ex != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[UnhandledException] {ex.Message}");
                    MessageBox.Show($"A critical error occurred and the application must close:\n\n{ex.Message}", 
                        "Tech ToolKit Pro - Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }
            };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // ── Log admin status at startup (for debugging) ───────────
            System.Diagnostics.Debug.WriteLine(
                AdminHelper.IsAdmin
                    ? "[Tech ToolKit Pro] Running as Administrator."
                    : "[Tech ToolKit Pro] Running as Standard User.");

            Application.Run(new Form1());
        }
    }
}
