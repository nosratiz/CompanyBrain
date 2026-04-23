using System.Text.RegularExpressions;
using CompanyBrain.Dashboard.Helpers;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Services;

/// <summary>
/// Mandatory post-processing filter applied to every AI answer before it is posted
/// to Slack or Teams.
///
/// <para>
/// Layer 1 — delegates to <see cref="SecurityHelpers.RedactPii"/> for the standard
/// patterns (emails, API keys, IP addresses, GitHub tokens, Slack tokens, AWS keys).
/// </para>
/// <para>
/// Layer 2 — applies chat-relay-specific patterns that are out-of-scope for the
/// general PII helper:
/// <list type="bullet">
///   <item><description>
///     Windows-style internal hostnames: e.g. <c>PRODDB01</c>, <c>APP-SERVER-02</c>
///     (ALL-CAPS word with trailing digits or hyphen-separated ALL-CAPS segments).
///   </description></item>
///   <item><description>
///     Internal FQDNs: e.g. <c>db.corp</c>, <c>fileserver.local</c>,
///     <c>intranet.internal</c>.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// NOTE: Person-name detection via regex produces too many false positives on
/// normal prose.  NLP-based detection (e.g. via a NER model) is the recommended
/// approach and can be plugged in by overriding <see cref="RedactPersonNames"/>.
/// </para>
/// </summary>
public partial class SovereignPostProcessor
{
    private const string ServerNameReplacement = "[SERVER_REDACTED]";
    private const string InternalFqdnReplacement = "[INTERNAL_HOST_REDACTED]";

    /// <summary>
    /// Applies all redaction layers and returns the sanitized output.
    /// </summary>
    public string Process(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Layer 1: existing PII patterns (emails, IPs, API keys, tokens, AWS)
        var result = SecurityHelpers.RedactPii(text);

        // Layer 2: Windows server hostnames  — at least 3 ALL-CAPS chars ending with 2+ digits
        //          e.g.  WEBPROD01  APPDB001  SQLNODE42
        result = ServerHostnameRegex().Replace(result, ServerNameReplacement);

        // Layer 3: hyphen-separated ALL-CAPS server names (APP-SERVER-01, DC-EAST-02)
        result = HyphenServerRegex().Replace(result, ServerNameReplacement);

        // Layer 4: internal-TLD FQDNs  — anything ending in .local / .internal / .corp / .lan / .intranet / .ad
        result = InternalFqdnRegex().Replace(result, InternalFqdnReplacement);

        return result;
    }

    /// <summary>Extension point for tests to inject NLP-based person-name redaction.</summary>
    protected virtual string RedactPersonNames(string text) => text;

    // ── Source-generated regexes ────────────────────────────────────────────────

    /// <summary>ALL-CAPS word of 3+ chars followed directly by 2+ digits: WEBPROD01, SQLNODE42.</summary>
    [GeneratedRegex(@"\b[A-Z]{3,}[0-9]{2,}\b", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex ServerHostnameRegex();

    /// <summary>Two or more ALL-CAPS segments separated by hyphens: APP-SERVER, DC-EAST-02.</summary>
    [GeneratedRegex(@"\b[A-Z]{2,}(?:-[A-Z0-9]{2,}){1,4}\b", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex HyphenServerRegex();

    /// <summary>Any hostname with an internal TLD: server.local, db.corp, host.internal.</summary>
    [GeneratedRegex(
        @"\b(?:[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?\.)+(?:local|internal|corp|lan|intranet|ad|priv)\b",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 500)]
    private static partial Regex InternalFqdnRegex();
}
