namespace CompanyBrain.Dashboard.Data.Models;

/// <summary>
/// Represents an assignment of a document to a tenant.
/// Stored locally in SQLite to track which documents are accessible to which tenants.
/// </summary>
public sealed class DocumentTenantAssignment
{
    public int Id { get; set; }
    
    /// <summary>
    /// The file name of the document (e.g., "company-policies.md").
    /// </summary>
    public required string FileName { get; set; }
    
    /// <summary>
    /// The unique identifier of the tenant from the external tenant API.
    /// </summary>
    public required Guid TenantId { get; set; }
    
    /// <summary>
    /// The display name of the tenant (cached locally for convenience).
    /// </summary>
    public required string TenantName { get; set; }
    
    /// <summary>
    /// When this assignment was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this assignment was last updated.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; set; }
}
