using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AmphetamineNet.Services;

namespace AmphetamineNet.ViewModels;

/// <summary>
/// Represents a selectable session duration
/// </summary>
/// <param name="Minutes">Duration in minutes</param>
public sealed record DurationOption(int Minutes)
{
    /// <summary>
    /// Localized duration label
    /// </summary>
    /// <value>Display title</value>
    public string Title => Localization.FormatDuration(Minutes);

    /// <summary>
    /// Returns the localized duration title
    /// </summary>
    /// <returns>Duration title</returns>
    public override string ToString() => Title;
}

/// <summary>
/// View model for keep-awake session state and settings
/// </summary>
public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Built-in timer durations in minutes
    /// </summary>
    public static readonly int[] PresetDurations = [0, 5, 15, 30, 60, 120, 300, 480];

    /// <summary>
    /// macOS keep-awake service
    /// </summary>
    private readonly MacKeepAwakeService _keepAwake;

    /// <summary>
    /// Persisted application settings
    /// </summary>
    private readonly AppSettings _settings;

    /// <summary>
    /// Timer that refreshes lid status
    /// </summary>
    private readonly DispatcherTimer? _lidTimer;

    /// <summary>
    /// Timer that updates the session countdown
    /// </summary>
    private readonly DispatcherTimer? _countdownTimer;

    /// <summary>
    /// Whether the object has been disposed
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Last published detail text
    /// </summary>
    private string _lastDetail = "";

    /// <summary>
    /// Last published error text
    /// </summary>
    private string? _lastError;

    /// <summary>
    /// Last known lid-closed state
    /// </summary>
    private bool? _lastLid;

    /// <summary>
    /// Last known session active state
    /// </summary>
    private bool _lastActive;

    /// <summary>
    /// Last published countdown text
    /// </summary>
    private string _lastRemainingKey = "";

    /// <summary>
    /// Creates the main session view model
    /// </summary>
    /// <param name="keepAwake">macOS keep-awake service</param>
    /// <param name="settings">Loaded application settings</param>
    public MainViewModel(MacKeepAwakeService keepAwake, AppSettings settings)
    {
        _keepAwake = keepAwake;
        _settings = settings;
        _keepAwake.StateChanged += OnKeepAwakeStateChanged;

        Localization.SetLanguage(settings.Language);

        RebuildDurations();
        SelectedDurationMinutes = ResolveInitialDuration(settings);
        AllowClosedLid = settings.AllowClosedLid;
        PreventDisplaySleep = settings.PreventDisplaySleep;
        CustomDurationMinutes = settings.CustomDurationMinutes;

        // Set language last so PersistSettings writes the loaded code, not the default.
        var language = Localization.Normalize(settings.Language);
        if (string.Equals(Language, language, StringComparison.OrdinalIgnoreCase))
        {
            Localization.SetLanguage(language);
        }
        else
        {
            Language = language;
        }

        _lidTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _lidTimer.Tick += (_, _) => RefreshLidStatusOnly();
        _lidTimer.Start();

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => TickCountdown();
        _countdownTimer.Start();

        RefreshStatus(force: true);

        if (settings.StartWithSession)
            StartSession();
    }

    /// <summary>
    /// Available duration options for the UI
    /// </summary>
    /// <value>Observable duration list</value>
    public ObservableCollection<DurationOption> Durations { get; } = [];

    /// <summary>
    /// Currently selected duration in minutes
    /// </summary>
    /// <value>Duration in minutes</value>
    public int SelectedDurationMinutes { get; private set; }

    /// <summary>
    /// Currently selected duration option
    /// </summary>
    /// <value>Selected duration option</value>
    public DurationOption SelectedDuration =>
        Durations.FirstOrDefault(d => d.Minutes == SelectedDurationMinutes)
        ?? Durations.FirstOrDefault()
        ?? new DurationOption(0);

    /// <summary>
    /// Keeps the Mac awake with the lid closed
    /// </summary>
    /// <value>True when closed-lid keep-awake is enabled</value>
    [ObservableProperty]
    public partial bool AllowClosedLid { get; set; }

    /// <summary>
    /// Prevents the display from sleeping
    /// </summary>
    /// <value>True when display sleep is blocked</value>
    [ObservableProperty]
    public partial bool PreventDisplaySleep { get; set; }

    /// <summary>
    /// Whether a keep-awake session is running
    /// </summary>
    /// <value>True when the session is active</value>
    [ObservableProperty]
    public partial bool IsSessionActive { get; set; }

    /// <summary>
    /// Short session status text
    /// </summary>
    /// <value>Localized status label</value>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "Inactive";

    /// <summary>
    /// Lid state text for the UI
    /// </summary>
    /// <value>Lid status label</value>
    [ObservableProperty]
    public partial string LidStatusText { get; set; } = "Lid: —";

    /// <summary>
    /// Detailed session status text
    /// </summary>
    /// <value>Detail description</value>
    [ObservableProperty]
    public partial string DetailText { get; set; } = string.Empty;

    /// <summary>
    /// Last error or warning text
    /// </summary>
    /// <value>Error message, or null</value>
    [ObservableProperty]
    public partial string? ErrorText { get; set; }

    /// <summary>
    /// Selected UI language code
    /// </summary>
    /// <value>BCP-47 language code</value>
    [ObservableProperty]
    public partial string Language { get; set; } = Localization.DefaultLanguage;

    /// <summary>
    /// Remembered custom timer duration
    /// </summary>
    /// <value>Custom duration in minutes, if any</value>
    [ObservableProperty]
    public partial int? CustomDurationMinutes { get; set; }

    /// <summary>
    /// Formatted remaining countdown text
    /// </summary>
    /// <value>Countdown string</value>
    [ObservableProperty]
    public partial string RemainingText { get; set; } = string.Empty;

    /// <summary>
    /// Whether the active session has a timer
    /// </summary>
    /// <value>True for timed active sessions</value>
    public bool IsTimedSession => IsSessionActive && SelectedDurationMinutes > 0;

    /// <summary>
    /// Label for the start or stop action
    /// </summary>
    /// <value>Localized toggle label</value>
    public string ToggleButtonText => IsSessionActive
        ? Localization.T("menu.stop")
        : Localization.T("menu.start");

    /// <summary>
    /// Handles session active state changes
    /// </summary>
    /// <param name="value">New active state</param>
    partial void OnIsSessionActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(ToggleButtonText));
        OnPropertyChanged(nameof(IsTimedSession));
    }

    /// <summary>
    /// Applies closed-lid option changes
    /// </summary>
    /// <param name="value">New closed-lid value</param>
    partial void OnAllowClosedLidChanged(bool value)
    {
        PersistSettings();
        if (IsSessionActive)
            StartSession();
        else
            RefreshStatus(force: true);
    }

    /// <summary>
    /// Applies display-awake option changes
    /// </summary>
    /// <param name="value">New display-awake value</param>
    partial void OnPreventDisplaySleepChanged(bool value)
    {
        PersistSettings();
        if (IsSessionActive)
            StartSession();
    }

    /// <summary>
    /// Applies UI language changes
    /// </summary>
    /// <param name="value">New language code</param>
    partial void OnLanguageChanged(string value)
    {
        var normalized = Localization.Normalize(value);
        if (!string.Equals(Language, normalized, StringComparison.OrdinalIgnoreCase))
        {
            // Keep the property aligned with the normalized code used for persistence.
            Language = normalized;
            return;
        }

        Localization.SetLanguage(normalized);
        PersistSettings();
        RebuildDurations();
        OnPropertyChanged(nameof(SelectedDuration));
        OnPropertyChanged(nameof(ToggleButtonText));
        RefreshStatus(force: true);
    }

    /// <summary>
    /// Persists custom duration changes
    /// </summary>
    /// <param name="value">New custom duration</param>
    partial void OnCustomDurationMinutesChanged(int? value)
    {
        PersistSettings();
        RebuildDurations();
        OnPropertyChanged(nameof(SelectedDuration));
    }

    /// <summary>
    /// Starts or stops the keep-awake session
    /// </summary>
    [RelayCommand]
    private void ToggleSession()
    {
        if (IsSessionActive)
            StopSession();
        else
            StartSession();
    }

    /// <summary>
    /// Selects a duration and starts a session
    /// </summary>
    /// <param name="minutes">Duration in minutes</param>
    [RelayCommand]
    private void StartWithDuration(int minutes)
    {
        SetDuration(minutes);
        StartSession();
    }

    /// <summary>
    /// Selects a duration without starting a session
    /// </summary>
    /// <param name="minutes">Duration in minutes</param>
    public void SetDuration(int minutes)
    {
        if (minutes < 0)
            return;

        if (!PresetDurations.Contains(minutes))
            CustomDurationMinutes = minutes;

        if (SelectedDurationMinutes != minutes)
        {
            SelectedDurationMinutes = minutes;
            OnPropertyChanged(nameof(SelectedDurationMinutes));
        }

        OnPropertyChanged(nameof(SelectedDuration));
        OnPropertyChanged(nameof(IsTimedSession));
        PersistSettings();
    }

    /// <summary>
    /// Saves a custom duration and starts a session
    /// </summary>
    /// <param name="minutes">Custom duration in minutes</param>
    public void SetCustomDurationAndStart(int minutes)
    {
        if (minutes <= 0)
            return;

        CustomDurationMinutes = minutes;
        SetDuration(minutes);
        StartSession();
    }

    /// <summary>
    /// Persists options and refreshes an active session
    /// </summary>
    [RelayCommand]
    private void ApplyOptionChanges()
    {
        PersistSettings();
        if (IsSessionActive)
            StartSession();
    }

    /// <summary>
    /// Starts a keep-awake session with current options
    /// </summary>
    [RelayCommand]
    private void StartSession()
    {
        try
        {
            PersistSettings();
            TimeSpan? duration = SelectedDurationMinutes > 0
                ? TimeSpan.FromMinutes(SelectedDurationMinutes)
                : null;

            _keepAwake.Start(AllowClosedLid, PreventDisplaySleep, duration);
            RefreshStatus(force: true);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            RefreshStatus(force: true);
        }
    }

    /// <summary>
    /// Stops the keep-awake session
    /// </summary>
    [RelayCommand]
    private void StopSession()
    {
        try
        {
            _keepAwake.Stop();
            RefreshStatus(force: true);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            RefreshStatus(force: true);
        }
    }

    /// <summary>
    /// Persists settings and shows a confirmation detail
    /// </summary>
    [RelayCommand]
    private void SaveSettings()
    {
        PersistSettings();
        SetDetail(Localization.T("detail.settings_saved"));
    }

    /// <summary>
    /// Writes current options to AppSettings
    /// </summary>
    private void PersistSettings()
    {
        _settings.AllowClosedLid = AllowClosedLid;
        _settings.PreventDisplaySleep = PreventDisplaySleep;
        _settings.DurationMinutes = SelectedDurationMinutes;
        _settings.CustomDurationMinutes = CustomDurationMinutes;
        _settings.Language = Localization.CurrentLanguage;
        _settings.Save();
    }

    /// <summary>
    /// Refreshes UI state when the service changes
    /// </summary>
    /// <param name="sender">Event source</param>
    /// <param name="e">Event data</param>
    private void OnKeepAwakeStateChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => RefreshStatus(force: true));

    /// <summary>
    /// Updates the remaining-time countdown text
    /// </summary>
    private void TickCountdown()
    {
        if (!IsSessionActive || SelectedDurationMinutes <= 0)
        {
            if (RemainingText.Length > 0)
                RemainingText = string.Empty;
            return;
        }

        var remaining = _keepAwake.RemainingTime ?? TimeSpan.Zero;
        var text = Localization.FormatRemaining(remaining);
        if (text == _lastRemainingKey)
            return;

        _lastRemainingKey = text;
        RemainingText = text;
        OnPropertyChanged(nameof(SelectedDuration));
    }

    /// <summary>
    /// Refreshes lid status when it changes
    /// </summary>
    private void RefreshLidStatusOnly()
    {
        var lid = _keepAwake.IsLidClosed();
        if (lid == _lastLid)
            return;
        _lastLid = lid;
        LidStatusText = FormatLid(lid);
    }

    /// <summary>
    /// Refreshes session, lid, and detail status
    /// </summary>
    /// <param name="force">True to refresh even when values are unchanged</param>
    private void RefreshStatus(bool force = false)
    {
        var active = _keepAwake.IsActive;
        if (force || active != _lastActive)
        {
            _lastActive = active;
            IsSessionActive = active;
            StatusText = active
                ? Localization.T("status.active")
                : Localization.T("status.inactive");
        }

        var lid = _keepAwake.IsLidClosed();
        if (force || lid != _lastLid)
        {
            _lastLid = lid;
            LidStatusText = FormatLid(lid);
        }

        SetError(_keepAwake.LastWarning);

        if (active && SelectedDurationMinutes > 0)
        {
            var remaining = _keepAwake.RemainingTime ?? TimeSpan.Zero;
            _lastRemainingKey = Localization.FormatRemaining(remaining);
            RemainingText = _lastRemainingKey;
        }
        else
        {
            _lastRemainingKey = "";
            RemainingText = string.Empty;
        }

        string detail;
        if (active)
        {
            var parts = new List<string>(4) { Localization.T("detail.assertions") };
            if (AllowClosedLid)
            {
                parts.Add(_keepAwake.IsPowerProtectActive
                    ? Localization.T("detail.closed_lid_ok")
                    : Localization.T("detail.closed_lid_partial"));
            }

            if (PreventDisplaySleep)
                parts.Add(Localization.T("detail.display_awake"));
            parts.Add(SelectedDurationMinutes > 0
                ? string.Format(Localization.T("detail.timer"), SelectedDuration.Title)
                : Localization.T("detail.indefinite"));
            detail = string.Join(" · ", parts);
        }
        else if (string.IsNullOrEmpty(_keepAwake.LastWarning))
        {
            detail = Localization.T("detail.hint");
        }
        else
        {
            detail = _lastDetail;
        }

        SetDetail(detail);
    }

    /// <summary>
    /// Rebuilds the duration list including the custom entry
    /// </summary>
    private void RebuildDurations()
    {
        Durations.Clear();
        foreach (var minutes in PresetDurations)
            Durations.Add(new DurationOption(minutes));

        if (CustomDurationMinutes is { } custom &&
            custom > 0 &&
            !PresetDurations.Contains(custom))
        {
            Durations.Add(new DurationOption(custom));
        }
    }

    /// <summary>
    /// Chooses the initial duration from settings
    /// </summary>
    /// <param name="settings">Loaded application settings</param>
    /// <returns>Initial duration in minutes</returns>
    private int ResolveInitialDuration(AppSettings settings)
    {
        if (PresetDurations.Contains(settings.DurationMinutes) ||
            (settings.CustomDurationMinutes is { } c && c == settings.DurationMinutes && c > 0))
        {
            return settings.DurationMinutes;
        }

        return 0;
    }

    /// <summary>
    /// Publishes an error message when it changes
    /// </summary>
    /// <param name="error">Error text, or null</param>
    private void SetError(string? error)
    {
        if (error == _lastError)
            return;
        _lastError = error;
        ErrorText = error;
    }

    /// <summary>
    /// Publishes detail text when it changes
    /// </summary>
    /// <param name="detail">Detail text</param>
    private void SetDetail(string detail)
    {
        if (detail == _lastDetail)
            return;
        _lastDetail = detail;
        DetailText = detail;
    }

    /// <summary>
    /// Formats a lid-closed value for display
    /// </summary>
    /// <param name="lid">Lid-closed state</param>
    /// <returns>Lid status label</returns>
    private static string FormatLid(bool? lid) => lid switch
    {
        true => Localization.T("lid.closed"),
        false => Localization.T("lid.open"),
        null => Localization.T("lid.na"),
    };

    /// <summary>
    /// Stops the session and releases native resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _lidTimer?.Stop();
        _countdownTimer?.Stop();
        _keepAwake.StateChanged -= OnKeepAwakeStateChanged;
    }
}
