using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Repository;
using NLog;

namespace ChewsiPlugin.Launcher
{
    internal class App : ApplicationContext
    {
        private static Logger _logger;
        private const string PluginExecutableName = "ChewsiPlugin.UI.exe";
        private const string DentrixExecutableNameWithoutExtension = "Office";
        private const string OpenDentalExecutableNameWithoutExtension = "OpenDental";
        private const string EagleSoftExecutableNameWithoutExtension = "Eaglesoft";
        private const int RefreshIntervalMs = 1500;
        private static NotifyIcon _notifyIcon;
        private readonly CancellationTokenSource _tokenSource;
        private readonly string _pmsProcessName;
        private bool _pmsStarted;
        private readonly int _currentSessionId;

        /// <summary>
        /// Minimize memory consumption
        /// </summary>
        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hwProc);
        const int SW_RESTORE = 9;
        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("User32.dll")]
        private static extern bool ShowWindow(IntPtr handle, int nCmdShow);
        [DllImport("User32.dll")]
        private static extern bool IsIconic(IntPtr handle);
        
        public App()
        {
            _currentSessionId = Process.GetCurrentProcess().SessionId;
            var repository = new Repository();
            repository.Initialize();

            var pmsType = repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
            switch (pmsType)
            {
                case Settings.PMS.Types.Dentrix:
                    _pmsProcessName = DentrixExecutableNameWithoutExtension;
                    break;
                case Settings.PMS.Types.OpenDental:
                    _pmsProcessName = OpenDentalExecutableNameWithoutExtension;
                    break;
                case Settings.PMS.Types.Eaglesoft:
                    _pmsProcessName = EagleSoftExecutableNameWithoutExtension;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _notifyIcon = new NotifyIcon
            {
                Icon = Properties.Resources.Icon,
                ContextMenu = new ContextMenu(new[]
                {
                    new MenuItem("Launch Chewsi Plugin", Launch),
                    new MenuItem("Exit", Exit)
                }),
                Visible = true
            };
            
            _tokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(Lookup, _tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);

            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }
        
        private void Exit(object sender, EventArgs e)
        {
            // Remove the icon before exit
            _notifyIcon.Visible = false;
            _tokenSource.Cancel();
            Application.Exit();
        }

        private Logger Logger => _logger ?? (_logger = LogManager.GetCurrentClassLogger());

        private void Launch(object sender, EventArgs e)
        {
            StartPlugin();
        }

        private void Lookup()
        {
            while (true)
            {
                var runningProcesses = Process.GetProcessesByName(_pmsProcessName);
                var pmsProcess = runningProcesses.FirstOrDefault(m => m.SessionId == _currentSessionId);
                if (pmsProcess != null)
                {
                    if (!_pmsStarted)
                    {
                        _pmsStarted = true;
                        StartPlugin();
                        //pmsProcess.WaitForExit();
                    }
                }
                else
                {
                    _pmsStarted = false;
                }
                Utils.SleepWithCancellation(_tokenSource.Token, RefreshIntervalMs);
            }
        }

        private static void BringProcessToFront(Process process)
        {
            IntPtr handle = process.MainWindowHandle;
            // if window is minimized
            if (IsIconic(handle))
            {
                ShowWindow(handle, SW_RESTORE);
            }
            SetForegroundWindow(handle);
        }
        
        private void StartPlugin()
        {
            Process[] runningProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(PluginExecutableName));
            var process = runningProcesses.FirstOrDefault(p => p.SessionId == _currentSessionId);
            if (process == null)
            {
                try
                {
                    Process.Start(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), PluginExecutableName));
                }
                catch (Win32Exception ex)
                {
                    Logger.Error(ex, "Failed to start plugin");
                }
                catch (FileNotFoundException ex)
                {
                    Logger.Error(ex, "Plugin not found");
                }
            }
            else
            {
                // Plugin is running, send window to foreground
                BringProcessToFront(process);
            }
        }
    }
}