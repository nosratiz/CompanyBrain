namespace CompanyBrain.Dashboard.Features.SharePoint.Data;

/// <summary>
/// Stores encrypted OAuth tokens for SharePoint access.
/// </summary>
public sealed class SharePointAuthToken
{
    public int Id { get; set; }

    /// <summary>
    /// Azure AD Tenant ID this token belongs to.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// User Principal Name (email) of the authenticated user.
    /// </summary>
    public required string UserPrincipalName { get; set; }

    /// <summary>
    /// AES-GCM encrypted refresh token.
    /// </summary>
    public required byte[] EncryptedRefreshToken { get; set; }

    /// <summary>
    /// Encryption IV/Nonce.
    /// </summary>
    public required byte[] EncryptionNonce { get; set; }

    /// <summary>
    /// Authentication tag for AES-GCM.
    /// </summary>
    public required byte[] AuthTag { get; set; }

    /// <summary>
    /// When this token was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this token was last refreshed.
    /// </summary>
    public DateTimeOffset? LastRefreshedAtUtc { get; set; }

    /// <summary>
    /// Whether this token is still valid.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
