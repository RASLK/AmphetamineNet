using System.Runtime.InteropServices;

namespace AmphetamineNet.Native;

/// <summary>
/// Tray-only (Accessory) apps don't show the osascript password dialog —
/// we temporarily switch to Regular and activate the app.
/// </summary>
internal static class MacAppActivation
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    [DllImport(ObjC)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint NInt_objc_msgSend_nint(IntPtr receiver, IntPtr selector, nint arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void Void_objc_msgSend_byte(IntPtr receiver, IntPtr selector, byte arg1);

    public static void ActivateForAdminPrompt()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        try
        {
            var nsApp = IntPtr_objc_msgSend(
                objc_getClass("NSApplication"),
                sel_registerName("sharedApplication"));

            // NSApplicationActivationPolicyRegular = 0
            _ = NInt_objc_msgSend_nint(nsApp, sel_registerName("setActivationPolicy:"), 0);
            Void_objc_msgSend_byte(nsApp, sel_registerName("activateIgnoringOtherApps:"), 1);
        }
        catch
        {
            // ignore — osascript will try anyway
        }
    }

    public static void ReturnToAccessory()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        try
        {
            var nsApp = IntPtr_objc_msgSend(
                objc_getClass("NSApplication"),
                sel_registerName("sharedApplication"));

            // NSApplicationActivationPolicyAccessory = 1
            _ = NInt_objc_msgSend_nint(nsApp, sel_registerName("setActivationPolicy:"), 1);
        }
        catch
        {
            // ignore
        }
    }
}
