using System.Runtime.InteropServices;

namespace CompanyBrain.Search.Vector;

/// <summary>
/// Resolves the platform-specific path to the sqlite-vec native extension
/// shipped by the <c>sqlite-vec</c> NuGet package.
/// </summary>
internal static class SqliteVecLoader
{
    private static readonly Lazy<string> ResolvedPath = new(Resolve);

    public static string GetExtensionPath() => ResolvedPath.Value;

    private static string Resolve()
    {
        var fileName = GetFileName();
        var rid = GetRuntimeIdentifier();

        // 1) Side-by-side with the executable (common for published apps).
        var baseDir = AppContext.BaseDirectory;
        var sideBySide = Path.Combine(baseDir, fileName);
        if (File.Exists(sideBySide))
        {
            return sideBySide;
        }

        // 2) Standard runtimes/{rid}/native layout (test runs / dev builds).
        var ridPath = Path.Combine(baseDir, "runtimes", rid, "native", fileName);
        if (File.Exists(ridPath))
        {
            return ridPath;
        }

        // 3) Let SQLite resolve via the OS loader path (last-resort).
        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static string GetFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "vec0.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "vec0.dylib";
        return "vec0.so";
    }

    private static string GetRuntimeIdentifier()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return $"win-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return $"osx-{arch}";
        return $"linux-{arch}";
    }
}
