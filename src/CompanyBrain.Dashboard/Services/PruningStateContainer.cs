using CompanyBrain.Pruning;

namespace CompanyBrain.Dashboard.Services;

/// <summary>
/// Observable container that bridges MCP pruning events to the Blazor UI.
/// Subscribers receive real-time notifications when pruning state changes.
/// Registered as a singleton so all circuits see the same state.
/// </summary>
public sealed class PruningStateContainer
{
    private readonly PruningSessionState _sessionState;
    private readonly PruningConfiguration _configuration;

    public PruningStateContainer(PruningSessionState sessionState, PruningConfiguration configuration)
    {
        _sessionState = sessionState;
        _configuration = configuration;
    }

    /// <summary>
    /// Raised whenever the pruning state changes (new event recorded, config changed).
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// The current processing phase displayed in the UI.
    /// </summary>
    public ProcessingPhase CurrentPhase { get; private set; } = ProcessingPhase.Idle;

    /// <summary>
    /// Whether a query is currently being processed.
    /// </summary>
    public bool IsProcessing { get; private set; }

    /// <summary>
    /// The currently active query text, if processing.
    /// </summary>
    public string? ActiveQuery { get; private set; }

    /// <summary>
    /// Access to the underlying session state for reading events.
    /// </summary>
    public PruningSessionState Session => _sessionState;

    /// <summary>
    /// Access to the live configuration for the slider/toggle controls.
    /// </summary>
    public PruningConfiguration Configuration => _configuration;

    /// <summary>
    /// Estimated dollars saved based on average LLM pricing ($15 per 1M tokens).
    /// </summary>
    public decimal EstimatedDollarsSaved =>
        _sessionState.CumulativeTokensSaved * 15m / 1_000_000m;

    /// <summary>
    /// Ratio of data kept local (0.0–1.0) for the donut chart.
    /// </summary>
    public double DataKeptLocalRatio =>
        _sessionState.CumulativeOriginalTokens == 0
            ? 0.0
            : (double)_sessionState.CumulativeTokensSaved / _sessionState.CumulativeOriginalTokens;

    /// <summary>
    /// Called by the MCP pipeline when a pruning operation starts.
    /// </summary>
    public void NotifyProcessingStarted(string query)
    {
        ActiveQuery = query;
        IsProcessing = true;
        CurrentPhase = ProcessingPhase.LocalRanking;
        NotifyStateChanged();
    }

    /// <summary>
    /// Advances the processing phase animation.
    /// </summary>
    public void NotifyPhaseChanged(ProcessingPhase phase)
    {
        CurrentPhase = phase;
        NotifyStateChanged();
    }

    /// <summary>
    /// Called when a pruning operation completes and an event is recorded.
    /// </summary>
    public void NotifyEventRecorded(PruningEvent pruningEvent)
    {
        _sessionState.Record(pruningEvent);
        IsProcessing = false;
        CurrentPhase = ProcessingPhase.Complete;
        NotifyStateChanged();
    }

    /// <summary>
    /// Called when the user changes configuration via the UI controls.
    /// </summary>
    public void NotifyConfigurationChanged()
    {
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all session data.
    /// </summary>
    public void Reset()
    {
        _sessionState.Clear();
        IsProcessing = false;
        CurrentPhase = ProcessingPhase.Idle;
        ActiveQuery = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

/// <summary>
/// The processing phases displayed in the progress visualizer.
/// </summary>
public enum ProcessingPhase
{
    Idle,
    LocalRanking,
    PiiMasking,
    BudgetSelection,
    Complete
}
