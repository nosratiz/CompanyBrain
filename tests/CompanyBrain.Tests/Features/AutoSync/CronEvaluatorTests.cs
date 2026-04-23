using CompanyBrain.Dashboard.Features.AutoSync.Services;
using FluentAssertions;

namespace CompanyBrain.Tests.Features.AutoSync;

public sealed class CronEvaluatorTests
{
    // ── IsDue ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsDue_WhenNeverSynced_ReturnsTrue()
    {
        // Any valid expression should be due when lastSyncUtc is null
        CronEvaluator.IsDue("0 2 * * *", lastSyncUtc: null).Should().BeTrue();
    }

    [Fact]
    public void IsDue_WhenNextOccurrenceIsInThePast_ReturnsTrue()
    {
        // Daily at 02:00 UTC; last sync 25 hours ago → next occurrence is in the past
        var lastSync = DateTime.UtcNow.AddHours(-25);
        CronEvaluator.IsDue("0 2 * * *", lastSync).Should().BeTrue();
    }

    [Fact]
    public void IsDue_WhenSyncedJustNow_ReturnsFalse()
    {
        // Hourly cron; last sync 1 minute ago → next occurrence is 59 minutes away
        var lastSync = DateTime.UtcNow.AddMinutes(-1);
        CronEvaluator.IsDue("0 * * * *", lastSync).Should().BeFalse();
    }

    [Fact]
    public void IsDue_WeeklyExpression_ReturnsFalse_WhenSyncedYesterday()
    {
        // Every Monday at midnight; last sync 1 day ago → not yet due (7-day period)
        var lastSync = DateTime.UtcNow.AddDays(-1);
        CronEvaluator.IsDue("0 0 * * 1", lastSync).Should().BeFalse();
    }

    [Fact]
    public void IsDue_WeeklyExpression_ReturnsTrue_WhenSyncedEightDaysAgo()
    {
        // Every Monday at midnight; last sync 8 days ago → definitely overdue
        var lastSync = DateTime.UtcNow.AddDays(-8);
        CronEvaluator.IsDue("0 0 * * 1", lastSync).Should().BeTrue();
    }

    // ── 6-field (with seconds) ────────────────────────────────────────────────

    [Fact]
    public void IsDue_SixFieldExpression_Accepted()
    {
        // Every 30 seconds: "*/30 * * * * *"; last sync 1 minute ago → overdue
        var lastSync = DateTime.UtcNow.AddMinutes(-1);
        CronEvaluator.IsDue("*/30 * * * * *", lastSync).Should().BeTrue();
    }

    // ── GetNextOccurrence ─────────────────────────────────────────────────────

    [Fact]
    public void GetNextOccurrence_ReturnsExpectedUtcTime()
    {
        // Daily at midnight; from a known point in time
        var from = new DateTime(2026, 1, 1, 23, 0, 0, DateTimeKind.Utc);
        var next = CronEvaluator.GetNextOccurrence("0 0 * * *", from);

        next.Should().NotBeNull();
        next!.Value.Should().Be(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetNextOccurrence_EveryMinute_AdvancesByOneMinute()
    {
        var from = new DateTime(2026, 6, 1, 12, 30, 0, DateTimeKind.Utc);
        var next = CronEvaluator.GetNextOccurrence("* * * * *", from);

        next.Should().NotBeNull();
        next!.Value.Should().Be(new DateTime(2026, 6, 1, 12, 31, 0, DateTimeKind.Utc));
    }

    // ── IsValid ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0 2 * * *")]         // daily 02:00
    [InlineData("*/5 * * * *")]       // every 5 minutes
    [InlineData("0 0 * * 1")]         // every Monday midnight
    [InlineData("0 9 * * 1-5")]       // weekdays at 09:00
    [InlineData("0 0 1 * *")]         // monthly on the 1st
    [InlineData("0 */6 * * *")]       // every 6 hours
    [InlineData("0 0 0 * * *")]       // 6-field: daily at midnight
    public void IsValid_WellFormedExpression_ReturnsTrue(string expression)
    {
        CronEvaluator.IsValid(expression).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a cron")]
    [InlineData("99 99 99 99 99")]
    public void IsValid_BadExpression_ReturnsFalse(string? expression)
    {
        CronEvaluator.IsValid(expression).Should().BeFalse();
    }
}
