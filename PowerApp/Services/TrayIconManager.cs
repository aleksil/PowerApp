using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PowerApp.Models;

namespace PowerApp.Services
{
    public class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private Icon? _currentIcon;
        private bool _isDisposed;

        public event Action? ToggleWindowRequested;
        public event Action? RefreshRequested;
        public event Action? ExitRequested;
        public event Action? RestartAsAdminRequested;
        public event Action? CopyRequestsRequested;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public TrayIconManager()
        {
            _contextMenu = new ContextMenuStrip();
            InitializeContextMenu();

            _notifyIcon = new NotifyIcon
            {
                Visible = true,
                Text = "Power Wake Requests Monitor",
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ToggleWindowRequested?.Invoke();
                }
            };

            _notifyIcon.DoubleClick += (s, e) =>
            {
                ToggleWindowRequested?.Invoke();
            };

            UpdateIcon(hasActiveRequests: false, isElevated: true, error: null);
        }

        private void InitializeContextMenu()
        {
            var showItem = new ToolStripMenuItem("Show Wake Requests")
            {
                Font = new Font(_contextMenu.Font, FontStyle.Bold)
            };
            showItem.Click += (s, e) => ToggleWindowRequested?.Invoke();

            var refreshItem = new ToolStripMenuItem("Refresh Now");
            refreshItem.Click += (s, e) => RefreshRequested?.Invoke();

            var copyItem = new ToolStripMenuItem("Copy to Clipboard (powercfg /requests)");
            copyItem.Click += (s, e) => CopyRequestsRequested?.Invoke();

            var adminItem = new ToolStripMenuItem("Restart as Administrator")
            {
                Name = "AdminItem",
                Visible = false
            };
            adminItem.Click += (s, e) => RestartAsAdminRequested?.Invoke();

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitRequested?.Invoke();

            _contextMenu.Items.Add(showItem);
            _contextMenu.Items.Add(refreshItem);
            _contextMenu.Items.Add(copyItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(adminItem);
            _contextMenu.Items.Add(exitItem);
        }

        public void UpdateFromSnapshot(PowerSnapshot snapshot)
        {
            if (_isDisposed)
                return;

            bool hasRequests = snapshot.HasActiveRequests;
            bool isElevated = snapshot.IsElevated;
            string? error = snapshot.ErrorMessage;

            UpdateIcon(hasRequests, isElevated, error);

            // Update tooltip text (max 127 chars for NotifyIcon.Text)
            string text;
            if (!string.IsNullOrEmpty(error))
            {
                text = error.Length > 60 ? error[..60] + "..." : error;
            }
            else if (hasRequests)
            {
                text = $"Wake Requests: {snapshot.TotalActiveRequests} active";
            }
            else
            {
                text = "Wake Requests: None (Sleep allowed)";
            }

            if (text.Length >= 64)
            {
                text = text[..63];
            }
            _notifyIcon.Text = text;

            // Admin menu item visibility
            var adminMenuItem = _contextMenu.Items["AdminItem"];
            if (adminMenuItem != null)
            {
                adminMenuItem.Visible = !isElevated;
            }
        }

        public void UpdateIcon(bool hasActiveRequests, bool isElevated, string? error)
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // Base circle / pill representing power symbol or monitor
            // Outer dark ring for contrast on both light & dark taskbars
            using (var outerPen = new Pen(Color.FromArgb(200, 30, 30, 30), 2.5f))
            {
                g.DrawEllipse(outerPen, 3, 3, size - 7, size - 7);
            }

            Color dotColor;
            Color glowColor;

            if (!string.IsNullOrEmpty(error) && !isElevated)
            {
                // Amber / Yellow for elevation needed
                dotColor = Color.FromArgb(245, 158, 11);
                glowColor = Color.FromArgb(120, 245, 158, 11);
            }
            else if (hasActiveRequests)
            {
                // Bright Red for active wake requests
                dotColor = Color.FromArgb(239, 68, 68);
                glowColor = Color.FromArgb(140, 239, 68, 68);
            }
            else
            {
                // Emerald Green for idle / sleep allowed
                dotColor = Color.FromArgb(34, 197, 94);
                glowColor = Color.FromArgb(120, 34, 197, 94);
            }

            // Glow ring
            using (var glowBrush = new SolidBrush(glowColor))
            {
                g.FillEllipse(glowBrush, 5, 5, size - 11, size - 11);
            }

            // Main center solid dot
            using (var dotBrush = new SolidBrush(dotColor))
            {
                g.FillEllipse(dotBrush, 8, 8, size - 17, size - 17);
            }

            // Highlight glint for 3D glassy look
            using (var glintBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255)))
            {
                g.FillEllipse(glintBrush, 11, 10, 4, 3);
            }

            IntPtr hIcon = bitmap.GetHicon();
            var newIcon = Icon.FromHandle(hIcon);

            _notifyIcon.Icon = newIcon;

            if (_currentIcon != null)
            {
                DestroyIcon(_currentIcon.Handle);
                _currentIcon.Dispose();
            }

            _currentIcon = newIcon;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _contextMenu.Dispose();

                if (_currentIcon != null)
                {
                    DestroyIcon(_currentIcon.Handle);
                    _currentIcon.Dispose();
                }
            }
        }
    }
}
