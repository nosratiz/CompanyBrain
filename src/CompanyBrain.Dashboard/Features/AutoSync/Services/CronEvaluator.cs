using Cronos;

namespace CompanyBrain.Dashboard.Features.AutoSync.Services;

/// <summary>
/// Thin wrapper around the Cronos library for evaluating cron expressions
/// against UTC timestamps. Supports both 5-field (standard) and 6-field
/// (with seconds) expressions.
/// </summary>
public static class CronEvaluator
{
    /// <summary>
    /// Returns <see langword="true"/> when the schedule described by
    /// <paramref name="cronExpression"/> is due for execution.
    ///
    /// <para>
    /// A schedule is due when the next occurrence calculated from
    /// <paramref name="lastSyncUtc"/> falls on or before <see cref="DateTime.UtcNow"/>.
    /// If <paramref name="lastSyncUtc"/> is <see langword="null"/> the schedule has
    /// never run and is always considered due.
    /// </para>
    /// </summary>
    /// <param name="cronExpression">Standard 5-field or 6-field (with seconds) cron string.</param>
    /// <param name="lastSyncUtc">UTC timestamp of the previous successful run, or null.</param>
    /// <exception cref="CronFormatException">Thrown when the expression cannot be parsed.</exception>
    public static bool IsDue(string cronExpression, DateTime? lastSyncUtc)
    {
        if (lastSyncUtc is null)
            return true; // never synced → always due

        var cron = Parse(cronExpression);
        // EF Core SQLite returns DateTime with Unspecified kind from TEXT columns.
        // Cronos requires DateTimeKind.Utc — normalise here since LastSyncUtc is semantically UTC.
        var fromUtc = DateTime.SpecifyKind(lastSyncUtc.Value, DateTimeKind.Utc);
        var next = cron.GetNextOccurrence(fromUtc, TimeZoneInfo.Utc);
        return next.HasValue && next.Value <= DateTime.UtcNow;
    }

    /// <summary>
    /// Returns the next UTC occurrence after <paramref name="from"/>,
    /// or <see langword="null"/> if the expression never fires again.
    /// </summary>
    /// <param name="cronExpression">Standard 5-field or 6-field cron string.</param>
    /// <param name="from">Start of the search window (inclusive).</param>
    /// <exception cref="CronFormatException">Thrown when the expression cannot be parsed.</exception>
    public static DateTime? GetNextOccurrence(string cronExpression, DateTime from)
    {
        var cron = Parse(cronExpression);
        // Normalise kind — Cronos requires DateTimeKind.Utc.
        var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        return cron.GetNextOccurrence(fromUtc, TimeZoneInfo.Utc);
    }

    /// <summary>
    /// Validates that <paramref name="cronExpression"/> is a parseable cron string.
    /// Returns <see langword="true"/> when valid, <see langword="false"/> otherwise.
    /// </summary>
    public static bool IsValid(string? cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return false;

        try
        {
            Parse(cronExpression);
            return true;
        }
        catch (CronFormatException)
        {
            return false;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a cron expression, automatically detecting whether it uses
    /// 5 fields (standard, no seconds) or 6 fields (with leading seconds field).
    /// </summary>
    private static CronExpression Parse(string expression)
    {
        var trimmed = expression.Trim();

        // Count whitespace-separated fields to choose the right format
        var fieldCount = trimmed.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

        var format = fieldCount >= 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
        return CronExpression.Parse(trimmed, format);
    }
}
