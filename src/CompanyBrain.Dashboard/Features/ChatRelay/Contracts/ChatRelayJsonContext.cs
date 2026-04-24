using System.Text.Json.Serialization;
using CompanyBrain.Dashboard.Features.ChatRelay.Contracts;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Contracts;

/// <summary>
/// AOT-compatible JSON serialization context for all ChatRelay webhook payloads.
/// All types used in <see cref="System.Text.Json.JsonSerializer"/> calls within
/// the ChatRelay feature must be registered here.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SlackUrlVerification))]
[JsonSerializable(typeof(SlackChallengeResponse))]
[JsonSerializable(typeof(SlackEventCallback))]
[JsonSerializable(typeof(SlackEvent))]
[JsonSerializable(typeof(SlackPostMessageRequest))]
[JsonSerializable(typeof(SlackPostMessageResponse))]
[JsonSerializable(typeof(TeamsActivity))]
[JsonSerializable(typeof(TeamsChannelAccount))]
[JsonSerializable(typeof(TeamsConversation))]
[JsonSerializable(typeof(BotFrameworkTokenResponse))]
[JsonSerializable(typeof(TeamsReplyActivity))]
public sealed partial class ChatRelayJsonContext : JsonSerializerContext
{
}
