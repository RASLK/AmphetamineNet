using System.Runtime.InteropServices;
using AmphetamineNet.Native;
using static AmphetamineNet.Native.IoKitNative;

namespace AmphetamineNet.Services;

/// <summary>
/// Keeps the Mac awake: IOPM assertions + IOKit clamshell + pmset disablesleep (verified against ioreg).
/// </summary>
public sealed class MacKeepAwakeService : IDisposable
{
    private readonly object _gate = new();
    private readonly List<uint> _assertionIds = [];
    private uint _rootDomainService;
    private uint _rootDomainConnection;
    private Timer? _heartbeat;
    private Timer? _sessionTimer;
    private bool _active;
    private bool _allowClosedLid = true;
    private bool _preventDisplaySleep;
    private bool _pmsetDisableSleepHeld;
    private bool _disposed;

    // CFString keys cached for the lifetime of the process (never CFRelease'd).
    private static readonly IntPtr KeyAppleClamshellState =
        CFStringCreateWithCString(IntPtr.Zero, "AppleClamshellState", kCFStringEncodingUTF8);
    private static readonly IntPtr KeySleepDisabled =
        CFStringCreateWithCString(IntPtr.Zero, "SleepDisabled", kCFStringEncodingUTF8);

    public bool IsActive
    {
        get { lock (_gate) return _active; }
    }

    public bool IsPowerProtectActive
    {
        get { lock (_gate) return _pmsetDisableSleepHeld; }
    }

    public string? LastWarning { get; private set; }

    public bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public Action? PrepareForAdminPrompt { get; set; }
    public Action? FinishAdminPrompt { get; set; }

    public event EventHandler? StateChanged;

    public void EnsurePowerProtectInstalled()
    {
        if (!IsSupported)
            return;

        if (PowerProtect.IsSudoersInstalled())
            return;

        PrepareForAdminPrompt?.Invoke();
        try
        {
            PowerProtect.Install(prepareUi: null);
            AppLog.Write("Power Protect installed automatically");
        }
        finally
        {
            FinishAdminPrompt?.Invoke();
        }
    }

    public void Start(bool allowClosedLid, bool preventDisplaySleep, TimeSpan? duration)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException("AmphetamineNet only works on macOS.");

        if (allowClosedLid)
        {
            try
            {
                EnsurePowerProtectInstalled();
            }
            catch (Exception ex)
            {
                LastWarning =
                    "Failed to install Power Protect. Without it, the closed-lid setting won't work on battery. " +
                    ex.Message;
                AppLog.Write($"Power Protect install FAILED: {ex.Message}");
            }
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            StopCore(releaseClamshellConnection: false);

            _allowClosedLid = allowClosedLid;
            _preventDisplaySleep = preventDisplaySleep;
            LastWarning = null;

            CreateAssertions();
            if (_allowClosedLid)
                SetClamshellSleepDisabled(true);
        }

