using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerApp.Models;
using PowerApp.Native;
using PowerApp.Services;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace PowerApp
{
    public partial class MainWindow : Window
    {
        private readonly PowerMonitorService _monitorService;
        private PowerSnapshot? _currentSnapshot;
        private bool _isRealExit;

        public MainWindow(PowerMonitorService monitorService)
        {
            InitializeComponent();
            _monitorService = monitorService;

            _monitorService.SnapshotUpdated += OnSnapshotUpdated;

            Loaded += OnMainWindowLoaded;
        }

        private async void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            await _monitorService.RefreshNowAsync();
        }

        public void ForceExit()
        {
            _isRealExit = true;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isRealExit)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }

        public void ShowAndActivate()
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            BringIntoView();
        }

        private void OnSnapshotUpdated(PowerSnapshot snapshot)
        {
            Dispatcher.Invoke(() =>
            {
                _currentSnapshot = snapshot;
                RenderSnapshot(snapshot);
            });
        }

        private void RenderSnapshot(PowerSnapshot snapshot)
        {
            // 1. Update Header & Dot
            bool hasRequests = snapshot.HasActiveRequests;
            bool isElevated = snapshot.IsElevated;

            if (!string.IsNullOrEmpty(snapshot.ErrorMessage) && !isElevated)
            {
                // Elevation issue
                StatusGlowEllipse.Fill = new SolidColorBrush(Color.FromArgb(50, 245, 158, 11));
                StatusDotEllipse.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                StatusTitleText.Text = "Elevation Required";
                StatusSubtitleText.Text = "Cannot query system power requests without administrator privileges";
                ActiveCountBadge.Visibility = Visibility.Collapsed;
            }
            else if (hasRequests)
            {
                // Active wake requests
                StatusGlowEllipse.Fill = new SolidColorBrush(Color.FromArgb(60, 239, 68, 68));
                StatusDotEllipse.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                StatusTitleText.Text = "Wake Requests Active";
                StatusSubtitleText.Text = "One or more applications/drivers are preventing system sleep";
                ActiveCountBadge.Visibility = Visibility.Visible;
                ActiveCountBadgeText.Text = $"{snapshot.TotalActiveRequests} Active";
            }
            else
            {
                // Idle / No requests
                StatusGlowEllipse.Fill = new SolidColorBrush(Color.FromArgb(50, 34, 197, 94));
                StatusDotEllipse.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                StatusTitleText.Text = "No Wake Requests Active";
                StatusSubtitleText.Text = "System is allowed to enter sleep mode normally";
                ActiveCountBadge.Visibility = Visibility.Collapsed;
            }

            LastUpdatedText.Text = $"Updated: {snapshot.Timestamp:HH:mm:ss}";

            if (isElevated)
            {
                ElevationStatusText.Text = "🛡️ Elevated (Admin)";
                ElevationStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                AdminBanner.Visibility = Visibility.Collapsed;
            }
            else
            {
                ElevationStatusText.Text = "⚠️ Non-Elevated";
                ElevationStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 179, 0));
                AdminBanner.Visibility = Visibility.Visible;
            }

            // 2. Render Category Summary Chips
            RenderCategoryChips(snapshot);

            // 3. Render Main Category Cards
            RenderCategoryCards(snapshot);
        }

        private void RenderCategoryChips(PowerSnapshot snapshot)
        {
            CategoryChipsPanel.Children.Clear();

            foreach (var cat in snapshot.Categories)
            {
                var chipBorder = new Border
                {
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = cat.HasActiveRequests
                        ? new SolidColorBrush(Color.FromArgb(50, 239, 68, 68))
                        : new SolidColorBrush(Color.FromRgb(40, 40, 52)),
                    BorderBrush = cat.HasActiveRequests
                        ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                        : new SolidColorBrush(Color.FromRgb(60, 60, 75)),
                    BorderThickness = new Thickness(1)
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                var titleBlock = new TextBlock
                {
                    Text = cat.Title,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = cat.HasActiveRequests
                        ? new SolidColorBrush(Color.FromRgb(255, 120, 120))
                        : new SolidColorBrush(Color.FromRgb(180, 180, 195)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var countBadge = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(5, 1, 5, 1),
                    Margin = new Thickness(6, 0, 0, 0),
                    Background = cat.HasActiveRequests
                        ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                        : new SolidColorBrush(Color.FromRgb(55, 55, 70))
                };
                var countText = new TextBlock
                {
                    Text = cat.ActiveCount.ToString(),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White)
                };
                countBadge.Child = countText;

                sp.Children.Add(titleBlock);
                sp.Children.Add(countBadge);
                chipBorder.Child = sp;

                CategoryChipsPanel.Children.Add(chipBorder);
            }
        }

        private void RenderCategoryCards(PowerSnapshot snapshot)
        {
            CategoriesPanel.Children.Clear();

            foreach (var cat in snapshot.Categories)
            {
                var groupBorder = new Border
                {
                    Style = (Style)FindResource("CategoryBorderStyle"),
                    Padding = cat.HasActiveRequests ? new Thickness(14, 12, 14, 12) : new Thickness(14, 10, 14, 10),
                    Margin = cat.HasActiveRequests ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 8)
                };

                var groupStack = new StackPanel();

                // Category Header
                var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, cat.HasActiveRequests ? 8 : 0) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var headerLeft = new StackPanel();
                var titleBlock = new TextBlock
                {
                    Text = cat.Title,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = cat.HasActiveRequests
                        ? new SolidColorBrush(Color.FromRgb(248, 113, 113))
                        : new SolidColorBrush(Color.FromRgb(220, 220, 230))
                };
                var descBlock = new TextBlock
                {
                    Text = cat.Description,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 155)),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                headerLeft.Children.Add(titleBlock);
                headerLeft.Children.Add(descBlock);
                Grid.SetColumn(headerLeft, 0);

                var badgeBorder = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = cat.HasActiveRequests
                        ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                        : new SolidColorBrush(Color.FromRgb(48, 48, 60))
                };
                var badgeText = new TextBlock
                {
                    Text = cat.HasActiveRequests ? $"{cat.ActiveCount} Active" : "None",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = cat.HasActiveRequests
                        ? new SolidColorBrush(Colors.White)
                        : new SolidColorBrush(Color.FromRgb(150, 150, 165))
                };
                badgeBorder.Child = badgeText;
                Grid.SetColumn(badgeBorder, 1);

                headerGrid.Children.Add(headerLeft);
                headerGrid.Children.Add(badgeBorder);
                groupStack.Children.Add(headerGrid);

                // Category Content (Only rendered if there are active requests)
                if (cat.HasActiveRequests)
                {
                    foreach (var req in cat.Requests)
                    {
                        var reqBorder = new Border
                        {
                            Style = (Style)FindResource("RequestItemStyle")
                        };

                        var reqStack = new StackPanel();

                        // Top row: Type Tag + Requester Name + Counter
                        var topRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        // Tag color
                        Color tagBgColor = req.CallerType switch
                        {
                            RequesterType.KernelRequester => Color.FromRgb(109, 40, 217),
                            RequesterType.UserSharedServiceRequester => Color.FromRgb(13, 148, 136),
                            _ => Color.FromRgb(37, 99, 235)
                        };

                        var tagBorder = new Border
                        {
                            Background = new SolidColorBrush(tagBgColor),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            Margin = new Thickness(0, 0, 8, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var tagText = new TextBlock
                        {
                            Text = $"[{req.RequesterTypeName}]",
                            FontSize = 10,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Colors.White)
                        };
                        tagBorder.Child = tagText;
                        Grid.SetColumn(tagBorder, 0);

                        var nameBlock = new TextBlock
                        {
                            Text = req.DisplayHeader,
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Colors.White),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Grid.SetColumn(nameBlock, 1);

                        if (req.Count > 1)
                        {
                            var countBadgeReq = new Border
                            {
                                Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(5, 1, 5, 1),
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            var countTextReq = new TextBlock
                            {
                                Text = $"{req.Count} times",
                                FontSize = 10,
                                FontWeight = FontWeights.Bold,
                                Foreground = new SolidColorBrush(Colors.White)
                            };
                            countBadgeReq.Child = countTextReq;
                            Grid.SetColumn(countBadgeReq, 2);
                            topRow.Children.Add(countBadgeReq);
                        }

                        topRow.Children.Add(tagBorder);
                        topRow.Children.Add(nameBlock);
                        reqStack.Children.Add(topRow);

                        // Second row: Details / Path (if available)
                        if (!string.IsNullOrWhiteSpace(req.Details) && req.CallerType != RequesterType.UserSharedServiceRequester)
                        {
                            var detailsBlock = new TextBox
                            {
                                Text = req.Details,
                                FontSize = 11,
                                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 175)),
                                Background = Brushes.Transparent,
                                BorderThickness = new Thickness(0),
                                IsReadOnly = true,
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 0, 0, 4)
                            };
                            reqStack.Children.Add(detailsBlock);
                        }

                        // Third row: Reason callout
                        if (!string.IsNullOrWhiteSpace(req.Reason))
                        {
                            var reasonBorder = new Border
                            {
                                Background = new SolidColorBrush(Color.FromArgb(40, 20, 20, 30)),
                                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 80)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(8, 4, 8, 4),
                                Margin = new Thickness(0, 2, 0, 0)
                            };
                            var reasonText = new TextBlock
                            {
                                Text = $"💬 {req.Reason}",
                                FontSize = 11,
                                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 235)),
                                TextWrapping = TextWrapping.Wrap
                            };
                            reasonBorder.Child = reasonText;
                            reqStack.Children.Add(reasonBorder);
                        }

                        reqBorder.Child = reqStack;
                        groupStack.Children.Add(reqBorder);
                    }
                }

                groupBorder.Child = groupStack;
                CategoriesPanel.Children.Add(groupBorder);
            }
        }

        private async void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            await _monitorService.RefreshNowAsync();
        }

        private void OnCopyOutputClicked(object sender, RoutedEventArgs e)
        {
            if (_currentSnapshot != null)
            {
                Clipboard.SetText(_currentSnapshot.ToPowercfgOutput());
                MessageBox.Show("Power requests copied to clipboard in powercfg /requests format.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnAutoRefreshChanged(object sender, RoutedEventArgs e)
        {
            if (_monitorService != null && AutoRefreshCheckBox != null)
            {
                _monitorService.IsAutoRefreshEnabled = AutoRefreshCheckBox.IsChecked == true;
            }
        }

        private void OnIntervalSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_monitorService != null && RefreshIntervalComboBox?.SelectedItem is ComboBoxItem item)
            {
                string text = item.Content?.ToString() ?? "5s";
                int seconds = text switch
                {
                    "3s" => 3,
                    "5s" => 5,
                    "10s" => 10,
                    "30s" => 30,
                    _ => 5
                };
                _monitorService.Interval = TimeSpan.FromSeconds(seconds);
            }
        }

        private async void OnTryDemoClicked(object sender, RoutedEventArgs e)
        {
            _monitorService.IsDemoMode = true;
            await _monitorService.RefreshNowAsync();
        }

        private void OnRestartAsAdminClicked(object sender, RoutedEventArgs e)
        {
            RestartAsAdministrator();
        }

        public static void RestartAsAdministrator()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0],
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(processInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart with administrative privileges: {ex.Message}", "Elevation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnHideToTrayClicked(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}