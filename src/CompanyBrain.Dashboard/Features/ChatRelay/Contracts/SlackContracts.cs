using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Contracts;

// ── Slack Verification Challenge ─────────────────────────────────────────────

/// <summary>
/// Payload sent by Slack the first time an Event Subscriptions URL is registered.
/// Must be echoed back as <c>{ "challenge": "…" }</c>.
/// </summary>
public sealed class SlackUrlVerification
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("challenge")]
    public string Challenge { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}

/// <summary>Response body for the Slack URL verification challenge.</summary>
public sealed class SlackChallengeResponse
{
    [JsonPropertyName("challenge")]
    public string Challenge { get; set; } = string.Empty;
}

// ── Slack Event Callback ──────────────────────────────────────────────────────

/// <summary>
/// Outer wrapper for all Slack event callbacks (<c>type == "event_callback"</c>).
/// </summary>
public sealed class SlackEventCallback
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("team_id")]
    public string TeamId { get; set; } = string.Empty;

    [JsonPropertyName("api_app_id")]
    public string ApiAppId { get; set; } = string.Empty;

    [JsonPropertyName("event")]
    public SlackEvent? Event { get; set; }

    [JsonPropertyName("event_id")]
    public string EventId { get; set; } = string.Empty;

    [JsonPropertyName("event_time")]
    public long EventTime { get; set; }
}

/// <summary>Inner event payload for <c>app_mention</c> events.</summary>
public sealed class SlackEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Slack user ID of the person who mentioned the bot.</summary>
    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;

    /// <summary>Raw message text including the <c>&lt;@BOTID&gt;</c> mention markup.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Timestamp of this specific message.</summary>
    [JsonPropertyName("ts")]
    public string Ts { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the root/parent message — present when the mention arrives inside a thread.
    /// If absent, the current message IS the root: use <see cref="Ts"/> as the thread anchor.
    /// </summary>
    [JsonPropertyName("thread_ts")]
    public string? ThreadTs { get; set; }

    /// <summary>Channel where the mention occurred.</summary>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("event_ts")]
    public string EventTs { get; set; } = string.Empty;
}

// ── Slack API: chat.postMessage ────────────────────────────────────────────────

/// <summary>Request body for <c>POST https://slack.com/api/chat.postMessage</c>.</summary>
public sealed class SlackPostMessageRequest
{
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Supplying <c>thread_ts</c> causes Slack to post as a reply inside the thread
    /// rather than as a new top-level message.
    /// </summary>
    [JsonPropertyName("thread_ts")]
    public string ThreadTs { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>Minimal response from <c>chat.postMessage</c>.</summary>
public sealed class SlackPostMessageResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("ts")]
    public string? Ts { get; set; }
}
