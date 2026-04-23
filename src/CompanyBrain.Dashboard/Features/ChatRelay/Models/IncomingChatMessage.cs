namespace CompanyBrain.Dashboard.Features.ChatRelay.Models;

/// <summary>
/// Normalised representation of an incoming chat message after platform-specific
/// parsing.  Used as the single input type for <see cref="ChatRelayService"/>.
/// </summary>
public sealed record IncomingChatMessage(
    ChatPlatform Platform,

    /// <summary>Platform user ID (Slack U-ID or Teams AAD object ID).</summary>
    string UserId,

    /// <summary>Slack channel ID or Teams channel ID.</summary>
    string ChannelId,

    /// <summary>
    /// Slack: <c>thread_ts</c> of the root message (equals <c>ts</c> for top-level messages).
    /// Teams: <c>conversation.id</c>.
    /// </summary>
    string ThreadId,

    /// <summary>The user's query text, with bot-mention markup stripped.</summary>
    string Text,

    /// <summary>
    /// Teams: <c>serviceUrl</c> from the Activity (needed to post the reply).
    /// Slack: empty — Slack replies use the Bot Token + channel + thread_ts.
    /// </summary>
    string ServiceUrl);
