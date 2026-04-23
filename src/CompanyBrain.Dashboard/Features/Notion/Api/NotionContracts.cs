using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Features.Notion.Api;

// ── Search ──────────────────────────────────────────────────────────────────

public sealed record NotionSearchResult
{
    [JsonPropertyName("results")]
    public List<NotionPageObject> Results { get; init; } = [];

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; init; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }
}

public sealed record NotionPageObject
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; init; } = string.Empty;

    [JsonPropertyName("last_edited_time")]
    public DateTime LastEditedTime { get; init; }

    [JsonPropertyName("parent")]
    public NotionParent? Parent { get; init; }

    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertyValue> Properties { get; init; } = [];

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>Extracts the plain-text title from the page properties.</summary>
    public string GetTitle()
    {
        if (Properties.TryGetValue("title", out var prop) && prop.Title?.Count > 0)
            return string.Concat(prop.Title.Select(t => t.PlainText));

        if (Properties.TryGetValue("Name", out var nameProp) && nameProp.Title?.Count > 0)
            return string.Concat(nameProp.Title.Select(t => t.PlainText));

        // Fallback: use the page ID
        return Id;
    }
}

public sealed record NotionParent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("page_id")]
    public string? PageId { get; init; }

    [JsonPropertyName("database_id")]
    public string? DatabaseId { get; init; }
}

public sealed record NotionPropertyValue
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public List<NotionRichText>? Title { get; init; }
}

public sealed record NotionRichText
{
    [JsonPropertyName("plain_text")]
    public string PlainText { get; init; } = string.Empty;
}

// ── Block children ──────────────────────────────────────────────────────────

public sealed record NotionBlockChildrenResult
{
    [JsonPropertyName("results")]
    public List<NotionBlock> Results { get; init; } = [];

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; init; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }
}

public sealed record NotionBlock
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("has_children")]
    public bool HasChildren { get; init; }

    [JsonPropertyName("paragraph")]
    public NotionParagraph? Paragraph { get; init; }

    [JsonPropertyName("heading_1")]
    public NotionHeading? Heading1 { get; init; }

    [JsonPropertyName("heading_2")]
    public NotionHeading? Heading2 { get; init; }

    [JsonPropertyName("heading_3")]
    public NotionHeading? Heading3 { get; init; }

    [JsonPropertyName("bulleted_list_item")]
    public NotionListItem? BulletedListItem { get; init; }

    [JsonPropertyName("numbered_list_item")]
    public NotionListItem? NumberedListItem { get; init; }

    [JsonPropertyName("code")]
    public NotionCode? Code { get; init; }

    [JsonPropertyName("quote")]
    public NotionQuote? Quote { get; init; }

    [JsonPropertyName("callout")]
    public NotionCallout? Callout { get; init; }

    [JsonPropertyName("table")]
    public NotionTable? Table { get; init; }

    [JsonPropertyName("table_row")]
    public NotionTableRow? TableRow { get; init; }

    [JsonPropertyName("child_page")]
    public NotionChildPage? ChildPage { get; init; }
}

public sealed record NotionParagraph
{
    [JsonPropertyName("rich_text")]
    public List<NotionRichText> RichText { get; init; } = [];
}

public sealed record NotionHeading
{
    [JsonPropertyName("rich_text")]
    public List<NotionRichText> RichText { get; init; } = [];
}

public sealed record NotionListItem
{
    [JsonPropertyName("rich_text")]
    public List<NotionRichText> RichText { get; init; } = [];
}

public sealed record NotionCode
{
    [JsonPropertyName("rich_text")]
    public List<NotionRichText> RichText { get; init; } = [];

    [JsonPropertyName("language")]
    public string Language { get; init; } = string.Empty;
}

public sealed record NotionQuote
{
    [JsonPropertyName("rich_text")]
    public List<NotionRichText> RichText { get; init; } = [];
}

public sealed record NotionCallout
{
    [JsonPropertyName("rich_text")]
    public List<NotionRichText> RichText { get; init; } = [];
}

public sealed record NotionTable
{
    [JsonPropertyName("table_width")]
    public int TableWidth { get; init; }
}

public sealed record NotionTableRow
{
    [JsonPropertyName("cells")]
    public List<List<NotionRichText>> Cells { get; init; } = [];
}

public sealed record NotionChildPage
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
}

// ── Users ───────────────────────────────────────────────────────────────────

public sealed record NotionUserResult
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
