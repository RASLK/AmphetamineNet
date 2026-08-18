using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AmphetamineNet.Native;
using AmphetamineNet.Services;
using AmphetamineNet.ViewModels;
using AmphetamineNet.Views;

namespace AmphetamineNet;

/// <summary>
/// Avalonia application that owns the tray icon and menu
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// macOS keep-awake service
    /// </summary>
    private MacKeepAwakeService? _keepAwake;

    /// <summary>
    /// Main session view model
    /// </summary>
    private MainViewModel? _mainViewModel;

    /// <summary>
    /// Tray menu view model
    /// </summary>
    private TrayViewModel? _trayViewModel;

    /// <summary>
    /// Classic desktop application lifetime
    /// </summary>
    private IClassicDesktopStyleApplicationLifetime? _desktop;

    /// <summary>
    /// Menu-bar tray icon
    /// </summary>
    private TrayIcon? _trayIcon;

    /// <summary>
    /// Tray menu status header item
    /// </summary>
    private NativeMenuItem? _statusItem;

    /// <summary>
    /// Start or stop session menu item
    /// </summary>
    private NativeMenuItem? _toggleItem;

    /// <summary>
    /// Timer submenu root item
    /// </summary>
    private NativeMenuItem? _timerRoot;

    /// <summary>
    /// Modifiers submenu root item
    /// </summary>
    private NativeMenuItem? _modifiersRoot;

    /// <summary>
    /// Language submenu root item
    /// </summary>
    private NativeMenuItem? _languageRoot;

    /// <summary>
    /// Closed-lid modifier menu item
    /// </summary>
    private NativeMenuItem? _closedLidItem;

    /// <summary>
    /// Display-awake modifier menu item
    /// </summary>
    private NativeMenuItem? _displayItem;

    /// <summary>
    /// Custom duration menu item
    /// </summary>
    private NativeMenuItem? _customDurationItem;

    /// <summary>
    /// Quit application menu item
    /// </summary>
    private NativeMenuItem? _quitItem;

    /// <summary>
    /// Duration menu items keyed by minutes
    /// </summary>
    private readonly Dictionary<int, NativeMenuItem> _durationItems = new();

    /// <summary>
    /// Language menu items keyed by code
    /// </summary>
    private readonly Dictionary<string, NativeMenuItem> _languageItems = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cache key for the current tray icon state
    /// </summary>
    private string _lastIconKey = "";

    /// <summary>
    /// Last applied selection state per menu item, to skip redundant icon rebuilds
    /// </summary>
    private readonly Dictionary<NativeMenuItem, bool> _selectionStates = new();

    /// <summary>
    /// Loads Avalonia XAML resources
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Creates services and the tray UI on startup
    /// </summary>
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
                        Text = Localization.T("os.unsupported"),
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
                Localization.SetLanguage(settings.Language);

                _keepAwake = new MacKeepAwakeService();
                _keepAwake.PrepareForAdminPrompt = PrepareAdminUi;
                _keepAwake.FinishAdminPrompt = FinishAdminUi;
                _mainViewModel = new MainViewModel(_keepAwake, settings);
                _trayViewModel = new TrayViewModel(this, _mainViewModel);
                DataContext = _trayViewModel;

                SetupTrayIcon(_trayViewModel);

                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                desktop.Exit += (_, _) => Cleanup();

                Localization.LanguageChanged += OnLanguageChanged;

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

    /// <summary>
    /// Applies UI language changes
    /// </summary>
    /// <param name="sender">Event source</param>
    /// <param name="e">Event data</param>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UiDispatch.Post(() =>
        {
            if (_trayViewModel is null)
                return;
            try
            {
                RelocalizeTrayMenu(_trayViewModel);
                UpdateTrayIcon(_trayViewModel);
            }
            catch (Exception ex)
            {
                AppLog.Write($"language menu update error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Creates the tray icon and wires refresh handlers
    /// </summary>
    /// <param name="trayVm">Tray view model</param>
    private void SetupTrayIcon(TrayViewModel trayVm)
    {
        TrayIcon.SetIcons(this, null);

        _trayIcon = new TrayIcon
        {
            ToolTipText = trayVm.TrayToolTip,
            IsVisible = true,
        };
        UpdateTrayIcon(trayVm);

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
                UpdateTrayIcon(_trayViewModel);
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

    /// <summary>
    /// Builds the full tray native menu
    /// </summary>
    /// <param name="trayVm">Tray view model</param>
    private void BuildTrayMenu(TrayViewModel trayVm)
    {
        if (_trayIcon is null)
            return;

        // Replacing TrayIcon.Menu after the first assign crashes Avalonia.Native on macOS.
        if (_trayIcon.Menu is not null)
        {
            RelocalizeTrayMenu(trayVm);
            return;
        }

        var menu = new NativeMenu();
        _durationItems.Clear();
        _languageItems.Clear();

        _statusItem = new NativeMenuItem(trayVm.SessionHeader) { IsEnabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new NativeMenuItemSeparator());

        // Primary action first, per macOS menu-bar conventions.
        _toggleItem = new NativeMenuItem(trayVm.ToggleSessionHeader)
        {
            Command = trayVm.ToggleSessionCommand,
        };
        menu.Items.Add(_toggleItem);

        menu.Items.Add(new NativeMenuItemSeparator());

        _timerRoot = new NativeMenuItem(Localization.T("menu.timer"))
        {
            Menu = new NativeMenu(),
        };
        menu.Items.Add(_timerRoot);

        _modifiersRoot = new NativeMenuItem(Localization.T("menu.modifiers"))
        {
            Menu = new NativeMenu(),
        };
        menu.Items.Add(_modifiersRoot);

        menu.Items.Add(new NativeMenuItemSeparator());

        _languageRoot = new NativeMenuItem(Localization.T("menu.language"))
        {
            Menu = new NativeMenu(),
        };
        menu.Items.Add(_languageRoot);

        menu.Items.Add(new NativeMenuItemSeparator());

        _quitItem = new NativeMenuItem(Localization.T("menu.quit"))
        {
            Command = trayVm.ExitCommand,
        };
        menu.Items.Add(_quitItem);

        _trayIcon.Menu = menu;
        RelocalizeTrayMenu(trayVm);
    }

    /// <summary>
    /// Rebuilds submenu contents and headers without replacing TrayIcon.Menu
    /// </summary>
    /// <param name="trayVm">Tray view model</param>
    private void RelocalizeTrayMenu(TrayViewModel trayVm)
    {
        // Prefer in-place header/icon updates. Replacing TrayIcon.Menu crashes Avalonia.Native.
        if (_timerRoot?.Menu is { } timerMenu && (_durationItems.Count == 0 || NeedsTimerRebuild(trayVm)))
            FillTimerMenu(timerMenu, trayVm);

        if (_modifiersRoot?.Menu is { } modifiersMenu && _closedLidItem is null)
            FillModifiersMenu(modifiersMenu, trayVm);

        if (_languageRoot?.Menu is { } languageMenu && _languageItems.Count == 0)
            FillLanguageMenu(languageMenu, trayVm);

        RefreshTrayMenu(trayVm);
    }

    /// <summary>
    /// Fills the Timer submenu
    /// </summary>
    /// <param name="submenu">Target submenu</param>
    /// <param name="trayVm">Tray view model</param>
    private void FillTimerMenu(NativeMenu submenu, TrayViewModel trayVm)
    {
        submenu.Items.Clear();
        foreach (var stale in _durationItems.Values)
            _selectionStates.Remove(stale);
        _durationItems.Clear();

        foreach (var minutes in MainViewModel.PresetDurations)
            AddDuration(submenu, trayVm, minutes);

        if (trayVm.CustomDurationMinutes is { } custom &&
            custom > 0 &&
            !MainViewModel.PresetDurations.Contains(custom))
        {
            AddDuration(submenu, trayVm, custom);
        }

        submenu.Items.Add(new NativeMenuItemSeparator());

        _customDurationItem = new NativeMenuItem(Localization.T("menu.custom_time"))
        {
            Command = trayVm.PromptCustomDurationCommand,
        };
        submenu.Items.Add(_customDurationItem);
    }

    /// <summary>
    /// Fills the Modifiers submenu
    /// </summary>
    /// <param name="submenu">Target submenu</param>
    /// <param name="trayVm">Tray view model</param>
    private void FillModifiersMenu(NativeMenu submenu, TrayViewModel trayVm)
    {
        submenu.Items.Clear();

        _closedLidItem = new NativeMenuItem(Localization.T("mod.closed_lid"))
        {
            Command = trayVm.ToggleClosedLidCommand,
        };
        SetSelectionIcon(_closedLidItem, trayVm.AllowClosedLid);
        submenu.Items.Add(_closedLidItem);

        _displayItem = new NativeMenuItem(Localization.T("mod.display"))
        {
            Command = trayVm.ToggleDisplaySleepCommand,
        };
        SetSelectionIcon(_displayItem, trayVm.PreventDisplaySleep);
        submenu.Items.Add(_displayItem);
    }

    /// <summary>
    /// Fills the Language submenu
    /// </summary>
    /// <param name="submenu">Target submenu</param>
    /// <param name="trayVm">Tray view model</param>
    private void FillLanguageMenu(NativeMenu submenu, TrayViewModel trayVm)
    {
        submenu.Items.Clear();
        foreach (var stale in _languageItems.Values)
            _selectionStates.Remove(stale);
        _languageItems.Clear();

        var current = Localization.CurrentLanguage;
        foreach (var lang in Localization.Languages)
        {
            var selected = current.Equals(lang.Code, StringComparison.OrdinalIgnoreCase);
            var item = new NativeMenuItem(lang.NativeName)
            {
                Command = trayVm.SelectLanguageCommand,
                CommandParameter = lang.Code,
            };
            SetSelectionIcon(item, selected);
            _languageItems[lang.Code] = item;
            submenu.Items.Add(item);
        }
    }

    /// <summary>
    /// Updates tray menu headers, icons, and checks
    /// </summary>
    /// <param name="trayVm">Tray view model</param>
    private void RefreshTrayMenu(TrayViewModel trayVm)
    {
        if (_statusItem is not null)
            _statusItem.Header = trayVm.SessionHeader;
        if (_toggleItem is not null)
            _toggleItem.Header = trayVm.ToggleSessionHeader;
        if (_timerRoot is not null)
            _timerRoot.Header = Localization.T("menu.timer");
        if (_modifiersRoot is not null)
            _modifiersRoot.Header = Localization.T("menu.modifiers");
        if (_languageRoot is not null)
            _languageRoot.Header = Localization.T("menu.language");
        if (_customDurationItem is not null)
            _customDurationItem.Header = Localization.T("menu.custom_time");
        if (_quitItem is not null)
            _quitItem.Header = Localization.T("menu.quit");

        // Rebuild timer items in-place when the remembered custom duration changes.
        if (_timerRoot?.Menu is { } timerMenu && NeedsTimerRebuild(trayVm))
            FillTimerMenu(timerMenu, trayVm);

        foreach (var (minutes, item) in _durationItems)
        {
            item.Header = Localization.FormatDuration(minutes);
            SetSelectionIcon(item, trayVm.IsDurationSelected(minutes));
        }

        if (_closedLidItem is not null)
        {
            _closedLidItem.Header = Localization.T("mod.closed_lid");
            SetSelectionIcon(_closedLidItem, trayVm.AllowClosedLid);
        }

        if (_displayItem is not null)
        {
            _displayItem.Header = Localization.T("mod.display");
            SetSelectionIcon(_displayItem, trayVm.PreventDisplaySleep);
        }

        var currentLanguage = Localization.CurrentLanguage;
        foreach (var (code, item) in _languageItems)
            SetSelectionIcon(item, currentLanguage.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Detects whether the Timer submenu must be rebuilt
    /// </summary>
    /// <param name="trayVm">Tray view model</param>
    /// <returns>True when the timer submenu is stale</returns>
    private bool NeedsTimerRebuild(TrayViewModel trayVm)
    {
        var custom = trayVm.CustomDurationMinutes;
        if (custom is { } c && c > 0 && !MainViewModel.PresetDurations.Contains(c))
            return !_durationItems.ContainsKey(c);

        foreach (var key in _durationItems.Keys)
        {
            if (!MainViewModel.PresetDurations.Contains(key))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds a duration item to a native menu
    /// </summary>
    /// <param name="menu">Target native menu</param>
    /// <param name="trayVm">Tray view model</param>
    /// <param name="minutes">Duration in minutes</param>
    private void AddDuration(NativeMenu menu, TrayViewModel trayVm, int minutes)
    {
        var item = new NativeMenuItem(Localization.FormatDuration(minutes))
        {
            Command = trayVm.SelectDurationCommand,
            CommandParameter = minutes.ToString(),
        };
        SetSelectionIcon(item, trayVm.IsDurationSelected(minutes));
        _durationItems[minutes] = item;
        menu.Items.Add(item);
    }

    /// <summary>
    /// Applies the selection indicator to a menu item, skipping unchanged states
    /// </summary>
    /// <param name="item">Target menu item</param>
    /// <param name="selected">Whether the item is selected</param>
    private void SetSelectionIcon(NativeMenuItem item, bool selected)
    {
        if (_selectionStates.TryGetValue(item, out var last) && last == selected && item.Icon is not null)
            return;

        _selectionStates[item] = selected;
        // Fresh instances avoid macOS native-menu icon sharing glitches.
        item.Icon = TrayIconPainter.CreateSelectionDot(selected);
    }

    /// <summary>
    /// Rebuilds the tray icon when session state changes
    /// </summary>
    /// <param name="trayVm">Tray view model</param>
    private void UpdateTrayIcon(TrayViewModel trayVm)
    {
        if (_trayIcon is null)
            return;

        var active = trayVm.Main.IsSessionActive;
        var timed = trayVm.Main.IsTimedSession;
        var lid = trayVm.AllowClosedLid;
        var display = trayVm.PreventDisplaySleep;
        var key = $"{active}:{timed}:{lid}:{display}";
        if (key == _lastIconKey && _trayIcon.Icon is not null)
            return;

        _lastIconKey = key;
        _trayIcon.Icon = TrayIconPainter.CreateTrayIcon(active, timed, lid, display);
    }

    /// <summary>
    /// Activates the app for an admin password prompt
    /// </summary>
    public void PrepareAdminUi()
    {
        UiDispatch.Invoke(MacAppActivation.ActivateForAdminPrompt);
    }

    /// <summary>
    /// Returns the app to accessory mode after an admin prompt
    /// </summary>
    public void FinishAdminUi()
    {
        UiDispatch.Post(MacAppActivation.ReturnToAccessory);
    }

    /// <summary>
    /// Opens the custom duration prompt
    /// </summary>
    public async void PromptCustomDuration()
    {
        if (_mainViewModel is null || _desktop is null)
            return;

        try
        {
            MacAppActivation.ActivateForAdminPrompt();

            var dialog = new CustomDurationWindow(_mainViewModel.CustomDurationMinutes);
            var closed = new TaskCompletionSource();
            dialog.Closed += (_, _) => closed.TrySetResult();
            dialog.Show();
            await closed.Task;

            if (dialog.ResultMinutes is { } minutes)
            {
                _mainViewModel.SetCustomDurationAndStart(minutes);
                if (_trayViewModel is not null)
                {
                    RelocalizeTrayMenu(_trayViewModel);
                    UpdateTrayIcon(_trayViewModel);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"custom duration prompt error: {ex.Message}");
        }
        finally
        {
            MacAppActivation.ReturnToAccessory();
        }
    }

    /// <summary>
    /// Shows a one-shot launch notification
    /// </summary>
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
                    $"display notification \"{EscapeAppleScript(Localization.T("notify.body"))}\" with title \"AmphetamineNet\"",
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

    /// <summary>
    /// Escapes text for an AppleScript string literal
    /// </summary>
    /// <param name="text">Raw text</param>
    /// <returns>Escaped AppleScript text</returns>
    private static string EscapeAppleScript(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// Cleans up and shuts down the application
    /// </summary>
    public void ExitApplication()
    {
        Cleanup();
        _desktop?.Shutdown();
    }

    /// <summary>
    /// Disposes tray, view models, and keep-awake resources
    /// </summary>
    private void Cleanup()
    {
        Localization.LanguageChanged -= OnLanguageChanged;

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
    }
}