        if (allowClosedLid)
        {
            try
            {
                SetPmsetDisableSleep(true);
                if (!ReadSleepDisabledFromIoreg())
                    throw new InvalidOperationException("pmset ran, but SleepDisabled is still No.");

                lock (_gate)
                    _pmsetDisableSleepHeld = true;

                LastWarning = null;
                AppLog.Write("pmset disablesleep 1 OK (ioreg SleepDisabled=Yes)");
            }
            catch (Exception ex)
            {
                lock (_gate)
                {
                    _pmsetDisableSleepHeld = false;
                    LastWarning = "Closed lid not activated (SleepDisabled). " + ex.Message;
                }

                AppLog.Write($"pmset disablesleep FAILED: {ex.Message}");
            }
        }
        else
        {
            LastWarning = null;
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _heartbeat = new Timer(
                _ =>
                {
                    try
                    {
                        bool reapplyPmset;
                        lock (_gate)
                        {
                            if (!_active || !_allowClosedLid)
                                return;
                            SetClamshellSleepDisabled(true);
                            reapplyPmset = _pmsetDisableSleepHeld;
                        }

                        if (reapplyPmset && !ReadSleepDisabledFromIoreg())
                        {
                            AppLog.Write("heartbeat: SleepDisabled was reset, retrying pmset");
                            try { SetPmsetDisableSleep(true); }
                            catch (Exception ex) { AppLog.Write($"heartbeat pmset: {ex.Message}"); }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLog.Write($"heartbeat error: {ex.Message}");
                    }
                },
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));

            if (duration is { } d && d > TimeSpan.Zero)
            {
                _sessionTimer = new Timer(
                    _ =>
                    {
                        try { Stop(); }
                        catch (Exception ex) { AppLog.Write($"session timer error: {ex.Message}"); }
                    },
                    null,
                    d,
                    Timeout.InfiniteTimeSpan);
            }

            _active = true;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            StopCore(releaseClamshellConnection: false);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool? IsLidClosed()
    {
        if (!IsSupported)
            return null;

        try
        {
            return ReadIoregBoolean(KeyAppleClamshellState);
        }
        catch
        {
            return null;
        }
    }

    public bool IsSystemSleepDisabled() => ReadSleepDisabledFromIoreg();

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            StopCore(releaseClamshellConnection: true);
            _disposed = true;
        }
    }

    private void StopCore(bool releaseClamshellConnection)
    {
        _sessionTimer?.Dispose();
        _sessionTimer = null;
        _heartbeat?.Dispose();
        _heartbeat = null;

        if (_allowClosedLid && _active)
        {
            try { SetClamshellSleepDisabled(false); }
            catch (Exception ex) { AppLog.Write($"clamshell enable error: {ex.Message}"); }
        }

        if (_pmsetDisableSleepHeld)
        {
            try
            {
                SetPmsetDisableSleep(false);
                AppLog.Write("pmset disablesleep 0 OK");
            }
            catch (Exception ex)
            {
                AppLog.Write($"pmset disablesleep 0 FAILED: {ex.Message}");
                LastWarning = "Failed to restore sleep. Run: sudo pmset -a disablesleep 0";
            }

            _pmsetDisableSleepHeld = false;
        }

        ReleaseAssertions();
        if (releaseClamshellConnection)
            CloseClamshellHandles();
        _active = false;
    }

    private void CreateAssertions()
    {
        ReleaseAssertions();
        CreateAssertion(PreventUserIdleSystemSleep, "AmphetamineNet idle system");
        CreateAssertion(PreventSystemSleep, "AmphetamineNet system");
        if (_preventDisplaySleep)
            CreateAssertion(PreventUserIdleDisplaySleep, "AmphetamineNet display");
    }

    private void CreateAssertion(string type, string name)
    {
        var typeRef = CFStringCreateWithCString(IntPtr.Zero, type, kCFStringEncodingUTF8);
        var nameRef = CFStringCreateWithCString(IntPtr.Zero, name, kCFStringEncodingUTF8);
        if (typeRef == IntPtr.Zero || nameRef == IntPtr.Zero)
        {
            if (typeRef != IntPtr.Zero) CFRelease(typeRef);
            if (nameRef != IntPtr.Zero) CFRelease(nameRef);
            throw new InvalidOperationException($"CFStringCreate failed for assertion {type}");
        }

        try
        {
            var kr = IOPMAssertionCreateWithName(typeRef, kIOPMAssertionLevelOn, nameRef, out var id);
            if (kr != kIOReturnSuccess)
                throw new InvalidOperationException($"IOPMAssertionCreateWithName({type}) failed: 0x{kr:X8}");
            _assertionIds.Add(id);
        }
        finally
        {
            CFRelease(typeRef);
            CFRelease(nameRef);
        }
    }

    private void ReleaseAssertions()
    {
        foreach (var id in _assertionIds)
            IOPMAssertionRelease(id);
        _assertionIds.Clear();
    }

    private void EnsureClamshellConnection()
    {
        if (_rootDomainConnection != 0)
            return;

        _rootDomainService = IOServiceGetMatchingService(kIOMainPortDefault, IOServiceMatching("IOPMrootDomain"));
        if (_rootDomainService == 0)
            throw new InvalidOperationException("IOPMrootDomain not found");

        var kr = IOServiceOpen(_rootDomainService, CurrentMachTask, 0, out _rootDomainConnection);
        if (kr != kIOReturnSuccess || _rootDomainConnection == 0)
        {
            IOObjectRelease(_rootDomainService);
            _rootDomainService = 0;
            _rootDomainConnection = 0;
            throw new InvalidOperationException($"IOServiceOpen(IOPMrootDomain) failed: 0x{kr:X8}");
        }
    }

    private void SetClamshellSleepDisabled(bool disable)
    {
        EnsureClamshellConnection();
        ulong input = disable ? 1uL : 0uL;
        var kr = IOConnectCallScalarMethod(
            _rootDomainConnection,
            kPMSetClamshellSleepState,
            ref input,
            1,
            IntPtr.Zero,
            IntPtr.Zero);

        if (kr != kIOReturnSuccess)
            throw new InvalidOperationException($"kPMSetClamshellSleepState failed: 0x{kr:X8}");
    }

    private void CloseClamshellHandles()
    {
        if (_rootDomainConnection != 0)
        {
            IOServiceClose(_rootDomainConnection);
            _rootDomainConnection = 0;
        }

        if (_rootDomainService != 0)
        {
            IOObjectRelease(_rootDomainService);
            _rootDomainService = 0;
        }
    }

    private static bool? ReadIoregBoolean(IntPtr key)
    {
        if (key == IntPtr.Zero)
            return null;

        var service = IOServiceGetMatchingService(kIOMainPortDefault, IOServiceMatching("IOPMrootDomain"));
        if (service == 0)
            return null;

        try
        {
            var prop = IORegistryEntryCreateCFProperty(service, key, IntPtr.Zero, 0);
            if (prop == IntPtr.Zero)
                return null;

            try
            {
                var typeId = CFGetTypeID(prop);
                if (typeId == CFBooleanGetTypeID())
                    return CFBooleanGetValue(prop) != 0;

                if (typeId == CFNumberGetTypeID() &&
                    CFNumberGetValue(prop, kCFNumberIntType, out var number) != 0)
                    return number != 0;

                return null;
            }
            finally
            {
                CFRelease(prop);
            }
        }
        finally
        {
            IOObjectRelease(service);
        }
    }

    private static bool ReadSleepDisabledFromIoreg() => ReadIoregBoolean(KeySleepDisabled) == true;

    private void SetPmsetDisableSleep(bool disable)
    {
        var value = disable ? "1" : "0";
        if (PowerProtect.TryPmsetDisableSleep(disable, out var err))
        {
            AppLog.Write($"sudo -n pmset disablesleep {value} OK");
            return;
        }

        if (!PowerProtect.IsSudoersInstalled())
        {
            EnsurePowerProtectInstalled();
            if (PowerProtect.TryPmsetDisableSleep(disable, out err))
            {
                AppLog.Write($"sudo -n pmset disablesleep {value} OK (after install)");
                return;
            }
        }

        throw new InvalidOperationException(
            $"Failed to run pmset disablesleep {value}: {err}");
    }
}
