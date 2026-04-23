namespace CompanyBrain.Dashboard.Features.AutoSync.Models;

/// <summary>
/// Identifies the platform from which content will be ingested.
/// </summary>
public enum SourceType
{
    /// <summary>Generic HTML page or wiki (e.g. internal Confluence/Notion exported page, any HTTP URL).</summary>
    WebWiki = 0,

    /// <summary>Microsoft SharePoint (synced via Graph API delta queries).</summary>
    SharePoint = 1,

    /// <summary>Atlassian Confluence (synced via REST API).</summary>
    Confluence = 2,

    /// <summary>GitHub Wiki (standard HTML wiki pages under /wiki).</summary>
    GitHub = 3,

    /// <summary>Notion (synced via the official Notion API — requires integration token).</summary>
    Notion = 4,
}
