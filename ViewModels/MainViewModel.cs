using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AmphetamineNet.Services;

namespace AmphetamineNet.ViewModels;

public sealed record DurationOption(string Title, int Minutes)
{
    public override string ToString() => Title;
}

public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly MacKeepAwakeService _keepAwake;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer? _lidTimer;
    private bool _disposed;
    private string _lastDetail = "";
    private string? _lastError;
    private bool? _lastLid;
    private bool _lastActive;

    public MainViewModel(MacKeepAwakeService keepAwake, AppSettings settings)
    {
        _keepAwake = keepAwake;
        _settings = settings;
        _keepAwake.StateChanged += OnKeepAwakeStateChanged;

        Durations =
        [
            new("Indefinitely", 0),
            new("5 minutes", 5),
            new("15 minutes", 15),
            new("30 minutes", 30),
            new("1 hour", 60),
            new("2 hours", 120),
            new("5 hours", 300),
        ];

        SelectedDuration = Durations.FirstOrDefault(d => d.Minutes == settings.DurationMinutes)
                           ?? Durations[0];
        AllowClosedLid = settings.AllowClosedLid;
        PreventDisplaySleep = settings.PreventDisplaySleep;

        // Rarely refresh only the lid state (not every 2 s)
        _lidTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _lidTimer.Tick += (_, _) => RefreshLidStatusOnly();
        _lidTimer.Start();

        RefreshStatus(force: true);

        if (settings.StartWithSession)
            StartSession();
    }

    public ObservableCollection<DurationOption> Durations { get; }

    [ObservableProperty]
    public partial DurationOption SelectedDuration { get; set; }

    [ObservableProperty]
    public partial bool AllowClosedLid { get; set; }

    [ObservableProperty]
    public partial bool PreventDisplaySleep { get; set; }

    [ObservableProperty]
    public partial bool IsSessionActive { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Inactive";

    [ObservableProperty]
    public partial string LidStatusText { get; set; } = "Lid: —";

    [ObservableProperty]
    public partial string DetailText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorText { get; set; }

    public string ToggleButtonText => IsSessionActive ? "Stop Session" : "Start Session";

    partial void OnIsSessionActiveChanged(bool value) => OnPropertyChanged(nameof(ToggleButtonText));

    partial void OnSelectedDurationChanged(DurationOption value) => PersistSettings();

    partial void OnAllowClosedLidChanged(bool value)
    {
        PersistSettings();
        // Power Protect is set up by Start/SetPmset — we don't duplicate Ensure here
        if (IsSessionActive)
            StartSession();
        else
            RefreshStatus(force: true);
    }

    partial void OnPreventDisplaySleepChanged(bool value)
    {
        PersistSettings();
        if (IsSessionActive)
            StartSession();
    }

    [RelayCommand]
    private void ToggleSession()
    {
        if (IsSessionActive)
            StopSession();
        else
            StartSession();
    }

    [RelayCommand]
    private void StartWithDuration(int minutes)
    {
        SelectedDuration = Durations.FirstOrDefault(d => d.Minutes == minutes) ?? Durations[0];
        StartSession();
    }

    [RelayCommand]
    private void ApplyOptionChanges()
    {
        PersistSettings();
        if (IsSessionActive)
            StartSession();
    }

    [RelayCommand]
    private void StartSession()
    {
        try
        {
            PersistSettings();
            TimeSpan? duration = SelectedDuration.Minutes > 0
                ? TimeSpan.FromMinutes(SelectedDuration.Minutes)
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

    [RelayCommand]
    private void SaveSettings()
    {
        PersistSettings();
        SetDetail("Settings saved");
    }

    private void PersistSettings()
    {
        _settings.AllowClosedLid = AllowClosedLid;
        _settings.PreventDisplaySleep = PreventDisplaySleep;
        _settings.DurationMinutes = SelectedDuration.Minutes;
        _settings.Save();
    }

    private void OnKeepAwakeStateChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => RefreshStatus(force: true));

    private void RefreshLidStatusOnly()
    {
        var lid = _keepAwake.IsLidClosed();
        if (lid == _lastLid)
            return;
        _lastLid = lid;
        LidStatusText = FormatLid(lid);
    }

    private void RefreshStatus(bool force = false)
    {
        var active = _keepAwake.IsActive;
        if (force || active != _lastActive)
        {
            _lastActive = active;
            IsSessionActive = active;
            StatusText = active ? "Active — Mac is staying awake" : "Inactive";
        }

        var lid = _keepAwake.IsLidClosed();
        if (force || lid != _lastLid)
        {
            _lastLid = lid;
            LidStatusText = FormatLid(lid);
        }

        SetError(_keepAwake.LastWarning);

        string detail;
        if (active)
        {
            var parts = new List<string>(4) { "IOPM assertions" };
            if (AllowClosedLid)
            {
                parts.Add(_keepAwake.IsPowerProtectActive
                    ? "closed lid OK"
                    : "lid without SleepDisabled");
            }

            if (PreventDisplaySleep)
                parts.Add("display awake");
            parts.Add(SelectedDuration.Minutes > 0
                ? $"timer {SelectedDuration.Title.ToLowerInvariant()}"
                : "indefinite");
            detail = string.Join(" · ", parts);
        }
        else if (string.IsNullOrEmpty(_keepAwake.LastWarning))
        {
            detail =
                "Turn on \"Allow closed lid\" — Power Protect will install itself the first time (one password prompt).";
        }
        else
        {
            detail = _lastDetail;
        }

        SetDetail(detail);
    }

    private void SetError(string? error)
    {
        if (error == _lastError)
            return;
        _lastError = error;
        ErrorText = error;
    }

    private void SetDetail(string detail)
    {
        if (detail == _lastDetail)
            return;
        _lastDetail = detail;
        DetailText = detail;
    }

    private static string FormatLid(bool? lid) => lid switch
    {
        true => "Lid: closed",
        false => "Lid: open",
        null => "Lid: n/a",
    };

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _lidTimer?.Stop();
        _keepAwake.StateChanged -= OnKeepAwakeStateChanged;
    }
}
