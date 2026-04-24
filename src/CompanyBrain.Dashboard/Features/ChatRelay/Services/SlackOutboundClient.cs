using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CompanyBrain.Dashboard.Features.ChatRelay.Contracts;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Services;

/// <summary>
/// Sends replies to Slack using the <c>chat.postMessage</c> Web API.
/// Each reply is posted into the originating thread so the main channel stays clean.
/// </summary>
public sealed class SlackOutboundClient(
    IHttpClientFactory httpClientFactory,
    ILogger<SlackOutboundClient> logger)
{
    private const string SlackApiBase = "https://slack.com/api/";

    /// <summary>
    /// Posts <paramref name="text"/> as a threaded reply in Slack.
    /// </summary>
    /// <param name="botToken">Decrypted <c>xoxb-…</c> bot token.</param>
    /// <param name="channel">Slack channel ID, e.g. <c>C1234567890</c>.</param>
    /// <param name="threadTs">
    /// The <c>ts</c> of the root message.  Passing this makes Slack attach the reply
    /// to the thread instead of posting a new top-level message.
    /// </param>
    /// <param name="text">PII-redacted answer text.</param>
    public async Task<bool> PostThreadedReplyAsync(
        string botToken,
        string channel,
        string threadTs,
        string text,
        CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("slack");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", botToken);

        var payload = new SlackPostMessageRequest
        {
            Channel = channel,
            ThreadTs = threadTs,
            Text = text,
        };

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"{SlackApiBase}chat.postMessage",
                payload,
                ChatRelayJsonContext.Default.SlackPostMessageRequest,
                ct);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync(
                ChatRelayJsonContext.Default.SlackPostMessageResponse,
                ct);

            if (body?.Ok == true)
            {
                logger.LogDebug("Slack reply posted to channel {Channel} thread {Thread}", channel, threadTs);
                return true;
            }

            logger.LogWarning(
                "Slack chat.postMessage returned ok=false: {Error}", body?.Error);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to post Slack reply to channel {Channel}", channel);
            return false;
        }
    }
}
