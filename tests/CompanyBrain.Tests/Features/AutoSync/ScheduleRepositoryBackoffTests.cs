using CompanyBrain.Dashboard.Features.AutoSync.Services;
using FluentAssertions;

namespace CompanyBrain.Tests.Features.AutoSync;

public sealed class ScheduleRepositoryBackoffTests
{
    [Theory]
    [InlineData(1, 5)]      // first failure → 5-minute back-off
    [InlineData(2, 15)]     // second → 15 min
    [InlineData(3, 60)]     // third → 1 h
    [InlineData(4, 240)]    // fourth → 4 h
    [InlineData(5, 1440)]   // fifth+ → 24 h
    [InlineData(10, 1440)]  // large count capped at 24 h
    public void ComputeNextRetryUtc_BackoffMatchesExpectedMinutes(int failureCount, int expectedMinutes)
    {
        var before = DateTime.UtcNow;
        var retryUtc = ScheduleRepository.ComputeNextRetryUtc(failureCount);
        var after = DateTime.UtcNow;

        var minExpected = before.AddMinutes(expectedMinutes - 1);
        var maxExpected = after.AddMinutes(expectedMinutes + 1);

        retryUtc.Should().BeAfter(minExpected).And.BeBefore(maxExpected);
    }

    [Fact]
    public void ComputeNextRetryUtc_AlwaysReturnsUtcKind()
    {
        var result = ScheduleRepository.ComputeNextRetryUtc(1);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }
}
