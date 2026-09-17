using System;
using System.Threading;
using System.Threading.Tasks;
using PowerApp.Models;
using PowerApp.Native;

namespace PowerApp.Services
{
    public class PowerMonitorService : IDisposable
    {
        private readonly System.Threading.Timer _timer;
        private bool _isDisposed;
        private bool _isRefreshing;
        private TimeSpan _interval = TimeSpan.FromSeconds(5);

        public event Action<PowerSnapshot>? SnapshotUpdated;

        public PowerSnapshot LatestSnapshot { get; private set; }

        public TimeSpan Interval
        {
            get => _interval;
            set
            {
                _interval = value;
                if (!_isDisposed)
                {
                    _timer.Change(TimeSpan.Zero, _interval);
                }
            }
        }

        public bool IsAutoRefreshEnabled { get; set; } = true;
        public bool IsDemoMode { get; set; }

        public PowerMonitorService()
        {
            LatestSnapshot = new PowerSnapshot { Timestamp = DateTime.Now };
            _timer = new System.Threading.Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            _timer.Change(TimeSpan.Zero, _interval);
        }

        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public async Task<PowerSnapshot> RefreshNowAsync()
        {
            return await Task.Run(() =>
            {
                var snapshot = IsDemoMode
                    ? PowerRequestReader.CreateDemoSnapshot()
                    : PowerRequestReader.QueryPowerRequests();
                LatestSnapshot = snapshot;
                SnapshotUpdated?.Invoke(snapshot);
                return snapshot;
            });
        }

        private void OnTimerTick(object? state)
        {
            if (_isDisposed || !IsAutoRefreshEnabled)
                return;

            if (_isRefreshing)
                return;

            _isRefreshing = true;
            try
            {
                var snapshot = IsDemoMode
                    ? PowerRequestReader.CreateDemoSnapshot()
                    : PowerRequestReader.QueryPowerRequests();
                LatestSnapshot = snapshot;
                SnapshotUpdated?.Invoke(snapshot);
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _timer.Dispose();
            }
        }
    }
}
