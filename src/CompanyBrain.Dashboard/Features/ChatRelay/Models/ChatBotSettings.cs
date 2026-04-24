namespace CompanyBrain.Dashboard.Features.ChatRelay.Models;

/// <summary>
/// Singleton entity that stores all chat-platform integration credentials.
/// Bot tokens and secrets are stored encrypted with ASP.NET Data Protection
/// — see <see cref="ChatRelaySettingsService"/> for encrypt/decrypt round-trips.
/// </summary>
public sealed class ChatBotSettings
{
    /// <summary>Fixed singleton GUID — only ever one row per installation.</summary>
    public Guid Id { get; set; } = ChatBotSettingsConstants.SingletonId;

    // ── Slack ─────────────────────────────────────────────────────────────────

    /// <summary>When true, the Slack webhook endpoint is active.</summary>
    public bool SlackEnabled { get; set; }

    /// <summary>
    /// Encrypted <c>xoxb-…</c> Bot User OAuth Token used for posting messages.
    /// Empty string means not yet configured.
    /// </summary>
    public string EncryptedSlackBotToken { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted Slack Signing Secret used to verify <c>X-Slack-Signature</c> on incoming webhooks.
    /// </summary>
    public string EncryptedSlackSigningSecret { get; set; } = string.Empty;

    // ── Teams ─────────────────────────────────────────────────────────────────

    /// <summary>When true, the Teams webhook endpoint is active.</summary>
    public bool TeamsEnabled { get; set; }

    /// <summary>Microsoft App (Client) ID registered in Azure AD for the bot.</summary>
    public string TeamsAppId { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted Microsoft App Password (client secret) used to obtain Bot Framework tokens.
    /// </summary>
    public string EncryptedTeamsAppPassword { get; set; } = string.Empty;

    // ── Tunnel ────────────────────────────────────────────────────────────────

    /// <summary>When true, <see cref="DevTunnelService"/> will spin up a secure inbound tunnel.</summary>
    public bool TunnelEnabled { get; set; }

    /// <summary>
    /// Persistent devtunnel ID created on first startup (e.g. "abc123def").
    /// When set, <see cref="DevTunnelService"/> re-hosts the same tunnel so the
    /// public URL stays stable across restarts and the Slack/Teams webhook URL
    /// never needs to be updated.
    /// </summary>
    public string DevTunnelId { get; set; } = string.Empty;

    /// <summary>Last known public tunnel URL (runtime-populated, persisted for display).</summary>
    public string TunnelUrl { get; set; } = string.Empty;

    /// <summary>Timestamp of the last settings change.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Fixed constants for the singleton <see cref="ChatBotSettings"/> row.</summary>
public static class ChatBotSettingsConstants
{
    public static readonly Guid SingletonId = new("b2c3d4e5-f6a7-4890-9bcd-ef1234567890");
}
