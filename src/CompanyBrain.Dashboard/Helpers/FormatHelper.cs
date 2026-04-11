namespace CompanyBrain.Dashboard.Helpers;

/// <summary>
/// Static helper class for common formatting operations.
/// </summary>
public static class FormatHelper
{
    private static readonly string[] ByteSizes = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// Formats a byte count into a human-readable string (e.g., "1.5 MB").
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
            return "0 B";

        var order = 0;
        var size = (double)bytes;

        while (size >= 1024 && order < ByteSizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return order == 0
            ? $"{size:0} {ByteSizes[order]}"
            : $"{size:0.##} {ByteSizes[order]}";
    }

    /// <summary>
    /// Truncates a string to a maximum length, appending "..." if truncated.
    /// </summary>
    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Length <= maxLength)
            return value;

        return maxLength <= 3
            ? value[..maxLength]
            : value[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Strips the "resources/" prefix from a resource name if present.
    /// </summary>
    public static string StripResourcesPrefix(string name)
    {
        const string prefix = "resources/";
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? name[prefix.Length..]
            : name;
    }
}
