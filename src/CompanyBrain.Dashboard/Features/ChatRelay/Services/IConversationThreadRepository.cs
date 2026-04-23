using CompanyBrain.Dashboard.Features.ChatRelay.Models;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Services;

/// <summary>
/// Data-access contract for <see cref="ConversationThread"/> persistence.
/// Extracted as an interface so <see cref="ChatRelayService"/> can be unit-tested
/// without a live SQLite database.
/// </summary>
public interface IConversationThreadRepository
{
    /// <summary>
    /// Finds an active thread mapping, or returns <c>null</c> when no mapping exists.
    /// </summary>
    Task<ConversationThread?> FindAsync(
        ChatPlatform platform,
        string externalThreadId,
        CancellationToken ct);

    /// <summary>
    /// Returns the existing mapping if one exists, otherwise creates and persists a new one.
    /// </summary>
    Task<ConversationThread> GetOrCreateAsync(
        ChatPlatform platform,
        string externalThreadId,
        string channelId,
        string serviceUrl,
        CancellationToken ct);

    /// <summary>Refreshes <see cref="ConversationThread.LastActivityUtc"/> to now.</summary>
    Task TouchAsync(int threadId, CancellationToken ct);

    /// <summary>Returns all active thread mappings for the Bot Management UI.</summary>
    Task<IReadOnlyList<ConversationThread>> GetActiveThreadsAsync(CancellationToken ct);

    /// <summary>
    /// Deactivates all threads.  When <paramref name="platform"/> is provided,
    /// only that platform's threads are revoked.
    /// </summary>
    Task RevokeAllAsync(ChatPlatform? platform, CancellationToken ct);

    /// <summary>Deactivates a single thread by its surrogate ID.</summary>
    Task RevokeAsync(int threadId, CancellationToken ct);
}
