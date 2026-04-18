namespace CompanyBrain.Dashboard.Features.Confluence.Data;

public sealed class ConfluenceCredentials
{
    public int Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public byte[] EncryptedApiToken { get; set; } = [];
    public byte[] EncryptionNonce { get; set; } = [];
    public byte[] AuthTag { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
