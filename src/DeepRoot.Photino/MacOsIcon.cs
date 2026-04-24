// Photino's SetIconFile only sets the window proxy icon on macOS.
// The Dock icon must be pushed explicitly through NSApp via the ObjC runtime.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DeepRoot.Photino;

internal static class MacOsIcon
{
    [SupportedOSPlatform("macos")]
    internal static void SetDockIcon(string icnsPath)
    {
        // NSString *nsPath = [NSString stringWithUTF8String: icnsPath]
        nint NSStringClass  = objc_getClass("NSString");
        nint utf8Sel        = sel_registerName("stringWithUTF8String:");
        nint nsPath         = objc_msgSend_cstr(NSStringClass, utf8Sel, icnsPath);

        // NSImage *image = [[NSImage alloc] initWithContentsOfFile: nsPath]
        nint NSImageClass   = objc_getClass("NSImage");
        nint allocSel       = sel_registerName("alloc");
        nint initFileSel    = sel_registerName("initWithContentsOfFile:");
        nint imageAlloc     = objc_msgSend(NSImageClass, allocSel);
        nint image          = objc_msgSend(imageAlloc, initFileSel, nsPath);
        if (image == 0)
        {
            Console.Error.WriteLine($"[DeepRoot] MacOsIcon: NSImage failed to load '{icnsPath}'");
            return;
        }

        // [NSApp setApplicationIconImage: image]
        nint NSAppClass     = objc_getClass("NSApplication");
        nint sharedAppSel   = sel_registerName("sharedApplication");
        nint setIconSel     = sel_registerName("setApplicationIconImage:");
        nint nsApp          = objc_msgSend(NSAppClass, sharedAppSel);
        if (nsApp == 0)
        {
            Console.Error.WriteLine("[DeepRoot] MacOsIcon: NSApplication.sharedApplication returned nil");
            return;
        }
        objc_msgSend(nsApp, setIconSel, image);
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern nint objc_getClass([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern nint sel_registerName([MarshalAs(UnmanagedType.LPStr)] string name);

    // objc_msgSend overloads for the argument shapes we need
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend(nint receiver, nint selector, nint arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_cstr(nint receiver, nint selector,
        [MarshalAs(UnmanagedType.LPStr)] string arg);
}
