using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeSwitcher.Core;

namespace ClaudeSwitcher.App;

public partial class MainWindow : Window
{
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshList();
        App.Current.Pipe.Connected += UpdateBridgeStatus;
        App.Current.Pipe.Disconnected += UpdateBridgeStatus;
        Closing += (_, e) =>
        {
            // Hide instead of close — keep the tray running.
            e.Cancel = true;
            Hide();
        };
    }

    public void RefreshList()
    {
        Dispatcher.Invoke(() =>
        {
            var store = App.Current.Repo.Load();
            RelaunchToggle.IsChecked = store.RelaunchApps;

            var active = store.Profiles.FirstOrDefault(p => p.Id == store.ActiveProfileId);
            ActiveLabel.Text = active is null ? "Active: —" : $"Active: {active.Name}";
            ActiveLabel.Foreground = active is null
                ? (Brush)FindResource("FgDim")
                : (Brush)FindResource("Green");

            ProfilesList.Items.Clear();
            if (store.Profiles.Count == 0)
            {
                ProfilesList.Items.Add(new TextBlock
                {
                    Text = "No profiles yet.\nLog in to one of your accounts, then click '+ Add Profile' below.",
                    Foreground = (Brush)FindResource("FgDim"),
                    Margin = new Thickness(0, 40, 0, 0),
                    TextAlignment = TextAlignment.Center
                });
            }
            else
            {
                foreach (var p in store.Profiles)
                    ProfilesList.Items.Add(BuildCard(p, isActive: store.ActiveProfileId == p.Id));
            }

            UpdateBridgeStatus();
        });
    }

    private UIElement BuildCard(Profile p, bool isActive)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("BgCard"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4, 0, 4),
            BorderBrush = isActive ? (Brush)FindResource("Green") : (Brush)FindResource("BgMid"),
            BorderThickness = new Thickness(2)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        var color = new Border
        {
            Background = (Brush)new BrushConverter().ConvertFromString(p.ColorHex)!,
            Width = 6,
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 2, 8, 2)
        };
        Grid.SetColumn(color, 0);
        Grid.SetRowSpan(color, 2);
        grid.Children.Add(color);

        var name = new TextBlock
        {
            Text = isActive ? $"{p.Name}  ✓ active" : p.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = isActive ? (Brush)FindResource("Green") : (Brush)FindResource("FgMain")
        };
        Grid.SetColumn(name, 1);
        Grid.SetRow(name, 0);
        grid.Children.Add(name);

        var meta = new TextBlock
        {
            Text = p.LastActivated is null
                ? $"added {p.Added:yyyy-MM-dd}"
                : $"last switched {p.LastActivated:yyyy-MM-dd HH:mm}",
            FontSize = 11,
            Foreground = (Brush)FindResource("FgDim")
        };
        Grid.SetColumn(meta, 1);
        Grid.SetRow(meta, 1);
        grid.Children.Add(meta);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 0)
        };
        Grid.SetColumn(buttons, 2);
        Grid.SetRowSpan(buttons, 2);
        grid.Children.Add(buttons);

        if (!isActive)
        {
            var switchBtn = new Button
            {
                Content = "Switch",
                Style = (Style)FindResource("AccentButton"),
                Margin = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            switchBtn.Click += async (_, _) => await DoSwitch(p.Id);
            buttons.Children.Add(switchBtn);
        }

        var capture = new Button
        {
            Content = isActive ? "Recapture" : "Capture",
            Style = (Style)FindResource("GhostButton"),
            ToolTip = "Save the current live state into this profile",
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        capture.Click += async (_, _) => await DoCapture(p.Id);
        buttons.Children.Add(capture);

        var rename = new Button
        {
            Content = "…",
            Style = (Style)FindResource("GhostButton"),
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        rename.Click += (_, _) => ShowContextMenu(rename, p);
        buttons.Children.Add(rename);

        border.Child = grid;
        return border;
    }

    private void ShowContextMenu(Button anchor, Profile p)
    {
        var menu = new ContextMenu();
        var renameItem = new MenuItem { Header = "Rename…" };
        renameItem.Click += (_, _) => DoRename(p);
        menu.Items.Add(renameItem);
        var deleteItem = new MenuItem { Header = "Delete profile" };
        deleteItem.Click += (_, _) => DoDelete(p);
        menu.Items.Add(deleteItem);
        menu.PlacementTarget = anchor;
        menu.IsOpen = true;
    }

    private async Task DoSwitch(Guid id)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            await App.Current.OnSwitchRequested(id);
        }
        finally { _busy = false; }
    }

    private async Task DoCapture(Guid id)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var results = await App.Current.Orchestrator.CaptureAsync(id, progress: null, CancellationToken.None);
            var ok = results.Count(r => r.Status == SwapStatus.Ok);
            App.Current.Tray.Notify("Profile captured", $"{ok} targets snapshotted");
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Capture failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _busy = false; }
    }

    private void DoRename(Profile p)
    {
        var name = PromptDialog.Show(this, "Rename profile", "New name:", p.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        var store = App.Current.Repo.Load();
        var match = store.Profiles.FirstOrDefault(x => x.Id == p.Id);
        if (match is null) return;
        match.Name = name.Trim();
        App.Current.Repo.Save(store);
        App.Current.Tray.Refresh();
        RefreshList();
    }

    private void DoDelete(Profile p)
    {
        var ok = MessageBox.Show(this,
            $"Delete profile \"{p.Name}\"? Its saved credentials snapshot will be removed from disk.",
            "Delete profile", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.OK) return;
        var store = App.Current.Repo.Load();
        App.Current.Repo.Delete(store, p.Id);
        App.Current.Tray.Refresh();
        RefreshList();
    }

    private async void OnAddClicked(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var name = PromptDialog.Show(this, "Add profile",
            "Name (e.g. \"Personal\" or \"Work\"):\n\nYour current Claude logins will be captured into this profile.", "");
        if (string.IsNullOrWhiteSpace(name)) return;

        _busy = true;
        try
        {
            var store = App.Current.Repo.Load();
            var profile = App.Current.Repo.Add(store, name.Trim(), PickColor(store.Profiles.Count));
            var results = await App.Current.Orchestrator.CaptureAsync(profile.Id, progress: null, CancellationToken.None);

            // Mark this profile as the active one (it represents what's live right now).
            var fresh = App.Current.Repo.Load();
            fresh.ActiveProfileId = profile.Id;
            fresh.Profiles.First(p => p.Id == profile.Id).LastActivated = DateTimeOffset.UtcNow;
            App.Current.Repo.Save(fresh);

            var ok = results.Count(r => r.Status == SwapStatus.Ok);
            var failed = results.Count(r => r.Status == SwapStatus.Failed);
            App.Current.Tray.Notify("Profile added",
                $"\"{name}\" captured: {ok} ok, {failed} failed",
                failed == 0 ? System.Windows.Forms.ToolTipIcon.Info : System.Windows.Forms.ToolTipIcon.Warning);
            App.Current.Tray.Refresh();
            RefreshList();
        }
        finally { _busy = false; }
    }

    private void OnRelaunchChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var store = App.Current.Repo.Load();
        store.RelaunchApps = RelaunchToggle.IsChecked == true;
        App.Current.Repo.Save(store);
    }

    private void UpdateBridgeStatus()
    {
        Dispatcher.Invoke(() =>
        {
            BridgeLabel.Text = App.Current.Pipe.IsConnected
                ? "Browser bridge: connected"
                : "Browser bridge: disconnected (install the Chrome extension to switch tabs)";
            BridgeLabel.Foreground = App.Current.Pipe.IsConnected
                ? (Brush)FindResource("Green")
                : (Brush)FindResource("FgDim");
        });
    }

    private static string PickColor(int index)
    {
        var palette = new[] { "#D97706", "#3B82F6", "#22C55E", "#EC4899", "#A855F7", "#F59E0B", "#06B6D4" };
        return palette[index % palette.Length];
    }
}
