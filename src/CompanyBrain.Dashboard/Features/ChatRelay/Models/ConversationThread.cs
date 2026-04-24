namespace CompanyBrain.Dashboard.Features.ChatRelay.Models;

/// <summary>
/// Persists the mapping between an external chat thread (Slack <c>thread_ts</c> or Teams
/// <c>ConversationId</c>) and a local session ID, enabling conversation memory across
/// multiple messages in the same thread.
/// </summary>
public sealed class ConversationThread
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>The originating chat platform.</summary>
    public ChatPlatform Platform { get; set; }

    /// <summary>
    /// Slack: the <c>thread_ts</c> (timestamp of the root message) — also used as the
    /// reply anchor.
    /// Teams: the <c>Activity.Conversation.Id</c>.
    /// </summary>
    public string ExternalThreadId { get; set; } = string.Empty;

    /// <summary>
    /// Slack: the <c>channel</c> ID (<c>C…</c>).
    /// Teams: the <c>channelId</c> field from the Activity.
    /// </summary>
    public string ExternalChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Teams only: base service URL for replying (e.g. <c>https://smba.trafficmanager.net/amer/</c>).
    /// Empty for Slack.
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Opaque local session identifier.  Future builds will attach conversation history
    /// keyed by this ID so the AI can answer follow-up questions in context.
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>False once the user has revoked this thread from the Bot Management UI.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when this thread mapping was first created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent bot activity in this thread.</summary>
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
}
