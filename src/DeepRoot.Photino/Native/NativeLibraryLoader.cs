// -----------------------------------------------------------------------------
//  Pillar 4 — Cross-platform native loader for sqlite-vec.
//
//  At publish-time the .csproj copies the correct payload into the app folder:
//      Windows : vec0.dll
//      macOS   : vec0.dylib
//      Linux   : vec0.so
//
//  At runtime we register a NativeLibrary resolver so any P/Invoke that asks
//  for "vec0" (or sqlite_vec_init via SQLitePCL) finds the file regardless of
//  the OS-specific naming conventions, search paths, or sandboxing rules.
// -----------------------------------------------------------------------------

using System.Reflection;
using System.Runtime.InteropServices;
using SQLitePCL;

namespace DeepRoot.Photino.Native;

internal static class NativeLibraryLoader
{
    private const string LibraryName = "vec0";
    private static int   _initialized;          // 0 = not loaded, 1 = loaded
    private static IntPtr _handle = IntPtr.Zero;

    /// <summary>
    /// Eagerly loads the sqlite-vec extension for the current OS/arch
    /// and wires up a NativeLibrary resolver for late-bound P/Invoke calls.
    /// Safe to call multiple times.
    /// </summary>
    public static void EnsureSqliteVecLoaded(string baseDirectory)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        // 1. Initialise the SQLitePCLRaw provider exactly once.
        Batteries_V2.Init();

        // 2. Register a per-assembly resolver — works on Windows, macOS, Linux.
        NativeLibrary.SetDllImportResolver(
            typeof(NativeLibraryLoader).Assembly,
            ResolveDll);

        // 3. Pre-load the binary so SQLite's load_extension() succeeds even
        //    when the working directory is not the app directory (common on
        //    macOS app bundles and systemd Linux services).
        string path = ResolvePath(baseDirectory);
        if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
            _handle = handle;
    }

    public static IntPtr Handle => _handle;

    // -------------------------------------------------------------------------
    private static IntPtr ResolveDll(string libraryName, Assembly _, DllImportSearchPath? __)
    {
        if (!libraryName.Equals(LibraryName, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        if (_handle != IntPtr.Zero)
            return _handle;

        string path = ResolvePath(AppContext.BaseDirectory);
        return NativeLibrary.TryLoad(path, out var h) ? h : IntPtr.Zero;
    }

    private static string ResolvePath(string baseDirectory)
    {
        string fileName =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "vec0.dll"   :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)     ? "vec0.dylib" :
                                                                  "vec0.so";

        // Look in the app directory first (default after `dotnet publish`),
        // then fall back to the per-RID `runtimes/<rid>/native` folder for
        // dev-time `dotnet run` scenarios.
        string flat = Path.Combine(baseDirectory, fileName);
        if (File.Exists(flat))
            return flat;

        string rid = RuntimeInformation.RuntimeIdentifier;
        return Path.Combine(baseDirectory, "runtimes", rid, "native", fileName);
    }
}
