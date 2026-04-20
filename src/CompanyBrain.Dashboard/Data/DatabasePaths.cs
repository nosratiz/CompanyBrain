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
    /// Uses the current working directory as root (matches how relative SQLite paths resolve).
    /// </summary>
    public static string EnsureDbFolder()
    {
        var folder = Path.Combine(Directory.GetCurrentDirectory(), FolderName);
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
