using System.Runtime.InteropServices;
using AmphetamineNet.Native;
using static AmphetamineNet.Native.IoKitNative;

namespace AmphetamineNet.Services;

/// <summary>
/// Keeps the Mac awake via IOPM assertions and pmset
/// </summary>
public sealed class MacKeepAwakeService : IDisposable
{
    /// <summary>
    /// Synchronization lock for service state
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// Active IOPM assertion identifiers
    /// </summary>
    private readonly List<uint> _assertionIds = [];

    /// <summary>
    /// IOKit service handle for IOPMrootDomain
    /// </summary>
    private uint _rootDomainService;

    /// <summary>
    /// IOKit connection to IOPMrootDomain
    /// </summary>
    private uint _rootDomainConnection;

    /// <summary>
    /// Timer that re-applies clamshell protection
    /// </summary>
    private Timer? _heartbeat;

    /// <summary>
    /// Timer that stops a timed session
    /// </summary>
    private Timer? _sessionTimer;

    /// <summary>
    /// Whether a keep-awake session is active
    /// </summary>
    private bool _active;

    /// <summary>
    /// Whether closed-lid keep-awake is requested
    /// </summary>
    private bool _allowClosedLid = true;

    /// <summary>
    /// Whether display sleep prevention is requested
    /// </summary>
    private bool _preventDisplaySleep;

    /// <summary>
    /// Whether pmset disablesleep is currently held
    /// </summary>
    private bool _pmsetDisableSleepHeld;

    /// <summary>
    /// Whether the object has been disposed
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// UTC end time of a timed session
    /// </summary>
    private DateTimeOffset? _sessionEndsAt;

    /// <summary>
    /// CFString key for AppleClamshellState
    /// </summary>
    private static readonly IntPtr KeyAppleClamshellState =
        CFStringCreateWithCString(IntPtr.Zero, "AppleClamshellState", kCFStringEncodingUTF8);

    /// <summary>
    /// CFString key for SleepDisabled
    /// </summary>
    private static readonly IntPtr KeySleepDisabled =
        CFStringCreateWithCString(IntPtr.Zero, "SleepDisabled", kCFStringEncodingUTF8);

    /// <summary>
    /// Whether a keep-awake session is running
    /// </summary>
    /// <value>True when assertions are held</value>
    public bool IsActive
    {
        get { lock (_gate) return _active; }
    }

    /// <summary>
    /// Whether pmset SleepDisabled is held
    /// </summary>
    /// <value>True when Power Protect is active</value>
    public bool IsPowerProtectActive
    {
        get { lock (_gate) return _pmsetDisableSleepHeld; }
    }

    /// <summary>
    /// UTC end time of the timed session
    /// </summary>
    /// <value>End timestamp, or null when inactive or indefinite</value>
    public DateTimeOffset? SessionEndsAt
    {
        get { lock (_gate) return _sessionEndsAt; }
    }

    /// <summary>
    /// Time left in the timed session
    /// </summary>
    /// <value>Remaining duration, or null when inactive or indefinite</value>
    public TimeSpan? RemainingTime
    {
        get
        {
            lock (_gate)
            {
                if (_sessionEndsAt is not { } ends)
                    return null;
                var left = ends - DateTimeOffset.UtcNow;
                return left < TimeSpan.Zero ? TimeSpan.Zero : left;
            }
        }
    }

    /// <summary>
    /// Last non-fatal warning message
    /// </summary>
    /// <value>Warning text, or null when none</value>
    public string? LastWarning { get; private set; }

    /// <summary>
    /// Whether the current OS supports keep-awake
    /// </summary>
    /// <value>True on macOS</value>
    public bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// Callback invoked before an admin password prompt
    /// </summary>
    /// <value>UI preparation action</value>
    public Action? PrepareForAdminPrompt { get; set; }

    /// <summary>
    /// Callback invoked after an admin password prompt
    /// </summary>
    /// <value>UI cleanup action</value>
    public Action? FinishAdminPrompt { get; set; }

    /// <summary>
    /// Raised when session activity changes
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Installs the passwordless pmset sudoers helper if needed
    /// </summary>
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

    /// <summary>
    /// Starts a keep-awake session
    /// </summary>
    /// <param name="allowClosedLid">Enable closed-lid keep-awake</param>
    /// <param name="preventDisplaySleep">Prevent display sleep</param>
    /// <param name="duration">Optional session duration</param>
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
                _sessionEndsAt = DateTimeOffset.UtcNow + d;
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
            else
            {
                _sessionEndsAt = null;
            }

            _active = true;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stops the keep-awake session
    /// </summary>
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

    /// <summary>
    /// Reads whether the lid is currently closed
    /// </summary>
    /// <returns>True when closed, false when open, or null when unknown</returns>
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

    /// <summary>
    /// Reads SleepDisabled from IORegistry
    /// </summary>
    /// <returns>True when system sleep is disabled</returns>
    public bool IsSystemSleepDisabled() => ReadSleepDisabledFromIoreg();

    /// <summary>
    /// Stops the session and releases native resources
    /// </summary>
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

    /// <summary>
    /// Releases assertions and restores sleep settings
    /// </summary>
    /// <param name="releaseClamshellConnection">Whether to close the IOKit connection</param>
    private void StopCore(bool releaseClamshellConnection)
    {
        _sessionTimer?.Dispose();
        _sessionTimer = null;
        _sessionEndsAt = null;
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

    /// <summary>
    /// Creates IOPM assertions for the current options
    /// </summary>
    private void CreateAssertions()
    {
        ReleaseAssertions();
        CreateAssertion(PreventUserIdleSystemSleep, "AmphetamineNet idle system");
        CreateAssertion(PreventSystemSleep, "AmphetamineNet system");
        if (_preventDisplaySleep)
            CreateAssertion(PreventUserIdleDisplaySleep, "AmphetamineNet display");
    }

    /// <summary>
    /// Creates a single IOPM assertion
    /// </summary>
    /// <param name="type">IOPM assertion type</param>
    /// <param name="name">Assertion display name</param>
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

    /// <summary>
    /// Releases all held IOPM assertions
    /// </summary>
    private void ReleaseAssertions()
    {
        foreach (var id in _assertionIds)
            IOPMAssertionRelease(id);
        _assertionIds.Clear();
    }

    /// <summary>
    /// Opens an IOKit connection to IOPMrootDomain
    /// </summary>
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

    /// <summary>
    /// Enables or disables clamshell sleep via IOKit
    /// </summary>
    /// <param name="disable">True to disable clamshell sleep</param>
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

    /// <summary>
    /// Closes IOKit clamshell handles
    /// </summary>
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

    /// <summary>
    /// Reads a boolean IORegistry property from IOPMrootDomain
    /// </summary>
    /// <param name="key">CFString property key</param>
    /// <returns>Property value, or null when unavailable</returns>
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

    /// <summary>
    /// Reads the SleepDisabled IORegistry flag
    /// </summary>
    /// <returns>True when SleepDisabled is set</returns>
    private static bool ReadSleepDisabledFromIoreg() => ReadIoregBoolean(KeySleepDisabled) == true;

    /// <summary>
    /// Runs pmset disablesleep through Power Protect
    /// </summary>
    /// <param name="disable">True to disable system sleep</param>
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
