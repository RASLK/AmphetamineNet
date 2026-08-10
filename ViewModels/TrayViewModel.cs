using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AmphetamineNet.Services;
using AmphetamineNet.ViewModels;

namespace AmphetamineNet;

/// <summary>
/// View model that drives the tray menu and tooltip
/// </summary>
public sealed partial class TrayViewModel : ObservableObject
{
    /// <summary>
    /// Application instance
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// Main session view model
    /// </summary>
    private readonly MainViewModel _main;

    /// <summary>
    /// Debounce timer for tray menu refresh
    /// </summary>
    private readonly DispatcherTimer _debounce;

    /// <summary>
    /// Creates the tray menu view model
    /// </summary>
    /// <param name="app">Application instance</param>
    /// <param name="main">Main session view model</param>
    public TrayViewModel(App app, MainViewModel main)
    {
        _app = app;
        _main = main;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            NotifyMenuStateCore();
        };

        _main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.IsSessionActive)
                or nameof(MainViewModel.SelectedDuration)
                or nameof(MainViewModel.SelectedDurationMinutes)
                or nameof(MainViewModel.AllowClosedLid)
                or nameof(MainViewModel.PreventDisplaySleep)
                or nameof(MainViewModel.StatusText)
                or nameof(MainViewModel.LidStatusText)
                or nameof(MainViewModel.RemainingText)
                or nameof(MainViewModel.Language)
                or nameof(MainViewModel.CustomDurationMinutes)
                or nameof(MainViewModel.IsTimedSession))
            {
                ScheduleNotify();
            }
        };

        Localization.LanguageChanged += (_, _) => ScheduleNotify();
        NotifyMenuStateCore();
    }

    /// <summary>
    /// Underlying session view model
    /// </summary>
    /// <value>Main view model</value>
    public MainViewModel Main => _main;

    /// <summary>
    /// Status line shown at the top of the tray menu
    /// </summary>
    /// <value>Localized session header</value>
    public string SessionHeader
    {
        get
        {
            if (!_main.IsSessionActive)
                return $"○ {Localization.T("status.inactive")}";

            if (_main.SelectedDurationMinutes > 0)
            {
                var remaining = string.IsNullOrEmpty(_main.RemainingText)
                    ? Localization.FormatDuration(_main.SelectedDurationMinutes)
                    : _main.RemainingText;
                return $"● {Localization.T("status.active")} · {remaining}";
            }

            return $"● {Localization.T("status.active")} · {Localization.T("duration.indefinitely")}";
        }
    }

    /// <summary>
    /// Label for the start or stop menu item
    /// </summary>
    /// <value>Localized toggle label</value>
    public string ToggleSessionHeader => _main.IsSessionActive
        ? Localization.T("menu.stop")
        : Localization.T("menu.start");

    /// <summary>
    /// Tray icon tooltip text
    /// </summary>
    /// <value>Localized tooltip</value>
    public string TrayToolTip => _main.IsSessionActive
        ? string.Format(
            Localization.T("tooltip.active"),
            _main.SelectedDurationMinutes > 0 && !string.IsNullOrEmpty(_main.RemainingText)
                ? _main.RemainingText
                : _main.SelectedDuration.Title)
        : Localization.T("tooltip.inactive");

    /// <summary>
    /// Closed-lid keep-awake option
    /// </summary>
    /// <value>True when closed-lid mode is enabled</value>
    public bool AllowClosedLid
    {
        get => _main.AllowClosedLid;
        set
        {
            if (_main.AllowClosedLid == value)
                return;
            _main.AllowClosedLid = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Display-awake option
    /// </summary>
    /// <value>True when display sleep is blocked</value>
    public bool PreventDisplaySleep
    {
        get => _main.PreventDisplaySleep;
        set
        {
            if (_main.PreventDisplaySleep == value)
                return;
            _main.PreventDisplaySleep = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Selected UI language
    /// </summary>
    /// <value>Language code</value>
    public string Language
    {
        get => _main.Language;
        set
        {
            if (_main.Language == value)
                return;
            _main.Language = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Remembered custom timer duration
    /// </summary>
    /// <value>Custom duration in minutes, if any</value>
    public int? CustomDurationMinutes => _main.CustomDurationMinutes;

    /// <summary>
    /// Checks whether a duration is currently selected
    /// </summary>
    /// <param name="minutes">Duration in minutes</param>
    /// <returns>True when the duration is selected</returns>
    public bool IsDurationSelected(int minutes) => _main.SelectedDurationMinutes == minutes;

    /// <summary>
    /// Starts or stops the keep-awake session
    /// </summary>
    [RelayCommand]
    private void ToggleSession() => _main.ToggleSessionCommand.Execute(null);

    /// <summary>
    /// Starts a session with the chosen duration
    /// </summary>
    /// <param name="minutesText">Duration in minutes as text</param>
    [RelayCommand]
    private void SelectDuration(string? minutesText)
    {
        if (!int.TryParse(minutesText, out var minutes))
            return;

        _main.StartWithDurationCommand.Execute(minutes);
        ScheduleNotify();
    }

    /// <summary>
    /// Toggles the closed-lid modifier
    /// </summary>
    [RelayCommand]
    private void ToggleClosedLid()
    {
        AllowClosedLid = !AllowClosedLid;
        ScheduleNotify();
    }

    /// <summary>
    /// Toggles the display-awake modifier
    /// </summary>
    [RelayCommand]
    private void ToggleDisplaySleep()
    {
        PreventDisplaySleep = !PreventDisplaySleep;
        ScheduleNotify();
    }

    /// <summary>
    /// Changes the UI language
    /// </summary>
    /// <param name="code">Language code</param>
    [RelayCommand]
    private void SelectLanguage(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;
        Language = Localization.Normalize(code);
        ScheduleNotify();
    }

    /// <summary>
    /// Opens the custom duration prompt
    /// </summary>
    [RelayCommand]
    private void PromptCustomDuration() => _app.PromptCustomDuration();

    /// <summary>
    /// Exits the application
    /// </summary>
    [RelayCommand]
    private void Exit() => _app.ExitApplication();

    /// <summary>
    /// Schedules a debounced tray menu refresh
    /// </summary>
    private void ScheduleNotify()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>
    /// Raises property changes used by the tray menu
    /// </summary>
    private void NotifyMenuStateCore()
    {
        OnPropertyChanged(nameof(SessionHeader));
        OnPropertyChanged(nameof(ToggleSessionHeader));
        OnPropertyChanged(nameof(TrayToolTip));
        OnPropertyChanged(nameof(AllowClosedLid));
        OnPropertyChanged(nameof(PreventDisplaySleep));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(CustomDurationMinutes));
    }
}
