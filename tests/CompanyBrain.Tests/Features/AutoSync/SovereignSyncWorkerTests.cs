using CompanyBrain.Dashboard.Features.AutoSync.Models;
using CompanyBrain.Dashboard.Features.AutoSync.Providers;
using CompanyBrain.Dashboard.Features.AutoSync.Services;
using CompanyBrain.Dashboard.Services.Audit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CompanyBrain.Tests.Features.AutoSync;

public sealed class SovereignSyncWorkerTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static SyncSchedule MakeDueSchedule(int id = 1, SourceType sourceType = SourceType.WebWiki) =>
        new()
        {
            Id = id,
            SourceUrl = $"https://example.com/wiki/{id}",
            SourceType = sourceType,
            CronExpression = "* * * * *",   // every minute → always due
            LastSyncUtc = DateTime.UtcNow.AddHours(-2),
            IsActive = true,
        };

    private static SyncSchedule MakeScheduleInBackoff(int id = 2) =>
        new()
        {
            Id = id,
            SourceUrl = "https://example.com/wiki/backoff",
            SourceType = SourceType.WebWiki,
            CronExpression = "* * * * *",
            LastSyncUtc = DateTime.UtcNow.AddHours(-2),
            NextRetryUtc = DateTime.UtcNow.AddHours(4), // still in back-off window
            ConsecutiveFailureCount = 3,
            IsActive = true,
        };

    private static IServiceScopeFactory BuildNullScopeFactory()
    {
        var audit = Substitute.For<IAuditService>();
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.GetService(typeof(IAuditService)).Returns(audit);
        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);
        return factory;
    }

    private static (SovereignSyncWorker Worker, IScheduleRepository Repo, IngestionProviderFactory Factory)
        BuildWorker(
            IReadOnlyList<SyncSchedule> schedules,
            IngestionResult providerResult)
    {
        var repo = Substitute.For<IScheduleRepository>();

        repo.GetActiveSchedulesAsync(Arg.Any<CancellationToken>())
            .Returns(schedules);

        var provider = Substitute.For<IIngestionProvider>();
        provider.SourceType.Returns(SourceType.WebWiki);
        provider.SyncAsync(Arg.Any<SyncSchedule>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(providerResult));

        var factory = new IngestionProviderFactory([provider]);

        var worker = new SovereignSyncWorker(
            repo,
            factory,
            BuildNullScopeFactory(),
            NullLogger<SovereignSyncWorker>.Instance);

        return (worker, repo, factory);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TriggerImmediate_DueScheduleSucceeds_CallsUpdateAfterSuccess()
    {
        var schedule = MakeDueSchedule();
        var (worker, repo, _) = BuildWorker([schedule], IngestionResult.Succeeded("abc123"));

        await worker.TriggerImmediateAsync();

        await repo.Received(1)
            .UpdateAfterSuccessAsync(schedule.Id, "abc123", Arg.Any<CancellationToken>());
        await repo.DidNotReceive()
            .UpdateAfterFailureAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerImmediate_DueScheduleFails_CallsUpdateAfterFailure()
    {
        var schedule = MakeDueSchedule();
        var (worker, repo, _) = BuildWorker([schedule], IngestionResult.Failure("HTTP 401 Unauthorized"));

        await worker.TriggerImmediateAsync();

        await repo.Received(1)
            .UpdateAfterFailureAsync(schedule.Id, "HTTP 401 Unauthorized", Arg.Any<CancellationToken>());
        await repo.DidNotReceive()
            .UpdateAfterSuccessAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerImmediate_UnchangedContent_CallsUpdateAfterSuccess()
    {
        var schedule = MakeDueSchedule();
        var (worker, repo, _) = BuildWorker([schedule], IngestionResult.Unchanged("hash_same"));

        await worker.TriggerImmediateAsync();

        // Unchanged still counts as success (LastSyncUtc is updated, hash preserved)
        await repo.Received(1)
            .UpdateAfterSuccessAsync(schedule.Id, "hash_same", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerImmediate_ScheduleInBackoff_IsSkipped()
    {
        var backoffSchedule = MakeScheduleInBackoff();
        var (worker, repo, _) = BuildWorker([backoffSchedule], IngestionResult.Succeeded(null));

        await worker.TriggerImmediateAsync();

        // Provider should NOT have been called and no DB updates should have happened
        await repo.DidNotReceive()
            .UpdateAfterSuccessAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await repo.DidNotReceive()
            .UpdateAfterFailureAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerImmediate_NoActiveSchedules_DoesNotThrow()
    {
        var (worker, repo, _) = BuildWorker([], IngestionResult.Succeeded(null));

        Func<Task> act = () => worker.TriggerImmediateAsync();
        await act.Should().NotThrowAsync();

        await repo.DidNotReceive()
            .UpdateAfterSuccessAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerImmediate_NoProviderForSourceType_RecordsFailure()
    {
        // Schedule is for Notion, but factory has no Notion provider
        var schedule = MakeDueSchedule(sourceType: SourceType.Notion);

        var repo = Substitute.For<IScheduleRepository>();
        repo.GetActiveSchedulesAsync(Arg.Any<CancellationToken>()).Returns([schedule]);

        var emptyFactory = new IngestionProviderFactory([]);
        var worker = new SovereignSyncWorker(repo, emptyFactory, BuildNullScopeFactory(), NullLogger<SovereignSyncWorker>.Instance);

        await worker.TriggerImmediateAsync();

        await repo.Received(1)
            .UpdateAfterFailureAsync(
                schedule.Id,
                Arg.Is<string>(msg => msg.Contains("Notion")),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerImmediate_MultipleDueSchedules_AllProcessed()
    {
        var s1 = MakeDueSchedule(id: 1);
        var s2 = MakeDueSchedule(id: 2);
        var (worker, repo, _) = BuildWorker([s1, s2], IngestionResult.Succeeded("hash"));

        await worker.TriggerImmediateAsync();

        await repo.Received(1).UpdateAfterSuccessAsync(1, "hash", Arg.Any<CancellationToken>());
        await repo.Received(1).UpdateAfterSuccessAsync(2, "hash", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckInterval_Is2Minutes()
    {
        SovereignSyncWorker.CheckInterval.Should().Be(TimeSpan.FromMinutes(2));
    }
}
