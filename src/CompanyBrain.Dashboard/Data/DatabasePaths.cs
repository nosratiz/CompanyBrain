namespace CompanyBrain.Dashboard.Data;

/// <summary>
/// Central resolver for the <c>db/</c> directory where all SQLite databases are stored.
/// Ensures the folder exists before any database context is created.
/// </summary>
internal static class DatabasePaths
{
    /// <summary>
    /// The subfolder name under the content root where all .db files live.
    /// </summary>
    public const string FolderName = "Db";

    /// <summary>
    /// Resolves the absolute path to the <c>db/</c> directory and ensures it exists.
    /// Uses the directory of the running executable so both the web server and the
    /// Claude Desktop stdio process always share the same database files regardless
    /// of what the current working directory is when each process starts.
    /// </summary>
    public static string EnsureDbFolder()
    {
        // AppContext.BaseDirectory is the folder containing the compiled assembly —
        // the same for both `dotnet run` (web) and `--stdio` (Claude Desktop).
        var root = AppContext.BaseDirectory;
        var folder = Path.Combine(root, FolderName);
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// Builds a SQLite connection string pointing to a file inside the <c>db/</c> folder.
    /// </summary>
    public static string ConnectionString(string fileName)
    {
        var folder = EnsureDbFolder();
        var fullPath = Path.Combine(folder, fileName);
        return $"Data Source={fullPath}";
    }
}
