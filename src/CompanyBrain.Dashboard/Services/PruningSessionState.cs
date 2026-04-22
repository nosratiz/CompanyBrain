namespace CompanyBrain.Dashboard.Services;

/// <summary>
/// Retains the last N pruning events so users can audit what happened "under the hood."
/// Thread-safe for concurrent MCP tool calls.
/// </summary>
public sealed class PruningSessionState
{
    private readonly object _lock = new();
    private readonly LinkedList<PruningEvent> _events = new();

    /// <summary>
    /// Maximum number of events retained.
    /// </summary>
    public int Capacity { get; } = 5;

    /// <summary>
    /// Cumulative tokens saved across all recorded events.
    /// </summary>
    public long CumulativeTokensSaved { get; private set; }

    /// <summary>
    /// Total tool calls recorded (including events that have been evicted from the ring buffer).
    /// </summary>
    public int TotalCallsRecorded { get; private set; }

    /// <summary>
    /// Cumulative original tokens across all recorded events.
    /// </summary>
    public long CumulativeOriginalTokens { get; private set; }

    /// <summary>
    /// Cumulative pruned tokens across all recorded events.
    /// </summary>
    public long CumulativePrunedTokens { get; private set; }

    /// <summary>
    /// Returns a snapshot of the current events, most recent first.
    /// </summary>
    public IReadOnlyList<PruningEvent> GetRecentEvents()
    {
        lock (_lock)
        {
            return _events.ToList();
        }
    }

    /// <summary>
    /// Records a new pruning event. Evicts the oldest event if at capacity.
    /// </summary>
    public void Record(PruningEvent pruningEvent)
    {
        lock (_lock)
        {
            _events.AddFirst(pruningEvent);

            if (_events.Count > Capacity)
            {
                _events.RemoveLast();
            }

            CumulativeTokensSaved += pruningEvent.TokensSaved;
            CumulativeOriginalTokens += pruningEvent.OriginalTokens;
            CumulativePrunedTokens += pruningEvent.PrunedTokens;
            TotalCallsRecorded++;
        }
    }

    /// <summary>
    /// Clears all recorded events and resets counters.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
            CumulativeTokensSaved = 0;
            CumulativeOriginalTokens = 0;
            CumulativePrunedTokens = 0;
            TotalCallsRecorded = 0;
        }
    }
}
