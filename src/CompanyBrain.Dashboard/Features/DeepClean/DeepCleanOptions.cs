using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Features.DeepClean;

/// <summary>
/// Configuration options for the DeepClean background service.
/// Bound from appsettings.json section "DeepClean".
/// </summary>
public sealed class DeepCleanOptions
{
    public const string SectionName = "DeepClean";

    /// <summary>
    /// Maximum allowed index directory size in megabytes. Default: 500 MB.
    /// </summary>
    public long QuotaMb { get; set; } = 500;

    /// <summary>
    /// When quota is exceeded, purge LRU records until total size falls
    /// below this percentage of <see cref="QuotaMb"/>. Default: 0.80 (80%).
    /// </summary>
    public double PurgeTargetRatio { get; set; } = 0.80;

    /// <summary>
    /// Interval between full maintenance cycles. Default: 24 hours.
    /// </summary>
    public TimeSpan CycleInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Delay before the first cycle runs after startup. Default: 2 minutes.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Number of secure-overwrite passes for temp fragment erasure.
    /// 1 = zero-fill only, 3 = DoD-style (zero, ones, random). Default: 1.
    /// </summary>
    public int SecureDeletePasses { get; set; } = 1;

    /// <summary>
    /// Maximum retry attempts when a file is locked during secure deletion.
    /// </summary>
    public int FileRetryCount { get; set; } = 3;

    /// <summary>
    /// Delay between file-lock retries.
    /// </summary>
    public TimeSpan FileRetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}

/// <summary>
/// Source-generated JSON serializer context for <see cref="DeepCleanOptions"/>.
/// Native AOT safe — no reflection.
/// </summary>
[JsonSerializable(typeof(DeepCleanOptions))]
internal sealed partial class DeepCleanOptionsJsonContext : JsonSerializerContext;
