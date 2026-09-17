using System;
using System.Threading;
using System.Windows;
using PowerApp.Services;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;

namespace PowerApp
{
    public partial class App : System.Windows.Application
    {
        private Mutex? _mutex;
        private PowerMonitorService? _monitorService;
        private TrayIconManager? _trayIconManager;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "Local\\PowerAppWakeRequestsMonitor_SingleInstance";
            _mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                MessageBox.Show("PowerApp is already running in the system tray.", "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            _monitorService = new PowerMonitorService();
            if (e.Args.Any(a => a.Equals("--demo", StringComparison.OrdinalIgnoreCase) || a.Equals("--mock", StringComparison.OrdinalIgnoreCase)))
            {
                _monitorService.IsDemoMode = true;
            }
            _mainWindow = new MainWindow(_monitorService);
            _trayIconManager = new TrayIconManager();

            _trayIconManager.ToggleWindowRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_mainWindow.IsVisible)
                    {
                        _mainWindow.Hide();
                    }
                    else
                    {
                        _mainWindow.ShowAndActivate();
                    }
                });
            };

            _trayIconManager.RefreshRequested += async () =>
            {
                await _monitorService.RefreshNowAsync();
            };

            _trayIconManager.CopyRequestsRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_monitorService.LatestSnapshot != null)
                    {
                        Clipboard.SetText(_monitorService.LatestSnapshot.ToPowercfgOutput());
                    }
                });
            };

            _trayIconManager.RestartAsAdminRequested += () =>
            {
                Dispatcher.Invoke(PowerApp.MainWindow.RestartAsAdministrator);
            };

            _trayIconManager.ExitRequested += () =>
            {
                Dispatcher.Invoke(ShutdownApp);
            };

            _monitorService.SnapshotUpdated += snapshot =>
            {
                _trayIconManager.UpdateFromSnapshot(snapshot);
            };

            // Start background monitoring
            _monitorService.Start();

            // Show window on launch
            _mainWindow.ShowAndActivate();
        }

        private void ShutdownApp()
        {
            _mainWindow?.ForceExit();
            _trayIconManager?.Dispose();
            _monitorService?.Dispose();
            _mutex?.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIconManager?.Dispose();
            _monitorService?.Dispose();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}