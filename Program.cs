using System.Net.Http;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Win32;

namespace IPNotifier
{
    internal static class Program
    {
        private static NotifyIcon? trayIcon;
        private static string? currentIP = "Unknown";
        private static DispatcherTimer? ipCheckTimer;

        private const string AppName = "IPNotifier";
        private const string AutoStartRegKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AutoStartRegKeyValue = "{C7F9A9A7-9D2F-4F3E-A60A-A7D0077CA9D7}";

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            InitializeTrayIcon();
            InitializeIPCheckTimer();
            CheckIPAsync().ConfigureAwait(false); // Первоначальная проверка IP

            Application.Run();
        }

        private static void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Visible = true,
                Text = "IP Notifier"
            };

            trayIcon.ContextMenuStrip = new ContextMenuStrip();
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, Exit);
            trayIcon.ContextMenuStrip.Items.Add("Auto Start", null, ToggleAutoStart);

            trayIcon.MouseClick += TrayIcon_MouseClick;
            trayIcon.BalloonTipClicked += TrayIcon_BalloonTipClicked;
        }

        private static void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                trayIcon?.ShowBalloonTip(5000, "Current IP", $"Current IP: {currentIP}", ToolTipIcon.Info);
            }
        }

        private static void TrayIcon_BalloonTipClicked(object? sender, EventArgs e)
        {
            MessageBox.Show($"Current IP: {currentIP}", "IP Address", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void Exit(object? sender, EventArgs? e)
        {
            ipCheckTimer?.Stop();
            trayIcon?.Dispose();
            Application.Exit();
        }

        private static void ToggleAutoStart(object? sender, EventArgs? e)
        {
            try
            {
                using (RegistryKey regKey = Registry.CurrentUser.OpenSubKey(AutoStartRegKeyPath, true))
                {
                    if (regKey == null)
                    {
                        Log("Failed to open registry key. Access denied.", true);
                        MessageBox.Show("Access denied. Please run the application as administrator.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (regKey.GetValue(AutoStartRegKeyValue) == null)
                    {
                        regKey.SetValue(AutoStartRegKeyValue, Application.ExecutablePath);
                        Log("Auto start enabled.");
                        MessageBox.Show("Auto start enabled.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        regKey.DeleteValue(AutoStartRegKeyValue, false);
                        Log("Auto start disabled.");
                        MessageBox.Show("Auto start disabled.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error accessing registry: {ex.Message}", true);
                MessageBox.Show($"Error accessing registry: {ex.Message}", AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void InitializeIPCheckTimer()
        {
            ipCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            ipCheckTimer.Tick += IpCheckTimer_Tick;
            ipCheckTimer.Start();
        }

        private static void IpCheckTimer_Tick(object? sender, EventArgs e)
        {
            CheckIPAsync().ConfigureAwait(false);
        }

        private static async Task CheckIPAsync()
        {
            string? newIP = await GetExternalIPAsync();
            if (!newIP.Equals(currentIP))
            {
                currentIP = newIP;
                trayIcon?.ShowBalloonTip(5000, "IP Change Detected", $"Your new IP is: {newIP}", ToolTipIcon.Info);
            }
        }

        private static async Task<string?> GetExternalIPAsync()
        {
            using HttpClient client = new();
            try
            {
                Log("Fetching external IP...");
                var response = await client.GetStringAsync("https://api.ipify.org");
                Log($"External IP: {response.Trim()}");
                return response.Trim();
            }
            catch (Exception ex)
            {
                Log($"Error fetching IP: {ex.Message}", true);
                MessageBox.Show($"Error fetching IP: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return currentIP; // Возвращаем последний известный IP в случае ошибки
            }
        }

        private static void Log(string message, bool isError = false)
        {
            string logMessage = $"{DateTime.Now}: {message}";
            if (isError)
            {
                Console.Error.WriteLine(logMessage);
            }
            else
            {
                Console.WriteLine(logMessage);
            }
        }
    }
}