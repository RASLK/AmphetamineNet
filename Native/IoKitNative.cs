using System.Runtime.InteropServices;

namespace AmphetamineNet.Native;

internal static class IoKitNative
{
    public const uint kIOMainPortDefault = 0;
    public const uint kIOPMAssertionLevelOn = 255;
    public const uint kPMSetClamshellSleepState = 12;
    public const uint kCFStringEncodingUTF8 = 0x08000100;
    public const int kIOReturnSuccess = 0;

    public const string PreventUserIdleSystemSleep = "PreventUserIdleSystemSleep";
    public const string PreventUserIdleDisplaySleep = "PreventUserIdleDisplaySleep";
    public const string PreventSystemSleep = "PreventSystemSleep";

    private static readonly Lazy<IntPtr> MachTaskSelf = new(ResolveMachTaskSelf);

    public static IntPtr CurrentMachTask => MachTaskSelf.Value;

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string str, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOPMAssertionCreateWithName(
        IntPtr assertionType,
        uint assertionLevel,
        IntPtr assertionName,
        out uint assertionId);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOPMAssertionRelease(uint assertionId);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern IntPtr IOServiceMatching(string name);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern uint IOServiceGetMatchingService(uint mainPort, IntPtr matching);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOServiceOpen(uint service, IntPtr owningTask, uint type, out uint connect);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOServiceClose(uint connect);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOObjectRelease(uint obj);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOConnectCallScalarMethod(
        uint connection,
        uint selector,
        ref ulong input,
        uint inputCount,
        IntPtr output,
        IntPtr outputCount);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern IntPtr IORegistryEntryCreateCFProperty(
        uint entry,
        IntPtr key,
        IntPtr allocator,
        uint options);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern byte CFBooleanGetValue(IntPtr boolean);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern IntPtr CFGetTypeID(IntPtr cf);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern IntPtr CFBooleanGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern IntPtr CFNumberGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern byte CFNumberGetValue(IntPtr number, nint theType, out int value);

    // kCFNumberIntType = 9
    public const nint kCFNumberIntType = 9;

    private static IntPtr ResolveMachTaskSelf()
    {
        if (!NativeLibrary.TryLoad("/usr/lib/libSystem.dylib", out var lib))
            throw new InvalidOperationException("Failed to load libSystem.dylib");

        var symbol = NativeLibrary.GetExport(lib, "mach_task_self_");
        return Marshal.ReadIntPtr(symbol);
    }
}
