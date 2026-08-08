using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AmphetamineNet.Native;
using AmphetamineNet.Services;
using AmphetamineNet.ViewModels;
using AmphetamineNet.Views;

namespace AmphetamineNet;

public partial class App : Application
{
    private MacKeepAwakeService? _keepAwake;
    private MainViewModel? _mainViewModel;
    private TrayViewModel? _trayViewModel;
    private MainWindow? _mainWindow;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _statusItem;
    private NativeMenuItem? _toggleItem;
    private NativeMenuItem? _closedLidItem;
    private NativeMenuItem? _displayItem;
    private readonly Dictionary<int, NativeMenuItem> _durationItems = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                desktop.MainWindow = new Window
                {
                    Title = "AmphetamineNet",
                    Width = 420,
                    Height = 160,
                    Content = new TextBlock
                    {
                        Text = "This app only works on macOS.",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        Margin = new Thickness(24),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    },
                };
                base.OnFrameworkInitializationCompleted();
                return;
            }

            try
            {
                AppLog.Write("start");

                var settings = AppSettings.Load();
                settings.LaunchMinimized = true;

                _keepAwake = new MacKeepAwakeService();
                _keepAwake.PrepareForAdminPrompt = PrepareAdminUi;
                _keepAwake.FinishAdminPrompt = FinishAdminUi;
                _mainViewModel = new MainViewModel(_keepAwake, settings);
                _trayViewModel = new TrayViewModel(this, _mainViewModel);
                DataContext = _trayViewModel;

                // Important: the TrayIcon from XAML is created before DataContext — its bindings are dead.
                // We build the tray manually after the VM is ready.
                SetupTrayIcon(_trayViewModel);

                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                desktop.Exit += (_, _) => Cleanup();

                NotifyStarted();
                AppLog.Write("tray ready");
            }
            catch (Exception ex)
            {
                AppLog.Write($"FATAL: {ex}");
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(TrayViewModel trayVm)
    {
        TrayIcon.SetIcons(this, null);

        _trayIcon = new TrayIcon
        {
            ToolTipText = trayVm.TrayToolTip,
            IsVisible = true,
            Icon = LoadTrayIcon(),
        };

        BuildTrayMenu(trayVm);
        RefreshTrayMenu(trayVm);

        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        debounce.Tick += (_, _) =>
        {
            debounce.Stop();
            if (_trayIcon is null || _trayViewModel is null)
                return;
            try
            {
                _trayIcon.ToolTipText = _trayViewModel.TrayToolTip;
                RefreshTrayMenu(_trayViewModel);
            }
            catch (Exception ex)
            {
                AppLog.Write($"tray update error: {ex.Message}");
            }
        };

        trayVm.PropertyChanged += (_, _) =>
        {
            if (_trayIcon is null)
                return;
            debounce.Stop();
            debounce.Start();
        };

        TrayIcon.SetIcons(this, [_trayIcon]);
    }

    private void BuildTrayMenu(TrayViewModel trayVm)
    {
        if (_trayIcon is null)
            return;

        var menu = new NativeMenu();
        _durationItems.Clear();

        _statusItem = new NativeMenuItem(trayVm.SessionHeader) { IsEnabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new NativeMenuItemSeparator());

        _toggleItem = new NativeMenuItem(trayVm.ToggleSessionHeader)
        {
            Command = trayVm.ToggleSessionCommand,
        };
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new NativeMenuItemSeparator());

        AddDuration(menu, trayVm, "Indefinitely", 0);
        AddDuration(menu, trayVm, "5 minutes", 5);
        AddDuration(menu, trayVm, "15 minutes", 15);
        AddDuration(menu, trayVm, "30 minutes", 30);
        AddDuration(menu, trayVm, "1 hour", 60);
        AddDuration(menu, trayVm, "2 hours", 120);
        AddDuration(menu, trayVm, "5 hours", 300);

        menu.Items.Add(new NativeMenuItemSeparator());

        _closedLidItem = new NativeMenuItem("Allow closed lid")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = trayVm.AllowClosedLid,
            Command = trayVm.ToggleClosedLidCommand,
        };
        menu.Items.Add(_closedLidItem);

        _displayItem = new NativeMenuItem("Keep display awake")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = trayVm.PreventDisplaySleep,
            Command = trayVm.ToggleDisplaySleepCommand,
        };
        menu.Items.Add(_displayItem);

        menu.Items.Add(new NativeMenuItemSeparator());

        menu.Items.Add(new NativeMenuItem("Preferences…") { Command = trayVm.ShowPreferencesCommand });
        menu.Items.Add(new NativeMenuItem("Quit") { Command = trayVm.ExitCommand });

        _trayIcon.Menu = menu;
    }

    private void RefreshTrayMenu(TrayViewModel trayVm)
    {
        if (_statusItem is not null)
            _statusItem.Header = trayVm.SessionHeader;
        if (_toggleItem is not null)
            _toggleItem.Header = trayVm.ToggleSessionHeader;

        foreach (var (minutes, item) in _durationItems)
        {
            item.IsChecked = minutes switch
            {
                0 => trayVm.IsIndefinite,
                5 => trayVm.Is5Min,
                15 => trayVm.Is15Min,
                30 => trayVm.Is30Min,
                60 => trayVm.Is1Hour,
                120 => trayVm.Is2Hours,
                300 => trayVm.Is5Hours,
                _ => false,
            };
        }

        if (_closedLidItem is not null)
        {
            _closedLidItem.Header = "Allow closed lid";
            _closedLidItem.IsChecked = trayVm.AllowClosedLid;
        }

        if (_displayItem is not null)
        {
            _displayItem.Header = "Keep display awake";
            _displayItem.IsChecked = trayVm.PreventDisplaySleep;
        }

    }

    private void AddDuration(NativeMenu menu, TrayViewModel trayVm, string title, int minutes)
    {
        var item = new NativeMenuItem(title)
        {
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = false,
            Command = trayVm.SelectDurationCommand,
            CommandParameter = minutes.ToString(),
        };
        _durationItems[minutes] = item;
        menu.Items.Add(item);
    }

    public void PrepareAdminUi()
    {
        UiDispatch.Invoke(MacAppActivation.ActivateForAdminPrompt);
    }

    public void FinishAdminUi()
    {
        UiDispatch.Post(() =>
        {
            if (_mainWindow is { IsVisible: true })
                return;

            MacAppActivation.ReturnToAccessory();
        });
    }

    private static WindowIcon LoadTrayIcon()
    {
        try
        {
            var uri = new Uri("avares://AmphetamineNet/Assets/tray.png");
            using var stream = AssetLoader.Open(uri);
            return new WindowIcon(stream);
        }
        catch
        {
            var uri = new Uri("avares://AmphetamineNet/Assets/avalonia-logo.ico");
            using var stream = AssetLoader.Open(uri);
            return new WindowIcon(stream);
        }
    }

    private static void NotifyStarted()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                ArgumentList =
                {
                    "-e",
                    "display notification \"The menu bar icon is at the top right. Start a session from there.\" with title \"AmphetamineNet\"",
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            System.Diagnostics.Process.Start(psi)?.Dispose();
        }
        catch
        {
            // ignore
        }
    }

    public void ShowMainWindow()
    {
        if (_mainViewModel is null || _desktop is null)
            return;

        MacAppActivation.ActivateForAdminPrompt();

        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow
            {
                DataContext = _mainViewModel,
            };
            _desktop.MainWindow = _mainWindow;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
        _mainWindow.WindowState = WindowState.Normal;
    }

    public void ExitApplication()
    {
        _mainWindow?.AllowClose();
        Cleanup();
        _desktop?.Shutdown();
    }

    private void Cleanup()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
            TrayIcon.SetIcons(this, null);
        }

        _mainViewModel?.Dispose();
        _mainViewModel = null;
        _trayViewModel = null;
        _keepAwake?.Dispose();
        _keepAwake = null;
        _mainWindow = null;
    }
}
