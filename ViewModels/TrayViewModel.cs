using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AmphetamineNet.ViewModels;

namespace AmphetamineNet;

public sealed partial class TrayViewModel : ObservableObject
{
    private readonly App _app;
    private readonly MainViewModel _main;
    private readonly DispatcherTimer _debounce;

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
            // Tray doesn't depend on DetailText/ErrorText — don't wake the menu needlessly
            if (e.PropertyName is nameof(MainViewModel.IsSessionActive)
                or nameof(MainViewModel.SelectedDuration)
                or nameof(MainViewModel.AllowClosedLid)
                or nameof(MainViewModel.PreventDisplaySleep)
                or nameof(MainViewModel.StatusText)
                or nameof(MainViewModel.LidStatusText))
            {
                ScheduleNotify();
            }
        };

        NotifyMenuStateCore();
    }

    public MainViewModel Main => _main;

    public string SessionHeader => _main.IsSessionActive
        ? $"● Active · {_main.SelectedDuration.Title}"
        : "○ Inactive";

    public string ToggleSessionHeader => _main.IsSessionActive
        ? "Stop Session"
        : "Start Session";

    public string TrayToolTip => _main.IsSessionActive
        ? $"AmphetamineNet — active ({_main.SelectedDuration.Title})"
        : "AmphetamineNet — inactive";

    public bool IsIndefinite => _main.SelectedDuration.Minutes == 0;
    public bool Is5Min => _main.SelectedDuration.Minutes == 5;
    public bool Is15Min => _main.SelectedDuration.Minutes == 15;
    public bool Is30Min => _main.SelectedDuration.Minutes == 30;
    public bool Is1Hour => _main.SelectedDuration.Minutes == 60;
    public bool Is2Hours => _main.SelectedDuration.Minutes == 120;
    public bool Is5Hours => _main.SelectedDuration.Minutes == 300;

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

    [RelayCommand]
    private void ToggleSession() => _main.ToggleSessionCommand.Execute(null);

    [RelayCommand]
    private void SelectDuration(string? minutesText)
    {
        if (!int.TryParse(minutesText, out var minutes))
            return;

        _main.StartWithDurationCommand.Execute(minutes);
        ScheduleNotify();
    }

    [RelayCommand]
    private void ToggleClosedLid()
    {
        AllowClosedLid = !AllowClosedLid;
        ScheduleNotify();
    }

    [RelayCommand]
    private void ToggleDisplaySleep()
    {
        PreventDisplaySleep = !PreventDisplaySleep;
        ScheduleNotify();
    }

    [RelayCommand]
    private void ShowPreferences() => _app.ShowMainWindow();

    [RelayCommand]
    private void Exit() => _app.ExitApplication();

    private void ScheduleNotify()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void NotifyMenuStateCore()
    {
        OnPropertyChanged(nameof(SessionHeader));
        OnPropertyChanged(nameof(ToggleSessionHeader));
        OnPropertyChanged(nameof(TrayToolTip));
        OnPropertyChanged(nameof(IsIndefinite));
        OnPropertyChanged(nameof(Is5Min));
        OnPropertyChanged(nameof(Is15Min));
        OnPropertyChanged(nameof(Is30Min));
        OnPropertyChanged(nameof(Is1Hour));
        OnPropertyChanged(nameof(Is2Hours));
        OnPropertyChanged(nameof(Is5Hours));
        OnPropertyChanged(nameof(AllowClosedLid));
        OnPropertyChanged(nameof(PreventDisplaySleep));
    }
}
