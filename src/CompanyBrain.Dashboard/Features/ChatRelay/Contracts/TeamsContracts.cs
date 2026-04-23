using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Contracts;

// ── Teams Bot Activity ────────────────────────────────────────────────────────

/// <summary>
/// Minimal representation of a Microsoft Bot Framework Activity object.
/// Only the properties required by the chat relay are mapped here.
/// AOT-safe via source-generated serialization.
/// </summary>
public sealed class TeamsActivity
{
    /// <summary>Activity type, e.g. <c>message</c>, <c>conversationUpdate</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>The channel identifier, e.g. <c>msteams</c>.</summary>
    [JsonPropertyName("channelId")]
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the Teams channel service.  Used to send reply activities.
    /// Example: <c>https://smba.trafficmanager.net/amer/</c>
    /// </summary>
    [JsonPropertyName("serviceUrl")]
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>The entity that sent the message (human or bot).</summary>
    [JsonPropertyName("from")]
    public TeamsChannelAccount? From { get; set; }

    /// <summary>The bot (recipient of the message).</summary>
    [JsonPropertyName("recipient")]
    public TeamsChannelAccount? Recipient { get; set; }

    [JsonPropertyName("conversation")]
    public TeamsConversation? Conversation { get; set; }

    /// <summary>Plain-text message body.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed class TeamsChannelAccount
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Azure AD object ID of the user (available in Teams).</summary>
    [JsonPropertyName("aadObjectId")]
    public string? AadObjectId { get; set; }
}

public sealed class TeamsConversation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("isGroup")]
    public bool IsGroup { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

// ── Bot Framework OAuth token request/response ────────────────────────────────

/// <summary>
/// Response from the Bot Framework token endpoint.
/// <c>POST https://login.microsoftonline.com/botframework.com/oauth2/v2.0/token</c>
/// </summary>
public sealed class BotFrameworkTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;
}

// ── Outbound Teams reply ───────────────────────────────────────────────────────

/// <summary>
/// Activity payload posted to
/// <c>{serviceUrl}/v3/conversations/{conversationId}/activities</c>.
/// </summary>
public sealed class TeamsReplyActivity
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "message";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
