namespace CompanyBrain.Dashboard.Api.Contracts;

/// <summary>
/// Request to discover and ingest all wiki documents from a base URL.
/// </summary>
/// <param name="Url">The base wiki URL to discover links from.</param>
/// <param name="LinkSelector">Optional CSS selector or XPath to find wiki links. Defaults to discovering all internal links.</param>
internal sealed record IngestWikiBatchRequest(string Url, string? LinkSelector = null);
